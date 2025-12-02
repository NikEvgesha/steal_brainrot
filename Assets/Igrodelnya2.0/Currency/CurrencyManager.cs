using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class CurrencyManager : MonoBehaviour
{
    [SerializeField] private Sprite _gemsIcon;
    [SerializeField] private Sprite _coinIcon;
    [SerializeField] private Sprite _realIcon; // Tmp

    [SerializeField] private float StartCoinsAmount;

    [SerializeField] private AudioClip _audioBuy;
    [SerializeField] private AudioClip _audioSell;
    [SerializeField] private AudioSource _audioSource;

    private Dictionary<CurrencyType, float> _balance = new() 
    {
        { CurrencyType.Coins, 0},
        { CurrencyType.Gems, 0}
    };

    private Dictionary<CurrencyType, Sprite> _currencyIcons;

    public double Gems { get { return _balance[CurrencyType.Gems]; } }
    public double Coins { get { return _balance[CurrencyType.Coins]; } }

    public UnityEvent<CurrencyType, float> CurrencyChanged;
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
        AddCurrency(CurrencyType.Coins, StartCoinsAmount);
        AddCurrency(CurrencyType.Coins, G.Save.LoadGameCoin());
        AddCurrency(CurrencyType.Gems, G.Save.GetGems());
    }


    public void Reset()
    {
        _balance[CurrencyType.Coins] = 0;
        AddCurrency(CurrencyType.Coins, StartCoinsAmount);
    }

    public void AddCurrency(CurrencyType type, float amount)
    {
        amount = Mathf.RoundToInt(amount);
        if (_audioSource)
            if(_audioSell)
                _audioSource.PlayOneShot(_audioSell);
        _balance[type] += amount;
        CurrencyChanged?.Invoke(type, _balance[type]);
        if (type == CurrencyType.Gems)
            G.Save.SaveGems(_balance[type]);
        else
            G.Save.SaveGameCoin(_balance[type]);
    }

    public bool RemoveCurrency(CurrencyType type, float amount)
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

    public float GetBalance(CurrencyType type)
    {
        return _balance[type];
    }

    public bool CheckEnoughCurrency(CurrencyType type, float amount, bool showNoGemsShop = true)
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
    
}
