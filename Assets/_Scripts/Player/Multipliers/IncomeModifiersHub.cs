using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public sealed class IncomeModifiersHub : MonoBehaviour
{
    [SerializeField] private bool _autoCollectFromChildren = true;
    [SerializeField] private bool _autoAddConveyorUpgradeBonus = true;
    [SerializeField] private List<IncomeModifierBehaviour> _modifiers = new();

    private CurrencyManager _currencyManager;

    public event UnityAction Changed;

    public IReadOnlyList<IncomeModifierBehaviour> Modifiers => _modifiers;

    public void Initialize(CurrencyManager currencyManager)
    {
        _currencyManager = currencyManager;
    }

    private void Awake()
    {
        if (G.Income != null && G.Income != this)
        {
            Destroy(gameObject);
            return;
        }
        G.Income = this;

        EnsureAutoModifiers();

        if (_autoCollectFromChildren)
        {
            var childModifiers = GetComponentsInChildren<IncomeModifierBehaviour>(includeInactive: true);
            for (int i = 0; i < childModifiers.Length; i++)
            {
                if (childModifiers[i] != null && !_modifiers.Contains(childModifiers[i]))
                    _modifiers.Add(childModifiers[i]);
            }
        }

        for (int i = 0; i < _modifiers.Count; i++)
        {
            if (_modifiers[i] == null) continue;
            _modifiers[i].Changed += OnAnyModifierChanged;
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _modifiers.Count; i++)
        {
            if (_modifiers[i] == null) continue;
            _modifiers[i].Changed -= OnAnyModifierChanged;
        }
    }

    private void OnAnyModifierChanged()
    {
        Changed?.Invoke();
    }

    private void EnsureAutoModifiers()
    {
        if (!_autoAddConveyorUpgradeBonus)
            return;

        if (GetComponentInChildren<ConveyorUpgradeBonusModifierMB>(includeInactive: true) != null)
            return;

        gameObject.AddComponent<ConveyorUpgradeBonusModifierMB>();
    }

    public void Register(IncomeModifierBehaviour modifier)
    {
        if (modifier == null) return;
        if (_modifiers.Contains(modifier)) return;

        _modifiers.Add(modifier);
        modifier.Changed += OnAnyModifierChanged;

        Changed?.Invoke();
    }

    public void Unregister(IncomeModifierBehaviour modifier)
    {
        if (modifier == null) return;
        if (!_modifiers.Remove(modifier)) return;

        modifier.Changed -= OnAnyModifierChanged;

        Changed?.Invoke();
    }

    public double Apply(double baseIncome)
    {
        double percentAdd = 0f;
        double multiplierProduct = 1f;

        for (int i = 0; i < _modifiers.Count; i++)
        {
            var m = _modifiers[i];
            if (m == null || !m.IsActive) continue;

            if (m.Kind == ModifierKind.PercentAdd) percentAdd += m.Value;
            else multiplierProduct *= m.Value;
        }

        return baseIncome * (1f + percentAdd) * multiplierProduct;
    }

    public void AddCoins(double baseIncome)
    {
        TryAddCoins(baseIncome);
    }

    public bool TryAddCoins(double baseIncome)
    {
        var currencyManager = ResolveCurrencyManager();
        if (currencyManager == null)
        {
            Debug.LogError($"{nameof(IncomeModifiersHub)}: CurrencyManager is not initialized.");
            return false;
        }

        double final = Apply(baseIncome);
        currencyManager.AddCurrency(CurrencyType.Coins, final);
        return true;
    }

    public void AddCurrency(CurrencyType type, double baseIncome)
    {
        TryAddCurrency(type, baseIncome);
    }

    public bool TryAddCurrency(CurrencyType type, double baseIncome)
    {
        var currencyManager = ResolveCurrencyManager();
        if (currencyManager == null)
        {
            Debug.LogError($"{nameof(IncomeModifiersHub)}: CurrencyManager is not initialized.");
            return false;
        }

        double final = Apply(baseIncome);
        currencyManager.AddCurrency(type, final);
        return true;
    }

    private CurrencyManager ResolveCurrencyManager()
    {
        if (_currencyManager != null)
            return _currencyManager;

        if (G.Currency != null)
        {
            _currencyManager = G.Currency;
            return _currencyManager;
        }

        _currencyManager = FindAnyObjectByType<CurrencyManager>();
        return _currencyManager;
    }
}
