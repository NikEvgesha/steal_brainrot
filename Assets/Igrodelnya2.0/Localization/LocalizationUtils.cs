using System;
using System.Collections.Generic;
using UnityEngine;

public static class LocalizationUtils
{
    private static readonly HashSet<string> MissingKeys = new HashSet<string>();
    private static LocalizationData fallbackData;
    private static string fallbackLanguage;

    public static event Action<string> OnFallbackLanguageChanged;

    public static void ConfigureFallback(LocalizationData data)
    {
        if (data != null)
            fallbackData = data;
    }

    public static void SetFallbackLanguage(string language)
    {
        fallbackLanguage = language;
        OnFallbackLanguageChanged?.Invoke(language);
    }

    public static string T(string key, string fallback = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            return fallback ?? string.Empty;

        var manager = LocalizationManager.Instance;
        var data = manager != null && manager.LocalizationData != null ? manager.LocalizationData : fallbackData;
        if (data == null)
            return fallback ?? key;

        var language = manager != null ? manager.CurrentLanguage : fallbackLanguage;
        if (string.IsNullOrWhiteSpace(language) && data.Languages.Count > 0)
            language = data.Languages[0];

        var translated = data.GetTranslation(key, language);
        if (!string.IsNullOrWhiteSpace(translated) && translated != key)
            return translated;

        if (!string.IsNullOrEmpty(fallback))
        {
            var currentLanguage = manager != null ? manager.CurrentLanguage : fallbackLanguage;
            var warningKey = key + "|" + currentLanguage;
            if (MissingKeys.Add(warningKey))
                Debug.LogWarning($"[Localization] Missing key '{key}', using fallback '{fallback}'.");
            return fallback;
        }

        return string.IsNullOrWhiteSpace(translated) ? key : translated;
    }

    public static string Format(string key, string fallback, params object[] args)
    {
        var format = T(key, fallback);
        if (args == null || args.Length == 0)
            return format;

        return string.Format(format, args);
    }
}
