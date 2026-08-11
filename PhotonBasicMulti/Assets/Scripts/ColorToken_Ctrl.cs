using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

// 색상 선택 씬에서 플레이어 1명을 대표하는 네트워크 토큰.
// 별도의 이동/전투 로직 없이, 머리 위에 닉네임과 "현재 조작 중인 RGB 팔레트" 스와치만 보여준다.
public class ColorToken_Ctrl : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Components")]
    [SerializeField] private PhotonView pv;
    [SerializeField] private Text id;               // 머리 위 닉네임
    [SerializeField] private Image swatchImage;      // 머리 위 실시간 RGB 팔레트 스와치
    [SerializeField] private Text rgbValueText;      // 머리 위 RGB 수치 표시(다른 플레이어도 정확한 값을 알 수 있도록)
    [SerializeField] private GameObject swatchRoot;  // 조작 중이 아닐 때 꺼둘 오브젝트(스와치 배경 포함)

    bool m_IsAdjusting = false;                 // 로컬: 현재 슬라이더 조작 중인지
    Color32 m_PreviewColor = new Color32(255, 255, 255, 255);

    bool m_NetIsAdjusting = false;              // 원격: 수신받은 값
    Color32 m_NetPreviewColor = new Color32(255, 255, 255, 255);

    private void Awake()
    {
        if (pv.IsMine)
        {
            if (id != null)
                id.text = PhotonNetwork.LocalPlayer.NickName;
        }
    }

    private void Update()
    {
        bool adjusting = pv.IsMine ? m_IsAdjusting : m_NetIsAdjusting;
        Color32 color = pv.IsMine ? m_PreviewColor : m_NetPreviewColor;

        if (swatchRoot != null)
            swatchRoot.SetActive(adjusting);

        if (swatchImage != null)
            swatchImage.color = color;

        if (rgbValueText != null)
            rgbValueText.text = "R:" + color.r + " G:" + color.g + " B:" + color.b;
    }

    // RGBPalette_Ctrl에서 슬라이더 값이 바뀔 때마다(혹은 확정/타임아웃 시) 호출
    public void SetPreview(bool isAdjusting, Color32 color)
    {
        m_IsAdjusting = isAdjusting;
        m_PreviewColor = color;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(m_IsAdjusting);
            stream.SendNext(ColorSelect_Mgr.PackColor(m_PreviewColor.r, m_PreviewColor.g, m_PreviewColor.b));
        }
        else
        {
            m_NetIsAdjusting = (bool)stream.ReceiveNext();
            int packed = (int)stream.ReceiveNext();
            m_NetPreviewColor = ColorSelect_Mgr.UnpackColor(packed);
        }
    }
}
