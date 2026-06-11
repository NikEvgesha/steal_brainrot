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
        if (string.IsNullOrWhiteSpace(purchaseId))
        {
            Debug.LogError("[PurchasesManager] Cannot buy purchase: purchase id is empty.");
            onComplete?.Invoke(false);
            return;
        }

        if (provider == null)
        {
            Debug.LogError("Purchases provider not initialized!");
            onComplete?.Invoke(false);
            return;
        }

        if (!provider.IsInitialized)
        {
            Debug.LogWarning("[PurchasesManager] Cannot buy purchase: provider is not ready yet.");
            onComplete?.Invoke(false);
            return;
        }

        provider.BuyPurchase(purchaseId, onComplete);
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
