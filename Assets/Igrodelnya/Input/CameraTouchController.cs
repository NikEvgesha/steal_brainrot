using UnityEngine;
using UnityEngine.EventSystems;

public class CameraTouchController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler { 
    [SerializeField] private RectTransform _touchArea;
    [SerializeField] private float _dragSpeed = 0.1f;
    [SerializeField] private float _deadZone = 2f;

    [SerializeField] private float _smoothSpeed = 10f; // Скорость сглаживания

    private bool _isDragging;
    private Vector2 _startPos;
    private Vector2 _input;

    private Vector2 _targetInput; // Целевое значение ввода
    private Vector2 _currentInput; // Текущее сглаженное значение
    private int _cameraTouchId = -1;

    private void Update()
    {
        // Сглаживание ввода
        _currentInput = Vector2.Lerp(_currentInput, _targetInput, Time.deltaTime * _smoothSpeed);

        // Обрабатываем касания
        foreach (Touch touch in Input.touches)
        {
            // Пропускаем, если это не касание камеры
            if (touch.fingerId != _cameraTouchId)
                continue;

            // Проверяем, не над UI ли касание
            if (EventSystem.current.IsPointerOverGameObject(touch.fingerId))
            {
                Debug.Log("Pointer over UI");
                EndDrag();
                continue;
            }

            if (touch.phase == TouchPhase.Began && !_isDragging)
            {
                Debug.Log("Begin drag");
                StartDrag(touch.position, touch.fingerId);
            }
            else if (touch.phase == TouchPhase.Moved && _isDragging)
            {
                Debug.Log("move drag");
                Drag(touch.position);
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                Debug.Log("end drag");
                EndDrag();
            }
        }
    }

    public void StartDrag(Vector3 startPos,  int fingerId = -1)
    {
        _startPos = startPos;
        _isDragging = true;
        if (fingerId != -1)
            _cameraTouchId = fingerId; // Сохраняем ID касания
    }

    public void Drag(Vector3 pointerPos)
    {
        if (!_isDragging) return;

        Vector2 currentPos = pointerPos;
        Vector2 delta = currentPos - _startPos;

        if (delta.magnitude < _deadZone)
        {
            delta = Vector2.zero;
        }

        _input = new Vector2(delta.x, delta.y) * _dragSpeed;
        _targetInput = _input; // Обновляем целевое значение
        _startPos = currentPos;
    }

    public void EndDrag()
    {
        _isDragging = false;
        _input = Vector2.zero;
        _targetInput = Vector2.zero;
        _cameraTouchId = -1;
    }



    public void OnPointerDown(PointerEventData eventData)
    {
        // Create a list to store raycast results
        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        RaycastResult? res = null;
        // Check each result
        foreach (var result in results)
        {
            // If we hit any UI element that's not part of world space canvas
            if (result.gameObject.GetComponentInParent<Canvas>()?.renderMode == RenderMode.WorldSpace)
            {
                res = result;
                break;
            }
        }

        if (res.HasValue)
        {
            BuyTouchHandler handler = res.Value.gameObject.GetComponentInParent<BuyTouchHandler>();
            if (handler)
            {
                handler.OnPointerDown(eventData);
            }
        } else
        {
            if (_cameraTouchId == -1)
            {
                StartDrag(eventData.position);
            }
        }

            
    }

    public void OnDrag(PointerEventData eventData)
    {
        Drag(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // Create a list to store raycast results
        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        RaycastResult? res = null;
        // Check each result
        foreach (var result in results)
        {
            // If we hit any UI element that's not part of world space canvas
            if (result.gameObject.GetComponentInParent<Canvas>()?.renderMode == RenderMode.WorldSpace)
            {
                res = result;
                break;
            }
        }

        if (res.HasValue)
        {
            BuyTouchHandler handler = res.Value.gameObject.GetComponentInParent<BuyTouchHandler>();
            if (handler)
            {
                handler.OnPointerUp(eventData);
            }
        }
        else
        {
            EndDrag();
        }
    }

    public Vector2 GetRotationInput()
    {
        return _currentInput;
    }


}
