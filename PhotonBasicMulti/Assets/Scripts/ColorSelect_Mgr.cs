using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// 색상 선택 & 술래 변형 시스템의 씬 오케스트레이터.
// 마스터 클라이언트가 Room CustomProperties(rIdx/rEnd)로 4라운드(라운드당 20초) 타이머를 진행시키고,
// 각 클라이언트는 자기 자신의 Player CustomProperties(pClr)에만 색상을 커밋한다.
public class ColorSelect_Mgr : MonoBehaviourPunCallbacks
{
    static public ColorSelect_Mgr Inst;

    public const int ROUND_COUNT = 4;      // 총 라운드 수
    public const float ROUND_TIME = 20.0f; // 라운드당 제한시간(초)

    const float RESOLVE_GRACE = 1.0f; // 라운드 종료 후, 낙오자를 마스터가 강제 확정시켜주기까지의 유예시간
    const float REVEAL_DELAY = 3.0f;  // 술래 판정 연출 후 GameScene 전환까지 대기시간

    const string KEY_ROUND_IDX = "rIdx"; // Room: 현재 라운드 인덱스(0~3)
    const string KEY_ROUND_END = "rEnd"; // Room: 현재 라운드 종료 시각(PhotonNetwork.Time 기준)
    const string KEY_PHASE = "phase";    // Room: 진행 상태
    const string PHASE_RESOLVED = "resolved"; // 4라운드 종료 + 술래 판정까지 끝난 상태

    const string KEY_PLAYER_COLORS = "pClr"; // Player: 라운드별 확정 색상(int[4], 미확정 슬롯은 -1)
    const string KEY_PLAYER_SEEKER = "pSeek"; // Player: 술래 여부

    bool m_bResolveStarted = false;   // 술래 판정 로직 중복 실행 방지
    bool m_bSceneLoadStarted = false; // 씬 전환 코루틴 중복 실행 방지

    private void Awake()
    {
        Inst = this;
    }

    private void Start()
    {
        PhotonNetwork.IsMessageQueueRunning = true;

        CreateToken();

        if (PhotonNetwork.IsMasterClient)
        {
            InitRoomForFirstRound();
        }
    }

    private void Update()
    {
        if (!PhotonNetwork.InRoom)
            return;

        if (IsPhaseResolved())
        {
            BeginSceneTransitionOnce();
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            MasterTickRound();
        }
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        // 진행 중이던 마스터가 방을 나가 새 마스터로 위임된 경우.
        // 이미 시작된 라운드 진행 상태(Room CustomProperties)는 그대로 이어받는다.
        if (PhotonNetwork.IsMasterClient)
        {
            InitRoomForFirstRound();
        }
    }

    //--- 플레이어별 색상 토큰 스폰 (GameManager.CreateHero와 동일한 패턴)
    private void CreateToken()
    {
        Vector3 pos = Vector3.zero;
        Vector3 addPos = Vector3.zero;

        GameObject posObj = GameObject.Find("ColorSelectSpawnPos");
        if (posObj != null)
        {
            addPos.x = Random.Range(-3.0f, 3.0f);
            addPos.z = Random.Range(-3.0f, 3.0f);
            pos = posObj.transform.position + addPos;
        }

        PhotonNetwork.Instantiate("ColorSelectToken", pos, Quaternion.identity, 0);
    }

    //--- 마스터: 첫 라운드 세팅. 이미 진행 중인 라운드가 있으면 건드리지 않는다(마스터 위임 대비).
    private void InitRoomForFirstRound()
    {
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(KEY_ROUND_IDX))
            return;

        PhotonNetwork.CurrentRoom.IsOpen = false; // 색상 선택 시작 시 방 잠금(늦은 입장 차단)

