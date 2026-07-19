#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One-time, reference-safe migration of the project's first-party uGUI Text components to TMP.
/// The manifest is kept in Library because it bridges the script field-type recompilation.
/// </summary>
public static class LegacyTextToTmpMigration
{
    private const string LegacyTextGuid = "5f7201a12d95ffc409449d95f23cf332";
    private const string FontPath = "Assets/Igrodelnya2.0/Fonts/RussoOne-Regular Cyrillic SDF.asset";
    private const string ManifestPath = "Library/LegacyTextToTmpMigration.json";

    [Serializable]
    private sealed class Manifest
    {
        public List<TextRecord> texts = new();
        public List<ReferenceRecord> references = new();
    }

    [Serializable]
    private sealed class TextRecord
    {
        public string assetPath;
        public string hierarchyPath;
        public string text;
        public Color color;
        public bool enabled;
        public bool raycastTarget;
        public bool maskable;
        public int fontSize;
        public int fontStyle;
        public int alignment;
        public bool bestFit;
        public int bestFitMin;
        public int bestFitMax;
        public int horizontalOverflow;
        public int verticalOverflow;
        public bool richText;
        public bool hasOutline;
        public Color outlineColor;
        public Vector2 outlineDistance;
    }

    [Serializable]
    private sealed class ReferenceRecord
    {
        public string assetPath;
        public string ownerHierarchyPath;
        public string ownerType;
        public int ownerTypeIndex;
        public string propertyPath;
        public string textHierarchyPath;
    }

    private sealed class Scope
    {
        public string assetPath;
        public GameObject[] roots;
        public Action save;
    }

    [MenuItem("Tools/UI/Migrate first-party Text to TMP/1. Prepare components")]
    public static void Prepare()
    {
        EnsureSafeEditorState();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
            throw new InvalidOperationException($"TMP font not found at {FontPath}");

        string[] assets = FindTargetAssets();
        var manifest = new Manifest();

        // Capture every reference before any nested prefab source is changed.
        ForEachScope(assets, scope => ScanScope(scope, manifest));
        File.WriteAllText(ManifestPath, JsonUtility.ToJson(manifest, true));

        int converted = 0;
        ForEachScope(assets, scope => converted += ConvertScope(scope, manifest, font));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[TMP Migration] Prepared {converted} Text components and {manifest.references.Count} references across {assets.Length} assets. Change runtime fields to TMP_Text, wait for compilation, then run Finish.");
    }

    public static string PrepareBatch(int startIndex, int count)
    {
        EnsureSafeEditorState();
        Manifest manifest = LoadManifest();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        string[] assets = manifest.texts.Select(record => record.assetPath).Distinct().OrderBy(path => path, StringComparer.Ordinal).ToArray();
        string[] batch = assets.Skip(Mathf.Max(0, startIndex)).Take(Mathf.Max(1, count)).ToArray();
        int converted = 0;
        ForEachScope(batch, scope => converted += ConvertScope(scope, manifest, font));
        AssetDatabase.SaveAssets();
        return $"Prepared batch {startIndex}..{startIndex + batch.Length - 1}: {converted} components in {batch.Length} assets ({assets.Length} total assets).";
    }

    [MenuItem("Tools/UI/Migrate first-party Text to TMP/2. Restore references and validate")]
    public static void Finish()
    {
        EnsureSafeEditorState();
        if (!File.Exists(ManifestPath))
            throw new FileNotFoundException("TMP migration manifest is missing. Run Prepare first.", ManifestPath);

        Manifest manifest = LoadManifest();
        int restored = 0;
        var grouped = ReferencesToRestore(manifest).GroupBy(record => record.assetPath).ToDictionary(group => group.Key, group => group.ToList());
        ForEachScope(grouped.Keys.ToArray(), scope => restored += RestoreReferences(scope, grouped[scope.assetPath]));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string[] leftovers = FindTargetAssets();
        if (leftovers.Length > 0)
            throw new InvalidOperationException($"TMP migration left legacy Text components in: {string.Join(", ", leftovers)}");

        Debug.Log($"[TMP Migration] Complete. Restored {restored}/{manifest.references.Count} serialized references. No first-party legacy Text components remain.");
    }

