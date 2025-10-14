using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LocalizationData))]
public class LocalizationDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        LocalizationData localization = (LocalizationData)target;

        serializedObject.Update();

        if (localization.Languages == null || localization.Entries == null ||
            localization.Languages.Count == 0 || localization.Entries.Count == 0)
        {
            EditorGUILayout.HelpBox("Нет данных. Попробуйте загрузить из Google Sheets!", MessageType.Warning);
            return;
        }

        // Заголовок
        EditorGUILayout.LabelField("Таблица локализации", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Заголовок таблицы (ключ + языки)
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Ключ", EditorStyles.boldLabel, GUILayout.Width(150));

        foreach (var lang in localization.Languages)
        {
            EditorGUILayout.LabelField(lang, EditorStyles.boldLabel, GUILayout.Width(100));
        }
        EditorGUILayout.LabelField("", GUILayout.Width(25)); // Отступ под кнопку удаления
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        // Таблица с ключами и переводами
        for (int i = localization.Entries.Count - 1; i >= 0; i--)
        {
            var entry = localization.Entries[i];
            EditorGUILayout.BeginHorizontal();

            // Поле для ключа
            entry.Key = EditorGUILayout.TextField(entry.Key, GUILayout.Width(150));

            // Поля для переводов
            for (int j = 0; j < localization.Languages.Count; j++)
            {
                if (j < entry.Translations.Count)
                {
                    entry.Translations[j] = EditorGUILayout.TextField(entry.Translations[j], GUILayout.Width(100));
                }
                else
                {
                    EditorGUILayout.LabelField("-", GUILayout.Width(100));
                }
            }

            // Кнопка удаления строки
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                localization.Entries.RemoveAt(i);
                EditorUtility.SetDirty(localization);
                break; // Выходим из цикла, так как коллекция изменилась
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(10);

        // Кнопка добавления новой строки
        if (GUILayout.Button("Добавить новую строку"))
        {
            localization.Entries.Add(new LocalizationEntry { Key = "new_key" });
            EditorUtility.SetDirty(localization);
        }

        // Кнопка сохранения изменений
        if (GUILayout.Button("Сохранить изменения"))
        {
            EditorUtility.SetDirty(localization);
            AssetDatabase.SaveAssets();
        }

        serializedObject.ApplyModifiedProperties();
    }
}
