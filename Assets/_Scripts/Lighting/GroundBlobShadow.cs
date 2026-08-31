using UnityEngine;
using UnityEngine.Rendering;

public enum GroundBlobShadowPreset
{
    Player,
    Egg,
    Animal,
    BigPet
}

/// <summary>
/// Cheap unlit contact shadow. It uses one shared quad/material and only probes
/// the ground while its owner is moving, so it does not add realtime light shadows.
/// </summary>
[DisallowMultipleComponent]
public sealed class GroundBlobShadow : MonoBehaviour
{
    public const string VisualObjectName = "GroundBlobShadowVisual";

    private const string MaterialResourcePath = "Materials/GroundBlobShadow";
    private const float GroundOffset = 0.025f;
    private const float MovementThresholdSqr = 0.0025f;
    private const float ScaleThresholdSqr = 0.0001f;
    private const float RayStartHeight = 3f;
    private const float RayDistance = 30f;

    private static readonly RaycastHit[] GroundHits = new RaycastHit[16];
    private static Mesh _sharedMesh;
    private static Material _sharedMaterial;
    private static int _instanceSequence;

    [SerializeField] private GroundBlobShadowPreset _preset = GroundBlobShadowPreset.Animal;

    private Transform _visual;
    private MeshRenderer _visualRenderer;
    private Vector2 _worldSize = Vector2.one;
    private Vector3 _localAnchor;
    private Vector3 _lastOwnerPosition;
    private Vector3 _lastOwnerScale;
    private float _lastOwnerYaw;
    private float _groundY;
    private float _nextProbeTime;
    private int _nextUpdateFrame;
    private int _updateCadence = 4;
    private float _probeInterval = 0.14f;
    private bool _hasGround;
    private bool _configured;

    public static GroundBlobShadow Ensure(GameObject owner, GroundBlobShadowPreset preset)
    {
        if (owner == null)
            return null;

        GroundBlobShadow shadow = owner.GetComponent<GroundBlobShadow>();
        if (shadow == null)
            shadow = owner.AddComponent<GroundBlobShadow>();

        shadow.Configure(preset);
        return shadow;
    }

    public static bool IsShadowRenderer(Renderer renderer)
    {
        return renderer != null && renderer.gameObject.name == VisualObjectName;
    }

    public void Configure(GroundBlobShadowPreset preset)
    {
        _preset = preset;
        _configured = true;
        ApplyPresetTiming();
        EnsureVisual();
        DisableRealtimeShadowCasting();
        RefreshFootprint();
        ForceGroundRefresh();
    }

    public void RefreshFootprint()
    {
        GetPresetDefaults(out Vector2 defaultSize, out Vector2 minimumSize, out Vector2 maximumSize,
            out float footprintMultiplier);

        _worldSize = defaultSize;
        _localAnchor = Vector3.zero;

        if (_preset != GroundBlobShadowPreset.Player && TryGetGeometryBounds(out Bounds bounds))
        {
            _worldSize = new Vector2(
                Mathf.Clamp(bounds.size.x * footprintMultiplier, minimumSize.x, maximumSize.x),
                Mathf.Clamp(bounds.size.z * footprintMultiplier, minimumSize.y, maximumSize.y));

            Vector3 center = bounds.center;
            center.y = transform.position.y;
            _localAnchor = transform.InverseTransformPoint(center);
            _localAnchor.y = 0f;
        }

        _lastOwnerScale = transform.lossyScale;
        UpdateVisualTransform(GetWorldAnchor());
    }

    private void Awake()
    {
        ApplyPresetTiming();
        EnsureVisual();
    }

    private void OnEnable()
    {
        EnsureVisual();
        if (_visualRenderer != null)
            _visualRenderer.enabled = true;

        _lastOwnerPosition = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        _nextUpdateFrame = Time.frameCount + (_instanceSequence++ % Mathf.Max(1, _updateCadence));
        ForceGroundRefresh();
    }

    private void Start()
    {
        DisableRealtimeShadowCasting();
        if (!_configured)
            RefreshFootprint();
        ForceGroundRefresh();
    }

    private void OnDisable()
    {
        if (_visualRenderer != null)
            _visualRenderer.enabled = false;
    }

