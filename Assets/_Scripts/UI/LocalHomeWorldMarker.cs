using UnityEngine;
using UnityEngine.UI;

public sealed class LocalHomeWorldMarker : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private RemoteBasesApplier _remoteBases;
    [SerializeField] private Vector3 _worldOffset = new Vector3(0f, 20f, 0f);

    [Header("Presentation")]
    [SerializeField] private Vector2 _iconSize = new Vector2(112f, 112f);
    [SerializeField] private float _worldScale = 0.1f;
    [SerializeField] private Canvas _canvas;
    [SerializeField] private Image _image;

    [Header("Tracking")]
    [SerializeField] private float _targetRefreshInterval = 0.35f;
    [SerializeField] private float _cameraRefreshInterval = 0.5f;
    [SerializeField] private float _bobAmplitude = 0.18f;
    [SerializeField] private float _bobSpeed = 2.2f;

    private RectTransform _rectTransform;
    private Transform _target;
    private Camera _mainCamera;
    private float _nextTargetRefreshTime;
    private float _nextCameraRefreshTime;

    public void Initialize(RemoteBasesApplier remoteBases)
    {
        _remoteBases = remoteBases;
        ApplyPresentation();
        RefreshTarget(force: true);
    }

    private void Awake()
    {
        ApplyPresentation();
    }

    private void OnValidate()
    {
        _worldScale = Mathf.Max(0.001f, _worldScale);
        _targetRefreshInterval = Mathf.Max(0.05f, _targetRefreshInterval);
        _cameraRefreshInterval = Mathf.Max(0.1f, _cameraRefreshInterval);
        _bobAmplitude = Mathf.Max(0f, _bobAmplitude);
        _bobSpeed = Mathf.Max(0f, _bobSpeed);
        ApplyPresentation();
    }

    private void LateUpdate()
    {
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

    private void ApplyPresentation()
    {
        if (_canvas == null)
            _canvas = GetComponent<Canvas>();
        if (_image == null)
            _image = GetComponentInChildren<Image>(true);
        if (_rectTransform == null)
            _rectTransform = transform as RectTransform;

        if (_rectTransform != null)
        {
            _rectTransform.sizeDelta = _iconSize;
            _rectTransform.localScale = Vector3.one * _worldScale;
        }

        if (_image != null && _image.transform is RectTransform iconRect)
        {
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = _iconSize;
            iconRect.localScale = Vector3.one;
        }
    }

    private void RefreshTarget(bool force)
    {
        if (!force && Time.unscaledTime < _nextTargetRefreshTime)
            return;

        _nextTargetRefreshTime = Time.unscaledTime + _targetRefreshInterval;
        _target = _remoteBases != null
            ? (_remoteBases.GetLocalSlotEntryPoint() != null ? _remoteBases.GetLocalSlotEntryPoint() : _remoteBases.GetLocalSlotRoot())
            : null;
    }

    private void FaceCamera()
    {
        if (_mainCamera == null || Time.unscaledTime >= _nextCameraRefreshTime)
        {
            _nextCameraRefreshTime = Time.unscaledTime + _cameraRefreshInterval;
            _mainCamera = Camera.main;
        }

        if (_mainCamera == null)
            return;

        transform.LookAt(
            transform.position + _mainCamera.transform.rotation * Vector3.forward,
            _mainCamera.transform.rotation * Vector3.up);
    }

    private void SetVisible(bool visible)
    {
        if (_canvas != null && _canvas.enabled != visible)
            _canvas.enabled = visible;
    }
}
