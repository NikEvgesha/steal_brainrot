using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Filters invalid browser pointer coordinates before the EventSystem sends them
/// into GraphicRaycaster. Some WebGL hosts briefly return NaN while the page or
/// canvas is being initialized, which otherwise produces an error every frame.
/// </summary>
[DefaultExecutionOrder(-10000)]
public sealed class SafeEventSystemInput : BaseInput
{
    private Vector2 _lastValidMousePosition;

    public override Vector2 mousePosition
    {
        get
        {
            Vector2 current = base.mousePosition;
            if (IsFinite(current))
            {
                _lastValidMousePosition = current;
                return current;
            }

            if (!IsFinite(_lastValidMousePosition))
                _lastValidMousePosition = GetScreenCenter();

            return _lastValidMousePosition;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        _lastValidMousePosition = GetScreenCenter();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterInstaller()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallAfterInitialSceneLoad()
    {
        InstallForAllEventSystems();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallForAllEventSystems();
    }

    private static void InstallForAllEventSystems()
    {
        StandaloneInputModule[] modules = Object.FindObjectsByType<StandaloneInputModule>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < modules.Length; i++)
        {
            StandaloneInputModule module = modules[i];
            SafeEventSystemInput safeInput = module.GetComponent<SafeEventSystemInput>();
            if (safeInput == null)
                safeInput = module.gameObject.AddComponent<SafeEventSystemInput>();

            module.inputOverride = safeInput;
        }
    }

    private static Vector2 GetScreenCenter()
    {
        return new Vector2(
            Mathf.Max(0, Screen.width) * 0.5f,
            Mathf.Max(0, Screen.height) * 0.5f);
    }

    private static bool IsFinite(Vector2 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y);
    }
}
