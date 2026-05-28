using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MovingRoadVisualScroller : MonoBehaviour
{
    private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");
    private static readonly int MainTexSt = Shader.PropertyToID("_MainTex_ST");

    [SerializeField] private Renderer[] renderers;
    [SerializeField] private Vector2 uvDirection = Vector2.right;
    [SerializeField] private float scrollSpeed = 0.35f;

    private MaterialPropertyBlock _propertyBlock;
    private Entry[] _entries;
    private Vector2 _offset;

    public float ScrollSpeed
    {
        get => scrollSpeed;
        set => scrollSpeed = value;
    }

    public void Configure(Renderer[] targetRenderers, Vector2 direction, float speed)
    {
        renderers = targetRenderers;
        uvDirection = direction.sqrMagnitude > 0.0001f ? direction : Vector2.right;
        scrollSpeed = speed;
        _entries = null;

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

        Vector2 direction = uvDirection.normalized;
        _offset += direction * (scrollSpeed * Time.deltaTime);
        _offset.x = Mathf.Repeat(_offset.x, 1f);
        _offset.y = Mathf.Repeat(_offset.y, 1f);

        ApplyOffset();
    }

    private void ApplyOffset()
    {
        if (_propertyBlock == null)
            _propertyBlock = new MaterialPropertyBlock();

        for (int i = 0; i < _entries.Length; i++)
        {
            Entry entry = _entries[i];
            Renderer targetRenderer = entry.Renderer;
            if (targetRenderer == null)
                continue;

            targetRenderer.GetPropertyBlock(_propertyBlock, entry.MaterialIndex);
            _propertyBlock.SetVector(BaseMapSt, new Vector4(entry.BaseMapScale.x, entry.BaseMapScale.y, _offset.x, _offset.y));
            _propertyBlock.SetVector(MainTexSt, new Vector4(entry.MainTexScale.x, entry.MainTexScale.y, _offset.x, _offset.y));

            targetRenderer.SetPropertyBlock(_propertyBlock, entry.MaterialIndex);
            _propertyBlock.Clear();
        }
    }

    private void RebuildEntries()
    {
        if (renderers == null || renderers.Length == 0)
        {
            _entries = new Entry[0];
            return;
        }

        List<Entry> entries = new List<Entry>();
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer targetRenderer = renderers[rendererIndex];
            if (targetRenderer == null)
                continue;

            Material[] materials = targetRenderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                entries.Add(new Entry
                {
                    Renderer = targetRenderer,
                    MaterialIndex = materialIndex,
                    BaseMapScale = GetTextureScale(material, "_BaseMap"),
                    MainTexScale = GetTextureScale(material, "_MainTex")
                });
            }
        }

        _entries = entries.ToArray();
    }

    private static Vector2 GetTextureScale(Material material, string propertyName)
    {
        if (material == null || !material.HasProperty(propertyName))
            return Vector2.one;

        return material.GetTextureScale(propertyName);
    }

    private struct Entry
    {
        public Renderer Renderer;
        public int MaterialIndex;
        public Vector2 BaseMapScale;
        public Vector2 MainTexScale;
    }
}
