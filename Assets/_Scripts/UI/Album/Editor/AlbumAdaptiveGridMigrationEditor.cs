#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class AlbumAdaptiveGridMigrationEditor
{
    private const string AlbumScreenPath = "Assets/_Prefabs/UI/Album/AlbumScreen.prefab";
    private const string AlbumEntryViewPath = "Assets/_Prefabs/UI/Album/AlbumEntryView.prefab";
    private const string AlbumRareTabViewPath = "Assets/_Prefabs/UI/Album/AlbumRareTabView.prefab";
    private const string CardsPath = "panelRoot/Root/cardsRoot/cards";
    private const string RareTabsPath = "panelRoot/Root/InfoPanel/rareTabsRoot";

    [MenuItem("Tools/Album/Migrate AlbumScreen To Adaptive Grid")]
    public static void Apply()
    {
        var root = PrefabUtility.LoadPrefabContents(AlbumScreenPath);
        try
        {
            var cards = root.transform.Find(CardsPath);
            if (cards == null)
            {
                Debug.LogError("[AlbumAdaptiveGridMigration] Cards container not found: " + CardsPath);
                return;
            }

            RemoveComponent<DynamicGridSpawner>(cards.gameObject);
            RemoveComponent<VerticalLayoutGroup>(cards.gameObject);

            var grid = cards.GetComponent<AdaptiveGridSpawner>();
            if (grid == null)
                grid = cards.gameObject.AddComponent<AdaptiveGridSpawner>();

            var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AlbumEntryViewPath);
            grid.SetItemPrefab(cardPrefab);
            grid.ConfigureFitItemCount(
                0,
                new Vector2(12f, 12f),
                new RectOffset(10, 10, 10, 10),
                new Vector2(95f, 95f),
                new Vector2(145f, 145f),
                columns: 5);
            grid.ConfigurePercentLayout(
                new Vector2(1.4f, 1.8f),
                new Vector4(1.2f, 1.2f, 1.6f, 1.6f));

            var gridSo = new SerializedObject(grid);
            SetBool(gridSo, "deferRuntimeRebuildOneFrame", false);
            gridSo.ApplyModifiedPropertiesWithoutUndo();

            var controller = root.GetComponent<AlbumScreenController>();
            if (controller == null)
            {
                Debug.LogError("[AlbumAdaptiveGridMigration] AlbumScreenController not found.");
                return;
            }

            var controllerSo = new SerializedObject(controller);
            SetObject(controllerSo, "cardsRoot", cards);
            SetObject(controllerSo, "cardsAdaptiveGrid", grid);
            SetObject(controllerSo, "cardsDynamicGrid", null);

            var rareTabs = root.transform.Find(RareTabsPath);
            if (rareTabs != null)
            {
                RemoveComponent<HorizontalLayoutGroup>(rareTabs.gameObject);

                var rareTabsGrid = rareTabs.GetComponent<AdaptiveGridSpawner>();
                if (rareTabsGrid == null)
                    rareTabsGrid = rareTabs.gameObject.AddComponent<AdaptiveGridSpawner>();

                var rareTabPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AlbumRareTabViewPath);
                rareTabsGrid.SetItemPrefab(rareTabPrefab);
                rareTabsGrid.ConfigureFixed(
                    new Vector2(62f, 46f),
                    new Vector2(6f, 6f),
                    new RectOffset(4, 4, 4, 0),
                    hideOverflow: false);

                var rareTabsGridSo = new SerializedObject(rareTabsGrid);
                SetBool(rareTabsGridSo, "deferRuntimeRebuildOneFrame", false);
                rareTabsGridSo.ApplyModifiedPropertiesWithoutUndo();

                SetObject(controllerSo, "rareTabsRoot", rareTabs);
                SetObject(controllerSo, "rareTabsAdaptiveGrid", rareTabsGrid);
            }

            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, AlbumScreenPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AlbumAdaptiveGridMigration] AlbumScreen migrated to AdaptiveGridSpawner.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RemoveComponent<T>(GameObject target) where T : Component
    {
        var component = target.GetComponent<T>();
        if (component != null)
            Object.DestroyImmediate(component, true);
    }

    private static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
    {
        var property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        var property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }
}
#endif
