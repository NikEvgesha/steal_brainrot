using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[DefaultExecutionOrder(-120)]
public class CameraTouchController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [SerializeField] private RectTransform _touchArea;
    [SerializeField, Range(0f, 1f)] private float _rightSideStart = 0.42f;
    [SerializeField] private float _dragSpeed = 0.035f;
    [SerializeField] private float _deadZone = 1f;
    [SerializeField] private float _smoothSpeed = 18f;
    [SerializeField] private float _pinchZoomSensitivity = 1.35f;
    [SerializeField] private bool _ignoreTouchesOverUI = true;

    private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>(16);
    private bool _isDragging;
    private Vector2 _lastPosition;
    private Vector2 _targetInput;
    private Vector2 _currentInput;
    private int _cameraTouchId = -1;
    private float _zoomInput;
    private bool _isPinching;

    private void Update()
    {
        _zoomInput = 0f;
        ProcessTouches();

        float blend = 1f - Mathf.Exp(-Mathf.Max(0.01f, _smoothSpeed) * Time.unscaledDeltaTime);
        _currentInput = Vector2.Lerp(_currentInput, _targetInput, blend);
        _targetInput = Vector2.zero;
    }

    private void ProcessTouches()
    {
        if (TryProcessPinch())
            return;

        if (_isPinching)
        {
            _isPinching = false;
            EndDrag();
            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);

            if (_cameraTouchId < 0)
            {
                if (touch.phase != TouchPhase.Began || touch.position.x < Screen.width * _rightSideStart)
                    continue;
                if (_ignoreTouchesOverUI && IsPointerBlockedByUI(touch.fingerId, touch.position))
                    continue;

                StartDrag(touch.position, touch.fingerId);
                continue;
            }

            if (touch.fingerId != _cameraTouchId)
                continue;

            if (touch.phase == TouchPhase.Moved)
                Drag(touch.position);
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                EndDrag();
        }
    }

    private bool TryProcessPinch()
    {
        Touch first = default;
        Touch second = default;
        int eligibleTouches = 0;
        float rightBoundary = Screen.width * _rightSideStart;

        for (int i = 0; i < Input.touchCount && eligibleTouches < 2; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                continue;
            if (touch.position.x < rightBoundary)
                continue;
            if (_ignoreTouchesOverUI && IsPointerBlockedByUI(touch.fingerId, touch.position))
                continue;

            if (eligibleTouches == 0)
                first = touch;
            else
                second = touch;
            eligibleTouches++;
        }

        if (eligibleTouches < 2)
            return false;

        if (!_isPinching)
        {
            EndDrag();
            _isPinching = true;
        }

        Vector2 firstPrevious = first.position - first.deltaPosition;
        Vector2 secondPrevious = second.position - second.deltaPosition;
        float previousDistance = Vector2.Distance(firstPrevious, secondPrevious);
        float currentDistance = Vector2.Distance(first.position, second.position);
        float screenReference = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height));
        _zoomInput = (currentDistance - previousDistance) / screenReference * _pinchZoomSensitivity;
        _targetInput = Vector2.zero;
        _currentInput = Vector2.zero;
        return true;
    }

    public void StartDrag(Vector3 startPos, int fingerId = -1)
    {
        if (_isDragging)
            return;

        _lastPosition = startPos;
        _isDragging = true;
        _cameraTouchId = fingerId;
    }

    public void Drag(Vector3 pointerPos)
    {
        if (!_isDragging)
            return;

        Vector2 currentPosition = pointerPos;
        Vector2 delta = currentPosition - _lastPosition;
        _lastPosition = currentPosition;

        if (delta.sqrMagnitude < _deadZone * _deadZone)
            return;

        _targetInput += delta * _dragSpeed;
    }

    public void EndDrag()
    {
        _isDragging = false;
        _targetInput = Vector2.zero;
        _currentInput = Vector2.zero;
        _cameraTouchId = -1;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_cameraTouchId < 0)
            StartDrag(eventData.position, eventData.pointerId);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId == _cameraTouchId)
            Drag(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId == _cameraTouchId)
            EndDrag();
    }

    public Vector2 GetRotationInput()
    {
        return _currentInput;
    }

    public float GetZoomInput()
    {
        return _zoomInput;
    }

    private bool IsPointerBlockedByUI(int pointerId, Vector2 position)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        var eventData = new PointerEventData(eventSystem)
        {
            pointerId = pointerId,
            position = position
        };

        _raycastResults.Clear();
        eventSystem.RaycastAll(eventData, _raycastResults);
        for (int i = 0; i < _raycastResults.Count; i++)
        {
            GameObject hitObject = _raycastResults[i].gameObject;
            if (hitObject == null)
                continue;
            if (_touchArea != null && hitObject.transform.IsChildOf(_touchArea))
                continue;
            if (hitObject.GetComponentInParent<Canvas>() != null)
                return true;
        }

        return false;
    }

    private void OnDisable()
    {
        _isPinching = false;
        _zoomInput = 0f;
        EndDrag();
    }
}
