using System;
using UnityEngine;

public class DebugAdsProvider : AdsProvider
{
    public override bool IsInitialized => true;

    public override void Initialize()
    {
        Debug.Log("Debug Ads initialized");
    }

    public override bool IsRewardedAdReady()
    {
        return true;
    }

    public override bool IsInterstitialAdReady()
    {
        return true;
    }

    public override void ShowRewardedAd(string rewardId, Action<bool> onComplete)
    {
        Debug.Log($"Debug Rewarded Ad shown with ID: {rewardId}");
        onComplete?.Invoke(true);
    }

    public override void ShowInterstitialAd()
    {
        ShowInterstitialAd(null);
    }

    public override void ShowInterstitialAd(Action<bool> onComplete)
    {
        Debug.Log("Debug Interstitial Ad shown");
        AdClosed?.Invoke();
        onComplete?.Invoke(true);
    }
}
