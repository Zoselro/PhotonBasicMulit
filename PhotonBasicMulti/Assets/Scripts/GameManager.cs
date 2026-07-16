using Photon.Pun;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviourPunCallbacks
{
    const int MAX_CHAT = 50; // 채팅 최대 갯수

    [SerializeField] private PhotonView pv;
    [SerializeField] private Button m_BackBtn; // 룸 나가기 버튼
    [SerializeField] private TMP_InputField InputFdChat; // 채팅 입력 필드

    [SerializeField] TextMeshProUGUI txtLogMsg;

    private List<string> m_MsgList = new List<string>();
    private bool bEnter = false;

    private void Start()
    {
        Time.timeScale = 1.0f; // 일시정지 풀어주기
        PhotonNetwork.IsMessageQueueRunning = true; // 포톤 메시지 큐를 활성화하여 RPC 호출을 받을 수 있도록 설정

        Debug.Log(PhotonNetwork.IsMessageQueueRunning);
        if (m_BackBtn != null)
            m_BackBtn.onClick.AddListener(OnClickBackBtn);

        // 로그 메시지에 출력할 문자열 생성
        string msg = "\n<color=#33ff33>[" +
                        PhotonNetwork.LocalPlayer.NickName +
                        "] Connected</color>";

        pv.RPC("LogMsg", RpcTarget.AllBuffered, msg, false);
    }

    private void Update()
    {
        //--- 채팅 구현 텍스트
        if (Input.GetKeyUp(KeyCode.Return))
        {// 엔터키를 누르면 인풋 필드 활성화
            bEnter = !bEnter;

            if (bEnter)
            {
                InputFdChat.gameObject.SetActive(true);
                InputFdChat.ActivateInputField(); // <--- 키보드 커서 입력 상자 쪽으로 가게 만들어 줌
            }
            else
            {
                InputFdChat.gameObject.SetActive(false);
                if (!string.IsNullOrEmpty(InputFdChat.text.Trim()))
                {
                    BroadcastingChat();
                }
            }
        }
    }

    // --- Back Button 처리 함수 (룸 나가기 버턴)
    public void OnClickBackBtn()
    {

        if (m_BackBtn != null) m_BackBtn.interactable = false;

        // 로그 메시지에 출력할 문자열 생성
        string msg = "\n<color=#ff0000>]" +
                    PhotonNetwork.LocalPlayer.NickName +
                    "] 방 나감</color>";

        // 마지막 사람이 방을 떠날 때 룸의 CustomProerties를 초기화 해줘야 한다.
        if (PhotonNetwork.PlayerList != null && PhotonNetwork.PlayerList.Length <= 1)
        {
            Debug.Log("마지막 사람이 방 나감");
            if (PhotonNetwork.CurrentRoom != null)
            {
                PhotonNetwork.CurrentRoom.CustomProperties.Clear();
                Debug.Log("방의 CustomProperties 초기화 완료!");
            }
        }
        //RPC 함수 호출
        pv.RPC("LogMsg", RpcTarget.AllBuffered, msg, false);

        // 지금 나가려는 유저를 찾아서 그 유저의
        // 모든 CustomProperties를 초기화 해 주고 나가는 것이 좋다.
        // 그렇지 않으면 나갔다 즉시 방 입장시 오류가 발생한다.
        if (PhotonNetwork.LocalPlayer != null)
        {
            PhotonNetwork.LocalPlayer.CustomProperties.Clear();
            Debug.Log("나가는 유저의 CustomProperties 초기화 완료!");
        }
        // 그래야 중계되던 것이 모두 초기화 될 것이다.

        Debug.Log("방 나가기 버튼 클릭!");

        // 현재 룸을 빠져나가며 생성한 모든 네트워크 객체를 삭제
        PhotonNetwork.LeaveRoom(); // 포톤 방을 빠져나간다.
        Debug.Log("PhotonNetwork.LeaveRoom() 호출 완료!");
    }

    // 룸에서 접속 종료 되었을 때 호출되는 콜백 함수
    // LeaveRoom을 호출 되었을 때 호출되는 콜백 함수
    public override void OnLeftRoom()
    {
        Debug.Log("방 나가기 완료! OnLoeftRoom 콜백함수 호출!");
        //Time.timeScale = 1.0f; // 일시정지 풀어주기
        SceneManager.LoadScene("PhotonLobby"); // 로비씬으로 이동
    }

    // 중계 하기 위함
    [PunRPC]
    private void LogMsg(string msg, bool isChatMsg, PhotonMessageInfo info)
    {
        //로컬에서 내가 보낸 메시지인 경우만
        //채팅 메시지인지?
        //info.Sender.IsLocal == true // 로컬에서 보낸 메시지
        //info.Sender.IsLocal == false // PhotonNetwork.LocalPlayer.ActorNumber(IsMine의 고유번호)
        if (info.Sender.IsLocal == true && isChatMsg == true)
        {
            // 방장이 말을 한 경우는 "#00ffff"로 들어 오니까 방장이 한 말은 자신도 그냥 하늘 색으로 보일 것
            msg = msg.Replace("#ffffff", "#ffff00"); // 문자열을 찾아서, 바꿔주는 역할
        }

        m_MsgList.Add(msg);

        if(m_MsgList.Count > MAX_CHAT)
        {
            m_MsgList.RemoveAt(0);
        }

        // 로그 메시지 Text UI에 텍스트를 누적시켜 표시
        txtLogMsg.text = "";
        for (int i = 0; i < m_MsgList.Count; i++)
        {
            txtLogMsg.text += m_MsgList[i];
        }
    }

    //채팅 내용을 중계하는 함수
    private void BroadcastingChat()
    {
        if (!PhotonNetwork.InRoom)
        {
            return;
        }

        string msg = "\n<color=#ffffff>[" +
                    PhotonNetwork.LocalPlayer.NickName + "] " +
                    InputFdChat.text + "</color>";

        pv.RPC("LogMsg", RpcTarget.AllBuffered, msg, true);

        InputFdChat.text = "";
    }
}
