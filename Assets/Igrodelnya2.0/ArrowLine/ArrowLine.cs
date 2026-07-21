using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class ArrowLine : MonoBehaviour
{
    [Tooltip("Texture scrolling speed")]
    public float textureScrollSpeed = 1f;

    [Tooltip("Length of one arrow tile in world units")]
    public float arrowWidth = 1f;

    [Header("Ground projection")]
    [SerializeField] private bool _projectToGround;
    [SerializeField, Min(0f)] private float _groundOffset = 0.12f;
    [SerializeField, Min(0f)] private float _startPadding = 1.1f;
    [SerializeField, Min(0f)] private float _endPadding = 0.75f;
    [SerializeField, Min(0f)] private float _maxVisibleDistance = 14f;
    [SerializeField, Min(0.25f)] private float _groundSampleSpacing = 1.5f;
    [SerializeField, Min(0.02f)] private float _pathRefreshInterval = 0.1f;
    [SerializeField] private LayerMask _groundMask = ~0;

    private LineRenderer _lineRenderer;
    private Material _lineMaterial;
    private bool _isActive;
    private float _nextPathRefresh;
    private Transform _startTransform;
    private Transform _endTransform;

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        if (_lineRenderer == null)
        {
            Debug.LogError("ArrowLine requires a LineRenderer.");
            return;
        }

        Material sourceMaterial = _lineRenderer.sharedMaterial;
        Shader fallbackShader = Shader.Find("Universal Render Pipeline/Unlit") ??
                                Shader.Find("Unlit/Transparent");
        if (sourceMaterial != null)
            _lineMaterial = new Material(sourceMaterial);
        else if (fallbackShader != null)
            _lineMaterial = new Material(fallbackShader);

        if (_lineMaterial != null)
        {
            PrepareTransparentMaterial(_lineMaterial, fallbackShader);
            _lineRenderer.sharedMaterial = _lineMaterial;
        }

        _lineRenderer.useWorldSpace = true;
        ActiveArrowLine(false);
    }

    private void Update()
    {
        if (!_isActive)
            return;

        if (Time.unscaledTime >= _nextPathRefresh)
        {
            RefreshPath();
            _nextPathRefresh = Time.unscaledTime + _pathRefreshInterval;
        }

        AnimateArrows();
    }

    private void AnimateArrows()
    {
        if (!_startTransform || !_endTransform)
        {
            ActiveArrowLine(false);
            return;
        }

        if (_lineMaterial == null)
            return;

        Vector2 offset = _lineMaterial.mainTextureOffset;
        offset.x = (offset.x - textureScrollSpeed * Time.unscaledDeltaTime) % 1f;
        _lineMaterial.mainTextureOffset = offset;
    }

    private void RefreshPath()
    {
        if (!_startTransform || !_endTransform || _lineRenderer == null)
        {
            ActiveArrowLine(false);
            return;
        }

        Vector3 rawStart = _startTransform.position;
        Vector3 rawEnd = _endTransform.position;
        Vector3 start = rawStart;
        Vector3 end = rawEnd;
        Vector3 flatDirection = rawEnd - rawStart;
        flatDirection.y = 0f;
        float flatDistance = flatDirection.magnitude;
        if (flatDistance > 0.001f)
        {
            Vector3 direction = flatDirection / flatDistance;
            float availablePadding = Mathf.Max(0f, flatDistance - 0.2f);
            float startPadding = Mathf.Min(_startPadding, availablePadding * 0.55f);
            bool reachesTarget = _maxVisibleDistance <= 0f || flatDistance <= _maxVisibleDistance;
            start = rawStart + direction * startPadding;
            if (reachesTarget)
            {
                float endPadding = Mathf.Min(_endPadding, availablePadding - startPadding);
                end = rawEnd - direction * endPadding;
            }
            else
            {
                end = rawStart + direction * _maxVisibleDistance;
                end.y = Mathf.Lerp(rawStart.y, rawEnd.y, _maxVisibleDistance / flatDistance);
            }
        }

        int pointCount = 2;
        if (_projectToGround && _groundSampleSpacing > 0f)
            pointCount = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(start, end) / _groundSampleSpacing) + 1);

        _lineRenderer.positionCount = pointCount;
        float pathLength = 0f;
        Vector3 previousPoint = default;
        float rayOriginY = Mathf.Max(rawStart.y, rawEnd.y) + 20f;
        for (int i = 0; i < pointCount; i++)
        {
            float t = pointCount <= 1 ? 0f : i / (float)(pointCount - 1);
            Vector3 point = Vector3.Lerp(start, end, t);
            if (_projectToGround)
                point = ProjectPointToGround(point, rayOriginY);

            _lineRenderer.SetPosition(i, point);
            if (i > 0)
                pathLength += Vector3.Distance(previousPoint, point);
            previousPoint = point;
        }

        if (_lineMaterial != null)
        {
            float safeArrowWidth = Mathf.Max(0.05f, arrowWidth);
            float textureScaleX = _lineRenderer.textureMode == LineTextureMode.Tile
                ? 1f / safeArrowWidth
                : pathLength / safeArrowWidth;
            _lineMaterial.mainTextureScale = new Vector2(textureScaleX, 1f);
        }
    }

    private Vector3 ProjectPointToGround(Vector3 point, float rayOriginY)
    {
        Vector3 origin = new Vector3(point.x, rayOriginY, point.z);
        float rayDistance = Mathf.Max(40f, rayOriginY - point.y + 20f);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayDistance, _groundMask, QueryTriggerInteraction.Ignore))
            point.y = hit.point.y + _groundOffset;
        else
            point.y += _groundOffset;
        return point;
    }

    private static void PrepareTransparentMaterial(Material material, Shader fallbackShader)
    {
        if (material == null)
            return;

        Texture texture = material.mainTexture;
        bool requiresShaderUpgrade = material.shader == null ||
                                     material.shader.name == "Standard" ||
                                     material.shader.name == "GUI/Text Shader" ||
                                     material.shader.name == "Hidden/InternalErrorShader";
        if (requiresShaderUpgrade && fallbackShader != null)
            material.shader = fallbackShader;

        if (texture != null)
        {
            material.mainTexture = texture;
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    public void StartArrowLine(Transform start, Transform end)
    {
        _startTransform = start;
        _endTransform = end;
        ActiveArrowLine(start && end);
        if (!_isActive)
            return;

        RefreshPath();
        _nextPathRefresh = Time.unscaledTime + _pathRefreshInterval;
    }

    public void ConfigureGroundPath(
        bool projectToGround,
        float groundOffset = 0.12f,
        float startPadding = 1.1f,
        float endPadding = 0.75f,
        float maxVisibleDistance = 14f,
        float groundSampleSpacing = 1.5f)
    {
        _projectToGround = projectToGround;
        _groundOffset = Mathf.Max(0f, groundOffset);
        _startPadding = Mathf.Max(0f, startPadding);
        _endPadding = Mathf.Max(0f, endPadding);
        _maxVisibleDistance = Mathf.Max(0f, maxVisibleDistance);
        _groundSampleSpacing = Mathf.Max(0.25f, groundSampleSpacing);
        _nextPathRefresh = 0f;
    }

    public void SetGroundMask(LayerMask groundMask)
    {
        _groundMask = groundMask;
        _nextPathRefresh = 0f;
    }

    public void SetColor(Color color)
    {
        if (_lineMaterial == null)
            return;
        _lineMaterial.color = color;
        if (_lineMaterial.HasProperty("_BaseColor"))
            _lineMaterial.SetColor("_BaseColor", color);
    }

    public void ActiveArrowLine(bool isActive)
    {
        _isActive = isActive && _lineRenderer != null;
        if (_lineRenderer != null)
            _lineRenderer.enabled = _isActive;
    }

    private void OnDestroy()
    {
        if (_lineMaterial == null)
            return;
        if (Application.isPlaying)
            Destroy(_lineMaterial);
        else
            DestroyImmediate(_lineMaterial);
    }
}
