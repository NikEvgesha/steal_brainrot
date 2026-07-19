using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class LocalizedText : MonoBehaviour
{
    [SerializeField] private LocalizationData localizationData;
    [SerializeField] private string selectedKey;
    [SerializeField] private string currentLanguage;

    private TMP_Text tmpText;
    private LocalizationManager subscribedManager;

    public LocalizationData LocalizationData => localizationData;
    public string SelectedKey { get => selectedKey; set => selectedKey = value; }
    public string CurrentLanguage { get => currentLanguage; set => currentLanguage = value; }

    private void Awake()
    {
        TryGetComponent(out tmpText);
    }

    private void OnEnable()
    {
        LocalizationManager.OnInstanceReady += HandleManagerReady;
        SubscribeToManager(LocalizationManager.Instance);

        UpdateText();
    }

    private void OnDisable()
    {
        LocalizationManager.OnInstanceReady -= HandleManagerReady;
        UnsubscribeFromManager();
    }

    public void Configure(LocalizationData data, string key, string language)
    {
        localizationData = data;
        selectedKey = key;
        currentLanguage = language;
        UpdateText();
    }

    public void SetLanguage(string language)
    {
        currentLanguage = language;
        UpdateText();
    }

    private void UpdateText()
    {
        if (localizationData == null || string.IsNullOrEmpty(selectedKey))
            return;

        if (string.IsNullOrEmpty(currentLanguage))
        {
            var manager = LocalizationManager.Instance;
            if (manager != null && !string.IsNullOrEmpty(manager.CurrentLanguage))
                currentLanguage = manager.CurrentLanguage;
            else if (localizationData.Languages.Count > 0)
                currentLanguage = localizationData.Languages[0];
        }

        var translatedText = localizationData.GetTranslation(selectedKey, currentLanguage);
        if (string.IsNullOrEmpty(translatedText))
            translatedText = selectedKey;

        if (tmpText != null)
            tmpText.text = translatedText;
    }

    private void OnValidate()
    {
        if (localizationData == null)
            return;

        if (!ContainsLanguage(localizationData, currentLanguage))
            currentLanguage = localizationData.Languages.Count > 0 ? localizationData.Languages[0] : string.Empty;

        if (!string.IsNullOrEmpty(currentLanguage))
            UpdateText();
    }

    private void HandleManagerReady(LocalizationManager manager)
    {
        SubscribeToManager(manager);
    }

    private void SubscribeToManager(LocalizationManager manager)
    {
        if (manager == null || subscribedManager == manager)
            return;

        UnsubscribeFromManager();
        subscribedManager = manager;
        subscribedManager.OnLanguageChanged += SetLanguage;

        if (!string.IsNullOrEmpty(subscribedManager.CurrentLanguage))
            SetLanguage(subscribedManager.CurrentLanguage);
    }

    private void UnsubscribeFromManager()
    {
        if (subscribedManager == null)
            return;

        subscribedManager.OnLanguageChanged -= SetLanguage;
        subscribedManager = null;
    }

    private static bool ContainsLanguage(LocalizationData data, string language)
    {
        if (data == null || data.Languages == null || data.Languages.Count == 0 || string.IsNullOrWhiteSpace(language))
            return false;

        for (var i = 0; i < data.Languages.Count; i++)
        {
            if (string.Equals(data.Languages[i], language, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
