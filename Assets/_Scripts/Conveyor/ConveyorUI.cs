using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ConveyorUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _levelName;
    [SerializeField] private Image _icon;
    [SerializeField] private Image _newEggIcon;
    [SerializeField] private TMP_Text _priceCoins;
    [SerializeField] private TMP_Text _priceGems;
    [SerializeField] private TMP_Text _incomeMultiplier;
    [SerializeField] private Button _buttonBuyGems;
    [SerializeField] private Button _buttonBuyCoins;
    [SerializeField] private Sprite _buyButtonGradientSprite;
    [SerializeField] private Button _buttonActivate;
    [SerializeField] private GameObject _activeText;
    [SerializeField] private GameObject _notAvailableText;
    [SerializeField] private TMP_Text _dropChancesText;
    [SerializeField] private bool _showLuckComparison = true;
    [SerializeField] private bool _showEggBreakdown = true;
    [SerializeField] private string _chancesBaseLocalizationKey = "UI/Conveyor/ChancesBase";
    [SerializeField] private string _chancesBaseText = "Without luck bonus";
    [SerializeField] private string _chancesWithLuckLocalizationKey = "UI/Conveyor/ChancesWithLuck";
    [SerializeField] private string _chancesWithLuckText = "With luck bonus";
    [SerializeField] private string _eggBreakdownLocalizationKey = "UI/Conveyor/EggBreakdown";
    [SerializeField] private string _eggBreakdownText = "Eggs and possible hatch outcomes";
    [SerializeField] private ConveyorLevelTab _tabPrefab;
    [SerializeField] private Transform _tansParent;
    [SerializeField] private EggDropCatalogUI _chancesPanel;
    [SerializeField] private Transform _newEggPetsPanel;
    [SerializeField] private GameObject _newEggPetIcon;

    private GameObject _panel;
    private bool _isOpen;
    private ConveyorLevel _currentLevelInfo;
    private int _currentActiveIdx;
    private List<ConveyorLevelTab> _tabs;
    private Conveyor _conveyor;
    private readonly List<ConveyorDropChanceCalculator.ChanceEntry> _cachedDropChances = new();
    private readonly List<ConveyorDropChanceCalculator.ChanceEntry> _cachedDropChancesWithLuck = new();
    private readonly List<ConveyorDropChanceCalculator.EggBreakdownEntry> _cachedEggBreakdown = new();
    private LocalizationManager _subscribedLocalizationManager;
    private CurrencyManager _subscribedCurrencyManager;
    private Button _closeButton;

    [HideInInspector]
    public UnityEvent<ConveyorLevel> LevelActivated = new();

    public bool IsOpen => _isOpen;
    public ConveyorLevel CurrentLevelInfo => _currentLevelInfo;
    public Transform CoinBuyTarget => _buttonBuyCoins != null ? _buttonBuyCoins.transform : transform;



    public void Init(List<ConveyorLevel> levels)
    {
        _tabs = new List<ConveyorLevelTab>();
        if (levels == null || levels.Count == 0)
            return;

        if (_tabPrefab != null && _tansParent != null)
        {
            foreach (ConveyorLevel level in levels)
            {
                if (level == null)
                    continue;

                ConveyorLevelTab tab = Instantiate(_tabPrefab, _tansParent);
                tab.Init(level);
                tab.OnClick.AddListener(SetInfo);
                _tabs.Add(tab);
            }
        }

        if (_tabs.Count > _currentActiveIdx && _tabs[_currentActiveIdx] != null)
            _tabs[_currentActiveIdx].SetLvlActive(true);
        //_currentLevelInfo = levels[0];
        SetInfo(levels[0]);
    }

    private void Awake()
    {
        _conveyor = GetComponentInParent<Conveyor>(true);
        EnsurePanel();
        ConfigureResponsiveLayout();
        ConfigureHeaderVisuals();
        ConfigureInfoCardVisuals();
        StyleIncomeBonusDisplay();
    }

    private void OnEnable()
    {
        LocalizationManager.OnInstanceReady += OnLocalizationManagerReady;
        LocalizationUtils.OnFallbackLanguageChanged -= OnLanguageChanged;
        LocalizationUtils.OnFallbackLanguageChanged += OnLanguageChanged;
        SubscribeToLocalizationManager(LocalizationManager.Instance);
        SubscribeToCurrency();
        SetButtons();
    }

    private void OnDisable()
    {
        LocalizationManager.OnInstanceReady -= OnLocalizationManagerReady;
        LocalizationUtils.OnFallbackLanguageChanged -= OnLanguageChanged;
        UnsubscribeFromLocalizationManager();
        UnsubscribeFromCurrency();
    }

    public void ToggleOpen(bool open)
    {
        bool changed = _isOpen != open;
        _isOpen = open;
        EnsurePanel();
        if (_panel == null) return;
        _panel.SetActive(open);
        _chancesPanel?.Close();
        if (open)
            RefreshCurrentDropChances();
        if (changed)
            G.Sound?.Play(open ? GameAudioId.SFX_UI_OPEN : GameAudioId.SFX_UI_CLOSE);
    }

    private void EnsurePanel()
    {
        if (_panel != null) return;
        if (transform.childCount <= 0) return;
        _panel = transform.GetChild(0).gameObject;
    }


    public void SetInfo(ConveyorLevel level)
    {
        if (level == null)
            return;
        if (_currentLevelInfo == level) return;

        bool switchingExistingLevel = _currentLevelInfo != null;
        _currentLevelInfo = level;
        if (switchingExistingLevel)
            G.Sound?.Play(GameAudioId.SFX_UI_TAB);
        if (_levelName != null)
            _levelName.text = ConveyorLevelTab.GetLocalizedName(level);
        if (_icon != null)
            _icon.sprite = level.Icon;

        var newEgg = level.NewEgg;
        if (_newEggIcon != null)
        {
            _newEggIcon.sprite = newEgg != null ? newEgg.Icon : null;
            _newEggIcon.gameObject.SetActive(newEgg != null && newEgg.Icon != null);
            ApplyAlbumCardStyle(_newEggIcon.transform.parent, newEgg != null ? newEgg.RareType : RareType.Common);
        }

        if (_newEggPetsPanel != null)
        {
            for (int i = _newEggPetsPanel.childCount - 1; i >= 0; i--)
                Destroy(_newEggPetsPanel.GetChild(i).gameObject);

            if (newEgg != null && _newEggPetIcon != null)
            {
                foreach (Brainrot pet in ConveyorDropChanceCalculator.GetBrainrotsForDisplay(newEgg))
                {
                    if (pet == null)
                        continue;

                    GameObject icon = Instantiate(_newEggPetIcon, _newEggPetsPanel);
                    var image = ResolveCardIcon(icon.transform);
                    if (image != null)
                    {
                        image.sprite = pet.Icon;
                        image.preserveAspect = true;
                        image.raycastTarget = false;
                    }

                    ApplyAlbumCardStyle(icon.transform, pet.RareType);
                }

                if (_newEggPetsPanel is RectTransform petsRect)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(petsRect);
            }
        }

        UpdateIncomeBonusDisplay();
        UpdateDropChances(level);

        SetButtons();
    }

    private void SetButtons()
    {
        if (_currentLevelInfo == null)
            return;

        SubscribeToCurrency();
        if (_activeText != null)
            _activeText.SetActive(_currentLevelInfo.IsActive);
        if (_notAvailableText != null)
            _notAvailableText.SetActive(!_currentLevelInfo.IsPurchased && !_currentLevelInfo.IsAvailable);
        if (_buttonActivate != null)
            _buttonActivate.gameObject.SetActive(_currentLevelInfo.IsPurchased && !_currentLevelInfo.IsActive);
        bool showPurchaseButtons = !_currentLevelInfo.IsPurchased && _currentLevelInfo.IsAvailable;
        if (_buttonBuyCoins != null)
            _buttonBuyCoins.gameObject.SetActive(showPurchaseButtons);
        if (_buttonBuyGems != null)
            _buttonBuyGems.gameObject.SetActive(showPurchaseButtons);
        

        if (!_currentLevelInfo.IsPurchased)
        {
            if (_priceCoins != null)
                _priceCoins.text = CurrencyText.Coins(FormatPrice(_currentLevelInfo.PriceCoin));
            if (_priceGems != null)
                _priceGems.text = FormatPrice(_currentLevelInfo.PriceGems);
        }

        bool currencyReady = G.Currency != null;
        BlockyUITheme.StylePurchaseButton(
            _buttonBuyCoins,
            CurrencyType.Coins,
            showPurchaseButtons && currencyReady && G.Currency.Coins >= _currentLevelInfo.PriceCoin,
            _buyButtonGradientSprite);
        BlockyUITheme.StylePurchaseButton(
            _buttonBuyGems,
            CurrencyType.Gems,
            showPurchaseButtons && currencyReady && G.Currency.Gems >= _currentLevelInfo.PriceGems,
            _buyButtonGradientSprite);
    }

    private void SubscribeToCurrency()
    {
        if (_subscribedCurrencyManager != null || G.Currency == null)
            return;

        _subscribedCurrencyManager = G.Currency;
        _subscribedCurrencyManager.CurrencyChanged?.AddListener(OnCurrencyChanged);
    }

    private void UnsubscribeFromCurrency()
    {
        if (_subscribedCurrencyManager == null)
            return;

        _subscribedCurrencyManager.CurrencyChanged?.RemoveListener(OnCurrencyChanged);
        _subscribedCurrencyManager = null;
    }

    private void OnCurrencyChanged(CurrencyType _, double __)
    {
        SetButtons();
    }

    private static string FormatPrice(double price)
    {
        return G.Currency != null
            ? G.Currency.ToString(price)
            : Math.Round(price).ToString("0");
    }


    public void _OnBuyCoinsClick()
    {
        if (_currentLevelInfo == null)
            return;

        _currentLevelInfo.TryBuy(forGems:false);

        if (_currentLevelInfo.IsPurchased)
        {
            G.Sound?.PlayAt(GameAudioId.SFX_CONVEYOR_UPGRADE, _conveyor != null ? _conveyor.transform.position : transform.position);
            SetButtons();
        }
    }

    public void _OnBuyGemsClick()
    {
        if (_currentLevelInfo == null)
            return;

        _currentLevelInfo.TryBuy(forGems: true);

        if (_currentLevelInfo.IsPurchased)
        {
            G.Sound?.PlayAt(GameAudioId.SFX_CONVEYOR_UPGRADE, _conveyor != null ? _conveyor.transform.position : transform.position);
            SetButtons();
        }
    }

    public void _OnBuyActivateClick()
    {
        if (_currentLevelInfo == null)
            return;

        bool wasActive = _currentLevelInfo.IsActive;
        LevelActivated?.Invoke(_currentLevelInfo);
        if (_currentLevelInfo.IsPurchased && !wasActive)
        {
            GameAnalytics.Track(AnalyticsEventNames.ConveyorLevelActivated, GameAnalytics.Params(
                "conveyor_level_id", _currentLevelInfo.Name,
                "rarity", _currentLevelInfo.RareType.ToString().ToLowerInvariant(),
                "income_multiplier", _currentLevelInfo.IncomeMultiplier,
                "source", "conveyor_level_ui",
                "result", "success"));
        }
        G.Sound?.PlayAt(GameAudioId.SFX_CONVEYOR_ACTIVATE, _conveyor != null ? _conveyor.transform.position : transform.position);
        SetButtons();
    }

    public void UpdateActiveLvl(int idx)
    {
        if (_tabs == null || _tabs.Count == 0)
            return;

        if (_currentActiveIdx >= 0 && _currentActiveIdx < _tabs.Count && _tabs[_currentActiveIdx] != null)
            _tabs[_currentActiveIdx].SetLvlActive(false);

        _currentActiveIdx = idx;
        if (_currentActiveIdx < 0)
            _currentActiveIdx = 0;
        if (_currentActiveIdx >= _tabs.Count)
            _currentActiveIdx = _tabs.Count - 1;

        if (_tabs[_currentActiveIdx] != null)
            _tabs[_currentActiveIdx].SetLvlActive(true);

        UpdateIncomeBonusDisplay();
    }

    private void UpdateIncomeBonusDisplay()
    {
        if (_incomeMultiplier == null || _currentLevelInfo == null)
            return;

        if (_conveyor == null)
            _conveyor = GetComponentInParent<Conveyor>(true);

        int selectedBonus = Mathf.Max(0, Mathf.RoundToInt((_currentLevelInfo.IncomeMultiplier - 1f) * 100f));
        float activeMultiplier = _conveyor != null
            ? _conveyor.UnlockedIncomeMultiplier
            : _currentLevelInfo.IncomeMultiplier;
        int activeBonus = Mathf.Max(0, Mathf.RoundToInt((activeMultiplier - 1f) * 100f));
        string label = LocalizationUtils.T("UI/Conveyor/IncomeIncrease", "Income increase:");
        string activeLabel = LocalizationUtils.Format(
            "UI/Conveyor/ActivatedBonus",
            "active +{0}%",
            activeBonus);

        _incomeMultiplier.text = $"{label} +{selectedBonus}% ({activeLabel})";
    }

    private void StyleIncomeBonusDisplay()
    {
        if (_incomeMultiplier == null)
            return;

        Transform label = transform.Find("Panel/Income");
        if (label == null && _incomeMultiplier.transform.parent != null)
            label = _incomeMultiplier.transform.parent.Find("Income");
        if (label != null && label.gameObject != _incomeMultiplier.gameObject)
            label.gameObject.SetActive(false);

        if (_incomeMultiplier.transform is RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.05f, rect.anchorMin.y);
            rect.anchorMax = new Vector2(0.963f, rect.anchorMax.y);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        _incomeMultiplier.alignment = TextAlignmentOptions.Center;
        _incomeMultiplier.fontStyle |= FontStyles.Bold;
        _incomeMultiplier.color = BlockyUITheme.YellowAccent;
        _incomeMultiplier.enableAutoSizing = true;
        _incomeMultiplier.fontSizeMin = 18;
        _incomeMultiplier.fontSizeMax = 46;
        _incomeMultiplier.outlineColor = BlockyUITheme.BlackStroke;
        _incomeMultiplier.outlineWidth = Mathf.Max(_incomeMultiplier.outlineWidth, 0.16f);
    }

    private void ConfigureResponsiveLayout()
    {
        var container = transform.Find("container") as RectTransform;
        if (container != null)
        {
            container.pivot = new Vector2(0.5f, 0.5f);
            container.anchoredPosition = Vector2.zero;
        }

        var infoPanel = transform.Find("container/Panel/Panel") as RectTransform;
        if (infoPanel == null)
            return;

        // The old layout placed the animal block 149 reference pixels below the
        // egg block. That looked correct only at the authoring resolution.
        // Use normalized vertical bands so both sections stay aligned when the
        // window is resized or shown on a different aspect ratio.
        var eggSection = infoPanel.Find("NewEgg") as RectTransform;
        if (eggSection != null)
        {
            eggSection.anchorMin = new Vector2(0.36f, 0.64f);
            eggSection.anchorMax = new Vector2(0.98f, 0.90f);
            eggSection.anchoredPosition = Vector2.zero;
            eggSection.sizeDelta = Vector2.zero;

            ConfigureSectionLayout(
                eggSection,
                "Image",
                new Vector2(0.02f, 0.04f),
                new Vector2(0.27f, 0.70f));
        }

        var petsSection = infoPanel.Find("Brainrots") as RectTransform;
        if (petsSection != null)
        {
            petsSection.anchorMin = new Vector2(0.36f, 0.38f);
            petsSection.anchorMax = new Vector2(0.98f, 0.64f);
            petsSection.anchoredPosition = Vector2.zero;
            petsSection.sizeDelta = Vector2.zero;

            ConfigureSectionLayout(
                petsSection,
                "layout",
                new Vector2(0f, 0.02f),
                new Vector2(1f, 0.70f));
        }

        CenterStretchChild(infoPanel.Find("Info") as RectTransform);
        CenterStretchChild(infoPanel.Find("Name") as RectTransform);
    }

    private static void ConfigureSectionLayout(
        RectTransform section,
        string contentName,
        Vector2 contentAnchorMin,
        Vector2 contentAnchorMax)
    {
        if (section == null)
            return;

        var header = section.Find("HeaderBackground") as RectTransform;
        if (header != null)
        {
            header.anchorMin = new Vector2(0f, 0.78f);
            header.anchorMax = Vector2.one;
            header.anchoredPosition = Vector2.zero;
            header.sizeDelta = Vector2.zero;
        }

        var title = section.Find("Text (Legacy)") as RectTransform;
        if (title != null)
        {
            title.anchorMin = new Vector2(0f, 0.80f);
            title.anchorMax = Vector2.one;
            title.anchoredPosition = Vector2.zero;
            title.sizeDelta = Vector2.zero;
        }

        var content = section.Find(contentName) as RectTransform;
        if (content != null)
        {
            content.anchorMin = contentAnchorMin;
            content.anchorMax = contentAnchorMax;
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            content.SetAsFirstSibling();
        }

        // Header and text must be rendered after the cards even if a future
        // layout adjustment lets their rectangles touch again.
        header?.SetAsLastSibling();
        title?.SetAsLastSibling();
    }

    private void ConfigureHeaderVisuals()
    {
        var window = transform.Find("container/Panel") as RectTransform;
        var header = window != null ? window.Find("Top") as RectTransform : null;
        if (window == null || header == null)
            return;

        header.anchorMin = new Vector2(0f, 0.86f);
        header.anchorMax = Vector2.one;
        header.anchoredPosition = Vector2.zero;
        header.sizeDelta = Vector2.zero;

        var headerImage = header.GetComponent<Image>();
        if (headerImage != null)
        {
            headerImage.type = headerImage.sprite != null ? Image.Type.Tiled : Image.Type.Simple;
            headerImage.pixelsPerUnitMultiplier = 1f;
        }

        var title = header.Find("Text (Legacy)")?.GetComponent<TMP_Text>();
        if (title != null)
        {
            title.color = Color.white;
            title.fontStyle |= FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
            title.outlineColor = BlockyUITheme.BlackStroke;
            title.outlineWidth = Mathf.Max(title.outlineWidth, 0.18f);
            title.enableAutoSizing = true;
            title.fontSizeMin = 28f;
            title.fontSizeMax = 58f;

            if (title.transform is RectTransform titleRect)
            {
                titleRect.anchorMin = new Vector2(0.14f, 0f);
                titleRect.anchorMax = new Vector2(0.86f, 1f);
                titleRect.anchoredPosition = Vector2.zero;
                titleRect.sizeDelta = Vector2.zero;
            }
        }

        EnsureGradient(header, "HeaderGradient", _buyButtonGradientSprite, 0.22f);
        if (title != null)
            title.transform.SetAsLastSibling();

        EnsureCloseButton(window, headerImage, title);
    }

    private void EnsureCloseButton(RectTransform window, Image headerImage, TMP_Text titleTemplate)
    {
        if (window == null)
            return;

        Transform existing = window.Find("CloseButton");
        GameObject closeObject;
        if (existing == null)
        {
            closeObject = new GameObject(
                "CloseButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(AspectRatioFitter),
                typeof(Outline),
                typeof(Shadow));
            closeObject.transform.SetParent(window, false);
        }
        else
        {
            closeObject = existing.gameObject;
        }

        var rect = closeObject.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.98f, 0.875f);
            rect.anchorMax = new Vector2(0.98f, 0.985f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-8f, 0f);
            rect.sizeDelta = Vector2.zero;
        }

        var aspect = closeObject.GetComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
        aspect.aspectRatio = 1f;

        var image = closeObject.GetComponent<Image>();
        image.sprite = headerImage != null ? headerImage.sprite : null;
        image.type = image.sprite != null ? Image.Type.Tiled : Image.Type.Simple;
        image.pixelsPerUnitMultiplier = 1f;
        image.color = BlockyUITheme.RedHeader;
        image.raycastTarget = true;

        var outline = closeObject.GetComponent<Outline>();
        outline.effectColor = BlockyUITheme.BlackStroke;
        outline.effectDistance = new Vector2(3f, -3f);

        Shadow shadow = null;
        var shadows = closeObject.GetComponents<Shadow>();
        for (int i = 0; i < shadows.Length; i++)
        {
            if (shadows[i] != null && !(shadows[i] is Outline))
            {
                shadow = shadows[i];
                break;
            }
        }

        if (shadow == null)
            shadow = closeObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.38f);
        shadow.effectDistance = new Vector2(0f, -3f);
        shadow.useGraphicAlpha = true;

        _closeButton = closeObject.GetComponent<Button>();
        _closeButton.targetGraphic = image;
        _closeButton.onClick.RemoveListener(CloseFromButton);
        _closeButton.onClick.AddListener(CloseFromButton);

        var colors = _closeButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.80f, 0.80f, 0.80f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.48f, 0.48f, 0.48f, 0.78f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        _closeButton.colors = colors;

        EnsureGradient(closeObject.transform, "Gradient", _buyButtonGradientSprite, 0.20f);
        EnsureCloseGlyph(closeObject.transform, titleTemplate);
        closeObject.transform.SetAsLastSibling();
    }

    private static void EnsureGradient(Transform parent, string objectName, Sprite sprite, float alpha)
    {
        if (parent == null || sprite == null)
            return;

        Transform existing = parent.Find(objectName);
        GameObject gradientObject;
        if (existing == null)
        {
            gradientObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gradientObject.transform.SetParent(parent, false);
        }
        else
        {
            gradientObject = existing.gameObject;
        }

        var rect = gradientObject.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        var image = gradientObject.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        image.raycastTarget = false;
        gradientObject.transform.SetAsFirstSibling();
    }

    private static void EnsureCloseGlyph(Transform parent, TMP_Text titleTemplate)
    {
        if (parent == null)
            return;

        Transform existing = parent.Find("Text");
        GameObject textObject;
        if (existing == null)
        {
            textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
        }
        else
        {
            textObject = existing.gameObject;
        }

        var rect = textObject.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(-10f, -8f);
        }

        var text = textObject.GetComponent<TMP_Text>();
        if (titleTemplate != null)
        {
            text.font = titleTemplate.font;
            text.fontSharedMaterial = titleTemplate.fontSharedMaterial;
        }
        else
        {
            TmpUiTextFactory.ApplyDefaults(text);
        }

        text.text = "X";
        text.color = Color.white;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 24f;
        text.fontSizeMax = 54f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.outlineColor = BlockyUITheme.BlackStroke;
        text.outlineWidth = Mathf.Max(text.outlineWidth, 0.18f);
        text.raycastTarget = false;
        textObject.transform.SetAsLastSibling();
    }

    private void CloseFromButton()
    {
        ToggleOpen(false);
    }

    private void ConfigureInfoCardVisuals()
    {
        var infoPanel = transform.Find("container/Panel/Panel");
        if (infoPanel == null)
            return;

        var panelImage = infoPanel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.color = BlockyUITheme.BrownBody;
            panelImage.raycastTarget = true;
        }

        ApplyAlbumCardStyle(_newEggIcon != null ? _newEggIcon.transform.parent : null, RareType.Common);
        ConfigureTextBacking(infoPanel.Find("NewEgg/HeaderBackground"));
        ConfigureTextBacking(infoPanel.Find("Brainrots/HeaderBackground"));
        ConfigureTextBacking(infoPanel.Find("IncomeBackground"));
    }

    private static void CenterStretchChild(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);
    }

    private static Image ResolveCardIcon(Transform root)
    {
        if (root == null)
            return null;

        var icon = root.Find("Container/Icon") ?? root.Find("Icon");
        if (icon != null)
            return icon.GetComponent<Image>();

        var images = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && images[i].gameObject.name.Equals("Icon", StringComparison.OrdinalIgnoreCase))
                return images[i];
        }

        return images.Length > 0 ? images[images.Length - 1] : null;
    }

    private static void ApplyAlbumCardStyle(Transform root, RareType rareType)
    {
        if (root == null)
            return;

        var container = root.Find("Container") ?? root;
        var background = container.GetComponent<Image>();
        if (background == null)
            return;

        // Keep the same serialized soft-stud sprite and gradient as the Album
        // card prefab. Only the rarity tint is dynamic; ApplyPanel would swap
        // the sprite for the older generated BlockyStudPanel.
        background.color = BlockyUITheme.GetRareColor(rareType);
        background.type = Image.Type.Tiled;
        background.pixelsPerUnitMultiplier = 1f;
        background.raycastTarget = false;

        var gradient = container.Find("Gradient")?.GetComponent<Image>();
        if (gradient != null)
            gradient.raycastTarget = false;
    }

    private static void ConfigureTextBacking(Transform backingTransform)
    {
        if (backingTransform == null)
            return;

        var image = backingTransform.GetComponent<Image>();
        if (image == null)
            return;

        image.color = new Color(
            BlockyUITheme.DarkBrownPanel.r,
            BlockyUITheme.DarkBrownPanel.g,
            BlockyUITheme.DarkBrownPanel.b,
            0.88f);
        image.raycastTarget = false;
    }

    public IReadOnlyList<ConveyorDropChanceCalculator.ChanceEntry> GetCurrentDropChances()
    {
        return _cachedDropChances;
    }

    public IReadOnlyList<ConveyorDropChanceCalculator.ChanceEntry> GetCurrentDropChancesWithLuck()
    {
        return _cachedDropChancesWithLuck;
    }

    public IReadOnlyList<ConveyorDropChanceCalculator.EggBreakdownEntry> GetCurrentEggBreakdown()
    {
        return _cachedEggBreakdown;
    }

    public void RefreshCurrentDropChances()
    {
        if (_currentLevelInfo == null)
            return;

        UpdateDropChances(_currentLevelInfo);
    }

    public void ShowLevel(ConveyorLevel level)
    {
        SetInfo(level);
    }

    private void OnLocalizationManagerReady(LocalizationManager manager)
    {
        SubscribeToLocalizationManager(manager);
    }

    private void SubscribeToLocalizationManager(LocalizationManager manager)
    {
        if (manager == null || manager == _subscribedLocalizationManager)
            return;

        UnsubscribeFromLocalizationManager();
        _subscribedLocalizationManager = manager;
        _subscribedLocalizationManager.OnLanguageChanged += OnLanguageChanged;
    }

    private void UnsubscribeFromLocalizationManager()
    {
        if (_subscribedLocalizationManager == null)
            return;

        _subscribedLocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        _subscribedLocalizationManager = null;
    }

    private void OnLanguageChanged(string _)
    {
        if (_currentLevelInfo == null)
            return;

        if (_levelName != null)
            _levelName.text = ConveyorLevelTab.GetLocalizedName(_currentLevelInfo);
        UpdateIncomeBonusDisplay();
        RefreshCurrentDropChances();
        if (_chancesPanel != null && _chancesPanel.IsOpen)
            _chancesPanel.Refresh();
    }

    private void UpdateDropChances(ConveyorLevel level)
    {
        _cachedDropChances.Clear();
        _cachedDropChancesWithLuck.Clear();
        _cachedEggBreakdown.Clear();
        if (level == null)
            return;

        var baseList = ConveyorDropChanceCalculator.BuildBrainrotChances(level, applyLuckBonus: false);
        if (baseList != null && baseList.Count > 0)
            _cachedDropChances.AddRange(baseList);

        var luckList = ConveyorDropChanceCalculator.BuildBrainrotChances(level, applyLuckBonus: true);
        if (luckList != null && luckList.Count > 0)
            _cachedDropChancesWithLuck.AddRange(luckList);

        var eggBreakdown = ConveyorDropChanceCalculator.BuildEggBreakdown(level);
        if (eggBreakdown != null && eggBreakdown.Count > 0)
            _cachedEggBreakdown.AddRange(eggBreakdown);

        if (_dropChancesText == null)
            return;

        if (_cachedDropChances.Count == 0 && _cachedDropChancesWithLuck.Count == 0 && _cachedEggBreakdown.Count == 0)
        {
            _dropChancesText.text = string.Empty;
            return;
        }

        var sb = new StringBuilder();
        if (_showLuckComparison)
        {
            AppendSection(sb, L(_chancesBaseLocalizationKey, _chancesBaseText), _cachedDropChances);
            if (_cachedDropChancesWithLuck.Count > 0)
            {
                if (sb.Length > 0)
                    sb.Append('\n').Append('\n');
                AppendSection(sb, L(_chancesWithLuckLocalizationKey, _chancesWithLuckText), _cachedDropChancesWithLuck);
            }
        }
        else
        {
            AppendSection(sb, string.Empty, _cachedDropChancesWithLuck.Count > 0 ? _cachedDropChancesWithLuck : _cachedDropChances);
        }

        if (_showEggBreakdown && _cachedEggBreakdown.Count > 0)
        {
            if (sb.Length > 0)
                sb.Append('\n').Append('\n');

            AppendEggBreakdownSection(sb, _cachedEggBreakdown, _showLuckComparison);
        }

        _dropChancesText.text = sb.ToString();
    }

    private static void AppendSection(StringBuilder sb, string title, IReadOnlyList<ConveyorDropChanceCalculator.ChanceEntry> rows)
    {
        if (rows == null || rows.Count == 0)
            return;

        if (!string.IsNullOrWhiteSpace(title))
        {
            sb.Append(title);
            sb.Append('\n');
        }

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (i > 0)
                sb.Append('\n');
            sb.Append(row.name);
            sb.Append(": ");
            sb.Append((row.chance * 100d).ToString("0.00"));
            sb.Append('%');
        }
    }

    private void AppendEggBreakdownSection(
        StringBuilder sb,
        IReadOnlyList<ConveyorDropChanceCalculator.EggBreakdownEntry> eggRows,
        bool showLuckComparison)
    {
        if (eggRows == null || eggRows.Count == 0)
            return;

        sb.Append(L(_eggBreakdownLocalizationKey, _eggBreakdownText));

        for (int i = 0; i < eggRows.Count; i++)
        {
            var eggRow = eggRows[i];
            if (eggRow == null || eggRow.egg == null)
                continue;

            sb.Append('\n');
            sb.Append("- ");
            sb.Append(eggRow.name);
            sb.Append(": ");
            sb.Append((eggRow.chance * 100d).ToString("0.00"));
            sb.Append('%');

            var perEggBase = ConveyorDropChanceCalculator.BuildBrainrotChances(eggRow.egg, applyLuckBonus: false);
            if (perEggBase == null || perEggBase.Count == 0)
                continue;

            var perEggLuck = showLuckComparison
                ? ConveyorDropChanceCalculator.BuildBrainrotChances(eggRow.egg, applyLuckBonus: true)
                : null;
            var luckById = BuildChanceLookup(perEggLuck);

            for (int j = 0; j < perEggBase.Count; j++)
            {
                var row = perEggBase[j];
                sb.Append('\n');
                sb.Append("   - ");
                sb.Append(row.name);
                sb.Append(": ");
                sb.Append((row.chance * 100d).ToString("0.00"));
                sb.Append('%');

                if (!showLuckComparison)
                    continue;

                if (luckById.TryGetValue(row.id ?? string.Empty, out var luckChance))
                {
                    sb.Append(" -> ");
                    sb.Append((luckChance * 100d).ToString("0.00"));
                    sb.Append('%');
                }
            }
        }
    }

    private static Dictionary<string, double> BuildChanceLookup(IReadOnlyList<ConveyorDropChanceCalculator.ChanceEntry> rows)
    {
        var map = new Dictionary<string, double>(StringComparer.Ordinal);
        if (rows == null)
            return map;

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row == null || string.IsNullOrWhiteSpace(row.id))
                continue;
            map[row.id] = row.chance;
        }

        return map;
    }

    private static string L(string key, string fallback)
    {
        if (LocalizationManager.Instance != null && LocalizationManager.Instance.LocalizationData != null)
        {
            var translated = LocalizationManager.Instance.LocalizationData.GetTranslation(key);
            if (!string.IsNullOrWhiteSpace(translated) && !string.Equals(translated, key, StringComparison.Ordinal))
                return translated;
        }

        return fallback;
    }

}

