using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Events;

public class CurrencyManager : MonoBehaviour
{
    [SerializeField] private Sprite _gemsIcon;
    [SerializeField] private Sprite _coinIcon;
    [SerializeField] private Sprite _realIcon; // Tmp

    [SerializeField] private double StartCoinsAmount;
    [SerializeField] private List<String> _amountAbbreviation;

    [SerializeField] private AudioClip _audioBuy;
    [SerializeField] private AudioClip _audioSell;
    [SerializeField] private AudioSource _audioSource;

    private Dictionary<CurrencyType, double> _balance = new() 
    {
        { CurrencyType.Coins, 0},
        { CurrencyType.Gems, 0}
    };

    private Dictionary<CurrencyType, Sprite> _currencyIcons;

    public double Gems { get { return _balance[CurrencyType.Gems]; } }
    public double Coins { get { return _balance[CurrencyType.Coins]; } }

    public UnityEvent<CurrencyType, double> CurrencyChanged;
    public UnityEvent NoGems;
    public UnityEvent NoCoins;
    public UnityEvent<bool> ShowGems;

    private void Awake()
    {
        if (G.Currency == null)
        {
            G.Currency = this;
            DontDestroyOnLoad(gameObject);
            _currencyIcons = new Dictionary<CurrencyType, Sprite> {
            {CurrencyType.Gems, _gemsIcon},
            {CurrencyType.Coins, _coinIcon},
            {CurrencyType.Real, _realIcon},
        };
        }
        else
        {
            Destroy(gameObject);
        }

    }

    private void Start()
    {
        double coins = G.Save.LoadGameCoin();
        if (coins == -1)
            AddCurrency(CurrencyType.Coins, StartCoinsAmount);
        else
            AddCurrency(CurrencyType.Coins, coins);
        AddCurrency(CurrencyType.Gems, G.Save.GetGems());
    }


    public void Reset()
    {
        _balance[CurrencyType.Coins] = 0;
        AddCurrency(CurrencyType.Coins, StartCoinsAmount);
    }

    public void AddCurrency(CurrencyType type, double amount)
    {
        amount = Math.Round(amount);
        if (_audioSource)
            if(_audioSell)
                _audioSource.PlayOneShot(_audioSell);
        _balance[type] += amount;
        _balance[type] = (double.IsInfinity(_balance[type])) ? float.MaxValue : _balance[type];
        CurrencyChanged?.Invoke(type, _balance[type]);
        if (type == CurrencyType.Gems)
            G.Save.SaveGems(_balance[type]);
        else
            G.Save.SaveGameCoin(_balance[type]);
    }

    public bool RemoveCurrency(CurrencyType type, double amount)
    {
        if (_balance[type] >= amount)
        {
            if (_audioSource)
                if (_audioBuy)
                    _audioSource.PlayOneShot(_audioBuy);

            _balance[type] -= amount;
            CurrencyChanged?.Invoke(type, _balance[type]);
            if (type == CurrencyType.Gems)
                G.Save.SaveGems(_balance[type]);
            else
                G.Save.SaveGameCoin(_balance[type]);
            return true;
        }
        if (type == CurrencyType.Gems)
            NoGems.Invoke();
        return false;
    }

    public double GetBalance(CurrencyType type)
    {
        return _balance[type];
    }

    public bool CheckEnoughCurrency(CurrencyType type, double amount, bool showNoGemsShop = true)
    {
        if (amount <= _balance[type])
        {
            return true;
        }

        if (type == CurrencyType.Gems)
        {
            if (showNoGemsShop)
                NoGems?.Invoke();
        } else if (type == CurrencyType.Coins)
        {
            NoCoins?.Invoke();
        }
        return false;
    }


    public Sprite GetCurrencyIcon(CurrencyType type)
    {
        return _currencyIcons[type];
    }

    public String ToString(double amount)
    {
        if (double.IsNaN(amount) || double.IsInfinity(amount))
            return "0";

        var negative = amount < 0d;
        var value = Math.Abs(amount);

        if (_amountAbbreviation == null || _amountAbbreviation.Count == 0)
        {
            var fallback = value.ToString("0.##", CultureInfo.InvariantCulture);
            return negative && fallback != "0" ? "-" + fallback : fallback;
        }

        var abbrId = 0;
        while (value >= 1000d && abbrId < _amountAbbreviation.Count - 1)
        {
            value /= 1000d;
            abbrId++;
        }

        var format = value >= 100d ? "0" : value >= 10d ? "0.#" : "0.##";
        var numeric = value.ToString(format, CultureInfo.InvariantCulture);
        if (negative && numeric != "0")
            numeric = "-" + numeric;

        return numeric + _amountAbbreviation[abbrId];
    }
    
}
