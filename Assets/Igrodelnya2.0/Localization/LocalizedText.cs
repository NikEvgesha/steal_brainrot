using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Text))]
public class LocalizedText : MonoBehaviour
{
    [SerializeField] private LocalizationData localizationData;
    [SerializeField] private string selectedKey;
    [SerializeField] private string currentLanguage;

    private Text uiText;
    private TextMeshProUGUI tmpText;

    public LocalizationData LocalizationData => localizationData;
    public string SelectedKey { get => selectedKey; set => selectedKey = value; }
    public string CurrentLanguage { get => currentLanguage; set => currentLanguage = value; }

    private void Awake()
    {
        TryGetComponent(out uiText);
        TryGetComponent(out tmpText);
    }
    private void OnEnable()
    {
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged += SetLanguage;
            SetLanguage(LocalizationManager.Instance.CurrentLanguage);
        }
    }

    private void OnDisable()
    {
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged -= SetLanguage;
        }
    }

    public void SetLanguage(string language)
    {
        currentLanguage = language;
        UpdateText();
    }

    private void UpdateText()
    {
        if (localizationData == null || string.IsNullOrEmpty(selectedKey)) return;

        string translatedText = localizationData.GetTranslation(selectedKey, currentLanguage);

        if (uiText != null) uiText.text = translatedText;
        if (tmpText != null) tmpText.text = translatedText;
    }

    private void OnValidate()
    {
        if (localizationData != null)
        {
            if (!localizationData.Languages.Contains(currentLanguage))
            {
                currentLanguage = localizationData.Languages.Count > 0 ? localizationData.Languages[0] : "";
            }
            if (!string.IsNullOrEmpty(currentLanguage))
            {
                UpdateText();
            }
        }
    }

}
