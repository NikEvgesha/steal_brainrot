using UnityEngine;
using MirraGames.SDK;  // пространство имён MirraSDK

//#if MIRRA_SDK_ENABLED
public class MirraSDKPauseProvider : PauseProvider
{
    private bool _isPaused;

    public override bool IsPaused => _isPaused;

    public override void Initialize()
    {
        MirraSDK.WaitForProviders(() => { });
    }
    public override void SetPause(bool paused, bool controlAudio = true)
    {
        if (_isPaused == paused) return;

        _isPaused = paused;

        // Управляем временем через MirraSDK.Time.Scale
        MirraSDK.Time.Scale = paused ? 0f : 1f;  // :contentReference[oaicite:0]{index=0}

        if (controlAudio)
        {
            // Управляем звуком через MirraSDK.Audio.Pause
            MirraSDK.Audio.Pause = paused;         // :contentReference[oaicite:1]{index=1}
        }

        // Оповещаем подписчиков об изменении паузы
        RaisePauseChanged(_isPaused);
        if (_isPaused)
            AnalyticsManager.Instance.MarkGameplayStopped("game_pause");
        else 
            AnalyticsManager.Instance.MarkGameplayStarted("game_pause");


        Debug.Log($"MirraSDKPauseProvider: pause set to {_isPaused}");
    }
}
//#endif
