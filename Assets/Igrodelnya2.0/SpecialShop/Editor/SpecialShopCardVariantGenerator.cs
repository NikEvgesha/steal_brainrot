using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps the shop prefab connected to the four intentionally authored card
/// templates. Older style-specific prefabs remain as an archive and are not
/// instantiated by the shop.
/// </summary>
internal static class SpecialShopCardVariantGenerator
{
    private const string ShopPrefabPath = "Assets/Igrodelnya2.0/SpecialShop/SpecialShop.prefab";
    private const string SmallUsePath = "Assets/Igrodelnya2.0/SpecialShop/CardTemplates/Active/SpecialShopCard_SmallUse.prefab";
    private const string SmallPath = "Assets/Igrodelnya2.0/SpecialShop/CardTemplates/Active/SpecialShopCard_Small.prefab";
    private const string WidePath = "Assets/Igrodelnya2.0/SpecialShop/CardTemplates/Active/SpecialShopCard_Wide.prefab";
    private const string EternalPath = "Assets/Igrodelnya2.0/SpecialShop/CardTemplates/Active/SpecialShopCard_EternalPack.prefab";

    [MenuItem("Tools/Special Shop/Assign Four Active Card Templates")]
    public static void AssignActiveTemplates()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ShopPrefabPath);
        try
        {
            SpecialShop shop = root.GetComponent<SpecialShop>();
            if (shop == null)
                throw new InvalidOperationException("SpecialShop component is missing on the shop prefab root.");

            var serializedShop = new SerializedObject(shop);
            Assign(serializedShop, "_smallUseSlotPrefab", SmallUsePath);
            Assign(serializedShop, "_smallSlotPrefab", SmallPath);
            Assign(serializedShop, "_wideSlotPrefab", WidePath);
            Assign(serializedShop, "_eternalPackSlotPrefab", EternalPath);
            serializedShop.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, ShopPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[SpecialShop] Four active card templates are assigned.");
    }

    public static void GenerateFromCommandLine() => AssignActiveTemplates();

    private static void Assign(SerializedObject serializedShop, string fieldName, string prefabPath)
    {
        SerializedProperty property = serializedShop.FindProperty(fieldName);
        if (property == null)
            throw new MissingFieldException(typeof(SpecialShop).Name, fieldName);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        property.objectReferenceValue = prefab != null ? prefab.GetComponent<SpecialShopSlot>() : null;
        if (property.objectReferenceValue == null)
            throw new InvalidOperationException($"SpecialShopSlot is missing in {prefabPath}");
    }
}