    private void LateUpdate()
    {
        if (Time.frameCount < _nextUpdateFrame)
            return;

        _nextUpdateFrame = Time.frameCount + _updateCadence;
        Vector3 ownerPosition = transform.position;
        Vector3 ownerScale = transform.lossyScale;
        float ownerYaw = transform.eulerAngles.y;
        bool moved = (ownerPosition - _lastOwnerPosition).sqrMagnitude > MovementThresholdSqr;
        bool scaleChanged = (ownerScale - _lastOwnerScale).sqrMagnitude > ScaleThresholdSqr;
        bool rotated = Mathf.Abs(Mathf.DeltaAngle(ownerYaw, _lastOwnerYaw)) > 1f;

        if (scaleChanged)
            RefreshFootprint();

        Vector3 anchor = GetWorldAnchor();
        if (moved && Time.unscaledTime >= _nextProbeTime)
            ProbeGround(anchor);

        if (moved || rotated || scaleChanged)
            UpdateVisualTransform(anchor);

        _lastOwnerPosition = ownerPosition;
        _lastOwnerScale = ownerScale;
        _lastOwnerYaw = ownerYaw;
    }

    private void ForceGroundRefresh()
    {
        _nextProbeTime = 0f;
        Vector3 anchor = GetWorldAnchor();
        ProbeGround(anchor);
        UpdateVisualTransform(anchor);
        _lastOwnerPosition = transform.position;
        _lastOwnerScale = transform.lossyScale;
        _lastOwnerYaw = transform.eulerAngles.y;
    }

    private void ProbeGround(Vector3 anchor)
    {
        _nextProbeTime = Time.unscaledTime + _probeInterval;
        Vector3 origin = new Vector3(anchor.x, Mathf.Max(anchor.y, transform.position.y) + RayStartHeight, anchor.z);
        int layerMask = Physics.DefaultRaycastLayers;
        ExcludeLayer(ref layerMask, "Player");
        ExcludeLayer(ref layerMask, "Creature");
        ExcludeLayer(ref layerMask, "Water");
        ExcludeLayer(ref layerMask, "UI");

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            GroundHits,
            RayDistance,
            layerMask,
            QueryTriggerInteraction.Ignore);

        float closestDistance = float.PositiveInfinity;
        bool found = false;
        float foundY = 0f;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = GroundHits[i];
            if (hit.collider == null || hit.normal.y < 0.25f || IsOwnedCollider(hit.collider))
                continue;

            if (hit.distance >= closestDistance)
                continue;

