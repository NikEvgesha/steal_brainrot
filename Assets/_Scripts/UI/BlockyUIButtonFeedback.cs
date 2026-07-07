using UnityEngine;
using UnityEngine.EventSystems;

public class BlockyUIButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float hoverScale = 1.03f;
    [SerializeField] private float pressedScale = 0.96f;
    [SerializeField] private float animationSpeed = 18f;

    private Vector3 _baseScale;
    private float _targetScale = 1f;
    private bool _initialized;
    private bool _hovered;
    private bool _pressed;

    private void OnEnable()
    {
        if (!_initialized)
        {
            _baseScale = transform.localScale;
            if (_baseScale == Vector3.zero)
                _baseScale = Vector3.one;
            _initialized = true;
        }

        ResolveTargetScale();
    }

    private void OnDisable()
    {
        _hovered = false;
        _pressed = false;
        _targetScale = 1f;
        if (_initialized)
            transform.localScale = _baseScale;
    }

    private void Update()
    {
        if (!_initialized)
            return;

        var target = _baseScale * _targetScale;
        transform.localScale = Vector3.Lerp(transform.localScale, target, Time.unscaledDeltaTime * animationSpeed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        ResolveTargetScale();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        _pressed = false;
        ResolveTargetScale();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _pressed = true;
        ResolveTargetScale();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _pressed = false;
        ResolveTargetScale();
    }

    private void ResolveTargetScale()
    {
        _targetScale = _pressed ? pressedScale : _hovered ? hoverScale : 1f;
    }
}
