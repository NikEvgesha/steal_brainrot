using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LocalizationData", menuName = "Localization/Data")]
public class LocalizationData : ScriptableObject
{
    private const string DefaultLanguage = "En";

    [SerializeField] private List<string> languages = new List<string>();
    [SerializeField] private List<LocalizationEntry> entries = new List<LocalizationEntry>();

    private readonly Dictionary<string, LocalizationEntry> entryDictionary = new Dictionary<string, LocalizationEntry>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> keyByTranslation = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly HashSet<string> ambiguousTranslations = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> missingKeyWarnings = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> missingLanguageWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> emptyTranslationWarnings = new HashSet<string>(StringComparer.Ordinal);
    private bool cacheDirty = true;

    public List<string> Languages => languages;
    public List<LocalizationEntry> Entries => entries;

    private void OnEnable()
    {
        cacheDirty = true;
    }

    private void OnValidate()
    {
        cacheDirty = true;
    }

    public string GetTranslation(string key)
    {
        var manager = LocalizationManager.Instance;
        if (Application.isPlaying && manager != null && !manager.IsLanguageReady)
            return string.Empty;

        var language = manager != null ? manager.CurrentLanguage : (languages.Count > 0 ? languages[0] : string.Empty);
        return GetTranslation(key, language);
    }

    public string GetTranslation(string key, string language)
    {
        if (entries == null || languages == null)
        {
            Debug.LogError("LocalizationData: entries or languages are not initialized.");
            return key;
        }

        EnsureCache();
        key = NormalizeCell(key);
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        if (!entryDictionary.TryGetValue(key, out var entry) || entry == null)
            entry = FindEntryByKeySlow(key);

        if (entry == null)
        {
            if (missingKeyWarnings.Add(key))
                Debug.LogWarning($"LocalizationData: key '{key}' not found.");
            return key;
        }

        var requestedIndex = FindLanguageIndex(language);
        if (requestedIndex < 0)
        {
            var languageWarningKey = string.IsNullOrEmpty(language) ? "<empty>" : language;
            if (missingLanguageWarnings.Add(languageWarningKey))
                Debug.LogWarning($"LocalizationData: language '{language}' was not resolved.");
        }

        if (TryGetEntryTranslation(entry, requestedIndex, out var resolved))
            return resolved;

        var fallbackIndex = FindLanguageIndex(DefaultLanguage);
        if (TryGetEntryTranslation(entry, fallbackIndex, out resolved))
            return resolved;

        if (TryGetAnyEntryTranslation(entry, out resolved))
            return resolved;

        var emptyKey = $"{key}|{language}";
        if (emptyTranslationWarnings.Add(emptyKey))
            Debug.LogWarning($"LocalizationData: key '{key}' has no non-empty translations.");

        return key;
    }

    public bool TryGetTranslation(string key, string language, out string value)
    {
        value = null;
        EnsureCache();

        key = NormalizeCell(key);
        if (string.IsNullOrEmpty(key))
            return false;

        if (!entryDictionary.TryGetValue(key, out var entry) || entry == null)
            entry = FindEntryByKeySlow(key);

        if (entry == null)
            return false;

        var langIndex = FindLanguageIndex(language);
        if (langIndex < 0 || langIndex >= entry.Translations.Count)
            return false;

        value = entry.Translations[langIndex];
        return true;
    }

    public bool TryFindKeyByTranslation(string translation, out string key)
    {
        key = null;
        EnsureCache();

        var normalized = NormalizeCell(translation);
        if (string.IsNullOrEmpty(normalized))
            return false;

        if (ambiguousTranslations.Contains(normalized))
            return false;

        return keyByTranslation.TryGetValue(normalized, out key);
    }

    public string GetTranslation(string key, string language, string tag)
    {
        return GetTranslation(GetTranslation(tag, language) + key, language);
    }

    public void SetData(List<string[]> rawData)
    {
        if (rawData == null || rawData.Count == 0)
        {
            Debug.LogWarning("LocalizationData: raw data is empty, skip update.");
            return;
        }

        var header = rawData[0];
        if (header == null || header.Length == 0)
        {
            Debug.LogWarning("LocalizationData: header row is missing.");
            return;
        }

        var keyColumn = FindKeyColumn(header);
        var languageColumns = FindLanguageColumns(header, keyColumn);
        if (languageColumns.Count == 0)
        {
            var startCol = Mathf.Clamp(keyColumn + 1, 1, header.Length);
            for (var i = 0; i < languages.Count && startCol + i < header.Length; i++)
                languageColumns.Add((startCol + i, languages[i]));
        }

        if (languageColumns.Count == 0)
        {
            Debug.LogWarning("LocalizationData: language columns were not detected.");
            return;
        }

        languages.Clear();
        foreach (var (_, languageName) in languageColumns)
            languages.Add(languageName);

        entries.Clear();
        var duplicateKeys = 0;
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 1; i < rawData.Count; i++)
        {
            var row = rawData[i];
            if (row == null || row.Length == 0)
                continue;

            var rawKey = keyColumn < row.Length ? row[keyColumn] : string.Empty;
            var key = NormalizeCell(rawKey).Trim('"');
            if (string.IsNullOrEmpty(key))
                continue;

            if (!seenKeys.Add(key))
            {
                duplicateKeys++;
                continue;
            }

            var entry = new LocalizationEntry { Key = key };
            for (var j = 0; j < languageColumns.Count; j++)
            {
                var col = languageColumns[j].col;
                var value = col < row.Length ? row[col] : string.Empty;
                entry.Translations.Add(NormalizeCell(value).Trim('"'));
            }

            entries.Add(entry);
        }

