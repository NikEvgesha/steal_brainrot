using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ConveyorUI : MonoBehaviour
{
    [SerializeField] private Text _levelName;
    [SerializeField] private Image _icon;
    [SerializeField] private Image _newEggIcon;
    [SerializeField] private Text _priceCoins;
    [SerializeField] private Text _priceGems;
    [SerializeField] private Text _incomeMultiplier;
    [SerializeField] private Button _buttonBuyGems;
    [SerializeField] private Button _buttonBuyCoins;
    [SerializeField] private Button _buttonActivate;
    [SerializeField] private GameObject _activeText;
    [SerializeField] private GameObject _notAvailableText;
    [SerializeField] private Text _dropChancesText;
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
    private readonly List<ConveyorDropChanceCalculator.ChanceEntry> _cachedDropChances = new();
    private readonly List<ConveyorDropChanceCalculator.ChanceEntry> _cachedDropChancesWithLuck = new();
    private readonly List<ConveyorDropChanceCalculator.EggBreakdownEntry> _cachedEggBreakdown = new();

    [HideInInspector]
    public UnityEvent<ConveyorLevel> LevelActivated = new();



    public void Init(List<ConveyorLevel> levels)
    {
        _tabs = new();
        foreach (ConveyorLevel level in levels) {
            ConveyorLevelTab tab = Instantiate(_tabPrefab, _tansParent);
            tab.Init(level);
            tab.OnClick.AddListener(SetInfo);
            _tabs.Add(tab);
        }
        _tabs[_currentActiveIdx].SetLvlActive(true);
        //_currentLevelInfo = levels[0];
        SetInfo(levels[0]);
    }

    private void Awake()
    {
        EnsurePanel();
    }

    public void ToggleOpen(bool open)
    {
        _isOpen = open;
        EnsurePanel();
        if (_panel == null) return;
        _panel.SetActive(open);
        _chancesPanel.Close();
        if (open)
            RefreshCurrentDropChances();
    }

    private void EnsurePanel()
    {
        if (_panel != null) return;
        if (transform.childCount <= 0) return;
        _panel = transform.GetChild(0).gameObject;
    }


    public void SetInfo(ConveyorLevel level)
    {
        if (_currentLevelInfo == level) return;

        _currentLevelInfo = level;
        _levelName.text = level.Name;
        _icon.sprite = level.Icon;
        _newEggIcon.sprite = level.NewEgg.Icon;

        for (int i = _newEggPetsPanel.childCount - 1; i >= 0; i--)
        {
            Destroy(_newEggPetsPanel.GetChild(i).gameObject);
        }

        foreach (Brainrot pet in level.NewEgg.Data.Brainrots)
        {
            GameObject icon = Instantiate(_newEggPetIcon, _newEggPetsPanel);
            icon.GetComponentInChildren<Image>().sprite = pet.Icon;
        }

        _incomeMultiplier.text = "+" + ((level.IncomeMultiplier - 1) * 100).ToString() + "%";
        UpdateDropChances(level);

        SetButtons();
    }

    private void SetButtons()
    {
        _activeText.gameObject.SetActive(_currentLevelInfo.IsActive);
        _notAvailableText.gameObject.SetActive(!_currentLevelInfo.IsPurchased && !_currentLevelInfo.IsAvailable);
        _buttonActivate.gameObject.SetActive(_currentLevelInfo.IsPurchased && !_currentLevelInfo.IsActive);
        _buttonBuyCoins.gameObject.SetActive(!_currentLevelInfo.IsPurchased && _currentLevelInfo.IsAvailable);
        _buttonBuyGems.gameObject.SetActive(!_currentLevelInfo.IsPurchased && _currentLevelInfo.IsAvailable);
        

        if (!_currentLevelInfo.IsPurchased)
        {
            _priceCoins.text = _currentLevelInfo.PriceCoin.ToString();
            _priceGems.text = _currentLevelInfo.PriceGems.ToString();
        }
    }


    public void _OnBuyCoinsClick()
    {
        _currentLevelInfo.TryBuy(forGems:false);

        if (_currentLevelInfo.IsPurchased)
        {
            SetButtons();
        }
    }

    public void _OnBuyGemsClick()
    {
        _currentLevelInfo.TryBuy(forGems: true);

        if (_currentLevelInfo.IsPurchased)
        {
            SetButtons();
        }
    }

    public void _OnBuyActivateClick()
    {
        LevelActivated?.Invoke(_currentLevelInfo);
        SetButtons();
    }

    public void UpdateActiveLvl(int idx)
    {
        _tabs[_currentActiveIdx].SetLvlActive(false);
        _currentActiveIdx = idx;
        _tabs[_currentActiveIdx].SetLvlActive(true);
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

    public static List<ChanceEntry> BuildEggChances(ConveyorLevel level)
    {
        var result = new List<ChanceEntry>();
        if (level == null || level.Eggs == null || level.Eggs.Count == 0)
            return result;

        var totalWeight = 0d;
        for (int i = 0; i < level.Eggs.Count; i++)
            totalWeight += Math.Max(0d, level.Eggs[i].weight);
        if (totalWeight <= 0d)
            return result;

        var aggregate = new Dictionary<string, ChanceEntry>(StringComparer.Ordinal);
        for (int i = 0; i < level.Eggs.Count; i++)
        {
            var source = level.Eggs[i];
            if (source.egg == null || source.weight <= 0f)
                continue;

            var chance = Math.Max(0d, source.weight) / totalWeight;
            var id = GetId(source.egg.name, source.egg.Name);
            var name = string.IsNullOrWhiteSpace(source.egg.Name) ? source.egg.name : source.egg.Name;
            AddOrAccumulate(aggregate, id, name, chance);
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
        if (level == null || level.Eggs == null || level.Eggs.Count == 0)
            return result;

        var totalWeight = 0d;
        for (int i = 0; i < level.Eggs.Count; i++)
            totalWeight += Math.Max(0d, level.Eggs[i].weight);
        if (totalWeight <= 0d)
            return result;

        var aggregate = new Dictionary<string, EggBreakdownEntry>(StringComparer.Ordinal);
        for (int i = 0; i < level.Eggs.Count; i++)
        {
            var source = level.Eggs[i];
            var egg = source.egg;
            if (egg == null || source.weight <= 0f)
                continue;

            var chance = Math.Max(0d, source.weight) / totalWeight;
            var id = GetId(egg.name, egg.Name);
            var name = string.IsNullOrWhiteSpace(egg.Name) ? egg.name : egg.Name;
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

            entry.chance += chance;
        }

        result.AddRange(aggregate.Values);
        result.Sort((a, b) => b.chance.CompareTo(a.chance));
        return result;
    }

    public static List<ChanceEntry> BuildBrainrotChances(ConveyorLevel level, bool applyLuckBonus)
    {
        var result = new List<ChanceEntry>();
        if (level == null || level.Eggs == null || level.Eggs.Count == 0)
            return result;

        var totalEggWeight = 0d;
        for (int i = 0; i < level.Eggs.Count; i++)
            totalEggWeight += Math.Max(0d, level.Eggs[i].weight);
        if (totalEggWeight <= 0d)
            return result;

        var aggregate = new Dictionary<string, ChanceEntry>(StringComparer.Ordinal);
        for (int i = 0; i < level.Eggs.Count; i++)
        {
            var source = level.Eggs[i];
            var egg = source.egg;
            if (egg == null || source.weight <= 0f)
                continue;
            if (egg.Data.Brainrots == null || egg.Data.Brainrots.Count == 0)
                continue;

            var eggChance = Math.Max(0d, source.weight) / totalEggWeight;

            if (!applyLuckBonus)
            {
                var perBrainrot = eggChance / egg.Data.Brainrots.Count;
                for (int j = 0; j < egg.Data.Brainrots.Count; j++)
                {
                    var brainrot = egg.Data.Brainrots[j];
                    if (brainrot == null)
                        continue;

                    var id = GetId(brainrot.name, brainrot.Name);
                    var name = string.IsNullOrWhiteSpace(brainrot.Name) ? brainrot.name : brainrot.Name;
                    AddOrAccumulate(aggregate, id, name, perBrainrot);
                }
                continue;
            }

            var weightSum = 0d;
            for (int j = 0; j < egg.Data.Brainrots.Count; j++)
                weightSum += GetLuckAdjustedWeight(egg.Data.Brainrots[j], egg.Data.Luck, applyLuckBonus);
            if (weightSum <= 0d)
                continue;

            for (int j = 0; j < egg.Data.Brainrots.Count; j++)
            {
                var brainrot = egg.Data.Brainrots[j];
                var itemWeight = GetLuckAdjustedWeight(brainrot, egg.Data.Luck, applyLuckBonus);
                if (brainrot == null || itemWeight <= 0d)
                    continue;

                var brainrotChance = eggChance * (itemWeight / weightSum);
                var id = GetId(brainrot.name, brainrot.Name);
                var name = string.IsNullOrWhiteSpace(brainrot.Name) ? brainrot.name : brainrot.Name;
                AddOrAccumulate(aggregate, id, name, brainrotChance);
            }
        }

        result.AddRange(aggregate.Values);
        result.Sort((a, b) => b.chance.CompareTo(a.chance));
        return result;
    }

    public static List<ChanceEntry> BuildBrainrotChances(Egg egg)
    {
        return BuildBrainrotChances(egg, applyLuckBonus: false);
    }

    public static List<ChanceEntry> BuildBrainrotChances(Egg egg, bool applyLuckBonus)
    {
        var result = new List<ChanceEntry>();
        if (egg == null || egg.Data.Brainrots == null || egg.Data.Brainrots.Count == 0)
            return result;

        var aggregate = new Dictionary<string, ChanceEntry>(StringComparer.Ordinal);

        if (!applyLuckBonus)
        {
            var chance = 1d / egg.Data.Brainrots.Count;
            for (int i = 0; i < egg.Data.Brainrots.Count; i++)
            {
                var brainrot = egg.Data.Brainrots[i];
                if (brainrot == null)
                    continue;

                var id = GetId(brainrot.name, brainrot.Name);
                var name = string.IsNullOrWhiteSpace(brainrot.Name) ? brainrot.name : brainrot.Name;
                AddOrAccumulate(aggregate, id, name, chance);
            }
        }
        else
        {
            var totalWeight = 0d;
            for (int i = 0; i < egg.Data.Brainrots.Count; i++)
                totalWeight += GetLuckAdjustedWeight(egg.Data.Brainrots[i], egg.Data.Luck, applyLuckBonus);
            if (totalWeight <= 0d)
                return result;

            for (int i = 0; i < egg.Data.Brainrots.Count; i++)
            {
                var brainrot = egg.Data.Brainrots[i];
                var weight = GetLuckAdjustedWeight(brainrot, egg.Data.Luck, applyLuckBonus);
                if (brainrot == null || weight <= 0d)
                    continue;

                var id = GetId(brainrot.name, brainrot.Name);
                var name = string.IsNullOrWhiteSpace(brainrot.Name) ? brainrot.name : brainrot.Name;
                AddOrAccumulate(aggregate, id, name, weight / totalWeight);
            }
        }

        result.AddRange(aggregate.Values);
        result.Sort((a, b) => b.chance.CompareTo(a.chance));
        return result;
    }

    public static Brainrot PickRandomBrainrot(Egg egg, bool applyLuckBonus = true)
    {
        if (egg == null || egg.Data.Brainrots == null || egg.Data.Brainrots.Count == 0)
            return null;

        return PickRandomBrainrot(egg.Data.Brainrots, egg.Data.Luck, applyLuckBonus);
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
