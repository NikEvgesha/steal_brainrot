using UnityEngine;
using UnityEngine.EventSystems;

public class OnScreenJoystick : MonoBehaviour, IDragHandler, IPointerUpHandler, IPointerDownHandler
{
    [SerializeField] private RectTransform background;  // The joystick background
    [SerializeField] private RectTransform knob;        // The movable knob
    [SerializeField] private float maxRadius = 100f;    // Maximum distance knob can move

    private Vector2 inputVector;                       // The final input value
    //private Vector2 startPos;                          // Initial position of background
    //private bool isDragging = false;

    void Start()
    {
        //startPos = background.anchoredPosition;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        //isDragging = true;
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint
        );

        localPoint = Vector2.ClampMagnitude(localPoint, maxRadius);
        knob.anchoredPosition = localPoint;
        inputVector = localPoint / maxRadius;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        //isDragging = false;
        inputVector = Vector2.zero;
        knob.anchoredPosition = Vector2.zero;
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
}
