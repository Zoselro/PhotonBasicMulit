using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

public class RoomItem : MonoBehaviour
{
    // 외부 접근을 위한 public 변수들...
    [HideInInspector] public string roomName = "";
    [HideInInspector] public int connectPlayer = 0;
    [HideInInspector] public int maxPlayers = 0;

    //룸 이름 표시할 Text UI 항목
    public Text textRoomName;
    // 룸 접속자 수와 최대 접속자 수를 표시할 Text UI 항목
    public Text textConnectInfo;

    [HideInInspector] public string ReadyState = ""; // 레디 상태 표시 -> 게임을 시작하거나, 방이 가득 찼을 경우, 표시해주기 위함

    private void Start()
    {
        this.GetComponent<Button>().onClick.AddListener(() =>
        {
            // 방 입장 시도
            PhotonInit refPtnInit = FindFirstObjectByType<PhotonInit>();
            if (refPtnInit != null)
            {
                refPtnInit.OnClickRoomItem(roomName);
            }
        });
    }

    public void DispRoomData(bool isOpen)
    {
        if (isOpen)
        {
            textRoomName.color = new Color32(0, 0, 0, 255);
            textConnectInfo.color = new Color32(0, 0, 0, 255); // Black
        }
        else
        {
            textRoomName.color = new Color32(0, 0, 255, 255); // Blue
            textConnectInfo.color = new Color32(0, 0, 255, 255);
        }

        textRoomName.text = roomName;
        textConnectInfo.text = "(" + connectPlayer.ToString() + "/" + maxPlayers.ToString() + ")"; // 방 인원수 표시
    }

    private void OnGUI()
    {
        string str = PhotonNetwork.NetworkClientState.ToString();
        // 현재 포톤상태를 string으로 리턴 해 주는 함수
        GUI.Label(new Rect(10, 1, 1500, 60),
            "<color=#00ff00><size=35>" + str + "</size></color>");
    }
}
