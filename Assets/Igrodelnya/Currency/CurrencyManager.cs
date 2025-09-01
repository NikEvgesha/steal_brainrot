using MirraGames.SDK;
using System;
using System.Collections.Generic;
using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    [SerializeField] private Sprite _gemsIcon;
    [SerializeField] private Sprite _coinIcon;
    [SerializeField] private Sprite _realIcon; // Tmp

    [SerializeField] private int StartCoinsAmount;

    [SerializeField] private AudioClip _audioBuy;
    [SerializeField] private AudioClip _audioSell;
    [SerializeField] private AudioSource _audioSource;
    private static CurrencyManager _instance;

    private Dictionary<CurrencyType, int> _balance = new() 
    {
        { CurrencyType.Coins, 0},
        { CurrencyType.Gems, 0}
    };

    private Dictionary<CurrencyType, Sprite> _currencyIcons;

    public int Gems { get { return _balance[CurrencyType.Gems]; } }
    public int Coins { get { return _balance[CurrencyType.Coins]; } }

    public Action<CurrencyType, int> CurrencyChanged;
    public Action NoGems;
    public Action NoCoins;
    public Action<bool> ShowGems;
    public static CurrencyManager Instance { get { return _instance; } }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
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
        
        AddCurrency(CurrencyType.Coins, SaveManager.Instance.LoadGameCoin());
        AddCurrency(CurrencyType.Gems, SaveManager.Instance.GetGems());
    }


    public void Reset()
    {
        _balance[CurrencyType.Coins] = 0;
        AddCurrency(CurrencyType.Coins, StartCoinsAmount);
    }

    public void AddCurrency(CurrencyType type, int amount)
    {
        if (_audioSource)
            if(_audioSell)
                _audioSource.PlayOneShot(_audioSell);
        _balance[type] += amount;
        CurrencyChanged?.Invoke(type, _balance[type]);
        if (type == CurrencyType.Gems)
            SaveManager.Instance.SaveGems(_balance[type]);
        else
            SaveManager.Instance.SaveGameCoin(_balance[type]);
    }

    public bool RemoveCurrency(CurrencyType type, int amount)
    {
        if (_balance[type] >= amount)
        {
            if (_audioSource)
                if (_audioBuy)
                    _audioSource.PlayOneShot(_audioBuy);

            _balance[type] -= amount;
            CurrencyChanged?.Invoke(type, _balance[type]);
            if (type == CurrencyType.Gems)
                SaveManager.Instance.SaveGems(_balance[type]);
            else
                SaveManager.Instance.SaveGameCoin(_balance[type]);
            return true;
        }
        return false;
    }

    public int GetBalance(CurrencyType type)
    {
        return _balance[type];
    }

    public bool CheckEnoughCurrency(CurrencyType type, int amount, bool showNoGemsShop = true)
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
