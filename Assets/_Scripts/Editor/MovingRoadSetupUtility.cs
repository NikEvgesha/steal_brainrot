using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MovingRoadSetupUtility
{
    private const string PrefabPath = "Assets/_Prefabs/strelka doroga.prefab";
    private const string RootName = "strelka doroga";
    private const string RightLaneName = "Strelka right";
    private const string LeftLaneName = "Strelka Left";
    private const string TriggerName = "MoveTrigger";
    private const float DefaultMoveSpeed = 5f;
    private const float DefaultScrollSpeed = 0.35f;

    [MenuItem("Tools/Moving Road/Configure Selected Strelka Doroga")]
    private static void ConfigureSelected()
    {
        HashSet<Transform> roots = new HashSet<Transform>();
        for (int i = 0; i < Selection.gameObjects.Length; i++)
            CollectRoadRoots(Selection.gameObjects[i].transform, roots);

        int configured = ConfigureRoots(roots, true);
        Debug.Log("Configured moving roads in selection: " + configured);
    }

    [MenuItem("Tools/Moving Road/Configure Open Scenes Strelka Doroga")]
    private static void ConfigureOpenScenes()
    {
        HashSet<Transform> roots = new HashSet<Transform>();
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform target = transforms[i];
            if (target == null || !IsEditableSceneObject(target.gameObject) || !IsRoadRoot(target))
                continue;

            roots.Add(target);
        }

        int configured = ConfigureRoots(roots, true);
        Debug.Log("Configured moving roads in open scenes: " + configured);
    }

    [MenuItem("Tools/Moving Road/Configure Strelka Doroga Prefab Asset")]
    private static void ConfigurePrefabAsset()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            if (ConfigureRoadRoot(root.transform, false))
            {
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Configured moving road prefab: " + PrefabPath);
            }
            else
            {
                Debug.LogWarning("Prefab does not contain expected strelka doroga lanes: " + PrefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static int ConfigureRoots(HashSet<Transform> roots, bool recordUndo)
    {
        int configured = 0;
        foreach (Transform root in roots)
        {
            if (root == null || !ConfigureRoadRoot(root, recordUndo))
                continue;

            configured++;
            if (root.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }

        return configured;
    }

    private static bool ConfigureRoadRoot(Transform root, bool recordUndo)
    {
        Transform rightLane = FindChild(root, RightLaneName);
        Transform leftLane = FindChild(root, LeftLaneName);
        if (rightLane == null || leftLane == null)
            return false;

        ConfigureLane(rightLane, new Vector3(-57.00002f, 40f, 67f), Vector3.right, Vector2.right, recordUndo);
        ConfigureLane(leftLane, new Vector3(-57.00002f, 40f, 117f), Vector3.left, Vector2.left, recordUndo);
        return true;
    }

    private static void ConfigureLane(Transform lane, Vector3 triggerLocalPosition, Vector3 localDirection, Vector2 uvDirection, bool recordUndo)
    {
        Transform trigger = lane.Find(TriggerName);
        if (trigger == null)
        {
            GameObject triggerObject = new GameObject(TriggerName);
            if (recordUndo)
                Undo.RegisterCreatedObjectUndo(triggerObject, "Create moving road trigger");

            trigger = triggerObject.transform;
            trigger.SetParent(lane, false);
        }
        else if (recordUndo)
        {
            Undo.RecordObject(trigger, "Configure moving road trigger");
        }

        trigger.localPosition = triggerLocalPosition;
        trigger.localRotation = Quaternion.identity;
        trigger.localScale = Vector3.one;

        BoxCollider collider = GetOrAddComponent<BoxCollider>(trigger.gameObject, recordUndo);
        if (recordUndo)
            Undo.RecordObject(collider, "Configure moving road trigger collider");

        collider.isTrigger = true;
        collider.center = Vector3.zero;
        collider.size = new Vector3(208f, 48f, 34f);
        EditorUtility.SetDirty(collider);

        MovingRoad movingRoad = GetOrAddComponent<MovingRoad>(trigger.gameObject, recordUndo);
        if (recordUndo)
            Undo.RecordObject(movingRoad, "Configure moving road");

        movingRoad.Configure(lane, localDirection, DefaultMoveSpeed, true, true);
        EditorUtility.SetDirty(movingRoad);

        MovingRoadVisualScroller scroller = GetOrAddComponent<MovingRoadVisualScroller>(lane.gameObject, recordUndo);
        if (recordUndo)
            Undo.RecordObject(scroller, "Configure moving road visual scroller");

        scroller.Configure(GetLaneRenderers(lane, trigger), uvDirection, DefaultScrollSpeed);
        EditorUtility.SetDirty(scroller);
    }

    private static Renderer[] GetLaneRenderers(Transform lane, Transform trigger)
    {
        List<Renderer> renderers = new List<Renderer>();
        Renderer[] childRenderers = lane.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < childRenderers.Length; i++)
        {
            Renderer target = childRenderers[i];
            if (target == null || target.transform == trigger || target.transform.IsChildOf(trigger))
                continue;

            renderers.Add(target);
        }

        return renderers.ToArray();
    }

    private static T GetOrAddComponent<T>(GameObject target, bool recordUndo) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component != null)
            return component;

        return recordUndo ? Undo.AddComponent<T>(target) : target.AddComponent<T>();
    }

    private static void CollectRoadRoots(Transform selected, HashSet<Transform> roots)
    {
        Transform parentRoot = FindRoadRootInParents(selected);
        if (parentRoot != null)
            roots.Add(parentRoot);

        Transform[] children = selected.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (IsRoadRoot(children[i]))
                roots.Add(children[i]);
        }
    }

    private static Transform FindRoadRootInParents(Transform target)
    {
        while (target != null)
        {
            if (IsRoadRoot(target))
                return target;

            target = target.parent;
        }

        return null;
    }

    private static Transform FindChild(Transform root, string name)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == name)
                return children[i];
        }

        return null;
    }

    private static bool IsRoadRoot(Transform target)
    {
        return target.name.StartsWith(RootName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEditableSceneObject(GameObject target)
    {
        return target.scene.IsValid() && target.scene.isLoaded && !EditorUtility.IsPersistent(target);
    }
}
