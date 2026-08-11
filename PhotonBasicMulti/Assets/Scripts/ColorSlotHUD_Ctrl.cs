using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

// 화면 하단 4개 슬롯 HUD. 로컬 플레이어 전용(네트워크 아님).
// 라운드가 확정될 때마다 Player CustomProperties(pClr)를 읽어 1~4번 슬롯을 순서대로 채운다.
// 아직 확정 전인 현재 라운드 슬롯은, 조작 중인 실시간 색상을 미리 보여준다(RGBPalette_Ctrl이 전달).
public class ColorSlotHUD_Ctrl : MonoBehaviour
{
    [SerializeField] private Image[] slotImages; // 1~4번 슬롯 (순서대로)

    int m_LivePreviewRound = -1;
    Color32 m_LivePreviewColor;

    private void Update()
    {
        if (!PhotonNetwork.InRoom || slotImages == null)
            return;

        int[] colors = ColorSelect_Mgr.GetPlayerColors(PhotonNetwork.LocalPlayer);

        for (int i = 0; i < slotImages.Length && i < colors.Length; i++)
        {
            if (slotImages[i] == null)
                continue;

            if (colors[i] < 0 && i == m_LivePreviewRound)
                slotImages[i].color = m_LivePreviewColor; // 미확정이지만 현재 조작 중인 색상을 미리 반영
            else
                slotImages[i].color = ColorSelect_Mgr.UnpackColor(colors[i]); // 미확정(-1)이면 흰색
        }
    }

    // RGBPalette_Ctrl에서 색상 조작 중일 때마다 호출
    public void SetLivePreview(int roundIdx, Color32 color)
    {
        m_LivePreviewRound = roundIdx;
        m_LivePreviewColor = color;
    }
}
