using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class LocalizationAutoBinder
{
    private const string DefaultLocalizationAssetPath = "Assets/Igrodelnya2.0/Localization/LocalizationData.asset";
    private const string ReportDirectory = "Assets/Igrodelnya2.0/Localization/Reports";
    private const string ReportPath = ReportDirectory + "/AutoBindUnresolved.txt";
    private static readonly string[] IgnoredAssetPrefixes =
    {
        "Assets/_Sprites/",
        "Assets/Import/",
        "Assets/TextMesh Pro/",
    };
    private static readonly HashSet<string> IgnoredTextTokens = new HashSet<string>(StringComparer.Ordinal)
    {
        "E",
        "1/s",
        "name",
        "Name",
        "common",
        "NickName234562341",
        "AdfjAKFjasfdl",
        "x5",
        "x99",
        "x100 fortune",
        "CUR",
        "Button",
        "Toggle",
        "Pack Name",
    };

    [MenuItem("Tools/Localization/Bind Static Texts (Scenes + Prefabs)")]
    public static void BindStaticTextsMenu()
    {
        BindStaticTexts(writeReport: true);
    }

    public static void RunFromCommandLine()
    {
        try
        {
            BindStaticTexts(writeReport: true);
            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LocalizationAutoBinder] Failed: {ex}");
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }

    private static void BindStaticTexts(bool writeReport)
    {
        var localizationData = LoadLocalizationData();
        if (localizationData == null)
        {
            Debug.LogError("[LocalizationAutoBinder] LocalizationData not found.");
            return;
        }

        var defaultLanguage = localizationData.Languages.Count > 0 ? localizationData.Languages[0] : "Ru";
        var unresolved = new HashSet<string>(StringComparer.Ordinal);

        var addedCount = 0;
        var changedPrefabs = ProcessPrefabs(localizationData, defaultLanguage, unresolved, ref addedCount);
        var changedScenes = ProcessScenes(localizationData, defaultLanguage, unresolved, ref addedCount);

        AssetDatabase.SaveAssets();
        if (writeReport)
            WriteReport(unresolved);

        Debug.Log($"[LocalizationAutoBinder] Done. Added={addedCount}, ChangedPrefabs={changedPrefabs}, ChangedScenes={changedScenes}, Unresolved={unresolved.Count}");
    }

    private static LocalizationData LoadLocalizationData()
    {
        var data = AssetDatabase.LoadAssetAtPath<LocalizationData>(DefaultLocalizationAssetPath);
        if (data != null)
            return data;

        var guids = AssetDatabase.FindAssets("t:LocalizationData");
        if (guids.Length == 0)
            return null;

        var path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<LocalizationData>(path);
    }

    private static int ProcessPrefabs(LocalizationData localizationData, string defaultLanguage, ISet<string> unresolved, ref int addedCount)
    {
        var changed = 0;
        var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });

        for (var i = 0; i < prefabGuids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            if (!ShouldProcessAsset(path))
                continue;

            EditorUtility.DisplayProgressBar("Localization Auto Binder", $"Prefabs: {path}", (float)i / Math.Max(1, prefabGuids.Length));

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (!ProcessHierarchy(root, localizationData, defaultLanguage, path, unresolved, ref addedCount))
                    continue;

                PrefabUtility.SaveAsPrefabAsset(root, path);
                changed++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        EditorUtility.ClearProgressBar();
        return changed;
    }

    private static int ProcessScenes(LocalizationData localizationData, string defaultLanguage, ISet<string> unresolved, ref int addedCount)
    {
        var changed = 0;
        var sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        var previousScenePath = SceneManager.GetActiveScene().path;

        for (var i = 0; i < sceneGuids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
            EditorUtility.DisplayProgressBar("Localization Auto Binder", $"Scenes: {path}", (float)i / Math.Max(1, sceneGuids.Length));

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var sceneChanged = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (ProcessHierarchy(root, localizationData, defaultLanguage, path, unresolved, ref addedCount))
                    sceneChanged = true;
            }

            if (!sceneChanged)
                continue;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            changed++;
        }

        if (!string.IsNullOrEmpty(previousScenePath) && File.Exists(previousScenePath))
            EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);

        EditorUtility.ClearProgressBar();
        return changed;
    }

    private static bool ProcessHierarchy(GameObject root, LocalizationData localizationData, string defaultLanguage, string assetPath, ISet<string> unresolved, ref int addedCount)
    {
        var changed = false;

        var uiTexts = root.GetComponentsInChildren<Text>(true);
        foreach (var text in uiTexts)
        {
            if (TryBind(text, text.text, localizationData, defaultLanguage, assetPath, unresolved, out var didBind))
            {
                changed = true;
                addedCount++;
            }

            if (didBind)
                EditorUtility.SetDirty(text.gameObject);
        }

        var tmpTexts = root.GetComponentsInChildren<TMP_Text>(true);
        foreach (var text in tmpTexts)
        {
            if (TryBind(text, text.text, localizationData, defaultLanguage, assetPath, unresolved, out var didBind))
            {
                changed = true;
                addedCount++;
            }

            if (didBind)
                EditorUtility.SetDirty(text.gameObject);
        }

        return changed;
    }

    private static bool TryBind(Component target, string rawText, LocalizationData localizationData, string defaultLanguage, string assetPath, ISet<string> unresolved, out bool didBind)
    {
        didBind = false;
        if (target == null)
            return false;

        var gameObject = target.gameObject;
        if (gameObject.GetComponent<LocalizedText>() != null)
            return false;

        var text = NormalizeText(rawText);
        if (string.IsNullOrEmpty(text) || LooksDynamic(text))
            return false;

        var hierarchyPath = GetHierarchyPath(gameObject);
        if (ShouldIgnoreText(text, hierarchyPath))
            return false;

        if (!localizationData.TryFindKeyByTranslation(text, out var key))
        {
            unresolved.Add($"{assetPath} :: {hierarchyPath} => '{text}'");
            return false;
        }

        var localized = gameObject.AddComponent<LocalizedText>();
        localized.Configure(localizationData, key, defaultLanguage);
        EditorUtility.SetDirty(localized);
        didBind = true;
        return true;
    }

    private static bool LooksDynamic(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return true;

        if (text.Contains("{") && text.Contains("}"))
            return true;

        if (text.Contains("<sprite=", StringComparison.OrdinalIgnoreCase))
            return true;

        var hasLetter = false;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsLetter(text[i]))
            {
                hasLetter = true;
                break;
            }
        }

        if (!hasLetter)
            return true;

        return false;
    }

    private static bool ShouldIgnoreText(string text, string hierarchyPath)
    {
        if (IgnoredTextTokens.Contains(text))
            return true;

        if (IsHotkeyLabel(text))
            return true;

        if (LooksCounterOrTimer(text))
            return true;

        if (LooksLikelyRuntimePlaceholder(text, hierarchyPath))
            return true;

        return false;
    }

    private static bool IsHotkeyLabel(string text)
    {
        return text.Length == 3
            && text[0] == '['
            && char.IsLetter(text[1])
            && text[2] == ']';
    }

    private static bool LooksCounterOrTimer(string text)
    {
        var hasDigit = false;
        var hasLetter = false;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsDigit(text[i]))
                hasDigit = true;
            if (char.IsLetter(text[i]))
                hasLetter = true;
        }

        if (!hasDigit)
            return false;

        if (!hasLetter)
            return true;

        if (text.StartsWith("x", StringComparison.OrdinalIgnoreCase))
            return true;

        if (text.Contains("/") || text.Contains("%"))
            return true;

        return text.Length <= 8;
    }

    private static bool LooksLikelyRuntimePlaceholder(string text, string hierarchyPath)
    {
        if (string.IsNullOrEmpty(hierarchyPath))
            return false;

        if (hierarchyPath.IndexOf("/BuyHintDesctop", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (hierarchyPath.IndexOf("/ID", StringComparison.OrdinalIgnoreCase) >= 0 && text.IndexOf(' ') < 0)
            return true;

        if (hierarchyPath.IndexOf("/income", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (hierarchyPath.IndexOf("/amount", StringComparison.OrdinalIgnoreCase) >= 0 && LooksCounterOrTimer(text))
            return true;

        return false;
    }

    private static bool ShouldProcessAsset(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return false;

        for (var i = 0; i < IgnoredAssetPrefixes.Length; i++)
        {
            if (assetPath.StartsWith(IgnoredAssetPrefixes[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
    }

    private static string GetHierarchyPath(GameObject go)
    {
        if (go == null)
            return string.Empty;

        var path = go.name;
        var current = go.transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private static void WriteReport(IReadOnlyCollection<string> unresolved)
    {
        if (!AssetDatabase.IsValidFolder(ReportDirectory))
            AssetDatabase.CreateFolder("Assets/Igrodelnya2.0/Localization", "Reports");

        var lines = new List<string>(unresolved);
        lines.Sort(StringComparer.Ordinal);
        File.WriteAllLines(ReportPath, lines);
        AssetDatabase.ImportAsset(ReportPath);
    }
}
