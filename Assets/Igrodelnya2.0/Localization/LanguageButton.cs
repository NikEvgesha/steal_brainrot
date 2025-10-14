using UnityEngine;

public class LanguageButton : MonoBehaviour
{
    [SerializeField] private string language;
    // Можно также назначить провайдер, если он не является синглтоном
    [SerializeField] private LocalizationProvider localizationProvider;

    public string Language { get => language; set => language = value; }

    private void Start()
    {
        localizationProvider = LocalizationManager.Instance.LocalizationProvider;
    }
    public void OnButtonClick()
    {
        if (string.IsNullOrEmpty(language))
        {
            Debug.LogWarning("Переменная языка не задана!");
            return;
        }

        if (localizationProvider != null)
        {
            // Приводим код языка к нужному формату (например, все буквы в нижнем регистре)
            string formattedLang = char.ToLowerInvariant(language[0]) + language.Substring(1);
            localizationProvider.SwitchLanguage(formattedLang);
            Debug.LogFormat("Выбранный язык: {0}", language);
        }
        else
        {
            Debug.LogWarning("LocalizationProvider не назначен на кнопке!");
        }
    }
}
