using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(LocalizedText))]
public class LocalizedTextEditor : Editor
{
    public override void OnInspectorGUI()
    {
        LocalizedText localizedText = (LocalizedText)target;

        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("localizationData"));

        if (localizedText.LocalizationData == null || localizedText.LocalizationData.Entries == null)
        {
            EditorGUILayout.HelpBox("LocalizationData не назначен!", MessageType.Warning);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        // Выбор ключа перевода
        List<string> keys = new List<string>();
        foreach (var entry in localizedText.LocalizationData.Entries)
        {
            keys.Add(entry.Key);
        }

        SerializedProperty keyProp = serializedObject.FindProperty("selectedKey");
        int selectedIndex = keys.IndexOf(keyProp.stringValue);
        selectedIndex = EditorGUILayout.Popup("Localization Key", selectedIndex, keys.ToArray());

        if (selectedIndex >= 0 && selectedIndex < keys.Count && keyProp.stringValue != keys[selectedIndex])
        {
            keyProp.stringValue = keys[selectedIndex];
            localizedText.SetLanguage(localizedText.CurrentLanguage);
            EditorUtility.SetDirty(localizedText);
        }

        // Выбор языка
        List<string> languages = localizedText.LocalizationData.Languages;
        SerializedProperty langProp = serializedObject.FindProperty("currentLanguage");
        int langIndex = languages.IndexOf(langProp.stringValue);
        langIndex = EditorGUILayout.Popup("Current Language", langIndex, languages.ToArray());

        if (langIndex >= 0 && langIndex < languages.Count && langProp.stringValue != languages[langIndex])
        {
            langProp.stringValue = languages[langIndex];
            localizedText.SetLanguage(languages[langIndex]);
            EditorUtility.SetDirty(localizedText);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
