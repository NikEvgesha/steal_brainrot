using System;
using UnityEngine;

public class LocalizationManager : MonoBehaviour
{
    private const string DefaultLanguage = "En";

    public static LocalizationManager Instance { get; private set; }
    public static event Action<LocalizationManager> OnInstanceReady;

    [SerializeField] private LocalizationData localizationData;
    [SerializeField] private string currentLanguage;

    public LocalizationProvider LocalizationProvider { get; private set; }
    public event Action<string> OnLanguageChanged;

    public LocalizationData LocalizationData => localizationData;
    public string CurrentLanguage => currentLanguage;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            OnInstanceReady?.Invoke(this);
        }
        else
        {
            Debug.LogWarning("LocalizationManager already exists. Destroy duplicate.");
            Destroy(gameObject);
            return;
        }

        LocalizationProvider = GetComponent<LocalizationProvider>();
    }

    private void OnEnable()
    {
        if (LocalizationProvider == null)
        {
            Debug.LogWarning("LocalizationProvider is not assigned.");
            ApplyFallbackLanguage();
            return;
        }

        LocalizationProvider.OnSwitchLang += OnSwitchLanguage;

        var providerLang = LocalizationProvider.GetCurrentLanguage();
        if (!string.IsNullOrEmpty(providerLang))
        {
            OnSwitchLanguage(providerLang);
            return;
        }

        ApplyFallbackLanguage();
    }

    private void OnDisable()
    {
        if (LocalizationProvider != null)
            LocalizationProvider.OnSwitchLang -= OnSwitchLanguage;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ChangeLanguage(string newLanguage)
    {
        if (localizationData == null)
            return;

        var resolvedLanguage = ResolveLanguageName(newLanguage);
        if (string.IsNullOrEmpty(resolvedLanguage))
            resolvedLanguage = ResolveFallbackLanguage();

        if (string.IsNullOrEmpty(resolvedLanguage) || string.Equals(resolvedLanguage, currentLanguage, StringComparison.Ordinal))
            return;

        currentLanguage = resolvedLanguage;
        OnLanguageChanged?.Invoke(resolvedLanguage);
    }

    private void OnSwitchLanguage(string langCode)
    {
        ChangeLanguage(langCode);
    }

    private void ApplyFallbackLanguage()
    {
        ChangeLanguage(currentLanguage);
    }

    private string ResolveFallbackLanguage()
    {
        if (localizationData == null || localizationData.Languages == null || localizationData.Languages.Count == 0)
            return string.Empty;

        var byDefault = ResolveLanguageName(DefaultLanguage);
        if (!string.IsNullOrEmpty(byDefault))
            return byDefault;

        return localizationData.Languages[0];
    }

    private string ResolveLanguageName(string candidate)
    {
        if (localizationData == null || localizationData.Languages == null || localizationData.Languages.Count == 0)
            return string.Empty;

        if (string.IsNullOrWhiteSpace(candidate))
            return string.Empty;

        var normalized = NormalizeLanguageCode(candidate);
        if (string.IsNullOrEmpty(normalized))
            return string.Empty;

        var languages = localizationData.Languages;
        for (var i = 0; i < languages.Count; i++)
        {
            var lang = languages[i];
            if (string.Equals(lang, normalized, StringComparison.OrdinalIgnoreCase))
                return lang;
        }

        var normalizedBase = ExtractLanguageBase(normalized);
        if (string.IsNullOrEmpty(normalizedBase))
            return string.Empty;

        for (var i = 0; i < languages.Count; i++)
        {
            var lang = languages[i];
            if (string.Equals(ExtractLanguageBase(lang), normalizedBase, StringComparison.OrdinalIgnoreCase))
                return lang;
        }

        return string.Empty;
    }

    private static string NormalizeLanguageCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim().Replace('_', '-');
        if (trimmed.Length == 2)
            return char.ToUpperInvariant(trimmed[0]) + trimmed.Substring(1).ToLowerInvariant();

        return trimmed;
    }

    private static string ExtractLanguageBase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().Replace('_', '-');
        var split = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries);
        var token = split.Length > 0 ? split[0] : normalized;
        if (token.Length >= 2 && char.IsLetter(token[0]) && char.IsLetter(token[1]))
            return token.Substring(0, 2).ToLowerInvariant();

        return token.ToLowerInvariant();
    }
}
