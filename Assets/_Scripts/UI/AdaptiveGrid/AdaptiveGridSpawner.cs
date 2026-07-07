using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class AdaptiveGridSpawner : MonoBehaviour
{
    public enum CellSizingMode
    {
        FixedCellSize,
        FitItemCount
    }

    public enum StartCorner
    {
        UpperLeft,
        UpperRight,
        LowerLeft,
        LowerRight,
        UpperCenter,
        MiddleLeft,
        MiddleCenter,
        MiddleRight,
        LowerCenter
    }

    public enum FillDirection
    {
        HorizontalThenVertical,
        VerticalThenHorizontal
    }

    public enum LayoutValueMode
    {
        Pixels,
        Percent
    }

    [Header("Source")]
    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private bool skipPrefabWhenItIsChild = true;
    [SerializeField] private bool includeInactiveChildren;

    [Header("Layout")]
    [SerializeField] private CellSizingMode sizingMode = CellSizingMode.FixedCellSize;
    [SerializeField] private StartCorner startCorner = StartCorner.UpperLeft;
    [SerializeField] private FillDirection fillDirection = FillDirection.HorizontalThenVertical;
    [SerializeField] private RectOffset padding;
    [SerializeField] private Vector2 spacing = new Vector2(8f, 8f);
    [SerializeField] private LayoutValueMode paddingUnits = LayoutValueMode.Pixels;
    [SerializeField] private PercentPadding paddingPercent;
    [SerializeField] private LayoutValueMode spacingUnits = LayoutValueMode.Pixels;
    [SerializeField] private Vector2 spacingPercent;

    [Header("Fixed Cell Size")]
    [SerializeField] private Vector2 fixedCellSize = new Vector2(120f, 120f);
    [SerializeField] private bool hideFixedOverflow = true;

    [Header("Fit Item Count")]
    [SerializeField, Min(0)] private int targetItemCount = 12;
    [SerializeField, Min(0)] private int preferredColumns;
    [SerializeField, Min(0)] private int preferredRows;
    [SerializeField] private bool preserveAspectRatio = true;
    [SerializeField, Min(0.05f)] private float cellAspectRatio = 1f;
    [SerializeField] private bool useMinCellSize = true;
    [SerializeField] private Vector2 minCellSize = new Vector2(56f, 56f);
    [SerializeField] private bool useMaxCellSize = true;
    [SerializeField] private Vector2 maxCellSize = new Vector2(180f, 180f);
    [SerializeField] private bool allowBelowMinWhenNeeded = true;

    [Header("Runtime")]
    [SerializeField] private bool rebuildOnEnable = true;
    [SerializeField] private bool rebuildOnRectChange = true;
    [SerializeField] private bool deferRuntimeRebuildOneFrame = true;

    private List<RectTransform> _spawnedItems;
    private HashSet<RectTransform> _hiddenOverflowItems;
    private RectTransform _rectTransform;
    private Coroutine _deferredRebuild;
    private Vector2 _lastRectSize = new Vector2(-1f, -1f);
#if UNITY_EDITOR
    private bool _editorRebuildQueued;
#endif

    public int Columns { get; private set; }
    public int Rows { get; private set; }
    public int Capacity { get; private set; }
    public Vector2 CellSize { get; private set; }

    private RectTransform RectTransform
    {
        get
        {
            if (_rectTransform == null)
                _rectTransform = transform as RectTransform;
            return _rectTransform;
        }
    }

    private void Awake()
    {
        EnsureRuntimeState();
        _rectTransform = transform as RectTransform;
    }

    private void OnEnable()
    {
        EnsureRuntimeState();
        if (rebuildOnEnable)
            QueueRebuild();
    }

    private void OnDisable()
    {
        if (_deferredRebuild != null)
        {
            StopCoroutine(_deferredRebuild);
            _deferredRebuild = null;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= RunDelayedEditorRebuild;
        _editorRebuildQueued = false;
#endif
    }

    private void OnValidate()
    {
        EnsureRuntimeState();
        ClampSerializedValues();
        if (!isActiveAndEnabled)
            return;

        QueueRebuild();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!rebuildOnRectChange || !isActiveAndEnabled)
            return;

        var rect = RectTransform != null ? RectTransform.rect : default;
        var currentSize = rect.size;
        if (Vector2.Distance(currentSize, _lastRectSize) < 0.1f)
            return;

        _lastRectSize = currentSize;
        QueueRebuild();
    }

    public void SetItemPrefab(GameObject prefab)
    {
        itemPrefab = prefab;
    }

    public void ConfigureFixed(
        Vector2 cellSize,
        Vector2 cellSpacing,
        RectOffset cellPadding = null,
        bool hideOverflow = true)
    {
        sizingMode = CellSizingMode.FixedCellSize;
        fixedCellSize = cellSize;
        spacing = cellSpacing;
        spacingUnits = LayoutValueMode.Pixels;
        if (cellPadding != null)
        {
            padding = cellPadding;
            paddingUnits = LayoutValueMode.Pixels;
        }
        hideFixedOverflow = hideOverflow;
        QueueRebuild();
    }

    public void ConfigureFitItemCount(
        int itemCount,
        Vector2 cellSpacing,
        RectOffset cellPadding = null,
        Vector2? minSize = null,
        Vector2? maxSize = null,
        int columns = 0,
        int rows = 0)
    {
        sizingMode = CellSizingMode.FitItemCount;
        targetItemCount = Mathf.Max(0, itemCount);
        spacing = cellSpacing;
        spacingUnits = LayoutValueMode.Pixels;
        if (cellPadding != null)
        {
            padding = cellPadding;
            paddingUnits = LayoutValueMode.Pixels;
        }
        if (minSize.HasValue)
            minCellSize = minSize.Value;
        if (maxSize.HasValue)
            maxCellSize = maxSize.Value;
        preferredColumns = Mathf.Max(0, columns);
        preferredRows = Mathf.Max(0, rows);
        QueueRebuild();
    }

    public void ConfigurePercentLayout(Vector2 cellSpacingPercent, Vector4 cellPaddingPercent)
    {
        spacingUnits = LayoutValueMode.Percent;
        paddingUnits = LayoutValueMode.Percent;
        spacingPercent = cellSpacingPercent;
        paddingPercent = PercentPadding.FromVector4(cellPaddingPercent);
        QueueRebuild();
    }

    public T SpawnObject<T>(GameObject prefabOverride = null) where T : Component
    {
        var spawned = SpawnObject(prefabOverride);
        return spawned != null ? spawned.GetComponent<T>() : null;
    }

    public GameObject SpawnObject(GameObject prefabOverride = null)
    {
        EnsureRuntimeState();
        PruneSpawnedItems();

        var prefab = prefabOverride != null ? prefabOverride : itemPrefab;
        if (prefab == null)
            return null;

        var spawned = Instantiate(prefab, transform);
        spawned.name = prefab.name;
        spawned.SetActive(true);

        var rect = spawned.transform as RectTransform;
        if (rect != null && !_spawnedItems.Contains(rect))
            _spawnedItems.Add(rect);

        QueueRebuild();
        return spawned;
    }

    public void EnsureSpawnedItemCount(int count)
    {
        count = Mathf.Max(0, count);
        PruneSpawnedItems();

        while (_spawnedItems.Count < count)
            SpawnObject();

        while (_spawnedItems.Count > count)
        {
            var index = _spawnedItems.Count - 1;
            var item = _spawnedItems[index];
            _spawnedItems.RemoveAt(index);
            if (item != null)
                item.SetParent(null, false);
            DestroyItem(item);
        }

        if (sizingMode == CellSizingMode.FitItemCount)
            targetItemCount = count;

        QueueRebuild();
    }

    public void ClearSpawnedItems()
    {
        PruneSpawnedItems();
        for (var i = _spawnedItems.Count - 1; i >= 0; i--)
        {
            if (_spawnedItems[i] != null)
                _spawnedItems[i].SetParent(null, false);
            DestroyItem(_spawnedItems[i]);
        }

        _spawnedItems.Clear();
        QueueRebuild();
    }

    public void Rebuild()
    {
        EnsureRuntimeState();
        _deferredRebuild = null;
        ClampSerializedValues();

        var root = RectTransform;
        if (root == null)
            return;

        var rect = root.rect;
        var layout = ResolveLayout(rect.size);
        var availableSize = new Vector2(
            Mathf.Max(0f, rect.width - layout.PaddingHorizontal),
            Mathf.Max(0f, rect.height - layout.PaddingVertical));

        var items = CollectLayoutItems();
        var requestedCount = sizingMode == CellSizingMode.FitItemCount
            ? Mathf.Max(targetItemCount, items.Count)
            : items.Count;

        var metrics = CalculateMetrics(availableSize, Mathf.Max(0, requestedCount), layout.Spacing);
        var visibleItemCount = ResolveVisibleItemCount(items.Count, requestedCount, metrics);
        var occupied = CalculateOccupiedMetrics(metrics, visibleItemCount);
        Columns = metrics.columns;
        Rows = metrics.rows;
        Capacity = metrics.capacity;
        CellSize = metrics.cellSize;

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item == null)
                continue;

            var overflow = sizingMode == CellSizingMode.FixedCellSize && hideFixedOverflow && i >= Capacity;
            if (overflow)
            {
                _hiddenOverflowItems.Add(item);
                item.gameObject.SetActive(false);
                continue;
            }

            if (_hiddenOverflowItems.Remove(item) && !item.gameObject.activeSelf)
                item.gameObject.SetActive(true);

            ApplyItemRect(item, i, metrics, occupied, layout);
        }
    }

    private void QueueRebuild()
    {
        EnsureRuntimeState();
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            QueueEditorRebuild();
            return;
        }
