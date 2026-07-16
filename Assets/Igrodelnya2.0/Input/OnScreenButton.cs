using UnityEngine;
using UnityEngine.EventSystems;

public class OnScreenButton : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private bool _triggerOnPointerDown;

    private bool _triggered;
    private bool _holded;
    public bool IsTriggered
    {
        get
        {
            var tmp = _triggered;
            _triggered = false;
            return tmp;
        }
        private set { }
    }

    public bool IsHolded
    {
        get { return _holded; }
        private set { }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_triggerOnPointerDown)
            _triggered = true;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _holded = true;
        if (_triggerOnPointerDown)
            _triggered = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _holded = false;
    }


}
