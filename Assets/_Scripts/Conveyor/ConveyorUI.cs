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
    [SerializeField] private ConveyorLevelTab _tabPrefab;
    [SerializeField] private Transform _tansParent;

    private GameObject _panel;
    private bool _isOpen;
    private ConveyorLevel _currentLevelInfo;
    private int _currentActiveIdx;
    private List<ConveyorLevelTab> _tabs;
    private readonly List<ConveyorDropChanceCalculator.ChanceEntry> _cachedDropChances = new();

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

    private void UpdateDropChances(ConveyorLevel level)
    {
        _cachedDropChances.Clear();
        if (level == null)
            return;

        var list = ConveyorDropChanceCalculator.BuildBrainrotChances(level);
        if (list != null && list.Count > 0)
            _cachedDropChances.AddRange(list);

        if (_dropChancesText == null)
            return;

        if (_cachedDropChances.Count == 0)
        {
            _dropChancesText.text = string.Empty;
            return;
        }

        var sb = new StringBuilder();
        for (int i = 0; i < _cachedDropChances.Count; i++)
        {
            var row = _cachedDropChances[i];
            if (i > 0) sb.Append('\n');
            sb.Append(row.name);
            sb.Append(": ");
            sb.Append((row.chance * 100d).ToString("0.00"));
            sb.Append('%');
        }

        _dropChancesText.text = sb.ToString();
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
        }

        result.AddRange(aggregate.Values);
        result.Sort((a, b) => b.chance.CompareTo(a.chance));
        return result;
    }

    public static List<ChanceEntry> BuildBrainrotChances(Egg egg)
    {
        var result = new List<ChanceEntry>();
        if (egg == null || egg.Data.Brainrots == null || egg.Data.Brainrots.Count == 0)
            return result;

        var chance = 1d / egg.Data.Brainrots.Count;
        var aggregate = new Dictionary<string, ChanceEntry>(StringComparer.Ordinal);
        for (int i = 0; i < egg.Data.Brainrots.Count; i++)
        {
            var brainrot = egg.Data.Brainrots[i];
            if (brainrot == null)
                continue;

            var id = GetId(brainrot.name, brainrot.Name);
            var name = string.IsNullOrWhiteSpace(brainrot.Name) ? brainrot.name : brainrot.Name;
            AddOrAccumulate(aggregate, id, name, chance);
        }

        result.AddRange(aggregate.Values);
        result.Sort((a, b) => b.chance.CompareTo(a.chance));
        return result;
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
}
