#if YG_SDK_ENABLED
using System;
using UnityEngine;
using YG;

public class YGLocalizationProvider : LocalizationProvider
{
    public override event Action<string> OnSwitchLang;

    private void OnEnable()
    {
        // Подписываемся на событие YG2
        YG2.onSwitchLang += HandleSwitchLang;
    }

    private void OnDisable()
    {
        YG2.onSwitchLang -= HandleSwitchLang;
    }

    private void HandleSwitchLang(string langCode)
    {
        // При получении события вызываем наш event, чтобы уведомить менеджер локализации
        OnSwitchLang?.Invoke(langCode);
    }

    public override string GetCurrentLanguage()
    {
        // Возвращаем язык из YG2, если он задан
        return YG2.lang;
    }

    public override void SwitchLanguage(string langCode)
    {
        // В YG2 для переключения языка вызываем соответствующий метод
        YG2.SwitchLanguage(langCode);
    }
}
#endif
