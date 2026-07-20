using Photon.Pun;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public enum MonType
{
    Skeleton = 0,
    Alien,
    Count
}

[System.Serializable]
public class Anim
{
    public AnimationClip Idle;
    public AnimationClip Move;
    public AnimationClip Attack1;
    public AnimationClip Attack2;
    public AnimationClip Skill1;
    public AnimationClip Skill2;
    public AnimationClip Die;
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
    private float targetCheckTimer = 0f;
    private float targetCheckInterval = 0.2f; // 0.2초마다 타겟 체크 연산 실행

    [Header("Options")]
    [SerializeField] float MaxHp = 100;
    [SerializeField] private float detectRange = 10f; // 감지 범위 (10m)


    //--- Hp 바 표시
    float CurHp;
    float NetHp;
    string m_Id = "";
    //--- Hp 바 표시

    //--- 애니메이션
    [SerializeField] private Animator m_RefAnimator = null;
    private Anim anim;
    [SerializeField] private Animation m_RefAnimation = null;

    AnimState m_PreState = AnimState.idle; //애니메이션 변경을 위한 함수 
    AnimState m_CurState = AnimState.idle; //애니메이션 변경을 위한 변수
    //--- 애니메이션

    private Vector3 CurPos = Vector3.zero;
    private Quaternion CurRot = Quaternion.identity;

    private bool isChase = false;
    bool isFirstUpdate = true;
    private void Start()
    {
        m_RefAnimator = GetComponent<Animator>();
        CurHp = MaxHp;
    }

