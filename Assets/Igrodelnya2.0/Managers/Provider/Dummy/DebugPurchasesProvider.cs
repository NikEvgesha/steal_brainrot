using System;
using UnityEngine;


// Отладочный провайдер
public class DebugPurchasesProvider : PurchasesProvider
{
    public override void Initialize()
    {
        Debug.Log("Debug Purchases initialized");
    }

    public override void BuyPurchase(string purchaseId, Action<bool> onComplete)
    {
        Debug.Log($"Debug Purchase requested: {purchaseId}");
        onComplete?.Invoke(true); // Симулируем успешную покупку
    }

    public override void ConsumePendingPurchases()
    {
        Debug.Log("Debug Consuming pending purchases (none)");
    }

    public override PurchaseData GetPurchaseData(string purchaseId)
    {
        return new PurchaseData(purchaseId, "Test Item", "A debug purchase", "1.99 USD", "");
    }

    public override bool PurchasesAvailable()
    {
        return true;
    }
}