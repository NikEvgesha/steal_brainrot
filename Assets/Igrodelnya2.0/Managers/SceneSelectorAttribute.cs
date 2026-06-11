using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.Linq;
#endif

public class SceneSelectorAttribute : PropertyAttribute { }

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(SceneSelectorAttribute))]
public class SceneSelectorDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var scenes = EditorBuildSettings.scenes
            .Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path))
            .ToArray();

        int index = Mathf.Max(0, System.Array.IndexOf(scenes, property.stringValue));
        index = EditorGUI.Popup(position, label.text, index, scenes);

        property.stringValue = scenes.Length > 0 ? scenes[index] : "";
    }
}
#endif
