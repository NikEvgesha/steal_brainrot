using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UIButton = UnityEngine.UI.Button;
using UIImage = UnityEngine.UI.Image;
using UIOutline = UnityEngine.UI.Outline;

public sealed class OfflineRewardWindow : MonoBehaviour
{
    private static readonly Vector2 WindowSize = new(930f, 760f);
    private const float SafeMargin = 30f;

    private OfflineRewardManager _owner;
    private OfflineRewardSnapshot _snapshot;
    private TMP_FontAsset _font;
    private Sprite _panelSprite;
    private Sprite _headerSprite;
    private Sprite _buttonSprite;
    private Sprite _premiumButtonSprite;
    private Sprite _closeSprite;
    private Sprite _closeIconSprite;
    private Sprite _coinSprite;
    private Sprite _gemSprite;
    private Sprite _adSprite;

    private RectTransform _panel;
    private TMP_Text _title;
    private TMP_Text _info;
    private TMP_Text _elapsedText;
    private TMP_Text _maxText;
    private TMP_Text _amountTitle;
    private TMP_Text _amountBefore;
    private TMP_Text _amountArrow;
    private TMP_Text _amountAfter;
    private TMP_Text _multiplyPrompt;
    private TMP_Text _activeStatus;
    private TMP_Text _activeClaimText;
    private TMP_Text _adLabel;
    private TMP_Text _monthlyBoostMainLabel;
    private TMP_Text _monthlyBoostDurationLabel;
    private UIImage _progressFill;
    private UIButton _closeButton;
    private UIButton _adButton;
    private UIButton _gemButton;
    private UIButton _activeClaimButton;
    private UIButton _monthlyBoostButton;
    private RectTransform _choiceRoot;
    private RectTransform _activeRoot;
    private CanvasGroup _canvasGroup;
    private float _nextRefreshAt;
    private bool _renderedMonthlyState;
    private LocalizationManager _subscribedLocalizationManager;

    public bool IsVisible => gameObject.activeInHierarchy &&
                             (_canvasGroup == null || _canvasGroup.alpha > 0.5f);

    public void Initialize(OfflineRewardManager owner, OfflineRewardSnapshot snapshot)
    {
        _owner = owner;
        CaptureStyleSources();
        DisableLegacyLayout();
        BuildModernLayout();
        BindButtons();
        Refresh(snapshot);
        SetVisible(true);
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

    private void Update()
    {
        UpdateResponsiveScale();

        bool monthlyActive = _owner != null && _owner.IsMonthlyBoostActive;
        if (!monthlyActive && _gemButton != null && _gemButton.gameObject.activeSelf)
        {
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 4.2f) * 0.018f;
            _gemButton.transform.localScale = new Vector3(pulse, pulse, 1f);
        }
        else if (_gemButton != null)
        {
            _gemButton.transform.localScale = Vector3.one;
        }

        if (Time.unscaledTime < _nextRefreshAt)
            return;

        _nextRefreshAt = Time.unscaledTime + 1f;
        if (monthlyActive || monthlyActive != _renderedMonthlyState)
            Refresh(_snapshot);
    }