    private void Update()
    {
        if (pv.IsMine) // 이 몬스터의 소유권을 가진 컴퓨터만 AI를 연산함
        {
            if (CurHp <= 0.0f) return; // 사망 시 AI 중지

            // 0.2초마다 타겟팅 상태를 갱신 (매 프레임 OverlapSphere를 돌리면 렉 유발)
            targetCheckTimer += Time.deltaTime;
            if (targetCheckTimer >= targetCheckInterval)
            {
                targetCheckTimer = 0f;
                TargetScanning();
            }

            // 타겟이 있고 추적 상태(`isChase`)일 때만 실제로 NavMeshAgent를 이동시킴
            if (m_AggroTarget != null && nav.enabled && isChase)
            {
                nav.SetDestination(m_AggroTarget.position);
                nav.isStopped = false; // 브레이크 해제, 전진!
                ChangeAnim(AnimState.trace);
            }
            else
            {
                // 타겟이 없거나 범위 밖이면 제자리에 정지
                if (nav.enabled)
                {
                    ChangeAnim(AnimState.idle);
                    nav.isStopped = true;
                }
            }
        }
        else // 다른 사람들의 화면(원격 아바타)일 경우
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
        }
    }

    private void TargetScanning()
    {
        // --- 1단계: 기존 타겟이 유효한지 먼저 검사 ---
        if (m_AggroTarget != null)
        {
            // 타겟이 파괴되었거나, 비활성화되었거나, 10m 범위를 벗어났다면 타겟 상실 처리
            float distance = Vector3.Distance(transform.position, m_AggroTarget.position);
            if (!m_AggroTarget.gameObject.activeInHierarchy || distance > detectRange)
            {
                m_AggroTarget = null;
                isChase = false; // 범위 밖으로 나가면 추적 중지
            }
            else
            {
                // 아직 타겟이 유효하고 범위 내에 있다면 다른 타겟을 찾지 않고 그대로 유지
                return;
            }
        }

        // --- 2단계: 기존 타겟이 없거나 유효하지 않을 때, 새 타겟 탐색 ---
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

    //void ChangeAnim(AnimState newState, float CrossTime = 0.0f)
    //{
    //    // 1. 현재 상태와 요청된 상태가 같다면 중복 재생 방지를 위해 리턴
    //    if (m_PreState == newState)
    //        return;
    //    Debug.Log($"newState : {newState}");
    //    // 2. 레거시 Animation 컴포넌트와 상수 관리용 스크립트가 모두 있는지 검사
    //    if (m_RefAnimation != null)
    //    {
    //        // 기본값은 IDLE로 설정 (상수이므로 '클래스명.상수명'으로 접근)
    //        string strAnim = Animation_Test.IDLE;

    //        // 각 상태(Enum)에 맞는 애니메이션 파일 이름을 매핑
    //        if (newState == AnimState.idle)
    //            strAnim = Animation_Test.IDLE;
    //        else if (newState == AnimState.trace)
    //            strAnim = Animation_Test.RUN;
    //        else if (newState == AnimState.attack)
    //            strAnim = Animation_Test.ATTACK;
    //        else if (newState == AnimState.die)
    //            strAnim = Animation_Test.DEATH;
    //        else if (newState == AnimState.hit)
    //            strAnim = Animation_Test.DAMAGE;

    //        // 3. 부드럽게 넘길지(CrossFade), 즉시 바꿀지(Play) 결정
    //        if (0.0f < CrossTime)
    //            m_RefAnimation.CrossFade(strAnim, CrossTime);
    //        else
    //            m_RefAnimation.Play(strAnim);

    //        // 4. 애니메이션 재생이 성공한 뒤에 상태 변수들 갱신
    //        m_PreState = newState;
    //        m_CurState = newState;
    //    }
    //}

    void ChangeAnim(AnimState newState, float CrossTime = 0.0f)
    {
        if (m_PreState == newState)
            return;

        if (m_RefAnimation != null)
        {
            string strAnim = anim.Idle.name;
            if (newState == AnimState.idle)
                strAnim = anim.Idle.name;
            else if (newState == AnimState.trace)
                strAnim = anim.Move.name;
            else if (newState == AnimState.attack)
                strAnim = anim.Attack1.name;
            else if (newState == AnimState.die)
                strAnim = anim.Die.name;

            if (0.0f < CrossTime)
                m_RefAnimation.CrossFade(strAnim, CrossTime);
            else
                m_RefAnimation.Play(strAnim);
        }//if (m_RefAnimation != null)

        if (m_RefAnimator != null)
        {
            m_RefAnimator.ResetTrigger(m_PreState.ToString());
            //기존에 적용되어 있던 Trigger 변수 제거

            if (0.0f < CrossTime)
                m_RefAnimator.SetTrigger(newState.ToString());
            else
            {
                string animName = anim.Idle.name;
                m_RefAnimator.Play(animName, -1, 0);
                //가운데 -1은 Layer Index, 뒤에 0은 처음부터 다시 시작 플레이 시키겠다는 의미
            }
        }//if(m_RefAnimator != null)

        m_PreState = newState;
        m_CurState = newState;

    }

    //현재 공격 중인지 확인하는 메서드
    public bool IsWait()
    {
        return m_CurState == AnimState.attack || m_CurState == AnimState.hit || m_CurState == AnimState.die;
    }
    public void TakeDamage(GameObject Attacker, float Damage)
    {
        if (CurHp <= 0.0f)
            return;
        if (pv.IsMine) // 실제 데미지는 IsMine인 쪽에서만 계산해서 적용하도록 처리, 아니면, 
        {
            CurHp -= Damage;
            if (CurHp < 0.0f)
                CurHp = 0.0f;
            ChangeAnim(AnimState.hit);
            ImgHpbar.fillAmount = CurHp / MaxHp;
            if(CurHp <= 0.0f)
            {
                CurHp = 0.0f;
                StartCoroutine(Die());
            }
        }
    }

    private IEnumerator Die()
    {
        Debug.Log("몬스터 사망");
        isChase = false;
        m_AggroTarget = null;
        ChangeAnim(AnimState.die, 0.1f);
        if (nav != null)
        {
            nav.isStopped = true;
            nav.enabled = false; // 컴포넌트 자체를 끄기
        }
        yield return new WaitForSeconds(2f);

        PhotonNetwork.Destroy(gameObject);
    }

    private void Remote_Take_Damage() // 원격지 컴퓨터에서 hp 동기화 함수
    {
        if (0.0f < CurHp)
        {
            CurHp = NetHp; // 원격 플레이어의 Monster의 hp를 수신 받은 hp로 업데이트
            ImgHpbar.fillAmount = CurHp / (float)MaxHp; // hp 바 업데이트

            if (CurHp <= 0.0f)
            {
                CurHp = 0.0f;
            }
        }
        else
        {
            CurHp = NetHp; // 원격 플레이어의 Monster의 hp를 수신 받은 hp로 업데이트
            ImgHpbar.fillAmount = CurHp / (float)MaxHp; // hp 바 업데이트
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        Debug.Log("몬스터 네트워크 오브젝트 동기화 처리");
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
            m_Id  = (string)stream.ReceiveNext();
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
