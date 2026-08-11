using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 포토샵/엔진 에디터 스타일의 색상 선택 위젯.
// SV 박스 + 휴 슬라이더 + RGB 슬라이더/인풋 + HSV 슬라이더/인풋 + 헥스 인풋을 하나의 색상(Color32)으로 완전히 동기화한다.
// 게임/네트워크 로직을 전혀 모르는 순수 UI 컴포넌트 - 외부에서는 CurrentColor / SetColor / ColorChanged만 사용하면 된다.
public class ColorPickerUI : MonoBehaviour
{
    const int SV_TEX_SIZE = 64;  // SV 박스 텍스처 해상도 (조작감에 비해 과도하게 크지 않게)
    const int HUE_TEX_SIZE = 64; // 휴 슬라이더 그라데이션 텍스처 해상도

    [Header("SV Box")]
    [SerializeField] private RawImage svBoxImage;
    [SerializeField] private SVBoxDragHandler svBoxDrag;
    [SerializeField] private RectTransform svPointer;

    [Header("Hue Slider (Vertical, 0~360)")]
    [SerializeField] private Slider sliderHue;
    [SerializeField] private Image hueSliderBackground;

    [Header("Preview")]
    [SerializeField] private Image previewImage;

    [Header("RGB (0~255)")]
    [SerializeField] private Slider sliderR;
    [SerializeField] private Slider sliderG;
    [SerializeField] private Slider sliderB;
    [SerializeField] private TMP_InputField inputR;
    [SerializeField] private TMP_InputField inputG;
    [SerializeField] private TMP_InputField inputB;

    [Header("HSV 수치 (H:0~360, S/V:0~100)")]
    [SerializeField] private TMP_InputField inputH;
    [SerializeField] private Slider sliderS;
    [SerializeField] private TMP_InputField inputS;
    [SerializeField] private Slider sliderV;
    [SerializeField] private TMP_InputField inputV;

    [Header("Hex (#RRGGBB)")]
    [SerializeField] private TMP_InputField inputHex;

    public event Action<Color32> ColorChanged;

    public Color32 CurrentColor { get { return m_Color; } }

    Color32 m_Color = new Color32(128, 128, 128, 255);
    float m_H, m_S, m_V; // 0~1 정규화 값 (내부 정본)

    Texture2D m_SVTexture;
    float m_SVTextureHue = -1f; // 마지막으로 SV 텍스처를 생성한 휴 값 (변화 없으면 재생성 생략)

    bool m_bLocked = false; // 확정 후 조작 잠금

    private void Awake()
    {
        GenerateHueBackground();
    }

    private void Start()
    {
        if (svBoxDrag != null) svBoxDrag.Dragged += OnSVBoxDragged;

        if (sliderHue != null) sliderHue.onValueChanged.AddListener(_ => OnHueSliderChanged());
        if (sliderS != null) sliderS.onValueChanged.AddListener(_ => OnSVSliderChanged());
        if (sliderV != null) sliderV.onValueChanged.AddListener(_ => OnSVSliderChanged());

        if (sliderR != null) sliderR.onValueChanged.AddListener(_ => OnRGBSliderChanged());
        if (sliderG != null) sliderG.onValueChanged.AddListener(_ => OnRGBSliderChanged());
        if (sliderB != null) sliderB.onValueChanged.AddListener(_ => OnRGBSliderChanged());

        if (inputR != null) inputR.onValueChanged.AddListener(_ => OnRGBInputChanged(inputR, sliderR));
        if (inputG != null) inputG.onValueChanged.AddListener(_ => OnRGBInputChanged(inputG, sliderG));
        if (inputB != null) inputB.onValueChanged.AddListener(_ => OnRGBInputChanged(inputB, sliderB));

        if (inputH != null) inputH.onValueChanged.AddListener(_ => OnHueInputChanged());
        if (inputS != null) inputS.onValueChanged.AddListener(_ => OnSVInputChanged(inputS, sliderS));
        if (inputV != null) inputV.onValueChanged.AddListener(_ => OnSVInputChanged(inputV, sliderV));

        if (inputHex != null) inputHex.onValueChanged.AddListener(_ => OnHexInputChanged());

        ApplyFromRGB(m_Color.r, m_Color.g, m_Color.b, true);
    }

    private void OnDestroy()
    {
        if (svBoxDrag != null) svBoxDrag.Dragged -= OnSVBoxDragged;

        if (m_SVTexture != null)
            Destroy(m_SVTexture);
    }

    //--- 외부 공개 API ------------------------------------------------------