public static class ConveyorDropChanceCalculator
{
    [Serializable]
    public class ChanceEntry
    {
        public string id;
        public string name;
        public double chance;
    }

    [Serializable]
    public class EggBreakdownEntry
    {
        public string id;
        public string name;
        public Egg egg;
        public double chance;
    }

    private readonly struct BrainrotDropView
    {
        public readonly Brainrot brainrot;
        public readonly double weight;

        public BrainrotDropView(Brainrot brainrot, double weight)
        {
            this.brainrot = brainrot;
            this.weight = weight;
        }
    }

    public static List<ChanceEntry> BuildEggChances(ConveyorLevel level)
    {
        var result = new List<ChanceEntry>();
        var eggs = BuildValidLevelEggs(level);
        if (eggs.Count == 0)
            return result;

        var eggChance = 1d / eggs.Count;

        var aggregate = new Dictionary<string, ChanceEntry>(StringComparer.Ordinal);
        for (int i = 0; i < eggs.Count; i++)
        {
            var egg = eggs[i];
            var id = GetId(egg.name, egg.Name);
            var name = GetLocalizedEggName(egg, id);
            AddOrAccumulate(aggregate, id, name, eggChance);
        }

        result.AddRange(aggregate.Values);
        result.Sort((a, b) => b.chance.CompareTo(a.chance));
        return result;
    }

