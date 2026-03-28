using OpalStudio.CustomToolbar.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace OpalStudio.CustomToolbar.Editor.ToolbarElements
{
      sealed internal class ToolbarClearPlayerPrefs : BaseToolbarElement
      {
            private GUIContent _buttonContent;

            protected override string Name => "Clear PlayerPrefs";
            protected override string Tooltip => "Deletes only PlayerPrefs keys. For full reset use Tools/Save/Clear ALL Save Data.";

            public override void OnInit()
            {
                  Texture icon = EditorGUIUtility.IconContent("d_TreeEditor.Trash").image;
                  _buttonContent = new GUIContent(icon, this.Tooltip);
            }

            public override void OnDrawInToolbar()
            {
                  if (GUILayout.Button(_buttonContent, ToolbarStyles.CommandButtonStyle, GUILayout.Width(this.Width)) && EditorUtility.DisplayDialog("Clear PlayerPrefs",
                                  "Delete ONLY PlayerPrefs keys?\n\nUse Tools/Save/Clear ALL Save Data for full save reset (PlayerPrefs + provider data).", "Yes, delete PlayerPrefs", "Cancel"))
                  {
                        PlayerPrefs.DeleteAll();
                        PlayerPrefs.Save();
                        Debug.LogWarning("PlayerPrefs cleared. Note: this is a partial reset. For full save reset use Tools/Save/Clear ALL Save Data.");
                  }
            }
      }
}
