using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class WebGLAudioModerationValidator : IPreprocessBuildWithReport
{
    private static bool _settingsPassRunning;

    public int callbackOrder => -1000;

    [InitializeOnLoadMethod]
    private static void ScheduleSafeWebGLSettings()
    {
        EditorApplication.delayCall += () => ApplySafeWebGLSettings();
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform == BuildTarget.WebGL)
        {
            ApplySafeWebGLSettings();
            Validate(throwOnError: true);
        }
    }

    [MenuItem("Tools/Validation/Apply WebGL Audio System Player Fix")]
    private static void ApplyFromMenu()
    {
        int changed = ApplySafeWebGLSettings();
        Debug.Log($"[WebGLAudioModerationValidator] Applied Decompress On Load to {changed} audio clips.");
    }

    [MenuItem("Tools/Validation/Audit WebGL Audio For System Player")]
    private static void AuditFromMenu()
    {
        Validate(throwOnError: false);
    }

    private static void Validate(bool throwOnError)
    {
        var unsafeAssets = new List<string>();
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
                continue;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            if (importer.ContainsSampleSettingsOverride("WebGL"))
                settings = importer.GetOverrideSampleSettings("WebGL");

            if (settings.loadType != AudioClipLoadType.DecompressOnLoad)
                unsafeAssets.Add(path);
        }

        if (unsafeAssets.Count == 0)
        {
            Debug.Log("[WebGLAudioModerationValidator] OK: all WebGL audio uses Decompress On Load.");
            return;
        }

        string message = "WebGL build contains audio clips that do not use Decompress On Load. They can expose the browser/system media player, which is forbidden by platform moderation:\n" +
                         string.Join("\n", unsafeAssets);
        if (throwOnError)
            throw new BuildFailedException(message);

        Debug.LogError(message);
    }

    private static int ApplySafeWebGLSettings()
    {
        if (_settingsPassRunning || EditorApplication.isPlayingOrWillChangePlaymode)
            return 0;

        _settingsPassRunning = true;
        int changed = 0;
        try
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
                    continue;

                AudioImporterSampleSettings settings = importer.defaultSampleSettings;

                if (settings.loadType == AudioClipLoadType.DecompressOnLoad &&
                    !settings.preloadAudioData)
                    continue;

                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.preloadAudioData = false;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
                changed++;
            }
        }
        finally
        {
            _settingsPassRunning = false;
        }

        if (changed > 0)
            Debug.Log($"[WebGLAudioModerationValidator] Updated {changed} audio import settings.");
        return changed;
    }
}
