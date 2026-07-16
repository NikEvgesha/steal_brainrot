using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public sealed class MobileSafeArea : MonoBehaviour
{
    private RectTransform _rectTransform;
    private Rect _lastSafeArea;
    private Vector2Int _lastScreenSize;

    private void Awake()
    {
        _rectTransform = (RectTransform)transform;
    }

    private void OnEnable()
    {
        ApplySafeArea(true);
    }

    private void Update()
    {
        ApplySafeArea(false);
    }

    private void ApplySafeArea(bool force)
    {
        var screenSize = new Vector2Int(Screen.width, Screen.height);
        Rect safeArea = GetValidSafeArea(screenSize);
        if (!force && safeArea == _lastSafeArea && screenSize == _lastScreenSize)
            return;
        if (screenSize.x <= 0 || screenSize.y <= 0)
            return;

        _lastSafeArea = safeArea;
        _lastScreenSize = screenSize;

        _rectTransform.anchorMin = new Vector2(
            safeArea.xMin / screenSize.x,
            safeArea.yMin / screenSize.y);
        _rectTransform.anchorMax = new Vector2(
            safeArea.xMax / screenSize.x,
            safeArea.yMax / screenSize.y);
        _rectTransform.offsetMin = Vector2.zero;
        _rectTransform.offsetMax = Vector2.zero;
    }

    private static Rect GetValidSafeArea(Vector2Int screenSize)
    {
        if (screenSize.x <= 0 || screenSize.y <= 0)
            return Rect.zero;

        Rect safeArea = Screen.safeArea;
        bool isValid = safeArea.width > 0f &&
                       safeArea.height > 0f &&
                       safeArea.xMin >= 0f &&
                       safeArea.yMin >= 0f &&
                       safeArea.xMax <= screenSize.x + 0.5f &&
                       safeArea.yMax <= screenSize.y + 0.5f;

        return isValid
            ? safeArea
            : new Rect(0f, 0f, screenSize.x, screenSize.y);
    }
}
