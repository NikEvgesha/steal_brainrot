using UnityEngine;
using UnityEngine.UI;

public sealed class LocalHomeWorldMarker : MonoBehaviour
{
    private const int IconTextureSize = 64;
    private const string HomeSpriteResourcePath = "WorldMarkers/home_marker_icon";

    [SerializeField] private RemoteBasesApplier _remoteBases;
    [SerializeField] private Vector3 _worldOffset = new Vector3(0f, 20f, 0f);
    [SerializeField] private Vector2 _iconSize = new Vector2(112f, 112f);
    [SerializeField] private float _worldScale = 0.03f;
    [SerializeField] private float _targetRefreshInterval = 0.35f;
    [SerializeField] private float _cameraRefreshInterval = 0.5f;
    [SerializeField] private float _bobAmplitude = 0.18f;
    [SerializeField] private float _bobSpeed = 2.2f;

    private static Sprite _homeSprite;

    private Canvas _canvas;
    private RectTransform _rectTransform;
    private Image _image;
    private Transform _target;
    private Camera _mainCamera;
    private float _nextTargetRefreshTime;
    private float _nextCameraRefreshTime;

    public void Initialize(
        RemoteBasesApplier remoteBases,
        Vector3 worldOffset,
        Vector2 iconSize,
        float worldScale,
        float bobAmplitude,
        float bobSpeed)
    {
        _remoteBases = remoteBases;
        _worldOffset = worldOffset;
        _iconSize = iconSize;
        _worldScale = Mathf.Max(0.001f, worldScale);
        _bobAmplitude = Mathf.Max(0f, bobAmplitude);
        _bobSpeed = Mathf.Max(0f, bobSpeed);

        EnsureUi();
        RefreshTarget(force: true);
    }

    private void Awake()
    {
        EnsureUi();
    }

    private void LateUpdate()
    {
        EnsureUi();
        RefreshTarget(force: false);

        if (_target == null)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);
        float bob = _bobAmplitude > 0f ? Mathf.Sin(Time.unscaledTime * _bobSpeed) * _bobAmplitude : 0f;
        transform.position = _target.position + _worldOffset + Vector3.up * bob;
        FaceCamera();
    }

    private void EnsureUi()
    {
        if (_canvas == null)
        {
            _canvas = GetComponent<Canvas>();
            if (_canvas == null)
                _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.overrideSorting = true;
            // Keep the world marker below screen-space menus (GameCanvas uses order 100).
            _canvas.sortingOrder = 50;
        }

        if (_rectTransform == null)
        {
            _rectTransform = transform as RectTransform;
            if (_rectTransform != null)
            {
                _rectTransform.sizeDelta = _iconSize;
                _rectTransform.localScale = Vector3.one * _worldScale;
            }
        }

        if (_image == null)
        {
            var icon = new GameObject("HomeIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            icon.transform.SetParent(transform, false);
            _image = icon.GetComponent<Image>();
            _image.sprite = GetHomeSprite();
            _image.preserveAspect = true;
            _image.raycastTarget = false;

            var iconRect = icon.transform as RectTransform;
            if (iconRect != null)
            {
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = _iconSize;
                iconRect.localScale = Vector3.one;
            }
        }
    }

    private void RefreshTarget(bool force)
    {
        if (!force && Time.unscaledTime < _nextTargetRefreshTime)
            return;

        _nextTargetRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, _targetRefreshInterval);
        _target = _remoteBases != null
            ? (_remoteBases.GetLocalSlotEntryPoint() != null ? _remoteBases.GetLocalSlotEntryPoint() : _remoteBases.GetLocalSlotRoot())
            : null;
    }

    private void FaceCamera()
    {
        if (_mainCamera == null || Time.unscaledTime >= _nextCameraRefreshTime)
        {
            _nextCameraRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, _cameraRefreshInterval);
            _mainCamera = Camera.main;
        }

        if (_mainCamera == null)
            return;

        transform.LookAt(transform.position + _mainCamera.transform.rotation * Vector3.forward, _mainCamera.transform.rotation * Vector3.up);
    }

    private void SetVisible(bool visible)
    {
        if (_canvas != null && _canvas.enabled != visible)
            _canvas.enabled = visible;
    }

    private static Sprite GetHomeSprite()
    {
        if (_homeSprite != null)
            return _homeSprite;

        _homeSprite = Resources.Load<Sprite>(HomeSpriteResourcePath);
        if (_homeSprite != null)
            return _homeSprite;

        var texture = new Texture2D(IconTextureSize, IconTextureSize, TextureFormat.RGBA32, false);
        texture.name = "GeneratedHomeMarkerIcon";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        var clear = new Color32(0, 0, 0, 0);
        var outline = new Color32(42, 31, 18, 255);
        var roof = new Color32(255, 142, 28, 255);
        var wall = new Color32(255, 222, 94, 255);
        var door = new Color32(86, 53, 34, 255);
        var shine = new Color32(255, 246, 166, 255);

        var pixels = texture.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clear;
        texture.SetPixels32(pixels);

        FillTriangle(texture, 6, 32, 32, 58, 58, 32, outline);
        FillTriangle(texture, 11, 33, 32, 53, 53, 33, roof);
        FillRect(texture, 13, 10, 38, 26, outline);
        FillRect(texture, 18, 13, 28, 20, wall);
        FillRect(texture, 28, 13, 8, 14, door);
        FillRect(texture, 21, 24, 8, 7, shine);
        FillRect(texture, 38, 24, 6, 6, shine);

        texture.Apply(false, true);
        _homeSprite = Sprite.Create(texture, new Rect(0, 0, IconTextureSize, IconTextureSize), new Vector2(0.5f, 0.5f), IconTextureSize);
        return _homeSprite;
    }

    private static void FillRect(Texture2D texture, int x, int y, int width, int height, Color32 color)
    {
        for (int yy = y; yy < y + height; yy++)
        {
            for (int xx = x; xx < x + width; xx++)
                SetPixelSafe(texture, xx, yy, color);
        }
    }

    private static void FillTriangle(Texture2D texture, int x1, int y1, int x2, int y2, int x3, int y3, Color32 color)
    {
        int minX = Mathf.Min(x1, Mathf.Min(x2, x3));
        int maxX = Mathf.Max(x1, Mathf.Max(x2, x3));
        int minY = Mathf.Min(y1, Mathf.Min(y2, y3));
        int maxY = Mathf.Max(y1, Mathf.Max(y2, y3));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (PointInTriangle(x + 0.5f, y + 0.5f, x1, y1, x2, y2, x3, y3))
                    SetPixelSafe(texture, x, y, color);
            }
        }
    }

    private static bool PointInTriangle(float px, float py, float x1, float y1, float x2, float y2, float x3, float y3)
    {
        float d1 = Sign(px, py, x1, y1, x2, y2);
        float d2 = Sign(px, py, x2, y2, x3, y3);
        float d3 = Sign(px, py, x3, y3, x1, y1);
        bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(hasNeg && hasPos);
    }

    private static float Sign(float px, float py, float x1, float y1, float x2, float y2)
    {
        return (px - x2) * (y1 - y2) - (x1 - x2) * (py - y2);
    }

    private static void SetPixelSafe(Texture2D texture, int x, int y, Color32 color)
    {
        if (x < 0 || y < 0 || x >= IconTextureSize || y >= IconTextureSize)
            return;

        texture.SetPixel(x, y, color);
    }
}
