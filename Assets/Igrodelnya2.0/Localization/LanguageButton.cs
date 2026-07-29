using UnityEngine;

public class LanguageButton : MonoBehaviour
{
    [SerializeField] private string language;
    [SerializeField] private LocalizationProvider localizationProvider;

    public string Language { get => language; set => language = value; }

    public void OnButtonClick()
    {
        if (string.IsNullOrEmpty(language))
        {
            Debug.LogWarning("Language is not configured for this button.");
            return;
        }

        var manager = LocalizationManager.Instance;
        var provider = localizationProvider != null
            ? localizationProvider
            : manager != null ? manager.LocalizationProvider : null;

        if (provider != null)
        {
            var formattedLanguage = char.ToLowerInvariant(language[0]) + language.Substring(1);
            provider.SwitchLanguage(formattedLanguage);
        }

        if (manager != null)
        {
            string previousLanguage = manager.CurrentLanguage;
            manager.ChangeLanguage(language);
            Debug.LogFormat("Selected language: {0}", manager.CurrentLanguage);
            if (!string.Equals(previousLanguage, manager.CurrentLanguage, System.StringComparison.OrdinalIgnoreCase))
            {
                GameAnalytics.Track(AnalyticsEventNames.SettingsChanged, GameAnalytics.Params(
                    "setting_name", "language",
                    "value_before", previousLanguage,
                    "value_after", manager.CurrentLanguage,
                    "source", "language_button",
                    "result", "changed"));
            }
            return;
        }

        ApplyEditorFallback(language);
    }

    private static void ApplyEditorFallback(string selectedLanguage)
    {
        var localizedTexts = FindObjectsByType<LocalizedText>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        LocalizationData data = null;
        for (var i = 0; i < localizedTexts.Length; i++)
        {
            var localizedText = localizedTexts[i];
            if (localizedText == null)
                continue;

            if (data == null && localizedText.LocalizationData != null)
                data = localizedText.LocalizationData;

            localizedText.SetLanguage(selectedLanguage);
        }

        LocalizationUtils.ConfigureFallback(data);
        LocalizationUtils.SetFallbackLanguage(selectedLanguage);
        Debug.LogWarning("LocalizationManager is missing. Applied language to the current scene for editor testing.");
    }
}
