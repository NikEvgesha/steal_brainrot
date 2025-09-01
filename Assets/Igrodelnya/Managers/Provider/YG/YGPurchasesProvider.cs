#if YG_SDK_ENABLED
// Реализация для YG плагина
using System;
using UnityEngine;
using UnityEngine.Networking;
using YG;

public class YGPurchasesProvider : PurchasesProvider
{
    private bool isInitialized = false;

    public override void Initialize()
    {
        YG.YG2.onPurchaseSuccess += OnPurchaseSuccess;
        YG.YG2.onPurchaseFailed += OnPurchaseFailed;
        isInitialized = true;
        Debug.Log("YG Purchases initialized");
    }

    private Action<bool> currentCallback;
    public override void BuyPurchase(string purchaseId, Action<bool> onComplete)
    {
        if (!isInitialized)
        {
            Debug.LogWarning("YG Purchases not initialized!");
            onComplete?.Invoke(false);
            return;
        }

        currentCallback = onComplete;
        YG.YG2.BuyPayments(purchaseId);
        Debug.Log($"YG Purchase requested: {purchaseId}");
    }

    public override void ConsumePendingPurchases()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("YG Purchases not initialized!");
            return;
        }

        YG.YG2.ConsumePurchases(true); // Автоматически вызывает onPurchaseSuccess для необработанных покупок
        Debug.Log("YG Consuming pending purchases");
    }

    public override PurchaseData GetPurchaseData(string purchaseId)
    {
        if (!isInitialized)
        {
            Debug.LogWarning("YG Purchases not initialized!");
            return null;
        }

        var purchase = YG.YG2.PurchaseByID(purchaseId);
        if (purchase == null)
        {
            Debug.LogError($"No purchase found with ID: {purchaseId}");
            return null;
        }
        return new PurchaseData(purchase.id, purchase.title, purchase.description, purchase.price, purchase.currencyImageURL);
    }

    private void OnPurchaseSuccess(string id)
    {
        Debug.Log($"YG Purchase successful: {id}");
        Shop.Instance.OnRestorePurchases(id);
        currentCallback?.Invoke(true);
        currentCallback = null;
    }

    private void OnPurchaseFailed(string id)
    {
        Debug.LogWarning($"YG Purchase failed: {id}");
        currentCallback?.Invoke(false);
        currentCallback = null;
    }

    void OnDestroy()
    {
        YG.YG2.onPurchaseSuccess -= OnPurchaseSuccess;
        YG.YG2.onPurchaseFailed -= OnPurchaseFailed;
    }
}
#endif