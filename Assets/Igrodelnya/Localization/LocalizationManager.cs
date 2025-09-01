using UnityEngine;
using System;

public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }

    [SerializeField] private LocalizationData localizationData;
    [SerializeField] private string currentLanguage;
    public LocalizationProvider LocalizationProvider { get; private set; } // Назначаем нужный провайдер в инспекторе

    public event Action<string> OnLanguageChanged;
    public LocalizationData LocalizationData => localizationData;
    public string CurrentLanguage => currentLanguage;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("LocalizationManager уже существует! Удаляем дубликат.");
            Destroy(gameObject);
        }
        LocalizationProvider = GetComponent<LocalizationProvider>();
    }

    private void OnEnable()
    {
        if (LocalizationProvider != null)
        {
            LocalizationProvider.OnSwitchLang += OnSwitchLanguage;

            // Если провайдер вернул язык, используем его
            string providerLang = LocalizationProvider.GetCurrentLanguage();
            if (!string.IsNullOrEmpty(providerLang))
            {
                OnSwitchLanguage(providerLang);
            }
            else if (!string.IsNullOrEmpty(currentLanguage))
            {
                ChangeLanguage(currentLanguage);
            }
            else if (localizationData != null && localizationData.Languages.Count > 0)
            {
                ChangeLanguage(localizationData.Languages[0]); // Язык по умолчанию
            }
        }
        else
        {
            Debug.LogWarning("LocalizationProvider не назначен!");
        }
    }

    private void OnDisable()
    {
        if (LocalizationProvider != null)
        {
            LocalizationProvider.OnSwitchLang -= OnSwitchLanguage;
        }
    }

    public void ChangeLanguage(string newLanguage)
    {
        //Debug.Log($"Язык начал изменяться на: {newLanguage}");
        if (localizationData == null || newLanguage == currentLanguage)
        {
            return;
        }

        if (!localizationData.Languages.Contains(newLanguage))
        {
            newLanguage = "En";
        }

        currentLanguage = newLanguage;

        // Оповещаем подписчиков об изменении языка
        OnLanguageChanged?.Invoke(newLanguage);

        // Обновляем все объекты с локализованным текстом
        foreach (LocalizedText text in FindObjectsOfType<LocalizedText>())
        {
            text.SetLanguage(newLanguage);
        }

        //Debug.Log($"Язык изменен на: {newLanguage}");
    }

    private void OnSwitchLanguage(string langCode)
    {
        if (string.IsNullOrEmpty(langCode))
        {
            Debug.LogWarning("Получен пустой код языка!");
            return;
        }
        //Debug.LogWarning("Получен код языка!" + langCode);
        // Приводим код к нужному формату (например, первая буква в верхнем регистре)
        string formattedLang = char.ToUpper(langCode[0]) + langCode.Substring(1);
        ChangeLanguage(formattedLang);
    }
}
