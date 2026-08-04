#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class FoodIconRendererEditor
{
    private const int OutputSize = 512;
    private const int RenderLayer = 30;
    private const float FramePadding = 1.18f;
    private const int OutlineRadius = 5;

    private readonly struct FoodIconDefinition
    {
        public readonly string PrefabPath;
        public readonly string IconPath;

        public FoodIconDefinition(string prefabName, string iconName)
        {
            PrefabPath = $"Assets/_Prefabs/Food/{prefabName}.prefab";
            IconPath = $"Assets/_Sprites/Food/{iconName}.png";
        }
    }

    private static readonly FoodIconDefinition[] Definitions =
    {
        new("Strawberry", "strawberry"),
        new("banana", "banana"),
        new("Blueberry", "blueberry"),
        new("Apple", "apple"),
        new("Pear", "pear"),
        new("Corn", "corn"),
        new("Coconut", "coconut"),
        new("Orange", "orange"),
        new("Mango", "mango"),
        new("Watermelon", "watermelon"),
        new("DragonFruit", "dragonfruit"),
        new("Carrot", "carrot")
    };

    [MenuItem("Tools/Food/Regenerate Angled Icons")]
    public static void RegenerateAngledIcons()
    {
        var previousAmbientMode = RenderSettings.ambientMode;
        var previousAmbientLight = RenderSettings.ambientLight;
        var sceneLights = UnityEngine.Object.FindObjectsByType<Light>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        var sceneLightStates = sceneLights.Select(light => light.enabled).ToArray();
        var generated = new List<FoodIconDefinition>();

        try
        {
            foreach (var sceneLight in sceneLights)
                sceneLight.enabled = false;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.42f, 0.42f, 1f);

            foreach (var definition in Definitions)
            {
                if (RenderIcon(definition))
                    generated.Add(definition);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            foreach (var definition in generated)
            {
                ConfigureSpriteImporter(definition.IconPath);
                AssignIconToFoodPrefab(definition);
            }

            // Keep the base strawberry item correct as well, even though the shop uses Strawberry.prefab.
            AssignSpriteToFoodPrefab(
                "Assets/_Prefabs/Food/food.prefab",
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Sprites/Food/strawberry.png"));

            AssetDatabase.SaveAssets();
            Debug.Log($"Regenerated {generated.Count} food icons from the in-game 3D models.");
        }
        finally
        {
            for (var index = 0; index < sceneLights.Length; index++)
            {
                if (sceneLights[index] != null)
                    sceneLights[index].enabled = sceneLightStates[index];
            }

            RenderSettings.ambientMode = previousAmbientMode;
            RenderSettings.ambientLight = previousAmbientLight;
        }
    }

    private static bool RenderIcon(FoodIconDefinition definition)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(definition.PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"Food icon source prefab was not found: {definition.PrefabPath}");
            return false;
        }

        GameObject instance = null;
        GameObject cameraObject = null;
        GameObject keyLightObject = null;
        GameObject fillLightObject = null;
        Camera camera = null;
        RenderTexture renderTexture = null;
        Texture2D renderedTexture = null;
        Texture2D outlinedTexture = null;

        try
        {
            // URP does not render regular game materials in a Unity preview scene.
            // HideAndDontSave keeps these temporary objects out of the actual scene data.
            instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                return false;

            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.SetActive(true);
            SetLayerRecursively(instance.transform, RenderLayer);

            var renderers = instance.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy)
                .ToArray();
            if (renderers.Length == 0)
            {
                Debug.LogError($"Food prefab has no visible renderer: {definition.PrefabPath}");
                return false;
            }

            var bounds = CalculateBounds(renderers);
            instance.transform.position -= bounds.center;
            bounds = CalculateBounds(renderers);

            cameraObject = new GameObject("FoodIconCamera")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = RenderLayer
            };

            camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << RenderLayer;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;

            // A slightly elevated three-quarter view exposes the front, side and top.
            var viewDirection = new Vector3(1f, 0.72f, -1f).normalized;
            camera.transform.rotation = Quaternion.LookRotation(-viewDirection, Vector3.up);
            camera.transform.position = viewDirection * Mathf.Max(3f, bounds.extents.magnitude * 6f);
            FrameBounds(camera, renderers);

            keyLightObject = CreateDirectionalLight(
                "FoodIconKeyLight",
                new Vector3(38f, -38f, 0f),
                0.82f,
                new Color(1f, 0.94f, 0.82f));
            fillLightObject = CreateDirectionalLight(
                "FoodIconFillLight",
                new Vector3(28f, 145f, 0f),
                0.24f,
                new Color(0.72f, 0.84f, 1f));

            renderTexture = new RenderTexture(OutputSize, OutputSize, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4,
                hideFlags = HideFlags.HideAndDontSave
            };
            renderTexture.Create();

            camera.targetTexture = renderTexture;
            camera.Render();

            var previousActive = RenderTexture.active;
            RenderTexture.active = renderTexture;
            renderedTexture = new Texture2D(OutputSize, OutputSize, TextureFormat.RGBA32, false, false);
            renderedTexture.ReadPixels(new Rect(0, 0, OutputSize, OutputSize), 0, 0);
            renderedTexture.Apply(false, false);
            RenderTexture.active = previousActive;

            outlinedTexture = AddOutline(
                renderedTexture,
                OutlineRadius,
                new Color32(35, 20, 12, 245));

            var absolutePath = Path.GetFullPath(definition.IconPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? string.Empty);
            File.WriteAllBytes(absolutePath, outlinedTexture.EncodeToPNG());
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
        finally
        {
            if (camera != null)
                camera.targetTexture = null;

            if (renderTexture != null)
            {
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }

            if (renderedTexture != null)
                UnityEngine.Object.DestroyImmediate(renderedTexture);
            if (outlinedTexture != null)
                UnityEngine.Object.DestroyImmediate(outlinedTexture);

            if (fillLightObject != null)
                UnityEngine.Object.DestroyImmediate(fillLightObject);
            if (keyLightObject != null)
                UnityEngine.Object.DestroyImmediate(keyLightObject);
            if (cameraObject != null)
                UnityEngine.Object.DestroyImmediate(cameraObject);
            if (instance != null)
                UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static void FrameBounds(Camera camera, IReadOnlyList<Renderer> renderers)
    {
        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        foreach (var renderer in renderers)
        {
            var bounds = renderer.bounds;
            foreach (var corner in GetCorners(bounds))
            {
                var cameraSpace = camera.transform.InverseTransformPoint(corner);
                min = Vector2.Min(min, new Vector2(cameraSpace.x, cameraSpace.y));
                max = Vector2.Max(max, new Vector2(cameraSpace.x, cameraSpace.y));
            }
        }

        var projectedCenter = (min + max) * 0.5f;
        camera.transform.position +=
            camera.transform.right * projectedCenter.x +
            camera.transform.up * projectedCenter.y;

        var projectedSize = max - min;
        camera.orthographicSize = Mathf.Max(projectedSize.y * 0.5f, projectedSize.x * 0.5f) * FramePadding;
    }

    private static IEnumerable<Vector3> GetCorners(Bounds bounds)
    {
        var center = bounds.center;
        var extents = bounds.extents;

        for (var x = -1; x <= 1; x += 2)
        for (var y = -1; y <= 1; y += 2)
        for (var z = -1; z <= 1; z += 2)
            yield return center + Vector3.Scale(extents, new Vector3(x, y, z));
    }

    private static Bounds CalculateBounds(IReadOnlyList<Renderer> renderers)
    {
        var bounds = renderers[0].bounds;
        for (var index = 1; index < renderers.Count; index++)
            bounds.Encapsulate(renderers[index].bounds);
        return bounds;
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
            SetLayerRecursively(child, layer);
    }

    private static GameObject CreateDirectionalLight(
        string name,
        Vector3 eulerAngles,
        float intensity,
        Color color)
    {
        var lightObject = new GameObject(name)
        {
            hideFlags = HideFlags.HideAndDontSave,
            layer = RenderLayer
        };
        lightObject.transform.rotation = Quaternion.Euler(eulerAngles);

        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
        light.shadows = LightShadows.None;
        light.cullingMask = 1 << RenderLayer;
        return lightObject;
    }

    private static Texture2D AddOutline(Texture2D source, int radius, Color32 outlineColor)
    {
        var width = source.width;
        var height = source.height;
        var sourcePixels = source.GetPixels32();
        var outputPixels = (Color32[])sourcePixels.Clone();

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                if (sourcePixels[index].a > 16)
                    continue;

                var hasOpaqueNeighbour = false;
                for (var offsetY = -radius; offsetY <= radius && !hasOpaqueNeighbour; offsetY++)
                {
                    var neighbourY = y + offsetY;
                    if (neighbourY < 0 || neighbourY >= height)
                        continue;

                    for (var offsetX = -radius; offsetX <= radius; offsetX++)
                    {
                        if (offsetX * offsetX + offsetY * offsetY > radius * radius)
                            continue;

                        var neighbourX = x + offsetX;
                        if (neighbourX < 0 || neighbourX >= width)
                            continue;

                        if (sourcePixels[neighbourY * width + neighbourX].a <= 48)
                            continue;

                        hasOpaqueNeighbour = true;
                        break;
                    }
                }

                if (hasOpaqueNeighbour)
                    outputPixels[index] = outlineColor;
            }
        }

        var output = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
        output.SetPixels32(outputPixels);
        output.Apply(false, false);
        return output;
    }

    private static void ConfigureSpriteImporter(string assetPath)
    {
        if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = OutputSize;
        importer.SaveAndReimport();
    }

    private static void AssignIconToFoodPrefab(FoodIconDefinition definition)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(definition.IconPath);
        AssignSpriteToFoodPrefab(definition.PrefabPath, sprite);
    }

    private static void AssignSpriteToFoodPrefab(string prefabPath, Sprite sprite)
    {
        if (sprite == null)
        {
            Debug.LogError($"Generated food sprite could not be loaded for {prefabPath}");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var food = root.GetComponent<Food>();
            if (food == null)
                return;

            var serializedFood = new SerializedObject(food);
            var iconProperty = serializedFood.FindProperty("_icon");
            if (iconProperty == null)
            {
                Debug.LogError($"Serialized icon field was not found in {prefabPath}");
                return;
            }

            iconProperty.objectReferenceValue = sprite;
            serializedFood.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
