using UnityEngine;

/// <summary>
/// Стандартный провайдер паузы на основе Time.timeScale + AudioListener.pause.
/// </summary>
[RequireComponent(typeof(MonoBehaviour))]
public class DefaultPauseProvider : PauseProvider
{
    private bool _isPaused;

    public override bool IsPaused => _isPaused;

    public override void Initialize()
    {
        // Можно сразу поднять начальное состояние, если нужно:
        _isPaused = Time.timeScale <= 0f;
        Debug.Log("DefaultPauseProvider initialized, isPaused=" + _isPaused);
    }

    public override void SetPause(bool paused, bool controlAudio = true)
    {
        if (_isPaused == paused) return;

        _isPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        if (controlAudio)
            AudioListener.pause = paused;

        // Уведомляем подписчиков
        RaisePauseChanged(_isPaused);
        Debug.Log($"DefaultPauseProvider: pause set to {_isPaused}");
    }
}