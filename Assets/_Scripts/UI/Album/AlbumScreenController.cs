using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AlbumScreenController : MonoBehaviour
{
    [Serializable]
    private struct RewardOverride
    {
        public string id;
        public int gems;
    }

    [Serializable]
    private struct AnimalDescriptionOverride
    {
        public string id;
        [TextArea(2, 6)] public string description;
    }

    private sealed class EntryData
    {
        public AlbumEntityType type;
        public string id;
        public string displayName;
        public Sprite icon;
        public RareType rareType;
        public Egg egg;
        public Brainrot animal;
    }

    private struct AnimalSource
    {
        public string eggId;
        public Egg egg;
        public double chance;
    }

    private struct HatchPreviewEntry
    {
        public Sprite icon;
        public bool unlocked;
        public double chance;
    }

    [Header("Root")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private bool hideOnStart = true;
    [SerializeField] private ItemPrefabStorage itemStorage;
    [SerializeField] private AlbumProgressService progressService;

    [Header("Tab Buttons")]
    [SerializeField] private Button eggsTabButton;
    [SerializeField] private Button animalsTabButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button embeddedOpenButton;
    [SerializeField] private TMP_Text eggsTabText;
    [SerializeField] private TMP_Text animalsTabText;
    [SerializeField] private Image eggsTabBackground;
    [SerializeField] private Image animalsTabBackground;
    [SerializeField] private Image eggsTabSelectedFrame;
    [SerializeField] private Image animalsTabSelectedFrame;
    [SerializeField] private GameObject eggsTabMention;
    [SerializeField] private GameObject animalsTabMention;
    [SerializeField] private GameObject albumIconMention;

    [Header("Cards")]
    [SerializeField] private TMP_Text cardsSectionTitle;
    [SerializeField] private Transform cardsRoot;
    [SerializeField] private AlbumEntryView cardPrefab;
    [SerializeField] private AdaptiveGridSpawner cardsAdaptiveGrid;
    [SerializeField] private DynamicGridSpawner cardsDynamicGrid;

    [Header("Element Tabs")]
    [SerializeField] private Transform rareTabsRoot;
    [SerializeField] private AdaptiveGridSpawner rareTabsAdaptiveGrid;
    [SerializeField] private AlbumRareTabView rareTabPrefab;

    [Header("Info Panel")]
    [SerializeField] private AlbumInfoPanelView infoPanelView;
    [SerializeField] private Image infoIcon;
    [SerializeField] private TMP_Text infoTitle;
    [SerializeField] private TMP_Text infoDescription;
    [SerializeField] private TMP_Text infoIncome;
    [SerializeField] private TMP_Text infoSources;
    [SerializeField] private GameObject eggHatchSection;
    [SerializeField] private Transform eggHatchIconsRoot;
    [SerializeField] private Image eggHatchIconTemplate;
    [SerializeField] private int eggHatchMaxIcons = 4;
    [SerializeField] private Color eggHatchUnlockedColor = Color.white;
    [SerializeField] private Color eggHatchLockedColor = new Color(0f, 0f, 0f, 0.92f);
    [SerializeField] private GameObject infoLockedOverlay;
    [SerializeField] private TMP_Text infoLockedText;
    [SerializeField] private Color infoUnlockedColor = Color.white;
    [SerializeField] private Color infoLockedColor = new Color(0f, 0f, 0f, 0.92f);

    [Header("Reward")]
    [SerializeField] private Button rewardButton;
    [SerializeField] private TMP_Text rewardButtonText;
    [SerializeField] private GameObject rewardMentionBadge;
    [SerializeField] private int defaultEggRewardGems = 5;
    [SerializeField] private int defaultAnimalRewardGems = 5;
    [SerializeField] private List<RewardOverride> eggRewardOverrides = new();
    [SerializeField] private List<RewardOverride> animalRewardOverrides = new();

    [Header("Localization")]
    [SerializeField] private string eggsTabKey = "UI/Album/TabEggs";
    [SerializeField] private string eggsTabFallback = "Eggs";
    [SerializeField] private string animalsTabKey = "UI/Album/TabAnimals";
    [SerializeField] private string animalsTabFallback = "Animals";
    [SerializeField] private string eggsSectionTitleKey = "UI/Album/SectionEggs";
    [SerializeField] private string eggsSectionTitleFallback = "Egg";
    [SerializeField] private string animalsSectionTitleKey = "UI/Album/SectionAnimals";
    [SerializeField] private string animalsSectionTitleFallback = "Pet";
    [SerializeField] private string unknownKey = "UI/Album/Unknown";
    [SerializeField] private string unknownFallback = "???";
    [SerializeField] private string eggInfoDescKey = "UI/Album/EggInfoDescription";
    [SerializeField] private string eggInfoDescFallback = "Can hatch from this egg:";
    [SerializeField] private string animalInfoDescKey = "UI/Album/AnimalInfoDescription";
    [SerializeField] private string animalInfoDescFallback = "Can hatch from eggs:";
    [SerializeField] private string incomeLabelKey = "UI/Album/Income";
    [SerializeField] private string incomeLabelFallback = "Income/sec";
    [SerializeField] private string eggPriceLabelKey = "UI/Album/EggPrice";
    [SerializeField] private string eggPriceLabelFallback = "";
    [SerializeField] private string eggFirstRareDateKey = "UI/Album/EggFirstElementDate";
    [SerializeField] private string eggFirstRareDateFallback = "{0}";
    [SerializeField] private string animalFirstDateKey = "UI/Album/AnimalFirstDate";
    [SerializeField] private string animalFirstDateFallback = "First obtained: {0}";
    [SerializeField] private string albumDateUnknownKey = "UI/Album/DateUnknown";
    [SerializeField] private string albumDateUnknownFallback = "Unknown";
    [SerializeField] private string animalDescriptionKeyPrefix = "UI/Album/AnimalDescription/";
    [SerializeField] private string animalDescriptionFallback = "-";
    [SerializeField] private string rewardInfoKey = "UI/Album/RewardInfo";
    [SerializeField] private string rewardInfoFallback = "Reward: +{0} Gems";
    [SerializeField] private string claimRewardKey = "UI/Album/ClaimReward";
    [SerializeField] private string claimRewardFallback = "Claim +{0} Gems";
    [SerializeField] private string rewardClaimedKey = "UI/Album/RewardClaimed";
    [SerializeField] private string rewardClaimedFallback = "Claimed";
    [SerializeField] private string rareLabelKeyPrefix = "UI/Album/Element/";
    [SerializeField] private string albumDateFormat = "yyyy.MM.dd";
    [SerializeField] private List<AnimalDescriptionOverride> animalDescriptionOverrides = new();

    [Header("Behavior")]
    [SerializeField] private bool applyLuckBonusInChances = true;
    [SerializeField] private bool sortByName = true;
    [SerializeField] private bool includeLockedInList = true;

    [Header("Runtime Visual Overrides")]
    [SerializeField] private bool applyRuntimeBlockyStyle = false;
    [SerializeField] private bool tintSelectedTopTab = false;
    [SerializeField] private bool boldSelectedTopTab = false;
    [SerializeField, Range(0f, 1f)] private float activeTopTabLighten = 0.22f;

    private readonly List<EntryData> _eggEntries = new();
    private readonly List<EntryData> _animalEntries = new();
    private readonly Dictionary<string, string> _animalAliasToCanonicalId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EntryData> _animalEntryById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<ConveyorDropChanceCalculator.ChanceEntry>> _eggChanceByEggId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<AnimalSource>> _animalSourcesByAnimalId = new(StringComparer.Ordinal);
    private readonly List<Image> _eggHatchIconPool = new();
    private readonly Dictionary<ElementType, AlbumRareTabView> _elementViews = new();
    private readonly List<AlbumEntryView> _spawnedCardViews = new();
    private readonly List<ElementType> _supportedElementTypes = new();

    private AlbumEntityType _currentTab = AlbumEntityType.Egg;
    private ElementType? _selectedElementFilter;
    private string _selectedEntryId;
    private bool _bindingsReady;
    private bool _catalogReady;
    private bool _suppressProgressChangedRefresh;
    private FontStyles _eggsTabDefaultStyle = FontStyles.Normal;
    private bool _hasEggsTabDefaultStyle;
    private FontStyles _animalsTabDefaultStyle = FontStyles.Normal;
    private bool _hasAnimalsTabDefaultStyle;
    private Color _eggsTabBaseColor = Color.white;
    private bool _hasEggsTabBaseColor;
    private Color _animalsTabBaseColor = Color.white;
    private bool _hasAnimalsTabBaseColor;
    private bool _externalOpenButtonRegistered;

    public bool IsOpen => panelRoot != null ? panelRoot.activeInHierarchy : gameObject.activeInHierarchy;

    private void Awake()
    {
        AutoSetupReferences();
        BuildCatalog();
        BuildElementTabs();
        BindButtons();
        RefreshStaticTexts();
        SetTab(AlbumEntityType.Egg, preserveSelection: false);
        ApplyBlockyStyle();

        if (hideOnStart)
            SetOpen(false);
    }

    private void OnDestroy()
    {
        UnbindButtons();
        if (progressService != null)
            progressService.Changed.RemoveListener(OnProgressChanged);
    }

    private void OnEnable()
    {
        if (G.Input != null)
        {
            G.Input.AOpenWindow -= CloseFromOtherWindow;
            G.Input.AOpenWindow += CloseFromOtherWindow;
        }

        if (progressService == null)
            progressService = G.Album;

        if (progressService != null)
        {
            progressService.Changed.RemoveListener(OnProgressChanged);
            progressService.Changed.AddListener(OnProgressChanged);
        }

        EnsureCatalogReady();
        Refresh();
    }

    private void OnDisable()
    {
        if (G.Input != null)
            G.Input.AOpenWindow -= CloseFromOtherWindow;

        if (progressService != null)
            progressService.Changed.RemoveListener(OnProgressChanged);
    }

    private void Update()
    {
        if (_catalogReady)
            return;

        if (!EnsureCatalogReady())
            return;

        Refresh();
    }

    public void Open()
    {
        SetOpen(true);
    }

    public void Close()
    {
        SetOpen(false);
    }

    public void Toggle()
    {
        SetOpen(!IsOpen);
    }

    public void RegisterExternalOpenButton(Button externalButton, GameObject mentionBadge)
    {
        _externalOpenButtonRegistered = externalButton != null;

        if (mentionBadge != null)
        {
            if (albumIconMention != null && albumIconMention != mentionBadge)
                albumIconMention.SetActive(false);

            albumIconMention = mentionBadge;
            albumIconMention.SetActive(false);
        }

        if (_externalOpenButtonRegistered)
            HideEmbeddedOpenButton();

        AutoSetupReferences();
        if (_supportedElementTypes.Count == 0)
            BuildElementTabs();
        EnsureCatalogReady();
        RefreshTabMentions();
    }

    public void SetOpen(bool open)
    {
        SetOpen(open, true);
    }

    private void SetOpen(bool open, bool updateCursor)
    {
        if (open && !gameObject.activeSelf)
            gameObject.SetActive(true);

        if (_externalOpenButtonRegistered)
            HideEmbeddedOpenButton();

        if (panelRoot != null)
            panelRoot.SetActive(open);
        else
            gameObject.SetActive(open);

        if (open)
        {
            Refresh();

            if (G.Input != null)
                G.Input.AOpenWindow?.Invoke(this);
        }

        if (updateCursor && G.Control != null)
            G.Control.CursorActive = open;
    }

    private void HideEmbeddedOpenButton()
    {
        var button = ResolveEmbeddedOpenButton();
        if (button != null)
            button.gameObject.SetActive(false);
    }

    private Button ResolveEmbeddedOpenButton()
    {
        if (embeddedOpenButton != null)
            return embeddedOpenButton;

        var buttonTransform = FindChildByName(transform, "ButtonOpen");
        if (buttonTransform != null)
            embeddedOpenButton = buttonTransform.GetComponent<Button>();

        return embeddedOpenButton;
    }

    private void CloseFromOtherWindow(MonoBehaviour ui)
    {
        if (ui != this && IsOpen)
            SetOpen(false, false);
    }

    public void SetTabEggs()
    {
        SetTab(AlbumEntityType.Egg, preserveSelection: false);
    }

    public void SetTabAnimals()
    {
        SetTab(AlbumEntityType.Animal, preserveSelection: false);
    }

    public void Refresh()
    {
        EnsureCatalogReady();
        RefreshLocalizedDisplayNames();
        RefreshTabMentions();
        RefreshTopTabVisuals();
        RefreshCardsSectionTitle();
        RebuildCards();
        RefreshElementTabs();
        RefreshInfoPanel();
        ApplyBlockyStyle();
    }

    private void SetTab(AlbumEntityType tab, bool preserveSelection)
    {
        _currentTab = tab;
        _selectedElementFilter = ResolveDefaultElementFilter(tab);
        if (!preserveSelection)
            _selectedEntryId = null;

        Refresh();
    }

    private void BuildCatalog()
    {
        _eggEntries.Clear();
        _animalEntries.Clear();
        _animalAliasToCanonicalId.Clear();
        _animalEntryById.Clear();
        _eggChanceByEggId.Clear();
        _animalSourcesByAnimalId.Clear();
        _catalogReady = false;

        var storage = itemStorage != null ? itemStorage : G.Storage;
        if (storage == null)
            return;

        _catalogReady = true;

        var eggs = storage.GetAllEggPrefabs();
        if (eggs != null)
        {
            var eggEntriesById = new Dictionary<string, EntryData>(StringComparer.Ordinal);
            for (var i = 0; i < eggs.Count; i++)
            {
                var egg = eggs[i];
                if (egg == null)
                    continue;

                var canonicalId = AlbumProgressService.NormalizeId(egg.Name);
                if (string.IsNullOrEmpty(canonicalId))
                    canonicalId = AlbumProgressService.NormalizeId(egg.name);
                if (string.IsNullOrEmpty(canonicalId))
                    continue;

                if (eggEntriesById.TryGetValue(canonicalId, out var existingEggEntry))
                {
                    if (ShouldPreferAsDefaultVariant(existingEggEntry.rareType, egg.RareType))
                    {
                        existingEggEntry.displayName = ResolveDisplayName(egg.Name, egg.name);
                        existingEggEntry.icon = egg.Icon;
                        existingEggEntry.rareType = egg.RareType;
                        existingEggEntry.egg = egg;
                    }

                    if (existingEggEntry.egg == egg || !_eggChanceByEggId.ContainsKey(canonicalId))
                    {
                        var mergedChances = ConveyorDropChanceCalculator.BuildBrainrotChances(egg, applyLuckBonusInChances);
                        _eggChanceByEggId[canonicalId] = mergedChances ?? new List<ConveyorDropChanceCalculator.ChanceEntry>();
                    }
                    continue;
                }

                var createdEggEntry = new EntryData
                {
                    type = AlbumEntityType.Egg,
                    id = canonicalId,
                    displayName = ResolveDisplayName(egg.Name, egg.name),
                    icon = egg.Icon,
                    rareType = egg.RareType,
                    egg = egg
                };
                _eggEntries.Add(createdEggEntry);
                eggEntriesById[canonicalId] = createdEggEntry;

                var chances = ConveyorDropChanceCalculator.BuildBrainrotChances(egg, applyLuckBonusInChances);
                _eggChanceByEggId[canonicalId] = chances ?? new List<ConveyorDropChanceCalculator.ChanceEntry>();
            }
        }

        var animals = storage.GetAllPetPrefabs();
        if (animals != null)
        {
            for (var i = 0; i < animals.Count; i++)
            {
                var pet = animals[i];
                if (pet == null)
                    continue;

                var canonicalId = AlbumProgressService.NormalizeId(pet.Name);
                if (string.IsNullOrEmpty(canonicalId))
                    canonicalId = AlbumProgressService.NormalizeId(pet.name);
                if (string.IsNullOrEmpty(canonicalId))
                    continue;

                if (_animalEntryById.TryGetValue(canonicalId, out var existingAnimalEntry))
                {
                    if (ShouldPreferAsDefaultVariant(existingAnimalEntry.rareType, pet.RareType))
                    {
                        existingAnimalEntry.displayName = ResolveDisplayName(pet.Name, pet.name);
                        existingAnimalEntry.icon = pet.Icon;
                        existingAnimalEntry.rareType = pet.RareType;
                        existingAnimalEntry.animal = pet;
                    }

                    AddAnimalAlias(canonicalId, pet.Name);
                    AddAnimalAlias(canonicalId, pet.name);
                    continue;
                }

                var createdAnimalEntry = new EntryData
                {
                    type = AlbumEntityType.Animal,
                    id = canonicalId,
                    displayName = ResolveDisplayName(pet.Name, pet.name),
                    icon = pet.Icon,
                    rareType = pet.RareType,
                    animal = pet
                };
                _animalEntries.Add(createdAnimalEntry);
                _animalEntryById[canonicalId] = createdAnimalEntry;

                AddAnimalAlias(canonicalId, pet.Name);
                AddAnimalAlias(canonicalId, pet.name);
            }
        }

        BuildAnimalSourcesFromEggs();

        if (sortByName)
        {
            _eggEntries.Sort((a, b) => string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase));
            _animalEntries.Sort((a, b) => string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void BuildAnimalSourcesFromEggs()
    {
        foreach (var kv in _eggChanceByEggId)
        {
            var eggId = kv.Key;
            var eggEntry = _eggEntries.FirstOrDefault(x => x.id == eggId);
            if (eggEntry == null || eggEntry.egg == null)
                continue;

            var list = kv.Value;
            if (list == null)
                continue;

            for (var i = 0; i < list.Count; i++)
            {
                var chanceRow = list[i];
                if (chanceRow == null)
                    continue;

                var chanceAnimalId = ResolveCanonicalAnimalId(chanceRow.id);
                if (string.IsNullOrEmpty(chanceAnimalId))
                    chanceAnimalId = AlbumProgressService.NormalizeId(chanceRow.id);
                if (string.IsNullOrEmpty(chanceAnimalId))
                    continue;

                if (!_animalSourcesByAnimalId.TryGetValue(chanceAnimalId, out var sources))
                {
                    sources = new List<AnimalSource>();
                    _animalSourcesByAnimalId.Add(chanceAnimalId, sources);
                }

                sources.Add(new AnimalSource
                {
                    eggId = eggId,
                    egg = eggEntry.egg,
                    chance = chanceRow.chance
                });
            }
        }
    }

    private void RefreshLocalizedDisplayNames()
    {
        var changed = false;

        for (var i = 0; i < _eggEntries.Count; i++)
        {
            var entry = _eggEntries[i];
            if (entry?.egg == null)
                continue;

            var displayName = ResolveDisplayName(entry.egg.Name, entry.egg.name);
            if (string.Equals(entry.displayName, displayName, StringComparison.Ordinal))
                continue;

            entry.displayName = displayName;
            changed = true;
        }

        for (var i = 0; i < _animalEntries.Count; i++)
        {
            var entry = _animalEntries[i];
            if (entry?.animal == null)
                continue;

            var displayName = ResolveDisplayName(entry.animal.Name, entry.animal.name);
            if (string.Equals(entry.displayName, displayName, StringComparison.Ordinal))
                continue;

            entry.displayName = displayName;
            changed = true;
        }

        if (!changed || !sortByName)
            return;

        _eggEntries.Sort((a, b) => string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase));
        _animalEntries.Sort((a, b) => string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase));
    }

    private void AddAnimalAlias(string canonicalId, string aliasRaw)
    {
        var alias = AlbumProgressService.NormalizeId(aliasRaw);
        if (string.IsNullOrEmpty(alias))
            return;

        _animalAliasToCanonicalId[alias] = canonicalId;
    }

    private string ResolveCanonicalAnimalId(string raw)
    {
        var normalized = AlbumProgressService.NormalizeId(raw);
        if (string.IsNullOrEmpty(normalized))
            return string.Empty;

        if (_animalAliasToCanonicalId.TryGetValue(normalized, out var canonical))
            return canonical;

        return normalized;
    }

    private void BuildElementTabs()
    {
        _supportedElementTypes.Clear();
        _supportedElementTypes.AddRange(AlbumProgressService.GetSupportedElementTypes());

        if (rareTabsRoot == null || rareTabPrefab == null)
            return;

        _elementViews.Clear();
        ClearElementTabsRootForRebuild();

        for (var i = 0; i < _supportedElementTypes.Count; i++)
        {
            var elementType = _supportedElementTypes[i];
            var view = SpawnRareTabView();
            if (view == null)
                continue;

            view.gameObject.SetActive(true);
            _elementViews[elementType] = view;
        }

        HideEmbeddedRareTabTemplate();

        rareTabsAdaptiveGrid?.Rebuild();
    }

    private void RefreshElementTabs()
    {
        if (_elementViews.Count == 0)
            return;

        var progress = progressService;
        foreach (var kv in _elementViews)
        {
            var elementType = kv.Key;
            var view = kv.Value;
            if (view == null)
                continue;

            var unlocked = progress != null && progress.IsElementUnlocked(elementType);
            var selected = _selectedElementFilter.HasValue && _selectedElementFilter.Value == elementType;
            var hasMention = progress != null && HasElementMentionForCurrentTab(elementType);
            var label = L(rareLabelKeyPrefix + elementType, GetElementLabelFallback(elementType));
            view.Bind(elementType, label, unlocked, selected, hasMention, () => OnElementPressed(elementType, unlocked));
        }
    }

    private void OnElementPressed(ElementType elementType, bool unlocked)
    {
        _selectedElementFilter = elementType;

        if (progressService != null && unlocked)
        {
            var selectedEntry = FindSelectedEntry();
            if (selectedEntry != null && !string.IsNullOrEmpty(selectedEntry.id))
            {
                var ids = new List<string> { selectedEntry.id };
                ExecuteWithoutProgressRefresh(() => progressService.MarkElementViewedForEntities(_currentTab, elementType, ids));
            }
        }

        Refresh();
    }

    private void RebuildCards()
    {
        if (cardsRoot == null || cardPrefab == null)
            return;

        ClearCardsRootForRebuild();

        var source = GetEntriesForCurrentTab();
        var visibleEntries = new List<EntryData>();
        for (var i = 0; i < source.Count; i++)
        {
            var entry = source[i];
            if (entry == null)
                continue;

            var unlocked = IsEntryDiscovered(entry);
            if (!includeLockedInList && !unlocked)
                continue;
            visibleEntries.Add(entry);
        }

        if (visibleEntries.Count == 0)
        {
            _selectedEntryId = null;
            HideEmbeddedCardTemplate();
            return;
        }

        if (string.IsNullOrEmpty(_selectedEntryId) || visibleEntries.All(x => x.id != _selectedEntryId))
            _selectedEntryId = visibleEntries[0].id;

        for (var i = 0; i < visibleEntries.Count; i++)
        {
            var entry = visibleEntries[i];
            var view = SpawnCardView();
            if (view == null)
                continue;
            view.gameObject.SetActive(true);

            var unlocked = IsEntryDiscovered(entry);
            var selected = string.Equals(entry.id, _selectedEntryId, StringComparison.Ordinal);
            var hasMention = progressService != null &&
                             (progressService.HasCardMention(entry.type, entry.id) ||
                              HasAnyRewardMention(entry) ||
                              HasAnyElementMention(entry));
            var title = unlocked ? entry.displayName : L(unknownKey, unknownFallback);

            view.Bind(entry.icon, title, unlocked, selected, hasMention, () => OnCardPressed(entry), entry.rareType);
            _spawnedCardViews.Add(view);
        }

        HideEmbeddedCardTemplate();
    }

    private void OnCardPressed(EntryData entry)
    {
        if (entry == null)
            return;

        _selectedEntryId = entry.id;
        if (progressService != null)
            ExecuteWithoutProgressRefresh(() => progressService.MarkCardViewed(entry.type, entry.id));
        Refresh();
    }

    private void RefreshInfoPanel()
    {
        var entry = FindSelectedEntry();
        if (entry == null)
        {
            SetLockedInfo(null);
            UpdateRewardUi(null, false);
            return;
        }

        var unlocked = IsEntryUnlockedForSelectedElement(entry);
        if (!unlocked)
        {
            SetLockedInfo(entry);
            UpdateRewardUi(entry, false);
            return;
        }

        SetUnlockedInfo(entry);
        UpdateRewardUi(entry, true);
    }

    private void SetLockedInfo(EntryData entry)
    {
        var unknown = L(unknownKey, unknownFallback);
        if (infoPanelView != null)
        {
            infoPanelView.ShowLocked(entry != null ? entry.icon : null, infoLockedColor, unknown);
            return;
        }

        if (infoIcon != null)
        {
            infoIcon.sprite = entry != null ? entry.icon : null;
            infoIcon.color = infoLockedColor;
        }

        SetEggHatchIcons(Array.Empty<HatchPreviewEntry>(), false);

        if (infoLockedOverlay != null)
            infoLockedOverlay.SetActive(true);

        if (infoTitle != null) infoTitle.text = unknown;
        if (infoDescription != null) infoDescription.text = unknown;
        if (infoIncome != null) infoIncome.text = unknown;
        if (infoSources != null) infoSources.text = unknown;
        if (infoLockedText != null) infoLockedText.text = unknown;
    }

    private void SetUnlockedInfo(EntryData entry)
    {
        if (infoPanelView != null)
        {
            SetUnlockedInfoView(entry);
            return;
        }

        if (infoIcon != null)
        {
            infoIcon.sprite = entry.icon;
            infoIcon.color = infoUnlockedColor;
        }

        if (infoLockedOverlay != null)
            infoLockedOverlay.SetActive(false);

        if (infoTitle != null)
            infoTitle.text = entry.displayName;

        if (entry.type == AlbumEntityType.Egg)
        {
            if (infoDescription != null)
                infoDescription.text = BuildEggFirstElementDateText(entry);

            if (infoIncome != null)
                infoIncome.text = BuildEggPriceText(entry);

            var previews = BuildEggHatchPreviewEntries(entry);
            var hasHatchUi = SetEggHatchIcons(previews, true);

            if (infoSources != null)
            {
                var rewardLine = BuildRewardInfoText(entry);
                if (hasHatchUi && previews.Count > 0)
                {
                    infoSources.text = string.Empty;
                }
                else
                {
                    var hatchLines = JoinNonEmptyLines(
                        L(eggInfoDescKey, eggInfoDescFallback),
                        BuildEggSourcesText(entry));
                    infoSources.text = JoinNonEmptyLines(rewardLine, hatchLines);
                }
            }
        }
        else
        {
            SetEggHatchIcons(Array.Empty<HatchPreviewEntry>(), false);

            if (infoDescription != null)
                infoDescription.text = BuildAnimalDescriptionText(entry);

            if (infoIncome != null)
                infoIncome.text = BuildAnimalIncomeText(entry);

            if (infoSources != null)
                infoSources.text = BuildAnimalMetaText(entry);
        }
    }

    private void SetUnlockedInfoView(EntryData entry)
    {
        if (entry == null)
            return;

        if (entry.type == AlbumEntityType.Egg)
        {
            var previews = BuildEggHatchPreviewEntries(entry);
            var hatchIcons = BuildInfoHatchIcons(previews);

            infoPanelView.ShowEgg(
                entry.icon,
                infoUnlockedColor,
                entry.displayName,
                BuildEggFirstElementDateText(entry),
                BuildEggPriceText(entry),
                string.Empty,
                hatchIcons,
                eggHatchUnlockedColor,
                eggHatchLockedColor);
            return;
        }

        infoPanelView.ShowAnimal(
            entry.icon,
            infoUnlockedColor,
            entry.displayName,
            BuildAnimalDescriptionText(entry),
            BuildAnimalIncomeText(entry),
            BuildAnimalMetaText(entry));
    }

    private static List<AlbumInfoPanelView.HatchIconData> BuildInfoHatchIcons(IReadOnlyList<HatchPreviewEntry> previews)
    {
        var result = new List<AlbumInfoPanelView.HatchIconData>();
        if (previews == null)
            return result;

        for (var i = 0; i < previews.Count; i++)
            result.Add(new AlbumInfoPanelView.HatchIconData(previews[i].icon, previews[i].unlocked));

        return result;
    }

    private List<HatchPreviewEntry> BuildEggHatchPreviewEntries(EntryData entry)
    {
        var result = new List<HatchPreviewEntry>();
        if (entry == null || string.IsNullOrEmpty(entry.id))
            return result;

        if (!_eggChanceByEggId.TryGetValue(entry.id, out var rows) || rows == null || rows.Count == 0)
            return result;

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row == null)
                continue;

            var canonicalAnimalId = ResolveCanonicalAnimalId(row.id);
            if (string.IsNullOrEmpty(canonicalAnimalId))
                canonicalAnimalId = AlbumProgressService.NormalizeId(row.id);
            if (string.IsNullOrEmpty(canonicalAnimalId))
                continue;

            _animalEntryById.TryGetValue(canonicalAnimalId, out var animalEntry);
            var unlocked = progressService != null && progressService.IsDiscovered(AlbumEntityType.Animal, canonicalAnimalId);
            result.Add(new HatchPreviewEntry
            {
                icon = animalEntry?.icon,
                unlocked = unlocked,
                chance = row.chance
            });
        }

        result.Sort((a, b) => b.chance.CompareTo(a.chance));

        var limit = eggHatchMaxIcons > 0 ? eggHatchMaxIcons : result.Count;
        if (result.Count > limit)
            result.RemoveRange(limit, result.Count - limit);

        return result;
    }

    private bool SetEggHatchIcons(IReadOnlyList<HatchPreviewEntry> previews, bool visible)
    {
        var shouldShowSection = visible && previews != null && previews.Count > 0;

        var requiredSlots = Mathf.Max(
            eggHatchMaxIcons > 0 ? eggHatchMaxIcons : 0,
            previews != null ? previews.Count : 0);

        if (!EnsureEggHatchIconPool(requiredSlots))
        {
            if (eggHatchSection != null)
                eggHatchSection.SetActive(false);
            return false;
        }

        if (eggHatchSection != null)
            eggHatchSection.SetActive(shouldShowSection);

        for (var i = 0; i < _eggHatchIconPool.Count; i++)
        {
            var icon = _eggHatchIconPool[i];
            if (icon == null)
                continue;

            var show = shouldShowSection && previews != null && i < previews.Count;
            icon.gameObject.SetActive(show);
            if (!show)
                continue;

            var preview = previews[i];
            icon.sprite = preview.icon;
            icon.color = preview.unlocked ? eggHatchUnlockedColor : eggHatchLockedColor;
        }

        return true;
    }

    private bool EnsureEggHatchIconPool(int requiredSlots)
    {
        _eggHatchIconPool.RemoveAll(x => x == null);

        if (_eggHatchIconPool.Count == 0 && eggHatchIconsRoot != null)
        {
            for (var i = 0; i < eggHatchIconsRoot.childCount; i++)
            {
                var child = eggHatchIconsRoot.GetChild(i);
                if (eggHatchIconTemplate != null && child == eggHatchIconTemplate.transform)
                    continue;

                var childImage = child.GetComponent<Image>();
                if (childImage != null)
                    _eggHatchIconPool.Add(childImage);
            }
        }

        if (eggHatchIconTemplate != null)
            eggHatchIconTemplate.gameObject.SetActive(false);

        if (eggHatchIconTemplate != null && eggHatchIconsRoot != null)
        {
            while (_eggHatchIconPool.Count < requiredSlots)
            {
                var icon = Instantiate(eggHatchIconTemplate, eggHatchIconsRoot);
                icon.gameObject.SetActive(false);
                _eggHatchIconPool.Add(icon);
            }
        }

        return _eggHatchIconPool.Count > 0;
    }

    private string BuildEggFirstElementDateText(EntryData entry)
    {
        var fallbackDate = L(albumDateUnknownKey, albumDateUnknownFallback);
        if (entry == null || progressService == null)
            return fallbackDate;

        var selectedElementType = _selectedElementFilter ?? ResolveDefaultElementForDisplay();
        if (!progressService.TryGetFirstElementDiscoveryDate(entry.type, entry.id, selectedElementType, out var firstSeenDate))
            return fallbackDate;

        return FormatAlbumDate(firstSeenDate);
    }

    private string BuildAnimalFirstDateText(EntryData entry)
    {
        var fallbackDate = L(albumDateUnknownKey, albumDateUnknownFallback);
        var rawLabel = L(animalFirstDateKey, animalFirstDateFallback);
        if (entry == null || progressService == null)
            return string.Format(rawLabel, fallbackDate);

        if (!progressService.TryGetFirstDiscoveryDate(entry.type, entry.id, out var firstSeenDate))
            return string.Format(rawLabel, fallbackDate);

        return string.Format(rawLabel, FormatAlbumDate(firstSeenDate));
    }

    private string BuildEggPriceText(EntryData entry)
    {
        if (entry?.egg == null)
            return "-";

        var rawPrice = Math.Max(0d, entry.egg.Data.Price);
        return FormatSoftAmount(rawPrice);
    }

    private string BuildAnimalDescriptionText(EntryData entry)
    {
        if (entry == null)
            return animalDescriptionFallback;

        if (animalDescriptionOverrides != null)
        {
            for (var i = 0; i < animalDescriptionOverrides.Count; i++)
            {
                var row = animalDescriptionOverrides[i];
                if (string.IsNullOrWhiteSpace(row.id) || string.IsNullOrWhiteSpace(row.description))
                    continue;
                if (!string.Equals(AlbumProgressService.NormalizeId(row.id), entry.id, StringComparison.Ordinal))
                    continue;
                return row.description.Trim();
            }
        }

        var key = animalDescriptionKeyPrefix + entry.id;
        var localized = L(key, key);
        if (!string.Equals(localized, key, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(localized))
            return localized;

        return L(animalInfoDescKey, animalInfoDescFallback);
    }

    private string BuildAnimalMetaText(EntryData entry)
    {
        return JoinNonEmptyLines(
            BuildAnimalFirstDateText(entry),
            BuildRewardInfoText(entry));
    }

    private string BuildRewardInfoText(EntryData entry)
    {
        if (entry == null)
            return string.Empty;

        var rewardAmount = ResolveRewardAmount(entry);
        if (rewardAmount <= 0)
            return string.Empty;

        var selectedElementType = _selectedElementFilter ?? ResolveDefaultElementForDisplay();
        var canClaim = progressService == null || progressService.CanClaimReward(entry.type, entry.id, selectedElementType);
        if (canClaim)
            return string.Format(L(rewardInfoKey, rewardInfoFallback), rewardAmount);

        return L(rewardClaimedKey, rewardClaimedFallback);
    }

    private string FormatAlbumDate(DateTimeOffset dateUtc)
    {
        var localDate = dateUtc.ToLocalTime();
        var format = string.IsNullOrWhiteSpace(albumDateFormat) ? "yyyy.MM.dd" : albumDateFormat;
        try
        {
            return localDate.ToString(format, CultureInfo.InvariantCulture);
        }
        catch
        {
            return localDate.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture);
        }
    }

    private static string JoinNonEmptyLines(params string[] lines)
    {
        if (lines == null || lines.Length == 0)
            return string.Empty;

        var sb = new StringBuilder();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (sb.Length > 0)
                sb.Append('\n');
            sb.Append(line.Trim());
        }

        return sb.ToString();
    }

    private string BuildEggSourcesText(EntryData entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.id) || !_eggChanceByEggId.TryGetValue(entry.id, out var list) || list == null || list.Count == 0)
            return "-";

        var sb = new StringBuilder();
        for (var i = 0; i < list.Count; i++)
        {
            var row = list[i];
            if (row == null)
                continue;

            var canonicalAnimalId = ResolveCanonicalAnimalId(row.id);
            var unlocked = progressService != null && progressService.IsDiscovered(AlbumEntityType.Animal, canonicalAnimalId);
            var name = unlocked ? ResolveAnimalDisplayName(canonicalAnimalId) : L(unknownKey, unknownFallback);
            if (sb.Length > 0)
                sb.Append('\n');
            sb.Append("• ");
            sb.Append(name);
            sb.Append(" - ");
            sb.Append((row.chance * 100d).ToString("0.##", CultureInfo.InvariantCulture));
            sb.Append('%');
        }

        return sb.Length > 0 ? sb.ToString() : "-";
    }

    private string BuildAnimalSourcesText(EntryData entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.id) || !_animalSourcesByAnimalId.TryGetValue(entry.id, out var list) || list == null || list.Count == 0)
            return "-";

        var sb = new StringBuilder();
        for (var i = 0; i < list.Count; i++)
        {
            var source = list[i];
            var unlocked = progressService != null && progressService.IsDiscovered(AlbumEntityType.Egg, source.eggId);
            var eggName = unlocked ? ResolveEggDisplayName(source.eggId) : L(unknownKey, unknownFallback);
            if (sb.Length > 0)
                sb.Append('\n');
            sb.Append("• ");
            sb.Append(eggName);
            sb.Append(" - ");
            sb.Append((source.chance * 100d).ToString("0.##", CultureInfo.InvariantCulture));
            sb.Append('%');
        }

        return sb.Length > 0 ? sb.ToString() : "-";
    }

    private string BuildAnimalIncomeText(EntryData entry)
    {
        if (entry?.animal == null)
            return "-";

        var incomeValue = Math.Max(0d, entry.animal.Data.StartIncome);
        var incomeLabel = L(incomeLabelKey, incomeLabelFallback);
        var incomeText = G.Currency != null
            ? G.Currency.ToString(incomeValue)
            : Math.Round(incomeValue).ToString("N0", CultureInfo.InvariantCulture);

        return $"{incomeLabel}: {incomeText}";
    }

    private void UpdateRewardUi(EntryData entry, bool unlocked)
    {
        if (rewardButton == null)
            return;

        if (!unlocked || entry == null || progressService == null)
        {
            rewardButton.gameObject.SetActive(false);
            if (rewardMentionBadge != null)
                rewardMentionBadge.SetActive(false);
            return;
        }

        var rewardAmount = ResolveRewardAmount(entry);
        var selectedElementType = _selectedElementFilter ?? ResolveDefaultElementForDisplay();
        var canClaim = rewardAmount > 0 && progressService.CanClaimReward(entry.type, entry.id, selectedElementType);

        if (!canClaim)
        {
            rewardButton.gameObject.SetActive(false);
            if (rewardMentionBadge != null)
                rewardMentionBadge.SetActive(false);
            return;
        }

        rewardButton.gameObject.SetActive(true);
        rewardButton.onClick.RemoveListener(OnRewardPressed);
        rewardButton.onClick.AddListener(OnRewardPressed);
        rewardButton.interactable = true;

        if (rewardButtonText != null)
        {
            rewardButtonText.text = string.Format(L(claimRewardKey, claimRewardFallback), rewardAmount);
        }

        if (rewardMentionBadge != null)
            rewardMentionBadge.SetActive(progressService.HasRewardMention(entry.type, entry.id, selectedElementType));
    }

    private void OnRewardPressed()
    {
        if (progressService == null)
            return;

        var entry = FindSelectedEntry();
        if (entry == null)
            return;

        var claimed = false;
        var selectedElementType = _selectedElementFilter ?? ResolveDefaultElementForDisplay();
        ExecuteWithoutProgressRefresh(() =>
        {
            claimed = progressService.TryClaimReward(entry.type, entry.id, selectedElementType);
            if (claimed)
                progressService.MarkCardViewed(entry.type, entry.id);
        });
        if (!claimed)
            return;

        var rewardAmount = ResolveRewardAmount(entry);
        if (rewardAmount > 0 && G.Currency != null)
            G.Currency.AddCurrency(CurrencyType.Gems, rewardAmount);

        if (rewardMentionBadge != null)
            rewardMentionBadge.SetActive(false);

        Refresh();
    }

    private int ResolveRewardAmount(EntryData entry)
    {
        var defaults = entry.type == AlbumEntityType.Egg ? defaultEggRewardGems : defaultAnimalRewardGems;
        var overrides = entry.type == AlbumEntityType.Egg ? eggRewardOverrides : animalRewardOverrides;
        if (overrides == null || overrides.Count == 0)
            return Mathf.Max(0, defaults);

        for (var i = 0; i < overrides.Count; i++)
        {
            var row = overrides[i];
            if (string.IsNullOrWhiteSpace(row.id))
                continue;
            if (!string.Equals(AlbumProgressService.NormalizeId(row.id), entry.id, StringComparison.Ordinal))
                continue;
            return Mathf.Max(0, row.gems);
        }

        return Mathf.Max(0, defaults);
    }

    private void RefreshTabMentions()
    {
        var progress = progressService;
        if (progress == null)
            return;

        var eggIds = _eggEntries.Select(x => x.id).ToList();
        var animalIds = _animalEntries.Select(x => x.id).ToList();

        var eggMention = progress.HasAnyTabMention(AlbumEntityType.Egg, eggIds, _supportedElementTypes);
        var animalMention = progress.HasAnyTabMention(AlbumEntityType.Animal, animalIds, _supportedElementTypes);

        if (eggsTabMention != null)
            eggsTabMention.SetActive(eggMention);
        if (animalsTabMention != null)
            animalsTabMention.SetActive(animalMention);
        if (albumIconMention != null)
            albumIconMention.SetActive(eggMention || animalMention);
    }

    private void RefreshStaticTexts()
    {
        if (eggsTabText != null)
            eggsTabText.text = L(eggsTabKey, eggsTabFallback);
        if (animalsTabText != null)
            animalsTabText.text = L(animalsTabKey, animalsTabFallback);
    }

    private void RefreshCardsSectionTitle()
    {
        if (cardsSectionTitle == null)
            return;

        cardsSectionTitle.text = _currentTab == AlbumEntityType.Egg
            ? L(eggsSectionTitleKey, eggsSectionTitleFallback)
            : L(animalsSectionTitleKey, animalsSectionTitleFallback);
    }

    private void RefreshTopTabVisuals()
    {
        ApplyTopTabVisual(
            eggsTabButton,
            eggsTabText,
            ref eggsTabBackground,
            ref eggsTabSelectedFrame,
            _currentTab == AlbumEntityType.Egg,
            ref _eggsTabBaseColor,
            ref _hasEggsTabBaseColor,
            ref _eggsTabDefaultStyle,
            ref _hasEggsTabDefaultStyle);

        ApplyTopTabVisual(
            animalsTabButton,
            animalsTabText,
            ref animalsTabBackground,
            ref animalsTabSelectedFrame,
            _currentTab == AlbumEntityType.Animal,
            ref _animalsTabBaseColor,
            ref _hasAnimalsTabBaseColor,
            ref _animalsTabDefaultStyle,
            ref _hasAnimalsTabDefaultStyle);
    }

    private void ApplyTopTabVisual(
        Button button,
        TMP_Text label,
        ref Image background,
        ref Image selectedFrame,
        bool selected,
        ref Color baseColor,
        ref bool hasBaseColor,
        ref FontStyles defaultStyle,
        ref bool hasDefaultStyle)
    {
        if (button == null)
            return;

        if (background == null)
            background = ResolveTopTabBackground(button);
        if (selectedFrame == null)
            selectedFrame = ResolveTopTabSelectedFrame(button);

        if (tintSelectedTopTab && background != null)
        {
            if (!hasBaseColor)
            {
                baseColor = background.color;
                hasBaseColor = true;
            }

            var targetColor = selected
                ? Color.Lerp(baseColor, Color.white, Mathf.Clamp01(activeTopTabLighten))
                : baseColor;
            targetColor.a = baseColor.a;
            background.color = targetColor;
        }
        else if (tintSelectedTopTab && button.targetGraphic != null)
        {
            if (!hasBaseColor)
            {
                baseColor = button.targetGraphic.color;
                hasBaseColor = true;
            }

            var targetColor = selected
                ? Color.Lerp(baseColor, Color.white, Mathf.Clamp01(activeTopTabLighten))
                : baseColor;
            targetColor.a = baseColor.a;
            button.targetGraphic.color = targetColor;
        }

        if (label != null)
        {
            if (!hasDefaultStyle)
            {
                defaultStyle = label.fontStyle;
                hasDefaultStyle = true;
            }

            if (boldSelectedTopTab)
                label.fontStyle = selected ? (defaultStyle | FontStyles.Bold) : defaultStyle;
            label.alpha = 1f;
        }

        if (selectedFrame != null)
        {
            selectedFrame.enabled = selected;
            selectedFrame.raycastTarget = false;
        }
    }

    private static Image ResolveTopTabBackground(Button button)
    {
        if (button == null)
            return null;

        var targetImage = button.targetGraphic as Image;
        if (targetImage != null && targetImage.sprite != null)
            return targetImage;

        Image candidate = null;
        var images = button.GetComponentsInChildren<Image>(true);
        for (var i = 0; i < images.Length; i++)
        {
            var image = images[i];
            if (image == null || image == targetImage)
                continue;
            if (image.sprite == null)
                continue;

            var imageName = image.gameObject.name;
            if (imageName.IndexOf("mention", StringComparison.OrdinalIgnoreCase) >= 0 ||
                imageName.IndexOf("badge", StringComparison.OrdinalIgnoreCase) >= 0 ||
                imageName.IndexOf("text", StringComparison.OrdinalIgnoreCase) >= 0 ||
                imageName.IndexOf("label", StringComparison.OrdinalIgnoreCase) >= 0 ||
                imageName.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            if (imageName.IndexOf("bg", StringComparison.OrdinalIgnoreCase) >= 0 ||
                imageName.IndexOf("background", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return image;
            }

            if (candidate == null)
                candidate = image;
        }

        return candidate ?? targetImage;
    }

    private static Image ResolveTopTabSelectedFrame(Button button)
    {
        if (button == null)
            return null;

        var images = button.GetComponentsInChildren<Image>(true);
        for (var i = 0; i < images.Length; i++)
        {
            var image = images[i];
            if (image == null)
                continue;

            var imageName = image.gameObject.name;
            if (imageName.IndexOf("selectedframe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                imageName.IndexOf("activeframe", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return image;
            }
        }

        return null;
    }

    private void BindButtons()
    {
        if (_bindingsReady)
            return;

        if (eggsTabButton != null)
            eggsTabButton.onClick.AddListener(SetTabEggs);
        if (animalsTabButton != null)
            animalsTabButton.onClick.AddListener(SetTabAnimals);
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
        if (rewardButton != null)
            rewardButton.onClick.AddListener(OnRewardPressed);

        _bindingsReady = true;
    }

    private void UnbindButtons()
    {
        if (!_bindingsReady)
            return;

        if (eggsTabButton != null)
            eggsTabButton.onClick.RemoveListener(SetTabEggs);
        if (animalsTabButton != null)
            animalsTabButton.onClick.RemoveListener(SetTabAnimals);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
        if (rewardButton != null)
            rewardButton.onClick.RemoveListener(OnRewardPressed);

        _bindingsReady = false;
    }

    private void OnProgressChanged()
    {
        if (_suppressProgressChangedRefresh)
            return;

        Refresh();
    }

    private ElementType? ResolveDefaultElementFilter(AlbumEntityType tab)
    {
        var source = tab == AlbumEntityType.Egg ? _eggEntries : _animalEntries;
        if (source == null || source.Count == 0)
            return null;

        if (progressService != null)
        {
            for (var i = 0; i < _supportedElementTypes.Count; i++)
            {
                var elementType = _supportedElementTypes[i];
                for (var j = 0; j < source.Count; j++)
                {
                    var entry = source[j];
                    if (entry == null || !IsEntryDiscovered(entry))
                        continue;
                    if (progressService.IsElementSeen(entry.type, entry.id, elementType))
                        return elementType;
                }
            }
        }

        if (_supportedElementTypes.Contains(ElementType.NoElement))
            return ElementType.NoElement;

        if (_supportedElementTypes.Contains(ElementType.Gold))
            return ElementType.Gold;

        if (_supportedElementTypes.Count > 0)
            return _supportedElementTypes[0];

        return null;
    }

    private bool IsEntryDiscovered(EntryData entry)
    {
        if (entry == null || progressService == null)
            return false;
        return progressService.IsDiscovered(entry.type, entry.id);
    }

    private bool IsEntryUnlockedForSelectedElement(EntryData entry)
    {
        if (!IsEntryDiscovered(entry))
            return false;

        var selectedElementType = _selectedElementFilter ?? ResolveDefaultElementForDisplay();
        if (progressService != null && progressService.IsElementSeen(entry.type, entry.id, selectedElementType))
            return true;

        // Compatibility fallback for old saves where per-element keys are absent.
        // In that case keep only the entry's base element visible, not all elements.
        if (progressService != null && !HasAnyElementSeenForEntry(entry))
            return ResolveEntryBaseElement(entry) == selectedElementType;

        return false;
    }

    private bool HasAnyElementSeenForEntry(EntryData entry)
    {
        if (entry == null || progressService == null || _supportedElementTypes == null || _supportedElementTypes.Count == 0)
            return false;

        for (var i = 0; i < _supportedElementTypes.Count; i++)
        {
            if (progressService.IsElementSeen(entry.type, entry.id, _supportedElementTypes[i]))
                return true;
        }

        return false;
    }

    private ElementType ResolveDefaultElementForDisplay()
    {
        if (_selectedElementFilter.HasValue)
            return _selectedElementFilter.Value;

        if (_supportedElementTypes.Count > 0)
            return _supportedElementTypes[0];

        return ElementType.NoElement;
    }

    private static ElementType ResolveEntryBaseElement(EntryData entry)
    {
        if (entry == null)
            return ElementType.NoElement;

        var element = ElementType.NoElement;
        if (entry.type == AlbumEntityType.Egg && entry.egg != null)
            element = entry.egg.Data.DinamicData.ElementType;
        else if (entry.type == AlbumEntityType.Animal && entry.animal != null)
            element = entry.animal.DinamicData.ElementType;

        return element == ElementType.ElementType ? ElementType.NoElement : element;
    }

    private List<EntryData> GetEntriesForCurrentTab()
    {
        return _currentTab == AlbumEntityType.Egg ? _eggEntries : _animalEntries;
    }

    private EntryData FindSelectedEntry()
    {
        if (string.IsNullOrEmpty(_selectedEntryId))
            return null;

        var source = GetEntriesForCurrentTab();
        for (var i = 0; i < source.Count; i++)
        {
            if (source[i] != null && string.Equals(source[i].id, _selectedEntryId, StringComparison.Ordinal))
                return source[i];
        }

        return null;
    }

    private string ResolveEggDisplayName(string id)
    {
        var normalized = AlbumProgressService.NormalizeId(id);
        for (var i = 0; i < _eggEntries.Count; i++)
        {
            if (_eggEntries[i] != null && _eggEntries[i].id == normalized)
                return _eggEntries[i].displayName;
        }
        return L(unknownKey, unknownFallback);
    }

    private string ResolveAnimalDisplayName(string id)
    {
        var normalized = AlbumProgressService.NormalizeId(id);
        for (var i = 0; i < _animalEntries.Count; i++)
        {
            if (_animalEntries[i] != null && _animalEntries[i].id == normalized)
                return _animalEntries[i].displayName;
        }
        return L(unknownKey, unknownFallback);
    }

    private static string ResolveDisplayName(string preferred, string fallbackName)
    {
        if (TryResolveLocalizedInventoryName(preferred, out var localized))
            return localized;
        if (TryResolveLocalizedInventoryName(fallbackName, out localized))
            return localized;

        if (!string.IsNullOrWhiteSpace(preferred))
            return preferred.Trim();
        if (!string.IsNullOrWhiteSpace(fallbackName))
            return fallbackName.Trim();
        return "Unknown";
    }

    private static bool TryResolveLocalizedInventoryName(string rawName, out string localized)
    {
        localized = null;
        if (string.IsNullOrWhiteSpace(rawName))
            return false;

        var key = rawName.Trim();
        if (TryGetLocalization(key, out localized))
            return true;

        return TryGetLocalization("Item/" + key, out localized);
    }

    private static bool TryGetLocalization(string key, out string localized)
    {
        localized = null;
        if (string.IsNullOrWhiteSpace(key))
            return false;

        var manager = LocalizationManager.Instance;
        var data = manager != null ? manager.LocalizationData : null;
        if (data == null)
            return false;

        var language = !string.IsNullOrWhiteSpace(manager.CurrentLanguage)
            ? manager.CurrentLanguage
            : null;
        if (string.IsNullOrWhiteSpace(language))
            return false;

        if (!data.TryGetTranslation(key, language, out var translated))
            return false;
        if (string.IsNullOrWhiteSpace(translated) || string.Equals(translated, key, StringComparison.Ordinal))
            return false;

        localized = translated.Trim();
        return true;
    }

    private static string FormatSoftAmount(double amount)
    {
        var value = Math.Max(0d, amount);
        var text = G.Currency != null
            ? G.Currency.ToString(value)
            : Math.Round(value).ToString("N0", CultureInfo.InvariantCulture);

        if (string.IsNullOrWhiteSpace(text))
            return "$0";

        text = text.Trim();
        return text.StartsWith("$", StringComparison.Ordinal) ? text : "$" + text;
    }

    private void AutoSetupReferences()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        if (itemStorage == null && G.Storage != null)
            itemStorage = G.Storage;
        if (progressService == null)
            progressService = G.Album;
        if (eggsTabBackground == null && eggsTabButton != null)
            eggsTabBackground = ResolveTopTabBackground(eggsTabButton);
        if (animalsTabBackground == null && animalsTabButton != null)
            animalsTabBackground = ResolveTopTabBackground(animalsTabButton);
        if (eggsTabSelectedFrame == null && eggsTabButton != null)
            eggsTabSelectedFrame = ResolveTopTabSelectedFrame(eggsTabButton);
        if (animalsTabSelectedFrame == null && animalsTabButton != null)
            animalsTabSelectedFrame = ResolveTopTabSelectedFrame(animalsTabButton);

        if (cardsRoot != null)
        {
            if (cardsAdaptiveGrid == null)
                cardsAdaptiveGrid = cardsRoot.GetComponent<AdaptiveGridSpawner>();
            if (cardsAdaptiveGrid == null)
                cardsAdaptiveGrid = cardsRoot.GetComponentInChildren<AdaptiveGridSpawner>(true);

            if (cardsAdaptiveGrid != null)
            {
                // If inspector references a wrapper root, prefer the actual grid root.
                if (cardsRoot != cardsAdaptiveGrid.transform)
                    cardsRoot = cardsAdaptiveGrid.transform;
            }
            else
            {
                if (cardsDynamicGrid == null)
                    cardsDynamicGrid = cardsRoot.GetComponent<DynamicGridSpawner>();
                if (cardsDynamicGrid == null)
                    cardsDynamicGrid = cardsRoot.GetComponentInChildren<DynamicGridSpawner>(true);

                if (cardsDynamicGrid != null && cardsRoot != cardsDynamicGrid.transform)
                    cardsRoot = cardsDynamicGrid.transform;
            }
        }

        if (rareTabsRoot != null)
        {
            if (rareTabsAdaptiveGrid == null)
                rareTabsAdaptiveGrid = rareTabsRoot.GetComponent<AdaptiveGridSpawner>();
            if (rareTabsAdaptiveGrid == null)
                rareTabsAdaptiveGrid = rareTabsRoot.GetComponentInChildren<AdaptiveGridSpawner>(true);

            if (rareTabsAdaptiveGrid != null && rareTabsRoot != rareTabsAdaptiveGrid.transform)
                rareTabsRoot = rareTabsAdaptiveGrid.transform;
        }

        if (panelRoot != null)
        {
            var panelTransform = panelRoot.transform;

            if (cardsSectionTitle == null)
            {
                var titleTransform = FindChildByName(panelTransform, "CardName") ??
                                     FindChildByName(panelTransform, "CardsTitle") ??
                                     FindChildByName(panelTransform, "SectionTitle");
                if (titleTransform != null)
                    cardsSectionTitle = titleTransform.GetComponent<TMP_Text>();
            }

            if (eggHatchSection == null)
            {
                var section = FindChildByName(panelTransform, "EggHatchSection") ??
                              FindChildByName(panelTransform, "HatchSection");
                if (section != null)
                    eggHatchSection = section.gameObject;
            }

            if (eggHatchIconsRoot == null)
            {
                eggHatchIconsRoot = FindChildByName(panelTransform, "EggHatchIconsRoot") ??
                                    FindChildByName(panelTransform, "HatchIconsRoot") ??
                                    FindChildByName(panelTransform, "InfoHatchIcons");
            }

            if (eggHatchSection == null && eggHatchIconsRoot != null)
                eggHatchSection = eggHatchIconsRoot.gameObject;

            if (eggHatchIconTemplate == null && eggHatchIconsRoot != null)
            {
                for (var i = 0; i < eggHatchIconsRoot.childCount; i++)
                {
                    var child = eggHatchIconsRoot.GetChild(i);
                    var image = child.GetComponent<Image>();
                    if (image == null)
                        continue;

                    if (!child.gameObject.activeSelf || child.name.IndexOf("template", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        eggHatchIconTemplate = image;
                        break;
                    }
                }
            }
        }
    }

    private void ApplyBlockyStyle()
    {
        if (!applyRuntimeBlockyStyle)
            return;

        BlockyUITheme.StyleWindow(panelRoot != null ? panelRoot : gameObject, BlockyUITheme.Tone.Green);
    }

    private AlbumEntryView SpawnCardView()
    {
        if (cardsAdaptiveGrid != null)
            return cardsAdaptiveGrid.SpawnObject<AlbumEntryView>(cardPrefab.gameObject);

        if (cardsDynamicGrid != null)
            return cardsDynamicGrid.SpawnObject<AlbumEntryView>(cardPrefab.gameObject);

        return Instantiate(cardPrefab, cardsRoot);
    }

    private AlbumRareTabView SpawnRareTabView()
    {
        if (rareTabsAdaptiveGrid != null)
            return rareTabsAdaptiveGrid.SpawnObject<AlbumRareTabView>(rareTabPrefab.gameObject);

        return Instantiate(rareTabPrefab, rareTabsRoot);
    }

    private void ClearElementTabsRootForRebuild()
    {
        if (rareTabsAdaptiveGrid != null)
            rareTabsAdaptiveGrid.ClearSpawnedItems();

        if (rareTabsRoot == null)
            return;

        var templateTransform = GetEmbeddedRareTabTemplateTransform();
        var pendingDestroy = new List<GameObject>();

        for (var i = rareTabsRoot.childCount - 1; i >= 0; i--)
        {
            var child = rareTabsRoot.GetChild(i);
            if (templateTransform != null && child == templateTransform)
            {
                child.gameObject.SetActive(false);
                continue;
            }

            if (child.GetComponent<AlbumRareTabView>() == null)
                continue;

            child.SetParent(null, false);
            pendingDestroy.Add(child.gameObject);
        }

        for (var i = 0; i < pendingDestroy.Count; i++)
            DestroyAlbumRuntimeObject(pendingDestroy[i]);
    }

    private Transform GetEmbeddedRareTabTemplateTransform()
    {
        if (rareTabPrefab == null || rareTabsRoot == null)
            return null;

        var template = rareTabPrefab.transform;
        if (template == null || !template.IsChildOf(rareTabsRoot))
            return null;

        return template;
    }

    private void HideEmbeddedRareTabTemplate()
    {
        var template = GetEmbeddedRareTabTemplateTransform();
        if (template != null)
            template.gameObject.SetActive(false);
    }

    private static void DestroyAlbumRuntimeObject(GameObject target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(target);
        else
            UnityEngine.Object.DestroyImmediate(target);
    }

    private void ClearCardsRootForRebuild()
    {
        var templateTransform = GetEmbeddedCardTemplateTransform();
        _spawnedCardViews.Clear();
        if (cardsAdaptiveGrid != null)
            cardsAdaptiveGrid.ClearSpawnedItems();

        var pendingDestroy = new List<GameObject>();

        for (var i = cardsRoot.childCount - 1; i >= 0; i--)
        {
            var child = cardsRoot.GetChild(i);
            if (templateTransform != null && child == templateTransform)
                continue;

            var isRuntimeRow = child.GetComponent<HorizontalLayoutGroup>() != null;
            var isDirectRuntimeCard = child.GetComponent<AlbumEntryView>() != null;
            if (!isRuntimeRow && !isDirectRuntimeCard)
                continue;

            child.SetParent(null, false);
            pendingDestroy.Add(child.gameObject);
        }

        for (var i = 0; i < pendingDestroy.Count; i++)
        {
            var go = pendingDestroy[i];
            if (go != null)
                Destroy(go);
        }
    }

    private Transform GetEmbeddedCardTemplateTransform()
    {
        if (cardPrefab == null || cardsRoot == null)
            return null;

        var template = cardPrefab.transform;
        if (template == null || !template.IsChildOf(cardsRoot))
            return null;

        return template;
    }

    private void HideEmbeddedCardTemplate()
    {
        var template = GetEmbeddedCardTemplateTransform();
        if (template != null)
            template.gameObject.SetActive(false);
    }

    private bool EnsureCatalogReady()
    {
        if (_catalogReady)
            return true;

        AutoSetupReferences();
        if (itemStorage == null)
            return false;

        BuildCatalog();
        return _catalogReady;
    }

    private void ExecuteWithoutProgressRefresh(Action action)
    {
        if (action == null)
            return;

        var previous = _suppressProgressChangedRefresh;
        _suppressProgressChangedRefresh = true;
        try
        {
            action.Invoke();
        }
        finally
        {
            _suppressProgressChangedRefresh = previous;
        }
    }

    private bool HasElementMentionForCurrentTab(ElementType elementType)
    {
        if (progressService == null)
            return false;

        var selectedEntry = FindSelectedEntry();
        if (selectedEntry != null && !string.IsNullOrEmpty(selectedEntry.id))
            return progressService.HasElementMention(selectedEntry.type, selectedEntry.id, elementType);

        var source = GetEntriesForCurrentTab();
        for (var i = 0; i < source.Count; i++)
        {
            var entry = source[i];
            if (entry == null)
                continue;
            if (progressService.HasElementMention(entry.type, entry.id, elementType))
                return true;
        }

        return false;
    }

    private bool HasAnyElementMention(EntryData entry)
    {
        if (entry == null || progressService == null || _supportedElementTypes == null || _supportedElementTypes.Count == 0)
            return false;

        for (var i = 0; i < _supportedElementTypes.Count; i++)
        {
            var elementType = _supportedElementTypes[i];
            if (progressService.HasElementMention(entry.type, entry.id, elementType))
                return true;
        }

        return false;
    }

    private bool HasAnyRewardMention(EntryData entry)
    {
        if (entry == null || progressService == null || _supportedElementTypes == null || _supportedElementTypes.Count == 0)
            return false;

        for (var i = 0; i < _supportedElementTypes.Count; i++)
        {
            var elementType = _supportedElementTypes[i];
            if (progressService.HasRewardMention(entry.type, entry.id, elementType))
                return true;
        }

        return false;
    }

    private static bool ShouldPreferAsDefaultVariant(RareType current, RareType candidate)
    {
        if (current == candidate)
            return false;

        if (candidate == RareType.Common && current != RareType.Common)
            return true;
        if (current == RareType.Common && candidate != RareType.Common)
            return false;

        return GetRarePriority(candidate) < GetRarePriority(current);
    }

    private static int GetRarePriority(RareType rareType)
    {
        switch (rareType)
        {
            case RareType.Common:
                return 0;
            case RareType.Uncommon:
                return 1;
            case RareType.Rare:
                return 2;
            case RareType.Epic:
                return 3;
            case RareType.Legendary:
                return 4;
            case RareType.Mythic:
                return 5;
            default:
                return 99;
        }
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrWhiteSpace(name))
            return null;

        if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase))
            return root;

        for (var i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            var match = FindChildByName(child, name);
            if (match != null)
                return match;
        }

        return null;
    }

    private static string GetElementLabelFallback(ElementType elementType)
    {
        switch (elementType)
        {
            case ElementType.Gold:
                return "Gold";
            case ElementType.Diamond:
                return "Diamond";
            case ElementType.Electric:
                return "Electric";
            case ElementType.Fire:
                return "Fire";
            case ElementType.NoElement:
                return "Neutral";
            default:
                return elementType.ToString();
        }
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