    public static List<ChanceEntry> BuildBrainrotChances(ConveyorLevel level)
    {
        return BuildBrainrotChances(level, applyLuckBonus: false);
    }

    public static List<EggBreakdownEntry> BuildEggBreakdown(ConveyorLevel level)
    {
        var result = new List<EggBreakdownEntry>();
        var eggs = BuildValidLevelEggs(level);
        if (eggs.Count == 0)
            return result;

        var eggChance = 1d / eggs.Count;

        var aggregate = new Dictionary<string, EggBreakdownEntry>(StringComparer.Ordinal);
        for (int i = 0; i < eggs.Count; i++)
        {
            var egg = eggs[i];
            var id = GetId(egg.name, egg.Name);
            var name = GetLocalizedEggName(egg, id);
            if (!aggregate.TryGetValue(id, out var entry))
            {
                entry = new EggBreakdownEntry
                {
                    id = id,
                    name = string.IsNullOrWhiteSpace(name) ? id : name,
                    egg = egg,
                    chance = 0d
                };
                aggregate.Add(id, entry);
            }
            else if (entry.egg == null)
            {
                entry.egg = egg;
            }

            entry.chance += eggChance;
        }

        result.AddRange(aggregate.Values);
        result.Sort((a, b) => b.chance.CompareTo(a.chance));
        return result;
    }

