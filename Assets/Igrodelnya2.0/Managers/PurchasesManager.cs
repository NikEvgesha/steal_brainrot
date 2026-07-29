using UnityEngine;
using System;
using System.Collections;

// Данные о покупке (универсальная структура)
public class PurchaseData
{
    public string Id { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public string Price { get; private set; }
    public string CurrencyImageURL { get; private set; }

    public PurchaseData(string id, string title, string description, string price, string currencyImageURL)
    {
        Id = id ?? "";
        Title = title ?? "";
        Description = description ?? "";
        Price = price ?? "";
        CurrencyImageURL = currencyImageURL ?? "";
    }

    public static PurchaseData Fallback(string id)
    {
        return new PurchaseData(id, "", "", "", "");
    }
}

// Главный менеджер покупок
public class PurchasesManager : MonoBehaviour
{
    //private static PurchasesManager _instance;
    //public static PurchasesManager Instance
    //{
    //    get
    //    {
    //        if (_instance == null)
    //        {
    //            GameObject go = new GameObject("PurchasesManager");
    //            _instance = go.AddComponent<PurchasesManager>();
    //            DontDestroyOnLoad(go);
    //        }
    //        return _instance;
    //    }
    //}

    [SerializeField] private MonoBehaviour activeProvider; // Активный провайдер в инспекторе
    private PurchasesProvider provider;
    private bool pendingRestore;
    private Coroutine restoreWhenReadyRoutine;

    void Awake()
    {
        if (G.Purchases != null && G.Purchases != this)
        {
            Destroy(gameObject);
            return;
        }
        G.Purchases = this;
        DontDestroyOnLoad(gameObject);

        // Проверка и инициализация провайдера
        if (activeProvider == null || !activeProvider.TryGetComponent(out provider))
        {
            Debug.LogError("No valid purchases provider assigned!");
            return;
        }

        provider.Initialize();
    }
    

    public void RestorePurchases()
    {
        if (provider == null)
        {
            Debug.LogWarning("[PurchasesManager] Cannot restore purchases: provider is not initialized.");
            return;
        }

        if (provider.IsInitialized)
        {
            provider.ConsumePendingPurchases();
            pendingRestore = false;
            return;
        }

        pendingRestore = true;
        if (restoreWhenReadyRoutine == null)
            restoreWhenReadyRoutine = StartCoroutine(RestoreWhenReady());
    }

    // Вызов покупки
    public void BuyPurchase(string purchaseId, Action<bool> onComplete)
    {
        string requestId = Guid.NewGuid().ToString("N");
        float requestedAt = Time.realtimeSinceStartup;
        GameAnalytics.TrackCritical(AnalyticsEventNames.PurchaseStarted, GameAnalytics.Params(
            "request_id", requestId,
            "product_id", purchaseId ?? string.Empty,
            "provider", provider != null ? provider.GetType().Name : string.Empty,
            "source", "platform_purchase",
            "result", "started"),
            requestId);

        if (string.IsNullOrWhiteSpace(purchaseId))
        {
            Debug.LogError("[PurchasesManager] Cannot buy purchase: purchase id is empty.");
            TrackPurchaseResult(requestId, purchaseId, requestedAt, false, "empty_product_id");
            onComplete?.Invoke(false);
            return;
        }

        if (provider == null)
        {
            Debug.LogError("Purchases provider not initialized!");
            TrackPurchaseResult(requestId, purchaseId, requestedAt, false, "provider_missing");
            onComplete?.Invoke(false);
            return;
        }

        if (!provider.IsInitialized)
        {
            Debug.LogWarning("[PurchasesManager] Cannot buy purchase: provider is not ready yet.");
            TrackPurchaseResult(requestId, purchaseId, requestedAt, false, "provider_not_ready");
            onComplete?.Invoke(false);
            return;
        }

        provider.BuyPurchase(purchaseId, success =>
        {
            TrackPurchaseResult(requestId, purchaseId, requestedAt, success,
                success ? string.Empty : "cancelled_or_failed");
            onComplete?.Invoke(success);
        });
    }

    private void TrackPurchaseResult(
        string requestId,
        string purchaseId,
        float requestedAt,
        bool success,
        string failureReason)
    {
        GameAnalytics.TrackCritical(AnalyticsEventNames.PurchaseResult, GameAnalytics.Params(
            "request_id", requestId,
            "product_id", purchaseId ?? string.Empty,
            "provider", provider != null ? provider.GetType().Name : string.Empty,
            "latency_ms", Math.Round(Math.Max(0f, Time.realtimeSinceStartup - requestedAt) * 1000d),
            "is_restore", false,
            "source", "platform_purchase",
            "result", success ? "success" : "failed",
            "failure_reason", failureReason ?? string.Empty),
            requestId);
    }

    // Получение данных о покупке
    public PurchaseData GetPurchaseData(string purchaseId)
    {
        if (provider == null || !provider.IsInitialized)
        {
            Debug.LogWarning($"[PurchasesManager] Purchase data for '{purchaseId}' requested before provider was ready. Using fallback data.");
            return PurchaseData.Fallback(purchaseId);
        }

        return provider.GetPurchaseData(purchaseId) ?? PurchaseData.Fallback(purchaseId);
    }

    // Установка нового провайдера в рантайме (опционально)
    public void SetProvider(PurchasesProvider newProvider)
    {
        if (newProvider == null)
        {
            Debug.LogError("[PurchasesManager] Cannot set null purchases provider.");
            return;
        }

        provider = newProvider;
        provider.Initialize();
        RestorePurchases();
    }

    public bool PurchasesAvailable()
    {
        return provider != null && provider.IsInitialized && provider.PurchasesAvailable();
    }

    private IEnumerator RestoreWhenReady()
    {
        while (provider != null && !provider.IsInitialized)
            yield return null;

        restoreWhenReadyRoutine = null;

        if (!pendingRestore || provider == null)
            yield break;

        provider.ConsumePendingPurchases();
        pendingRestore = false;
    }
}
