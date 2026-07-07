using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PlayModeStartSceneGuard
{
    private const string StartScenePath = "Assets/Scenes/LoadingScene.unity";
    private static readonly string[] BypassSceneNames =
    {
        "Foto",
        "AdaptiveGridDemo"
    };

    static PlayModeStartSceneGuard()
    {
        EditorApplication.delayCall += EnsurePlayModeStartScene;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode)
            return;

        var activeScene = SceneManager.GetActiveScene();
        if (IsBypassScene(activeScene.name))
        {
            EditorSceneManager.playModeStartScene = null;
            Debug.Log($"[PlayModeStartSceneGuard] Play from '{activeScene.path}' without redirect.");
            return;
        }

        EnsurePlayModeStartScene();

        var activeScenePath = activeScene.path;
        if (!string.Equals(activeScenePath, StartScenePath, StringComparison.OrdinalIgnoreCase))
            Debug.Log($"[PlayModeStartSceneGuard] Play pressed from '{activeScenePath}', starting from '{StartScenePath}'.");
    }

    private static void EnsurePlayModeStartScene()
    {
        var activeScene = SceneManager.GetActiveScene();
        if (IsBypassScene(activeScene.name))
        {
            if (EditorSceneManager.playModeStartScene != null)
                EditorSceneManager.playModeStartScene = null;

            return;
        }

        var startScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(StartScenePath);
        if (startScene == null)
        {
            Debug.LogWarning($"[PlayModeStartSceneGuard] Scene not found: {StartScenePath}");
            return;
        }

        if (EditorSceneManager.playModeStartScene != startScene)
            EditorSceneManager.playModeStartScene = startScene;
    }

    private static bool IsBypassScene(string sceneName)
    {
        for (var i = 0; i < BypassSceneNames.Length; i++)
        {
            if (string.Equals(sceneName, BypassSceneNames[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