    public static List<ChanceEntry> BuildBrainrotChances(ConveyorLevel level, bool applyLuckBonus)
    {
        var result = new List<ChanceEntry>();
        var eggs = BuildValidLevelEggs(level);
        if (eggs.Count == 0)
            return result;

        var eggChance = 1d / eggs.Count;
        var aggregate = new Dictionary<string, ChanceEntry>(StringComparer.Ordinal);
        for (int i = 0; i < eggs.Count; i++)
        {
            var egg = eggs[i];
            var drops = BuildDropList(egg);
            if (drops.Count == 0)
                continue;

            var weightSum = 0d;
            for (int j = 0; j < drops.Count; j++)
                weightSum += GetDropWeight(drops[j], egg.Data.Luck, applyLuckBonus);
            if (weightSum <= 0d)
                continue;

            for (int j = 0; j < drops.Count; j++)
            {
                var brainrot = drops[j].brainrot;
                var itemWeight = GetDropWeight(drops[j], egg.Data.Luck, applyLuckBonus);
                if (brainrot == null || itemWeight <= 0d)
                    continue;

                var brainrotChance = eggChance * (itemWeight / weightSum);
                var id = GetId(brainrot.name, brainrot.Name);
                var fallback = string.IsNullOrWhiteSpace(brainrot.Name) ? brainrot.name : brainrot.Name;
                var name = ItemDisplayNameResolver.ResolveItemName(brainrot.Name, fallback);
                AddOrAccumulate(aggregate, id, name, brainrotChance);
            }
        }

        result.AddRange(aggregate.Values);
        result.Sort((a, b) => b.chance.CompareTo(a.chance));
        return result;
    }

