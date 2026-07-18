using Photon.Pun;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Options")]
    [SerializeField] private float speed;
    [SerializeField] private float jumpPower;
    [SerializeField] private PhotonView pv;

    [Header("Components")]
    [SerializeField] private Rigidbody rb;

    private float velocity;
    private float baseSpeed; // 원래 속도 저장용
    private float m_MvDelay = 0.0f;


    [SerializeField] private bool isWalk;
    [SerializeField] private bool isRun;
    [SerializeField] private bool isJump;
    [SerializeField] private bool isDodge;
    [SerializeField] private bool isSwap;
    [SerializeField] private bool keepMovingAfterDodge; // 회피를 시작 후 끝날 때 까지 플래그 유지
    [SerializeField] private bool keepMovingAfterJump; // 점프가 시작 하고 끝날 때 까지 플래그 유지
    [SerializeField] private bool isFireReady; // 근접 공격 준비
    [SerializeField] private bool isAttack;
    [SerializeField] private bool isDead;

    private Vector3 rotation;
    private Vector3 rotation_value; // 행동 후 방향키 변경이 반영되지 않는 버그 수정을 위한 변수
    private Vector3 dodgeRotation;
    private Vector3 dodgeMoveDir; // 회피동작이 끝날 때 까지 이동에 사용될 벡터
    private Vector3 jumpMoveDir; // 점프동작이 끝날 때 까지 이동에 사용될 벡터

    // 처음 데이터를 받았는지 체크하는 변수
    // 주인공이 나갔다 들어 왔을 때
    // 다른 주인공의 위치가 쭉 밀리면서 보정되는 현상 개선하기 위해 필요한 변수
    bool isFirstUpdate = true;

    //--- 위치 정보를 송수신할 때 사용할 변수 선언 및 초기값 설정
    Vector3 CurPos = Vector3.zero; // 원격 플레이어의 위치 정보를 수신받아 저장할 변수
    Quaternion CurRot = Quaternion.identity; // 원격 플레이어의 회전 정보를 수신받아 저장할 변수

    private void Awake()
    {
        PlayerPrefs.SetInt("MaxScore", 112500);

        // 내가 조종하는 캐릭터 일 때만 카메라를 따라오게 하는 코드
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
    }

    // Input 체크는 프레임 누락을 방지하기 위해 Update에서 수행합니다.
    void Update()
    {
        if (isDead) return;

        if(!pv.IsMine) return;

        // 1. 이동 입력 (기본축: Horizontal, Vertical)
        CheckMovementInput();

        // 2. 걷기 입력 (왼쪽 쉬프트)
        CheckWalkInput();

        // 3. 점프 입력 (스페이스바)
        CheckJumpInput();

        // 4. 회피 입력 (왼쪽 컨트롤)
        CheckDodgeInput();
    }

    void FixedUpdate()
    {
        if (pv.IsMine)
        {
            baseSpeed = speed;

            if (!isDead)
                Move();
            else
            {
                rb.linearVelocity = Vector3.zero;
            }

            UpdateMouseLook();
        }
        else // 원격지 아바타 캐릭터 들은 위치,회전,애니메이션을 따라오게 동기화 처리
        {
            if (10.0f < (transform.position - CurPos).magnitude) // 벡터의 길이 값 아바타의 위치 값 - 현재 위치 값
            {
                transform.position = CurPos;
            }
            else
            {
                // 원격 플레이어의 플레이어를 수신 받은 위치까지 부드럽게 이동시킴
                transform.position = Vector3.Lerp(transform.position, CurPos,
                    Time.deltaTime * 10.0f);
            }

            // 원격 플레이어의 
            transform.rotation = Quaternion.Slerp(transform.rotation, CurRot,
                Time.deltaTime * 10.0f);
        }

    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isJump = false;
            keepMovingAfterJump = false;
        }
    }

    // --- 레거시 Input 입력 체크 메서드들 ---

    private void CheckMovementInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 inputDir = new Vector3(h, v, 0f).normalized; // 기존 코드의 Vector2 대용

        if (inputDir != Vector3.zero)
        {
            rotation = inputDir;
            rotation_value = rotation;

            if (isDodge)
                rotation = dodgeRotation;

            isRun = true;
        }
        else
        {
            isRun = false;
            rotation = Vector3.zero;
            rotation_value = Vector3.zero;
        }

        // animator.SetBool("IsRun", isRun);
    }

    private void CheckWalkInput()
    {
        // 왼쪽 쉬프트를 누르고 있을 때
        if (Input.GetKey(KeyCode.LeftShift))
        {
            isWalk = true;
        }
        else
        {
            isWalk = false;
        }
        // animator.SetBool("IsWalk", isWalk);
    }

    private void CheckJumpInput()
    {
        if (Input.GetKeyDown(KeyCode.Space) && !isJump && !isDodge && !isSwap && !isAttack)
        {
            rb.AddForce(Vector3.up * jumpPower, ForceMode.Impulse);
            isJump = true;
            keepMovingAfterJump = true;
            jumpMoveDir = rotation;
        }
    }

    private void CheckDodgeInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftControl) && rotation != Vector3.zero && !isJump && !isDodge && !isSwap && !isFireReady)
        {
            dodgeMoveDir = rotation;
            dodgeRotation = rotation;
            speed *= 2;
            isDodge = true;

            Invoke("DodgeOut", 0.5f);
        }
    }

    // --- 물리 이동 및 시선 처리 (기존 로직 유지) ---

    public void Move()
    {
        if (0.0f < m_MvDelay)
        {
            m_MvDelay -= Time.deltaTime;
            return;
        }

        if ((isFireReady && !isJump && !isDodge))
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        if (isDodge && keepMovingAfterDodge)
        {
            float moveSpeed = baseSpeed * Time.deltaTime;
            rb.linearVelocity = new Vector3(dodgeMoveDir.x * moveSpeed,
                                            rb.linearVelocity.y,
                                            dodgeMoveDir.y * moveSpeed);
            transform.LookAt(transform.position + new Vector3(dodgeMoveDir.x, 0f, dodgeMoveDir.y));
        }
        else if (isJump && keepMovingAfterJump)
        {
            float moveSpeed = baseSpeed * Time.deltaTime;
            rb.linearVelocity = new Vector3(jumpMoveDir.x * moveSpeed,
                                            rb.linearVelocity.y,
                                            jumpMoveDir.y * moveSpeed);
            transform.LookAt(transform.position + new Vector3(jumpMoveDir.x, 0f, jumpMoveDir.y));
        }
        else
        {
            Walking();
        }
    }

    public void Walking()
    {
        velocity = isWalk ? baseSpeed * 0.3f * Time.deltaTime : baseSpeed * Time.deltaTime;
        rb.linearVelocity = new Vector3(rotation.x * velocity, rb.linearVelocity.y, rotation.y * velocity);
        transform.LookAt(transform.position + new Vector3(rotation.x, 0f, rotation.y));
    }

    public void UpdateMouseLook()
    {
        // 구버전 마우스 클릭 체크 (0번이 좌클릭)
        if (Input.GetMouseButton(0) && !isDodge && !isDead)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit rayHit;
            if (Physics.Raycast(ray, out rayHit, 100))
            {
                Vector3 nextVec = rayHit.point - transform.position;
                nextVec.y = 0f;
                transform.LookAt(transform.position + nextVec);
            }
        }
    }

    private void DodgeOut()
    {
        speed *= 0.5f;
        isDodge = false;
        rotation = rotation_value;
        keepMovingAfterDodge = true;
    }

    // 0.3프레임당 호출되는 메서드
    // OnPhotonSerializeView : 관찰할 데이터들을 주고받으며 동기화 해주도록 구현할 수 있는 인터페이스
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // 방에 들어가지 않은 상태에서는 실행 금지
        if (!PhotonNetwork.InRoom)
            return;
        // 로컬 플레이어의 위치 정보 송신(pv.IsMine) -> IsMine 입장에서 보내는 것
        if (stream.IsWriting) // stream 쪽에 기록을 하기 위함
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
        }
        else // 원격 플레이어 위치 정보 수신 (아바타들) // 아바타들 입장에서 IsMine에서 보낸 것을 받는 것.
        {
            CurPos = (Vector3)stream.ReceiveNext();
            CurRot = (Quaternion)stream.ReceiveNext();

            if (isFirstUpdate)
            {
                // 보간(Lerp) 없이 바로 현재 위치로 강제 이동
                transform.position = CurPos;
                transform.rotation = CurRot;

                // 다음부터는 부드럽게 움직이도록 플래그 끔
                isFirstUpdate = false;
            }
        }// 원격 플레이어 위치 정보 수신 (아바타들) // 아바타들 입장에서 IsMine에서 보낸 것을 받는 것.
    }
}