    // 외부(라운드 초기화 등)에서 강제로 색상을 지정할 때 사용. 잠금 상태와 무관하게 항상 적용된다.
    public void SetColor(Color32 color)
    {
        ApplyFromRGB(color.r, color.g, color.b, true);
    }

    // 확정 후에는 더 이상 조작할 수 없도록 잠그거나(false), 새 라운드 시작 시 다시 풀어준다(true).
    public void SetInteractable(bool interactable)
    {
        m_bLocked = !interactable;

        SetSelectableInteractable(sliderHue, interactable);
        SetSelectableInteractable(sliderR, interactable);
        SetSelectableInteractable(sliderG, interactable);
        SetSelectableInteractable(sliderB, interactable);
        SetSelectableInteractable(sliderS, interactable);
        SetSelectableInteractable(sliderV, interactable);
        SetSelectableInteractable(inputR, interactable);
        SetSelectableInteractable(inputG, interactable);
        SetSelectableInteractable(inputB, interactable);
        SetSelectableInteractable(inputH, interactable);
        SetSelectableInteractable(inputS, interactable);
        SetSelectableInteractable(inputV, interactable);
        SetSelectableInteractable(inputHex, interactable);

        if (svBoxDrag != null)
            svBoxDrag.enabled = interactable;
    }

    private void SetSelectableInteractable(Selectable s, bool interactable)
    {
        if (s != null) s.interactable = interactable;
    }

    //--- SV 박스 / 휴 슬라이더 입력 -------------------------------------------

    private void OnSVBoxDragged(float s, float v)
    {
        if (m_bLocked) return;
        ApplyFromHSV(m_H, s, v);
    }

    private void OnHueSliderChanged()
    {
        if (m_bLocked) return;
        ApplyFromHSV(sliderHue.value / 360.0f, m_S, m_V);
    }

    private void OnHueInputChanged()
    {
        if (m_bLocked) return;

        int value;
        if (!int.TryParse(inputH.text, out value))
            return;

        value = Mathf.Clamp(value, 0, 360);
        ApplyFromHSV(value / 360.0f, m_S, m_V);
    }

    private void OnSVSliderChanged()
    {
        if (m_bLocked) return;
        ApplyFromHSV(m_H, sliderS.value / 100.0f, sliderV.value / 100.0f);
    }

    private void OnSVInputChanged(TMP_InputField input, Slider slider)
    {
        if (m_bLocked) return;

        int value;
        if (!int.TryParse(input.text, out value))
            return;

        value = Mathf.Clamp(value, 0, 100);
        slider.value = value; // Slider.onValueChanged -> OnSVSliderChanged로 이어져 자동 반영
    }

    //--- RGB 슬라이더 / 인풋 입력 --------------------------------------------

    private void OnRGBSliderChanged()
    {
        if (m_bLocked) return;

        byte r = (byte)sliderR.value;
        byte g = (byte)sliderG.value;
        byte b = (byte)sliderB.value;
        ApplyFromRGB(r, g, b, false);
    }

    private void OnRGBInputChanged(TMP_InputField input, Slider slider)
    {
        if (m_bLocked) return;

        int value;
        if (!int.TryParse(input.text, out value))
            return;

        value = Mathf.Clamp(value, 0, 255);
        slider.value = value; // Slider.onValueChanged -> OnRGBSliderChanged로 이어져 자동 반영
    }

    //--- 헥스 코드 입력 ------------------------------------------------------

    private void OnHexInputChanged()
    {
        if (m_bLocked) return;

        Color parsed;
        if (!ColorUtility.TryParseHtmlString("#" + inputHex.text, out parsed))
            return; // 6자리가 채워지기 전(타이핑 도중)에는 그냥 무시

        Color32 c32 = parsed;
        ApplyFromRGB(c32.r, c32.g, c32.b, false);
    }

    //--- 색상 갱신 공용 진입점 ------------------------------------------------

    // RGB 값이 기준(슬라이더/인풋/헥스/SetColor)일 때: RGB로부터 HSV를 새로 계산한다.
    private void ApplyFromRGB(byte r, byte g, byte b, bool refreshHex)
    {
        m_Color = new Color32(r, g, b, 255);
        Color.RGBToHSV(m_Color, out m_H, out m_S, out m_V);

        RefreshAllUI();
        RaiseColorChanged();
    }

