using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TutorialView : MonoBehaviour
{
    public event Action PrimaryPressed;
    public event Action SecondaryPressed;

    private TMP_Text _messageText;
    private TMP_Text _progressText;
    private TMP_Text _primaryButtonText;
    private Button _primaryButton;
    private Button _secondaryButton;
    private RectTransform _directionArrow;
    private Transform _worldTarget;
    private Camera _camera;

    public void Initialize()
    {
        RectTransform rootRect = transform as RectTransform;
        if (rootRect != null)
        {
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
        }

        SetNamedObjectActive("Dimed", false);
        SetNamedObjectActive("Character", false);

        _messageText = FindNamedComponent<TMP_Text>("Text_Message");
        _progressText = FindChild("Label_Name")?.GetComponentInChildren<TMP_Text>(true);
        _primaryButton = FindNamedComponent<Button>("Button_SkipText");
        _secondaryButton = FindNamedComponent<Button>("Button_SkipIcon");
        _primaryButtonText = _primaryButton != null
            ? _primaryButton.GetComponentInChildren<TMP_Text>(true)
            : null;

        if (_progressText != null)
        {
            _progressText.enableAutoSizing = true;
            _progressText.fontSizeMin = 18f;
            _progressText.fontSizeMax = 34f;
            _progressText.textWrappingMode = TextWrappingModes.NoWrap;
            RectTransform progressRect = _progressText.transform as RectTransform;
            if (progressRect != null)
                progressRect.sizeDelta = new Vector2(Mathf.Max(340f, progressRect.sizeDelta.x), progressRect.sizeDelta.y);
        }

        if (_primaryButton != null)
        {
            RectTransform primaryRect = _primaryButton.transform as RectTransform;
            if (primaryRect != null)
                primaryRect.sizeDelta = new Vector2(210f, 72f);
        }

        if (_primaryButtonText != null)
        {
            _primaryButtonText.enableAutoSizing = true;
            _primaryButtonText.fontSizeMin = 16f;
            _primaryButtonText.fontSizeMax = 30f;
            _primaryButtonText.textWrappingMode = TextWrappingModes.NoWrap;
        }

        RectTransform bubbleRect = FindChild("SpeechBubble") as RectTransform;
        if (bubbleRect != null)
        {
            bubbleRect.sizeDelta = new Vector2(bubbleRect.sizeDelta.x, 340f);
            bubbleRect.anchoredPosition = new Vector2(bubbleRect.anchoredPosition.x, 250f);
        }

        Transform arrow = FindChild("Icon_Arrow");
        if (arrow != null)
        {
            _directionArrow = arrow as RectTransform;
            if (_directionArrow != null)
            {
                _directionArrow.SetParent(transform, false);
                _directionArrow.anchorMin = new Vector2(0.5f, 0.5f);
                _directionArrow.anchorMax = new Vector2(0.5f, 0.5f);
                _directionArrow.pivot = new Vector2(0.5f, 0.5f);
                _directionArrow.sizeDelta = new Vector2(88f, 80f);
                Image arrowImage = _directionArrow.GetComponent<Image>();
                if (arrowImage != null)
                {
                    arrowImage.color = Color.white;
                    Outline outline = _directionArrow.GetComponent<Outline>();
                    if (outline == null)
                        outline = _directionArrow.gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(1f, 0.78f, 0.05f, 1f);
                    outline.effectDistance = new Vector2(6f, -6f);
                }
            }
        }

        if (_primaryButton != null)
        {
            _primaryButton.onClick.RemoveListener(OnPrimaryPressed);
            _primaryButton.onClick.AddListener(OnPrimaryPressed);
        }

        if (_secondaryButton != null)
        {
            _secondaryButton.onClick.RemoveListener(OnSecondaryPressed);
            _secondaryButton.onClick.AddListener(OnSecondaryPressed);
        }

        SetWorldTarget(null);
    }

    public void SetStep(string progress, string message, string primaryLabel, bool isFinalStep)
    {
        if (_progressText != null)
            _progressText.text = progress ?? string.Empty;
        if (_messageText != null)
            _messageText.text = message ?? string.Empty;
        if (_primaryButtonText != null)
            _primaryButtonText.text = primaryLabel ?? string.Empty;

        if (_primaryButton != null)
            _primaryButton.gameObject.SetActive(true);
        if (_secondaryButton != null)
            _secondaryButton.gameObject.SetActive(false);
    }

    public void ShowInlineSkipConfirmation(string progress, string message, string confirmLabel)
    {
        if (_progressText != null)
            _progressText.text = progress ?? string.Empty;
        if (_messageText != null)
            _messageText.text = message ?? string.Empty;
        if (_primaryButtonText != null)
            _primaryButtonText.text = confirmLabel ?? string.Empty;

        if (_primaryButton != null)
            _primaryButton.gameObject.SetActive(true);
        if (_secondaryButton != null)
            _secondaryButton.gameObject.SetActive(true);
    }

    public void SetWorldTarget(Transform target)
    {
        _worldTarget = target;
        if (_directionArrow != null)
            _directionArrow.gameObject.SetActive(_worldTarget != null);
    }

    private void LateUpdate()
    {
        UpdateDirectionArrow();
    }

    private void UpdateDirectionArrow()
    {
        if (_directionArrow == null || _worldTarget == null)
        {
            if (_directionArrow != null)
                _directionArrow.gameObject.SetActive(false);
            return;
        }

        Vector3 viewport;
        RectTransform uiTarget = _worldTarget as RectTransform;
        Canvas targetCanvas = uiTarget != null ? uiTarget.GetComponentInParent<Canvas>() : null;
        if (uiTarget != null && targetCanvas != null && targetCanvas.renderMode != RenderMode.WorldSpace)
        {
            Camera eventCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : targetCanvas.worldCamera;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, uiTarget.position);
            viewport = new Vector3(
                Screen.width > 0 ? screenPoint.x / Screen.width : 0.5f,
                Screen.height > 0 ? screenPoint.y / Screen.height : 0.5f,
                1f);
        }
        else
        {
            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null)
                return;
            viewport = _camera.WorldToViewportPoint(_worldTarget.position);
        }

        if (viewport.z < 0f)
        {
            viewport.x = 1f - viewport.x;
            viewport.y = 1f - viewport.y;
        }

        Rect safe = Screen.safeArea;
        float minX = Screen.width > 0 ? safe.xMin / Screen.width : 0f;
        float maxX = Screen.width > 0 ? safe.xMax / Screen.width : 1f;
        float minY = Screen.height > 0 ? safe.yMin / Screen.height : 0f;
        float maxY = Screen.height > 0 ? safe.yMax / Screen.height : 1f;
        minX = Mathf.Clamp01(minX + 0.06f);
        maxX = Mathf.Clamp01(maxX - 0.06f);
        minY = Mathf.Clamp01(minY + 0.09f);
        maxY = Mathf.Clamp01(maxY - 0.09f);

        Vector2 direction = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
        Vector2 clamped = new Vector2(
            Mathf.Clamp(viewport.x, minX, maxX),
            Mathf.Clamp(viewport.y, minY, maxY));

        _directionArrow.anchorMin = clamped;
        _directionArrow.anchorMax = clamped;
        _directionArrow.anchoredPosition = Vector2.zero;
        if (direction.sqrMagnitude > 0.0001f)
            _directionArrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f);
        _directionArrow.gameObject.SetActive(true);
    }

    private void OnPrimaryPressed()
    {
        PrimaryPressed?.Invoke();
    }

    private void OnSecondaryPressed()
    {
        SecondaryPressed?.Invoke();
    }

    private void OnDestroy()
    {
        if (_primaryButton != null)
            _primaryButton.onClick.RemoveListener(OnPrimaryPressed);
        if (_secondaryButton != null)
            _secondaryButton.onClick.RemoveListener(OnSecondaryPressed);
    }

    private void SetNamedObjectActive(string objectName, bool active)
    {
        Transform child = FindChild(objectName);
        if (child != null)
            child.gameObject.SetActive(active);
    }

    private T FindNamedComponent<T>(string objectName) where T : Component
    {
        Transform child = FindChild(objectName);
        return child != null ? child.GetComponent<T>() : null;
    }

    private Transform FindChild(string objectName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (string.Equals(children[i].name, objectName, StringComparison.Ordinal))
                return children[i];
        }

        return null;
    }
}
