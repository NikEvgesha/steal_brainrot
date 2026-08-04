#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

internal static class CurrencyTextSpriteAssetEditor
{
    private const float InlineSpriteBearingYRatio = 0.9f;
    private const string CoinTexturePath = "Assets/_Sprites/ItemIcon_Coin.png";
    private const string CoinAssetPath =
        "Assets/TextMesh Pro/Resources/Sprite Assets/Coin.asset";

    [InitializeOnLoadMethod]
    private static void ScheduleEnsureCoinAsset()
    {
        EditorApplication.delayCall += EnsureCoinAsset;
    }

    [MenuItem("Tools/UI/Rebuild Inline Coin Sprite")]
    private static void RebuildCoinAsset()
    {
        AssetDatabase.DeleteAsset(CoinAssetPath);
        EnsureCoinAsset();
    }

    private static void EnsureCoinAsset()
    {
        var existing =
            AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(CoinAssetPath);
        if (existing != null)
        {
            if (existing.material != null &&
                existing.spriteGlyphTable.Count > 0 &&
                existing.spriteCharacterTable.Count > 0)
            {
                EnsureInlineMetrics(existing);
                return;
            }

            AssetDatabase.DeleteAsset(CoinAssetPath);
        }

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(CoinTexturePath);
        var sprite = AssetDatabase.LoadAllAssetsAtPath(CoinTexturePath)
            .OfType<Sprite>()
            .FirstOrDefault();
        var shader = Shader.Find("TextMeshPro/Sprite");
        if (texture == null || sprite == null || shader == null)
        {
            Debug.LogError(
                "[CurrencyText] Cannot create the inline coin sprite asset.");
            return;
        }

        var spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
        spriteAsset.name = "Coin";
        spriteAsset.hashCode = TMP_TextUtilities.GetSimpleHashCode(
            spriteAsset.name);
        spriteAsset.spriteSheet = texture;
        var serializedAsset = new SerializedObject(spriteAsset);
        serializedAsset.FindProperty("m_Version").stringValue = "1.1.0";
        serializedAsset.ApplyModifiedPropertiesWithoutUndo();

        var glyph = new TMP_SpriteGlyph
        {
            index = 0,
            metrics = new GlyphMetrics(
                sprite.rect.width,
                sprite.rect.height,
                0f,
                sprite.rect.height * InlineSpriteBearingYRatio,
                sprite.rect.width),
            glyphRect = new GlyphRect(sprite.rect),
            scale = 1f,
            sprite = sprite
        };
        var character = new TMP_SpriteCharacter(0xFFFE, glyph)
        {
            name = "coin",
            scale = 1f
        };

        spriteAsset.spriteGlyphTable.Add(glyph);
        spriteAsset.spriteCharacterTable.Add(character);

        AssetDatabase.CreateAsset(spriteAsset, CoinAssetPath);

        var material = new Material(shader)
        {
            name = "Coin Material"
        };
        material.SetTexture(ShaderUtilities.ID_MainTex, texture);
        spriteAsset.material = material;
        AssetDatabase.AddObjectToAsset(material, spriteAsset);

        spriteAsset.UpdateLookupTables();
        EditorUtility.SetDirty(spriteAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(CoinAssetPath, ImportAssetOptions.ForceUpdate);
        Debug.Log("[CurrencyText] Created inline coin TMP sprite asset.");
    }

    private static void EnsureInlineMetrics(TMP_SpriteAsset spriteAsset)
    {
        var glyph = spriteAsset.spriteGlyphTable[0];
        var metrics = glyph.metrics;
        var expectedBearingY = metrics.height * InlineSpriteBearingYRatio;
        if (Mathf.Approximately(metrics.horizontalBearingX, 0f) &&
            Mathf.Approximately(metrics.horizontalBearingY, expectedBearingY) &&
            Mathf.Approximately(metrics.horizontalAdvance, metrics.width))
        {
            return;
        }

        glyph.metrics = new GlyphMetrics(
            metrics.width,
            metrics.height,
            0f,
            expectedBearingY,
            metrics.width);
        spriteAsset.UpdateLookupTables();
        EditorUtility.SetDirty(spriteAsset);
        AssetDatabase.SaveAssets();
        Debug.Log("[CurrencyText] Fixed inline coin glyph alignment.");
    }
}
#endif