    // HSV 값이 기준(SV박스/휴슬라이더/HSV인풋)일 때: HSV를 그대로 유지한 채 RGB만 재계산한다.
    // (RGB로 왕복 변환하면 바이트 양자화 때문에 슬라이더가 미세하게 튀는 문제가 생겨 HSV값 자체는 보존한다)
    private void ApplyFromHSV(float h, float s, float v)
    {
        h = Mathf.Repeat(h, 1.0f);
        s = Mathf.Clamp01(s);
        v = Mathf.Clamp01(v);

        m_H = h; m_S = s; m_V = v;
        m_Color = Color.HSVToRGB(m_H, m_S, m_V);
        m_Color.a = 255;

        RefreshAllUI();
        RaiseColorChanged();
    }

    private void RaiseColorChanged()
    {
        ColorChanged?.Invoke(m_Color);
    }

    //--- UI 동기화(이벤트 재발화 없이) ----------------------------------------

    private void RefreshAllUI()
    {
        if (previewImage != null)
            previewImage.color = m_Color;

        if (sliderR != null) sliderR.SetValueWithoutNotify(m_Color.r);
        if (sliderG != null) sliderG.SetValueWithoutNotify(m_Color.g);
        if (sliderB != null) sliderB.SetValueWithoutNotify(m_Color.b);
        if (inputR != null) inputR.SetTextWithoutNotify(m_Color.r.ToString());
        if (inputG != null) inputG.SetTextWithoutNotify(m_Color.g.ToString());
        if (inputB != null) inputB.SetTextWithoutNotify(m_Color.b.ToString());

        int hDeg = Mathf.RoundToInt(m_H * 360.0f);
        int sPct = Mathf.RoundToInt(m_S * 100.0f);
        int vPct = Mathf.RoundToInt(m_V * 100.0f);

        if (sliderHue != null) sliderHue.SetValueWithoutNotify(hDeg);
        if (inputH != null) inputH.SetTextWithoutNotify(hDeg.ToString());
        if (sliderS != null) sliderS.SetValueWithoutNotify(sPct);
        if (inputS != null) inputS.SetTextWithoutNotify(sPct.ToString());
        if (sliderV != null) sliderV.SetValueWithoutNotify(vPct);
        if (inputV != null) inputV.SetTextWithoutNotify(vPct.ToString());

        if (inputHex != null) inputHex.SetTextWithoutNotify(ColorUtility.ToHtmlStringRGB(m_Color));

        UpdateSVPointer();
        UpdateSVTexture();
    }

    private void UpdateSVPointer()
    {
        if (svPointer == null || svBoxImage == null)
            return;

        Rect r = svBoxImage.rectTransform.rect;
        float x = Mathf.Lerp(r.xMin, r.xMax, m_S);
        float y = Mathf.Lerp(r.yMin, r.yMax, m_V);
        svPointer.anchoredPosition = new Vector2(x, y);
    }

    //--- 텍스처 생성 (런타임 절차적 생성 - 별도 이미지 에셋 불필요) --------------

    private void GenerateHueBackground()
    {
        if (hueSliderBackground == null)
            return;

        Texture2D tex = new Texture2D(1, HUE_TEX_SIZE, TextureFormat.RGB24, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < HUE_TEX_SIZE; y++)
        {
            float h = (float)y / (HUE_TEX_SIZE - 1);
            tex.SetPixel(0, y, Color.HSVToRGB(h, 1f, 1f));
        }
        tex.Apply();

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 1, HUE_TEX_SIZE), new Vector2(0.5f, 0.5f));
        hueSliderBackground.sprite = sprite;
        hueSliderBackground.type = Image.Type.Simple;
    }

    private void UpdateSVTexture()
    {
        if (svBoxImage == null)
            return;

        if (Mathf.Approximately(m_SVTextureHue, m_H))
            return; // 휴가 바뀌지 않았으면 매 프레임 재생성할 필요 없음

        m_SVTextureHue = m_H;

        if (m_SVTexture == null)
        {
            m_SVTexture = new Texture2D(SV_TEX_SIZE, SV_TEX_SIZE, TextureFormat.RGB24, false);
            m_SVTexture.wrapMode = TextureWrapMode.Clamp;
            svBoxImage.texture = m_SVTexture;
        }

        for (int y = 0; y < SV_TEX_SIZE; y++)
        {
            float v = (float)y / (SV_TEX_SIZE - 1);
            for (int x = 0; x < SV_TEX_SIZE; x++)
            {
                float s = (float)x / (SV_TEX_SIZE - 1);
                m_SVTexture.SetPixel(x, y, Color.HSVToRGB(m_H, s, v));
            }
        }
        m_SVTexture.Apply();
    }
}