    private static List<Egg> BuildValidLevelEggs(ConveyorLevel level)
    {
        var result = new List<Egg>();
        if (level == null || level.Eggs == null)
            return result;

        for (int i = 0; i < level.Eggs.Count; i++)
        {
            var egg = level.Eggs[i].egg;
            if (egg != null)
                result.Add(egg);
        }

        return result;
    }

    public static List<ChanceEntry> BuildBrainrotChances(Egg egg)
    {
        return BuildBrainrotChances(egg, applyLuckBonus: false);
    }

    public static List<Brainrot> GetBrainrotsForDisplay(Egg egg)
    {
        var result = new List<Brainrot>();
        var drops = BuildDropList(egg);
        for (int i = 0; i < drops.Count; i++)
        {
            if (drops[i].brainrot != null && !result.Contains(drops[i].brainrot))
                result.Add(drops[i].brainrot);
        }

        return result;
    }

    public static List<ChanceEntry> BuildBrainrotChances(Egg egg, bool applyLuckBonus)
    {
        var result = new List<ChanceEntry>();
        var drops = BuildDropList(egg);
        if (drops.Count == 0)
            return result;

        var aggregate = new Dictionary<string, ChanceEntry>(StringComparer.Ordinal);
        var totalWeight = 0d;
        for (int i = 0; i < drops.Count; i++)
            totalWeight += GetDropWeight(drops[i], egg.Data.Luck, applyLuckBonus);
        if (totalWeight <= 0d)
            return result;

        for (int i = 0; i < drops.Count; i++)
        {
            var brainrot = drops[i].brainrot;
            var weight = GetDropWeight(drops[i], egg.Data.Luck, applyLuckBonus);
            if (brainrot == null || weight <= 0d)
                continue;

            var id = GetId(brainrot.name, brainrot.Name);
            var fallback = string.IsNullOrWhiteSpace(brainrot.Name) ? brainrot.name : brainrot.Name;
            var name = ItemDisplayNameResolver.ResolveItemName(brainrot.Name, fallback);
            AddOrAccumulate(aggregate, id, name, weight / totalWeight);
        }

        result.AddRange(aggregate.Values);
        result.Sort((a, b) => b.chance.CompareTo(a.chance));
        return result;
    }

