using UnityEditor;
using UnityEngine;

public static class RemoteProfilePopupEditorTools
{
    private const string PrefabPath = "Assets/_Prefabs/UI/Resources/RemoteProfilePopup.prefab";

    [MenuItem("Tools/UI/Restyle Remote Profile Popup")]
    public static void RestylePrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            RemoteProfilePopup popup = root.GetComponent<RemoteProfilePopup>();
            if (popup == null)
                throw new MissingComponentException($"RemoteProfilePopup is missing on {PrefabPath}.");

            popup.ApplyVisualStyle();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[RemoteProfilePopup] Restyled prefab with the correct blocky panel sprites: {PrefabPath}");
    }
}