    public void Refresh(OfflineRewardSnapshot snapshot)
    {
        _snapshot = snapshot;
        bool monthlyActive = _owner != null && _owner.IsMonthlyBoostActive;
        _renderedMonthlyState = monthlyActive;

        if (_title != null)
            _title.text = LocalizationUtils.T("UI/OfflineReward/Title", "OFFLINE REWARD");
        if (_info != null)
            _info.text = LocalizationUtils.T(
                "UI/OfflineReward/Info",
                "Your animals kept earning while you were away");

        if (_elapsedText != null)
            _elapsedText.text = LocalizationUtils.Format(
                "UI/OfflineReward/AwayTime",
                "AWAY FOR: {0}",
                OfflineRewardRules.FormatDuration(snapshot.ElapsedSeconds));

        if (_maxText != null)
            _maxText.text = LocalizationUtils.Format(
                "UI/OfflineReward/MaxAccrual",
                "Accumulates for up to {0}",
                OfflineRewardRules.FormatDuration(OfflineRewardRules.MaxAccrualSeconds));

        if (_progressFill != null)
        {
            _progressFill.fillAmount = Mathf.Clamp01(
                snapshot.CappedElapsedSeconds / (float)OfflineRewardRules.MaxAccrualSeconds);
        }

        string normalReward = FormatMoney(snapshot.DisplayedReward);
        string monthlyReward = FormatMoney(
            snapshot.GetReward(OfflineRewardRules.MonthlyBoostMultiplier));
        if (_amountTitle != null)
        {
            _amountTitle.text = monthlyActive
                ? LocalizationUtils.T("UI/OfflineReward/BoostedAmount", "BOOSTED INCOME")
                : LocalizationUtils.T("UI/OfflineReward/Amount", "ACCUMULATED");
        }

        if (_amountBefore != null)
        {
            _amountBefore.text = "$" + normalReward;
            RectTransform beforeRect = _amountBefore.rectTransform;
            beforeRect.anchoredPosition = monthlyActive
                ? new Vector2(-205f, -4f)
                : new Vector2(0f, -4f);
            beforeRect.sizeDelta = monthlyActive
                ? new Vector2(330f, 68f)
                : new Vector2(650f, 68f);
            _amountBefore.fontSizeMax = monthlyActive ? 43f : 54f;
        }

        if (_amountArrow != null)
        {
            _amountArrow.gameObject.SetActive(monthlyActive);
            _amountArrow.text = "x20  →";
        }

        if (_amountAfter != null)
        {
            _amountAfter.gameObject.SetActive(monthlyActive);
            _amountAfter.text = "$" + monthlyReward;
        }

        if (_choiceRoot != null)
            _choiceRoot.gameObject.SetActive(!monthlyActive);
        if (_monthlyBoostButton != null)
            _monthlyBoostButton.gameObject.SetActive(!monthlyActive);
        if (_activeRoot != null)
            _activeRoot.gameObject.SetActive(monthlyActive);

        if (_multiplyPrompt != null)
        {
            _multiplyPrompt.gameObject.SetActive(!monthlyActive);
            _multiplyPrompt.text = LocalizationUtils.T(
                "UI/OfflineReward/MultiplyPrompt",
                "MULTIPLY YOUR REWARD");
        }

        if (_adLabel != null)
        {
            string free = LocalizationUtils.T("UI/OfflineReward/Free", "FREE");
            _adLabel.text = _adSprite != null ? free : "▶  " + free;
        }

        if (_monthlyBoostMainLabel != null)
        {
            _monthlyBoostMainLabel.text = LocalizationUtils.Format(
                "UI/OfflineReward/MonthlyOffer",
                "BOOST OFFLINE INCOME x{0}",
                OfflineRewardRules.MonthlyBoostMultiplier);
        }

        if (_monthlyBoostDurationLabel != null)
        {
            _monthlyBoostDurationLabel.text = LocalizationUtils.Format(
                "UI/OfflineReward/MonthlyDuration",
                "FOR {0} DAYS",
                OfflineRewardRules.MonthlyBoostDurationDays);
        }

        if (monthlyActive)
        {
            long remaining = _owner != null ? _owner.GetMonthlyBoostRemainingSeconds() : 0L;
            if (_activeStatus != null)
            {
                _activeStatus.text = LocalizationUtils.Format(
                    "UI/OfflineReward/ActiveStatus",
                    "x{0} ACTIVE  •  {1} LEFT",
                    OfflineRewardRules.MonthlyBoostMultiplier,
                    FormatBoostDuration(remaining));
            }

            if (_activeClaimText != null)
            {
                _activeClaimText.text = LocalizationUtils.Format(
                    "UI/OfflineReward/Collect",
                    "COLLECT  ${0}",
                    monthlyReward);
            }
        }

        SetActionsInteractable(true);
    }

