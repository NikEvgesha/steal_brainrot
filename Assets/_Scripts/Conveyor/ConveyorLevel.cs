using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public struct LevelEggs
{
    public Egg egg;
}

public class ConveyorLevel : MonoBehaviour
{
    [SerializeField] private string _name;
    [SerializeField] private RareType _rarity;
    [SerializeField] private double _priceCoin;
    [SerializeField] private double _priceGems;
    [SerializeField] private Sprite _icon;
    [SerializeField] private float _incomeMultiplier;
    [SerializeField] private float _elementChanceBonus;
    [SerializeField] private List<LevelEggs> _eggs;
    [SerializeField] private Egg _newEgg;
    [SerializeField] private bool _purchased;
    private bool _active;
    private bool _availableForPurchase;

    public UnityEvent<ConveyorLevel> LevelPurchased;

    public float IncomeMultiplier => _incomeMultiplier;
    public float ElementChanceBonus01 => Mathf.Max(0f, _elementChanceBonus);
    public List<LevelEggs> Eggs => _eggs;
    public Egg NewEgg => _newEgg;

    public RareType RareType => _rarity;

    public string Name => _name;
    public double PriceCoin => _priceCoin;
    public double PriceGems => _priceGems;
    public Sprite Icon => _icon;

    public bool IsPurchased => _purchased;
    public bool IsActive => _active;
    public bool IsAvailable => _availableForPurchase;


    

    public Egg GetRandomEgg()
    {
        if (_eggs == null || _eggs.Count == 0)
            return null;

        int validCount = 0;
        for (int i = 0; i < _eggs.Count; i++)
        {
            if (_eggs[i].egg != null)
                validCount++;
        }

        if (validCount == 0)
            return null;

        int selectedValidIndex = UnityEngine.Random.Range(0, validCount);
        for (int i = 0; i < _eggs.Count; i++)
        {
            if (_eggs[i].egg == null)
                continue;

            if (selectedValidIndex == 0)
                return _eggs[i].egg;

            selectedValidIndex--;
        }

        return null;
    }

    public void TryBuy(bool forGems)
    {
        if (_purchased) return;

        CurrencyType currency = forGems ? CurrencyType.Gems : CurrencyType.Coins;
        double price = forGems ? _priceGems : _priceCoin;
        bool success = G.Currency.RemoveCurrency(currency, price);
        if (success)
        {
            SetPurchased(true);
            LevelPurchased?.Invoke(this);
        }

        var parameters = GameAnalytics.Params(
            "conveyor_level_id", _name,
            "rarity", _rarity.ToString().ToLowerInvariant(),
            "currency_type", currency.ToString().ToLowerInvariant(),
            "price", price,
            "source", "conveyor_upgrade_ui",
            "result", success ? "success" : "failed",
            "failure_reason", success ? string.Empty : "insufficient_currency");
        GameAnalytics.Track(AnalyticsEventNames.ConveyorUpgradeResult, parameters);
        if (success)
            GameAnalytics.TrackOnce("first_upgrade_purchased", AnalyticsEventNames.FirstUpgradePurchased, parameters);
    }

    public void SetPurchased(bool purchased)
    {
        _purchased = purchased;
    }


    public void SetActive(bool active)
    {
        _active = active;
    }

    public void SetPurchasingAvailable(bool available)
    {
        _availableForPurchase = available;
    }
}
