using UnityEngine;
using UnityEngine.Rendering;

public class FlatLightingController : MonoBehaviour
{
    [SerializeField] private bool applyOnAwake = true;
    [SerializeField] private bool disableAllShadows = true;
    [SerializeField] private bool flattenAmbient = true;
    [SerializeField] private Color ambientColor = new Color(0.92f, 0.92f, 0.92f, 1f);
    [SerializeField] [Range(0f, 2f)] private float ambientIntensity = 1.2f;
    [SerializeField] private bool reduceDirectionalContrast = true;
    [SerializeField] [Range(0f, 2f)] private float maxDirectionalIntensity = 0.25f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<FlatLightingController>() != null)
            return;

        var go = new GameObject("FlatLightingController");
        go.AddComponent<FlatLightingController>();
    }

    private void Awake()
    {
        if (applyOnAwake)
            Apply();
    }

    [ContextMenu("Apply Flat Lighting")]
    public void Apply()
    {
        if (disableAllShadows)
        {
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.shadowDistance = 0f;
        }

        if (flattenAmbient)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;
            RenderSettings.ambientIntensity = ambientIntensity;
        }

        var lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var light in lights)
        {
            if (light == null)
                continue;

            if (disableAllShadows)
                light.shadows = LightShadows.None;

            if (reduceDirectionalContrast && light.type == LightType.Directional)
                light.intensity = Mathf.Min(light.intensity, maxDirectionalIntensity);
        }
    }
}
