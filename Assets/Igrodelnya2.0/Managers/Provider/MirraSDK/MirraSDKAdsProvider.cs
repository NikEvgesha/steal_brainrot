using System;
using MirraGames.SDK;
using UnityEngine;

public class MirraSDKAdsProvider : AdsProvider
{
    private bool isInitialized;

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
        return isInitialized && MirraSDK.Ads.IsRewardedReady;
    }

    public override bool IsInterstitialAdReady()
    {
        return isInitialized && MirraSDK.Ads.IsInterstitialReady;
    }

    public override void ShowRewardedAd(string rewardId, Action<bool> onComplete)
    {
        if (!IsRewardedAdReady())
        {
            Debug.LogWarning("MirraSDK: Rewarded ad not ready");
            onComplete?.Invoke(false);
            return;
        }

        if (G.Control != null)
            G.Control.CursorActive = true;
        PauseManager.Instance?.SetPause(true);

        MirraSDK.Ads.InvokeRewarded(
            onOpen: () => { },
            rewardTag: rewardId,
            onClose: success =>
            {
                PauseManager.Instance?.SetPause(false);
                if (G.Control != null)
                    G.Control.CursorActive = false;

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

        MirraSDK.Ads.InvokeInterstitial(
            onOpen: () =>
            {
                PauseManager.Instance?.SetPause(true);
                if (G.Control != null)
                    G.Control.CursorActive = true;
                Debug.Log("MirraSDK: Interstitial ad opened");
            },
            onClose: success =>
            {
                Debug.Log("MirraSDK: Interstitial ad closed");
                PauseManager.Instance?.SetPause(false);
                if (G.Control != null)
                    G.Control.CursorActive = false;

                AdClosed?.Invoke();
                onComplete?.Invoke(success);
            }
        );
    }
}
