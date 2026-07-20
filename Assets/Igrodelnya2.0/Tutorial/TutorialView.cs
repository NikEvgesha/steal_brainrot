using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TutorialView : MonoBehaviour
{
    private const string CollapsedPreferenceKey = "Tutorial.UI.Collapsed";

    [Header("Shared UI Style")]
    [SerializeField] private Sprite _panelTexture;
    [SerializeField] private Sprite _buttonGradient;

    public event Action RewardClaimPressed;

    private TMP_Text _messageText;
    private TMP_Text _progressText;
    private TMP_Text _rewardText;
    private TMP_Text _doneButtonText;
    private TMP_Text _collapseButtonText;
    private Image _rewardIcon;
    private GameObject _rewardBadge;
    private Button _rewardClaimButton;
    private Button _doneButton;
    private Button _collapseButton;
    private RectTransform _panelViewport;
    private RectTransform _panelContent;
    private RectTransform _collapseButtonRect;
    private RectTransform _directionArrow;
    private RectTransform _secondaryDirectionArrow;
    private Transform _worldTarget;
    private Transform _secondaryWorldTarget;
    private Camera _camera;
    private Sprite _worldArrowSprite;
    private Sprite _handPointerSprite;
    private Coroutine _completionPulse;
    private Vector3 _panelBaseScale = Vector3.one;
    private bool _collapsed;
    private float _expandedPanelX;
    private float _collapsedPanelX;
    private float _targetPanelX;
    private Vector2 _panelViewportBasePosition;
    private Vector2 _collapseButtonBasePosition;
    private Rect _lastSafeArea;
    private int _lastScreenWidth = -1;
    private int _lastScreenHeight = -1;

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
        _rewardText = FindNamedComponent<TMP_Text>("Reward_Text");
        _rewardIcon = FindNamedComponent<Image>("Reward_Icon");
        _rewardBadge = FindChild("RewardBadge")?.gameObject;
        if (_rewardBadge != null)
        {
            _rewardClaimButton = _rewardBadge.GetComponent<Button>();
            if (_rewardClaimButton == null)
                _rewardClaimButton = _rewardBadge.AddComponent<Button>();

            Image rewardBackground = _rewardBadge.GetComponent<Image>();
            if (rewardBackground != null)
            {
                rewardBackground.raycastTarget = true;
                _rewardClaimButton.targetGraphic = rewardBackground;
            }

            ColorBlock colors = _rewardClaimButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.35f, 0.35f, 0.38f, 0.82f);
            colors.fadeDuration = 0.1f;
            _rewardClaimButton.colors = colors;
            _rewardClaimButton.transition = Selectable.Transition.ColorTint;
            _rewardClaimButton.navigation = new Navigation { mode = Navigation.Mode.None };
        }
        _doneButton = FindNamedComponent<Button>("Button_Done");
        _collapseButton = FindNamedComponent<Button>("Button_Collapse");
        _doneButtonText = _doneButton != null
            ? _doneButton.GetComponentInChildren<TMP_Text>(true)
            : null;
        _collapseButtonText = _collapseButton != null
            ? _collapseButton.GetComponentInChildren<TMP_Text>(true)
            : null;
        _panelViewport = FindChild("PanelViewport") as RectTransform;
        _panelContent = FindChild("TutorialPanel") as RectTransform;
        _collapseButtonRect = _collapseButton != null ? _collapseButton.transform as RectTransform : null;

        ApplyVisualStyle();

        if (_progressText != null)
        {
            _progressText.enableAutoSizing = true;
            _progressText.fontSizeMin = 18f;
            _progressText.fontSizeMax = 34f;
            _progressText.textWrappingMode = TextWrappingModes.NoWrap;
            RectTransform progressRect = _progressText.transform as RectTransform;
            if (progressRect != null && _panelContent == null)
                progressRect.sizeDelta = new Vector2(Mathf.Max(340f, progressRect.sizeDelta.x), progressRect.sizeDelta.y);
        }

        if (_doneButton != null)
        {
            RectTransform doneRect = _doneButton.transform as RectTransform;
            if (doneRect != null && _panelContent == null)
                doneRect.sizeDelta = new Vector2(210f, 72f);
        }

        if (_doneButtonText != null)
        {
            _doneButtonText.enableAutoSizing = true;
            _doneButtonText.fontSizeMin = 16f;
            _doneButtonText.fontSizeMax = 30f;
            _doneButtonText.textWrappingMode = TextWrappingModes.NoWrap;
        }

        RectTransform bubbleRect = _panelContent != null ? _panelContent : FindChild("SpeechBubble") as RectTransform;
        if (_panelContent == null && bubbleRect != null)
        {
            bubbleRect.anchorMin = Vector2.one;
            bubbleRect.anchorMax = Vector2.one;
            bubbleRect.pivot = Vector2.one;
            bubbleRect.sizeDelta = new Vector2(560f, 250f);
            bubbleRect.anchoredPosition = new Vector2(0f, -112f);
            _panelContent = bubbleRect;
        }

        if (_panelContent != null)
        {
            Vector2 panelPosition = _panelContent.anchoredPosition;
            _panelContent.anchoredPosition = new Vector2(0f, panelPosition.y);

            _panelBaseScale = _panelContent.localScale;
            _expandedPanelX = _panelContent.anchoredPosition.x;
            _collapsedPanelX = _expandedPanelX + Mathf.Max(300f, _panelContent.rect.width + 16f);
            SetCollapsed(PlayerPrefs.GetInt(CollapsedPreferenceKey, 0) == 1, immediate: true);
        }

        if (_panelViewport != null)
        {
            Vector2 viewportPosition = _panelViewport.anchoredPosition;
            _panelViewport.anchoredPosition = new Vector2(0f, viewportPosition.y);
            _panelViewportBasePosition = _panelViewport.anchoredPosition;
        }
        if (_collapseButtonRect != null)
            _collapseButtonBasePosition = _collapseButtonRect.anchoredPosition;
        ApplySafeArea(force: true);

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
                    _worldArrowSprite = arrowImage.sprite;
                    _handPointerSprite = Resources.Load<Sprite>("Tutorial/UIHandPointer");
                    arrowImage.color = Color.white;
                    Outline outline = _directionArrow.GetComponent<Outline>();
                    if (outline == null)
                        outline = _directionArrow.gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(1f, 0.78f, 0.05f, 1f);
                    outline.effectDistance = new Vector2(6f, -6f);
                }

                _secondaryDirectionArrow = Instantiate(_directionArrow, transform, false);
                _secondaryDirectionArrow.name = "Icon_Arrow_Secondary";
                Image secondaryImage = _secondaryDirectionArrow.GetComponent<Image>();
                if (secondaryImage != null)
                    secondaryImage.color = new Color(0.35f, 0.9f, 1f, 1f);
                Outline secondaryOutline = _secondaryDirectionArrow.GetComponent<Outline>();
                if (secondaryOutline != null)
                    secondaryOutline.effectColor = new Color(0.05f, 0.45f, 0.9f, 1f);
            }
        }

        if (_doneButton != null)
        {
            _doneButton.gameObject.SetActive(false);
        }

        if (_rewardClaimButton != null)
        {
            _rewardClaimButton.onClick.RemoveListener(OnRewardClaimPressed);
            _rewardClaimButton.onClick.AddListener(OnRewardClaimPressed);
            SetRewardButtonReady(false);
        }

        if (_collapseButton != null)
        {
            _collapseButton.onClick.RemoveListener(OnCollapsePressed);
            _collapseButton.onClick.AddListener(OnCollapsePressed);
        }

        SetWorldTargets(null, null);
    }

    public void SetStep(
        string progress,
        string message,
        string reward,
        Sprite rewardIcon,
        string doneLabel,
        bool isFinalStep)
    {
        if (_progressText != null)
            _progressText.text = progress ?? string.Empty;
        if (_messageText != null)
            _messageText.text = message ?? string.Empty;
        if (_rewardText != null)
            _rewardText.text = reward ?? string.Empty;
        if (_rewardIcon != null)
        {
            _rewardIcon.sprite = rewardIcon;
            _rewardIcon.gameObject.SetActive(rewardIcon != null);
        }
        UpdateRewardContentLayout(rewardIcon != null);
        if (_rewardBadge != null)
            _rewardBadge.SetActive(true);
        SetRewardButtonReady(false);
        if (_doneButtonText != null)
            _doneButtonText.text = doneLabel ?? string.Empty;
        if (_doneButton != null)
            _doneButton.gameObject.SetActive(false);
    }

    public void SetWorldTarget(Transform target)
    {
        SetWorldTargets(target, null);
    }

    public void SetWorldTargets(Transform primary, Transform secondary)
    {
        _worldTarget = primary;
        _secondaryWorldTarget = secondary;
        if (_directionArrow != null)
            _directionArrow.gameObject.SetActive(_worldTarget != null);
        if (_secondaryDirectionArrow != null)
            _secondaryDirectionArrow.gameObject.SetActive(_secondaryWorldTarget != null);
    }

    public void ShowCompleted(string label, string reward, Sprite rewardIcon)
    {
        if (_progressText != null)
            _progressText.text = "OK";
        if (_messageText != null)
            _messageText.text = label ?? string.Empty;
        if (_rewardText != null)
            _rewardText.text = reward ?? string.Empty;
        if (_rewardIcon != null)
        {
            _rewardIcon.sprite = rewardIcon;
            _rewardIcon.gameObject.SetActive(rewardIcon != null);
        }
        UpdateRewardContentLayout(rewardIcon != null);
        if (_rewardBadge != null)
            _rewardBadge.SetActive(true);
        SetRewardButtonReady(true);
        if (_doneButton != null)
            _doneButton.gameObject.SetActive(false);

        SetCollapsed(false, immediate: false);
        SetWorldTargets(null, null);
        if (_completionPulse != null)
            StopCoroutine(_completionPulse);
        _completionPulse = StartCoroutine(CompletionPulse());
    }

    public void ShowClaimed(string label)
    {
        if (_progressText != null)
            _progressText.text = "OK";
        if (_messageText != null)
            _messageText.text = label ?? string.Empty;
        SetRewardButtonReady(false);

        if (_completionPulse != null)
            StopCoroutine(_completionPulse);
        _completionPulse = StartCoroutine(CompletionPulse());
    }

    private void ApplyVisualStyle()
    {
        if (_panelViewport != null)
        {
            _panelViewport.sizeDelta = new Vector2(610f, 290f);
            RectMask2D mask = _panelViewport.GetComponent<RectMask2D>();
            if (mask != null)
                mask.padding = new Vector4(-8f, -8f, -8f, -8f);
        }

        if (_panelContent != null)
        {
            _panelContent.sizeDelta = new Vector2(610f, 290f);
            Image panelImage = _panelContent.GetComponent<Image>();
            ConfigureSurface(panelImage, new Color(0.10f, 0.055f, 0.025f, 0.94f));
            EnsureOutline(_panelContent.gameObject, BlockyUITheme.BlackStroke, new Vector2(5f, -5f));
            EnsureShadow(_panelContent.gameObject, new Color(0f, 0f, 0f, 0.42f), new Vector2(0f, -5f));

            Image header = EnsureDecorativeImage("TutorialHeader", _panelContent);
            if (header != null)
            {
                SetRect(
                    header.rectTransform,
                    new Vector2(0f, 1f),
                    Vector2.one,
                    new Vector2(0.5f, 1f),
                    Vector2.zero,
                    new Vector2(0f, 64f));
                ConfigureSurface(header, BlockyUITheme.GreenHeader);
                EnsureOutline(header.gameObject, BlockyUITheme.BlackStroke, new Vector2(4f, -4f));
                EnsureGradient(header.transform);
                header.transform.SetSiblingIndex(0);
            }

            Image messagePlate = EnsureDecorativeImage("TaskTextPlate", _panelContent);
            if (messagePlate != null)
            {
                SetRect(
                    messagePlate.rectTransform,
                    Vector2.zero,
                    Vector2.one,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -4f),
                    new Vector2(-32f, -142f));
                ConfigureSurface(messagePlate, new Color(0.055f, 0.025f, 0.012f, 0.76f));
                EnsureOutline(messagePlate.gameObject, new Color(0.02f, 0.01f, 0.005f, 0.95f), new Vector2(3f, -3f));
                messagePlate.transform.SetSiblingIndex(1);
            }
        }

        if (_progressText != null)
        {
            RectTransform rect = _progressText.rectTransform;
            SetRect(
                rect,
                new Vector2(0f, 1f),
                Vector2.one,
                new Vector2(0.5f, 1f),
                new Vector2(-34f, -3f),
                new Vector2(-100f, 58f));
            BlockyUITheme.ApplyText(_progressText, Color.white, 27);
            _progressText.color = Color.white;
            _progressText.alignment = TextAlignmentOptions.MidlineLeft;
        }

        if (_messageText != null)
        {
            RectTransform rect = _messageText.rectTransform;
            SetRect(
                rect,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -4f),
                new Vector2(-60f, -166f));
            BlockyUITheme.ApplyText(_messageText, Color.white, 24);
            _messageText.color = Color.white;
            _messageText.enableAutoSizing = true;
            _messageText.fontSizeMin = 18f;
            _messageText.fontSizeMax = 28f;
            _messageText.alignment = TextAlignmentOptions.TopLeft;
        }

        if (_rewardClaimButton != null)
        {
            RectTransform rect = _rewardClaimButton.transform as RectTransform;
            if (rect != null)
            {
                SetRect(
                    rect,
                    Vector2.right,
                    Vector2.right,
                    Vector2.right,
                    new Vector2(-18f, 18f),
                    new Vector2(260f, 56f));
            }
            StyleButton(_rewardClaimButton, BlockyUITheme.GreenHeader);
        }

        if (_rewardText != null)
        {
            BlockyUITheme.ApplyText(_rewardText, Color.white, 20);
            _rewardText.color = Color.white;
            _rewardText.enableAutoSizing = true;
            _rewardText.fontSizeMin = 15f;
            _rewardText.fontSizeMax = 23f;
            _rewardText.alignment = TextAlignmentOptions.Center;
        }

        if (_rewardIcon != null)
        {
            _rewardIcon.preserveAspect = true;
            _rewardIcon.raycastTarget = false;
            RectTransform iconRect = _rewardIcon.rectTransform;
            SetRect(
                iconRect,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(14f, 0f),
                new Vector2(40f, 40f));
        }

        if (_doneButton != null)
            StyleButton(_doneButton, BlockyUITheme.GreenHeader);

        if (_collapseButton != null)
        {
            if (_collapseButtonRect != null)
            {
                SetRect(
                    _collapseButtonRect,
                    Vector2.one,
                    Vector2.one,
                    Vector2.one,
                    new Vector2(-18f, -116f),
                    new Vector2(56f, 56f));
            }
            StyleButton(_collapseButton, BlockyUITheme.OrangeHeader);
        }

        if (_collapseButtonText != null)
        {
            BlockyUITheme.ApplyText(_collapseButtonText, Color.white, 28);
            _collapseButtonText.color = Color.white;
            _collapseButtonText.fontSize = 30f;
            _collapseButtonText.enableAutoSizing = false;
            _collapseButtonText.alignment = TextAlignmentOptions.Center;
        }

        UpdateRewardContentLayout(_rewardIcon != null && _rewardIcon.gameObject.activeSelf);
    }

    private Image EnsureDecorativeImage(string objectName, Transform parent)
    {
        if (parent == null)
            return null;

        Transform existing = parent.Find(objectName);
        GameObject gameObject;
        if (existing != null)
        {
            gameObject = existing.gameObject;
        }
        else
        {
            gameObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.layer = parent.gameObject.layer;
            gameObject.transform.SetParent(parent, false);
        }

        Image image = gameObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private void ConfigureSurface(Image image, Color color)
    {
        if (image == null)
            return;

        if (_panelTexture != null)
        {
            image.sprite = _panelTexture;
            image.type = Image.Type.Tiled;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = color;
        }
        else
        {
            BlockyUITheme.ApplyPanel(image, color, studs: true);
        }
    }

    private void StyleButton(Button button, Color color)
    {
        if (button == null)
            return;

        Image image = button.targetGraphic as Image;
        if (image == null)
            image = button.GetComponent<Image>();
        if (image == null)
            image = button.gameObject.AddComponent<Image>();

        ConfigureSurface(image, color);
        image.raycastTarget = true;
        button.targetGraphic = image;
        EnsureOutline(button.gameObject, BlockyUITheme.BlackStroke, new Vector2(3f, -3f));
        EnsureShadow(button.gameObject, new Color(0f, 0f, 0f, 0.36f), new Vector2(0f, -3f));
        EnsureGradient(button.transform);

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.38f, 0.38f, 0.40f, 0.82f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.transition = Selectable.Transition.ColorTint;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    private void EnsureGradient(Transform parent)
    {
        if (parent == null || _buttonGradient == null)
            return;

        Transform existing = parent.Find("Gradient");
        GameObject gameObject;
        if (existing != null)
        {
            gameObject = existing.gameObject;
        }
        else
        {
            gameObject = new GameObject("Gradient", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.layer = parent.gameObject.layer;
            gameObject.transform.SetParent(parent, false);
        }

        RectTransform rect = gameObject.transform as RectTransform;
        SetRect(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image gradient = gameObject.GetComponent<Image>();
        gradient.sprite = _buttonGradient;
        gradient.type = Image.Type.Simple;
        gradient.color = new Color(1f, 1f, 1f, 0.34f);
        gradient.raycastTarget = false;
        gameObject.transform.SetSiblingIndex(0);
    }

    private static void EnsureOutline(GameObject target, Color color, Vector2 distance)
    {
        if (target == null)
            return;

        Outline outline = target.GetComponent<Outline>();
        if (outline == null)
            outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    private static void EnsureShadow(GameObject target, Color color, Vector2 distance)
    {
        if (target == null)
            return;

        Shadow shadow = null;
        Shadow[] shadows = target.GetComponents<Shadow>();
        for (int i = 0; i < shadows.Length; i++)
        {
            if (shadows[i] != null && shadows[i].GetType() == typeof(Shadow))
            {
                shadow = shadows[i];
                break;
            }
        }

        if (shadow == null)
            shadow = target.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = distance;
        shadow.useGraphicAlpha = true;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        if (rect == null)
            return;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private void UpdateRewardContentLayout(bool hasIcon)
    {
        if (_rewardText == null)
            return;

        RectTransform rect = _rewardText.rectTransform;
        SetRect(
            rect,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            hasIcon ? new Vector2(22f, 0f) : Vector2.zero,
            hasIcon ? new Vector2(-62f, -8f) : new Vector2(-20f, -8f));
    }

    private void SetRewardButtonReady(bool ready)
    {
        if (_rewardClaimButton == null)
            return;

        _rewardClaimButton.interactable = ready;
        Outline outline = _rewardClaimButton.GetComponent<Outline>();
        if (outline != null)
            outline.effectColor = ready ? BlockyUITheme.YellowAccent : BlockyUITheme.BlackStroke;
    }

    private void LateUpdate()
    {
        ApplySafeArea(force: false);
        UpdatePanelSlide();
        UpdateDirectionArrow(_directionArrow, _worldTarget);
        UpdateDirectionArrow(_secondaryDirectionArrow, _secondaryWorldTarget);
    }

    private void ApplySafeArea(bool force)
    {
        Rect safeArea = Screen.safeArea;
        if (!force && _lastScreenWidth == Screen.width && _lastScreenHeight == Screen.height && _lastSafeArea == safeArea)
            return;

        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;
        _lastSafeArea = safeArea;

        Canvas canvas = GetComponentInParent<Canvas>();
        float scaleFactor = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        float rightInset = Mathf.Max(0f, Screen.width - safeArea.xMax) / scaleFactor;
        float topInset = Mathf.Max(0f, Screen.height - safeArea.yMax) / scaleFactor;
        Vector2 inset = new Vector2(-rightInset, -topInset);

        if (_panelViewport != null)
            _panelViewport.anchoredPosition = _panelViewportBasePosition + inset;
        if (_collapseButtonRect != null)
            _collapseButtonRect.anchoredPosition = _collapseButtonBasePosition + inset;
    }

    private void UpdatePanelSlide()
    {
        if (_panelContent == null)
            return;

        Vector2 position = _panelContent.anchoredPosition;
        float nextX = Mathf.MoveTowards(position.x, _targetPanelX, 1500f * Time.unscaledDeltaTime);
        if (!Mathf.Approximately(position.x, nextX))
            _panelContent.anchoredPosition = new Vector2(nextX, position.y);
    }

    private void UpdateDirectionArrow(RectTransform directionArrow, Transform worldTarget)
    {
        if (directionArrow == null || worldTarget == null)
        {
            if (directionArrow != null)
                directionArrow.gameObject.SetActive(false);
            return;
        }

        Vector3 viewport;
        RectTransform uiTarget = worldTarget as RectTransform;
        Canvas targetCanvas = uiTarget != null ? uiTarget.GetComponentInParent<Canvas>() : null;
        bool isScreenUi = uiTarget != null && targetCanvas != null && targetCanvas.renderMode != RenderMode.WorldSpace;
        if (isScreenUi)
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
            viewport = _camera.WorldToViewportPoint(worldTarget.position);
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

        Image pointerImage = directionArrow.GetComponent<Image>();
        Outline pointerOutline = directionArrow.GetComponent<Outline>();
        if (isScreenUi && _handPointerSprite != null)
        {
            Vector2 targetPixels = new Vector2(viewport.x * Screen.width, viewport.y * Screen.height);
            Vector2 fromCenterPixels = targetPixels - safe.center;
            Vector2 pointingDirection = fromCenterPixels.sqrMagnitude > 1f
                ? fromCenterPixels.normalized
                : Vector2.up;
            float tapPulse = Mathf.Sin(Time.unscaledTime * 6.5f) * 7f;
            Vector2 pointerPixels = targetPixels - pointingDirection * (76f - tapPulse);
            pointerPixels.x = Mathf.Clamp(pointerPixels.x, safe.xMin + 58f, safe.xMax - 58f);
            pointerPixels.y = Mathf.Clamp(pointerPixels.y, safe.yMin + 58f, safe.yMax - 58f);
            clamped = new Vector2(
                Screen.width > 0 ? pointerPixels.x / Screen.width : 0.5f,
                Screen.height > 0 ? pointerPixels.y / Screen.height : 0.5f);

            directionArrow.sizeDelta = new Vector2(112f, 112f);
            directionArrow.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(pointingDirection.y, pointingDirection.x) * Mathf.Rad2Deg - 90f);
            if (pointerImage != null)
            {
                pointerImage.sprite = _handPointerSprite;
                pointerImage.preserveAspect = true;
                pointerImage.color = Color.white;
            }
            if (pointerOutline != null)
                pointerOutline.enabled = false;
        }
        else
        {
            directionArrow.sizeDelta = new Vector2(88f, 80f);
            if (pointerImage != null)
            {
                pointerImage.sprite = _worldArrowSprite;
                pointerImage.preserveAspect = true;
                pointerImage.color = directionArrow == _secondaryDirectionArrow
                    ? new Color(0.35f, 0.9f, 1f, 1f)
                    : Color.white;
            }
            if (pointerOutline != null)
                pointerOutline.enabled = true;
            if (direction.sqrMagnitude > 0.0001f)
                directionArrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f);
        }

        directionArrow.anchorMin = clamped;
        directionArrow.anchorMax = clamped;
        directionArrow.anchoredPosition = Vector2.zero;
        directionArrow.gameObject.SetActive(true);
    }

    private IEnumerator CompletionPulse()
    {
        if (_panelContent == null)
            yield break;

        const float duration = 0.72f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(t * Mathf.PI) * (1f - t * 0.35f);
            _panelContent.localScale = _panelBaseScale * (1f + pulse * 0.1f);
            yield return null;
        }
        _panelContent.localScale = _panelBaseScale;
        _completionPulse = null;
    }

    private void OnRewardClaimPressed()
    {
        RewardClaimPressed?.Invoke();
    }

    private void OnCollapsePressed()
    {
        SetCollapsed(!_collapsed, immediate: false);
        PlayerPrefs.SetInt(CollapsedPreferenceKey, _collapsed ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void SetCollapsed(bool collapsed, bool immediate)
    {
        _collapsed = collapsed;
        _targetPanelX = collapsed ? _collapsedPanelX : _expandedPanelX;
        if (immediate && _panelContent != null)
        {
            Vector2 position = _panelContent.anchoredPosition;
            _panelContent.anchoredPosition = new Vector2(_targetPanelX, position.y);
        }
        if (_collapseButtonText != null)
            _collapseButtonText.text = collapsed ? "<" : ">";
    }

    private void OnDestroy()
    {
        if (_completionPulse != null)
            StopCoroutine(_completionPulse);
        if (_rewardClaimButton != null)
            _rewardClaimButton.onClick.RemoveListener(OnRewardClaimPressed);
        if (_collapseButton != null)
            _collapseButton.onClick.RemoveListener(OnCollapsePressed);
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