#endif

        if (!Application.isPlaying)
        {
            Rebuild();
            return;
        }

        if (!deferRuntimeRebuildOneFrame)
        {
            Rebuild();
            return;
        }

        if (_deferredRebuild != null)
            return;

        _deferredRebuild = StartCoroutine(RebuildNextFrame());
    }

#if UNITY_EDITOR
    private void QueueEditorRebuild()
    {
        if (_editorRebuildQueued)
            return;

        _editorRebuildQueued = true;
        UnityEditor.EditorApplication.delayCall += RunDelayedEditorRebuild;
    }

    private void RunDelayedEditorRebuild()
    {
        UnityEditor.EditorApplication.delayCall -= RunDelayedEditorRebuild;
        _editorRebuildQueued = false;

        if (this == null || !isActiveAndEnabled)
            return;

        Rebuild();
    }
#endif

    private IEnumerator RebuildNextFrame()
    {
        yield return null;
        Rebuild();
    }

    private LayoutMetrics CalculateMetrics(Vector2 availableSize, int itemCount, Vector2 resolvedSpacing)
    {
        if (sizingMode == CellSizingMode.FitItemCount)
            return CalculateFitMetrics(availableSize, itemCount, resolvedSpacing);

        var cell = ClampPositiveSize(fixedCellSize, new Vector2(1f, 1f));
        var columns = CalculateFitCount(availableSize.x, cell.x, resolvedSpacing.x);
        var rows = CalculateFitCount(availableSize.y, cell.y, resolvedSpacing.y);
        return new LayoutMetrics
        {
            columns = columns,
            rows = rows,
            capacity = Mathf.Max(0, columns * rows),
            cellSize = cell
        };
    }

    private LayoutMetrics CalculateFitMetrics(Vector2 availableSize, int itemCount, Vector2 resolvedSpacing)
    {
        if (itemCount <= 0)
        {
            return new LayoutMetrics
            {
                columns = 0,
                rows = 0,
                capacity = 0,
                cellSize = ClampPositiveSize(fixedCellSize, new Vector2(1f, 1f))
            };
        }

        if (preferredColumns > 0)
        {
            var columns = Mathf.Max(1, preferredColumns);
            var rows = Mathf.CeilToInt(itemCount / (float)columns);
            return BuildFitMetrics(availableSize, itemCount, columns, rows, resolvedSpacing);
        }

        if (preferredRows > 0)
        {
            var rows = Mathf.Max(1, preferredRows);
            var columns = Mathf.CeilToInt(itemCount / (float)rows);
            return BuildFitMetrics(availableSize, itemCount, columns, rows, resolvedSpacing);
        }

        var best = new LayoutMetrics();
        var bestScore = float.NegativeInfinity;

        for (var columns = 1; columns <= itemCount; columns++)
        {
            var rows = Mathf.CeilToInt(itemCount / (float)columns);
            var candidate = BuildFitMetrics(availableSize, itemCount, columns, rows, resolvedSpacing);
            var belowMinPenalty = useMinCellSize && (candidate.cellSize.x < minCellSize.x || candidate.cellSize.y < minCellSize.y)
                ? -1000000f
                : 0f;
            var score = candidate.cellSize.x * candidate.cellSize.y + belowMinPenalty;
            if (score <= bestScore)
                continue;

            bestScore = score;
            best = candidate;
        }

        return best.capacity > 0 ? best : BuildFitMetrics(availableSize, itemCount, 1, itemCount, resolvedSpacing);
    }

    private LayoutMetrics BuildFitMetrics(Vector2 availableSize, int itemCount, int columns, int rows, Vector2 resolvedSpacing)
    {
        columns = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rows);

        var rawWidth = (availableSize.x - resolvedSpacing.x * (columns - 1)) / columns;
        var rawHeight = (availableSize.y - resolvedSpacing.y * (rows - 1)) / rows;
        var cell = new Vector2(Mathf.Max(1f, rawWidth), Mathf.Max(1f, rawHeight));

        if (preserveAspectRatio)
        {
            var aspect = Mathf.Max(0.05f, cellAspectRatio);
            var widthFromHeight = cell.y * aspect;
            if (widthFromHeight <= cell.x)
                cell.x = widthFromHeight;
            else
                cell.y = cell.x / aspect;
        }

        if (useMaxCellSize)
        {
            var maxSize = NormalizeMaxSize(maxCellSize);
            cell.x = Mathf.Min(cell.x, maxSize.x);
            cell.y = Mathf.Min(cell.y, maxSize.y);
        }

        if (useMinCellSize && (!allowBelowMinWhenNeeded || (cell.x >= minCellSize.x && cell.y >= minCellSize.y)))
        {
            cell.x = Mathf.Max(cell.x, minCellSize.x);
            cell.y = Mathf.Max(cell.y, minCellSize.y);
        }

        return new LayoutMetrics
        {
            columns = columns,
            rows = rows,
            capacity = Mathf.Max(itemCount, columns * rows),
            cellSize = cell
        };
    }

    private int ResolveVisibleItemCount(int itemCount, int requestedCount, LayoutMetrics metrics)
    {
        var alignmentCount = sizingMode == CellSizingMode.FitItemCount
            ? Mathf.Max(itemCount, requestedCount)
            : itemCount;

        if (sizingMode == CellSizingMode.FixedCellSize && hideFixedOverflow)
            alignmentCount = Mathf.Min(alignmentCount, metrics.capacity);

        return Mathf.Max(0, alignmentCount);
    }

    private LayoutMetrics CalculateOccupiedMetrics(LayoutMetrics metrics, int itemCount)
    {
        if (itemCount <= 0 || metrics.columns <= 0 || metrics.rows <= 0)
        {
            metrics.usedColumns = 0;
            metrics.usedRows = 0;
            return metrics;
        }

        if (fillDirection == FillDirection.VerticalThenHorizontal)
        {
            metrics.usedRows = Mathf.Min(metrics.rows, itemCount);
            metrics.usedColumns = Mathf.CeilToInt(itemCount / (float)Mathf.Max(1, metrics.rows));
            if (sizingMode == CellSizingMode.FixedCellSize && hideFixedOverflow)
                metrics.usedColumns = Mathf.Min(metrics.usedColumns, metrics.columns);
        }
        else
        {
            metrics.usedColumns = Mathf.Min(metrics.columns, itemCount);
            metrics.usedRows = Mathf.CeilToInt(itemCount / (float)Mathf.Max(1, metrics.columns));
            if (sizingMode == CellSizingMode.FixedCellSize && hideFixedOverflow)
                metrics.usedRows = Mathf.Min(metrics.usedRows, metrics.rows);
        }

        metrics.usedColumns = Mathf.Max(1, metrics.usedColumns);
        metrics.usedRows = Mathf.Max(1, metrics.usedRows);
        return metrics;
    }

    private void ApplyItemRect(RectTransform item, int index, LayoutMetrics metrics, LayoutMetrics occupied, ResolvedLayout layout)
    {
        if (metrics.columns <= 0 || metrics.rows <= 0)
            return;

        int column;
        int row;
        if (fillDirection == FillDirection.VerticalThenHorizontal)
        {
            row = index % metrics.rows;
            column = index / metrics.rows;
        }
        else
        {
            column = index % metrics.columns;
            row = index / metrics.columns;
        }

        var cell = metrics.cellSize;
        var step = new Vector2(cell.x + layout.Spacing.x, cell.y + layout.Spacing.y);
        var anchor = GetAnchorForStartCorner(startCorner);
        item.anchorMin = anchor;
        item.anchorMax = anchor;
        item.pivot = anchor;
        item.sizeDelta = cell;
        item.anchoredPosition = GetAnchoredPosition(column, row, step, occupied, layout);
    }

    private Vector2 GetAnchoredPosition(int column, int row, Vector2 step, LayoutMetrics occupied, ResolvedLayout layout)
    {
        var cell = occupied.cellSize;
        var blockWidth = occupied.usedColumns * cell.x + Mathf.Max(0, occupied.usedColumns - 1) * layout.Spacing.x;
        var blockHeight = occupied.usedRows * cell.y + Mathf.Max(0, occupied.usedRows - 1) * layout.Spacing.y;

        switch (startCorner)
        {
            case StartCorner.UpperRight:
                return new Vector2(-layout.Right - column * step.x, -layout.Top - row * step.y);
            case StartCorner.UpperCenter:
                return new Vector2(-blockWidth * 0.5f + cell.x * 0.5f + column * step.x, -layout.Top - row * step.y);
            case StartCorner.LowerLeft:
                return new Vector2(layout.Left + column * step.x, layout.Bottom + row * step.y);
            case StartCorner.LowerRight:
                return new Vector2(-layout.Right - column * step.x, layout.Bottom + row * step.y);
            case StartCorner.LowerCenter:
                return new Vector2(-blockWidth * 0.5f + cell.x * 0.5f + column * step.x, layout.Bottom + row * step.y);
            case StartCorner.MiddleLeft:
                return new Vector2(layout.Left + column * step.x, blockHeight * 0.5f - cell.y * 0.5f - row * step.y);
            case StartCorner.MiddleRight:
                return new Vector2(-layout.Right - column * step.x, blockHeight * 0.5f - cell.y * 0.5f - row * step.y);
            case StartCorner.MiddleCenter:
                return new Vector2(
                    -blockWidth * 0.5f + cell.x * 0.5f + column * step.x,
                    blockHeight * 0.5f - cell.y * 0.5f - row * step.y);
            default:
                return new Vector2(layout.Left + column * step.x, -layout.Top - row * step.y);
        }
    }

    private static Vector2 GetAnchorForStartCorner(StartCorner corner)
    {
        switch (corner)
        {
            case StartCorner.UpperRight:
                return new Vector2(1f, 1f);
            case StartCorner.UpperCenter:
                return new Vector2(0.5f, 1f);
            case StartCorner.LowerLeft:
                return new Vector2(0f, 0f);
            case StartCorner.LowerRight:
                return new Vector2(1f, 0f);
            case StartCorner.LowerCenter:
                return new Vector2(0.5f, 0f);
            case StartCorner.MiddleLeft:
                return new Vector2(0f, 0.5f);
            case StartCorner.MiddleRight:
                return new Vector2(1f, 0.5f);
            case StartCorner.MiddleCenter:
                return new Vector2(0.5f, 0.5f);
            default:
                return new Vector2(0f, 1f);
        }
    }

    private List<RectTransform> CollectLayoutItems()
    {
        EnsureRuntimeState();
        var result = new List<RectTransform>();
        var prefabTransform = itemPrefab != null ? itemPrefab.transform : null;

        for (var i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i) as RectTransform;
            if (child == null)
                continue;
            if (skipPrefabWhenItIsChild && prefabTransform != null && child == prefabTransform)
                continue;
            if (!includeInactiveChildren && !child.gameObject.activeSelf && !_hiddenOverflowItems.Contains(child))
                continue;

            result.Add(child);
        }

        return result;
    }

    private void PruneSpawnedItems()
    {
        EnsureRuntimeState();
        for (var i = _spawnedItems.Count - 1; i >= 0; i--)
        {
            if (_spawnedItems[i] == null)
                _spawnedItems.RemoveAt(i);
        }
    }

    private static void DestroyItem(RectTransform item)
    {
        if (item == null)
            return;

        if (Application.isPlaying)
            Destroy(item.gameObject);
        else
            DestroyImmediate(item.gameObject);
    }

    private void ClampSerializedValues()
    {
        EnsureRuntimeState();
        fixedCellSize = ClampPositiveSize(fixedCellSize, new Vector2(1f, 1f));
        minCellSize = ClampPositiveSize(minCellSize, new Vector2(1f, 1f));
        maxCellSize.x = Mathf.Max(0f, maxCellSize.x);
        maxCellSize.y = Mathf.Max(0f, maxCellSize.y);
        spacing.x = Mathf.Max(0f, spacing.x);
        spacing.y = Mathf.Max(0f, spacing.y);
        spacingPercent.x = Mathf.Clamp(spacingPercent.x, 0f, 100f);
        spacingPercent.y = Mathf.Clamp(spacingPercent.y, 0f, 100f);
        paddingPercent.Clamp();
        targetItemCount = Mathf.Max(0, targetItemCount);
        preferredColumns = Mathf.Max(0, preferredColumns);
        preferredRows = Mathf.Max(0, preferredRows);
        cellAspectRatio = Mathf.Max(0.05f, cellAspectRatio);

    }

    private void EnsureRuntimeState()
    {
        if (padding == null)
            padding = new RectOffset(12, 12, 12, 12);
        if (_spawnedItems == null)
            _spawnedItems = new List<RectTransform>();
        if (_hiddenOverflowItems == null)
            _hiddenOverflowItems = new HashSet<RectTransform>();
    }

    private ResolvedLayout ResolveLayout(Vector2 rootSize)
    {
        var resolved = new ResolvedLayout
        {
            Left = padding.left,
            Right = padding.right,
            Top = padding.top,
            Bottom = padding.bottom,
            Spacing = spacing
        };

        if (paddingUnits == LayoutValueMode.Percent)
        {
            resolved.Left = rootSize.x * paddingPercent.Left * 0.01f;
            resolved.Right = rootSize.x * paddingPercent.Right * 0.01f;
            resolved.Top = rootSize.y * paddingPercent.Top * 0.01f;
            resolved.Bottom = rootSize.y * paddingPercent.Bottom * 0.01f;
        }

        if (spacingUnits == LayoutValueMode.Percent)
        {
            resolved.Spacing = new Vector2(
                rootSize.x * spacingPercent.x * 0.01f,
                rootSize.y * spacingPercent.y * 0.01f);
        }

        return resolved;
    }

    private static Vector2 ClampPositiveSize(Vector2 value, Vector2 fallback)
    {
        if (value.x <= 0f)
            value.x = fallback.x;
        if (value.y <= 0f)
            value.y = fallback.y;
        return value;
    }

    private static Vector2 NormalizeMaxSize(Vector2 value)
    {
        if (value.x <= 0f)
            value.x = float.PositiveInfinity;
        if (value.y <= 0f)
            value.y = float.PositiveInfinity;
        return value;
    }

    private static int CalculateFitCount(float available, float cell, float gap)
    {
        if (available <= 0f || cell <= 0f)
            return 0;

        return Mathf.Max(1, Mathf.FloorToInt((available + gap) / (cell + gap)));
    }

    private struct LayoutMetrics
    {
        public int columns;
        public int rows;
        public int usedColumns;
        public int usedRows;
        public int capacity;
        public Vector2 cellSize;
    }

    private struct ResolvedLayout
    {
        public float Left;
        public float Right;
        public float Top;
        public float Bottom;
        public Vector2 Spacing;

        public float PaddingHorizontal => Left + Right;
        public float PaddingVertical => Top + Bottom;
    }

    [System.Serializable]
    private struct PercentPadding
    {
        public float Left;
        public float Right;
        public float Top;
        public float Bottom;

        public static PercentPadding FromVector4(Vector4 value)
        {
            return new PercentPadding
            {
                Left = value.x,
                Right = value.y,
                Top = value.z,
                Bottom = value.w
            };
        }

        public void Clamp()
        {
            Left = Mathf.Clamp(Left, 0f, 100f);
            Right = Mathf.Clamp(Right, 0f, 100f);
            Top = Mathf.Clamp(Top, 0f, 100f);
            Bottom = Mathf.Clamp(Bottom, 0f, 100f);
        }
    }
}
