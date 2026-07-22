using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public class Player : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Options")]
    [SerializeField] private float speed;
    [SerializeField] private float jumpPower;
    [SerializeField] private float MaxHp;

    [Header("Components")]
    [SerializeField] private PhotonView pv;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Text id;
    [SerializeField] private Image ImgHpbar;

    private float velocity;
    private float baseSpeed;
    private float m_MvDelay = 0.0f;
    private float h = 0, v = 0; // 이동 입력 값 저장용 변수
   [SerializeField] private float CurHp;

    //--- Animator 관련 변수
    Animator m_Animator = null;
    AnimState m_PreState = AnimState.idle;
    AnimState m_CurState = AnimState.idle;
    //--- Animator 관련 변수


    [Header("States")]
    [SerializeField] private bool isJump;
    [SerializeField] private bool isDodge;
    [SerializeField] private bool keepMovingAfterDodge;
    [SerializeField] private bool keepMovingAfterJump;
    [SerializeField] private bool isDead;
    [SerializeField] private bool isChat;

    private Vector3 rotation;
    private Vector3 rotation_value;
    private Vector3 dodgeRotation;
    private Vector3 dodgeMoveDir;
    private Vector3 jumpMoveDir;

    // Y축(높이) 이동을 수동으로 제어하기 위한 변수
    private float yVelocity = 0f;
    private float gravity = -9.81f; // 커스텀 중력 값

    bool isFirstUpdate = true;

    Vector3 CurPos = Vector3.zero; // 원격 플레이어의 위치 정보를 수신받아 저장할 변수
    Quaternion CurRot = Quaternion.identity; // 원격 플레이어의 회전 정보를 수신받아 저장할 변수

    bool m_IsJump; // 원격 플레이어의 점프 상태 정보를 수신받아 저장할 변수
    string m_Id = ""; // 원격 플레이어의 ID 정보를 수신받아 저장할 변수
    private float NetHp; // 원격 플레이어의 HP 정보를 수신받아 저장할 변수

    GameObject[] m_EnemyList = null;
    Vector3 m_CacTgVec = Vector3.zero;  //타겟까지의 거리 계산용 변수
    float m_AttackDist = 1.9f;          //주인공의 공격거리


    private void Awake()
    {
        PlayerPrefs.SetInt("MaxScore", 112500);

        if (pv.IsMine)
        {
            Camera_Ctrl a_CamCtrl = Camera.main.GetComponent<Camera_Ctrl>();
            if (a_CamCtrl != null)
            {
                a_CamCtrl.InitCamera(this.gameObject);
                id.text = PhotonNetwork.LocalPlayer.NickName;
            }
        }
    }

    private void Start()
    {
        CurHp = MaxHp;
        baseSpeed = speed;
        if (pv.IsMine)
        {
            GameManager.Inst.SetPlayer(this);
        }
        m_Animator = this.GetComponent<Animator>();
    }

    void Update()
    {
        if (isDead) return;

        if (pv.IsMine) // 자신이 조종하는 캐릭터일 때만 이동 처리
        {
            ApplyGravity(); // 수동 중력 적용
            Move();
            AttackOrder();
            CheckMovementInput(); // 이동 입력 체크
            CheckJumpInput(); // 점프 입력 체크
            CheckDodgeInput(); // 회피 입력 체크
        }
        else // 원격지 아바타 캐릭터들은 위치, 회전, 애니메이션을 따라오게 동기화 처리
        {
            if (10.0f < (transform.position - CurPos).magnitude)
            {
                transform.position = CurPos;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, CurPos, Time.deltaTime * 10.0f);
            }
            ChangeAnimState(m_CurState); // 원격지 아바타들은 여기서 애니메이션 동기화
            transform.rotation = Quaternion.Slerp(transform.rotation, CurRot, Time.deltaTime * 10.0f);
            Remote_TakeDamage();
        }

    }

    private void ApplyGravity()
    {
        if (isJump)
        {
            yVelocity += gravity * Time.deltaTime; // 시간에 따라 아래로 떨어지는 가속도 증가
            // 바닥 충돌 임시 처리 (Y 좌표가 0 이하로 떨어지면 착지)
            if (transform.position.y <= 0f && yVelocity < 0f)
            {
                Vector3 pos = transform.position;
                pos.y = 0f;
                transform.position = pos;

                isJump = false;
                keepMovingAfterJump = false;
                yVelocity = 0f;

                if (agent != null)
                {
                    agent.Warp(transform.position); // 에이전트 위치를 현재 착지한 곳으로 순간 이동시킴
                    agent.updatePosition = true;    // 다시 바닥 고정 기능 활성화
                }
            }
        }
    }

    private void CheckMovementInput()
    {
        // 채팅 중이라면 이동 입력을 무시하고 idle 상태로 돌린 뒤 리턴
        if (GameManager.Inst.Is_Conversating)
        {
            h = 0f;
            v = 0f;
            rotation = Vector3.zero;
            rotation_value = Vector3.zero;

            // 점프나 회피, 공격 중이 아닐 때만 안전하게 idle로 변경
            if (!isJump && !isDodge && !IsAttack())
                ChangeAnimState(AnimState.idle);

            return;
        }

        h = Input.GetAxisRaw("Horizontal");
        v = Input.GetAxisRaw("Vertical");

        Transform cam = Camera.main.transform;
        Vector3 forward = cam.forward;
        Vector3 right = cam.right;

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDir = (forward * v + right * h).normalized;

        if (moveDir != Vector3.zero)
        {
            rotation = moveDir;
            rotation_value = rotation;

            if (isDodge)
                rotation = dodgeRotation;

            if(!isJump && !isDodge) // 점프와 회피 중이 아닐 때만 이동 애니메이션 상태 변경
                ChangeAnimState(AnimState.move);
        }
        else
        {
            rotation = Vector3.zero;
            rotation_value = Vector3.zero;

            if(!isJump && !isDodge && !IsAttack()) // 점프와 회피 중이 아닐 때만 이동 애니메이션 상태 변경
                ChangeAnimState(AnimState.idle);
        }
    }

    private void CheckJumpInput()
    {
        if (Input.GetKeyDown(KeyCode.Space) && !isJump && !isDodge && !IsAttack())
        {
            yVelocity = jumpPower; // 순간적인 위쪽 속도 부여
            isJump = true;
            keepMovingAfterJump = true;
            jumpMoveDir = rotation;

            if (agent != null) // NavMeshAgent가 있다면 점프 중에는 위치 업데이트를 끕니다.
            {
                agent.updatePosition = false;
            }
            ChangeAnimState(AnimState.jump);
        }
    }

    private void CheckDodgeInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftControl) && rotation != Vector3.zero && !isJump && !isDodge && !IsAttack())
        {
            dodgeMoveDir = rotation;
            dodgeRotation = rotation;
            speed *= 2;
            isDodge = true;

            Invoke("DodgeOut", 0.5f);
            ChangeAnimState(AnimState.dodge); // 회피 애니메이션 상태로 변경
        }
    }
    private void DodgeOut()
    {
        speed *= 0.5f;
        isDodge = false;
        rotation = rotation_value;
        keepMovingAfterDodge = true;
    }

    // 움직일 때
    public void Move()
    {
        if (0.0f < m_MvDelay)
        {
            m_MvDelay -= Time.deltaTime;
            return;
        }

        if (IsAttack() && !isJump && !isDodge) // 캐릭터가 점프 또는 회피상태가 아닐 경우 공격적용
        {
            // 공격 중에는 수평 이동을 제한하고 Y축(중력/점프)만 반영
            transform.position += new Vector3(0f, yVelocity * Time.deltaTime, 0f);
            return;
        }

        Vector3 moveVector = Vector3.zero;

        if (isDodge && keepMovingAfterDodge) // 캐릭터가 회피중일 경우, 키보드에서 손을 떼더라도 회피를 시작했던 그 방향으로 강제로 밀어붙임
        {
            moveVector = new Vector3(dodgeMoveDir.x * speed, 0f, dodgeMoveDir.z * speed);
            if (dodgeMoveDir != Vector3.zero)
                transform.LookAt(transform.position + new Vector3(dodgeMoveDir.x, 0f, dodgeMoveDir.z));
        }
        else if (isJump && keepMovingAfterJump) // 점프 도중 키보드를 방향을 바꿔도 방향이 바뀌지 않고 포물선을 그리며 이동
        {
            moveVector = new Vector3(jumpMoveDir.x * baseSpeed, 0f, jumpMoveDir.z * baseSpeed);
            if (jumpMoveDir != Vector3.zero)
                transform.LookAt(transform.position + new Vector3(jumpMoveDir.x, 0f, jumpMoveDir.z));
        }
        else// Shift를 눌렀을 경우 일반 스피드의 30% 속도만큼 감. 아니면 100% 속도 유지
        {
            velocity = Input.GetKey(KeyCode.LeftShift) ? baseSpeed * 0.3f : baseSpeed;
            moveVector = new Vector3(rotation.x * velocity, 0f, rotation.z * velocity);
            if (rotation != Vector3.zero)
                transform.LookAt(transform.position + new Vector3(rotation.x, 0f, rotation.z));
        }


        // 수평 이동(X, Z)에 Y축 속도(점프/중력)를 병합하여 좌표 이동
        moveVector.y = yVelocity;
        transform.position += moveVector * Time.deltaTime;
        //agent.Move(moveVector * Time.deltaTime);
    }

    public void Event_AttHit()
    {
        m_EnemyList = GameObject.FindGameObjectsWithTag("Enemy");
        int iCount = m_EnemyList.Length;
        float fCacLen = 0.0f;
        Vector3 effPos = Vector3.zero;

        //--- 주변 모든 몬스터를 찾아서 데미지를 준다.(범위 공격)
        for (int i = 0; i < iCount; i++)
        {
            m_CacTgVec = m_EnemyList[i].transform.position - transform.position;
            fCacLen = m_CacTgVec.magnitude;
            m_CacTgVec.y = 0.0f;

            //공격각도 안에 있는 경우
            //45도 정도 범위 밖에 있다면 뜻
            if (Vector3.Dot(transform.forward, m_CacTgVec.normalized) < 0.45f)
                continue;

            //공격 거리 밖에 있는 경우
            if (m_AttackDist + 0.1f < fCacLen)
                continue;

            m_EnemyList[i].GetComponent<Monster_Ctrl>().TakeDamage(this.gameObject, 20f);
        }
        //--- 주변 모든 몬스터를 찾아서 데미지를 준다.(범위 공격)

    }//public void Event_AttHit()

    void Event_AttFinish()
    {
        if (!pv.IsMine) // IsMine 아니면 공격 조작 금지
            return;

        //Attack 상태일 때는 Attack상태로 끝나야 한다.
        if (m_CurState != AnimState.attack)
            return;

        ChangeAnimState(AnimState.idle);
        
    }//void Event_AttFinish()

    private void AttackOrder()
    {
        if (!pv.IsMine) // IsMine 아니면 공격 조작 금지
            return;

        if (IsAttack() == false && Input.GetMouseButton(0))  //공격중이거나 스킬 사용중이 아닐 때만... 
        {
            //키보드 컨트롤로 이동 중이고
            //공격키를 연타해서 누르면 달리는 애니메이션에 잠깐동안
            //애니메이션 보간 때문에 공격 애니가 끼어드는 문제가 발생한다.
            //<-- 이런 현상에 대한 예외처리
            if ((0.0f != h || 0.0f != v) )
            {
                return;
            }
            ChangeAnimState(AnimState.attack);

        }//if(IsAttack() == false)  //공격중이거나 스킬 사용중이 아닐 때만... 
    }//public void AttackOrder()

    //현재 공격 중인지 확인하는 메서드
    public bool IsAttack()
    {
        return m_CurState == AnimState.attack || m_CurState == AnimState.skill;
    }

    //현재 회피 중인지 확인하는 메서드
    public bool IsDodge()
    {
        return m_CurState == AnimState.dodge;
    }

    //--- 애니메이션 상태 변경 메서드
    public void ChangeAnimState(AnimState newState,
                                float crossTime = 0.1f, string animName = "") // crossTime은 애니메이션 전환 시간, animName은 애니메이션 이름
    {
        if (m_Animator == null)
            return;

        if (m_PreState == newState)
            return;

        m_Animator.ResetTrigger(m_PreState.ToString());
        //기존에 적용되어 있던 Trigger 변수를 제거

        if (0.0f < crossTime)
        {
            m_Animator.SetTrigger(newState.ToString());
        }
        else
        {
            m_Animator.Play(animName, -1, 0);
            //가운데 -1은 Layer Index, 뒤에 0은 처음부터 다시 시작 플레이 시키겠다는 의미
        }

        m_PreState = newState;
        m_CurState = newState;

    }

    public void TakeDamage(float Damage)
    {
        if (CurHp <= 0.0f)
            return;
        if (pv.IsMine)
        {
            CurHp -= Damage;
            if (CurHp < 0.0f)
                CurHp = 0.0f;
            ImgHpbar.fillAmount = CurHp / MaxHp;
        }

        Vector3 cacPos = this.transform.position;
        cacPos.y += 2.65f;

        if (pv.IsMine)
        {
            if (CurHp <= 0.0f)
            {
                Die();   //사망처리
            }
        }
    }


    private void Die()
    {
        if (pv.IsMine)
        {
            Debug.Log("주인공 사망");
            isDead = true;
        }
    }

    private void Remote_TakeDamage() // 원격지 컴퓨터에서 Hp 동기화 함수
    {
        if (0.0f < CurHp)
        {
            CurHp = NetHp; // 원격 플레이어의 체력 값을 수신 받은 NetHp로 업데이트

            // Image UI항목의 fillAmount을 속성을 조절해 생명 게이지값 조정
            ImgHpbar.fillAmount = CurHp / (float)MaxHp;

            if (CurHp <= 0.0f) // 사망 처리는 한 번 만 호출되기 하기 위함.
            {
                CurHp = 0.0f;
                //Die();   //사망처리
            }
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.InRoom)
            return;

        if (stream.IsWriting) // 로컬 플레이어의 위치 정보 송신
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext((int)m_CurState);
            stream.SendNext(isJump);
            stream.SendNext(id.text);
            stream.SendNext(CurHp);
        }
        else // 원격 플레이어의 위치 정보 수신
        {
            CurPos = (Vector3)stream.ReceiveNext();
            CurRot = (Quaternion)stream.ReceiveNext();
            m_CurState = (AnimState)stream.ReceiveNext();
            m_IsJump = (bool)stream.ReceiveNext();
            m_Id = (string)stream.ReceiveNext();
            NetHp = (float)stream.ReceiveNext();

            id.text = m_Id;

            if (m_IsJump)
            {
                agent.updatePosition = false; // 점프 중에는 NavMeshAgent 위치 업데이트를 끔
            }

            if (isFirstUpdate)
            {
                transform.position = CurPos;
                transform.rotation = CurRot;
                isFirstUpdate = false;
            }
        }
    }
}
