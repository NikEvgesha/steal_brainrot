#if YG_SDK_ENABLED
using System;
using UnityEngine;

public class YGAdsProvider : AdsProvider
{
    private bool isInitialized;
    private string currentRewardId;
    private Action<bool> currentRewardCallback;

    public override bool IsInitialized => isInitialized;

    public override void Initialize()
    {
        YG.YG2.onRewardAdv += OnReward;
        YG.YG2.onOpenRewardedAdv += OnRewardedAdOpened;
        YG.YG2.onErrorRewardedAdv += OnRewardedAdClosed;
        isInitialized = true;
        Debug.Log("YG Ads initialized");
    }

    public override bool IsRewardedAdReady()
    {
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

        currentRewardCallback = onComplete;
        currentRewardId = rewardId;
        YG.YG2.RewardedAdvShow(rewardId);
        Debug.Log($"YG Rewarded Ad requested with ID: {rewardId}");
    }

    private void OnReward(string id)
    {
        if (id != currentRewardId)
            return;

        Debug.Log($"YG Rewarded Ad completed with ID: {id}");
        currentRewardCallback?.Invoke(true);
        currentRewardCallback = null;
        currentRewardId = null;
        AdClosed?.Invoke();
    }

    private void OnRewardedAdOpened()
    {
        Debug.Log("YG Rewarded Ad opened");
    }

    private void OnRewardedAdClosed()
    {
        Debug.Log("YG Rewarded Ad closed");
        if (currentRewardCallback == null)
            return;

        currentRewardCallback.Invoke(false);
        currentRewardCallback = null;
        currentRewardId = null;
        AdClosed?.Invoke();
    }

    public override void ShowInterstitialAd()
    {
        ShowInterstitialAd(null);
    }

    public override void ShowInterstitialAd(Action<bool> onComplete)
    {
        if (!isInitialized)
        {
            Debug.LogWarning("YG Ads not initialized!");
            onComplete?.Invoke(false);
            return;
        }

        YG.YG2.InterstitialAdvShow();
        Debug.Log("YG Interstitial Ad requested");
        AdClosed?.Invoke();
        onComplete?.Invoke(true);
    }

    public void OnDestroy()
    {
        YG.YG2.onRewardAdv -= OnReward;
        YG.YG2.onOpenRewardedAdv -= OnRewardedAdOpened;
        YG.YG2.onErrorRewardedAdv -= OnRewardedAdClosed;
    }
}
#endif