    public static Brainrot PickRandomBrainrot(Egg egg, bool applyLuckBonus = true)
    {
        var drops = BuildDropList(egg);
        if (drops.Count == 0)
            return null;

        double totalWeight = 0d;
        for (int i = 0; i < drops.Count; i++)
            totalWeight += GetDropWeight(drops[i], egg.Data.Luck, applyLuckBonus);
        if (totalWeight <= 0d)
            return drops[UnityEngine.Random.Range(0, drops.Count)].brainrot;

        var roll = UnityEngine.Random.value * totalWeight;
        var cursor = 0d;
        for (int i = 0; i < drops.Count; i++)
        {
            var w = GetDropWeight(drops[i], egg.Data.Luck, applyLuckBonus);
            if (w <= 0d)
                continue;

            cursor += w;
            if (roll <= cursor)
                return drops[i].brainrot;
        }

        return drops[drops.Count - 1].brainrot;
    }

    public static Brainrot PickRandomBrainrot(IReadOnlyList<Brainrot> brainrots, int luckMultiplier, bool applyLuckBonus = true)
    {
        if (brainrots == null || brainrots.Count == 0)
            return null;

        double totalWeight = 0d;
        for (int i = 0; i < brainrots.Count; i++)
            totalWeight += GetLuckAdjustedWeight(brainrots[i], luckMultiplier, applyLuckBonus);
        if (totalWeight <= 0d)
            return brainrots[UnityEngine.Random.Range(0, brainrots.Count)];

        var roll = UnityEngine.Random.value * totalWeight;
        var cursor = 0d;
        for (int i = 0; i < brainrots.Count; i++)
        {
            var candidate = brainrots[i];
            var w = GetLuckAdjustedWeight(candidate, luckMultiplier, applyLuckBonus);
            if (w <= 0d)
                continue;

            cursor += w;
            if (roll <= cursor)
                return candidate;
        }

        return brainrots[brainrots.Count - 1];
    }

