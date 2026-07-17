using UnityEditor;
using UnityEngine;

public static class RemoteProfilePopupEditorTools
{
    private const string PrefabPath = "Assets/_Prefabs/UI/Resources/RemoteProfilePopup.prefab";
    private const string TexturePath = "Assets/_Sprites/texture.png";
    private const string GradientPath = "Assets/_Sprites/Gradient2.png";
    private const string FontPath = "Assets/Igrodelnya2.0/Fonts/RussoOne-Regular.ttf";

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
            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (texture == null || gradient == null || font == null)
            {
                throw new MissingReferenceException(
                    $"Remote profile theme assets are missing: texture={TexturePath}, gradient={GradientPath}, font={FontPath}.");
            }

            popup.ConfigureVisualAssets(texture, gradient, font);
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
