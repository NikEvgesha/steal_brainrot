using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class TimePlayedBonusUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TimePlayedBonusModifierMB _modifier;
    [SerializeField] private TMP_Text _text;
    [SerializeField] private string _format = "Time Bonus: {0}";
    [Header("Runtime Style")]
    [SerializeField] private Vector2 _panelSize = new Vector2(132f, 50f);
    [SerializeField] private Vector2 _panelOffset = new Vector2(-18f, 18f);
    [SerializeField] private Vector2 _tooltipSize = new Vector2(300f, 98f);
    [SerializeField] private Vector2 _tooltipOffset = new Vector2(0f, 8f);

    private Image _background;
    private GameObject _tooltipRoot;
    private TMP_Text _tooltipText;
    private string _tooltipValue = "";
    private static Sprite _panelSprite;
    private LocalizationManager _subscribedLocalizationManager;

    private void Awake()
    {
        EnsureRuntimeStyle();

        if (_modifier == null)
            _modifier = FindAnyObjectByType<TimePlayedBonusModifierMB>();

        if (_modifier != null)
            _modifier.Changed += UpdateView;

        UpdateView();
    }

    private void OnEnable()
    {
        LocalizationManager.OnInstanceReady += HandleLocalizationManagerReady;
        LocalizationUtils.OnFallbackLanguageChanged += HandleLanguageChanged;
        SubscribeToLocalizationManager(LocalizationManager.Instance);
    }

    private void OnDisable()
    {
        LocalizationManager.OnInstanceReady -= HandleLocalizationManagerReady;
        LocalizationUtils.OnFallbackLanguageChanged -= HandleLanguageChanged;
        UnsubscribeFromLocalizationManager();
    }

    private void OnDestroy()
    {
        if (_modifier != null)
            _modifier.Changed -= UpdateView;
    }

    private void UpdateView()
    {
        if (_text == null) return;

        if (_modifier == null || !_modifier.IsActive)
        {
            _text.text = string.Format(
                _format,
                LocalizationUtils.T("UI/TimeBonus/Off", "OFF"));
            _tooltipValue = LocalizationUtils.T(
                "UI/TimeBonus/DisabledTooltip",
                "Income bonus: off\nAvailable in test mode or on weekends. Increases with time played.");
            UpdateTooltipText();
            return;
        }

        _text.text = string.Format(_format, _modifier.DisplayValue);
        _tooltipValue = LocalizationUtils.Format(
            "UI/TimeBonus/ActiveTooltip",
            "Income bonus: {0}\n{1}",
            _modifier.DisplayValue,
            LocalizationUtils.T(
                "UI/TimeBonus/Description",
                "Available in test mode or on weekends. +1% per minute today, up to +50%."));
        UpdateTooltipText();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        EnsureRuntimeStyle();
        if (_tooltipRoot != null)
            _tooltipRoot.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_tooltipRoot != null)
            _tooltipRoot.SetActive(false);
    }

    private void EnsureRuntimeStyle()
    {
        var rect = transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = _panelOffset;
            rect.sizeDelta = _panelSize;
        }

        var canvasRenderer = GetComponent<CanvasRenderer>();
        if (canvasRenderer == null)
            canvasRenderer = gameObject.AddComponent<CanvasRenderer>();

        _background = GetComponent<Image>();
        if (_background == null)
            _background = gameObject.AddComponent<Image>();

        _background.sprite = GetPanelSprite();
        _background.type = Image.Type.Sliced;
        _background.pixelsPerUnitMultiplier = 2f;
        _background.color = new Color(0.12f, 0.13f, 0.15f, 0.82f);
        _background.raycastTarget = true;

        var shadow = GetComponent<Shadow>();
        if (shadow == null)
            shadow = gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        shadow.effectDistance = new Vector2(0f, -3f);

        if (_text != null)
        {
            _text.alignment = TextAlignmentOptions.Center;
            _text.fontSize = 26f;
            _text.raycastTarget = true;

            var textRect = _text.transform as RectTransform;
            if (textRect != null)
            {
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(12f, 6f);
                textRect.offsetMax = new Vector2(-12f, -6f);
            }

            var outline = _text.GetComponent<Outline>();
            if (outline == null)
                outline = _text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        EnsureTooltip();
    }

    private void EnsureTooltip()
    {
        if (_tooltipRoot != null)
            return;

        _tooltipRoot = new GameObject("TimePlayedBonusTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _tooltipRoot.transform.SetParent(transform, false);

        var tooltipRect = _tooltipRoot.transform as RectTransform;
        tooltipRect.anchorMin = new Vector2(1f, 1f);
        tooltipRect.anchorMax = new Vector2(1f, 1f);
        tooltipRect.pivot = new Vector2(1f, 0f);
        tooltipRect.anchoredPosition = _tooltipOffset;
        tooltipRect.sizeDelta = _tooltipSize;

        var image = _tooltipRoot.GetComponent<Image>();
        image.sprite = GetPanelSprite();
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 2f;
        image.color = new Color(0.08f, 0.09f, 0.11f, 0.94f);
        image.raycastTarget = false;

        var textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(_tooltipRoot.transform, false);

        var textRect = textObject.transform as RectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 10f);
        textRect.offsetMax = new Vector2(-14f, -10f);

        _tooltipText = textObject.GetComponent<TMP_Text>();
        _tooltipText.font = _text != null ? _text.font : _tooltipText.font;
        _tooltipText.fontSize = 18f;
        _tooltipText.alignment = TextAlignmentOptions.MidlineLeft;
        _tooltipText.color = Color.white;
        _tooltipText.raycastTarget = false;
        _tooltipText.textWrappingMode = TextWrappingModes.Normal;
        _tooltipText.overflowMode = TextOverflowModes.Ellipsis;

        var outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        UpdateTooltipText();
        _tooltipRoot.SetActive(false);
    }

    private void UpdateTooltipText()
    {
        if (_tooltipText != null)
            _tooltipText.text = _tooltipValue;
    }

    private static Sprite GetPanelSprite()
    {
        if (_panelSprite != null)
            return _panelSprite;

        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color fill = Color.white;
        Color border = new Color(1f, 0.86f, 0.32f, 1f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool inside = IsInsideRoundedRect(x, y, size, size, 10);
                bool inner = IsInsideRoundedRect(x, y, size, size, 7) && x > 3 && y > 3 && x < size - 4 && y < size - 4;
                texture.SetPixel(x, y, inside ? (inner ? fill : border) : clear);
            }
        }

        texture.Apply();
        texture.hideFlags = HideFlags.DontSave;

        _panelSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect, new Vector4(12f, 12f, 12f, 12f));
        _panelSprite.hideFlags = HideFlags.DontSave;
        return _panelSprite;
    }

    private static bool IsInsideRoundedRect(int x, int y, int width, int height, int radius)
    {
        int left = radius;
        int right = width - radius - 1;
        int bottom = radius;
        int top = height - radius - 1;

        if (x >= left && x <= right) return true;
        if (y >= bottom && y <= top) return true;

        int cx = x < left ? left : right;
        int cy = y < bottom ? bottom : top;
        int dx = x - cx;
        int dy = y - cy;
        return dx * dx + dy * dy <= radius * radius;
    }

    private void HandleLocalizationManagerReady(LocalizationManager manager)
    {
        SubscribeToLocalizationManager(manager);
        UpdateView();
    }

    private void HandleLanguageChanged(string _)
    {
        UpdateView();
    }

    private void SubscribeToLocalizationManager(LocalizationManager manager)
    {
        if (manager == null || manager == _subscribedLocalizationManager)
            return;

        UnsubscribeFromLocalizationManager();
        _subscribedLocalizationManager = manager;
        _subscribedLocalizationManager.OnLanguageChanged += HandleLanguageChanged;
    }

    private void UnsubscribeFromLocalizationManager()
    {
        if (_subscribedLocalizationManager == null)
            return;

        _subscribedLocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
        _subscribedLocalizationManager = null;
    }
}