    private static void AddOrAccumulate(IDictionary<string, ChanceEntry> map, string id, string name, double deltaChance)
    {
        if (string.IsNullOrWhiteSpace(id) || deltaChance <= 0d)
            return;

        if (!map.TryGetValue(id, out var entry))
        {
            entry = new ChanceEntry
            {
                id = id,
                name = string.IsNullOrWhiteSpace(name) ? id : name,
                chance = 0d
            };
            map.Add(id, entry);
        }

        entry.chance += deltaChance;
    }

    private static string GetId(string prefabName, string displayName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName.Trim();
        if (!string.IsNullOrWhiteSpace(prefabName))
            return prefabName.Trim();
        return "unknown";
    }

    private static string GetLocalizedEggName(Egg egg, string id)
    {
        if (egg == null)
            return string.IsNullOrWhiteSpace(id) ? "unknown" : id;

        var fallback = !string.IsNullOrWhiteSpace(egg.Name) ? egg.Name.Trim() : egg.name;
        var key = string.IsNullOrWhiteSpace(id) ? fallback : id.Trim();
        if (string.IsNullOrWhiteSpace(key))
            return "unknown";

        return ItemDisplayNameResolver.ResolveItemName(key, fallback);
    }

    private static List<BrainrotDropView> BuildDropList(Egg egg)
    {
        var result = new List<BrainrotDropView>();
        if (egg == null)
            return result;

        var configuredDrops = egg.Data.BrainrotDrops;
        if (configuredDrops != null)
        {
            for (int i = 0; i < configuredDrops.Count; i++)
            {
                var drop = configuredDrops[i];
                if (!Egg.IsAnimalDrop(drop.Brainrot) || drop.Weight <= 0f)
                    continue;

                result.Add(new BrainrotDropView(drop.Brainrot, drop.Weight));
            }
        }

        if (result.Count > 0)
            return result;

        var legacyBrainrots = egg.Data.Brainrots;
        if (legacyBrainrots == null)
            return result;

        var useDefaultSlotWeights = legacyBrainrots.Count == 4;
        for (int i = 0; i < legacyBrainrots.Count; i++)
        {
            if (Egg.IsAnimalDrop(legacyBrainrots[i]))
                result.Add(new BrainrotDropView(legacyBrainrots[i], useDefaultSlotWeights ? GetDefaultSlotWeight(i) : 1d));
        }

        return result;
    }

