using UnityEngine;

[System.Serializable]
public struct FoodData
{
    public double MoneyPrice;
    public double GemPrice;
    public int SecondsDuration;
    public int XPPerSecond;
    public float SupplyProbability;
    public int StockAmount;
}

public class Food : InventoryItem
{
    [SerializeField] private FoodData data;
    public FoodData Data => data;
    public long TotalExperience =>
        (long)Mathf.Max(0, data.SecondsDuration) * Mathf.Max(0, data.XPPerSecond);
}
