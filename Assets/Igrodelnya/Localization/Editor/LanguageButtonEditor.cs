using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(LanguageButton))]
public class LanguageButtonEditor : Editor
{
    private LocalizationManager manager;

    private void OnEnable()
    {
        manager = FindObjectOfType<LocalizationManager>();
    }

    public override void OnInspectorGUI()
    {
        LanguageButton langButton = (LanguageButton)target;
        serializedObject.Update();

        EditorGUILayout.LabelField("Ќастройка кнопки переключени€ €зыка", EditorStyles.boldLabel);

        if (manager == null || manager.LocalizationData == null)
        {
            EditorGUILayout.HelpBox("LocalizationManager или LocalizationData не найдены в сцене!", MessageType.Warning);
            langButton.Language = EditorGUILayout.TextField("язык", langButton.Language);
        }
        else
        {
            List<string> languages = manager.LocalizationData.Languages;

            if (languages.Count > 0)
            {
                int currentIndex = languages.IndexOf(langButton.Language);
                if (currentIndex < 0) currentIndex = 0;

                int selectedIndex = EditorGUILayout.Popup("язык", currentIndex, languages.ToArray());
                langButton.Language = languages[selectedIndex];
            }
            else
            {
                EditorGUILayout.HelpBox("¬ LocalizationData отсутствуют €зыки!", MessageType.Warning);
                langButton.Language = EditorGUILayout.TextField("язык", langButton.Language);
            }
        }

        if (GUILayout.Button("ќбновить список €зыков"))
        {
            manager = FindObjectOfType<LocalizationManager>();
            EditorUtility.SetDirty(langButton);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
