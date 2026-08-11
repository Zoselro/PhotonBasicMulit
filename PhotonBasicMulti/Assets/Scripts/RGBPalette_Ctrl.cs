using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 로컬 플레이어의 색상 선택 라운드 진행 담당.
// 실제 색상 조작 UI(SV박스/휴/RGB/HSV/헥스)는 ColorPickerUI가 전담하고,
// 여기서는 라운드 타이머 표시 / 확인·타임아웃 커밋 / 머리 위 토큰·하단 슬롯 동기화만 처리한다.
public class RGBPalette_Ctrl : MonoBehaviour
{
    [Header("Color Picker")]
    [SerializeField] private ColorPickerUI colorPicker;

    [Header("UI")]
    [SerializeField] private Button confirmBtn;
    [SerializeField] private TextMeshProUGUI txtCountdown;
    [SerializeField] private TextMeshProUGUI txtRoundInfo;
    [SerializeField] private ColorSlotHUD_Ctrl slotHud; // 확정 전 미리보기를 하단 슬롯에도 반영하기 위함

    ColorToken_Ctrl m_MyToken; // 로컬 플레이어의 색상 토큰(늦게 스폰되므로 지연 탐색)

    int m_LastSeenRound = -1;        // 라운드 전환 감지용
    bool m_bConfirmedThisRound = false; // 같은 라운드 내 중복 커밋(버튼+타임아웃 동시 발동) 방지 래치

    private void Start()
    {
        if (confirmBtn != null)
            confirmBtn.onClick.AddListener(OnClickConfirm);

        if (colorPicker != null)
            colorPicker.ColorChanged += OnPickerColorChanged;
    }

    private void OnDestroy()
    {
        if (colorPicker != null)
            colorPicker.ColorChanged -= OnPickerColorChanged;
    }

    private void Update()
    {
        if (!PhotonNetwork.InRoom)
            return;

        if (m_MyToken == null)
        {
            FindMyToken();
            return;
        }

        int roundIdx = ColorSelect_Mgr.GetRoundIdx();
        if (roundIdx != m_LastSeenRound)
        {
            OnRoundChanged(roundIdx);
        }

        UpdateCountdownUI();
        CheckLocalTimeout(roundIdx);
    }

    private void FindMyToken()
    {
        ColorToken_Ctrl[] tokens = FindObjectsByType<ColorToken_Ctrl>(FindObjectsSortMode.None);
        for (int i = 0; i < tokens.Length; i++)
        {
            if (tokens[i].photonView.IsMine)
            {
                m_MyToken = tokens[i];
                return;
            }
        }
    }

    private void OnRoundChanged(int roundIdx)
    {
        m_LastSeenRound = roundIdx;
        m_bConfirmedThisRound = false;

        // 매 라운드 중립 회색(128,128,128)에서 시작 - 유저가 직접 조작해야 색이 바뀌도록 함
        // (시작값을 랜덤으로 주면 손대지 않아도 이미 색이 정해진 것처럼 보여 혼동을 준다)
        if (colorPicker != null)
        {
            colorPicker.SetColor(new Color32(128, 128, 128, 255));
            colorPicker.SetInteractable(true);
        }

        if (confirmBtn != null)
            confirmBtn.interactable = true;

        if (txtRoundInfo != null)
            txtRoundInfo.text = (roundIdx + 1) + " / " + ColorSelect_Mgr.ROUND_COUNT + " 라운드";
    }

    // ColorPickerUI를 휠/슬라이더/인풋/헥스 중 무엇으로 조작하든 전부 이 한 곳으로 모여든다.
    private void OnPickerColorChanged(Color32 c)
    {
        if (m_bConfirmedThisRound)
            return;

        if (m_MyToken != null)
            m_MyToken.SetPreview(true, c); // 조작 중임을 머리 위 팔레트에 실시간 반영

        if (slotHud != null)
            slotHud.SetLivePreview(m_LastSeenRound, c); // 하단 슬롯에도 확정 전 미리보기 반영
    }

    private void UpdateCountdownUI()
    {
        double remain = ColorSelect_Mgr.GetRoundEndTime() - PhotonNetwork.Time;
        if (remain < 0.0) remain = 0.0;

        if (txtCountdown != null)
            txtCountdown.text = Mathf.CeilToInt((float)remain).ToString();
    }

    private void CheckLocalTimeout(int roundIdx)
    {
        if (m_bConfirmedThisRound)
            return;

        if (PhotonNetwork.Time < ColorSelect_Mgr.GetRoundEndTime())
            return;

        // 시간 초과 - 현재 조작 중이던 색상 그대로 자동 확정(더 이상 새 랜덤값을 뽑지 않는다)
        Color32 current = colorPicker != null ? colorPicker.CurrentColor : new Color32(128, 128, 128, 255);
        CommitColor(roundIdx, current);
    }

    private void OnClickConfirm()
    {
        if (m_bConfirmedThisRound)
            return;

        Color32 current = colorPicker != null ? colorPicker.CurrentColor : new Color32(128, 128, 128, 255);
        CommitColor(m_LastSeenRound, current);
    }

    private void CommitColor(int roundIdx, Color32 color)
    {
        m_bConfirmedThisRound = true;

        ColorSelect_Mgr.CommitOwnColor(roundIdx, color);

        if (m_MyToken != null)
            m_MyToken.SetPreview(false, color); // 확정 후에는 머리 위 조작 연출을 끔

        if (confirmBtn != null)
            confirmBtn.interactable = false;

        if (colorPicker != null)
            colorPicker.SetInteractable(false);
    }
}
