using System;
using System.Collections.Generic;
using UnityEngine;
using MirraGames.SDK;  // пространство имён MirraSDK
using MirraGames.SDK.Common;

public class MirraSDKLocalizationProvider : LocalizationProvider
{
    // событие при смене языка, передаём код в lowercase, например "en", "ru"
    public override event Action<string> OnSwitchLang;

    // хранит последний известный код языка
    private string lastLangCode = null;
    private string _langCode = null;

    // словарь для нестандартных кодов ISO
    private static readonly Dictionary<string, string> _exceptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "Portuguese", "pt" },
        { "Chinese",   "zh" },
        // добавляйте прочие исключения здесь
    };

    private void OnEnable()
    {
        // Ждём, пока провайдеры локализации не инициализируются
        MirraSDK.WaitForProviders(() =>
        {
            // устанавливаем начальное значение
            lastLangCode = GetCurrentLanguage();
        });
    }
    private void CheckLeng()
    {
        if (!MirraSDK.IsInitialized) return;
        // получаем текущий код языка
        string current = _langCode;
        Debug.Log("Был язык: " + lastLangCode + " Новый язык: " + current);
        // если изменилось — уведомляем подписчиков
        if (lastLangCode != null && current != lastLangCode)
        {

            Debug.Log("Изменяем язык на " + current);
            lastLangCode = current;
            OnSwitchLang?.Invoke(current);
        }

    }
    /// <summary>
    /// Возвращаем код текущего языка: двухбуквенный ISO или из словаря исключений
    /// </summary>
    public override string GetCurrentLanguage()
    {
        if (!MirraSDK.IsInitialized) return "en";
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
        //Debug.Log("Проверка инициализации СДК - " + MirraSDK.IsInitialized);
        if (!MirraSDK.IsInitialized) return;
        // приводим к нижнему регистру
        langCode = langCode.ToLowerInvariant();

        // перебираем все значения enum LanguageType
        foreach (LanguageType candidate in Enum.GetValues(typeof(LanguageType)))
        {
            string candidateName = candidate.ToString().ToLowerInvariant();
            if (candidateName.StartsWith(langCode))
            {
                _langCode = langCode.ToLowerInvariant();
                //MirraSDK.Language.Current = candidate;
                //Debug.Log("Усталавливаем MirraSDK.Language.Current = " + candidate);
                CheckLeng();
                return;
            }
        }

        Debug.LogWarning($"MirraSDKLocalizationProvider: unsupported language code '{langCode}'");
    }

    private void OnDisable()
    {
        // очищаем, чтобы не было утечек
        lastLangCode = null;
    }
}