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
        ConfigureInfoCardVisuals();
        StyleIncomeBonusDisplay();
    }

    private void OnEnable()
    {
        LocalizationManager.OnInstanceReady += OnLocalizationManagerReady;
        LocalizationUtils.OnFallbackLanguageChanged -= OnLanguageChanged;
        LocalizationUtils.OnFallbackLanguageChanged += OnLanguageChanged;
        SubscribeToLocalizationManager(LocalizationManager.Instance);
    }

    private void OnDisable()
    {
        LocalizationManager.OnInstanceReady -= OnLocalizationManagerReady;
        LocalizationUtils.OnFallbackLanguageChanged -= OnLanguageChanged;
        UnsubscribeFromLocalizationManager();
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

        if (_activeText != null)
            _activeText.SetActive(_currentLevelInfo.IsActive);
        if (_notAvailableText != null)
            _notAvailableText.SetActive(!_currentLevelInfo.IsPurchased && !_currentLevelInfo.IsAvailable);
        if (_buttonActivate != null)
            _buttonActivate.gameObject.SetActive(_currentLevelInfo.IsPurchased && !_currentLevelInfo.IsActive);
        if (_buttonBuyCoins != null)
            _buttonBuyCoins.gameObject.SetActive(!_currentLevelInfo.IsPurchased && _currentLevelInfo.IsAvailable);
        if (_buttonBuyGems != null)
            _buttonBuyGems.gameObject.SetActive(!_currentLevelInfo.IsPurchased && _currentLevelInfo.IsAvailable);
        

        if (!_currentLevelInfo.IsPurchased)
        {
            if (_priceCoins != null)
                _priceCoins.text = _currentLevelInfo.PriceCoin.ToString();
            if (_priceGems != null)
                _priceGems.text = _currentLevelInfo.PriceGems.ToString();
        }
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

        LevelActivated?.Invoke(_currentLevelInfo);
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
        }

        var petsSection = infoPanel.Find("Brainrots") as RectTransform;
        if (petsSection != null)
        {
            petsSection.anchorMin = new Vector2(0.36f, 0.38f);
            petsSection.anchorMax = new Vector2(0.98f, 0.64f);
            petsSection.anchoredPosition = Vector2.zero;
            petsSection.sizeDelta = Vector2.zero;
        }

        CenterStretchChild(infoPanel.Find("Info") as RectTransform);
        CenterStretchChild(infoPanel.Find("Name") as RectTransform);
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