    public static string FinishBatch(int startIndex, int count)
    {
        EnsureSafeEditorState();
        Manifest manifest = LoadManifest();
        var grouped = ReferencesToRestore(manifest).GroupBy(record => record.assetPath)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();
        var batch = grouped.Skip(Mathf.Max(0, startIndex)).Take(Mathf.Max(1, count)).ToArray();
        int restored = 0;
        var recordsByPath = batch.ToDictionary(group => group.Key, group => group.ToList());
        ForEachScope(recordsByPath.Keys, scope => restored += RestoreReferences(scope, recordsByPath[scope.assetPath]));
        AssetDatabase.SaveAssets();
        return $"Finished batch {startIndex}..{startIndex + batch.Length - 1}: restored {restored} references in {batch.Length} assets ({grouped.Length} total assets).";
    }

    public static string Validate()
    {
        string[] leftovers = FindTargetAssets();
        if (leftovers.Length > 0)
            return $"Legacy Text remains in {leftovers.Length} assets: {string.Join(", ", leftovers)}";
        return "No first-party legacy Text components remain.";
    }

    private static void ScanScope(Scope scope, Manifest manifest)
    {
        Text[] texts = scope.roots.SelectMany(root => root.GetComponentsInChildren<Text>(true)).ToArray();
        var localTexts = new HashSet<Text>(texts);

        foreach (Text text in texts)
        {
            if (!IsOwnedByScope(text, scope.assetPath))
                continue;

            Outline outline = text.GetComponent<Outline>();
            manifest.texts.Add(new TextRecord
            {
                assetPath = scope.assetPath,
                hierarchyPath = GetHierarchyPath(scope.roots, text.transform),
                text = text.text,
                color = text.color,
                enabled = text.enabled,
                raycastTarget = text.raycastTarget,
                maskable = text.maskable,
                fontSize = text.fontSize,
                fontStyle = (int)text.fontStyle,
                alignment = (int)text.alignment,
                bestFit = text.resizeTextForBestFit,
                bestFitMin = text.resizeTextMinSize,
                bestFitMax = text.resizeTextMaxSize,
                horizontalOverflow = (int)text.horizontalOverflow,
                verticalOverflow = (int)text.verticalOverflow,
                richText = text.supportRichText,
                hasOutline = outline != null && outline.enabled,
                outlineColor = outline != null ? outline.effectColor : Color.black,
                outlineDistance = outline != null ? outline.effectDistance : Vector2.zero
            });
        }

        foreach (Component owner in scope.roots.SelectMany(root => root.GetComponentsInChildren<Component>(true)))
        {
            if (owner == null)
                continue;
            if (PrefabUtility.IsPartOfPrefabInstance(owner))
                continue;

            try
            {
                var serialized = new SerializedObject(owner);
                SerializedProperty property = serialized.GetIterator();
                bool enterChildren = true;
                while (property.Next(enterChildren))
                {
                    enterChildren = true;
                    if (property.propertyType != SerializedPropertyType.ObjectReference)
                        continue;
                    if (property.objectReferenceValue is not Text target || !localTexts.Contains(target))
                        continue;

                    manifest.references.Add(new ReferenceRecord
                    {
                        assetPath = scope.assetPath,
                        ownerHierarchyPath = GetHierarchyPath(scope.roots, owner.transform),
                        ownerType = owner.GetType().FullName,
                        ownerTypeIndex = GetSameTypeIndex(owner),
                        propertyPath = property.propertyPath,
                        textHierarchyPath = GetHierarchyPath(scope.roots, target.transform)
                    });
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TMP Migration] Could not scan {owner.GetType().FullName} in {scope.assetPath}: {exception.Message}");
            }
        }
    }

