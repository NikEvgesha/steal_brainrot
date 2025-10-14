using System;
using UnityEngine;


/// <summary>
/// Интерфейс для работы с паузой в игре.
/// Позволяет сделать «обёртку» над Time.timeScale/AudioListener.pause
/// или любым другим SDK, реализующим паузу.
/// </summary>
public abstract class PauseProvider : MonoBehaviour
{
    /// <summary> Вызывается один раз при старте. </summary>
    public abstract void Initialize();

    /// <summary> Текущее состояние паузы. </summary>
    public abstract bool IsPaused { get; }

    /// <summary>
    /// Установить паузу (timeScale, звук и т.п.).
    /// </summary>
    /// <param name="paused">Нужно ли ставить паузу?</param>
    /// <param name="controlAudio">Отключать ли AudioListener.pause?</param>
    public abstract void SetPause(bool paused, bool controlAudio = true);

    /// <summary>
    /// Событие: когда пауза изменилась.
    /// Параметр — новое состояние (true = на паузе).
    /// </summary>
    public event Action<bool> OnPauseChanged;

    /// <summary>
    /// Вспомогательный метод для реализации: вызывать внутри провайдера,
    /// когда состояние действительно меняется.
    /// </summary>
    protected void RaisePauseChanged(bool isPaused)
    {
        OnPauseChanged?.Invoke(isPaused);
    }
}
