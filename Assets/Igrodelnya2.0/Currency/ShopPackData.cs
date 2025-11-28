using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct ShopReward
{
    public ShopRewardType Type;
    public CurrencyType RewardCurrencyType;
    public int Amount;
    public Sprite Icon; // for currency;
    public InventoryItem Item; // icon -> item.icon
}

[CreateAssetMenu(fileName = "CurrencyPack", menuName = "Scriptable/CurrencyPackData")]
public class ShopPackData : ScriptableObject
{
    [SerializeField] private string _id;
    [SerializeField] private string _name;
    [SerializeField] private int price;
    [SerializeField] private CurrencyType _priceCurrencyType;
    [SerializeField] private ShopSlotType _slotType;
    [SerializeField] private List<ShopReward> _rewards;

    public string Id => _id;
    public string Name => _name;
    public List<ShopReward> Rewards => _rewards;
    public int Price => price;
    public CurrencyType PriceCurrencyType => _priceCurrencyType;
    public ShopSlotType SlotType => _slotType;
}