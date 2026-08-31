using System;
using MirraGames.SDK;
using UnityEngine;

public class MirraSDKAdsProvider : AdsProvider
{
    private bool isInitialized;
    private bool _rewardedReadyErrorReported;
    private bool _interstitialReadyErrorReported;

    public override bool IsInitialized => isInitialized;

    public override void Initialize()
    {
        MirraSDK.WaitForProviders(() =>
        {
            isInitialized = true;
        });
    }

    public override bool IsRewardedAdReady()
    {
        if (!isInitialized)
            return false;

        try
        {
            var ads = MirraSDK.Ads;
            return ads.IsRewardedAvailable
                && ads.IsRewardedReady
                && !ads.IsRewardedVisible
                && !ads.IsInterstitialVisible;
        }
        catch (Exception exception)
        {
            if (!_rewardedReadyErrorReported)
            {
                _rewardedReadyErrorReported = true;
                Debug.LogWarning($"MirraSDK: Rewarded readiness check failed; waiting for SDK. {exception.Message}");
            }

            return false;
        }
    }

    public override bool IsInterstitialAdReady()
    {
        if (!isInitialized)
            return false;

        try
        {
            var ads = MirraSDK.Ads;
            return ads.IsInterstitialAvailable
                && ads.IsInterstitialReady
                && !ads.IsInterstitialVisible
                && !ads.IsRewardedVisible;
        }
        catch (Exception exception)
        {
            if (!_interstitialReadyErrorReported)
            {
                _interstitialReadyErrorReported = true;
                Debug.LogWarning($"MirraSDK: Interstitial readiness check failed; waiting for SDK. {exception.Message}");
            }

            return false;
        }
    }

    public override void ShowRewardedAd(string rewardId, Action<bool> onComplete)
    {
        if (!IsRewardedAdReady())
        {
            Debug.LogWarning("MirraSDK: Rewarded ad not ready");
            onComplete?.Invoke(false);
            return;
        }

        MirraSDK.Ads.InvokeRewarded(
            onOpen: () => { },
            rewardTag: rewardId,
            onClose: success =>
            {
                AdClosed?.Invoke();
                onComplete?.Invoke(success);
            }
        );
    }

    public override void ShowInterstitialAd()
    {
        ShowInterstitialAd(null);
    }

    public override void ShowInterstitialAd(Action<bool> onComplete)
    {
        if (!IsInterstitialAdReady())
        {
            Debug.LogWarning("MirraSDK: Interstitial ad not ready");
            onComplete?.Invoke(false);
            return;
        }

        bool wasOpened = false;
        MirraSDK.Ads.InvokeInterstitial(
            onOpen: () =>
            {
                wasOpened = true;
                Debug.Log("MirraSDK: Interstitial ad opened");
            },
            onClose: success =>
            {
                // Some WebGL platform adapters report false/undefined on a normal close even
                // after the fullscreen ad has actually opened. For an automatic interstitial,
                // opening is enough to fulfil the promised countdown reward. A request that
                // never opened still remains a failure and grants nothing.
                bool completed = success || wasOpened;
                if (wasOpened && !success)
                    Debug.LogWarning("MirraSDK: Interstitial closed after opening but the platform returned success=false; treating it as completed.");
                else
                    Debug.Log($"MirraSDK: Interstitial ad closed (success: {success}, opened: {wasOpened})");

                AdClosed?.Invoke();
                onComplete?.Invoke(completed);
            }
        );
    }
}