            closestDistance = hit.distance;
            foundY = hit.point.y;
            found = true;
        }

        _hasGround = found;
        if (found)
            _groundY = foundY;

        if (_visualRenderer != null)
            _visualRenderer.enabled = isActiveAndEnabled && _hasGround;
    }

    private bool IsOwnedCollider(Collider target)
    {
        if (target == null)
            return false;

        Transform targetTransform = target.transform;
        return targetTransform == transform || targetTransform.IsChildOf(transform);
    }

    private Vector3 GetWorldAnchor()
    {
        Vector3 anchor = transform.TransformPoint(_localAnchor);
        anchor.y = transform.position.y;
        return anchor;
    }

    private void UpdateVisualTransform(Vector3 anchor)
    {
        if (_visual == null)
            return;

        _visual.SetPositionAndRotation(
            new Vector3(anchor.x, _groundY + GroundOffset, anchor.z),
            Quaternion.Euler(0f, transform.eulerAngles.y, 0f));

        Vector3 parentScale = transform.lossyScale;
        _visual.localScale = new Vector3(
            SafeDivide(_worldSize.x, parentScale.x),
            1f,
            SafeDivide(_worldSize.y, parentScale.z));
    }

    private bool TryGetGeometryBounds(out Bounds bounds)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool found = false;
        bounds = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer == _visualRenderer || !renderer.enabled ||
                !renderer.gameObject.activeInHierarchy || renderer is ParticleSystemRenderer ||
                renderer is SpriteRenderer || renderer is TrailRenderer || renderer is LineRenderer)
            {
                continue;
            }

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found;
    }

    private void DisableRealtimeShadowCasting()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer == _visualRenderer)
                continue;

            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    private void ApplyPresetTiming()
    {
        switch (_preset)
        {
            case GroundBlobShadowPreset.Player:
                _updateCadence = 1;
                _probeInterval = 0.07f;
                break;
            case GroundBlobShadowPreset.BigPet:
                _updateCadence = 6;
                _probeInterval = 0.2f;
                break;
            default:
                _updateCadence = 4;
                _probeInterval = 0.14f;
                break;
        }
    }

    private void GetPresetDefaults(
        out Vector2 defaultSize,
        out Vector2 minimumSize,
        out Vector2 maximumSize,
        out float footprintMultiplier)
    {
        switch (_preset)
        {
            case GroundBlobShadowPreset.Player:
                defaultSize = new Vector2(1.05f, 0.68f);
                minimumSize = defaultSize;
                maximumSize = defaultSize;
                footprintMultiplier = 1f;
                break;
            case GroundBlobShadowPreset.Egg:
                defaultSize = new Vector2(0.9f, 0.72f);
                minimumSize = new Vector2(0.65f, 0.55f);
                maximumSize = new Vector2(1.65f, 1.4f);
                footprintMultiplier = 0.78f;
                break;
            case GroundBlobShadowPreset.BigPet:
                defaultSize = new Vector2(3.4f, 2.8f);
                minimumSize = new Vector2(1.6f, 1.35f);
                maximumSize = new Vector2(9f, 7f);
                footprintMultiplier = 0.72f;
                break;
            default:
                defaultSize = new Vector2(1f, 0.72f);
                minimumSize = new Vector2(0.65f, 0.5f);
                maximumSize = new Vector2(3f, 2.5f);
                footprintMultiplier = 0.72f;
                break;
        }
    }

    private void EnsureVisual()
    {
        if (_visual != null && _visualRenderer != null)
            return;

        Transform existing = transform.Find(VisualObjectName);
        GameObject visualObject;
        if (existing != null)
        {
            visualObject = existing.gameObject;
            _visual = existing;
        }
        else
        {
            visualObject = new GameObject(VisualObjectName);
            visualObject.hideFlags = HideFlags.HideInHierarchy;
            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreRaycastLayer >= 0)
                visualObject.layer = ignoreRaycastLayer;
            _visual = visualObject.transform;
            _visual.SetParent(transform, false);
        }

        MeshFilter filter = visualObject.GetComponent<MeshFilter>();
        if (filter == null)
            filter = visualObject.AddComponent<MeshFilter>();
        filter.sharedMesh = GetSharedMesh();

        _visualRenderer = visualObject.GetComponent<MeshRenderer>();
        if (_visualRenderer == null)
            _visualRenderer = visualObject.AddComponent<MeshRenderer>();
        _visualRenderer.sharedMaterial = GetSharedMaterial();
        _visualRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _visualRenderer.receiveShadows = false;
        _visualRenderer.lightProbeUsage = LightProbeUsage.Off;
        _visualRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        _visualRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        _visualRenderer.enabled = isActiveAndEnabled;
    }

    private static Mesh GetSharedMesh()
    {
        if (_sharedMesh != null)
            return _sharedMesh;

        _sharedMesh = new Mesh
        {
            name = "Shared Ground Blob Shadow Quad",
            hideFlags = HideFlags.HideAndDontSave,
            vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, -0.5f)
            },
            uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f)
            },
            triangles = new[] { 0, 1, 2, 0, 2, 3 },
            normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up }
        };
        _sharedMesh.RecalculateBounds();
        _sharedMesh.UploadMeshData(true);
        return _sharedMesh;
    }

    private static Material GetSharedMaterial()
    {
        if (_sharedMaterial != null)
            return _sharedMaterial;

        _sharedMaterial = Resources.Load<Material>(MaterialResourcePath);
        if (_sharedMaterial != null)
            return _sharedMaterial;

        Shader shader = Shader.Find("Zoo/Ground Blob Shadow");
        if (shader == null)
        {
            Debug.LogError("[GroundBlobShadow] Shadow shader/material is missing.");
            return null;
        }

        _sharedMaterial = new Material(shader)
        {
            name = "Runtime Ground Blob Shadow",
            hideFlags = HideFlags.HideAndDontSave
        };
        return _sharedMaterial;
    }

    private static void ExcludeLayer(ref int mask, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0)
            mask &= ~(1 << layer);
    }

    private static float SafeDivide(float value, float divisor)
    {
        return value / Mathf.Max(0.0001f, Mathf.Abs(divisor));
    }
}
