using Photon.Pun;
using HashTable = ExitGames.Client.Photon.Hashtable; // 포톤의 해시테이블을 사용하기 위한 별칭
using System.Collections.Generic;
using UnityEngine;
using Photon.Realtime;

public class MonSpawn_Mgr : MonoBehaviourPunCallbacks
{
    [System.Serializable]
    public class SpawnPos
    {
        public Transform transform;
        public string key; // "SP_0", "SP_1"
    }

    public List<SpawnPos> m_SpawnPos = new List<SpawnPos>();

    // [상태 정의] 직관적인 관리를 위한 변수
    const double IDLE_STATE = -1.0f; // 진짜 비어있음(초기 상태)
    const double STATE_ACTIVE = -2.0f; // 몬스터가 살아서 활동 중
    bool IsAllSpawns = false;

    float g_NetDelay = 0.0f; // 방장이 바뀌었을 때 네트워크 지연 시간

    public static MonSpawn_Mgr Inst = null;

    private void Awake()
    {
        Inst = this;

        Transform[] spawnPointList = gameObject.GetComponentsInChildren<Transform>();
        int index = 0;

        foreach (Transform child in spawnPointList)
        {
            if (child == this.transform) continue; // 자신은 제외

            SpawnPos data = new SpawnPos();
            data.transform = child;
            data.key = $"SP_{index}";
            m_SpawnPos.Add(data);
            index++;
        }
    }

    private void Update()
    {
        if (!PhotonNetwork.InRoom) // 포톤이 인게임 방안에 있을 때만..
        {
            return;
        }

        if (PhotonNetwork.IsMasterClient) // 방장만 몬스터 스폰 관리
        {
            // 전체 스폰 위치에 한 번 스폰 예약을 해야되는데 
            // 아직 전체 스폰이 되지 않은 상태라면?
            if (!IsAllSpawns)
            {
                // 무조건 덮어 쓰지 않고, 방에 정보가 없는지 확인
                if (!CheckifSpawnAlreadyExist())
                {
                    ScheduleAllSpawns(1.5f, 5.0f);
                }

                // 마스터는 이제 스폰 관리를 시작 했음을 표시
                IsAllSpawns = true;
            }
        }

        // 1. 마스터 변경 시 딜레이 처리
        if (0.0f < g_NetDelay)
        {
            g_NetDelay -= Time.deltaTime;
            return; // 마스터 변경 시 스폰 잠시 딜레이 주기 마스터 정보 동기화를 위해
        }

        CheckSpawn();
    }

    // Room CustomProperies에 전체 스폰 정보가 이미 있는지 확인하는 함수
    private bool CheckifSpawnAlreadyExist()
    {
        HashTable cp = PhotonNetwork.CurrentRoom.CustomProperties;

        // 첫 번째 스폰 포인트의 키가 존재하는지 확인
        if (m_SpawnPos.Count > 0 && cp.ContainsKey(m_SpawnPos[0].key))
        {
            return true; // 이미 스폰 위치가 존재함
        }

        return false; // 스폰 위치가 존재하지 않음
    }


    // 네 개의 스폰 포인트에 각각 랜덤한 시간으로 스폰 예약을 하는 함수
    public void ScheduleAllSpawns(float minDelay, float maxDelay)
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            return;

        HashTable props = new HashTable();
        double currentTime = PhotonNetwork.Time;

        foreach (var sp in m_SpawnPos)
        {
            double randomDelay = Random.Range(minDelay, maxDelay);
            props.Add(sp.key, currentTime + randomDelay);
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    // 각 스폰 자리별로 스폰 상태 체크 하는 함수
    private void CheckSpawn()
    {
        double currentTime = PhotonNetwork.Time; // 네트워크 시간을 바로 받아오기 위한 프로퍼티
        HashTable cp = PhotonNetwork.CurrentRoom.CustomProperties;
        HashTable newProbs = new HashTable(); // 방 단위에 생성되는 딕셔너리

        bool needsUpdate = false; // 업데이트가 필요한지 여부

        SpawnPos sp = null;
        var monList = FindObjectsByType<Monster_Ctrl>(FindObjectsSortMode.None);

        for (int i = 0; i < m_SpawnPos.Count; i++)
        {
            sp = m_SpawnPos[i];

            // 방 속성에서 시간 가져오기(없으면 대기 상태로 간주)

            double spawnTime = IDLE_STATE; // 기본값은 대기 상태
            if (cp.ContainsKey(sp.key))
            {
                spawnTime = (double)cp[sp.key];
            }

            // double spawnTime = IDLE_STATE or spawnTime == STATE_ACTIVE
            // 몬스터가 살아있다는 뜻이니 아무것도 하지말고 패스! (가장 빠른 탈출)
            if (spawnTime < 0.0f)
            {
                Debug.Log("몬스터가 살아있음!");
                continue;
            }

            // 시간이 되었으면 스폰
            if (spawnTime <= currentTime)
            {
                bool isActMon = false;
                foreach (var mon in monList)
                {
                    if(i == mon.SpawnIdx)
                    {
                        Debug.Log("mon.SpawnIdx : " + mon.SpawnIdx);
                        isActMon = true;
                        break;
                    }
                }

                if (!isActMon)
                {
                    SpawnMonster(sp, i);
                }

                // 스폰 처리가 끝났으니 상태로 ACTIVE(-2.0)으로 변경
                // 이제 이 자리는 몬스터가 죽을 때까지 아무도 못 건드림.
                newProbs.Add(sp.key, STATE_ACTIVE);
                needsUpdate = true;
            }
        }
        if (needsUpdate)
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(newProbs); // A, B, C Pc에서도 
        }
    }

    private void SpawnMonster(SpawnPos sp, int sitIdx)
    {
        //string monsterName = (Random.Range(0, 2) == 0) ? "Skeleton_Root" : "Alien_Root";

        string monsterName = "StoneMonster";

        // 스폰 될 때 가지고 갈 데이터 포장 (int 배열 등)
        object[] data = new object[] { sitIdx };

        // InstantiateRoomObject의 마지막 인자로 data를 넘김.
        // 방 기준으로 A, B, C Pc에서도 스폰이 될 때 동기화 처리가 된다.
        GameObject TempMon = PhotonNetwork.InstantiateRoomObject(monsterName, sp.transform.position, sp.transform.rotation, 0, data);
        Debug.Log($"Spawn {sitIdx}");
    }

    // 특정 위치의 스폰을 예약하는 함수
    public void ScheduleSpawn(int spawnIdx, float delay)
    {
        if (spawnIdx < 0 || m_SpawnPos.Count <= spawnIdx)
            return;

        // pun2 기준 인게임 상태가 아니거나
        // MasterClient 가 아닌 경우는 스킵 한다는 뜻
        // 마스터 클라이언트에서만 스폰을 관리하게 하기 위함
        if(!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
        {
            return;
        }

        SpawnPos sp = m_SpawnPos[spawnIdx];
        double targetTime = PhotonNetwork.Time + delay;

        HashTable props = new HashTable { {sp.key, targetTime } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    // 마스터 클라이언트 변경시 호출되는 함수
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        base.OnMasterClientSwitched(newMasterClient);

        // 새로운 마스터 클라이언트에게 모든 변수 상태를 즉시 인수 인계 해 주어야 한다.
        g_NetDelay = 1.0f;
    }
}
