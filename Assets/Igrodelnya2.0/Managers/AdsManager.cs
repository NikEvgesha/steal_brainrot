using UnityEngine;
using System.Collections.Generic;
using System;


// Главный менеджер рекламы
public class AdsManager : MonoBehaviour
{
    //private static AdsManager _instance;
    //public static AdsManager Instance
    //{
    //    get
    //    {
    //        if (_instance == null)
    //        {
    //            GameObject go = new GameObject("AdsManager");
    //            _instance = go.AddComponent<AdsManager>();
    //            DontDestroyOnLoad(go);
    //        }
    //        return _instance;
    //    }
    //}

    [SerializeField] private List<AdsProvider> adsProviders = new List<AdsProvider>(); // Список активных провайдеров

    public Action AdClosed;
    private readonly HashSet<AdsProvider> subscribedProviders = new HashSet<AdsProvider>();

    void Awake()
    {
        if (G.Ad == null)
        {
            G.Ad = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        // Инициализация всех провайдеров
        InitializeProviders();
    }


    private void Start()
    {
        foreach (var provider in adsProviders)
        {
            SubscribeProvider(provider);
        }
    }

    private void OnDisable()
    {
        foreach (var provider in adsProviders)
        {
            UnsubscribeProvider(provider);
        }
    }

    private void OnAdClosed()
    {
        AdClosed?.Invoke();
    }

    private void InitializeProviders()
    {
        foreach (var provider in adsProviders)
        {
            if (provider == null)
                continue;

            provider.Initialize();
            //Debug.Log($"Initialized ads provider: {provider.GetType().Name}");
        }
    }

    // Проверка готовности rewarded-рекламы
    public bool IsRewardedAdReady()
    {
        if (adsProviders.Count == 0)
        {
            Debug.LogWarning("No ads providers configured!");
            return false;
        }

        foreach (var provider in adsProviders)
        {
            if (provider != null && provider.IsInitialized && provider.IsRewardedAdReady())
            {
                return true;
            }
        }
        return false;
    }

    // Показ rewarded-рекламы через первый готовый провайдер
    public void ShowRewardedAd(string rewardId, Action<bool> onComplete)
    {
        if (adsProviders.Count == 0)
        {
            Debug.LogWarning("No ads providers configured!");
            onComplete?.Invoke(false);
            return;
        }

        foreach (var provider in adsProviders)
        {
            if (provider != null && provider.IsInitialized && provider.IsRewardedAdReady())
            {
                provider.ShowRewardedAd(rewardId, onComplete);
                return;
            }
        }
        Debug.LogWarning("No rewarded ads available!");
        onComplete?.Invoke(false);
    }

    // Показ interstitial-рекламы через первый доступный провайдер
    public void ShowInterstitialAd()
    {
        if (adsProviders.Count == 0)
        {
            Debug.LogWarning("No ads providers configured!");
            return;
        }

        foreach (var provider in adsProviders)
        {
            if (provider != null && provider.IsInitialized && provider.IsInterstitialAdReady())
            {
                provider.ShowInterstitialAd();
                return;
            }
        }
        Debug.LogWarning("No interstitial ads available!");
    }

    // Метод для добавления провайдера в рантайме
    public void AddProvider(AdsProvider provider)
    {
        if (provider == null)
            return;

        if (!adsProviders.Contains(provider))
        {
            adsProviders.Add(provider);
            provider.Initialize();
            SubscribeProvider(provider);
        }
    }

    private void SubscribeProvider(AdsProvider provider)
    {
        if (provider == null || !subscribedProviders.Add(provider))
            return;

        provider.AdClosed += OnAdClosed;
    }

    private void UnsubscribeProvider(AdsProvider provider)
    {
        if (provider == null || !subscribedProviders.Remove(provider))
            return;

        provider.AdClosed -= OnAdClosed;
    }
}
