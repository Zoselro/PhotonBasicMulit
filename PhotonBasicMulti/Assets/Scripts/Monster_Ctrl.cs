using Photon.Pun;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public enum MonType
{
    monster = 0,
    bossMonster
}

public class Monster_Ctrl : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Components")]
    [SerializeField] PhotonView pv = null; // Photon View 컴포넌트 할당 변수
    [SerializeField] private Image ImgHpbar;
    [SerializeField] private NavMeshAgent nav;
    [SerializeField] private Text id;

    [Header("MonsterAI")]
    [SerializeField] private Transform m_AggroTarget = null; // 공격 대상
    [SerializeField] private float targetCheckInterval = 0.2f; // 0.2초마다 타겟 체크 연산 실행
    private float targetCheckTimer = 0f;
    [SerializeField] private MonType monType;

    [Header("Options")]
    [SerializeField] float MaxHp = 100; // 몬스터 최대 체력
    [SerializeField] private float detectRange = 10f; // 감지 범위 (10m)
    [SerializeField] private float m_AttackDist = 1.5f; // 공격/정지 거리
    [SerializeField] private float m_DamageDist = 1.5f; // 공격모션을 할 때 몬스터와 플레이어간의 거리
    [SerializeField] private float damage = 100f; // 공격 시 데미지 

    //--- Hp 바 표시
    float CurHp;
    float NetHp;
    string m_Id = "";
    //--- Hp 바 표시

    //--- 애니메이션
    [SerializeField] private Animator m_RefAnimator = null;

    AnimState m_PreState = AnimState.idle; //애니메이션 변경을 위한 함수 
    AnimState m_CurState = AnimState.idle; //애니메이션 변경을 위한 변수
    //--- 애니메이션

    private Vector3 CurPos = Vector3.zero;
    private Quaternion CurRot = Quaternion.identity;

    private bool isChase = false;
    bool isFirstUpdate = true;
    private void Start()
    {
        CurHp = MaxHp;

        if (pv.IsMine)
        {
            // 소유자만 NavMeshAgent를 활성화하고 정지 거리를 설정
            if (nav != null)
            {
                nav.enabled = true;
                nav.stoppingDistance = m_AttackDist;
            }
        }
        else
        {
            // 원격 클라이언트에서는 NavMeshAgent를 아예 끄기
            // 이렇게 해야 원격 클라이언트의 플레이어를 억지로 밀어내거나 튕겨내지 않음
            if (nav != null)
            {
                nav.enabled = false;
            }
        }
    }

    private void Update()
    {
        targetCheckTimer += Time.deltaTime;
        if (targetCheckTimer >= targetCheckInterval)
        {
            targetCheckTimer = 0f;
            TargetScanning();
        }

        if (pv.IsMine)
        {
            MonStateUpdate();
        }

        // 0.2초마다 타겟팅 상태를 갱신 (매 프레임 OverlapSphere를 돌리면 렉 유발)

        if(!pv.IsMine) // 다른 사람들의 화면(원격 아바타)일 경우
        {
            // 플레이어 코드에서 했던 것처럼, 포톤으로 받아온 위치와 회전값을 동기화해 줍니다.
            if (10.0f < (transform.position - CurPos).magnitude)
            {
                transform.position = CurPos;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, CurPos, Time.deltaTime * 10.0f);
            }

            transform.rotation = Quaternion.Slerp(transform.rotation, CurRot, Time.deltaTime * 10.0f);

            // 원격 화면에서는 직접 움직이지 않으므로 NavMeshAgent의 위치 업데이트를 꺼둡니다.
            if (nav.enabled)
            {
                nav.updatePosition = false;
            }
            Remote_Take_Damage();
            Remote_Animation();
        }
    }

    private void TargetScanning()
    {
        // 기존 타겟이 유효한지 먼저 검사
        if (m_AggroTarget != null)
        {
            // 타겟이 파괴되었거나, 비활성화되었거나, 10m 범위를 벗어났다면 타겟 상실 처리
            float distance = Vector3.Distance(transform.position, m_AggroTarget.position);
            if (!m_AggroTarget.gameObject.activeInHierarchy || distance > detectRange)
            {
                m_AggroTarget = null;
                isChase = false; // 범위 밖으로 나가면 추적 중지
            }
        }

        // 기존 타겟이 없거나 유효하지 않을 때, 새 타겟 탐색
        // 주변 10m 범위 안의 모든 Collider를 수집
        Collider[] colliders = Physics.OverlapSphere(transform.position, detectRange);

        Transform closestPlayer = null;
        float closestDistance = Mathf.Infinity;

        foreach (Collider col in colliders)
        {
            // Player 컴포넌트가 있는지 확인
            Player player = col.GetComponent<Player>();
            if (player != null)
            {
                float dist = Vector3.Distance(transform.position, col.transform.position);
                // 범위 내의 플레이어들 중 가장 가까운 플레이어를 후보로 등록
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    closestPlayer = col.transform;
                }
            }
        }

        // 가장 가까운 플레이어를 타겟으로 확정
        if (closestPlayer != null)
        {
            m_AggroTarget = closestPlayer;
            isChase = true; // 추적 시작
        }
        else
        {
            // 범위 내에 플레이어가 단 한 명도 없다면 추적을 멈춤
            isChase = false;
        }
    }

    private void MonStateUpdate()
    {
        // 몬스터가 공격하거나, 데미지를 받았거나, 죽었을 경우 추적하지 않음.
        if (IsWait())
        {
            nav.isStopped = true;
            nav.velocity = Vector3.zero; // 이전 속도 잔여값 제거
            return;
        }
        // 타겟이 있고 추적 상태(`isChase`)일 때만 실제로 NavMeshAgent를 이동시킴
        if (m_AggroTarget != null && nav.enabled && isChase)
        {
            float distanceToTarget = Vector3.Distance(transform.position, m_AggroTarget.position);

            if (distanceToTarget <= m_AttackDist)
            {
                // ★ 2. 공격 시작! (이 함수 안에서 m_CurState가 attack으로 바뀝니다)
                //StartCoroutine(AttackRoutine());
                transform.LookAt(m_AggroTarget);
                Attack();
            }
            else
            {
                // 사거리 밖이면 이동
                nav.SetDestination(m_AggroTarget.position);
                nav.isStopped = false;
                ChangeAnim(AnimState.move, 0.12f);
            }
        }
        else
        {
            // 타겟이 없거나 범위 밖이면 제자리에 정지
            if (nav.enabled)
            {
                ChangeAnim(AnimState.idle, 0.12f);
                //ChangeAnim(AnimState.idle);
                nav.isStopped = true;
                nav.velocity = Vector3.zero; // 이전 속도 잔여값 제거
            }
        }
    }

    void ChangeAnim(AnimState newState, float CrossTime)
    {
        if (m_PreState == newState)
        {
            return;
        }

        if (m_RefAnimator != null)
        {
            m_RefAnimator.ResetTrigger(m_PreState.ToString());
            //기존에 적용되어 있던 Trigger 변수 제거

            //m_RefAnimator.SetTrigger(newState.ToString());

            if (0.0f < CrossTime)
            {
                m_RefAnimator.SetTrigger(newState.ToString());
            }
            else
            {
                m_RefAnimator.Play(newState.ToString(), 0, 0f);
            }
        }

        m_PreState = newState;
        m_CurState = newState;

    }

    //현재 공격이나 죽거나 데미지를 받았는지 확인하는 메서드 (잠깐 navMesh를 멈추기 위함)
    public bool IsWait()
    {
        return m_CurState == AnimState.attack || m_CurState == AnimState.damage || m_CurState == AnimState.die;
    }
    public void TakeDamage(GameObject Attacker, float Damage)
    {
        if (CurHp <= 0.0f)
            return;

        if (pv.IsMine) // 실제 데미지는 IsMine인 쪽에서만 계산해서 적용하도록 처리, 아니면, 
        {
            CurHp -= Damage;
            if (CurHp <= 0.0f)
            {
                StartCoroutine(Die());
                CurHp = 0.0f;
            }
            else
            {
                StartCoroutine(DamageAnim());
            }
            ImgHpbar.fillAmount = CurHp / MaxHp;
        }
    }

    // 몬스터가 데미지를 받았을 때 일어나는 애니메이션
    private IEnumerator DamageAnim()
    {
        ChangeAnim(AnimState.damage, 0f);
        float timer = 0f;

        while (timer < 1f)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        ChangeAnim(AnimState.idle, 0.12f);
    }

    private IEnumerator Die()
    {
        isChase = false;
        m_AggroTarget = null;
        ChangeAnim(AnimState.die, 0.1f);
        float timer = 0f;

        //ChangeAnim(AnimState.die);

        while(timer < 2f)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if(pv.IsMine)
            PhotonNetwork.Destroy(gameObject);
    }

    #region 이벤트 메서드
    // 애니메이션이 완전히 끝났을 때 호출할 이벤트 함수
    private void OnAnimationEnd()
    {
        if (pv.IsMine)
        {
            m_CurState = AnimState.idle;
            m_PreState = AnimState.idle; // Trigger 중복 방지 리셋
        }
    }

    // 공격 할 때 데미지가 들어가게 하는 이벤트 함수
    private void OnAttackHit()
    {
        if (m_AggroTarget != null)
        {
            Player player = m_AggroTarget.GetComponent<Player>();
            float distanceToTarget = Vector3.Distance(transform.position, m_AggroTarget.position);

            Debug.Log("player : " + player.gameObject.name);
            if(distanceToTarget < m_DamageDist) // 몬스터와 플레이어간의 거리가 정해둔 거리 미만 일 때 데미지가 들어감
            {
                player.TakeDamage(damage);
            }
        }
        else
        {
            Debug.Log("어그로 된 플레이어 존재하지 않음.");

            // 1. 트리거 리셋 및 강제 Idle 상태로 애니메이션 재시작
            if (m_RefAnimator != null)
            {
                m_RefAnimator.ResetTrigger(m_PreState.ToString());
                //m_RefAnimator.SetTrigger(AnimState.idle.ToString());
                m_RefAnimator.Play(AnimState.idle.ToString(), 0, 0f);
            }

            // 2. 상태 변수 강제 초기화
            m_CurState = AnimState.idle;
            m_PreState = AnimState.idle;

            // 3. NavMeshAgent 재개
            if (nav != null && nav.enabled)
            {
                nav.isStopped = false;
            }
        }
    }
    #endregion

    private void Attack()
    {
        nav.isStopped = true;
        nav.velocity = Vector3.zero; // 이전 속도 잔여값 제거

        ChangeAnim(AnimState.attack, 0.12f);
    }

    private void Remote_Take_Damage() // 원격지 컴퓨터에서 hp 동기화 함수
    {
        if (0.0f < CurHp)
        {
            CurHp = NetHp; // 원격 플레이어의 Monster의 hp를 수신 받은 hp로 업데이트
            ImgHpbar.fillAmount = CurHp / (float)MaxHp; // hp 바 업데이트

            //StartCoroutine(DamageAnim());
            if (CurHp <= 0.0f)
            {
                CurHp = 0.0f;
                StartCoroutine(Die());
            }
        }
        else
        {
            CurHp = NetHp; // 원격 플레이어의 Monster의 hp를 수신 받은 hp로 업데이트
            ImgHpbar.fillAmount = CurHp / (float)MaxHp; // hp 바 업데이트
        }
    }

    private void Remote_Animation() // 원격지 컴퓨터에서 애니메이션 동기화 함수
    {
        ChangeAnim(m_CurState, 0.12f);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // 로컬 몬스터 위치 정보 송신
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(CurHp);
            stream.SendNext(id.text);
            stream.SendNext((int)m_CurState);
        }
        else // 원격 몬스터의 위치 정보 수신
        {
            CurPos = (Vector3)stream.ReceiveNext();
            CurRot = (Quaternion)stream.ReceiveNext();
            NetHp = (float)stream.ReceiveNext();
            m_Id = (string)stream.ReceiveNext();
            m_CurState = (AnimState)stream.ReceiveNext();
            id.text = m_Id;

            if (isFirstUpdate)
            {
                // 보간(Lerp) 없이 바로 현재 위치로 강제 이동
                transform.position = CurPos;
                transform.rotation = CurRot;

                // 다음부터는 부드럽게 움직이도록 플래그 끔
                isFirstUpdate = false;
            }
        }
    }
}
