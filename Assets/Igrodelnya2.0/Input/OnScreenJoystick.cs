using UnityEngine;
using UnityEngine.EventSystems;

public class OnScreenJoystick : MonoBehaviour, IDragHandler, IPointerUpHandler, IPointerDownHandler
{
    [SerializeField] private RectTransform inputArea;
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform knob;
    [SerializeField] private CanvasGroup visuals;
    [SerializeField] private float maxRadius = 92f;
    [SerializeField] private bool dynamicOrigin = true;
    [SerializeField] private bool returnToDefaultPosition = true;
    [SerializeField, Range(0f, 1f)] private float inactiveAlpha = 0.38f;
    [SerializeField, Range(0f, 1f)] private float activeAlpha = 0.82f;

    private const int NoPointer = int.MinValue;
    private Vector2 inputVector;
    private Vector2 defaultPosition;
    private int activePointerId = NoPointer;

    private void Awake()
    {
        if (inputArea == null)
            inputArea = transform as RectTransform;

        if (background != null)
            defaultPosition = background.anchoredPosition;

        SetVisualAlpha(inactiveAlpha);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointerId != NoPointer)
            return;

        activePointerId = eventData.pointerId;
        if (dynamicOrigin)
            MoveOrigin(eventData);

        SetVisualAlpha(activeAlpha);
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId || background == null || knob == null)
            return;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint
        );

        float radius = Mathf.Max(1f, maxRadius);
        localPoint = Vector2.ClampMagnitude(localPoint, radius);
        knob.anchoredPosition = localPoint;
        inputVector = localPoint / radius;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId)
            return;

        ResetJoystick();
    }

    public float Horizontal()
    {
        return inputVector.x;
    }

    public float Vertical()
    {
        return inputVector.y;
    }

    public Vector2 GetInputDirection()
    {
        return inputVector;
    }

    private void MoveOrigin(PointerEventData eventData)
    {
        if (inputArea == null || background == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                inputArea,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
        {
            return;
        }

        Rect areaRect = inputArea.rect;
        Vector2 halfSize = background.rect.size * 0.5f;
        localPoint.x = Mathf.Clamp(localPoint.x, areaRect.xMin + halfSize.x, areaRect.xMax - halfSize.x);
        localPoint.y = Mathf.Clamp(localPoint.y, areaRect.yMin + halfSize.y, areaRect.yMax - halfSize.y);

        Vector2 anchorReference = new Vector2(
            Mathf.Lerp(areaRect.xMin, areaRect.xMax, background.anchorMin.x),
            Mathf.Lerp(areaRect.yMin, areaRect.yMax, background.anchorMin.y));
        background.anchoredPosition = localPoint - anchorReference;
    }

    private void ResetJoystick()
    {
        activePointerId = NoPointer;
        inputVector = Vector2.zero;
        if (knob != null)
            knob.anchoredPosition = Vector2.zero;
        if (returnToDefaultPosition && background != null)
            background.anchoredPosition = defaultPosition;
        SetVisualAlpha(inactiveAlpha);
    }

    private void SetVisualAlpha(float alpha)
    {
        if (visuals != null)
            visuals.alpha = alpha;
    }

    private void OnDisable()
    {
        ResetJoystick();
    }
}
