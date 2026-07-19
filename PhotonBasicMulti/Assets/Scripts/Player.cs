using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

public enum AnimState
{
    idle,
    move,
    trace,
    attack,
    skill,
    die,
    move_L,
    move_R,
    move_B,
    count,
    jump,
    dodge
}

public class Player : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Options")]
    [SerializeField] private float speed;
    [SerializeField] private float jumpPower;
    [SerializeField] private PhotonView pv;
    [SerializeField] private NavMeshAgent agent;

    // [삭제] Rigidbody 컴포넌트 변수 제거

    private float velocity;
    private float baseSpeed;
    private float m_MvDelay = 0.0f;
    private float h = 0, v = 0; // 이동 입력 값 저장용 변수

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
    [SerializeField] private bool isAttack;
    [SerializeField] private bool isDead;

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
    bool m_KeepMovingAfterJump; // 원격 플레이어의 점프 후 이동 유지 상태 정보를 수신받아 저장할 변수

    private void Awake()
    {
        PlayerPrefs.SetInt("MaxScore", 112500);

        if (pv.IsMine)
        {
            Camera_Ctrl a_CamCtrl = Camera.main.GetComponent<Camera_Ctrl>();
            if (a_CamCtrl != null)
                a_CamCtrl.InitCamera(this.gameObject);
        }
    }

    private void Start()
    {
        if (pv.IsMine)
            GameManager.Inst.SetPlayer(this);
        m_Animator = this.GetComponent<Animator>();
    }

    void Update()
    {
        if (isDead) return;
        if (GameManager.Inst.Is_Conversating) return;

        if (pv.IsMine) // 자신이 조종하는 캐릭터일 때만 이동 처리
        {
            baseSpeed = speed;

            // [수정] 죽지 않았을 때만 이동 로직을 처리합니다.
            if (!isDead)
            {
                ApplyGravity(); // 수동 중력 적용
                Move();
            }

            UpdateMouseLook(); // 마우스 시선 처리
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

            if(!isJump) // 점프 중이 아닐 때만 이동 애니메이션 상태 변경
                ChangeAnimState(AnimState.move);
        }
        else
        {
            rotation = Vector3.zero;
            rotation_value = Vector3.zero;
            if(!isJump) // 점프 중이 아닐 때만 이동 애니메이션 상태 변경
                ChangeAnimState(AnimState.idle);
        }
    }

    private void CheckJumpInput()
    {
        if (Input.GetKeyDown(KeyCode.Space) && !isJump && !isDodge && !isAttack)
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
        if (Input.GetKeyDown(KeyCode.LeftControl) && rotation != Vector3.zero && !isJump && !isDodge && !isAttack)
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

    // [수정] 모든 이동 방식을 transform.Translate 또는 transform.position 변경으로 전환
    public void Move()
    {
        if (0.0f < m_MvDelay)
        {
            m_MvDelay -= Time.deltaTime;
            return;
        }

        if (isAttack && !isJump && !isDodge)
        {
            // 공격 중에는 수평 이동을 제한하고 Y축(중력/점프)만 반영
            transform.position += new Vector3(0f, yVelocity * Time.deltaTime, 0f);
            return;
        }

        Vector3 moveVector = Vector3.zero;

        if (isDodge && keepMovingAfterDodge)
        {
            moveVector = new Vector3(dodgeMoveDir.x * speed, 0f, dodgeMoveDir.z * speed);
            if (dodgeMoveDir != Vector3.zero)
                transform.LookAt(transform.position + new Vector3(dodgeMoveDir.x, 0f, dodgeMoveDir.z));
        }
        else if (isJump && keepMovingAfterJump)
        {
            moveVector = new Vector3(jumpMoveDir.x * baseSpeed, 0f, jumpMoveDir.z * baseSpeed);
            if (jumpMoveDir != Vector3.zero)
                transform.LookAt(transform.position + new Vector3(jumpMoveDir.x, 0f, jumpMoveDir.z));
        }
        else
        {
            velocity = Input.GetKey(KeyCode.LeftShift) ? baseSpeed * 0.3f : baseSpeed;
            moveVector = new Vector3(rotation.x * velocity, 0f, rotation.z * velocity);
            if (rotation != Vector3.zero)
                transform.LookAt(transform.position + new Vector3(rotation.x, 0f, rotation.z));
        }

        // 수평 이동(X, Z)에 Y축 속도(점프/중력)를 병합하여 좌표 이동
        moveVector.y = yVelocity;
        transform.position += moveVector * Time.deltaTime;
    }

    public void UpdateMouseLook()
    {
        if (Input.GetMouseButton(0) && !isDodge && !isDead && !isJump)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit rayHit;
            if (Physics.Raycast(ray, out rayHit, 100))
            {
                Vector3 nextVec = rayHit.point - transform.position;
                nextVec.y = 0f;
                if (nextVec != Vector3.zero)
                    transform.LookAt(transform.position + nextVec);
            }
        }
    }

    //현재 공격 중인지 확인하는 메서드
    public bool IsAttack()
    {
        return m_CurState == AnimState.attack || m_CurState == AnimState.skill;
    }

    public void AttackOrder()
    {
        if (!pv.IsMine) // IsMine 아니면 공격 조작 금지
            return;

        if (IsAttack() == false)  //공격중이거나 스킬 사용중이 아닐 때만... 
        {
            //공격키를 연타해서 누르면 달리는 애니메이션에 잠깐동안
            //애니메이션 보간 때문에 공격 애니가 끼어드는 문제가 발생한다.
            //<-- 이런 현상에 대한 예외처리
            if ((0.0f != h || 0.0f != v))
                return;

            ChangeAnimState(AnimState.attack);
        }//if(IsAttack() == false)  //공격중이거나 스킬 사용중이 아닐 때만... 
    }//public void AttackOrder()

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

    }//public void ChangeAnimState(AnimState newState,

    //애니메이션 상태 업데이트 메서드

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
            stream.SendNext(keepMovingAfterJump);
            stream.SendNext(isDodge);
        }
        else // 원격 플레이어의 위치 정보 수신
        {
            CurPos = (Vector3)stream.ReceiveNext();
            CurRot = (Quaternion)stream.ReceiveNext();
            m_CurState = (AnimState)stream.ReceiveNext();
            m_IsJump = (bool)stream.ReceiveNext();
            m_KeepMovingAfterJump = (bool)stream.ReceiveNext();

            if (m_IsJump || m_KeepMovingAfterJump)
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