    private static double GetDefaultSlotWeight(int index)
    {
        return index switch
        {
            0 => 55d,
            1 => 25d,
            2 => 15d,
            3 => 5d,
            _ => 1d
        };
    }

    private static double GetDropWeight(BrainrotDropView drop, int luckMultiplier, bool applyLuckBonus)
    {
        if (drop.brainrot == null || drop.weight <= 0d)
            return 0d;

        return drop.weight * GetLuckAdjustedWeight(drop.brainrot, luckMultiplier, applyLuckBonus);
    }

    private static double GetLuckAdjustedWeight(Brainrot brainrot, int luckMultiplier, bool applyLuckBonus)
    {
        if (brainrot == null)
            return 0d;
        if (!applyLuckBonus)
            return 1d;

        var sanitizedLuck = Mathf.Clamp(luckMultiplier, 1, 10);
        if (sanitizedLuck <= 1)
            return 1d;

        var tier = GetRarityTier(brainrot.RareType);
        const double tierStep = 0.25d;
        var extra = sanitizedLuck - 1;
        return Math.Max(0.0001d, 1d + extra * tier * tierStep);
    }

    private static int GetRarityTier(RareType rareType)
    {
        return rareType switch
        {
            RareType.Common => 1,
            RareType.Uncommon => 2,
            RareType.Rare => 3,
            RareType.Epic => 4,
            RareType.Legendary => 5,
            RareType.Mythic => 6,
            _ => 1
        };
    }

}
