using System;
using UnityEngine;

// Интерфейс для рекламных провайдеров
public abstract class AdsProvider : MonoBehaviour
{
    public abstract void Initialize(); // Инициализация провайдера
    public abstract bool IsRewardedAdReady(); // Проверка готовности rewarded-рекламы
    public abstract void ShowRewardedAd(string rewardId, Action<bool> onComplete); // Показ rewarded-рекламы с коллбэком
    public abstract void ShowInterstitialAd(); // Показ interstitial-рекламы

    public Action AdClosed;
}
