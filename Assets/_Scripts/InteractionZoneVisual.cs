using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(BoxCollider))]
public sealed class InteractionZoneVisual : MonoBehaviour
{
    private const string RootName = "InteractionZoneVisual";

    [SerializeField] private Color fillColor = new Color(0.18f, 1f, 0.35f, 0.22f);
    [SerializeField] private Color borderColor = new Color(0.74f, 1f, 0.64f, 0.72f);
    [SerializeField] private float verticalOffset = 0.035f;
    [SerializeField] private float borderWidth = 0.08f;
    [SerializeField] private int segments = 48;
    [SerializeField] private bool pulse = true;
    [SerializeField] private float pulseSpeed = 1.35f;
    [SerializeField] private float pulseScale = 0.035f;

    private Transform _visualRoot;
    private Material _fillMaterial;
    private Material _borderMaterial;

    private void Awake()
    {
        Rebuild();
    }

    private void OnEnable()
    {
        if (_visualRoot == null)
            Rebuild();
        if (_visualRoot != null)
            _visualRoot.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (_visualRoot != null)
            _visualRoot.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        DestroyMaterial(_fillMaterial);
        DestroyMaterial(_borderMaterial);
    }

    private void Update()
    {
        if (!pulse || _visualRoot == null)
            return;

        float t = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseScale;
        _visualRoot.localScale = new Vector3(t, 1f, t);
    }

    public void Rebuild()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null)
            return;

        DestroyExistingVisual();

        segments = Mathf.Clamp(segments, 16, 96);
        borderWidth = Mathf.Max(0.01f, borderWidth);

        var root = new GameObject(RootName);
        root.layer = gameObject.layer;
        _visualRoot = root.transform;
        _visualRoot.SetParent(transform, false);
        _visualRoot.localPosition = box.center + Vector3.up * (box.size.y * 0.5f + verticalOffset);
        _visualRoot.localRotation = Quaternion.identity;
        _visualRoot.localScale = Vector3.one;

        float radiusX = Mathf.Max(0.05f, box.size.x * 0.5f);
        float radiusZ = Mathf.Max(0.05f, box.size.z * 0.5f);

        _fillMaterial = CreateTransparentMaterial(fillColor, "Interaction Zone Fill");
        _borderMaterial = CreateTransparentMaterial(borderColor, "Interaction Zone Border");

        CreateMeshObject("Fill", CreateDiscMesh(radiusX, radiusZ), _fillMaterial);
        CreateMeshObject("Border", CreateRingMesh(radiusX, radiusZ, borderWidth), _borderMaterial);
    }

    private void DestroyExistingVisual()
    {
        var existing = transform.Find(RootName);
        if (existing == null)
            return;

        Destroy(existing.gameObject);
    }

    private void CreateMeshObject(string objectName, Mesh mesh, Material material)
    {
        var go = new GameObject(objectName);
        go.layer = gameObject.layer;
        go.transform.SetParent(_visualRoot, false);

        var meshFilter = go.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        var renderer = go.AddComponent<MeshRenderer>();
        if (material != null)
            renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private Mesh CreateDiscMesh(float radiusX, float radiusZ)
    {
        var mesh = new Mesh { name = "Interaction Zone Disc" };
        var vertices = new Vector3[segments + 1];
        var triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        for (int i = 0; i < segments; i++)
        {
            float angle = (Mathf.PI * 2f * i) / segments;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radiusX, 0f, Mathf.Sin(angle) * radiusZ);
        }

        for (int i = 0; i < segments; i++)
        {
            int next = i == segments - 1 ? 1 : i + 2;
            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = next;
            triangles[triangleIndex + 2] = i + 1;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private Mesh CreateRingMesh(float radiusX, float radiusZ, float width)
    {
        var mesh = new Mesh { name = "Interaction Zone Ring" };
        var vertices = new Vector3[segments * 2];
        var triangles = new int[segments * 6];

        float innerX = Mathf.Max(0.01f, radiusX - width);
        float innerZ = Mathf.Max(0.01f, radiusZ - width);

        for (int i = 0; i < segments; i++)
        {
            float angle = (Mathf.PI * 2f * i) / segments;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            vertices[i * 2] = new Vector3(cos * innerX, 0.004f, sin * innerZ);
            vertices[i * 2 + 1] = new Vector3(cos * radiusX, 0.004f, sin * radiusZ);
        }

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            int i0 = i * 2;
            int i1 = i * 2 + 1;
            int n0 = next * 2;
            int n1 = next * 2 + 1;
            int triangleIndex = i * 6;
            triangles[triangleIndex] = i0;
            triangles[triangleIndex + 1] = n0;
            triangles[triangleIndex + 2] = n1;
            triangles[triangleIndex + 3] = i0;
            triangles[triangleIndex + 4] = n1;
            triangles[triangleIndex + 5] = i1;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material CreateTransparentMaterial(Color color, string materialName)
    {
        var shader = Shader.Find("Sprites/Default")
                     ?? Shader.Find("Unlit/Transparent")
                     ?? Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Standard");

        if (shader == null)
            return null;

        var material = new Material(shader) { name = materialName };
        ApplyMaterialColor(material, color);
        ConfigureTransparency(material);
        return material;
    }

    private static void ApplyMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private static void ConfigureTransparency(Material material)
    {
        material.SetOverrideTag("RenderType", "Transparent");
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_AlphaClip"))
            material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetInt("_ZWrite", 0);
        if (material.HasProperty("_Cull"))
            material.SetInt("_Cull", (int)CullMode.Off);
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private static void DestroyMaterial(Material material)
    {
        if (material == null)
            return;

        if (Application.isPlaying)
            Destroy(material);
        else
            DestroyImmediate(material);
    }
}
