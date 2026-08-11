using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PhotonInit : MonoBehaviourPunCallbacks
{
    // 플레이어 이름을 입력하는 UI 항목 연결
    [SerializeField] private InputField userID;
    [SerializeField] private Button JoinRandomRoomBtn;

    // 룸 이름을 입력 받을 UI 항목 연결 변수
    [SerializeField] private InputField roomName;
    [SerializeField] private Button createRoomBtn;

    // ---- 방 목록을 표시할 UI 항목 연결 변수
    // RoomItem 차일드로 생성할 Parent 객체
    [SerializeField] private GameObject scrollContents;
    // 룸 목록 만큼 생성될 RoomItem 프리팹
    [SerializeField] private GameObject roomItem;
    RoomItem[] m_RoomItemList; // Contents 아래에 생성된 룸 목록을 찾기 위한 배열
    // ---- 방 목록을 표시할 UI 항목 연결 변수

    // ---- 방 설정 여부 변수
    [SerializeField] private GameObject roomSetting; // 방 설정 여부를 입력 받을 UI 항목 연결 변수
    [SerializeField] private TMP_InputField maxPlayersInput; // 방 최대 인원 수를 입력 받을 UI 항목 연결 변수
    [SerializeField] private Toggle isOpenToggle; // 방이 열려 있는지 여부를 입력 받을 UI 항목 연결 변수
    [SerializeField] private Toggle showRoomToggle; // 방이 로비에서 보이는지 여부를 입력 받을 UI 항목 연결 변수
    [SerializeField] private Button yesBtn; // 방 설정 완료 버튼 UI 항목 연결 변수
    [SerializeField] private Button noBtn;
    // ---- 방 설정 여부 변수

    // 접속이 Disconnect 되었을 때, 재접속 하기 위한 Bool 변수
    private bool isReConnect = false;

    private void Awake()
    {
        if (PhotonNetwork.IsConnected == false)
        {
            PhotonNetwork.ConnectUsingSettings(); // 포톤 서버 접속 시도
            Debug.Log("포톤 서버 접속 시도");
        }
        roomName.text = "Room_" + Random.Range(0, 999).ToString("000");
    }
    private void Start()
    {
        if (JoinRandomRoomBtn != null)
        {
            JoinRandomRoomBtn.onClick.AddListener(OnClickJoinRandomRoom);
        }

        if (createRoomBtn != null)
        {
            createRoomBtn.onClick.AddListener(OnClickCreateRoom);
        }

        if(yesBtn != null)
        {
            yesBtn.onClick.AddListener(OnClickYesButton);
        }

        if(noBtn != null)
        {
            noBtn.onClick.AddListener(OnClickNoButton);
        }
    }

    private void LateUpdate()
    {
        if (isReConnect == false)
        {
            if (PhotonNetwork.IsConnected == false)
            {
                PhotonNetwork.ConnectUsingSettings(); // 포톤 서버 접속 시도
                Debug.Log("포톤 서버 재접속 시도");
            }
        }
    }
    #region Photon Callback Functions
    // PhotonNetwork.ConnectUsingSettings() 성공시 호출되는 포톤 서버 접속 콜백 함수
    override public void OnConnectedToMaster()
    {
        Debug.Log("포톤 서버 접속 성공");
        userID.text = GetUserID();
        PhotonNetwork.JoinLobby(); // 로비 접속 시도
        isReConnect = true;
    }


    // PhotonNetwork.JoinLobby() 성공시 호출되는 로비 접속 콜백 함수
    public override void OnJoinedLobby()
    {
        Debug.Log("로비 접속 성공");
    }

    // PhotonNetwork.Disconnect() 호출 시, 포톤 서버 접속 끊김 콜백 함수
    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.Log("포톤 서버 접속 끊김 : " + cause.ToString());
        isReConnect = false;
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        Debug.Log("방 목록 업데이트");

        // Contents 아래에 생성된 룸 목록을 찾기 위한 배열
        // 혹시나 Active 상태가 false 인 RoomItem도 포함해서 가져오기 위해서 true
        m_RoomItemList = scrollContents.transform.GetComponentsInChildren<RoomItem>(true);


        int roomCount = roomList.Count; // 방 목록의 개수
        int arrIdx = 0; // 배열 인덱스 초기화
        for (int i = 0; i < roomCount; i++)
        {
            arrIdx = MyFindIndex(m_RoomItemList, roomList[i]); // 방 목록에서 방 정보가 일치하는 RoomItem이 있는지 찾는 함수

            if (roomList[i].RemovedFromList == false)
            {// 누군가 방을 새로 생성했거나, 방정보를 갱신해 줘야 하는 상황
                if (arrIdx < 0)
                { // 방을 새로 생성하는 경우
                    // 스크롤 뷰에 붙여 줄 새로운 방 오브젝트를 새로 생성해 줘야 함
                    // --- 새로운 방 오브젝트 새로 생성
                    GameObject room = Instantiate(roomItem) as GameObject; // RoomItem 프리팹을 새로 생성
                    // 생성한 RoomItem 프리팹을 Contents의 자식으로 설정
                    room.transform.SetParent(scrollContents.transform, false); // Contents의 자식으로 설정하면서, 월드 좌표 유지 여부는 false로 설정 (false로 설정하면, 부모의 위치에 맞춰서 자식의 위치가 조정됨)
                    // 생성한 RoomItem에 표시하기 위한 텍스트 정보 전달
                    RoomItem roomData = room.GetComponent<RoomItem>(); // 생성한 RoomItem 프리팹에서 RoomItem 컴포넌트 가져오기
                    roomData.roomName = roomList[i].Name; // 방 이름 전달
                    roomData.connectPlayer = roomList[i].PlayerCount; // 방에 접속한 플레이어 수 전달
                    roomData.maxPlayers = roomList[i].MaxPlayers; // 방에 접속할 수 있는 최대 플레이어 수 전달

                    //텍스트 정보를 표시
                    roomData.DispRoomData(roomList[i].IsOpen); // 방이 열려있는지 여부 전달
                }
                else // 해당 방 목록이 존재하는 경우, 방 정보 갱신
                {
                    // 기존 방 정보만 갱신
                    m_RoomItemList[arrIdx].roomName = roomList[i].Name;
                    m_RoomItemList[arrIdx].connectPlayer = roomList[i].PlayerCount;
                    m_RoomItemList[arrIdx].maxPlayers = roomList[i].MaxPlayers;


                    //텍스트 정보를 표시
                    m_RoomItemList[arrIdx].DispRoomData(roomList[i].IsOpen);
                }
            }
            else // 방이 파괴가 되면서, 방 목록에서 제거되어야 하는 상황
            {
                if (0 <= arrIdx) // 방 목록에서 방 정보가 일치하는 RoomItem이 존재하는 경우, 해당 RoomItem 제거
                {
                    MyDestroy(m_RoomItemList, roomList[i]); // 이 방 정보를 갖고있는 리스트 뷰 목록을 모두 제거
                }
            }
        }
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.Log("방 생성 실패 : " + message);
    }

    public override void OnCreatedRoom()
    {
        Debug.Log("방 생성 성공 : " + PhotonNetwork.CurrentRoom.Name);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("방 입장 성공 : " + PhotonNetwork.CurrentRoom.Name);
        StartCoroutine(LoadBattleField());
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.Log("방 입장 실패" + message);
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log("무작위 방 입장 실패 : " + message);
    }

    #endregion

    private void OnClickCreateRoom()
    {
        roomSetting.SetActive(true); // 방 설정 UI 활성화
    }

    private void OnClickNoButton()
    {
        roomSetting.SetActive(false); // 방 설정 UI 비활성화
    }

    private void OnClickYesButton()
    {
        Debug.Log("내가 방을 생성하는 요청을 보냄");
        string roomName = this.roomName.text;

        if (string.IsNullOrEmpty(this.roomName.text))
        {
            roomName = "Room_" + Random.Range(0, 999).ToString("000");
        }

        PhotonNetwork.LocalPlayer.NickName = userID.text;
        PlayerPrefs.SetString("USER_ID", userID.text);

        RoomOptions roomOptions = new RoomOptions();

        roomOptions.IsOpen = !isOpenToggle.isOn; // 방이 열려 있는지 여부
        roomOptions.IsVisible = !showRoomToggle.isOn; // 방이 로비에서 보이는지 여부
        roomOptions.MaxPlayers = int.Parse(maxPlayersInput.text); // 방 최대 인원수

        PhotonNetwork.CreateRoom(roomName, roomOptions, TypedLobby.Default); // 기본 로비에 방 생성 요청
        roomSetting.SetActive(false); // 방 설정 UI 비활성화
    }

    private void OnClickJoinRandomRoom()
    {
        Debug.Log("JoinRandomRoom 버튼 클릭");
        // 로컬 플레이어 이름 설정
        PhotonNetwork.LocalPlayer.NickName = userID.text;

        // 플레이어 이름을 저장
        PlayerPrefs.SetString("USER_ID", userID.text);

        // 무작위 방 입장
        PhotonNetwork.JoinRandomRoom();
    }

    private string GetUserID()
    {
        string userID = PlayerPrefs.GetString("USER_ID");
        if (string.IsNullOrEmpty(userID))
        {
            userID = "USER " + Random.Range(0, 999).ToString("000");
        }
        return userID;
    }

    private int MyFindIndex(RoomItem[] rmItemList, RoomInfo roomInfo)
    {
        if (rmItemList == null || roomInfo == null) // 방 목록이 존재하지 않거나, 방 정보가 존재하지 않을 때, 방 정보가 일치하는 RoomItem이 있는지 찾기 위함
        {
            return -1;
        }

        if (rmItemList.Length <= 0) // 방 목록이 존재하지 않을 때, 방 정보가 일치하는 RoomItem이 있는지 찾기 위함
        {
            return -1;
        }

        for (int i = 0; i < rmItemList.Length; i++) // 방 목록에서 방 정보가 일치하는 RoomItem이 있는지 찾는 함수
        {
            if (rmItemList[i].roomName.Equals(roomInfo.Name)) // 방 이름이 일치하는 RoomItem이 있는지 찾는 조건문
            {
                return i;
            }
        }

        return -1; // 방 정보가 일치하는 RoomItem이 없는 경우, -1 반환
    }

    private void MyDestroy(RoomItem[] rmItemList, RoomInfo roomInfo)
    {
        if (rmItemList == null || roomInfo == null) // 방 목록이 존재하지 않거나, 방 정보가 존재하지 않을 때, 방 정보가 일치하는 RoomItem이 있는지 찾기 위함
        {
            return;
        }
        if (rmItemList.Length <= 0) // 방 목록이 존재하지 않을 때, 방 정보가 일치하는 RoomItem이 있는지 찾기 위함
        {
            return;
        }
        for (int i = 0; i < rmItemList.Length; i++) // 방 목록에서 방 정보가 일치하는 RoomItem이 있는지 찾는 함수
        {
            if (rmItemList[i].roomName.Equals(roomInfo.Name)) // 방 이름이 일치하는 RoomItem이 있는지 찾는 조건문
            {
                Destroy(rmItemList[i].gameObject); // 해당 RoomItem 오브젝트 제거
            }
        }
    }

    public void OnClickRoomItem(string roomName)
    {
        Debug.Log(GetUserID() + "님이 " + roomName + " 방에 참가 시도");

        // 로컬 플레이어 이름 설정
        PhotonNetwork.LocalPlayer.NickName = userID.text;
        // 플레이어 이름을 저장
        PlayerPrefs.SetString("USER_ID", userID.text);

        // 인자로 전달 된 이름에 해당 방에 입장
        PhotonNetwork.JoinRoom(roomName);
    }

    private IEnumerator LoadBattleField()
    {
        // 씬을 이동하는 동안 포톤 클라이드 서버로부터 네크워크 메시지 수신 중단
        PhotonNetwork.IsMessageQueueRunning = false;
        // 백그라운드로 씬 로딩
        AsyncOperation ao = SceneManager.LoadSceneAsync("ColorSelectScene"); // 로딩연출 할 때 쓰는 씬 (게이지바가 올라가는 거라던지..)
        //AsyncOperation ao = SceneManager.LoadSceneAsync("GameScene");
        //AsyncOperation ao = SceneManager.LoadSceneAsync("SampleScene");
        yield return ao;
    }
}
