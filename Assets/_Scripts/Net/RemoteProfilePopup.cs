using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public sealed class RemoteProfilePopup : MonoBehaviour
{
    private static RemoteProfilePopup _instance;
    private const float ExtraLikeNoticeSeconds = 2.5f;
    private const string ResourcesPrefabPath = "RemoteProfilePopup";

    [Header("Prefab Refs")]
    [SerializeField]
    private Canvas _canvas;
    [SerializeField]
    private GameObject _panel;
    [SerializeField]
    private Text _title;
    [SerializeField]
    private Text _avatarInitial;
    [SerializeField]
    private Text _friendCode;
    [SerializeField]
    private Text _body;
    [SerializeField]
    private Text _likes;
    [SerializeField]
    private Text _notice;
    [SerializeField]
    private Button _closeButton;
    [SerializeField]
    private Button _likeButton;
    [SerializeField]
    private Text _likeButtonLabel;

    [Header("Visual Theme")]
    [SerializeField]
    private Sprite _textureSprite;
    [SerializeField]
    private Sprite _buttonGradientSprite;
    [SerializeField]
    private Font _sharedFont;

    private Coroutine _noticeRoutine;
    private Coroutine _likeStateRoutine;
    private Coroutine _sendLikeRoutine;
    private FriendsApi _api;
    private string _targetPlayerId;
    private string _targetFriendCode;
    private bool _likedToday;
    private bool _likeRequestInFlight;
    private int _likesCount;
    private string _displayName;
    private PlayerPublicStatsDto _stats;
    private LocalizationManager _subscribedLocalizationManager;

    public static RemoteProfilePopup Instance
    {
        get
        {
            if (_instance != null)
                return _instance;

            _instance = FindLoadedInstance();
            if (_instance != null)
                return _instance;

            var prefab = Resources.Load<RemoteProfilePopup>(ResourcesPrefabPath);
            if (prefab != null)
            {
                _instance = Instantiate(prefab);
                _instance.name = prefab.name;
                return _instance;
            }

            var go = new GameObject("RemoteProfilePopup");
            _instance = go.AddComponent<RemoteProfilePopup>();
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        BuildUI();
        ApplyVisualStyle();
        BindButtonListeners();
        Hide();
    }

    private void OnEnable()
    {
        SubscribeLocalization();
    }

    private void OnDisable()
    {
        UnsubscribeLocalization();
    }

    private void OnDestroy()
    {
        UnsubscribeLocalization();
        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(Hide);
        if (_likeButton != null)
            _likeButton.onClick.RemoveListener(OnLikePressed);
    }

    public void Show(string displayName, PlayerPublicStatsDto stats, string targetPlayerId = null, string targetFriendCode = null)
    {
        if (_panel == null)
            BuildUI();

        _displayName = displayName;
        _stats = stats ?? new PlayerPublicStatsDto();
        _targetPlayerId = string.IsNullOrWhiteSpace(targetPlayerId) ? null : targetPlayerId;
        _targetFriendCode = string.IsNullOrWhiteSpace(targetFriendCode) ? null : targetFriendCode;
        _likedToday = false;
        _likesCount = 0;
        _likeRequestInFlight = false;

        MountOnGameCanvas();

        if (_noticeRoutine != null)
        {
            StopCoroutine(_noticeRoutine);
            _noticeRoutine = null;
        }
        if (_sendLikeRoutine != null)
        {
            StopCoroutine(_sendLikeRoutine);
            _sendLikeRoutine = null;
        }
        if (_likeStateRoutine != null)
        {
            StopCoroutine(_likeStateRoutine);
            _likeStateRoutine = null;
        }

        RefreshLocalizedContent();

        if (HasLikeTarget())
        {
            _likeStateRoutine = StartCoroutine(LoadLikeState());
        }

        if (_panel != null)
            _panel.SetActive(true);
    }

    private void MountOnGameCanvas()
    {
        if (_panel == null)
            return;

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Canvas host = null;
        int bestOrder = int.MinValue;
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas candidate = canvases[i];
            if (candidate == null || candidate == _canvas || !candidate.enabled ||
                candidate.renderMode == RenderMode.WorldSpace)
            {
                continue;
            }

            if (candidate.name.StartsWith("GameCanvas", StringComparison.OrdinalIgnoreCase))
            {
                host = candidate;
                break;
            }

            if (candidate.sortingOrder > bestOrder && candidate.sortingOrder < 5000)
            {
                host = candidate;
                bestOrder = candidate.sortingOrder;
            }
        }

        if (host == null)
            return;

        RectTransform panelRect = _panel.transform as RectTransform;
        if (_panel.transform.parent != host.transform)
            _panel.transform.SetParent(host.transform, false);
        if (panelRect != null)
        {
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panelRect.localScale = Vector3.one;
        }
        _panel.transform.SetAsLastSibling();

        if (_canvas != null)
            _canvas.enabled = false;
    }

    public void Hide()
    {
        if (_noticeRoutine != null)
        {
            StopCoroutine(_noticeRoutine);
            _noticeRoutine = null;
        }
        if (_sendLikeRoutine != null)
        {
            StopCoroutine(_sendLikeRoutine);
            _sendLikeRoutine = null;
        }
        if (_likeStateRoutine != null)
        {
            StopCoroutine(_likeStateRoutine);
            _likeStateRoutine = null;
        }
        _likeRequestInFlight = false;

        if (_notice != null)
            _notice.gameObject.SetActive(false);

        if (_panel != null)
            _panel.SetActive(false);
    }

    private void BuildUI()
    {
        if (TryBindPrefabUI())
            return;

        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = 5000;
        canvasGo.AddComponent<GraphicRaycaster>();

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        _panel = new GameObject("RemoteProfilePopupRoot");
        _panel.transform.SetParent(canvasGo.transform, false);

        var panelImage = _panel.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.42f);
        panelImage.raycastTarget = true;

        var panelRect = _panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var window = CreatePanel("ProfileWindow", _panel.transform, new Vector2(0.29f, 0.2f), new Vector2(0.71f, 0.8f), BlockyUITheme.BrownBody, true);
        AddOutline(window.gameObject, new Vector2(4f, -4f), BlockyUITheme.BlackStroke);
        AddShadow(window.gameObject, new Vector2(0f, -5f), new Color(0f, 0f, 0f, 0.45f));

        var header = CreatePanel("ProfileHeader", window.transform, new Vector2(0f, 0.79f), Vector2.one, BlockyUITheme.GreenHeader, false);
        AddOutline(header.gameObject, new Vector2(2f, -2f), BlockyUITheme.BlackStroke);

        var content = CreatePanel("ProfileContent", window.transform, new Vector2(0.035f, 0.08f), new Vector2(0.965f, 0.745f), BlockyUITheme.DarkBrownPanel, false);
        AddOutline(content.gameObject, new Vector2(3f, -3f), BlockyUITheme.BlackStroke);

        var avatarCard = CreatePanel("AvatarCard", content.transform, new Vector2(0.045f, 0.34f), new Vector2(0.31f, 0.87f), BlockyUITheme.BlueHeader, false);
        AddOutline(avatarCard.gameObject, new Vector2(3f, -3f), BlockyUITheme.BlackStroke);

        _title = CreateText("Title", header.transform, new Vector2(0.055f, 0.08f), new Vector2(0.82f, 0.93f), TextAnchor.MiddleLeft, 46);
        _avatarInitial = CreateText("AvatarInitial", avatarCard.transform, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.88f), TextAnchor.MiddleCenter, 86);
        _friendCode = CreateText("FriendCode", content.transform, new Vector2(0.04f, 0.16f), new Vector2(0.47f, 0.28f), TextAnchor.MiddleLeft, 23);
        _body = CreateText("Body", content.transform, new Vector2(0.36f, 0.34f), new Vector2(0.95f, 0.88f), TextAnchor.UpperLeft, 27);
        _body.lineSpacing = 1.15f;
        _likes = CreateText("Likes", content.transform, new Vector2(0.36f, 0.18f), new Vector2(0.62f, 0.31f), TextAnchor.MiddleLeft, 28);
        _notice = CreateText("Notice", content.transform, new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.13f), TextAnchor.MiddleCenter, 23);
        _notice.color = new Color(1f, 0.92f, 0.48f, 1f);
        _notice.gameObject.SetActive(false);

        _likeButton = CreateButton("LikeButton", content.transform, L("UI/Profile/LikeButton", "Like"), new Vector2(0.64f, 0.17f), new Vector2(0.94f, 0.31f), BlockyUITheme.GreenHeader);
        _likeButtonLabel = _likeButton.GetComponentInChildren<Text>(true);

        _closeButton = CreateButton("CloseButton", header.transform, "X", new Vector2(0.88f, 0.16f), new Vector2(0.97f, 0.86f), BlockyUITheme.RedHeader);
    }

    private static RemoteProfilePopup FindLoadedInstance()
    {
        var popups = FindObjectsByType<RemoteProfilePopup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return popups != null && popups.Length > 0 ? popups[0] : null;
    }

    private bool TryBindPrefabUI()
    {
        if (_canvas == null)
            _canvas = GetComponentInChildren<Canvas>(true);

        if (_canvas == null)
            return false;

        if (_panel == null)
        {
            var panel = FindChildByName(_canvas.transform, "RemoteProfilePopupRoot");
            _panel = panel != null ? panel.gameObject : _canvas.gameObject;
        }

        var root = _panel != null ? _panel.transform : _canvas.transform;
        if (_title == null)
            _title = FindComponentByName<Text>(root, "Title");
        if (_avatarInitial == null)
            _avatarInitial = FindComponentByName<Text>(root, "AvatarInitial");
        if (_friendCode == null)
            _friendCode = FindComponentByName<Text>(root, "FriendCode");
        if (_body == null)
            _body = FindComponentByName<Text>(root, "Body");
        if (_likes == null)
            _likes = FindComponentByName<Text>(root, "Likes");
        if (_notice == null)
            _notice = FindComponentByName<Text>(root, "Notice");
        if (_closeButton == null)
            _closeButton = FindComponentByName<Button>(root, "CloseButton");
        if (_likeButton == null)
            _likeButton = FindComponentByName<Button>(root, "LikeButton");
        if (_likeButtonLabel == null && _likeButton != null)
            _likeButtonLabel = FindComponentByName<Text>(_likeButton.transform, "Label");

        return _panel != null;
    }

    private void BindButtonListeners()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Hide);
            _closeButton.onClick.AddListener(Hide);
        }

        if (_likeButton != null)
        {
            _likeButton.onClick.RemoveListener(OnLikePressed);
            _likeButton.onClick.AddListener(OnLikePressed);
        }
    }

    public void ApplyVisualStyle()
    {
        Transform root = _panel != null ? _panel.transform : transform;
        ApplyTexturedPanelStyle(root, "ProfileWindow", BlockyUITheme.BrownBody, new Vector2(5f, -5f), addShadow: true);
        ApplyTexturedPanelStyle(root, "ProfileHeader", BlockyUITheme.GreenHeader, new Vector2(3f, -3f), addShadow: false);
        ApplyTexturedPanelStyle(root, "ProfileContent", BlockyUITheme.DarkBrownPanel, new Vector2(3f, -3f), addShadow: false);
        ApplyTexturedPanelStyle(root, "AvatarCard", BlockyUITheme.BlueHeader, new Vector2(3f, -3f), addShadow: false);
        if (_likeButton != null)
            ApplyTexturedButton(_likeButton, BlockyUITheme.GreenHeader);
        if (_closeButton != null)
        {
            ApplyTexturedButton(_closeButton, BlockyUITheme.RedHeader);
            EnsureSquareCloseButton(_closeButton);
        }

        foreach (Text text in root.GetComponentsInChildren<Text>(true))
        {
            if (text == null)
                continue;
            if (_sharedFont != null)
                text.font = _sharedFont;
            BlockyUITheme.ApplyText(text, text.color, Mathf.Max(12, text.fontSize));
            ApplyHeavyTextOutline(text);
        }
    }

    public void ConfigureVisualAssets(Sprite textureSprite, Sprite buttonGradientSprite, Font sharedFont)
    {
        _textureSprite = textureSprite;
        _buttonGradientSprite = buttonGradientSprite;
        _sharedFont = sharedFont;
    }

    private void ApplyTexturedPanelStyle(
        Transform root,
        string name,
        Color color,
        Vector2 outlineDistance,
        bool addShadow)
    {
        Transform target = FindChildByName(root, name);
        if (target == null || !target.TryGetComponent(out Image image))
            return;

        if (_textureSprite != null)
            image.sprite = _textureSprite;
        image.type = image.sprite != null ? Image.Type.Tiled : Image.Type.Simple;
        image.pixelsPerUnitMultiplier = 1f;
        image.color = color;
        image.raycastTarget = true;
        AddOutline(target.gameObject, outlineDistance, BlockyUITheme.BlackStroke);
        if (addShadow)
            AddShadow(target.gameObject, new Vector2(0f, -6f), new Color(0f, 0f, 0f, 0.48f));
    }

    private void ApplyTexturedButton(Button button, Color color)
    {
        if (button == null)
            return;

        BlockyUITheme.ApplyButton(button, color);
        Image background = button.targetGraphic as Image ?? button.GetComponent<Image>();
        if (background != null)
        {
            if (_textureSprite != null)
                background.sprite = _textureSprite;
            background.type = background.sprite != null ? Image.Type.Tiled : Image.Type.Simple;
            background.pixelsPerUnitMultiplier = 1f;
            background.color = color;
            background.raycastTarget = true;
            AddOutline(background.gameObject, new Vector2(3f, -3f), BlockyUITheme.BlackStroke);
        }

        EnsureButtonGradient(button);
    }

    private void EnsureButtonGradient(Button button)
    {
        if (button == null || _buttonGradientSprite == null)
            return;

        Transform gradientTransform = button.transform.Find("Gradient");
        if (gradientTransform == null)
        {
            var gradientObject = new GameObject("Gradient", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gradientObject.transform.SetParent(button.transform, false);
            gradientTransform = gradientObject.transform;
        }

        Image gradient = gradientTransform.GetComponent<Image>();
        gradient.sprite = _buttonGradientSprite;
        gradient.type = Image.Type.Simple;
        gradient.color = new Color(1f, 1f, 1f, 0.2f);
        gradient.raycastTarget = false;

        RectTransform rect = gradientTransform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
        gradientTransform.SetAsFirstSibling();
    }

    private static void EnsureSquareCloseButton(Button button)
    {
        if (button == null || !(button.transform is RectTransform rect))
            return;

        rect.anchorMin = new Vector2(0.925f, 0.51f);
        rect.anchorMax = rect.anchorMin;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(68f, 68f);
        rect.localScale = Vector3.one;
        rect.SetAsLastSibling();
    }

    private static void ApplyHeavyTextOutline(Text text)
    {
        if (text == null)
            return;

        Outline[] outlines = text.GetComponents<Outline>();
        Outline primary = outlines.Length > 0 ? outlines[0] : text.gameObject.AddComponent<Outline>();
        Outline secondary = outlines.Length > 1 ? outlines[1] : text.gameObject.AddComponent<Outline>();

        primary.effectColor = BlockyUITheme.BlackStroke;
        primary.effectDistance = new Vector2(3f, -3f);
        primary.useGraphicAlpha = true;

        secondary.effectColor = BlockyUITheme.BlackStroke;
        secondary.effectDistance = new Vector2(-3f, 3f);
        secondary.useGraphicAlpha = true;
    }

    private void RefreshLocalizedContent()
    {
        string playerName = string.IsNullOrWhiteSpace(_displayName)
            ? L("UI/Common/Player", "Player")
            : _displayName;
        PlayerPublicStatsDto safeStats = _stats ?? new PlayerPublicStatsDto();

        if (_title != null)
            _title.text = playerName;
        if (_avatarInitial != null)
            _avatarInitial.text = string.IsNullOrWhiteSpace(playerName) ? "?" : playerName.Substring(0, 1).ToUpperInvariant();
        if (_friendCode != null)
        {
            bool hasCode = !string.IsNullOrWhiteSpace(_targetFriendCode);
            _friendCode.gameObject.SetActive(hasCode);
            if (hasCode)
                _friendCode.text = $"{L("UI/Profile/FriendCode", "Code")}: {_targetFriendCode}";
        }
        if (_body != null)
        {
            _body.text =
                $"{L("UI/Profile/IncomeAllPets", "Income/sec (all pets)")}: {FormatValue(safeStats.petsIncomePerSec)}\n" +
                $"{L("UI/Profile/IncomeBestPet", "Best pet income/sec")}: {FormatValue(safeStats.bestPetIncomePerSec)}\n" +
                $"{L("UI/Profile/HatchedTotal", "Total hatched")}: {safeStats.totalHatched}\n" +
                $"{L("UI/Profile/IncomeBigPet", "Big pet income/sec")}: {FormatValue(safeStats.bigPetIncomePerSec)}";
        }

        UpdateLikeUi();
        if (_notice != null && _notice.gameObject.activeSelf && _likedToday)
            _notice.text = BuildAlreadyLikedText(null);
    }

    private void SubscribeLocalization()
    {
        LocalizationManager.OnInstanceReady -= OnLocalizationManagerReady;
        LocalizationManager.OnInstanceReady += OnLocalizationManagerReady;
        LocalizationUtils.OnFallbackLanguageChanged -= OnLanguageChanged;
        LocalizationUtils.OnFallbackLanguageChanged += OnLanguageChanged;
        BindLocalizationManager(LocalizationManager.Instance);
    }

    private void UnsubscribeLocalization()
    {
        LocalizationManager.OnInstanceReady -= OnLocalizationManagerReady;
        LocalizationUtils.OnFallbackLanguageChanged -= OnLanguageChanged;
        if (_subscribedLocalizationManager != null)
            _subscribedLocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        _subscribedLocalizationManager = null;
    }

    private void OnLocalizationManagerReady(LocalizationManager manager)
    {
        BindLocalizationManager(manager);
        RefreshLocalizedContent();
    }

    private void BindLocalizationManager(LocalizationManager manager)
    {
        if (_subscribedLocalizationManager == manager)
            return;
        if (_subscribedLocalizationManager != null)
            _subscribedLocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        _subscribedLocalizationManager = manager;
        if (_subscribedLocalizationManager != null)
            _subscribedLocalizationManager.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged(string _)
    {
        RefreshLocalizedContent();
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
            return null;

        if (string.Equals(root.name, childName, StringComparison.Ordinal))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var match = FindChildByName(root.GetChild(i), childName);
            if (match != null)
                return match;
        }

        return null;
    }

    private static T FindComponentByName<T>(Transform root, string objectName) where T : Component
    {
        var transform = FindChildByName(root, objectName);
        return transform != null && transform.TryGetComponent<T>(out var component) ? component : null;
    }

    private static Image CreatePanel(string name, Transform parent, Vector2 min, Vector2 max, Color color, bool studs)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        BlockyUITheme.ApplyPanel(image, color, studs);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return image;
    }

    private Text CreateText(string name, Transform parent, Vector2 min, Vector2 max, TextAnchor anchor, int fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = _sharedFont != null
            ? _sharedFont
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.color = Color.white;
        text.alignment = anchor;
        text.fontStyle = FontStyle.Bold;
        text.fontSize = fontSize;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 12;
        text.resizeTextMaxSize = fontSize;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var rect = text.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        BlockyUITheme.ApplyText(text, Color.white, fontSize);
        return text;
    }

    private Button CreateButton(string name, Transform parent, string label, Vector2 min, Vector2 max, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        var button = go.AddComponent<Button>();
        button.targetGraphic = image;

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var labelText = CreateText("Label", go.transform, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, 34);
        labelText.text = label;
        labelText.color = Color.white;
        labelText.resizeTextMaxSize = 34;
        BlockyUITheme.ApplyButton(button, color);

        return button;
    }

    private static void AddOutline(GameObject go, Vector2 distance, Color color)
    {
        if (go == null)
            return;

        var outline = go.GetComponent<Outline>();
        if (outline == null)
            outline = go.AddComponent<Outline>();

        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    private static void AddShadow(GameObject go, Vector2 distance, Color color)
    {
        if (go == null)
            return;

        Shadow shadow = null;
        var shadows = go.GetComponents<Shadow>();
        for (int i = 0; i < shadows.Length; i++)
        {
            if (shadows[i] != null && !(shadows[i] is Outline))
            {
                shadow = shadows[i];
                break;
            }
        }

        if (shadow == null)
            shadow = go.AddComponent<Shadow>();

        shadow.effectColor = color;
        shadow.effectDistance = distance;
        shadow.useGraphicAlpha = true;
    }

    private static string FormatValue(double value)
    {
        return Math.Round(Math.Max(0d, value)).ToString("N0", CultureInfo.InvariantCulture);
    }

    private bool HasLikeTarget()
    {
        var hasTarget = !string.IsNullOrWhiteSpace(_targetPlayerId) || !string.IsNullOrWhiteSpace(_targetFriendCode);
        if (!hasTarget)
            return false;

        return !IsTargetLocalPlayer();
    }

    private bool IsTargetLocalPlayer()
    {
        if (G.Save == null)
            return false;

        var profile = G.Save.LoadBackendProfile();
        if (!string.IsNullOrWhiteSpace(_targetPlayerId) &&
            !string.IsNullOrWhiteSpace(profile.playerId) &&
            string.Equals(_targetPlayerId, profile.playerId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(_targetFriendCode) &&
            !string.IsNullOrWhiteSpace(profile.friendCode) &&
            string.Equals(_targetFriendCode, profile.friendCode, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private FriendsApi ResolveApi()
    {
        if (_api != null)
            return _api;

        if (G.Backend != null && G.Backend.FriendsApi != null)
        {
            _api = G.Backend.FriendsApi;
            return _api;
        }

        _api = FindAnyObjectByType<FriendsApi>();
        return _api;
    }

    private IEnumerator LoadLikeState()
    {
        var api = ResolveApi();
        if (api == null)
        {
            UpdateLikeUi();
            yield break;
        }

        var hasResponse = false;
        FriendsApi.LikeStateResponse state = null;
        yield return api.GetLikeState(
            _targetPlayerId,
            _targetFriendCode,
            onOk: resp =>
            {
                hasResponse = true;
                state = resp;
            });

        if (!hasResponse || state == null)
        {
            UpdateLikeUi();
            yield break;
        }

        _likesCount = Mathf.Max(0, state.likesCount);
        _likedToday = state.likedToday || !state.canLike;
        UpdateLikeUi();
    }

    private void OnLikePressed()
    {
        if (!HasLikeTarget())
            return;

        if (_likeRequestInFlight)
            return;

        if (_likedToday)
        {
            ShowNotice(BuildAlreadyLikedText(null), ExtraLikeNoticeSeconds);
            return;
        }

        var api = ResolveApi();
        if (api == null)
        {
            ShowNotice(L("UI/Profile/LikeUnavailable", "Like is temporarily unavailable"), ExtraLikeNoticeSeconds);
            return;
        }

        if (_sendLikeRoutine != null)
        {
            StopCoroutine(_sendLikeRoutine);
            _sendLikeRoutine = null;
        }

        _sendLikeRoutine = StartCoroutine(SendLikeRoutine(api));
    }

    private IEnumerator SendLikeRoutine(FriendsApi api)
    {
        _likeRequestInFlight = true;
        UpdateLikeUi();

        var hasResponse = false;
        FriendsApi.LikeSendResponse response = null;
        long errCode = 0;
        string errText = null;

        yield return api.SendLike(
            _targetPlayerId,
            _targetFriendCode,
            onOk: resp =>
            {
                hasResponse = true;
                response = resp;
            },
            onErr: (code, text) =>
            {
                errCode = code;
                errText = text;
            });

        _likeRequestInFlight = false;

        if (!hasResponse || response == null)
        {
            Debug.LogWarning($"[RemoteProfilePopup] Like request failed: {errCode} {errText}");
            ShowNotice(L("UI/Profile/LikeUnavailable", "Like is temporarily unavailable"), ExtraLikeNoticeSeconds);
            UpdateLikeUi();
            yield break;
        }

        _likesCount = Mathf.Max(0, response.likesCount);
        _likedToday = response.likedToday || !response.canLike || response.ok;
        UpdateLikeUi();

        if (!response.ok)
            ShowNotice(BuildAlreadyLikedText(response.nextLikeAtUtc), ExtraLikeNoticeSeconds);
    }

    private void UpdateLikeUi()
    {
        var hasTarget = HasLikeTarget();

        if (_likes != null)
        {
            _likes.gameObject.SetActive(hasTarget);
            if (hasTarget)
                _likes.text = $"{L("UI/Profile/Likes", "Likes")}: {_likesCount}";
        }

        if (_likeButton != null)
        {
            _likeButton.gameObject.SetActive(hasTarget);
            _likeButton.interactable = hasTarget && !_likeRequestInFlight && !_likedToday;
        }

        if (_likeButtonLabel != null)
        {
            if (!hasTarget)
            {
                _likeButtonLabel.text = string.Empty;
            }
            else if (_likeRequestInFlight)
            {
                _likeButtonLabel.text = L("UI/Common/Loading", "Loading...");
            }
            else if (_likedToday)
            {
                _likeButtonLabel.text = L("UI/Profile/LikedToday", "Liked today");
            }
            else
            {
                _likeButtonLabel.text = L("UI/Profile/LikeButton", "Like");
            }
        }

        if (!hasTarget && _notice != null)
            _notice.gameObject.SetActive(false);
    }

    private void ShowNotice(string text, float seconds)
    {
        if (_notice == null)
            return;

        _notice.text = text ?? string.Empty;
        _notice.gameObject.SetActive(true);

        if (_noticeRoutine != null)
        {
            StopCoroutine(_noticeRoutine);
            _noticeRoutine = null;
        }

        _noticeRoutine = StartCoroutine(HideNoticeAfter(seconds));
    }

    private IEnumerator HideNoticeAfter(float seconds)
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, seconds));
        if (_notice != null)
            _notice.gameObject.SetActive(false);
        _noticeRoutine = null;
    }

    private string BuildAlreadyLikedText(string nextLikeAtUtc)
    {
        var baseText = L("UI/Profile/LikeAlreadyToday", "You already liked this player today");
        if (!TryParseUtc(nextLikeAtUtc, out var nextUtc))
            return baseText;

        var remaining = nextUtc - DateTime.UtcNow;
        if (remaining.TotalSeconds <= 0)
            return baseText;

        var hours = Mathf.Max(0, Mathf.FloorToInt((float)remaining.TotalHours));
        var minutes = Mathf.Max(0, remaining.Minutes);
        return $"{baseText} ({hours:00}:{minutes:00})";
    }

    private static bool TryParseUtc(string value, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dto))
        {
            utc = dto.UtcDateTime;
            return true;
        }

        return false;
    }

    private static string L(string key, string fallback)
    {
        return LocalizationUtils.T(key, fallback);
    }
}
