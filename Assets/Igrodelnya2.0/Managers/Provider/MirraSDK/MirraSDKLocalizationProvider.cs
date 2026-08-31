using System;
using System.Collections.Generic;
using UnityEngine;
using MirraGames.SDK;  // пространство имён MirraSDK

public class MirraSDKLocalizationProvider : LocalizationProvider
{
    // событие при смене языка, передаём код в lowercase, например "en", "ru"
    public override event Action<string> OnSwitchLang;

    // Хранит последний опубликованный код языка.
    private string lastLangCode;
    private bool _isEnabled;

    // словарь для нестандартных кодов ISO
    private static readonly Dictionary<string, string> _exceptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "Portuguese", "pt" },
        { "Chinese",   "zh" },
        // добавляйте прочие исключения здесь
    };

    private void OnEnable()
    {
        _isEnabled = true;

        // Ждём, пока провайдеры локализации не инициализируются
        MirraSDK.WaitForProviders(() =>
        {
            if (!_isEnabled)
                return;

            PublishCurrentLanguage();
        });
    }

    private void PublishCurrentLanguage()
    {
        string current = GetCurrentLanguage();
        if (string.IsNullOrEmpty(current) ||
            string.Equals(lastLangCode, current, StringComparison.OrdinalIgnoreCase))
            return;

        lastLangCode = current;
        OnSwitchLang?.Invoke(current);
    }
    /// <summary>
    /// Возвращаем код текущего языка: двухбуквенный ISO или из словаря исключений
    /// </summary>
    public override string GetCurrentLanguage()
    {
        // До готовности SDK не подставляем искусственный язык. Менеджер может
        // показать свой prefab fallback, а после WaitForProviders получит
        // настоящий язык платформы через OnSwitchLang.
        if (!MirraSDK.IsInitialized)
            return string.Empty;

        string name = MirraSDK.Language.Current.ToString();

        //Debug.Log(name + " = Берем значение языка из СДК ");
        if (_exceptions.TryGetValue(name, out var code))
        {

            //Debug.Log("Необычный код языка - " + code);
            return code;
        }
        // стандартное правило: первые две буквы
        code = name.Length >= 2
            ? name.Substring(0, 2).ToLowerInvariant()
            : name.ToLowerInvariant();

        //Debug.Log(code + " = Сокращенное значение из СДК ");
        return code;
    }

    /// <summary>
    /// Переключаем язык по коду (например "en", "ru", "ja").
    /// </summary>
    public override void SwitchLanguage(string langCode)
    {
        // Язык определяется площадкой через Mirra SDK. Ручной выбор в игровом
        // интерфейсе намеренно не поддерживается.
        PublishCurrentLanguage();
    }

    private void OnDisable()
    {
        _isEnabled = false;
        // очищаем, чтобы не было утечек
        lastLangCode = null;
    }
}
