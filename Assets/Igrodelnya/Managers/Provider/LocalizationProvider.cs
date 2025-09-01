using System;
using UnityEngine;

public abstract class LocalizationProvider : MonoBehaviour
{
    /// <summary>
    /// Получить текущий язык (например, из внешнего сервиса или сохранённых настроек).
    /// </summary>
    public abstract string GetCurrentLanguage();

    /// <summary>
    /// Переключить язык. langCode – код языка (например, "en", "ru").
    /// </summary>
    public abstract void SwitchLanguage(string langCode);

    /// <summary>
    /// Событие, вызываемое при переключении языка.
    /// </summary>
    public abstract event Action<string> OnSwitchLang;
}
