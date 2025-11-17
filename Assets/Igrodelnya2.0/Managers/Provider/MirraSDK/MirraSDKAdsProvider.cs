
//#if MIRRA_SDK_ENABLED
using System;
using UnityEngine;
using MirraGames.SDK;  // добавили пространство имён SDK

public class MirraSDKAdsProvider : AdsProvider
{
    private bool isInitialized;
    public override void Initialize()
    {
        MirraSDK.WaitForProviders(() =>
        {
            // устанавливаем начальное значение
            isInitialized=true;
            // В MirraSDK нет явной инициализации Ads-модуля,
            // но логируем факт подключения провайдера
            //Debug.Log("MirraSDKAdsProvider initialized");
        });
    }

    public override bool IsRewardedAdReady()
    {
        if (!isInitialized) return false;
        return MirraSDK.Ads.IsRewardedReady;
    }

    public override void ShowRewardedAd(string rewardId, Action<bool> onComplete)
    {
        if (!MirraSDK.Ads.IsRewardedReady)
        {
            Debug.LogWarning("MirraSDK: Rewarded ad not ready");
            onComplete?.Invoke(false);
            return;
        }
        G.Control.CursorActive = true;
        PauseManager.Instance?.SetPause(true);
        MirraSDK.Ads.InvokeRewarded(
            onOpen: () =>
            {
            },
            rewardTag: rewardId,
            onClose: (success) =>
            {
                PauseManager.Instance?.SetPause(false);
                G.Control.CursorActive = false;
                onComplete?.Invoke(success);
            }
        );
    }

    public override void ShowInterstitialAd()
    {
        if (!MirraSDK.Ads.IsInterstitialReady)
        {
            Debug.LogWarning("MirraSDK: Interstitial ad not ready");
            return;
        }


        //PauseManager.Instance.SetPause(true, true);
        // Правильные имена параметров: onOpen и onClose
        MirraSDK.Ads.InvokeInterstitial(
            onOpen: () =>
            {
                PauseManager.Instance?.SetPause(true);
                G.Control.CursorActive = true;
                Debug.Log("MirraSDK: Interstitial ad opened");
                //G.Control.CursorActive = true;
            },
            onClose: (success) =>
            {
                Debug.Log("MirraSDK: Interstitial ad closed");
                //AdClosed?.Invoke();
                PauseManager.Instance?.SetPause(false);
                G.Control.CursorActive = false;
                //PauseManager.Instance.SetPause(false, true);
                //G.Control.CursorActive = false;
            }
        );
    }

    private void OnDestroy()
    {
        // Ничего не подписывали — нечего и очищать
    }
}
//#endif
