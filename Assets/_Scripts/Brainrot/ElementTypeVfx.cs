using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class ElementTypeVfx : MonoBehaviour
{
    private const string EffectRootName = "ElementVfx";
    private const string RaysRootName = "ElementRays";
    private const string GroundRootName = "ElementGround";
    private const string ParticleTextureResourcePath = "ElementVfx/Label_Ribbon01_White_Deco_Star";
    private const float VisibilityCheckInterval = 0.5f;
    private const int RayCount = 5;

    private static Material _sharedParticleMaterial;
    private static Texture2D _sharedParticleTexture;
    private static Sprite _sharedRaySprite;
    private static Sprite _sharedGroundSprite;

    [SerializeField] private Transform targetRoot;
    [SerializeField] private bool enableDistanceCulling = true;
    [SerializeField] private float maxVisibleDistance = 45f;
    [SerializeField] private float verticalOffset = 0.15f;
    [SerializeField] private float radiusPadding = 0.15f;

    private ElementType _currentElement = ElementType.ElementType;
    private Transform _effectRoot;
    private Transform _raysRoot;
    private Transform _groundRoot;
    private ParticleSystem _particles;
    private SpriteRenderer[] _rayRenderers;
    private SpriteRenderer _groundRenderer;
    private Camera _mainCamera;
    private float _nextVisibilityCheckTime;
    private float _sizeMultiplier = 1f;
    private float _particleRadiusMultiplier = 1f;
    private float _emissionMultiplier = 1f;
    private float _rayAlphaMultiplier = 1f;
    private float _rayLengthWorldOverride;
    private float _rayThicknessWorldOverride;
    private float _raySpinSpeed;
    private float _raySpinAngle;
    private float _groundAlphaMultiplier = 1f;
    private float _groundRadiusWorldOverride;
    private float _groundYOffsetWorld = 0.02f;

    public static ElementTypeVfx Ensure(
        Component owner,
        Transform effectTargetRoot,
        ElementType elementType,
        float sizeMultiplier = 1f,
        float emissionMultiplier = 1f,
        float rayAlphaMultiplier = 1f,
        float particleRadiusMultiplier = 1f,
        float rayLengthWorldOverride = 0f,
        float rayThicknessWorldOverride = 0f,
        float raySpinSpeed = 0f,
        float groundAlphaMultiplier = 1f,
        float groundRadiusWorldOverride = 0f,
        float groundYOffsetWorld = 0.02f)
    {
        if (owner == null)
            return null;

        ElementTypeVfx vfx = owner.GetComponent<ElementTypeVfx>();
        if (vfx == null && !ShouldShowEffect(elementType))
            return null;

        if (vfx == null)
            vfx = owner.gameObject.AddComponent<ElementTypeVfx>();

        vfx.Apply(
            effectTargetRoot,
            elementType,
            sizeMultiplier,
            emissionMultiplier,
            rayAlphaMultiplier,
            particleRadiusMultiplier,
            rayLengthWorldOverride,
            rayThicknessWorldOverride,
            raySpinSpeed,
            groundAlphaMultiplier,
            groundRadiusWorldOverride,
            groundYOffsetWorld);
        return vfx;
    }

    public void Apply(
        Transform effectTargetRoot,
        ElementType elementType,
        float sizeMultiplier = 1f,
        float emissionMultiplier = 1f,
        float rayAlphaMultiplier = 1f,
        float particleRadiusMultiplier = 1f,
        float rayLengthWorldOverride = 0f,
        float rayThicknessWorldOverride = 0f,
        float raySpinSpeed = 0f,
        float groundAlphaMultiplier = 1f,
        float groundRadiusWorldOverride = 0f,
        float groundYOffsetWorld = 0.02f)
    {
        targetRoot = effectTargetRoot != null ? effectTargetRoot : transform;
        _currentElement = elementType;
        _sizeMultiplier = Mathf.Max(0.1f, sizeMultiplier);
        _particleRadiusMultiplier = Mathf.Max(0.1f, particleRadiusMultiplier);
        _emissionMultiplier = Mathf.Max(0.1f, emissionMultiplier);
        _rayAlphaMultiplier = Mathf.Max(0f, rayAlphaMultiplier);
        _rayLengthWorldOverride = Mathf.Max(0f, rayLengthWorldOverride);
        _rayThicknessWorldOverride = Mathf.Max(0f, rayThicknessWorldOverride);
        _raySpinSpeed = raySpinSpeed;
        _groundAlphaMultiplier = Mathf.Max(0f, groundAlphaMultiplier);
        _groundRadiusWorldOverride = Mathf.Max(0f, groundRadiusWorldOverride);
        _groundYOffsetWorld = groundYOffsetWorld;

        if (!ShouldShowEffect(elementType))
        {
            StopAndHide();
            return;
        }

        EnsureParticleSystem();
        _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ConfigureParticleSystem(elementType);
        ConfigureRays(elementType);
        ConfigureGround(elementType);
        _effectRoot.gameObject.SetActive(true);
        _particles.Play(true);
    }

    private void LateUpdate()
    {
        if (!enableDistanceCulling || _particles == null || _effectRoot == null || !ShouldShowEffect(_currentElement))
            return;

        if (_mainCamera == null)
            _mainCamera = Camera.main;

        if (_mainCamera == null)
            return;

        if (Time.unscaledTime >= _nextVisibilityCheckTime)
        {
            _nextVisibilityCheckTime = Time.unscaledTime + VisibilityCheckInterval;
            float sqrDistance = (_mainCamera.transform.position - transform.position).sqrMagnitude;
            bool inRange = sqrDistance <= maxVisibleDistance * maxVisibleDistance;
            if (_effectRoot.gameObject.activeSelf != inRange)
                _effectRoot.gameObject.SetActive(inRange);
            if (_raysRoot != null && _rayAlphaMultiplier > 0f && _raysRoot.gameObject.activeSelf != inRange)
                _raysRoot.gameObject.SetActive(inRange);
            if (_groundRoot != null && _groundAlphaMultiplier > 0f && _groundRoot.gameObject.activeSelf != inRange)
                _groundRoot.gameObject.SetActive(inRange);
        }

        if (_effectRoot.gameObject.activeSelf)
        {
            _raySpinAngle = Mathf.Repeat(_raySpinAngle + _raySpinSpeed * Time.deltaTime, 360f);
            FaceRaysToCamera();
        }
    }

    private void EnsureParticleSystem()
    {
        if (_particles != null && _effectRoot != null)
            return;

        Transform root = targetRoot != null ? targetRoot : transform;
        _effectRoot = root.Find(EffectRootName);
        if (_effectRoot == null)
        {
            GameObject effectObject = new GameObject(EffectRootName);
            _effectRoot = effectObject.transform;
            _effectRoot.SetParent(root, false);
        }

        _effectRoot.localPosition = Vector3.zero;
        _effectRoot.localRotation = Quaternion.identity;
        _effectRoot.localScale = Vector3.one;

        _particles = _effectRoot.GetComponent<ParticleSystem>();
        if (_particles == null)
            _particles = _effectRoot.gameObject.AddComponent<ParticleSystem>();

        ParticleSystemRenderer particleRenderer = _effectRoot.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer == null)
            particleRenderer = _effectRoot.gameObject.AddComponent<ParticleSystemRenderer>();

        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sortMode = ParticleSystemSortMode.YoungestInFront;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.maxParticleSize = Mathf.Clamp(0.18f * _sizeMultiplier, 0.18f, 0.55f);
        Material particleMaterial = GetSharedParticleMaterial();
        if (particleMaterial != null)
            particleRenderer.sharedMaterial = particleMaterial;
    }

    private void ConfigureParticleSystem(ElementType elementType)
    {
        ElementVfxSettings settings = GetSettings(elementType);
        Bounds bounds = CalculateLocalBounds(targetRoot != null ? targetRoot : transform);
        float width = Mathf.Max(bounds.size.x, bounds.size.z);
        float radius = Mathf.Max(0.25f, width * 0.55f + radiusPadding) * settings.RadiusMultiplier * _particleRadiusMultiplier;
        float height = Mathf.Max(0.35f, bounds.size.y);

        ParticleSystem.MainModule main = _particles.main;
        main.loop = true;
        main.prewarm = true;
        main.duration = 2f;
        main.maxParticles = Mathf.CeilToInt(settings.MaxParticles * _emissionMultiplier);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startLifetime = new ParticleSystem.MinMaxCurve(settings.MinLifetime, settings.MaxLifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(settings.MinSpeed, settings.MaxSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(settings.MinSize * _sizeMultiplier, settings.MaxSize * _sizeMultiplier);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(settings.PrimaryColor, settings.SecondaryColor);
        main.gravityModifier = settings.Gravity;

        ParticleSystem.EmissionModule emission = _particles.emission;
        emission.enabled = true;
        emission.rateOverTime = settings.RateOverTime * _emissionMultiplier;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0.25f, (short)1, (short)2),
            new ParticleSystem.Burst(1.15f, (short)1, (short)2)
        });

        ParticleSystem.ShapeModule shape = _particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;
        shape.radiusThickness = settings.RadiusThickness;
        shape.position = bounds.center + Vector3.up * (height * settings.HeightOffset + verticalOffset);

        ParticleSystem.VelocityOverLifetimeModule velocity = _particles.velocityOverLifetime;
        velocity.enabled = Mathf.Abs(settings.UpwardVelocity) > 0.0001f;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(0f);
        velocity.y = new ParticleSystem.MinMaxCurve(settings.UpwardVelocity);
        velocity.z = new ParticleSystem.MinMaxCurve(0f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = _particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = BuildFadeGradient(settings.PrimaryColor, settings.SecondaryColor);

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = _particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = BuildSizeCurve(settings.EndSizeMultiplier);

        ParticleSystem.RotationOverLifetimeModule rotation = _particles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-settings.RotationSpeed, settings.RotationSpeed);

        _particles.Clear(true);
    }

    private void ConfigureRays(ElementType elementType)
    {
        ElementVfxSettings settings = GetSettings(elementType);
        if (_rayAlphaMultiplier <= 0f || settings.RayAlpha <= 0f)
        {
            SetRaysActive(false);
            return;
        }

        EnsureRaysRoot();
        Bounds bounds = CalculateLocalBounds(targetRoot != null ? targetRoot : transform);
        float width = Mathf.Max(bounds.size.x, bounds.size.z);
        float height = Mathf.Max(0.35f, bounds.size.y);
        float rayLength = _rayLengthWorldOverride > 0f
            ? WorldToLocalSize(_rayLengthWorldOverride, Vector3.up)
            : Mathf.Max(0.45f, width * settings.RayLengthMultiplier) * _sizeMultiplier;
        float rayWidth = _rayThicknessWorldOverride > 0f
            ? WorldToLocalSize(_rayThicknessWorldOverride, Vector3.right)
            : Mathf.Max(0.08f, width * settings.RayWidthMultiplier) * _sizeMultiplier;

        _raysRoot.localPosition = bounds.center + Vector3.up * (height * 0.15f + verticalOffset);
        _raysRoot.localScale = Vector3.one;

        for (int i = 0; i < _rayRenderers.Length; i++)
        {
            SpriteRenderer ray = _rayRenderers[i];
            if (ray == null)
                continue;

            float angle = (360f / _rayRenderers.Length) * i + settings.RayAngleOffset;
            ray.transform.localPosition = Vector3.zero;
            ray.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            ray.transform.localScale = new Vector3(rayWidth, rayLength, 1f);
            ray.color = new Color(settings.PrimaryColor.r, settings.PrimaryColor.g, settings.PrimaryColor.b, Mathf.Clamp01(settings.RayAlpha * _rayAlphaMultiplier));
            ray.enabled = true;
        }

        _raysRoot.gameObject.SetActive(true);
        FaceRaysToCamera();
    }

    private void ConfigureGround(ElementType elementType)
    {
        ElementVfxSettings settings = GetSettings(elementType);
        if (_groundAlphaMultiplier <= 0f || settings.GroundAlpha <= 0f)
        {
            SetGroundActive(false);
            return;
        }

        EnsureGroundRoot();

        Bounds bounds = CalculateLocalBounds(targetRoot != null ? targetRoot : transform);
        float width = Mathf.Max(bounds.size.x, bounds.size.z);
        float radius = _groundRadiusWorldOverride > 0f
            ? WorldToLocalSize(_groundRadiusWorldOverride, Vector3.right)
            : Mathf.Max(0.25f, width * settings.GroundRadiusMultiplier);

        _groundRoot.localPosition = new Vector3(
            bounds.center.x,
            bounds.min.y + WorldToLocalSize(_groundYOffsetWorld, Vector3.up),
            bounds.center.z);
        _groundRoot.localRotation = Quaternion.Euler(90f, 0f, 0f);
        _groundRoot.localScale = new Vector3(radius * 2f, radius * 2f, 1f);

        _groundRenderer.sprite = GetSharedGroundSprite();
        _groundRenderer.color = new Color(settings.PrimaryColor.r, settings.PrimaryColor.g, settings.PrimaryColor.b, Mathf.Clamp01(settings.GroundAlpha * _groundAlphaMultiplier));
        _groundRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _groundRenderer.receiveShadows = false;
        _groundRenderer.enabled = true;
        _groundRoot.gameObject.SetActive(true);
    }

    private void StopAndHide()
    {
        if (_particles != null)
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (_effectRoot != null)
            _effectRoot.gameObject.SetActive(false);

        SetRaysActive(false);
        SetGroundActive(false);
    }

    private void EnsureRaysRoot()
    {
        if (_raysRoot != null && _rayRenderers != null && _rayRenderers.Length == RayCount)
            return;

        Transform root = targetRoot != null ? targetRoot : transform;
        _raysRoot = root.Find(RaysRootName);
        if (_raysRoot == null)
        {
            GameObject raysObject = new GameObject(RaysRootName);
            _raysRoot = raysObject.transform;
            _raysRoot.SetParent(root, false);
        }

        _rayRenderers = new SpriteRenderer[RayCount];
        for (int i = 0; i < RayCount; i++)
        {
            Transform rayTransform = _raysRoot.Find("Ray " + i);
            if (rayTransform == null)
            {
                GameObject rayObject = new GameObject("Ray " + i);
                rayTransform = rayObject.transform;
                rayTransform.SetParent(_raysRoot, false);
            }

            SpriteRenderer ray = rayTransform.GetComponent<SpriteRenderer>();
            if (ray == null)
                ray = rayTransform.gameObject.AddComponent<SpriteRenderer>();

            ray.sprite = GetSharedRaySprite();
            ray.shadowCastingMode = ShadowCastingMode.Off;
            ray.receiveShadows = false;
            _rayRenderers[i] = ray;
        }
    }

    private void FaceRaysToCamera()
    {
        if (_raysRoot == null || !_raysRoot.gameObject.activeSelf)
            return;

        if (_mainCamera == null)
            _mainCamera = Camera.main;
        if (_mainCamera == null)
            return;

        _raysRoot.rotation = _mainCamera.transform.rotation * Quaternion.Euler(0f, 0f, _raySpinAngle);
    }

    private void SetRaysActive(bool isActive)
    {
        if (_raysRoot != null)
            _raysRoot.gameObject.SetActive(isActive);
    }

    private void EnsureGroundRoot()
    {
        if (_groundRoot != null && _groundRenderer != null)
            return;

        Transform root = targetRoot != null ? targetRoot : transform;
        _groundRoot = root.Find(GroundRootName);
        if (_groundRoot == null)
        {
            GameObject groundObject = new GameObject(GroundRootName);
            _groundRoot = groundObject.transform;
            _groundRoot.SetParent(root, false);
        }

        _groundRenderer = _groundRoot.GetComponent<SpriteRenderer>();
        if (_groundRenderer == null)
            _groundRenderer = _groundRoot.gameObject.AddComponent<SpriteRenderer>();
    }

    private void SetGroundActive(bool isActive)
    {
        if (_groundRoot != null)
            _groundRoot.gameObject.SetActive(isActive);
    }

    private float WorldToLocalSize(float worldSize, Vector3 localAxis)
    {
        Transform root = targetRoot != null ? targetRoot : transform;
        float worldUnitsPerLocalUnit = root.TransformVector(localAxis.normalized).magnitude;
        if (worldUnitsPerLocalUnit <= 0.0001f)
            return worldSize;

        return worldSize / worldUnitsPerLocalUnit;
    }

    private static bool ShouldShowEffect(ElementType elementType)
    {
        return elementType != ElementType.ElementType && elementType != ElementType.NoElement;
    }

    private static ElementVfxSettings GetSettings(ElementType elementType)
    {
        switch (elementType)
        {
            case ElementType.Gold:
                return new ElementVfxSettings
                {
                    PrimaryColor = new Color(1f, 0.84f, 0.12f, 0.85f),
                    SecondaryColor = new Color(1f, 0.98f, 0.45f, 0.55f),
                    RateOverTime = 8f,
                    MaxParticles = 18,
                    MinLifetime = 0.7f,
                    MaxLifetime = 1.25f,
                    MinSpeed = 0.04f,
                    MaxSpeed = 0.18f,
                    MinSize = 0.035f,
                    MaxSize = 0.075f,
                    RadiusMultiplier = 1f,
                    RadiusThickness = 0.35f,
                    HeightOffset = 0.08f,
                    UpwardVelocity = 0.08f,
                    RotationSpeed = 80f,
                    EndSizeMultiplier = 0.35f,
                    RayAlpha = 0.28f,
                    RayLengthMultiplier = 0.95f,
                    RayWidthMultiplier = 0.08f,
                    RayAngleOffset = 0f,
                    GroundAlpha = 0.28f,
                    GroundRadiusMultiplier = 0.65f
                };
            case ElementType.Diamond:
                return new ElementVfxSettings
                {
                    PrimaryColor = new Color(0.35f, 0.9f, 1f, 0.75f),
                    SecondaryColor = new Color(0.1f, 0.45f, 1f, 0.55f),
                    RateOverTime = 7f,
                    MaxParticles = 16,
                    MinLifetime = 0.9f,
                    MaxLifetime = 1.5f,
                    MinSpeed = 0.02f,
                    MaxSpeed = 0.12f,
                    MinSize = 0.03f,
                    MaxSize = 0.065f,
                    RadiusMultiplier = 1.05f,
                    RadiusThickness = 0.2f,
                    HeightOffset = 0.04f,
                    UpwardVelocity = 0.03f,
                    RotationSpeed = 55f,
                    EndSizeMultiplier = 0.45f,
                    RayAlpha = 0.2f,
                    RayLengthMultiplier = 0.85f,
                    RayWidthMultiplier = 0.065f,
                    RayAngleOffset = 12f,
                    GroundAlpha = 0.24f,
                    GroundRadiusMultiplier = 0.68f
                };
            case ElementType.Electric:
                return new ElementVfxSettings
                {
                    PrimaryColor = new Color(0.95f, 0.15f, 1f, 0.9f),
                    SecondaryColor = new Color(0.1f, 0.85f, 1f, 0.7f),
                    RateOverTime = 14f,
                    MaxParticles = 24,
                    MinLifetime = 0.25f,
                    MaxLifetime = 0.55f,
                    MinSpeed = 0.08f,
                    MaxSpeed = 0.38f,
                    MinSize = 0.025f,
                    MaxSize = 0.06f,
                    RadiusMultiplier = 1.12f,
                    RadiusThickness = 0.65f,
                    HeightOffset = 0.02f,
                    UpwardVelocity = 0.02f,
                    RotationSpeed = 160f,
                    EndSizeMultiplier = 0.15f,
                    RayAlpha = 0.24f,
                    RayLengthMultiplier = 0.75f,
                    RayWidthMultiplier = 0.055f,
                    RayAngleOffset = 7f,
                    GroundAlpha = 0.26f,
                    GroundRadiusMultiplier = 0.7f
                };
            case ElementType.Fire:
                return new ElementVfxSettings
                {
                    PrimaryColor = new Color(1f, 0.23f, 0.05f, 0.82f),
                    SecondaryColor = new Color(1f, 0.65f, 0.1f, 0.58f),
                    RateOverTime = 12f,
                    MaxParticles = 22,
                    MinLifetime = 0.45f,
                    MaxLifetime = 0.9f,
                    MinSpeed = 0.05f,
                    MaxSpeed = 0.22f,
                    MinSize = 0.035f,
                    MaxSize = 0.085f,
                    RadiusMultiplier = 0.9f,
                    RadiusThickness = 0.5f,
                    HeightOffset = -0.08f,
                    UpwardVelocity = 0.28f,
                    RotationSpeed = 110f,
                    EndSizeMultiplier = 0.2f,
                    RayAlpha = 0.22f,
                    RayLengthMultiplier = 0.9f,
                    RayWidthMultiplier = 0.075f,
                    RayAngleOffset = 18f,
                    GroundAlpha = 0.26f,
                    GroundRadiusMultiplier = 0.62f
                };
            default:
                return default;
        }
    }

    private static ParticleSystem.MinMaxGradient BuildFadeGradient(Color primary, Color secondary)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 0.55f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(primary.a, 0.12f),
                new GradientAlphaKey(secondary.a, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });

        return new ParticleSystem.MinMaxGradient(gradient);
    }

    private static ParticleSystem.MinMaxCurve BuildSizeCurve(float endSizeMultiplier)
    {
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.15f, 1f),
            new Keyframe(1f, Mathf.Max(0f, endSizeMultiplier)));
        return new ParticleSystem.MinMaxCurve(1f, curve);
    }

    private static Bounds CalculateLocalBounds(Transform root)
    {
        if (root == null)
            return new Bounds(Vector3.zero, Vector3.one);

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds result = new Bounds(Vector3.zero, Vector3.zero);

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer targetRenderer = renderers[rendererIndex];
            if (targetRenderer == null || targetRenderer is ParticleSystemRenderer || targetRenderer is SpriteRenderer)
                continue;

            Bounds rendererBounds = targetRenderer.bounds;
            Vector3 min = rendererBounds.min;
            Vector3 max = rendererBounds.max;

            for (int x = 0; x <= 1; x++)
            {
                for (int y = 0; y <= 1; y++)
                {
                    for (int z = 0; z <= 1; z++)
                    {
                        Vector3 worldPoint = new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        Vector3 localPoint = root.InverseTransformPoint(worldPoint);

                        if (!hasBounds)
                        {
                            result = new Bounds(localPoint, Vector3.zero);
                            hasBounds = true;
                        }
                        else
                        {
                            result.Encapsulate(localPoint);
                        }
                    }
                }
            }
        }

        return hasBounds ? result : new Bounds(Vector3.zero, Vector3.one);
    }

    private static Material GetSharedParticleMaterial()
    {
        Texture2D particleTexture = GetSharedParticleTexture();
        if (_sharedParticleMaterial != null)
        {
            if (particleTexture != null)
                SetMaterialTexture(_sharedParticleMaterial, particleTexture);
            return _sharedParticleMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            return null;

        _sharedParticleMaterial = new Material(shader)
        {
            name = "Runtime Element Particles",
            hideFlags = HideFlags.DontSave
        };
        if (particleTexture != null)
            SetMaterialTexture(_sharedParticleMaterial, particleTexture);

        return _sharedParticleMaterial;
    }

    private static Texture2D GetSharedParticleTexture()
    {
        if (_sharedParticleTexture != null)
            return _sharedParticleTexture;

        Sprite particleSprite = Resources.Load<Sprite>(ParticleTextureResourcePath);
        if (particleSprite != null)
            _sharedParticleTexture = particleSprite.texture;
        if (_sharedParticleTexture == null)
            _sharedParticleTexture = Resources.Load<Texture2D>(ParticleTextureResourcePath);
        if (_sharedParticleTexture != null)
            return _sharedParticleTexture;

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Runtime Element Star Particle",
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxRadius = size * 0.46f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 delta = new Vector2(x, y) - center;
                float normalizedRadius = delta.magnitude / maxRadius;
                float angle = Mathf.Atan2(delta.y, delta.x);
                float starPoint = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 5f)), 1.15f);
                float starRadius = Mathf.Lerp(0.36f, 1f, starPoint);
                float body = Mathf.SmoothStep(0f, 1f, (starRadius - normalizedRadius) / 0.12f);
                float glow = Mathf.Clamp01(1f - normalizedRadius);
                glow = glow * glow * 0.24f;
                float alpha = Mathf.Clamp01(body + glow);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        _sharedParticleTexture = texture;
        return _sharedParticleTexture;
    }

    private static void SetMaterialTexture(Material material, Texture2D texture)
    {
        if (material == null || texture == null)
            return;

        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
    }

    private static Sprite GetSharedRaySprite()
    {
        if (_sharedRaySprite != null)
            return _sharedRaySprite;

        const int width = 16;
        const int height = 128;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Runtime Element Ray",
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        for (int y = 0; y < height; y++)
        {
            float vertical = y / (float)(height - 1);
            float lengthFade = Mathf.Sin(vertical * Mathf.PI);
            for (int x = 0; x < width; x++)
            {
                float horizontal = Mathf.Abs((x / (float)(width - 1)) - 0.5f) * 2f;
                float widthFade = Mathf.Clamp01(1f - horizontal);
                float alpha = lengthFade * widthFade * widthFade;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        _sharedRaySprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), height);
        _sharedRaySprite.name = "Runtime Element Ray";
        _sharedRaySprite.hideFlags = HideFlags.DontSave;
        return _sharedRaySprite;
    }

    private static Sprite GetSharedGroundSprite()
    {
        if (_sharedGroundSprite != null)
            return _sharedGroundSprite;

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Runtime Element Ground",
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxRadius = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float normalizedRadius = Vector2.Distance(new Vector2(x, y), center) / maxRadius;
                float alpha = Mathf.SmoothStep(1f, 0f, normalizedRadius);
                alpha *= alpha;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        _sharedGroundSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        _sharedGroundSprite.name = "Runtime Element Ground";
        _sharedGroundSprite.hideFlags = HideFlags.DontSave;
        return _sharedGroundSprite;
    }

    private struct ElementVfxSettings
    {
        public Color PrimaryColor;
        public Color SecondaryColor;
        public float RateOverTime;
        public int MaxParticles;
        public float MinLifetime;
        public float MaxLifetime;
        public float MinSpeed;
        public float MaxSpeed;
        public float MinSize;
        public float MaxSize;
        public float RadiusMultiplier;
        public float RadiusThickness;
        public float HeightOffset;
        public float UpwardVelocity;
        public float RotationSpeed;
        public float EndSizeMultiplier;
        public float Gravity;
        public float RayAlpha;
        public float RayLengthMultiplier;
        public float RayWidthMultiplier;
        public float RayAngleOffset;
        public float GroundAlpha;
        public float GroundRadiusMultiplier;
    }
}
