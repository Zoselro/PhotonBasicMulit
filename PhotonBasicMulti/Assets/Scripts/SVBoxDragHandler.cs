using System;
using UnityEngine;
using UnityEngine.EventSystems;

// SV(채도/명도) 박스 위에서의 포인터 클릭/드래그를 박스 기준 정규화 좌표(0~1)로 변환해 전달한다.
public class SVBoxDragHandler : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [SerializeField] private RectTransform box; // 좌표 계산 기준이 되는 박스(RawImage)의 RectTransform

    public event Action<float, float> Dragged; // (s, v) 각각 0~1

    public void OnPointerDown(PointerEventData eventData)
    {
        Report(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Report(eventData);
    }

    private void Report(PointerEventData eventData)
    {
        if (box == null)
            return;

        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(box, eventData.position, eventData.pressEventCamera, out local);

        Rect r = box.rect;
        float s = Mathf.InverseLerp(r.xMin, r.xMax, local.x);
        float v = Mathf.InverseLerp(r.yMin, r.yMax, local.y);

        Dragged?.Invoke(Mathf.Clamp01(s), Mathf.Clamp01(v));
    }
}