        if (duplicateKeys > 0)
            Debug.LogWarning($"LocalizationData: skipped duplicate keys: {duplicateKeys}.");

        cacheDirty = true;
        EnsureCache();
    }

    private void EnsureCache()
    {
        // Domain-reload/editor timing can leave non-serialized caches empty while cacheDirty is false.
        // Rebuild when dictionary is empty to avoid false "key not found" warnings.
        if (!cacheDirty && entryDictionary.Count > 0)
            return;

        entryDictionary.Clear();
        keyByTranslation.Clear();
        ambiguousTranslations.Clear();
        missingKeyWarnings.Clear();
        missingLanguageWarnings.Clear();
        emptyTranslationWarnings.Clear();

        if (entries != null)
        {
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                    continue;

                entryDictionary[entry.Key] = entry;
                if (entry.Translations == null)
                    continue;

                foreach (var rawTranslation in entry.Translations)
                {
                    var translation = NormalizeCell(rawTranslation);
                    if (string.IsNullOrEmpty(translation))
                        continue;

                    if (ambiguousTranslations.Contains(translation))
                        continue;

                    if (keyByTranslation.TryGetValue(translation, out var existing) && existing != entry.Key)
                    {
                        keyByTranslation.Remove(translation);
                        ambiguousTranslations.Add(translation);
                        continue;
                    }

                    keyByTranslation[translation] = entry.Key;
                }
            }
        }

        cacheDirty = false;
    }

    private static int FindKeyColumn(IReadOnlyList<string> header)
    {
        for (var i = 0; i < header.Count; i++)
        {
            var value = NormalizeHeader(header[i]);
            if (value == "key" || value == "id" || value == "loc_key" || value == "localization_key")
                return i;
        }

        return 0;
    }

    private static List<(int col, string languageName)> FindLanguageColumns(IReadOnlyList<string> header, int keyColumn)
    {
        var result = new List<(int col, string languageName)>();
        for (var i = 0; i < header.Count; i++)
        {
            if (i == keyColumn)
                continue;

            var normalized = NormalizeHeader(header[i]);
            if (string.IsNullOrEmpty(normalized) || IsMetaColumn(normalized))
                continue;

            result.Add((i, FormatLanguageName(header[i])));
        }

        return result;
    }

    private static bool IsMetaColumn(string name)
    {
        return name == "comment"
            || name == "comments"
            || name == "note"
            || name == "notes"
            || name == "context"
            || name == "tag"
            || name == "group"
            || name == "section"
            || name == "type";
    }

    private static string FormatLanguageName(string value)
    {
        var trimmed = NormalizeCell(value);
        if (string.IsNullOrEmpty(trimmed))
            return string.Empty;

        if (trimmed.Length == 1)
            return trimmed.ToUpperInvariant();

        return char.ToUpperInvariant(trimmed[0]) + trimmed.Substring(1).ToLowerInvariant();
    }

    private static string NormalizeHeader(string value)
    {
        return NormalizeCell(value).ToLowerInvariant();
    }

    private static string NormalizeCell(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
    }

    private LocalizationEntry FindEntryByKeySlow(string key)
    {
        if (entries == null || string.IsNullOrEmpty(key))
            return null;

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null)
                continue;

            if (!string.Equals(NormalizeCell(entry.Key), key, StringComparison.Ordinal))
                continue;

            entryDictionary[key] = entry;
            return entry;
        }

        return null;
    }

    private int FindLanguageIndex(string language)
    {
        if (languages == null || languages.Count == 0)
            return -1;

        if (!string.IsNullOrWhiteSpace(language))
        {
            for (var i = 0; i < languages.Count; i++)
            {
                if (string.Equals(languages[i], language, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
        }

        var requestedBase = ExtractLanguageBase(language);
        if (string.IsNullOrEmpty(requestedBase))
            return -1;

        for (var i = 0; i < languages.Count; i++)
        {
            var languageBase = ExtractLanguageBase(languages[i]);
            if (string.Equals(languageBase, requestedBase, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static string ExtractLanguageBase(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return string.Empty;

        var normalized = language.Trim().Replace('_', '-');
        var split = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries);
        var token = split.Length > 0 ? split[0] : normalized;
        if (token.Length >= 2 && char.IsLetter(token[0]) && char.IsLetter(token[1]))
            return token.Substring(0, 2).ToLowerInvariant();

        return token.ToLowerInvariant();
    }

    private static bool TryGetEntryTranslation(LocalizationEntry entry, int langIndex, out string value)
    {
        value = null;
        if (entry == null || entry.Translations == null)
            return false;

        if (langIndex < 0 || langIndex >= entry.Translations.Count)
            return false;

        value = NormalizeCell(entry.Translations[langIndex]);
        return !string.IsNullOrEmpty(value);
    }

    private static bool TryGetAnyEntryTranslation(LocalizationEntry entry, out string value)
    {
        value = null;
        if (entry == null || entry.Translations == null)
            return false;

        foreach (var translation in entry.Translations)
        {
            var normalized = NormalizeCell(translation);
            if (string.IsNullOrEmpty(normalized))
                continue;

            value = normalized;
            return true;
        }

        return false;
    }
}

[Serializable]
public class LocalizationEntry
{
    public string Key;
    public List<string> Translations = new List<string>();
}