    public void SetActionsInteractable(bool interactable)
    {
        if (_closeButton != null)
            _closeButton.interactable = interactable;
        if (_adButton != null)
            _adButton.interactable = interactable;
        if (_gemButton != null)
            _gemButton.interactable = interactable;
        if (_activeClaimButton != null)
            _activeClaimButton.interactable = interactable;
        if (_monthlyBoostButton != null)
            _monthlyBoostButton.interactable = interactable;
    }

    public void SetVisible(bool visible)
    {
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        _canvasGroup.alpha = visible ? 1f : 0f;
        _canvasGroup.interactable = visible;
        _canvasGroup.blocksRaycasts = visible;
        if (G.Control != null && visible)
            G.Control.CursorActive = true;
    }

    private void CaptureStyleSources()
    {
        TMP_Text legacyTitle = FindDeepChild(transform, "Text_Title")?.GetComponent<TMP_Text>();
        _font = legacyTitle != null ? legacyTitle.font : null;

        _panelSprite = GetImageSprite(transform.Find("Bg"));
        Transform closeButton = FindDeepChild(transform, "Button_Close");
        _closeSprite = GetImageSprite(closeButton);
        _closeIconSprite = GetImageSprite(FindDeepChild(closeButton, "Icon"));
        _buttonSprite = GetImageSprite(FindDeepChild(transform, "Button_Claim"));
        _premiumButtonSprite = GetImageSprite(FindDeepChild(transform, "Button_x2Claim"));

        _coinSprite = G.Currency != null ? G.Currency.GetCurrencyIcon(CurrencyType.Coins) : null;
        _gemSprite = G.Currency != null ? G.Currency.GetCurrencyIcon(CurrencyType.Gems) : null;
        _adSprite = G.SpecialShop != null ? G.SpecialShop.RewardedAdIcon : null;

        if (G.SpecialShop == null)
            return;

        Transform shopWindow = FindDeepChild(G.SpecialShop.transform, "Window");
        Transform shopHeader = FindDeepChild(G.SpecialShop.transform, "Header");
        Sprite shopPanelSprite = GetImageSprite(shopWindow);
        Sprite shopHeaderSprite = GetImageSprite(shopHeader);
        if (shopPanelSprite != null)
            _panelSprite = shopPanelSprite;
        if (shopHeaderSprite != null)
            _headerSprite = shopHeaderSprite;
    }

