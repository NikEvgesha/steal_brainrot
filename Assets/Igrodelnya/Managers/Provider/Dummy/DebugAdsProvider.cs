using System;
using UnityEngine;

// Отладочный провайдер
public class DebugAdsProvider : AdsProvider
{
    public override void Initialize()
    {
        Debug.Log("Debug Ads initialized");
    }

    public override bool IsRewardedAdReady()
    {
        return true; // Всегда готов для отладки
    }

    public override void ShowRewardedAd(string rewardId, Action<bool> onComplete)
    {
        Debug.Log($"Debug Rewarded Ad shown with ID: {rewardId}");
        onComplete?.Invoke(true); // Симулируем успешное завершение
    }

    public override void ShowInterstitialAd()
    {
        Debug.Log("Debug Interstitial Ad shown");
    }
}