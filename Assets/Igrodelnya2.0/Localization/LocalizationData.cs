using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "LocalizationData", menuName = "Localization/Data")]
public class LocalizationData : ScriptableObject
{
    [SerializeField] private List<string> languages = new List<string>();
    [SerializeField] private List<LocalizationEntry> entries = new List<LocalizationEntry>();
    private Dictionary<string, LocalizationEntry> entryDictionary;

    public List<string> Languages => languages;
    public List<LocalizationEntry> Entries => entries;

    public string GetTranslation(string key)
    {
        return GetTranslation(key, LocalizationManager.Instance.CurrentLanguage);
    }
    public string GetTranslation(string key, string language)
    {
        if (entries == null || languages == null)
        {
            Debug.LogError("LocalizationData: Entries или Languages не инициализированы!");
            return key;
        }

        var entry = entries.Find(e => e.Key == key);
        if (entry == null)
        {
            Debug.LogWarning($"LocalizationData:  люч '{key}' не найден!");
            return key;
        }

        int langIndex = languages.IndexOf(language);
        if (langIndex < 0 || langIndex >= entry.Translations.Count)
        {
            Debug.LogWarning($"LocalizationData: язык '{language}' не найден дл€ ключа '{key}'!");
            return key;
        }

        return entry.Translations[langIndex];
    }

    public string GetTranslation(string key, string language, string tag)
    {
        return GetTranslation(GetTranslation(tag, language) + key, language);
    }


    public void SetData(List<string[]> rawData)
    {
        entries.Clear();
        entryDictionary = new Dictionary<string, LocalizationEntry>();

        for (int i = 1; i < rawData.Count; i++)
        {
            var row = rawData[i];
            if (row.Length < 1) continue;

            LocalizationEntry entry = new LocalizationEntry { Key = row[0].Trim('\"') };

            for (int j = 1; j < row.Length && j - 1 < languages.Count; j++)
            {
                entry.Translations.Add(row[j].Trim('\"'));
            }

            entries.Add(entry);
            entryDictionary[entry.Key] = entry;
        }
    }

}

[System.Serializable]
public class LocalizationEntry
{
    public string Key;
    public List<string> Translations = new List<string>();
}