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

    [Header("Root")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private bool hideOnStart = true;
    [SerializeField] private ItemPrefabStorage itemStorage;
    [SerializeField] private AlbumProgressService progressService;

    [Header("Tab Buttons")]
    [SerializeField] private Button eggsTabButton;
    [SerializeField] private Button animalsTabButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text eggsTabText;
    [SerializeField] private TMP_Text animalsTabText;
    [SerializeField] private GameObject eggsTabMention;
    [SerializeField] private GameObject animalsTabMention;
    [SerializeField] private GameObject albumIconMention;

    [Header("Cards")]
    [SerializeField] private Transform cardsRoot;
    [SerializeField] private AlbumEntryView cardPrefab;
    [SerializeField] private DynamicGridSpawner cardsDynamicGrid;

    [Header("Rare Tabs")]
    [SerializeField] private Transform rareTabsRoot;
    [SerializeField] private AlbumRareTabView rareTabPrefab;

    [Header("Info Panel")]
    [SerializeField] private Image infoIcon;
    [SerializeField] private TMP_Text infoTitle;
    [SerializeField] private TMP_Text infoDescription;
    [SerializeField] private TMP_Text infoIncome;
    [SerializeField] private TMP_Text infoSources;
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
    [SerializeField] private string unknownKey = "UI/Album/Unknown";
    [SerializeField] private string unknownFallback = "???";
    [SerializeField] private string eggInfoDescKey = "UI/Album/EggInfoDescription";
    [SerializeField] private string eggInfoDescFallback = "Can hatch from this egg:";
    [SerializeField] private string animalInfoDescKey = "UI/Album/AnimalInfoDescription";
    [SerializeField] private string animalInfoDescFallback = "Can hatch from eggs:";
    [SerializeField] private string incomeLabelKey = "UI/Album/Income";
    [SerializeField] private string incomeLabelFallback = "Income/sec";
    [SerializeField] private string claimRewardKey = "UI/Album/ClaimReward";
    [SerializeField] private string claimRewardFallback = "Claim +{0} Gems";
    [SerializeField] private string rewardClaimedKey = "UI/Album/RewardClaimed";
    [SerializeField] private string rewardClaimedFallback = "Claimed";
    [SerializeField] private string rareLockedKey = "UI/Album/RareLocked";
    [SerializeField] private string rareLockedFallback = "Hold item of this rarity to unlock";
    [SerializeField] private string rareLabelKeyPrefix = "UI/Album/Rare/";

    [Header("Behavior")]
    [SerializeField] private bool applyLuckBonusInChances = true;
    [SerializeField] private bool sortByName = true;
    [SerializeField] private bool includeLockedInList = true;

    private readonly List<EntryData> _eggEntries = new();
    private readonly List<EntryData> _animalEntries = new();
    private readonly Dictionary<string, string> _animalAliasToCanonicalId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<ConveyorDropChanceCalculator.ChanceEntry>> _eggChanceByEggId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<AnimalSource>> _animalSourcesByAnimalId = new(StringComparer.Ordinal);
    private readonly Dictionary<RareType, AlbumRareTabView> _rareViews = new();
    private readonly List<AlbumEntryView> _spawnedCardViews = new();
    private readonly List<RareType> _supportedRareTypes = new();

    private AlbumEntityType _currentTab = AlbumEntityType.Egg;
    private RareType? _selectedRareFilter;
    private string _selectedEntryId;
    private bool _bindingsReady;
    private bool _catalogReady;

    public bool IsOpen => panelRoot != null ? panelRoot.activeSelf : gameObject.activeSelf;

    private void Awake()
    {
        AutoSetupReferences();
        BuildCatalog();
        BuildRareTabs();
        BindButtons();
        RefreshStaticTexts();
        SetTab(AlbumEntityType.Egg, preserveSelection: false);

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

    public void SetOpen(bool open)
    {
        if (panelRoot != null)
            panelRoot.SetActive(open);
        else
            gameObject.SetActive(open);

        if (open)
            Refresh();
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
        RefreshTabMentions();
        RefreshRareTabs();
        RebuildCards();
        RefreshInfoPanel();
    }

    private void SetTab(AlbumEntityType tab, bool preserveSelection)
    {
        _currentTab = tab;
        _selectedRareFilter = null;
        if (!preserveSelection)
            _selectedEntryId = null;

        Refresh();
    }

    private void BuildCatalog()
    {
        _eggEntries.Clear();
        _animalEntries.Clear();
        _animalAliasToCanonicalId.Clear();
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
            var seenEggIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < eggs.Count; i++)
            {
                var egg = eggs[i];
                if (egg == null)
                    continue;

                var canonicalId = AlbumProgressService.NormalizeId(egg.Name);
                if (string.IsNullOrEmpty(canonicalId))
                    canonicalId = AlbumProgressService.NormalizeId(egg.name);
                if (string.IsNullOrEmpty(canonicalId) || !seenEggIds.Add(canonicalId))
                    continue;

                _eggEntries.Add(new EntryData
                {
                    type = AlbumEntityType.Egg,
                    id = canonicalId,
                    displayName = ResolveDisplayName(egg.Name, egg.name),
                    icon = egg.Icon,
                    rareType = egg.RareType,
                    egg = egg
                });

                var chances = ConveyorDropChanceCalculator.BuildBrainrotChances(egg, applyLuckBonusInChances);
                _eggChanceByEggId[canonicalId] = chances ?? new List<ConveyorDropChanceCalculator.ChanceEntry>();
            }
        }

        var animals = storage.GetAllPetPrefabs();
        if (animals != null)
        {
            var seenAnimalIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < animals.Count; i++)
            {
                var pet = animals[i];
                if (pet == null)
                    continue;

                var canonicalId = AlbumProgressService.NormalizeId(pet.Name);
                if (string.IsNullOrEmpty(canonicalId))
                    canonicalId = AlbumProgressService.NormalizeId(pet.name);
                if (string.IsNullOrEmpty(canonicalId) || !seenAnimalIds.Add(canonicalId))
                    continue;

                _animalEntries.Add(new EntryData
                {
                    type = AlbumEntityType.Animal,
                    id = canonicalId,
                    displayName = ResolveDisplayName(pet.Name, pet.name),
                    icon = pet.Icon,
                    rareType = pet.RareType,
                    animal = pet
                });

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

    private void BuildRareTabs()
    {
        _supportedRareTypes.Clear();
        _supportedRareTypes.AddRange(AlbumProgressService.GetSupportedRareTypes());

        if (rareTabsRoot == null || rareTabPrefab == null)
            return;

        _rareViews.Clear();
        var existingViews = rareTabsRoot.GetComponentsInChildren<AlbumRareTabView>(true)
            .Where(x => x != null && x.gameObject != rareTabPrefab.gameObject)
            .ToList();

        for (var i = 0; i < _supportedRareTypes.Count; i++)
        {
            var rare = _supportedRareTypes[i];
            AlbumRareTabView view;
            if (i < existingViews.Count)
            {
                view = existingViews[i];
            }
            else
            {
                view = Instantiate(rareTabPrefab, rareTabsRoot);
            }

            view.gameObject.SetActive(true);
            _rareViews[rare] = view;
        }

        for (var i = _supportedRareTypes.Count; i < existingViews.Count; i++)
            existingViews[i].gameObject.SetActive(false);

        if (rareTabPrefab != null)
            rareTabPrefab.gameObject.SetActive(false);
    }

    private void RefreshRareTabs()
    {
        if (_rareViews.Count == 0)
            return;

        var progress = progressService;
        foreach (var kv in _rareViews)
        {
            var rare = kv.Key;
            var view = kv.Value;
            if (view == null)
                continue;

            var unlocked = progress != null && progress.IsRareUnlocked(rare);
            var selected = _selectedRareFilter.HasValue && _selectedRareFilter.Value == rare;
            var hasMention = progress != null && HasRareMentionForCurrentTab(rare);
            var label = L(rareLabelKeyPrefix + rare, GetRareLabelFallback(rare));
            view.Bind(label, unlocked, selected, hasMention, () => OnRarePressed(rare, unlocked));
        }
    }

    private void OnRarePressed(RareType rareType, bool unlocked)
    {
        if (!unlocked)
        {
            if (infoLockedText != null)
                infoLockedText.text = L(rareLockedKey, rareLockedFallback);
            return;
        }

        if (_selectedRareFilter.HasValue && _selectedRareFilter.Value == rareType)
            _selectedRareFilter = null;
        else
            _selectedRareFilter = rareType;

        if (progressService != null)
        {
            var ids = CollectIdsByRareForCurrentTab(rareType);
            progressService.MarkRareViewedForEntities(_currentTab, rareType, ids);
        }

        RebuildCards();
        RefreshInfoPanel();
        RefreshRareTabs();
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

            var unlocked = progressService != null && progressService.IsDiscovered(entry.type, entry.id);
            if (!includeLockedInList && !unlocked)
                continue;
            if (_selectedRareFilter.HasValue && entry.rareType != _selectedRareFilter.Value)
                continue;
            visibleEntries.Add(entry);
        }

        if (visibleEntries.Count == 0)
        {
            _selectedEntryId = null;
            if (cardPrefab != null)
                cardPrefab.gameObject.SetActive(false);
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

            var unlocked = progressService != null && progressService.IsDiscovered(entry.type, entry.id);
            var selected = string.Equals(entry.id, _selectedEntryId, StringComparison.Ordinal);
            var hasMention = progressService != null &&
                             (progressService.HasCardMention(entry.type, entry.id) ||
                              progressService.HasRewardMention(entry.type, entry.id) ||
                              progressService.HasRareMention(entry.type, entry.id, entry.rareType));
            var title = unlocked ? entry.displayName : L(unknownKey, unknownFallback);

            view.Bind(entry.icon, title, unlocked, selected, hasMention, () => OnCardPressed(entry));
            _spawnedCardViews.Add(view);
        }

        if (cardPrefab != null)
            cardPrefab.gameObject.SetActive(false);
    }

    private void OnCardPressed(EntryData entry)
    {
        if (entry == null)
            return;

        _selectedEntryId = entry.id;
        if (progressService != null)
            progressService.MarkCardViewed(entry.type, entry.id);
        RebuildCards();
        RefreshInfoPanel();
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

        var unlocked = progressService != null && progressService.IsDiscovered(entry.type, entry.id);
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
        if (infoIcon != null)
        {
            infoIcon.sprite = entry != null ? entry.icon : null;
            infoIcon.color = infoLockedColor;
        }

        if (infoLockedOverlay != null)
            infoLockedOverlay.SetActive(true);

        var unknown = L(unknownKey, unknownFallback);
        if (infoTitle != null) infoTitle.text = unknown;
        if (infoDescription != null) infoDescription.text = unknown;
        if (infoIncome != null) infoIncome.text = unknown;
        if (infoSources != null) infoSources.text = unknown;
        if (infoLockedText != null) infoLockedText.text = unknown;
    }

    private void SetUnlockedInfo(EntryData entry)
    {
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
                infoDescription.text = L(eggInfoDescKey, eggInfoDescFallback);

            if (infoIncome != null)
                infoIncome.text = "-";

            if (infoSources != null)
                infoSources.text = BuildEggSourcesText(entry);
        }
        else
        {
            if (infoDescription != null)
                infoDescription.text = L(animalInfoDescKey, animalInfoDescFallback);

            if (infoIncome != null)
                infoIncome.text = BuildAnimalIncomeText(entry);

            if (infoSources != null)
                infoSources.text = BuildAnimalSourcesText(entry);
        }
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

        rewardButton.gameObject.SetActive(true);
        var rewardAmount = ResolveRewardAmount(entry);
        var claimed = progressService.IsRewardClaimed(entry.type, entry.id);
        var canClaim = progressService.CanClaimReward(entry.type, entry.id);

        rewardButton.onClick.RemoveListener(OnRewardPressed);
        rewardButton.onClick.AddListener(OnRewardPressed);
        rewardButton.interactable = canClaim;

        if (rewardButtonText != null)
        {
            rewardButtonText.text = claimed
                ? L(rewardClaimedKey, rewardClaimedFallback)
                : string.Format(L(claimRewardKey, claimRewardFallback), rewardAmount);
        }

        if (rewardMentionBadge != null)
            rewardMentionBadge.SetActive(progressService.HasRewardMention(entry.type, entry.id));
    }

    private void OnRewardPressed()
    {
        if (progressService == null)
            return;

        var entry = FindSelectedEntry();
        if (entry == null)
            return;

        if (!progressService.TryClaimReward(entry.type, entry.id))
            return;

        var rewardAmount = ResolveRewardAmount(entry);
        if (rewardAmount > 0 && G.Currency != null)
            G.Currency.AddCurrency(CurrencyType.Gems, rewardAmount);

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

        var eggMention = progress.HasAnyTabMention(AlbumEntityType.Egg, eggIds, _supportedRareTypes);
        var animalMention = progress.HasAnyTabMention(AlbumEntityType.Animal, animalIds, _supportedRareTypes);

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
        Refresh();
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
        if (!string.IsNullOrWhiteSpace(preferred))
            return preferred.Trim();
        if (!string.IsNullOrWhiteSpace(fallbackName))
            return fallbackName.Trim();
        return "Unknown";
    }

    private void AutoSetupReferences()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        if (itemStorage == null && G.Storage != null)
            itemStorage = G.Storage;
        if (progressService == null)
            progressService = G.Album;
        if (cardsDynamicGrid == null && cardsRoot != null)
            cardsDynamicGrid = cardsRoot.GetComponent<DynamicGridSpawner>();
    }

    private AlbumEntryView SpawnCardView()
    {
        if (cardsDynamicGrid != null)
            return cardsDynamicGrid.SpawnObject<AlbumEntryView>(cardPrefab.gameObject);

        return Instantiate(cardPrefab, cardsRoot);
    }

    private void ClearCardsRootForRebuild()
    {
        var templateTransform = cardPrefab != null ? cardPrefab.transform : null;
        for (var i = cardsRoot.childCount - 1; i >= 0; i--)
        {
            var child = cardsRoot.GetChild(i);
            if (templateTransform != null && child == templateTransform)
                continue;

            Destroy(child.gameObject);
        }

        _spawnedCardViews.Clear();
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

    private List<string> CollectIdsByRareForCurrentTab(RareType rareType)
    {
        var ids = new List<string>();
        var source = GetEntriesForCurrentTab();
        for (var i = 0; i < source.Count; i++)
        {
            var entry = source[i];
            if (entry == null || entry.rareType != rareType || string.IsNullOrEmpty(entry.id))
                continue;
            ids.Add(entry.id);
        }

        return ids;
    }

    private bool HasRareMentionForCurrentTab(RareType rareType)
    {
        if (progressService == null)
            return false;

        var source = GetEntriesForCurrentTab();
        for (var i = 0; i < source.Count; i++)
        {
            var entry = source[i];
            if (entry == null || entry.rareType != rareType)
                continue;
            if (progressService.HasRareMention(entry.type, entry.id, rareType))
                return true;
        }

        return false;
    }

    private static string GetRareLabelFallback(RareType rareType)
    {
        switch (rareType)
        {
            case RareType.Common:
                return "Common";
            case RareType.Uncommon:
                return "Uncommon";
            case RareType.Rare:
                return "Rare";
            case RareType.Epic:
                return "Epic";
            case RareType.Legendary:
                return "Legendary";
            case RareType.Mythic:
                return "Mythic";
            default:
                return rareType.ToString();
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
