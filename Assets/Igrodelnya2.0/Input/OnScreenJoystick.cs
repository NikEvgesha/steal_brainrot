using System.Collections.Generic;
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
    [SerializeField] private bool ignoreTouchesOverUI = true;

    private const int NoPointer = int.MinValue;
    private const int MousePointer = -1;
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>(16);
    private Vector2 inputVector;
    private Vector2 defaultPosition;
    private int activePointerId = NoPointer;
    private Canvas parentCanvas;

    private void Awake()
    {
        if (inputArea == null)
            inputArea = transform as RectTransform;

        if (background != null)
            defaultPosition = background.anchoredPosition;

        parentCanvas = GetComponentInParent<Canvas>();
        SetVisualAlpha(inactiveAlpha);
    }

    private void Update()
    {
        if (Input.touchCount > 0)
        {
            ProcessTouches();
            return;
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        ProcessMouseFallback();
#endif
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointerId != NoPointer)
            return;
        if (ignoreTouchesOverUI && IsBlockedByOtherUI(eventData.pointerId, eventData.position))
            return;

        StartJoystick(eventData.position, eventData.pointerId, eventData.pressEventCamera);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId || background == null || knob == null)
            return;

        UpdateJoystick(eventData.position, eventData.pressEventCamera);
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

    private void ProcessTouches()
    {
        Camera eventCamera = ResolveEventCamera();

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);

            if (activePointerId == NoPointer)
            {
                if (touch.phase != TouchPhase.Began || !IsInsideInputArea(touch.position, eventCamera))
                    continue;
                if (ignoreTouchesOverUI && IsBlockedByOtherUI(touch.fingerId, touch.position))
                    continue;

                StartJoystick(touch.position, touch.fingerId, eventCamera);
                continue;
            }

            if (touch.fingerId != activePointerId)
                continue;

            if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                UpdateJoystick(touch.position, eventCamera);
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                ResetJoystick();
        }
    }

    private void ProcessMouseFallback()
    {
        Vector2 mousePosition = Input.mousePosition;
        if (!IsFinite(mousePosition))
            return;

        Camera eventCamera = ResolveEventCamera();
        if (activePointerId == NoPointer && Input.GetMouseButtonDown(0))
        {
            if (!IsInsideInputArea(mousePosition, eventCamera))
                return;
            if (ignoreTouchesOverUI && IsBlockedByOtherUI(MousePointer, mousePosition))
                return;

            StartJoystick(mousePosition, MousePointer, eventCamera);
        }
        else if (activePointerId == MousePointer && Input.GetMouseButton(0))
        {
            UpdateJoystick(mousePosition, eventCamera);
        }
        else if (activePointerId == MousePointer && Input.GetMouseButtonUp(0))
        {
            ResetJoystick();
        }
    }

    private void StartJoystick(Vector2 screenPosition, int pointerId, Camera eventCamera)
    {
        activePointerId = pointerId;
        if (dynamicOrigin)
            MoveOrigin(screenPosition, eventCamera);

        SetVisualAlpha(activeAlpha);
        UpdateJoystick(screenPosition, eventCamera);
    }

    private void UpdateJoystick(Vector2 screenPosition, Camera eventCamera)
    {
        if (background == null || knob == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                background,
                screenPosition,
                eventCamera,
                out Vector2 localPoint))
        {
            return;
        }

        float radius = Mathf.Max(1f, maxRadius);
        localPoint = Vector2.ClampMagnitude(localPoint, radius);
        knob.anchoredPosition = localPoint;
        inputVector = localPoint / radius;
    }

    private void MoveOrigin(Vector2 screenPosition, Camera eventCamera)
    {
        if (inputArea == null || background == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                inputArea,
                screenPosition,
                eventCamera,
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

    private bool IsInsideInputArea(Vector2 screenPosition, Camera eventCamera)
    {
        return inputArea != null &&
               RectTransformUtility.RectangleContainsScreenPoint(inputArea, screenPosition, eventCamera);
    }

    private bool IsBlockedByOtherUI(int pointerId, Vector2 position)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        var eventData = new PointerEventData(eventSystem)
        {
            pointerId = pointerId,
            position = position
        };

        raycastResults.Clear();
        eventSystem.RaycastAll(eventData, raycastResults);
        for (int i = 0; i < raycastResults.Count; i++)
        {
            Transform hit = raycastResults[i].gameObject != null
                ? raycastResults[i].gameObject.transform
                : null;
            if (hit == null || hit == transform || hit.IsChildOf(transform))
                continue;
            if (hit.GetComponentInParent<Canvas>() != null)
                return true;
        }

        return false;
    }

    private Camera ResolveEventCamera()
    {
        if (parentCanvas == null)
            parentCanvas = GetComponentInParent<Canvas>();

        return parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? parentCanvas.worldCamera
            : null;
    }

    private static bool IsFinite(Vector2 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y);
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
