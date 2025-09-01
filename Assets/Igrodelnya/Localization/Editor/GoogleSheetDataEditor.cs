using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GoogleSheetData))]
public class GoogleSheetDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();

        GoogleSheetData script = (GoogleSheetData)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Google Sheet Controls", EditorStyles.boldLabel);

        if (GUILayout.Button("Download & Parse Sheet"))
        {
            script.DownloadAndParseSheet();
            EditorUtility.SetDirty(script);
            Debug.Log("Downloading data from Google Sheets...");
        }

        if (GUILayout.Button("Open Sheet in Browser"))
        {
            script.OpenSheetInBrowser();
        }
    }
}
