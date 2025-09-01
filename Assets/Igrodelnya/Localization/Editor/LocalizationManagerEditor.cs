using UnityEditor;
using System.Collections.Generic;
using UnityEngine;

[CustomEditor(typeof(LocalizationManager))]
public class LocalizationManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        LocalizationManager manager = (LocalizationManager)target;

        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("localizationData"));

        if (manager.LocalizationData == null || manager.LocalizationData.Languages == null)
        {
            EditorGUILayout.HelpBox("LocalizationData не назначен!", MessageType.Warning);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        List<string> languages = manager.LocalizationData.Languages;
        SerializedProperty currentLangProp = serializedObject.FindProperty("currentLanguage");

        int langIndex = languages.IndexOf(currentLangProp.stringValue);
        langIndex = EditorGUILayout.Popup("Current Language", langIndex, languages.ToArray());

        if (langIndex >= 0 && langIndex < languages.Count)
        {
            string newLanguage = languages[langIndex];

            if (newLanguage != currentLangProp.stringValue)
            {
                currentLangProp.stringValue = newLanguage;
                manager.ChangeLanguage(newLanguage);

                // Вместо прямого вызова YG2.SwitchLanguage вызываем метод провайдера
                if (manager.LocalizationProvider != null)
                {
                    string formattedLang = char.ToLowerInvariant(newLanguage[0]) + newLanguage.Substring(1);
                    manager.LocalizationProvider.SwitchLanguage(formattedLang);
                }

                EditorUtility.SetDirty(manager);
            }
        }

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Обновить локализацию"))
        {
            manager.ChangeLanguage(manager.CurrentLanguage);
            EditorUtility.SetDirty(manager);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
