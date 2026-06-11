using System;
using UnityEngine;

public abstract class AdsProvider : MonoBehaviour
{
    public abstract bool IsInitialized { get; }
    public abstract void Initialize();
    public abstract bool IsRewardedAdReady();
    public virtual bool IsInterstitialAdReady() => IsInitialized;
    public abstract void ShowRewardedAd(string rewardId, Action<bool> onComplete);
    public abstract void ShowInterstitialAd();

    public virtual void ShowInterstitialAd(Action<bool> onComplete)
    {
        ShowInterstitialAd();
        onComplete?.Invoke(true);
    }

    public Action AdClosed;
}
