using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MovingRoadVisualScroller : MonoBehaviour
{
    [SerializeField] private Renderer[] renderers;
    [SerializeField] private Vector2 uvDirection = Vector2.right;
    [SerializeField] private float scrollSpeed = 5f;
    [SerializeField] private bool autoCollectChildRenderers = true;
    [SerializeField] private bool includeInactiveChildRenderers;
    [SerializeField] private float stripLengthOverride;
    [SerializeField] private float visualSpacing;

    private Entry[] _entries;
    private float _stripMin;
    private float _stripMax;
    private float _stripLength;

    public float ScrollSpeed
    {
        get => scrollSpeed;
        set => scrollSpeed = value;
    }

    public void Configure(Renderer[] targetRenderers, Vector2 direction, float speed)
    {
        RestoreOriginalPositions();

        renderers = targetRenderers;
        uvDirection = direction.sqrMagnitude > 0.0001f ? direction : Vector2.right;
        scrollSpeed = speed;

        if (isActiveAndEnabled)
            RebuildEntries();
    }

    private void Reset()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        _entries = null;
    }

    private void OnValidate()
    {
        if (uvDirection.sqrMagnitude < 0.0001f)
            uvDirection = Vector2.right;

        stripLengthOverride = Mathf.Max(0f, stripLengthOverride);
        visualSpacing = Mathf.Max(0f, visualSpacing);
        _entries = null;
    }

    private void OnEnable()
    {
        RebuildEntries();
    }

    private void LateUpdate()
    {
        if (_entries == null)
            RebuildEntries();

        if (_entries.Length == 0 || Mathf.Approximately(scrollSpeed, 0f))
            return;

        Vector3 axis = GetLocalAxis();
        float worldUnitsPerLocalUnit = transform.TransformVector(axis).magnitude;
        if (worldUnitsPerLocalUnit <= 0.0001f)
            return;

        float scrollStep = scrollSpeed * Time.deltaTime / worldUnitsPerLocalUnit;
        ApplyPositions(axis, scrollStep);
    }

    private void OnDisable()
    {
        RestoreOriginalPositions();
    }

    private void OnTransformChildrenChanged()
    {
        RestoreOriginalPositions();
    }

    private void ApplyPositions(Vector3 axis, float scrollStep)
    {
        for (int i = 0; i < _entries.Length; i++)
        {
            Entry entry = _entries[i];
            if (entry.Original == null || _stripLength <= 0.0001f)
                continue;

            float center = entry.CurrentCenter + scrollStep;
            while (center - entry.HalfLength > _stripMax)
                center -= _stripLength;
            while (center + entry.HalfLength < _stripMin)
                center += _stripLength;

            entry.CurrentCenter = center;
            float visualOffset = entry.CurrentCenter - entry.StartCenter;
            entry.Original.localPosition = entry.StartLocalPosition + GetParentLocalDelta(entry.Original, axis, visualOffset);
            _entries[i] = entry;
        }
    }

    private void RebuildEntries()
    {
        RestoreOriginalPositions();

        Renderer[] targetRenderers = ResolveRenderers();
        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            _entries = new Entry[0];
            return;
        }

        Vector3 axis = GetLocalAxis();
        List<Entry> entries = new List<Entry>();
        for (int rendererIndex = 0; rendererIndex < targetRenderers.Length; rendererIndex++)
        {
            Renderer targetRenderer = targetRenderers[rendererIndex];
            if (!IsUsableRenderer(targetRenderer))
                continue;

            Projection projection = MeasureProjection(targetRenderer, axis);
            Transform targetTransform = targetRenderer.transform;
            entries.Add(new Entry
            {
                Original = targetTransform,
                StartLocalPosition = targetTransform.localPosition,
                StartCenter = projection.Center,
                CurrentCenter = projection.Center,
                HalfLength = projection.Length * 0.5f
            });
        }

        entries.Sort((a, b) => a.StartCenter.CompareTo(b.StartCenter));
        CacheStripBounds(entries);
        _entries = entries.ToArray();
        ApplyPositions(axis, 0f);
    }

    private Vector3 GetLocalAxis()
    {
        Vector3 axis = new Vector3(uvDirection.x, 0f, uvDirection.y);
        return axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.right;
    }

    private Renderer[] ResolveRenderers()
    {
        if (!autoCollectChildRenderers)
            return renderers;

        return GetComponentsInChildren<Renderer>(includeInactiveChildRenderers);
    }

    private bool IsUsableRenderer(Renderer targetRenderer)
    {
        if (targetRenderer == null || !targetRenderer.transform.IsChildOf(transform))
            return false;

        if (!includeInactiveChildRenderers && !targetRenderer.gameObject.activeInHierarchy)
            return false;

        return includeInactiveChildRenderers || targetRenderer.enabled;
    }

    private void RestoreOriginalPositions()
    {
        if (_entries == null)
            return;

        for (int entryIndex = 0; entryIndex < _entries.Length; entryIndex++)
        {
            Entry entry = _entries[entryIndex];
            if (entry.Original != null)
                entry.Original.localPosition = entry.StartLocalPosition;
        }

        _entries = null;
    }

    private void CacheStripBounds(List<Entry> entries)
    {
        if (entries.Count == 0)
        {
            _stripMin = 0f;
            _stripMax = 0f;
            _stripLength = 0f;
            return;
        }

        _stripMin = float.MaxValue;
        _stripMax = float.MinValue;
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            _stripMin = Mathf.Min(_stripMin, entry.StartCenter - entry.HalfLength);
            _stripMax = Mathf.Max(_stripMax, entry.StartCenter + entry.HalfLength);
        }

        _stripLength = stripLengthOverride > 0.0001f
            ? stripLengthOverride
            : Mathf.Max(0.0001f, _stripMax - _stripMin + visualSpacing);
    }

    private Vector3 GetParentLocalDelta(Transform target, Vector3 axis, float offset)
    {
        if (target.parent == null)
            return transform.TransformVector(axis * offset);

        Vector3 worldDelta = transform.TransformVector(axis * offset);
        return target.parent.InverseTransformVector(worldDelta);
    }

    private Projection MeasureProjection(Renderer targetRenderer, Vector3 axis)
    {
        Bounds bounds = targetRenderer.localBounds;
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        float min = float.MaxValue;
        float max = float.MinValue;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 localPoint = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 laneLocalPoint = transform.InverseTransformPoint(targetRenderer.transform.TransformPoint(localPoint));
                    float projection = Vector3.Dot(laneLocalPoint, axis);
                    min = Mathf.Min(min, projection);
                    max = Mathf.Max(max, projection);
                }
            }
        }

        float length = Mathf.Max(0.0001f, max - min);
        return new Projection
        {
            Center = (min + max) * 0.5f,
            Length = length
        };
    }

    private struct Entry
    {
        public Transform Original;
        public Vector3 StartLocalPosition;
        public float StartCenter;
        public float CurrentCenter;
        public float HalfLength;
    }

    private struct Projection
    {
        public float Center;
        public float Length;
    }
}
