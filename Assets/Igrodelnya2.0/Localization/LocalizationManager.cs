using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }
    public static event Action<LocalizationManager> OnInstanceReady;

    [SerializeField] private LocalizationData localizationData;
    [SerializeField] private string currentLanguage;

    public LocalizationProvider LocalizationProvider { get; private set; }
    public event Action<string> OnLanguageChanged;

    public LocalizationData LocalizationData => localizationData;
    public string CurrentLanguage => currentLanguage;
    public bool IsLanguageReady => !string.IsNullOrEmpty(currentLanguage);

    private void Awake()
    {
        if (Instance == null)
        {
            // Serialized values are useful for prefab previews only. At runtime the
            // language must come exclusively from the active platform provider.
            currentLanguage = string.Empty;
            Instance = this;
            G.Localization = this;
            LocalizationProvider = GetComponent<LocalizationProvider>();
            DontDestroyOnLoad(gameObject);
            OnInstanceReady?.Invoke(this);
        }
        else
        {
            Debug.LogWarning("LocalizationManager already exists. Destroy duplicate.");
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        // Unity may reload the scripting domain while keeping the persistent
        // GameObject alive. Restore all non-serialized runtime references so the
        // game never falls back to the languages serialized in scene prefabs.
        if (Instance == null)
        {
            Instance = this;
            G.Localization = this;
            LocalizationProvider = GetComponent<LocalizationProvider>();
            DontDestroyOnLoad(gameObject);
            OnInstanceReady?.Invoke(this);
        }
        else if (Instance != this)
        {
            return;
        }
        else if (LocalizationProvider == null)
        {
            LocalizationProvider = GetComponent<LocalizationProvider>();
        }

        SceneManager.sceneLoaded += HandleSceneLoaded;

        if (LocalizationProvider == null)
        {
            Debug.LogWarning("LocalizationProvider is not assigned.");
            return;
        }

        LocalizationProvider.OnSwitchLang += OnSwitchLanguage;

        var providerLang = LocalizationProvider.GetCurrentLanguage();
        if (!string.IsNullOrEmpty(providerLang))
            OnSwitchLanguage(providerLang);

        RefreshLoadedLocalizedTexts();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (LocalizationProvider != null)
            LocalizationProvider.OnSwitchLang -= OnSwitchLanguage;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            if (G.Localization == this)
                G.Localization = null;
        }
    }

    public void ChangeLanguage(string newLanguage)
    {
        if (localizationData == null)
            return;

        if (Application.isPlaying)
        {
            LocalizationProvider?.SwitchLanguage(newLanguage);
            return;
        }

        ApplyProviderLanguage(newLanguage);
    }

    private void ApplyProviderLanguage(string newLanguage)
    {
        if (localizationData == null)
            return;

        var resolvedLanguage = ResolveLanguageName(newLanguage);
        if (string.IsNullOrEmpty(resolvedLanguage))
        {
            if (!string.IsNullOrWhiteSpace(newLanguage))
                Debug.LogWarning($"LocalizationManager: provider language '{newLanguage}' is not configured.");
            return;
        }

        if (string.IsNullOrEmpty(resolvedLanguage) || string.Equals(resolvedLanguage, currentLanguage, StringComparison.Ordinal))
            return;

        currentLanguage = resolvedLanguage;
        OnLanguageChanged?.Invoke(resolvedLanguage);
        RefreshLoadedLocalizedTexts();
    }

    private void OnSwitchLanguage(string langCode)
    {
        ApplyProviderLanguage(langCode);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshLoadedLocalizedTexts();
    }

    private void RefreshLoadedLocalizedTexts()
    {
        if (!IsLanguageReady)
            return;

        var localizedTexts = FindObjectsByType<LocalizedText>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (var i = 0; i < localizedTexts.Length; i++)
            localizedTexts[i].SetLanguage(currentLanguage);
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
