using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WaterSurface : MonoBehaviour
{
    private static readonly List<WaterSurface> ActiveSurfaces = new();

    [SerializeField] private Renderer boundsRenderer;
    [SerializeField] private Collider boundsCollider;
    [SerializeField] private float surfaceHeightOffset;
    [SerializeField] private float horizontalPadding = 0.75f;
    [SerializeField] private bool makeCollidersTriggers = true;
    [SerializeField] private Collider[] collidersToTrigger;

    public float SurfaceY => transform.position.y + surfaceHeightOffset;

    public static bool TryFindBestContaining(Vector3 worldPosition, out WaterSurface surface)
    {
        surface = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < ActiveSurfaces.Count; i++)
        {
            WaterSurface candidate = ActiveSurfaces[i];
            if (candidate == null || !candidate.isActiveAndEnabled)
                continue;

            if (!candidate.ContainsHorizontal(worldPosition))
                continue;

            float distance = Mathf.Abs(worldPosition.y - candidate.SurfaceY);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            surface = candidate;
        }

        return surface != null;
    }

    public bool ContainsHorizontal(Vector3 worldPosition)
    {
        Bounds bounds = GetWorldBounds();
        bounds.Expand(new Vector3(horizontalPadding * 2f, 1000f, horizontalPadding * 2f));
        return worldPosition.x >= bounds.min.x &&
               worldPosition.x <= bounds.max.x &&
               worldPosition.z >= bounds.min.z &&
               worldPosition.z <= bounds.max.z;
    }

    private void Reset()
    {
        boundsRenderer = GetComponent<Renderer>();
        boundsCollider = GetComponent<Collider>();
        collidersToTrigger = GetComponents<Collider>();
    }

    private void OnValidate()
    {
        horizontalPadding = Mathf.Max(0f, horizontalPadding);
        if (boundsRenderer == null)
            boundsRenderer = GetComponent<Renderer>();
        if (boundsCollider == null)
            boundsCollider = GetComponent<Collider>();
        if (collidersToTrigger == null || collidersToTrigger.Length == 0)
            collidersToTrigger = GetComponents<Collider>();
    }

    private void OnEnable()
    {
        if (!ActiveSurfaces.Contains(this))
            ActiveSurfaces.Add(this);

        ApplyColliderMode();
    }

    private void OnDisable()
    {
        ActiveSurfaces.Remove(this);
    }

    private Bounds GetWorldBounds()
    {
        if (boundsRenderer != null)
            return boundsRenderer.bounds;
        if (boundsCollider != null)
            return boundsCollider.bounds;

        return new Bounds(transform.position, Vector3.one);
    }

    private void ApplyColliderMode()
    {
        if (!makeCollidersTriggers || collidersToTrigger == null)
            return;

        for (int i = 0; i < collidersToTrigger.Length; i++)
        {
            Collider col = collidersToTrigger[i];
            if (col != null)
                col.isTrigger = true;
        }
    }
}
