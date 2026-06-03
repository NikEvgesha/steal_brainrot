using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MovingRoadVisualScroller : MonoBehaviour
{
    private const string LeftContainerName = "strelka_left";
    private const string RightContainerName = "strelka_right";

    private static readonly List<SharedGroup> Groups = new();

    [SerializeField] private Renderer[] renderers;
    [SerializeField] private Vector2 uvDirection = Vector2.right;
    [SerializeField] private float scrollSpeed = 5f;
    [SerializeField] private bool autoCollectChildRenderers = true;
    [SerializeField] private bool includeInactiveChildRenderers;
    [SerializeField] private float stripLengthOverride;
    [SerializeField] private float visualSpacing;

    private SharedGroup _group;
    private bool _registered;

    public float ScrollSpeed
    {
        get => scrollSpeed;
        set
        {
            scrollSpeed = value;
            _group?.RequestRebuild();
        }
    }

    public void Configure(Renderer[] targetRenderers, Vector2 direction, float speed)
    {
        UnregisterFromGroup();

        renderers = targetRenderers;
        uvDirection = direction.sqrMagnitude > 0.0001f ? direction : Vector2.right;
        scrollSpeed = speed;

        if (isActiveAndEnabled && Application.isPlaying)
            RegisterToGroup();
    }

    private void Reset()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void OnValidate()
    {
        if (uvDirection.sqrMagnitude < 0.0001f)
            uvDirection = Vector2.right;

        stripLengthOverride = Mathf.Max(0f, stripLengthOverride);
        visualSpacing = Mathf.Max(0f, visualSpacing);

        if (Application.isPlaying)
            _group?.RequestRebuild();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
            RegisterToGroup();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || !_registered)
            return;

        _group?.Tick(Time.deltaTime);
    }

    private void OnDisable()
    {
        UnregisterFromGroup();
    }

    private void OnDestroy()
    {
        UnregisterFromGroup();
    }

    private void OnTransformChildrenChanged()
    {
        if (!Application.isPlaying || !_registered)
            return;

        _group?.RequestRebuild();
    }

    private void RegisterToGroup()
    {
        if (_registered)
            return;

        Transform sharedRoot = ResolveSharedRoot();
        string containerName = ResolveContainerName();
        CleanupLegacyLocalContainer(containerName);

        _group = GetOrCreateGroup(sharedRoot, containerName);
        _group.Register(this);
        _registered = true;
    }

    private void UnregisterFromGroup()
    {
        if (!_registered)
            return;

        SharedGroup group = _group;
        _registered = false;
        _group = null;

        if (group != null)
            group.Unregister(this);
    }

    private Transform ResolveSharedRoot()
    {
        Transform prefabRoot = transform.parent;
        if (prefabRoot != null && prefabRoot.parent != null)
            return prefabRoot.parent;

        return prefabRoot != null ? prefabRoot : transform;
    }

    private string ResolveContainerName()
    {
        string laneName = gameObject.name.ToLowerInvariant();
        if (laneName.Contains("left"))
            return LeftContainerName;

        if (laneName.Contains("right"))
            return RightContainerName;

        return uvDirection.x >= 0f ? LeftContainerName : RightContainerName;
    }

    private Vector3 GetSharedAxis(Transform sharedRoot)
    {
        Vector3 localAxis = new Vector3(uvDirection.x, 0f, uvDirection.y);
        if (localAxis.sqrMagnitude < 0.0001f)
            localAxis = Vector3.right;

        Vector3 worldAxis = transform.TransformVector(localAxis.normalized);
        Vector3 sharedAxis = sharedRoot.InverseTransformVector(worldAxis);
        sharedAxis.y = 0f;
        return sharedAxis.sqrMagnitude > 0.0001f ? sharedAxis.normalized : Vector3.right;
    }

    private Renderer[] ResolveRenderers()
    {
        if (!autoCollectChildRenderers)
            return renderers;

        return GetComponentsInChildren<Renderer>(includeInactiveChildRenderers);
    }

    private bool IsTemplateArrowRenderer(Renderer targetRenderer)
    {
        if (targetRenderer == null || !targetRenderer.transform.IsChildOf(transform))
            return false;

        if (!includeInactiveChildRenderers && !targetRenderer.gameObject.activeInHierarchy)
            return false;

        if (IsAssignedRenderer(targetRenderer))
            return true;

        return targetRenderer.gameObject.name.IndexOf("strelka", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool IsAssignedRenderer(Renderer targetRenderer)
    {
        if (renderers == null)
            return false;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == targetRenderer)
                return true;
        }

        return false;
    }

    private void CleanupLegacyLocalContainer(string containerName)
    {
        Transform prefabRoot = transform.parent;
        if (prefabRoot == null)
            return;

        Transform oldContainer = prefabRoot.Find(containerName);
        if (oldContainer != null)
            DestroyRuntimeObject(oldContainer.gameObject);
    }

    private static SharedGroup GetOrCreateGroup(Transform root, string containerName)
    {
        for (int i = Groups.Count - 1; i >= 0; i--)
        {
            SharedGroup group = Groups[i];
            if (group.Root == null)
            {
                group.Cleanup();
                Groups.RemoveAt(i);
                continue;
            }

            if (group.Root == root && group.ContainerName == containerName)
                return group;
        }

        SharedGroup newGroup = new SharedGroup(root, containerName);
        Groups.Add(newGroup);
        return newGroup;
    }

    private sealed class SharedGroup
    {
        private readonly List<MovingRoadVisualScroller> _contributors = new();
        private readonly List<SourceRendererState> _sourceStates = new();
        private readonly List<RuntimeArrow> _runtimeArrows = new();

        private Transform _visualRoot;
        private Vector3 _axis;
        private float _stripStart;
        private float _stripLength;
        private float _scrollSpeed;
        private int _lastTickFrame = -1;
        private bool _needsRebuild = true;
        private bool _isRebuilding;

        public SharedGroup(Transform root, string containerName)
        {
            Root = root;
            ContainerName = containerName;
        }

        public Transform Root { get; }
        public string ContainerName { get; }

        public void Register(MovingRoadVisualScroller contributor)
        {
            if (contributor == null || _contributors.Contains(contributor))
                return;

            _contributors.Add(contributor);
            RequestRebuild();
        }

        public void Unregister(MovingRoadVisualScroller contributor)
        {
            _contributors.Remove(contributor);

            if (_contributors.Count == 0)
            {
                Cleanup();
                Groups.Remove(this);
            }
            else
            {
                RequestRebuild();
            }
        }

        public void RequestRebuild()
        {
            _needsRebuild = true;
        }

        public void Tick(float deltaTime)
        {
            if (_lastTickFrame == Time.frameCount)
                return;

            _lastTickFrame = Time.frameCount;

            if (_needsRebuild)
                Rebuild();

            if (_visualRoot == null || _runtimeArrows.Count == 0 || _stripLength <= 0.0001f || Mathf.Approximately(_scrollSpeed, 0f))
                return;

            float worldUnitsPerRootUnit = Root.TransformVector(_axis).magnitude;
            if (worldUnitsPerRootUnit <= 0.0001f)
                return;

            MoveRuntimeArrows(_scrollSpeed * deltaTime / worldUnitsPerRootUnit);
        }

        public void Cleanup()
        {
            RestoreSourceRenderers();
            _runtimeArrows.Clear();

            if (_visualRoot != null)
            {
                DestroyRuntimeObject(_visualRoot.gameObject);
                _visualRoot = null;
            }

            _stripLength = 0f;
            _stripStart = 0f;
            _needsRebuild = true;
        }

        private void Rebuild()
        {
            if (_isRebuilding)
                return;

            _isRebuilding = true;
            try
            {
                Cleanup();

                List<TemplateArrow> templates = CollectTemplates();
                if (templates.Count == 0)
                {
                    _needsRebuild = false;
                    return;
                }

                templates.Sort((a, b) => a.ProjectionCenter.CompareTo(b.ProjectionCenter));
                ResolveScrollPath(templates);
                if (_stripLength <= 0.0001f)
                {
                    _needsRebuild = false;
                    return;
                }

                CreateVisualRoot();
                CreateRuntimeArrows(templates);
                ApplyFrontArrowVisibility();
                HideSourceRenderers(templates);
                _visualRoot.localPosition = Vector3.zero;
                _needsRebuild = false;
            }
            finally
            {
                _isRebuilding = false;
            }
        }

        private List<TemplateArrow> CollectTemplates()
        {
            List<TemplateArrow> templates = new List<TemplateArrow>();
            MovingRoadVisualScroller firstContributor = null;

            for (int i = 0; i < _contributors.Count; i++)
            {
                MovingRoadVisualScroller contributor = _contributors[i];
                if (contributor == null || !contributor.isActiveAndEnabled)
                    continue;

                if (firstContributor == null)
                {
                    firstContributor = contributor;
                    _axis = firstContributor.GetSharedAxis(Root);
                    _scrollSpeed = firstContributor.scrollSpeed;
                }

                Renderer[] candidates = contributor.ResolveRenderers();
                if (candidates == null)
                    continue;

                for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
                {
                    Renderer candidate = candidates[candidateIndex];
                    if (!contributor.IsTemplateArrowRenderer(candidate))
                        continue;

                    templates.Add(CreateTemplate(contributor, candidate));
                }
            }

            return templates;
        }

        private TemplateArrow CreateTemplate(MovingRoadVisualScroller contributor, Renderer sourceRenderer)
        {
            Transform sourceTransform = sourceRenderer.transform;
            Projection projection = MeasureProjection(sourceRenderer);
            return new TemplateArrow
            {
                SourceRenderer = sourceRenderer,
                SourceTransform = sourceTransform,
                RootLocalPosition = Root.InverseTransformPoint(sourceTransform.position),
                RootLocalRotation = Quaternion.Inverse(Root.rotation) * sourceTransform.rotation,
                RootLocalScale = GetRelativeScale(sourceTransform, Root),
                ProjectionCenter = projection.Center,
                ProjectionLength = projection.Length,
                StripLengthOverride = contributor.stripLengthOverride,
                VisualSpacing = contributor.visualSpacing
            };
        }

        private void CreateVisualRoot()
        {
            Transform oldRoot = Root.Find(ContainerName);
            if (oldRoot != null)
                DestroyRuntimeObject(oldRoot.gameObject);

            GameObject rootObject = new GameObject(ContainerName);
            rootObject.hideFlags = HideFlags.DontSave;
            _visualRoot = rootObject.transform;
            _visualRoot.SetParent(Root, false);
            _visualRoot.localPosition = Vector3.zero;
            _visualRoot.localRotation = Quaternion.identity;
            _visualRoot.localScale = Vector3.one;
        }

        private void CreateRuntimeArrows(List<TemplateArrow> templates)
        {
            for (int templateIndex = 0; templateIndex < templates.Count; templateIndex++)
            {
                TemplateArrow template = templates[templateIndex];
                GameObject clone = Instantiate(template.SourceTransform.gameObject, _visualRoot);
                clone.name = "strelka_visual_" + templateIndex;
                clone.hideFlags = HideFlags.DontSave;

                Transform cloneTransform = clone.transform;
                cloneTransform.localPosition = template.RootLocalPosition;
                cloneTransform.localRotation = template.RootLocalRotation;
                cloneTransform.localScale = template.RootLocalScale;

                DisableColliders(cloneTransform);
                EnableRenderers(cloneTransform, true);

                _runtimeArrows.Add(new RuntimeArrow
                {
                    Root = cloneTransform,
                    BaseLocalPosition = template.RootLocalPosition,
                    BaseProjection = template.ProjectionCenter,
                    CurrentProjection = template.ProjectionCenter
                });
            }
        }

        private void HideSourceRenderers(List<TemplateArrow> templates)
        {
            for (int i = 0; i < templates.Count; i++)
            {
                Renderer sourceRenderer = templates[i].SourceRenderer;
                if (sourceRenderer == null)
                    continue;

                _sourceStates.Add(new SourceRendererState
                {
                    Renderer = sourceRenderer,
                    Enabled = sourceRenderer.enabled
                });

                sourceRenderer.enabled = false;
            }
        }

        private void RestoreSourceRenderers()
        {
            for (int i = 0; i < _sourceStates.Count; i++)
            {
                SourceRendererState state = _sourceStates[i];
                if (state.Renderer != null)
                    state.Renderer.enabled = state.Enabled;
            }

            _sourceStates.Clear();
        }

        private void MoveRuntimeArrows(float delta)
        {
            float stripEnd = _stripStart + _stripLength;

            for (int i = 0; i < _runtimeArrows.Count; i++)
            {
                RuntimeArrow arrow = _runtimeArrows[i];
                arrow.CurrentProjection += delta;

                while (arrow.CurrentProjection >= stripEnd)
                    arrow.CurrentProjection -= _stripLength;

                while (arrow.CurrentProjection < _stripStart)
                    arrow.CurrentProjection += _stripLength;

                arrow.Root.localPosition = arrow.BaseLocalPosition + _axis * (arrow.CurrentProjection - arrow.BaseProjection);
                _runtimeArrows[i] = arrow;
            }

            ApplyFrontArrowVisibility();
        }

        private void ApplyFrontArrowVisibility()
        {
            int hiddenIndex = FindFrontArrowIndex();
            for (int i = 0; i < _runtimeArrows.Count; i++)
            {
                RuntimeArrow arrow = _runtimeArrows[i];
                if (arrow.Root == null)
                    continue;

                bool shouldBeActive = i != hiddenIndex;
                if (arrow.Root.gameObject.activeSelf != shouldBeActive)
                    arrow.Root.gameObject.SetActive(shouldBeActive);
            }
        }

        private int FindFrontArrowIndex()
        {
            if (_runtimeArrows.Count <= 1)
                return -1;

            int frontIndex = -1;
            float frontProjection = float.MinValue;
            for (int i = 0; i < _runtimeArrows.Count; i++)
            {
                RuntimeArrow arrow = _runtimeArrows[i];
                if (arrow.Root == null || arrow.CurrentProjection <= frontProjection)
                    continue;

                frontProjection = arrow.CurrentProjection;
                frontIndex = i;
            }

            return frontIndex;
        }

        private void ResolveScrollPath(List<TemplateArrow> templates)
        {
            float stripOverride = 0f;
            float spacing = 0f;
            for (int i = 0; i < templates.Count; i++)
            {
                TemplateArrow template = templates[i];
                stripOverride = Mathf.Max(stripOverride, template.StripLengthOverride);
                spacing = Mathf.Max(spacing, template.VisualSpacing);
            }

            _stripStart = templates[0].ProjectionCenter;
            if (stripOverride > 0.0001f)
            {
                _stripLength = stripOverride;
                return;
            }

            if (templates.Count == 1)
            {
                _stripLength = Mathf.Max(0.0001f, templates[0].ProjectionLength + spacing);
                return;
            }

            float totalGap = 0f;
            for (int i = 1; i < templates.Count; i++)
                totalGap += Mathf.Max(0.0001f, templates[i].ProjectionCenter - templates[i - 1].ProjectionCenter);

            float averageGap = totalGap / (templates.Count - 1);
            _stripLength = Mathf.Max(0.0001f, averageGap * templates.Count + spacing);
        }

        private Projection MeasureProjection(Renderer targetRenderer)
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
                        Vector3 rootLocalPoint = Root.InverseTransformPoint(targetRenderer.transform.TransformPoint(localPoint));
                        float projection = Vector3.Dot(rootLocalPoint, _axis);
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
    }

    private static void EnableRenderers(Transform root, bool enabled)
    {
        Renderer[] childRenderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < childRenderers.Length; i++)
            childRenderers[i].enabled = enabled;
    }

    private static void DisableColliders(Transform root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;
    }

    private static void DestroyRuntimeObject(GameObject target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
        {
            target.SetActive(false);
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private static Vector3 GetRelativeScale(Transform target, Transform relativeTo)
    {
        Vector3 rootScale = relativeTo.lossyScale;
        Vector3 targetScale = target.lossyScale;
        return new Vector3(
            SafeDivide(targetScale.x, rootScale.x),
            SafeDivide(targetScale.y, rootScale.y),
            SafeDivide(targetScale.z, rootScale.z));
    }

    private static float SafeDivide(float value, float divisor)
    {
        return Mathf.Abs(divisor) > 0.0001f ? value / divisor : value;
    }

    private struct SourceRendererState
    {
        public Renderer Renderer;
        public bool Enabled;
    }

    private struct TemplateArrow
    {
        public Renderer SourceRenderer;
        public Transform SourceTransform;
        public Vector3 RootLocalPosition;
        public Quaternion RootLocalRotation;
        public Vector3 RootLocalScale;
        public float ProjectionCenter;
        public float ProjectionLength;
        public float StripLengthOverride;
        public float VisualSpacing;
    }

    private struct RuntimeArrow
    {
        public Transform Root;
        public Vector3 BaseLocalPosition;
        public float BaseProjection;
        public float CurrentProjection;
    }

    private struct Projection
    {
        public float Center;
        public float Length;
    }
}
