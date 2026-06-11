#if YG_SDK_ENABLED
using System;
using UnityEngine;
// Реализация для YG плагина
public class YGAdsProvider : AdsProvider
{
    private bool isInitialized = false;
    public override bool IsInitialized => isInitialized;

    public override void Initialize()
    {
        // Подписка на событие вознаграждения
        YG.YG2.onRewardAdv += OnReward;
        YG.YG2.onOpenRewardedAdv += OnRewardedAdOpened;
        YG.YG2.onErrorRewardedAdv += OnRewardedAdClosed;
        isInitialized = true;
        Debug.Log("YG Ads initialized");
    }

    public override bool IsRewardedAdReady()
    {
        // YG не предоставляет явного метода проверки готовности, предполагаем, что реклама доступна после инициализации
        return isInitialized;
    }

    public override bool IsInterstitialAdReady()
    {
        return isInitialized;
    }

    public override void ShowRewardedAd(string rewardId, Action<bool> onComplete)
    {
        if (!isInitialized)
        {
            Debug.LogWarning("YG Ads not initialized!");
            onComplete?.Invoke(false);
            return;
        }

        // Сохраняем коллбэк для обработки результата
        currentRewardCallback = onComplete;
        currentRewardId = rewardId;

        // Вызов rewarded-рекламы с ID
        YG.YG2.RewardedAdvShow(rewardId);
        Debug.Log($"YG Rewarded Ad requested with ID: {rewardId}");
    }

    private string currentRewardId;
    private Action<bool> currentRewardCallback;

    private void OnReward(string id)
    {
        if (id == currentRewardId)
        {
            Debug.Log($"YG Rewarded Ad completed with ID: {id}");
            currentRewardCallback?.Invoke(true);
            currentRewardCallback = null;
            currentRewardId = null;
        }
    }

    private void OnRewardedAdOpened()
    {
        Debug.Log("YG Rewarded Ad opened");
    }

    private void OnRewardedAdClosed()
    {
        Debug.Log("YG Rewarded Ad closed");
        // Если пользователь закрыл рекламу до получения награды
        if (currentRewardCallback != null)
        {
            currentRewardCallback?.Invoke(false);
            currentRewardCallback = null;
            currentRewardId = null;
        }
    }

    public override void ShowInterstitialAd()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("YG Ads not initialized!");
            return;
        }

        YG.YG2.InterstitialAdvShow();
        Debug.Log("YG Interstitial Ad requested");
        AdClosed?.Invoke();
    }

    // Очистка подписок
    public void OnDestroy()
    {
        YG.YG2.onRewardAdv -= OnReward;
        YG.YG2.onOpenRewardedAdv -= OnRewardedAdOpened;
        YG.YG2.onErrorRewardedAdv -= OnRewardedAdClosed;
    }
}
#endif
