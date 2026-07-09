using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct ShopReward
{
    public ShopRewardType Type;
    public CurrencyType RewardCurrencyType;
    public int Amount;
    public Sprite Icon;
    public InventoryItem Item;
    [Min(0f)] public float EffectValue;
    [Min(0)] public int DurationMinutes;
}

[CreateAssetMenu(fileName = "CurrencyPack", menuName = "Scriptable/CurrencyPackData")]
public class ShopPackData : ScriptableObject
{
    [SerializeField] private string _id;
    [SerializeField] private string _name;
    [SerializeField] private int price;
    [SerializeField] private CurrencyType _priceCurrencyType;
    [SerializeField] private ShopSlotType _slotType;
    [SerializeField] private ShopCategory _category;
    [SerializeField] private string _descriptionKey;
    [SerializeField, TextArea(2, 4)] private string _descriptionFallback;
    [SerializeField] private Sprite _icon;
    [SerializeField] private Color _accentColor = new Color(0.08f, 0.48f, 0.94f, 1f);
    [SerializeField] private int _sortOrder;
    [SerializeField] private bool _featured;
    [SerializeField] private List<ShopReward> _rewards;

    public string Id => _id;
    public string Name => _name;
    public List<ShopReward> Rewards => _rewards;
    public int Price => price;
    public CurrencyType PriceCurrencyType => _priceCurrencyType;
    public ShopSlotType SlotType => _slotType;
    public ShopCategory Category => _category;
    public string DescriptionKey => _descriptionKey;
    public string DescriptionFallback => _descriptionFallback;
    public Sprite Icon => _icon;
    public Color AccentColor => _accentColor;
    public int SortOrder => _sortOrder;
    public bool Featured => _featured;

    public bool HasConsumableReward
    {
        get
        {
            if (_rewards == null)
                return false;

            for (int i = 0; i < _rewards.Count; i++)
            {
                if (ShopRewardUtility.IsConsumable(_rewards[i].Type))
                    return true;
            }

            return false;
        }
    }
}

public static class ShopRewardUtility
{
    public static bool IsConsumable(ShopRewardType type)
    {
        return type == ShopRewardType.ConsumableIncomeBoost ||
               type == ShopRewardType.ConsumableElementLuckBoost ||
               type == ShopRewardType.ConsumableHatchSkip;
    }

    public static bool IsPermanent(ShopRewardType type)
    {
        return type == ShopRewardType.PermanentIncomePercent ||
               type == ShopRewardType.PermanentElementLuckPercent;
    }
}
