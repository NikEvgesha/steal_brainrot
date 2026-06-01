using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MovingRoadVisualScroller : MonoBehaviour
{
    private const int MinCopyCount = 3;

    [SerializeField] private Renderer[] renderers;
    [SerializeField] private Vector2 uvDirection = Vector2.right;
    [SerializeField] private float scrollSpeed = 5f;
    [SerializeField] private int copyCount = MinCopyCount;
    [SerializeField] private float visualSpacing;

    private Entry[] _entries;
    private float _distance;

    public float ScrollSpeed
    {
        get => scrollSpeed;
        set => scrollSpeed = value;
    }

    public void Configure(Renderer[] targetRenderers, Vector2 direction, float speed)
    {
        CleanupClones();

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

        copyCount = Mathf.Max(MinCopyCount, copyCount);
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

        _distance += scrollSpeed * Time.deltaTime / worldUnitsPerLocalUnit;
        ApplyPositions(axis);
    }

    private void OnDisable()
    {
        CleanupClones();
    }

    private void ApplyPositions(Vector3 axis)
    {
        for (int i = 0; i < _entries.Length; i++)
        {
            Entry entry = _entries[i];
            if (entry.Visuals == null || entry.Visuals.Length == 0 || entry.Span <= 0.0001f)
                continue;

            int centerIndex = entry.Visuals.Length / 2;
            float wrappedDistance = Mathf.Repeat(_distance, entry.Span);
            for (int visualIndex = 0; visualIndex < entry.Visuals.Length; visualIndex++)
            {
                Transform visual = entry.Visuals[visualIndex];
                if (visual == null)
                    continue;

                float visualOffset = wrappedDistance + (visualIndex - centerIndex) * entry.Span;
                visual.localPosition = entry.StartLocalPosition + axis * visualOffset;
            }
        }
    }

    private void RebuildEntries()
    {
        CleanupClones();

        if (renderers == null || renderers.Length == 0)
        {
            _entries = new Entry[0];
            return;
        }

        Vector3 axis = GetLocalAxis();
        List<Entry> entries = new List<Entry>();
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer targetRenderer = renderers[rendererIndex];
            if (targetRenderer == null)
                continue;

            Transform targetTransform = targetRenderer.transform;
            Transform[] visuals = CreateVisualCopies(targetTransform);
            entries.Add(new Entry
            {
                Original = targetTransform,
                Visuals = visuals,
                StartLocalPosition = targetTransform.localPosition,
                Span = Mathf.Max(0.0001f, MeasureLocalLength(targetRenderer, axis) + visualSpacing)
            });
        }

        _entries = entries.ToArray();
        ApplyPositions(axis);
    }

    private Vector3 GetLocalAxis()
    {
        Vector3 axis = new Vector3(uvDirection.x, 0f, uvDirection.y);
        return axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.right;
    }

    private Transform[] CreateVisualCopies(Transform original)
    {
        if (!Application.isPlaying)
            return new[] { original };

        int targetCopyCount = Mathf.Max(MinCopyCount, copyCount);
        Transform[] visuals = new Transform[targetCopyCount];
        visuals[0] = original;

        for (int i = 1; i < targetCopyCount; i++)
        {
            Transform clone = Instantiate(original, original.parent);
            clone.name = original.name + " Loop " + i;
            clone.localPosition = original.localPosition;
            clone.localRotation = original.localRotation;
            clone.localScale = original.localScale;
            clone.gameObject.hideFlags = HideFlags.DontSave;
            DisableColliders(clone);
            visuals[i] = clone;
        }

        return visuals;
    }

    private void CleanupClones()
    {
        if (_entries == null)
            return;

        for (int entryIndex = 0; entryIndex < _entries.Length; entryIndex++)
        {
            Entry entry = _entries[entryIndex];
            if (entry.Original != null)
                entry.Original.localPosition = entry.StartLocalPosition;

            if (entry.Visuals == null)
                continue;

            for (int visualIndex = 1; visualIndex < entry.Visuals.Length; visualIndex++)
            {
                Transform clone = entry.Visuals[visualIndex];
                if (clone == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(clone.gameObject);
                else
                    DestroyImmediate(clone.gameObject);
            }
        }

        _entries = null;
    }

    private float MeasureLocalLength(Renderer targetRenderer, Vector3 axis)
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

        return Mathf.Max(0.0001f, max - min);
    }

    private static void DisableColliders(Transform cloneRoot)
    {
        Collider[] colliders = cloneRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;
    }

    private struct Entry
    {
        public Transform Original;
        public Transform[] Visuals;
        public Vector3 StartLocalPosition;
        public float Span;
    }
}
