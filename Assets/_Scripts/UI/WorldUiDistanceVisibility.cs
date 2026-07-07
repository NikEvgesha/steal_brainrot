using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WorldUiDistanceVisibility : MonoBehaviour
{
    [SerializeField] private bool _distanceCullingEnabled;
    [SerializeField] private float _visibleDistance = 30f;
    [SerializeField] private float _hysteresis = 4f;
    [SerializeField] private float _refreshInterval = 0.2f;
    [SerializeField] private Transform _distanceTarget;

    private Canvas[] _canvases;
    private CanvasGroup _canvasGroup;
    private float _nextRefreshTime;
    private bool _visible = true;

    public static WorldUiDistanceVisibility Ensure(GameObject target)
    {
        if (target == null)
            return null;

        var visibility = target.GetComponent<WorldUiDistanceVisibility>();
        if (visibility == null)
            visibility = target.AddComponent<WorldUiDistanceVisibility>();

        return visibility;
    }

    public void Configure(bool enabled, Transform distanceTarget = null, float visibleDistance = -1f, float hysteresis = -1f)
    {
        _distanceCullingEnabled = enabled;
        _distanceTarget = distanceTarget != null ? distanceTarget : transform;

        if (visibleDistance > 0f)
            _visibleDistance = visibleDistance;
        if (hysteresis >= 0f)
            _hysteresis = hysteresis;

        CacheReferences();
        UpdateVisibility(force: true);
    }

    private void OnEnable()
    {
        CacheReferences();
        UpdateVisibility(force: true);
    }

    private void LateUpdate()
    {
        if (!_distanceCullingEnabled)
        {
            SetVisible(true);
            return;
        }

        if (Time.unscaledTime < _nextRefreshTime)
            return;

        _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, _refreshInterval);
        UpdateVisibility(force: false);
    }

    private void CacheReferences()
    {
        _canvases = GetComponentsInChildren<Canvas>(true);
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void UpdateVisibility(bool force)
    {
        if (!_distanceCullingEnabled)
        {
            SetVisible(true);
            return;
        }

        var player = G.Player;
        if (player == null)
        {
            SetVisible(true);
            return;
        }

        Transform target = _distanceTarget != null ? _distanceTarget : transform;
        float distance = Vector3.Distance(target.position, player.transform.position);
        float enterDistance = Mathf.Max(0.5f, _visibleDistance);
        float exitDistance = enterDistance + Mathf.Max(0f, _hysteresis);
        float threshold = _visible ? exitDistance : enterDistance;
        bool shouldShow = distance <= threshold;

        if (force || shouldShow != _visible)
            SetVisible(shouldShow);
    }

    private void SetVisible(bool visible)
    {
        if (_visible == visible && _canvasGroup != null)
            return;

        _visible = visible;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        if (_canvases == null)
            return;

        for (int i = 0; i < _canvases.Length; i++)
        {
            if (_canvases[i] != null)
                _canvases[i].enabled = visible;
        }
    }
}
