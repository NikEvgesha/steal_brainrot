using TMPro;
using UnityEditor;
using UnityEngine;

public static class RemoteProfilePopupEditorTools
{
    private const string PrefabPath = "Assets/_Prefabs/UI/Resources/RemoteProfilePopup.prefab";
    private const string TexturePath = "Assets/_Sprites/texture.png";
    private const string GradientPath = "Assets/_Sprites/Gradient2.png";
    private const string FontPath = "Assets/Igrodelnya2.0/Fonts/RussoOne-Regular SDF.asset";

    [MenuItem("Tools/UI/Restyle Remote Profile Popup")]
    public static void RestylePrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            RemoteProfilePopup popup = root.GetComponent<RemoteProfilePopup>();
            if (popup == null)
                throw new MissingComponentException($"RemoteProfilePopup is missing on {PrefabPath}.");

            Sprite texture = AssetDatabase.LoadAssetAtPath<Sprite>(TexturePath);
            Sprite gradient = AssetDatabase.LoadAssetAtPath<Sprite>(GradientPath);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (texture == null || gradient == null || font == null)
            {
                throw new MissingReferenceException(
                    $"Remote profile theme assets are missing: texture={TexturePath}, gradient={GradientPath}, font={FontPath}.");
            }

            popup.ConfigureVisualAssets(texture, gradient, font);
            popup.ApplyVisualStyle();

            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                var serializedText = new SerializedObject(text);
                serializedText.FindProperty("m_fontAsset").objectReferenceValue = font;
                serializedText.FindProperty("m_sharedMaterial").objectReferenceValue = font.material;
                serializedText.FindProperty("m_fontMaterial").objectReferenceValue = null;
                serializedText.ApplyModifiedPropertiesWithoutUndo();
            }

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