    private static int ConvertScope(Scope scope, Manifest manifest, TMP_FontAsset font)
    {
        int converted = 0;
        foreach (TextRecord record in manifest.texts.Where(item => item.assetPath == scope.assetPath))
        {
            Transform transform = ResolveHierarchyPath(scope.roots, record.hierarchyPath);
            if (transform == null)
                throw new InvalidOperationException($"Missing text transform {record.hierarchyPath} in {scope.assetPath}");

            Text legacy = transform.GetComponent<Text>();
            if (legacy == null)
                continue;

            foreach (Shadow effect in transform.GetComponents<Shadow>())
                UnityEngine.Object.DestroyImmediate(effect, true);

            UnityEngine.Object.DestroyImmediate(legacy, true);
            var tmp = transform.gameObject.AddComponent<TextMeshProUGUI>();
            ApplyRecord(tmp, record, font);
            converted++;
        }

        if (converted > 0)
            scope.save();
        return converted;
    }

    private static int RestoreReferences(Scope scope, List<ReferenceRecord> records)
    {
        int restored = 0;
        bool changed = false;
        foreach (ReferenceRecord record in records)
        {
            Transform ownerTransform = ResolveHierarchyPath(scope.roots, record.ownerHierarchyPath);
            Transform textTransform = ResolveHierarchyPath(scope.roots, record.textHierarchyPath);
            TMP_Text target = textTransform != null ? textTransform.GetComponent<TMP_Text>() : null;
            Component owner = FindOwner(ownerTransform, record.ownerType, record.ownerTypeIndex);
            if (owner == null || target == null)
            {
                Debug.LogError($"[TMP Migration] Missing owner or TMP target for {record.assetPath}: {record.ownerType}.{record.propertyPath}");
                continue;
            }

            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(record.propertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
            {
                Debug.LogError($"[TMP Migration] Missing reference property {record.ownerType}.{record.propertyPath} in {record.assetPath}");
                continue;
            }

            try
            {
                property.objectReferenceValue = target;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                restored++;
                changed = true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[TMP Migration] Cannot assign TMP to {record.ownerType}.{record.propertyPath} in {record.assetPath}: {exception.Message}");
            }
        }

        if (changed)
            scope.save();
        return restored;
    }

    private static void ApplyRecord(TextMeshProUGUI tmp, TextRecord record, TMP_FontAsset font)
    {
        tmp.font = font;
        if (font != null && font.material != null)
            tmp.fontSharedMaterial = font.material;
        tmp.text = record.text;
        tmp.color = record.color;
        tmp.enabled = record.enabled;
        tmp.raycastTarget = record.raycastTarget;
        tmp.maskable = record.maskable;
        tmp.fontSize = Mathf.Max(1, record.fontSize);
        tmp.fontSizeMin = Mathf.Max(1, record.bestFitMin);
        tmp.fontSizeMax = Mathf.Max(tmp.fontSizeMin, record.bestFitMax);
        tmp.enableAutoSizing = record.bestFit;
        tmp.fontStyle = ConvertFontStyle((FontStyle)record.fontStyle);
        tmp.alignment = ConvertAlignment((TextAnchor)record.alignment);
        tmp.textWrappingMode = (HorizontalWrapMode)record.horizontalOverflow == HorizontalWrapMode.Wrap
            ? TextWrappingModes.Normal
            : TextWrappingModes.NoWrap;
        tmp.overflowMode = (VerticalWrapMode)record.verticalOverflow == VerticalWrapMode.Truncate
            ? TextOverflowModes.Truncate
            : TextOverflowModes.Overflow;
        tmp.richText = record.richText;
        tmp.extraPadding = true;

        if (record.hasOutline)
        {
            tmp.outlineColor = record.outlineColor;
        }
    }

    private static FontStyles ConvertFontStyle(FontStyle style)
    {
        return style switch
        {
            FontStyle.Bold => FontStyles.Bold,
            FontStyle.Italic => FontStyles.Italic,
            FontStyle.BoldAndItalic => FontStyles.Bold | FontStyles.Italic,
            _ => FontStyles.Normal
        };
    }

    private static TextAlignmentOptions ConvertAlignment(TextAnchor anchor)
    {
        return anchor switch
        {
            TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter => TextAlignmentOptions.Top,
            TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
            TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
            TextAnchor.MiddleRight => TextAlignmentOptions.Right,
            TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
            TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
            _ => TextAlignmentOptions.Center
        };
    }

    private static void ForEachScope(IEnumerable<string> assetPaths, Action<Scope> action)
    {
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string assetPath in assetPaths)
            {
                if (assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    GameObject root = PrefabUtility.LoadPrefabContents(assetPath);
                    try
                    {
                        action(new Scope
                        {
                            assetPath = assetPath,
                            roots = new[] { root },
                            save = () => PrefabUtility.SaveAsPrefabAsset(root, assetPath)
                        });
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
                else
                {
                    Scene scene = EditorSceneManager.OpenScene(assetPath, OpenSceneMode.Single);
                    action(new Scope
                    {
                        assetPath = assetPath,
                        roots = scene.GetRootGameObjects(),
                        save = () => EditorSceneManager.SaveScene(scene)
                    });
                }
            }
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }

    private static string[] FindTargetAssets()
    {
        return AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
            .Concat(AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            .Where(path => !IsExcluded(path))
            .Where(path => File.Exists(path) && File.ReadAllText(path).Contains(LegacyTextGuid, StringComparison.Ordinal))
            .OrderBy(path => path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsExcluded(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized.Contains("/VoxelImporter/Examples/", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("/_Recovery/", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("/TouchControlsKit-Lite/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOwnedByScope(Text text, string assetPath)
    {
        if (assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            return !PrefabUtility.IsPartOfPrefabInstance(text);
        return !PrefabUtility.IsPartOfPrefabInstance(text);
    }

    private static string GetHierarchyPath(GameObject[] roots, Transform target)
    {
        var indices = new List<int>();
        Transform current = target;
        while (current.parent != null)
        {
            indices.Add(current.GetSiblingIndex());
            current = current.parent;
        }

        int rootIndex = Array.FindIndex(roots, root => root.transform == current);
        if (rootIndex < 0)
            throw new InvalidOperationException($"{target.name} is outside the migration scope");
        indices.Add(rootIndex);
        indices.Reverse();
        return string.Join("/", indices);
    }

    private static Transform ResolveHierarchyPath(GameObject[] roots, string path)
    {
        int[] indices = path.Split('/').Select(int.Parse).ToArray();
        if (indices.Length == 0 || indices[0] < 0 || indices[0] >= roots.Length)
            return null;

        Transform current = roots[indices[0]].transform;
        for (int i = 1; i < indices.Length; i++)
        {
            if (indices[i] < 0 || indices[i] >= current.childCount)
                return null;
            current = current.GetChild(indices[i]);
        }
        return current;
    }

    private static int GetSameTypeIndex(Component component)
    {
        Component[] matches = component.gameObject.GetComponents(component.GetType());
        return Array.IndexOf(matches, component);
    }

    private static Component FindOwner(Transform transform, string typeName, int typeIndex)
    {
        if (transform == null)
            return null;
        Component[] matches = transform.GetComponents<Component>().Where(component => component != null && component.GetType().FullName == typeName).ToArray();
        return typeIndex >= 0 && typeIndex < matches.Length ? matches[typeIndex] : null;
    }

    private static void EnsureSafeEditorState()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before running the TMP migration.");
        if (EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save the currently open scene before running the TMP migration.");
    }

    private static Manifest LoadManifest()
    {
        if (!File.Exists(ManifestPath))
            throw new FileNotFoundException("TMP migration manifest is missing. Run Prepare first.", ManifestPath);
        return JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
    }

    private static IEnumerable<ReferenceRecord> ReferencesToRestore(Manifest manifest)
    {
        // Scene instances inherit their references from migrated source prefabs. Writing them back
        // would create thousands of redundant overrides in the main gameplay scene.
        return manifest.references.Where(record => !record.assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase));
    }
}
#endif