        SetRoomRound(0, PhotonNetwork.Time + ROUND_TIME);
    }

    private void SetRoomRound(int roundIdx, double roundEnd)
    {
        Hashtable table = new Hashtable();
        table[KEY_ROUND_IDX] = roundIdx;
        table[KEY_ROUND_END] = roundEnd;
        PhotonNetwork.CurrentRoom.SetCustomProperties(table);
    }

    //--- 마스터: 매 프레임 라운드 진행 상황 체크
    private void MasterTickRound()
    {
        int roundIdx = GetRoundIdx();
        double roundEnd = GetRoundEndTime();

        if (PhotonNetwork.Time < roundEnd)
        {
            if (AllPlayersConfirmed(roundIdx)) // 전원 확정되면 제한시간 전이라도 바로 다음 라운드로
                AdvanceRound(roundIdx);
            return;
        }

        if (PhotonNetwork.Time < roundEnd + RESOLVE_GRACE)
            return; // 유예시간 동안은 낙오자가 스스로(로컬 타임아웃으로) 커밋하길 기다림

        ForceCommitStragglers(roundIdx); // 유예시간이 지나도 확정 못한 플레이어(연결 문제 등)는 마스터가 대신 확정
        AdvanceRound(roundIdx);
    }

    private void AdvanceRound(int roundIdx)
    {
        int nextIdx = roundIdx + 1;
        if (nextIdx >= ROUND_COUNT)
        {
            ResolveGame();
            return;
        }

        SetRoomRound(nextIdx, PhotonNetwork.Time + ROUND_TIME);
    }

    private bool AllPlayersConfirmed(int roundIdx)
    {
        Player[] players = PhotonNetwork.PlayerList;
        for (int i = 0; i < players.Length; i++)
        {
            if (GetPlayerColors(players[i])[roundIdx] < 0)
                return false;
        }
        return true;
    }

    private void ForceCommitStragglers(int roundIdx)
    {
        Player[] players = PhotonNetwork.PlayerList;
        for (int i = 0; i < players.Length; i++)
        {
            Player p = players[i];
            int[] colors = GetPlayerColors(p);
            if (colors[roundIdx] >= 0)
                continue;

            colors[roundIdx] = PackColor(Random.Range(0, 256), Random.Range(0, 256), Random.Range(0, 256));
            SetPlayerColors(p, colors);
        }
    }

    //--- 마스터: 4라운드 종료 후 술래 판정 및 색상 변형
    private void ResolveGame()
    {
        if (m_bResolveStarted)
            return;
        m_bResolveStarted = true;

        List<Player> players = new List<Player>(PhotonNetwork.PlayerList);
        int seekerIdx = Random.Range(0, players.Count);

        for (int i = 0; i < players.Count; i++)
        {
            Player p = players[i];
            bool isSeeker = (i == seekerIdx);

            if (isSeeker)
            {
                int[] colors = GetPlayerColors(p);
                int mutateSlot = Random.Range(0, ROUND_COUNT);
                colors[mutateSlot] = PackColor(Random.Range(0, 256), Random.Range(0, 256), Random.Range(0, 256));
                SetPlayerColors(p, colors);
            }

            Hashtable seekTable = new Hashtable();
            seekTable[KEY_PLAYER_SEEKER] = isSeeker;
            p.SetCustomProperties(seekTable);
        }

        Hashtable roomTable = new Hashtable();
        roomTable[KEY_PHASE] = PHASE_RESOLVED;
        PhotonNetwork.CurrentRoom.SetCustomProperties(roomTable);
    }

    private void BeginSceneTransitionOnce()
    {
        if (m_bSceneLoadStarted)
            return;
        m_bSceneLoadStarted = true;

        StartCoroutine(LoadGameSceneAfterDelay());
    }

    private IEnumerator LoadGameSceneAfterDelay()
    {
        yield return new WaitForSeconds(REVEAL_DELAY);

        PhotonNetwork.IsMessageQueueRunning = false;
        AsyncOperation ao = SceneManager.LoadSceneAsync("GameScene");
        yield return ao;
    }

    //--- Room 진행 상태 조회 ---------------------------------------------------

    public static bool IsPhaseResolved()
    {
        object v;
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(KEY_PHASE, out v))
            return (string)v == PHASE_RESOLVED;
        return false;
    }

    public static int GetRoundIdx()
    {
        object v;
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(KEY_ROUND_IDX, out v))
            return (int)v;
        return 0;
    }

    public static double GetRoundEndTime()
    {
        object v;
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(KEY_ROUND_END, out v))
            return (double)v;
        return 0.0;
    }

    //--- 플레이어 색상 데이터 조회/커밋 ------------------------------------------

    public static int[] GetPlayerColors(Player p)
    {
        object v;
        if (p != null && p.CustomProperties.TryGetValue(KEY_PLAYER_COLORS, out v))
            return (int[])v;

        return new int[] { -1, -1, -1, -1 };
    }

    public static void SetPlayerColors(Player p, int[] colors)
    {
        Hashtable table = new Hashtable();
        table[KEY_PLAYER_COLORS] = colors;
        p.SetCustomProperties(table);
    }

    public static bool IsPlayerSeeker(Player p)
    {
        object v;
        if (p != null && p.CustomProperties.TryGetValue(KEY_PLAYER_SEEKER, out v))
            return (bool)v;
        return false;
    }

    // 로컬 플레이어가 이번 라운드 색상을 확정할 때 호출 (수동 확인 / 로컬 타임아웃 공용 진입점)
    public static void CommitOwnColor(int roundIdx, Color32 color)
    {
        int[] colors = GetPlayerColors(PhotonNetwork.LocalPlayer);
        colors[roundIdx] = PackColor(color.r, color.g, color.b);
        SetPlayerColors(PhotonNetwork.LocalPlayer, colors);
    }

    //--- 색상 패킹 유틸 (Player CustomProperties/네트워크 스트림에는 int 하나로만 보관) -------

    public static int PackColor(int r, int g, int b)
    {
        return (r << 16) | (g << 8) | b;
    }

    public static Color32 UnpackColor(int packed)
    {
        if (packed < 0)
            return new Color32(255, 255, 255, 255); // 미확정 상태 기본값: 흰색

        byte r = (byte)((packed >> 16) & 0xFF);
        byte g = (byte)((packed >> 8) & 0xFF);
        byte b = (byte)(packed & 0xFF);
        return new Color32(r, g, b, 255);
    }
}
