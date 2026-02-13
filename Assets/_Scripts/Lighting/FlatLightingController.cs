using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class FlatLightingController : MonoBehaviour
{
    private enum SkyboxPreset
    {
        SoftBlue = 0,
        WarmSunset = 1,
        MintDay = 2
    }

    [SerializeField] private bool applyOnAwake = true;
    [SerializeField] private float reapplyIntervalSec = 2f;
    [SerializeField] private bool disableAllShadows = true;
    [SerializeField] private bool disableFog = true;
    [SerializeField] private bool flattenAmbient = true;
    [SerializeField] private bool useTrilightAmbient = true;
    [SerializeField] private Color ambientColor = new Color(0.92f, 0.92f, 0.92f, 1f);
    [SerializeField] private Color ambientSkyColor = new Color(0.95f, 0.95f, 0.95f, 1f);
    [SerializeField] private Color ambientEquatorColor = new Color(0.88f, 0.88f, 0.88f, 1f);
    [SerializeField] private Color ambientGroundColor = new Color(0.82f, 0.82f, 0.82f, 1f);
    [SerializeField] [Range(0f, 2f)] private float ambientIntensity = 1.1f;
    [SerializeField] private bool disableDirectionalLights = false;
    [SerializeField] private bool reduceDirectionalContrast = true;
    [SerializeField] [Range(0f, 2f)] private float maxDirectionalIntensity = 0.4f;
    [SerializeField] private bool disableReflections = false;
    [SerializeField] private bool forceRendererNoShadows = true;
    [Header("Skybox")]
    [SerializeField] private bool overrideSkybox = false;
    [SerializeField] private bool hideSkyboxSunDisk = true;
    [SerializeField] private SkyboxPreset skyboxPreset = SkyboxPreset.SoftBlue;
    [SerializeField] private Material customSkyboxMaterial;
    [Header("Post Effects (URP)")]
    [SerializeField] private bool enablePostEffects = true;
    [SerializeField] [Range(-2f, 2f)] private float postExposure = 0.02f;
    [SerializeField] [Range(-100f, 100f)] private float postContrast = 6f;
    [SerializeField] [Range(-100f, 100f)] private float postSaturation = 8f;
    [SerializeField] private bool enableTonemapping = true;
    [SerializeField] private TonemappingMode tonemappingMode = TonemappingMode.Neutral;
    [SerializeField] private bool enableVignette = true;
    [SerializeField] [Range(0f, 1f)] private float vignetteIntensity = 0.08f;
    [SerializeField] [Range(0f, 1f)] private float vignetteSmoothness = 0.35f;
    [SerializeField] private bool enableBloom = false;
    [SerializeField] private bool allowBloomInWebGL = false;
    [SerializeField] [Range(0f, 2f)] private float bloomIntensity = 0.1f;
    [SerializeField] [Range(0f, 2f)] private float bloomThreshold = 1f;

    private Coroutine _reapplyRoutine;
    private Material _runtimeSkyboxMaterial;
    private Volume _runtimePostVolume;
    private VolumeProfile _runtimePostProfile;
    private SkyboxPreset _activeSkyboxPreset = (SkyboxPreset)(-1);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<FlatLightingController>() != null)
            return;

        var go = new GameObject("FlatLightingController");
        go.AddComponent<FlatLightingController>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (reapplyIntervalSec > 0f && _reapplyRoutine == null)
            _reapplyRoutine = StartCoroutine(ReapplyRoutine());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_reapplyRoutine != null)
        {
            StopCoroutine(_reapplyRoutine);
            _reapplyRoutine = null;
        }
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        if (applyOnAwake)
            Apply();
    }

    private void OnDestroy()
    {
        if (_runtimeSkyboxMaterial != null)
        {
            Destroy(_runtimeSkyboxMaterial);
            _runtimeSkyboxMaterial = null;
        }

        if (_runtimePostProfile != null)
        {
            Destroy(_runtimePostProfile);
            _runtimePostProfile = null;
        }

        _runtimePostVolume = null;
    }

    private void OnSceneLoaded(Scene _, LoadSceneMode __)
    {
        Apply();
    }

    [ContextMenu("Apply Flat Lighting")]
    public void Apply()
    {
        ApplySkybox();
        ApplyPostEffects();

        if (disableAllShadows)
        {
            QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
            QualitySettings.shadowDistance = 0f;
        }

        if (disableFog)
            RenderSettings.fog = false;

        if (flattenAmbient)
        {
            if (useTrilightAmbient)
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = ambientSkyColor;
                RenderSettings.ambientEquatorColor = ambientEquatorColor;
                RenderSettings.ambientGroundColor = ambientGroundColor;
            }
            else
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = ambientColor;
            }
            RenderSettings.ambientIntensity = ambientIntensity;
        }

        if (disableReflections)
            RenderSettings.reflectionIntensity = 0f;

        var lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var light in lights)
        {
            if (light == null)
                continue;

            if (disableAllShadows)
                light.shadows = LightShadows.None;

            if (light.type != LightType.Directional)
                continue;

            if (disableDirectionalLights)
            {
                light.intensity = 0f;
                light.enabled = false;
                continue;
            }

            if (reduceDirectionalContrast)
                light.intensity = Mathf.Min(light.intensity, maxDirectionalIntensity);
        }

        if (!forceRendererNoShadows)
            return;

        var renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var renderer in renderers)
        {
            if (renderer == null)
                continue;

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private System.Collections.IEnumerator ReapplyRoutine()
    {
        var wait = new WaitForSeconds(Mathf.Max(0.25f, reapplyIntervalSec));
        while (true)
        {
            Apply();
            yield return wait;
        }
    }

    private void ApplySkybox()
    {
        if (!overrideSkybox)
            return;

        if (customSkyboxMaterial != null)
        {
            if (RenderSettings.skybox != customSkyboxMaterial)
            {
                RenderSettings.skybox = customSkyboxMaterial;
                DynamicGI.UpdateEnvironment();
            }
            return;
        }

        var shader = Shader.Find("Skybox/Procedural");
        if (shader == null)
            return;

        if (_runtimeSkyboxMaterial == null)
        {
            _runtimeSkyboxMaterial = new Material(shader)
            {
                name = "RuntimeSkyboxPreset"
            };
            _activeSkyboxPreset = (SkyboxPreset)(-1);
        }

        var changed = _activeSkyboxPreset != skyboxPreset;
        if (changed)
        {
            ConfigureSkyboxPreset(_runtimeSkyboxMaterial, skyboxPreset);
            _activeSkyboxPreset = skyboxPreset;
        }

        if (RenderSettings.skybox != _runtimeSkyboxMaterial || changed)
        {
            RenderSettings.skybox = _runtimeSkyboxMaterial;
            DynamicGI.UpdateEnvironment();
        }
    }

    private void ConfigureSkyboxPreset(Material skybox, SkyboxPreset preset)
    {
        if (skybox == null)
            return;

        skybox.SetFloat("_SunDisk", hideSkyboxSunDisk ? 0f : 1f);
        skybox.SetFloat("_SunSize", 0.03f);
        skybox.SetFloat("_SunSizeConvergence", 5f);

        switch (preset)
        {
            case SkyboxPreset.WarmSunset:
                skybox.SetColor("_SkyTint", new Color(1f, 0.7f, 0.52f));
                skybox.SetColor("_GroundColor", new Color(0.62f, 0.54f, 0.49f));
                skybox.SetFloat("_AtmosphereThickness", 0.9f);
                skybox.SetFloat("_Exposure", 0.95f);
                break;
            case SkyboxPreset.MintDay:
                skybox.SetColor("_SkyTint", new Color(0.58f, 0.9f, 0.8f));
                skybox.SetColor("_GroundColor", new Color(0.57f, 0.63f, 0.58f));
                skybox.SetFloat("_AtmosphereThickness", 0.72f);
                skybox.SetFloat("_Exposure", 0.98f);
                break;
            default:
                skybox.SetColor("_SkyTint", new Color(0.5f, 0.66f, 1f));
                skybox.SetColor("_GroundColor", new Color(0.61f, 0.64f, 0.69f));
                skybox.SetFloat("_AtmosphereThickness", 0.78f);
                skybox.SetFloat("_Exposure", 1.02f);
                break;
        }
    }

    private void ApplyPostEffects()
    {
        if (!enablePostEffects)
        {
            if (_runtimePostVolume != null)
                _runtimePostVolume.enabled = false;
            return;
        }

        EnsureRuntimePostVolume();
        EnsurePostProcessingOnCameras();

        if (_runtimePostVolume == null || _runtimePostProfile == null)
            return;

        _runtimePostVolume.enabled = true;

        var colorAdjustments = GetOrAddVolumeComponent<ColorAdjustments>(_runtimePostProfile);
        colorAdjustments.active = true;
        colorAdjustments.postExposure.Override(postExposure);
        colorAdjustments.contrast.Override(postContrast);
        colorAdjustments.saturation.Override(postSaturation);

        var tone = GetOrAddVolumeComponent<Tonemapping>(_runtimePostProfile);
        tone.active = enableTonemapping;
        tone.mode.Override(tonemappingMode);

        var vignette = GetOrAddVolumeComponent<Vignette>(_runtimePostProfile);
        vignette.active = enableVignette;
        vignette.color.Override(Color.black);
        vignette.intensity.Override(vignetteIntensity);
        vignette.smoothness.Override(vignetteSmoothness);
        vignette.rounded.Override(false);

        var bloom = GetOrAddVolumeComponent<Bloom>(_runtimePostProfile);
        var bloomAllowed = enableBloom && (Application.platform != RuntimePlatform.WebGLPlayer || allowBloomInWebGL);
        bloom.active = bloomAllowed;
        bloom.intensity.Override(bloomIntensity);
        bloom.threshold.Override(bloomThreshold);
        bloom.scatter.Override(0.65f);
    }

    private void EnsureRuntimePostVolume()
    {
        if (_runtimePostProfile == null)
        {
            _runtimePostProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _runtimePostProfile.name = "FlatLightingPostProfile";
        }

        if (_runtimePostVolume == null)
        {
            var volumeGo = new GameObject("FlatLightingPostVolume");
            volumeGo.transform.SetParent(transform, false);
            _runtimePostVolume = volumeGo.AddComponent<Volume>();
            _runtimePostVolume.isGlobal = true;
            _runtimePostVolume.priority = 100f;
        }

        if (_runtimePostVolume.sharedProfile != _runtimePostProfile)
            _runtimePostVolume.sharedProfile = _runtimePostProfile;
    }

    private void EnsurePostProcessingOnCameras()
    {
        var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var camera in cameras)
        {
            if (camera == null)
                continue;

            var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData != null)
                cameraData.renderPostProcessing = true;
        }
    }

    private static T GetOrAddVolumeComponent<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet<T>(out var component))
            component = profile.Add<T>(true);
        return component;
    }
}
