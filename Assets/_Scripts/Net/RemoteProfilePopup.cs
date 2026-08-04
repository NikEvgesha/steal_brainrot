using System;
using System.Collections;
using System.Globalization;
using TMPro;
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
    private TMP_Text _title;
    [SerializeField]
    private TMP_Text _avatarInitial;
    [SerializeField]
    private TMP_Text _friendCode;
    [SerializeField]
    private TMP_Text _body;
    [SerializeField]
    private TMP_Text _likes;
    [SerializeField]
    private TMP_Text _notice;
    [SerializeField]
    private Button _closeButton;
    [SerializeField]
    private Button _likeButton;
    [SerializeField]
    private TMP_Text _likeButtonLabel;

    [Header("Visual Theme")]
    [SerializeField]
    private Sprite _textureSprite;
    [SerializeField]
    private Sprite _buttonGradientSprite;
    [SerializeField]
    private TMP_FontAsset _sharedFont;
    [SerializeField]
    private Sprite _editIconSprite;

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
    private Button _editNameButton;
    private Button _copyCodeButton;
    private Image _editNameIcon;
    private TMP_Text _editConfirmLabel;
    private TMP_InputField _nameInput;
    private Coroutine _renameRoutine;
    private bool _isLocalOwner;
    private bool _nameEditing;
    private bool _renameInFlight;
    private int _profileViewVersion;

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
        if (_editNameButton != null)
            _editNameButton.onClick.RemoveListener(OnEditNamePressed);
        if (_copyCodeButton != null)
            _copyCodeButton.onClick.RemoveListener(OnCopyCodePressed);
        if (_nameInput != null)
            _nameInput.onSubmit.RemoveListener(OnNameSubmitted);
    }

    public void Show(
        string displayName,
        PlayerPublicStatsDto stats,
        string targetPlayerId = null,
        string targetFriendCode = null,
        bool isLocalOwner = false)
    {
        if (_panel == null)
            BuildUI();

        _displayName = displayName;
        _stats = stats ?? new PlayerPublicStatsDto();
        _targetPlayerId = string.IsNullOrWhiteSpace(targetPlayerId) ? null : targetPlayerId;
        _targetFriendCode = string.IsNullOrWhiteSpace(targetFriendCode) ? null : targetFriendCode;
        _isLocalOwner = isLocalOwner || IsTargetLocalPlayer();
        _profileViewVersion++;
        _likedToday = false;
        _likesCount = 0;
        _likeRequestInFlight = false;
        SetNameEditMode(false);

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

        GameAnalytics.Track(AnalyticsEventNames.RemoteProfileOpened, GameAnalytics.Params(
            "target_id_hash", GameAnalytics.HashId(_targetPlayerId ?? _targetFriendCode),
            "has_public_stats", stats != null,
            "likes_count", _likesCount,
            "source", "remote_profile",
            "result", "success"));
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

        SetNameEditMode(false);

        if (_panel != null)
            _panel.SetActive(false);
    }

    private void BuildUI()
    {
        if (TryBindPrefabUI())
        {
            EnsureProfileActions();
            return;
        }

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

        _title = CreateText("Title", header.transform, new Vector2(0.055f, 0.08f), new Vector2(0.82f, 0.93f), TextAlignmentOptions.Left, 46);
        _avatarInitial = CreateText("AvatarInitial", avatarCard.transform, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.88f), TextAlignmentOptions.Center, 86);
        _friendCode = CreateText("FriendCode", content.transform, new Vector2(0.04f, 0.16f), new Vector2(0.47f, 0.28f), TextAlignmentOptions.Left, 23);
        _body = CreateText("Body", content.transform, new Vector2(0.36f, 0.34f), new Vector2(0.95f, 0.88f), TextAlignmentOptions.TopLeft, 27);
        _body.lineSpacing = 1.15f;
        _likes = CreateText("Likes", content.transform, new Vector2(0.36f, 0.18f), new Vector2(0.62f, 0.31f), TextAlignmentOptions.Left, 28);
        _notice = CreateText("Notice", content.transform, new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.13f), TextAlignmentOptions.Center, 23);
        _notice.color = new Color(1f, 0.92f, 0.48f, 1f);
        _notice.gameObject.SetActive(false);

        _likeButton = CreateButton("LikeButton", content.transform, L("UI/Profile/LikeButton", "Like"), new Vector2(0.64f, 0.17f), new Vector2(0.94f, 0.31f), BlockyUITheme.GreenHeader);
        _likeButtonLabel = _likeButton.GetComponentInChildren<TMP_Text>(true);

        _closeButton = CreateButton("CloseButton", header.transform, "X", new Vector2(0.88f, 0.16f), new Vector2(0.97f, 0.86f), BlockyUITheme.RedHeader);
        EnsureProfileActions();
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
            _title = FindComponentByName<TMP_Text>(root, "Title");
        if (_avatarInitial == null)
            _avatarInitial = FindComponentByName<TMP_Text>(root, "AvatarInitial");
        if (_friendCode == null)
            _friendCode = FindComponentByName<TMP_Text>(root, "FriendCode");
        if (_body == null)
            _body = FindComponentByName<TMP_Text>(root, "Body");
        if (_likes == null)
            _likes = FindComponentByName<TMP_Text>(root, "Likes");
        if (_notice == null)
            _notice = FindComponentByName<TMP_Text>(root, "Notice");
        if (_closeButton == null)
            _closeButton = FindComponentByName<Button>(root, "CloseButton");
        if (_likeButton == null)
            _likeButton = FindComponentByName<Button>(root, "LikeButton");
        if (_likeButtonLabel == null && _likeButton != null)
            _likeButtonLabel = FindComponentByName<TMP_Text>(_likeButton.transform, "Label");

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

        if (_editNameButton != null)
        {
            _editNameButton.onClick.RemoveListener(OnEditNamePressed);
            _editNameButton.onClick.AddListener(OnEditNamePressed);
        }

        if (_copyCodeButton != null)
        {
            _copyCodeButton.onClick.RemoveListener(OnCopyCodePressed);
            _copyCodeButton.onClick.AddListener(OnCopyCodePressed);
        }

        if (_nameInput != null)
        {
            _nameInput.onSubmit.RemoveListener(OnNameSubmitted);
            _nameInput.onSubmit.AddListener(OnNameSubmitted);
        }
    }

    public void ApplyVisualStyle()
    {
        Transform root = _panel != null ? _panel.transform : transform;
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text == null)
                continue;
            if (_sharedFont != null)
            {
                text.font = _sharedFont;
                text.fontSharedMaterial = _sharedFont.material;
            }
            BlockyUITheme.ApplyText(text, text.color, Mathf.Max(12, Mathf.RoundToInt(text.fontSize)));
            ApplyHeavyTextOutline(text);
        }

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
        if (_editNameButton != null)
            ApplyTexturedButton(_editNameButton, BlockyUITheme.BlueHeader);
        if (_copyCodeButton != null)
            ApplyTexturedButton(_copyCodeButton, BlockyUITheme.BlueHeader);
        ApplyNameInputStyle();

    }

    public void ConfigureVisualAssets(
        Sprite textureSprite,
        Sprite buttonGradientSprite,
        TMP_FontAsset sharedFont,
        Sprite editIconSprite = null)
    {
        _textureSprite = textureSprite;
        _buttonGradientSprite = buttonGradientSprite;
        _sharedFont = sharedFont;
        if (editIconSprite != null)
            _editIconSprite = editIconSprite;
    }

    private void EnsureProfileActions()
    {
        Transform root = _panel != null ? _panel.transform : transform;
        Transform header = FindChildByName(root, "ProfileHeader");
        Transform content = FindChildByName(root, "ProfileContent");
        if (header == null || content == null)
            return;

        if (_editNameButton == null)
            _editNameButton = FindComponentByName<Button>(root, "EditNameButton");
        if (_copyCodeButton == null)
            _copyCodeButton = FindComponentByName<Button>(root, "CopyCodeButton");
        if (_nameInput == null)
            _nameInput = FindComponentByName<TMP_InputField>(root, "NameInput");

        if (_editNameButton == null)
            _editNameButton = CreateEditNameButton(header);
        if (_copyCodeButton == null)
            _copyCodeButton = CreateCopyCodeButton(content);
        if (_nameInput == null)
            _nameInput = CreateNameInput(header);

        if (_editNameButton != null)
        {
            _editNameIcon = FindComponentByName<Image>(_editNameButton.transform, "PencilIcon");
            _editConfirmLabel = FindComponentByName<TMP_Text>(_editNameButton.transform, "ConfirmLabel");
        }

        if (_title != null)
        {
            RectTransform titleRect = _title.rectTransform;
            titleRect.anchorMax = new Vector2(Mathf.Min(titleRect.anchorMax.x, 0.78f), titleRect.anchorMax.y);
        }

        UpdateOwnerActions();
    }

    private Button CreateEditNameButton(Transform header)
    {
        var buttonObject = new GameObject("EditNameButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.layer = header.gameObject.layer;
        buttonObject.transform.SetParent(header, false);

        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.82f, 0.5f);
        rect.anchorMax = rect.anchorMin;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(58f, 58f);

        var background = buttonObject.GetComponent<Image>();
        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;

        var iconObject = new GameObject("PencilIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObject.layer = buttonObject.layer;
        iconObject.transform.SetParent(buttonObject.transform, false);
        var iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.17f, 0.17f);
        iconRect.anchorMax = new Vector2(0.83f, 0.83f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        var icon = iconObject.GetComponent<Image>();
        icon.sprite = _editIconSprite;
        icon.color = Color.white;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        var confirm = CreateText("ConfirmLabel", buttonObject.transform, Vector2.zero, Vector2.one, TextAlignmentOptions.Center, 25);
        confirm.text = "OK";
        confirm.raycastTarget = false;
        confirm.gameObject.SetActive(false);

        return button;
    }

    private Button CreateCopyCodeButton(Transform content)
    {
        var buttonObject = new GameObject("CopyCodeButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.layer = content.gameObject.layer;
        buttonObject.transform.SetParent(content, false);

        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.52f, 0.22f);
        rect.anchorMax = rect.anchorMin;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(54f, 54f);

        var background = buttonObject.GetComponent<Image>();
        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;

        CreateCopySheet(buttonObject.transform, "CopyBack", new Vector2(-4f, 4f));
        CreateCopySheet(buttonObject.transform, "CopyFront", new Vector2(5f, -5f));
        return button;
    }

    private static void CreateCopySheet(Transform parent, string name, Vector2 position)
    {
        var sheetObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        sheetObject.layer = parent.gameObject.layer;
        sheetObject.transform.SetParent(parent, false);
        var rect = sheetObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = rect.anchorMin;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(25f, 29f);
        var image = sheetObject.GetComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;
        AddOutline(sheetObject, new Vector2(2f, -2f), BlockyUITheme.BlackStroke);
    }

    private TMP_InputField CreateNameInput(Transform header)
    {
        var inputObject = new GameObject("NameInput", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
        inputObject.layer = header.gameObject.layer;
        inputObject.transform.SetParent(header, false);
        var rect = inputObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.055f, 0.18f);
        rect.anchorMax = new Vector2(0.785f, 0.84f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var text = CreateText("InputText", inputObject.transform, Vector2.zero, Vector2.one, TextAlignmentOptions.Left, 42);
        text.margin = new Vector4(14f, 2f, 14f, 2f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;

        var placeholder = CreateText("Placeholder", inputObject.transform, Vector2.zero, Vector2.one, TextAlignmentOptions.Left, 42);
        placeholder.margin = new Vector4(14f, 2f, 14f, 2f);
        placeholder.color = new Color(1f, 1f, 1f, 0.45f);
        placeholder.text = L("UI/Friends/Nickname", "Nickname");

        var input = inputObject.GetComponent<TMP_InputField>();
        input.targetGraphic = inputObject.GetComponent<Image>();
        input.textViewport = text.rectTransform;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.characterLimit = 16;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = TMP_InputField.ContentType.Standard;
        input.restoreOriginalTextOnEscape = true;
        inputObject.SetActive(false);
        return input;
    }

    private void ApplyNameInputStyle()
    {
        if (_nameInput == null)
            return;

        var background = _nameInput.targetGraphic as Image ?? _nameInput.GetComponent<Image>();
        if (background != null)
        {
            if (_textureSprite != null)
                background.sprite = _textureSprite;
            background.type = background.sprite != null ? Image.Type.Tiled : Image.Type.Simple;
            background.pixelsPerUnitMultiplier = 1f;
            background.color = new Color(0.11f, 0.055f, 0.025f, 0.94f);
            AddOutline(background.gameObject, new Vector2(3f, -3f), BlockyUITheme.BlackStroke);
        }
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

    private static void ApplyHeavyTextOutline(TMP_Text text)
    {
        if (text == null)
            return;

        text.outlineColor = BlockyUITheme.BlackStroke;
        text.outlineWidth = Mathf.Max(text.outlineWidth, 0.18f);
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
        UpdateOwnerActions();
        if (_notice != null && _notice.gameObject.activeSelf && _likedToday)
            _notice.text = BuildAlreadyLikedText(null);
    }

    private void UpdateOwnerActions()
    {
        bool canRename = _isLocalOwner && ResolveApi() != null;
        bool canCopyCode = _isLocalOwner && !string.IsNullOrWhiteSpace(_targetFriendCode);

        if (_editNameButton != null)
        {
            _editNameButton.gameObject.SetActive(_isLocalOwner);
            _editNameButton.interactable = canRename && !_renameInFlight;
        }

        if (_copyCodeButton != null)
        {
            _copyCodeButton.gameObject.SetActive(canCopyCode);
            _copyCodeButton.interactable = canCopyCode;
        }

        if (!_isLocalOwner && _nameEditing)
            SetNameEditMode(false);
    }

    private void OnEditNamePressed()
    {
        if (!_isLocalOwner || _renameInFlight)
            return;

        if (!_nameEditing)
        {
            SetNameEditMode(true);
            return;
        }

        TryStartRename();
    }

    private void OnNameSubmitted(string _)
    {
        TryStartRename();
    }

    private void TryStartRename()
    {
        if (!_isLocalOwner || !_nameEditing || _renameInFlight || _nameInput == null)
            return;

        string newName = (_nameInput.text ?? string.Empty).Trim();
        if (newName.Length < 3 || newName.Length > 16)
        {
            ShowNotice(L("UI/Friends/MinNameLength", "Minimum 3 characters"), ExtraLikeNoticeSeconds);
            _nameInput.ActivateInputField();
            return;
        }

        if (string.Equals(newName, (_displayName ?? string.Empty).Trim(), StringComparison.Ordinal))
        {
            SetNameEditMode(false);
            return;
        }

        FriendsApi api = ResolveApi();
        if (api == null)
        {
            ShowNotice(L("UI/Friends/FailedRename", "Failed to rename"), ExtraLikeNoticeSeconds);
            return;
        }

        _renameRoutine = StartCoroutine(RenameRoutine(api, newName, _profileViewVersion));
    }

    private IEnumerator RenameRoutine(FriendsApi api, string newName, int viewVersion)
    {
        _renameInFlight = true;
        if (_nameInput != null)
            _nameInput.interactable = false;
        UpdateOwnerActions();
        ShowNotice(L("UI/Common/Loading", "Loading..."), 30f);

        bool success = false;
        string failure = null;
        yield return api.RenameMePaid(
            newName,
            onOk: value => success = value,
            onFail: message => failure = message);

        _renameInFlight = false;
        _renameRoutine = null;
        bool ownsCurrentView = viewVersion == _profileViewVersion && _isLocalOwner;
        if (ownsCurrentView && _nameInput != null)
            _nameInput.interactable = true;

        if (!ownsCurrentView)
        {
            UpdateOwnerActions();
            yield break;
        }

        if (!success)
        {
            string message = !string.IsNullOrWhiteSpace(failure) && !failure.TrimStart().StartsWith("{", StringComparison.Ordinal)
                ? failure
                : L("UI/Friends/FailedRename", "Failed to rename");
            ShowNotice(message, ExtraLikeNoticeSeconds);
            UpdateOwnerActions();
            if (_nameInput != null)
                _nameInput.ActivateInputField();
            yield break;
        }

        var profile = api.LocalProfile();
        _displayName = string.IsNullOrWhiteSpace(profile.displayName) ? newName : profile.displayName.Trim();
        SetNameEditMode(false);
        RefreshLocalizedContent();
        ShowNotice(L("UI/Friends/NicknameUpdated", "Nickname updated"), ExtraLikeNoticeSeconds);
    }

    private void SetNameEditMode(bool editing)
    {
        _nameEditing = editing && _isLocalOwner && _nameInput != null;

        if (_title != null)
            _title.gameObject.SetActive(!_nameEditing);
        if (_nameInput != null)
            _nameInput.gameObject.SetActive(_nameEditing);
        if (_editNameIcon != null)
            _editNameIcon.gameObject.SetActive(!_nameEditing);
        if (_editConfirmLabel != null)
            _editConfirmLabel.gameObject.SetActive(_nameEditing);

        if (!_nameEditing || _nameInput == null)
            return;

        _nameInput.interactable = !_renameInFlight;
        _nameInput.text = string.IsNullOrWhiteSpace(_displayName) ? string.Empty : _displayName.Trim();
        _nameInput.Select();
        _nameInput.ActivateInputField();
        _nameInput.MoveTextEnd(false);
    }

    private void OnCopyCodePressed()
    {
        if (!_isLocalOwner || string.IsNullOrWhiteSpace(_targetFriendCode))
            return;

        bool copied = ZooClipboard.TryCopyText(_targetFriendCode.Trim());
        ShowNotice(
            copied
                ? L("UI/Friends/Copied", "Copied")
                : L("UI/Profile/CopyFailed", "Failed to copy code"),
            ExtraLikeNoticeSeconds);
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

    private TMP_Text CreateText(string name, Transform parent, Vector2 min, Vector2 max, TextAlignmentOptions anchor, int fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var text = TmpUiTextFactory.Add(go);
        if (_sharedFont != null)
            text.font = _sharedFont;
        text.color = Color.white;
        text.alignment = anchor;
        text.fontStyle = FontStyles.Bold;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = 12;
        text.fontSizeMax = fontSize;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
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

        var labelText = CreateText("Label", go.transform, Vector2.zero, Vector2.one, TextAlignmentOptions.Center, 34);
        labelText.text = label;
        labelText.color = Color.white;
        labelText.fontSizeMax = 34;
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
            TrackLikeResult(false, errCode != 0 ? "backend_error_" + errCode : "empty_response");
            yield break;
        }

        _likesCount = Mathf.Max(0, response.likesCount);
        _likedToday = response.likedToday || !response.canLike || response.ok;
        UpdateLikeUi();

        if (response.ok)
        {
            G.Sound?.Play(GameAudioId.SFX_LIKE);
            TrackLikeResult(true, string.Empty);
        }
        else
        {
            ShowNotice(BuildAlreadyLikedText(response.nextLikeAtUtc), ExtraLikeNoticeSeconds);
            TrackLikeResult(false, "already_liked_or_limited");
        }
    }

    private void TrackLikeResult(bool success, string failureReason)
    {
        GameAnalytics.TrackCritical(AnalyticsEventNames.LikeResult, GameAnalytics.Params(
            "target_id_hash", GameAnalytics.HashId(_targetPlayerId ?? _targetFriendCode),
            "likes_count", _likesCount,
            "source", "remote_profile",
            "result", success ? "success" : "failed",
            "failure_reason", failureReason ?? string.Empty));
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
