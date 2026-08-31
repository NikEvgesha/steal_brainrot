using System;
using UnityEngine;
using MirraGames.SDK;
using MirraGames.SDK.Common;  // пространство имён SDK

//#if MIRRA_SDK_ENABLED
public class MirraSDKPurchaseProvider : PurchasesProvider
{
    private bool isInitialized = false;
    //private Action<bool> currentCallback;
    public override bool IsInitialized => isInitialized;

    public override void Initialize()
    {
        MirraSDK.WaitForProviders(() =>
        {
            isInitialized = true;
            //Debug.Log("MirraSDK: Payments initialized");
        });  // :contentReference[oaicite:0]{index=0}
    }

    public override void BuyPurchase(string purchaseId, Action<bool> onComplete)
    {
        if (!isInitialized)
        {
            Debug.LogWarning("MirraSDK: Payments not initialized");
            onComplete?.Invoke(false);
            return;
        }
/*
        PauseManager.Instance?.SetPause(true);
        G.Control.CursorActive = true;*/

        MirraSDK.Payments.Purchase(
            purchaseId,
            onSuccess: () =>
            {
                Debug.Log($"MirraSDK: Purchase successful: {purchaseId}");
                onComplete?.Invoke(true);/*
                PauseManager.Instance?.SetPause(false);
                G.Control.CursorActive = false;*/
            },
            onError: () =>
            {
                Debug.LogWarning($"MirraSDK: Purchase failed or closed: {purchaseId}");
                onComplete?.Invoke(false);/*
                PauseManager.Instance?.SetPause(false);
                G.Control.CursorActive = false;*/
            }
        );  // :contentReference[oaicite:1]{index=1}

        Debug.Log($"MirraSDK: Purchase requested: {purchaseId}");
    }

    public override void ConsumePendingPurchases()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("MirraSDK: Payments not initialized");
            return;
        }

        MirraSDK.Payments.RestorePurchases((restoreData) =>
        {
            //Debug.Log($"MirraSDK: Restored purchases: {string.Join(", ", restoreData.AllPurchases)}");
            //Debug.Log($"MirraSDK: Pending products: {string.Join(", ", restoreData.PendingProducts)}");

            foreach (var id in restoreData.PendingProducts)
            {

                restoreData.RestoreProduct(id, onProductRestore: () => {

                    SpecialShop.DeliverOrQueueRestoredPurchase(id);

                    Debug.Log($"Товар '{id}' восстановлен");

                });

            }
        });  // :contentReference[oaicite:3]{index=3}

        Debug.Log("MirraSDK: Restoring pending purchases");
    }

    public override PurchaseData GetPurchaseData(string purchaseId)
    {
        if (!isInitialized)
        {
            Debug.LogWarning("MirraSDK: Payments not initialized");
            return null;
        }

        ProductData data = MirraSDK.Payments.GetProductData(purchaseId);
        if (data == null)
        {
            Debug.LogError($"MirraSDK: No product data for ID '{purchaseId}'");
            return null;
        }

        return new PurchaseData(
            data.Tag,
            "",
            "",
            data.PriceFloat.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            "",
            data.Currency
        );  // :contentReference[oaicite:4]{index=4}
    }

    public override bool PurchasesAvailable()
    {
        return (MirraSDK.Platform.Current == MirraGames.SDK.Common.PlatformType.YandexGames);
    }

    private void OnDestroy()
    {
        // Никаких глобальных событий не подписывали, всё в делегатах.
    }
}
//#endif