    private void DisableLegacyLayout()
    {
        if (transform is RectTransform rootRect)
        {
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name == "Dimed")
            {
                child.gameObject.SetActive(true);
                if (child is RectTransform dimRect)
                {
                    dimRect.anchorMin = Vector2.zero;
                    dimRect.anchorMax = Vector2.one;
                    dimRect.offsetMin = Vector2.zero;
                    dimRect.offsetMax = Vector2.zero;
                }

                UIImage dimImage = child.GetComponent<UIImage>();
                if (dimImage != null)
                {
                    dimImage.color = new Color(0f, 0f, 0f, 0.76f);
                    dimImage.raycastTarget = true;
                }
                continue;
            }

            child.gameObject.SetActive(false);
        }
    }

    private void BuildModernLayout()
    {
        Transform existing = transform.Find("ModernOfflinePanel");
        if (existing != null)
            Destroy(existing.gameObject);

        RectTransform shadow = CreateImage(
            transform,
            "ModernOfflineShadow",
            _panelSprite,
            new Color(0.015f, 0.006f, 0.002f, 0.82f),
            WindowSize + new Vector2(22f, 22f),
            new Vector2(0f, -12f));
        shadow.SetAsLastSibling();

        _panel = CreateImage(
            transform,
            "ModernOfflinePanel",
            _panelSprite,
            new Color(0.34f, 0.13f, 0.035f, 1f),
            WindowSize,
            Vector2.zero);
        _panel.SetAsLastSibling();

        RectTransform header = CreateImage(
            _panel,
            "Header",
            _headerSprite ?? _panelSprite,
            new Color(0.19f, 0.92f, 0.045f, 1f),
            new Vector2(WindowSize.x - 24f, 92f),
            new Vector2(0f, 325f));

        _title = CreateText(
            header,
            "Title",
            LocalizationUtils.T("UI/OfflineReward/Title", "OFFLINE REWARD"),
            new Vector2(690f, 76f),
            new Vector2(-68f, 0f),
            52f,
            TextAlignmentOptions.MidlineLeft,
            Color.white);

        _closeButton = CreateButton(
            header,
            "CloseButton",
            _closeSprite,
            new Color(1f, 0.23f, 0.11f, 1f),
            new Vector2(72f, 72f),
            new Vector2(404f, 0f));
        if (_closeIconSprite != null)
        {
            CreateImage(
                _closeButton.transform,
                "CloseIcon",
                _closeIconSprite,
                Color.white,
                new Vector2(64f, 64f),
                Vector2.zero);
        }
        else
        {
            CreateText(
                _closeButton.transform,
                "X",
                "X",
                new Vector2(66f, 66f),
                Vector2.zero,
                54f,
                TextAlignmentOptions.Center,
                Color.white);
        }

        RectTransform content = CreateImage(
            _panel,
            "Content",
            _panelSprite,
            new Color(0.18f, 0.055f, 0.018f, 0.98f),
            new Vector2(880f, 624f),
            new Vector2(0f, -49f));

        _info = CreateText(
            content,
            "Info",
            string.Empty,
            new Vector2(810f, 48f),
            new Vector2(0f, 258f),
            28f,
            TextAlignmentOptions.Center,
            new Color(1f, 0.96f, 0.76f, 1f));

        RectTransform timeChip = CreateImage(
            content,
            "TimeChip",
            _buttonSprite,
            new Color(0.03f, 0.32f, 0.46f, 1f),
            new Vector2(520f, 62f),
            new Vector2(0f, 205f));
        CreateText(
            timeChip,
            "ClockIcon",
            "◷",
            new Vector2(52f, 52f),
            new Vector2(-210f, 0f),
            42f,
            TextAlignmentOptions.Center,
            new Color(0.75f, 1f, 1f, 1f));

        _elapsedText = CreateText(
            timeChip,
            "Elapsed",
            string.Empty,
            new Vector2(445f, 54f),
            new Vector2(20f, 0f),
            30f,
            TextAlignmentOptions.Center,
            Color.white);

        RectTransform progressBackground = CreateImage(
            content,
            "ProgressBackground",
            _buttonSprite,
            new Color(0.025f, 0.012f, 0.006f, 0.95f),
            new Vector2(520f, 16f),
            new Vector2(0f, 161f));
        RectTransform fillRect = CreateImage(
            progressBackground,
            "ProgressFill",
            null,
            new Color(0.34f, 1f, 0.04f, 1f),
            new Vector2(500f, 10f),
            Vector2.zero);
        fillRect.anchorMin = new Vector2(0f, 0.5f);
        fillRect.anchorMax = new Vector2(1f, 0.5f);
        fillRect.offsetMin = new Vector2(10f, -5f);
        fillRect.offsetMax = new Vector2(-10f, 5f);
        _progressFill = fillRect.GetComponent<UIImage>();
        _progressFill.type = UIImage.Type.Filled;
        _progressFill.fillMethod = UIImage.FillMethod.Horizontal;
        _progressFill.fillOrigin = 0;

        _maxText = CreateText(
            content,
            "Maximum",
            string.Empty,
            new Vector2(500f, 34f),
            new Vector2(0f, 137f),
            21f,
            TextAlignmentOptions.Center,
            new Color(0.82f, 0.78f, 0.72f, 1f));

        RectTransform amountCard = CreateImage(
            content,
            "AmountCard",
            _premiumButtonSprite ?? _buttonSprite,
            Color.white,
            new Vector2(780f, 116f),
            new Vector2(0f, 55f));
        _amountTitle = CreateText(
            amountCard,
            "AmountTitle",
            string.Empty,
            new Vector2(710f, 32f),
            new Vector2(0f, 37f),
            22f,
            TextAlignmentOptions.Center,
            new Color(1f, 1f, 0.25f, 1f));

        if (_coinSprite != null)
        {
            CreateImage(
                amountCard,
                "CoinIcon",
                _coinSprite,
                Color.white,
                new Vector2(64f, 64f),
                new Vector2(-335f, -8f));
        }

        _amountBefore = CreateText(
            amountCard,
            "BeforeAmount",
            string.Empty,
            new Vector2(650f, 68f),
            new Vector2(0f, -4f),
            54f,
            TextAlignmentOptions.Center,
            Color.white);
        _amountArrow = CreateText(
            amountCard,
            "Arrow",
            string.Empty,
            new Vector2(145f, 60f),
            new Vector2(0f, -4f),
            37f,
            TextAlignmentOptions.Center,
            new Color(1f, 0.96f, 0.12f, 1f));
        _amountAfter = CreateText(
            amountCard,
            "AfterAmount",
            string.Empty,
            new Vector2(330f, 68f),
            new Vector2(205f, -4f),
            43f,
            TextAlignmentOptions.Center,
            Color.white);

        _multiplyPrompt = CreateText(
            content,
            "MultiplyPrompt",
            string.Empty,
            new Vector2(780f, 38f),
            new Vector2(0f, -19f),
            25f,
            TextAlignmentOptions.Center,
            new Color(1f, 0.96f, 0.17f, 1f));

        BuildChoiceButtons(content);
        BuildMonthlyOffer(content);
        BuildActiveState(content);
        UpdateResponsiveScale();
    }

    private void BuildChoiceButtons(RectTransform content)
    {
        _choiceRoot = CreateRect(
            content,
            "ChoiceButtons",
            new Vector2(820f, 150f),
            new Vector2(0f, -109f));

        _adButton = CreateButton(
            _choiceRoot,
            "AdButton",
            _buttonSprite,
            new Color(0.04f, 0.58f, 1f, 1f),
            new Vector2(330f, 116f),
            new Vector2(-218f, 0f));
        CreateText(
            _adButton.transform,
            "Multiplier",
            "x5",
            new Vector2(300f, 58f),
            new Vector2(0f, 24f),
            45f,
            TextAlignmentOptions.Center,
            Color.white);
        if (_adSprite != null)
        {
            CreateImage(
                _adButton.transform,
                "AdIcon",
                _adSprite,
                Color.white,
                new Vector2(38f, 38f),
                new Vector2(-87f, -29f));
        }
        _adLabel = CreateText(
            _adButton.transform,
            "AdLabel",
            string.Empty,
            new Vector2(250f, 42f),
            new Vector2(18f, -31f),
            24f,
            TextAlignmentOptions.Center,
            Color.white);

        _gemButton = CreateButton(
            _choiceRoot,
            "GemButton",
            _premiumButtonSprite ?? _buttonSprite,
            Color.white,
            new Vector2(420f, 142f),
            new Vector2(187f, 0f));
        CreateText(
            _gemButton.transform,
            "Multiplier",
            "x10",
            new Vector2(380f, 68f),
            new Vector2(0f, 29f),
            54f,
            TextAlignmentOptions.Center,
            Color.white);
        if (_gemSprite != null)
        {
            CreateImage(
                _gemButton.transform,
                "GemIcon",
                _gemSprite,
                Color.white,
                new Vector2(48f, 48f),
                new Vector2(-37f, -35f));
        }
        CreateText(
            _gemButton.transform,
            "GemPrice",
            OfflineRewardRules.BoostPriceGems.ToString(),
            new Vector2(135f, 48f),
            new Vector2(42f, -35f),
            33f,
            TextAlignmentOptions.MidlineLeft,
            Color.white);
    }

    private void BuildMonthlyOffer(RectTransform content)
    {
        _monthlyBoostButton = CreateButton(
            content,
            "MonthlyBoostButton",
            _premiumButtonSprite ?? _buttonSprite,
            new Color(0.82f, 0.44f, 1f, 1f),
            new Vector2(820f, 108f),
            new Vector2(0f, -245f));
        _monthlyBoostMainLabel = CreateText(
            _monthlyBoostButton.transform,
            "MainLabel",
            string.Empty,
            new Vector2(590f, 50f),
            new Vector2(-88f, 20f),
            31f,
            TextAlignmentOptions.Center,
            Color.white);
        _monthlyBoostDurationLabel = CreateText(
            _monthlyBoostButton.transform,
            "Duration",
            string.Empty,
            new Vector2(360f, 36f),
            new Vector2(-200f, -25f),
            23f,
            TextAlignmentOptions.Center,
            new Color(1f, 1f, 0.2f, 1f));
        if (_gemSprite != null)
        {
            CreateImage(
                _monthlyBoostButton.transform,
                "GemIcon",
                _gemSprite,
                Color.white,
                new Vector2(52f, 52f),
                new Vector2(285f, -14f));
        }
        CreateText(
            _monthlyBoostButton.transform,
            "Price",
            OfflineRewardRules.MonthlyBoostPriceGems.ToString(),
            new Vector2(125f, 50f),
            new Vector2(355f, -14f),
            35f,
            TextAlignmentOptions.MidlineLeft,
            Color.white);
    }

    private void BuildActiveState(RectTransform content)
    {
        _activeRoot = CreateRect(
            content,
            "ActiveBoostState",
            new Vector2(820f, 250f),
            new Vector2(0f, -127f));

        _activeStatus = CreateText(
            _activeRoot,
            "Status",
            string.Empty,
            new Vector2(790f, 58f),
            new Vector2(0f, 73f),
            30f,
            TextAlignmentOptions.Center,
            new Color(0.5f, 1f, 0.16f, 1f));

        _activeClaimButton = CreateButton(
            _activeRoot,
            "CollectButton",
            _buttonSprite,
            new Color(0.22f, 1f, 0.025f, 1f),
            new Vector2(660f, 118f),
            new Vector2(0f, -18f));
        _activeClaimText = CreateText(
            _activeClaimButton.transform,
            "Label",
            string.Empty,
            new Vector2(620f, 94f),
            Vector2.zero,
            41f,
            TextAlignmentOptions.Center,
            Color.white);
    }

    private void BindButtons()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveAllListeners();
            _closeButton.onClick.AddListener(() => _owner?.ClaimNormal("close_button"));
        }

        if (_adButton != null)
        {
            _adButton.onClick.RemoveAllListeners();
            _adButton.onClick.AddListener(() => _owner?.ClaimWithAd());
        }

        if (_gemButton != null)
        {
            _gemButton.onClick.RemoveAllListeners();
            _gemButton.onClick.AddListener(() => _owner?.ClaimWithGems());
        }

        if (_monthlyBoostButton != null)
        {
            _monthlyBoostButton.onClick.RemoveAllListeners();
            _monthlyBoostButton.onClick.AddListener(() => _owner?.PurchaseMonthlyBoost());
        }

        if (_activeClaimButton != null)
        {
            _activeClaimButton.onClick.RemoveAllListeners();
            _activeClaimButton.onClick.AddListener(() => _owner?.ClaimNormal("monthly_collect"));
        }
    }

    private void UpdateResponsiveScale()
    {
        if (_panel == null || !(transform is RectTransform viewport))
            return;

        Vector2 size = viewport.rect.size;
        if (size.x <= 0f || size.y <= 0f)
            return;

        float scale = Mathf.Min(
            1f,
            Mathf.Min(
                Mathf.Max(1f, size.x - SafeMargin * 2f) / WindowSize.x,
                Mathf.Max(1f, size.y - SafeMargin * 2f) / WindowSize.y));
        Transform shadow = transform.Find("ModernOfflineShadow");
        if (shadow != null)
            shadow.localScale = new Vector3(scale, scale, 1f);
        _panel.localScale = new Vector3(scale, scale, 1f);
    }

    private RectTransform CreateRect(
        Transform parent,
        string objectName,
        Vector2 size,
        Vector2 position)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private RectTransform CreateImage(
        Transform parent,
        string objectName,
        Sprite sprite,
        Color color,
        Vector2 size,
        Vector2 position)
    {
        var go = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(UIImage));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        UIImage image = go.GetComponent<UIImage>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = false;
        image.type = sprite != null && sprite.border.sqrMagnitude > 0f
            ? UIImage.Type.Sliced
            : UIImage.Type.Simple;
        image.raycastTarget = false;
        return rect;
    }

    private UIButton CreateButton(
        Transform parent,
        string objectName,
        Sprite sprite,
        Color color,
        Vector2 size,
        Vector2 position)
    {
        RectTransform rect = CreateImage(
            parent,
            objectName,
            sprite,
            color,
            size,
            position);
        UIImage image = rect.GetComponent<UIImage>();
        image.raycastTarget = true;
        UIButton button = rect.gameObject.AddComponent<UIButton>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.65f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        return button;
    }

    private TMP_Text CreateText(
        Transform parent,
        string objectName,
        string value,
        Vector2 size,
        Vector2 position,
        float maxSize,
        TextAlignmentOptions alignment,
        Color color)
    {
        var go = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI),
            typeof(UIOutline));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = _font;
        text.fontStyle = FontStyles.Bold;
        text.color = color;
        text.alignment = alignment;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(13f, maxSize * 0.48f);
        text.fontSizeMax = maxSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.margin = new Vector4(5f, 2f, 5f, 2f);
        text.raycastTarget = false;

        var outline = go.GetComponent<UIOutline>();
        outline.effectColor = new Color(0.015f, 0.008f, 0.004f, 0.98f);
        outline.effectDistance = new Vector2(2.5f, -2.5f);
        outline.useGraphicAlpha = true;
        return text;
    }

    private static Sprite GetImageSprite(Transform target)
    {
        return target != null ? target.GetComponent<UIImage>()?.sprite : null;
    }

    private static Transform FindDeepChild(Transform parent, string objectName)
    {
        if (parent == null)
            return null;
        if (string.Equals(parent.name, objectName, StringComparison.Ordinal))
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static string FormatMoney(double amount)
    {
        if (G.Currency != null)
            return G.Currency.ToString(Math.Max(0d, amount));
        return Math.Round(Math.Max(0d, amount)).ToString("0");
    }

    private static string FormatBoostDuration(long seconds)
    {
        seconds = Math.Max(0L, seconds);
        long days = seconds / 86400L;
        long hours = seconds % 86400L / 3600L;
        long minutes = seconds % 3600L / 60L;
        if (days > 0L)
            return LocalizationUtils.Format(
                "UI/Duration/DaysHoursShort",
                "{0}d {1:00}h",
                days,
                hours);
        if (hours > 0L)
            return LocalizationUtils.Format(
                "UI/Duration/HoursMinutesShort",
                "{0}h {1:00}m",
                hours,
                minutes);
        return LocalizationUtils.Format("UI/Duration/MinutesShort", "{0}m", minutes);
    }

    private void HandleLocalizationManagerReady(LocalizationManager manager)
    {
        SubscribeToLocalizationManager(manager);
        HandleLanguageChanged(manager != null ? manager.CurrentLanguage : string.Empty);
    }

    private void HandleLanguageChanged(string _)
    {
        if (_panel != null)
            Refresh(_snapshot);
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
