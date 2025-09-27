// VoxelHumanoidBuilder.cs (v2.1)
// Place in Assets/Editor. Unity 2020+ (NET 4.x / .NET Standard 2.0).
// PURPOSE: Build a Humanoid-like bone hierarchy for segmented voxel characters
// and (optionally) parent mesh segments to the right bones — without Blender.
// NEW in v2.1:
//  • All hierarchy/parenting operations are deferred via EditorApplication.delayCall to avoid
//    IMGUI event-time assertions like "System is not interested in this transform".
//  • No heavy Transform.SetParent calls run directly inside OnGUI. Buttons enqueue safe actions.
//  • Minor refactor: reuse collected segment dict; fewer hierarchy passes.
//  • Mixamo naming kept from v2.0 (with optional "mixamorig:" prefix).

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine.XR;

public class VoxelHumanoidBuilder : EditorWindow
{
    public enum RigNaming { UnityHumanoid, Mixamo }

    [MenuItem("Tools/Voxel Humanoid Builder")]
    private static void ShowWindow()
    {
        GetWindow<VoxelHumanoidBuilder>(false, "Voxel Builder", true);
    }

    [Header("Input")] public GameObject root; // Parent containing segmented parts (from Voxel Importer)

    [Header("Options")] public bool createHumanoidRig = true;
    public bool keepMeshNames = true; // If false, normalize names to Humanoid-friendly set
    public bool addGizmos = true; // Draw gizmo spheres at joint positions
    public float gizmoSize = 0.02f;

    [Header("Rig Naming")] public RigNaming naming = RigNaming.Mixamo; // default to Mixamo per user request
    public bool mixamoPrefix = false; // add "mixamorig:" to Mixamo bone names

    [Header("Build Modes")] public bool useJointOverrides = true; // Use helper empties (JNT_*) when present
    public bool snapToContacts = true;    // Snap joints to touching faces between adjacent segments
    public bool onlyParenting = false;    // Build hierarchy & parent meshes, DO NOT move bones or meshes
    public bool deferMeshParenting = true; // Don't parent meshes during BUILD; attach later with button

    // Name normalization map (source token -> target logical body part)
    // Used only when keepMeshNames == false; purely cosmetic for segment names
    private static readonly (string token, string target)[] NameMap =
    {
        ("torso", "Chest"), ("spine", "Spine"), ("hips", "Hips"), ("pelvis", "Hips"),
        ("head", "Head"), ("neck", "Neck"),
        ("upperarm_l", "UpperArm_L"), ("lowerarm_l", "LowerArm_L"), ("hand_l", "Hand_L"),
        ("upperarm_r", "UpperArm_R"), ("lowerarm_r", "LowerArm_R"), ("hand_r", "Hand_R"),
        ("upperleg_l", "UpperLeg_L"), ("lowerleg_l", "LowerLeg_L"), ("foot_l", "Foot_L"), ("toes_l", "Toes_L"),
        ("upperleg_r", "UpperLeg_R"), ("lowerleg_r", "LowerLeg_R"), ("foot_r", "Foot_R"), ("toes_r", "Toes_R")
    };

    // Logical keys for bones (scheme-agnostic). We'll map these to actual transform names via B().
    private static readonly string[] LOGICAL_ORDER_UNITY =
    {
        "Root","Hips","Spine","Chest","Neck","Head",
        "UpperArm_L","LowerArm_L","Hand_L",
        "UpperArm_R","LowerArm_R","Hand_R",
        "UpperLeg_L","LowerLeg_L","Foot_L","Toes_L",
        "UpperLeg_R","LowerLeg_R","Foot_R","Toes_R"
    };

    // For Mixamo we include shoulders + extra spine segments + ToeBase names
    private static readonly string[] LOGICAL_ORDER_MIXAMO =
    {
        "Root","Hips","Spine","Spine1","Spine2","Neck","Head",
        "Shoulder_L","UpperArm_L","LowerArm_L","Hand_L",
        "Shoulder_R","UpperArm_R","LowerArm_R","Hand_R",
        "UpperLeg_L","LowerLeg_L","Foot_L","Toes_L",
        "UpperLeg_R","LowerLeg_R","Foot_R","Toes_R"
    };

    // Logical edges for Unity scheme
    private static readonly (string parent, string child)[] LOGICAL_EDGES_UNITY =
    {
        ("Root","Hips"), ("Hips","Spine"), ("Spine","Chest"), ("Chest","Neck"), ("Neck","Head"),
        ("Chest","UpperArm_L"), ("UpperArm_L","LowerArm_L"), ("LowerArm_L","Hand_L"),
        ("Chest","UpperArm_R"), ("UpperArm_R","LowerArm_R"), ("LowerArm_R","Hand_R"),
        ("Hips","UpperLeg_L"), ("UpperLeg_L","LowerLeg_L"), ("LowerLeg_L","Foot_L"), ("Foot_L","Toes_L"),
        ("Hips","UpperLeg_R"), ("UpperLeg_R","LowerLeg_R"), ("LowerLeg_R","Foot_R"), ("Foot_R","Toes_R")
    };

    // Logical edges for Mixamo scheme
    private static readonly (string parent, string child)[] LOGICAL_EDGES_MIXAMO =
    {
        ("Root","Hips"), ("Hips","Spine"), ("Spine","Spine1"), ("Spine1","Spine2"), ("Spine2","Neck"), ("Neck","Head"),
        ("Spine2","Shoulder_L"), ("Shoulder_L","UpperArm_L"), ("UpperArm_L","LowerArm_L"), ("LowerArm_L","Hand_L"),
        ("Spine2","Shoulder_R"), ("Shoulder_R","UpperArm_R"), ("UpperArm_R","LowerArm_R"), ("LowerArm_R","Hand_R"),
        ("Hips","UpperLeg_L"), ("UpperLeg_L","LowerLeg_L"), ("LowerLeg_L","Foot_L"), ("Foot_L","Toes_L"),
        ("Hips","UpperLeg_R"), ("UpperLeg_R","LowerLeg_R"), ("LowerLeg_R","Foot_R"), ("Foot_R","Toes_R")
    };

    // Mixamo mapping: logical -> actual Mixamo bone name
    private static readonly Dictionary<string, string> MIXAMO_MAP = new Dictionary<string, string>
    {
        {"Root","Root"},
        {"Hips","Hips"},
        {"Spine","Spine"},
        {"Spine1","Spine1"},
        {"Spine2","Spine2"},
        {"Chest","Spine2"}, // logical Chest maps to Spine2 in Mixamo
        {"Neck","Neck"}, {"Head","Head"},
        {"Shoulder_L","LeftShoulder"}, {"Shoulder_R","RightShoulder"},
        {"UpperArm_L","LeftArm"}, {"LowerArm_L","LeftForeArm"}, {"Hand_L","LeftHand"},
        {"UpperArm_R","RightArm"}, {"LowerArm_R","RightForeArm"}, {"Hand_R","RightHand"},
        {"UpperLeg_L","LeftUpLeg"}, {"LowerLeg_L","LeftLeg"}, {"Foot_L","LeftFoot"}, {"Toes_L","LeftToeBase"},
        {"UpperLeg_R","RightUpLeg"}, {"LowerLeg_R","RightLeg"}, {"Foot_R","RightFoot"}, {"Toes_R","RightToeBase"}
    };

    private Vector2 _scroll;

    private void OnGUI()
    {
        try
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Voxel Humanoid Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Workflow: 1) BUILD bones (choose naming). 2) Move joints (SNAP/JNT_* optional). 3) ATTACH MESHES NOW.", MessageType.Info);

            root = (GameObject)EditorGUILayout.ObjectField("Character Root", root, typeof(GameObject), true);
            createHumanoidRig = EditorGUILayout.Toggle("Create Humanoid Rig", createHumanoidRig);
            keepMeshNames = EditorGUILayout.Toggle("Keep Mesh Names", keepMeshNames);
            addGizmos = EditorGUILayout.Toggle("Add Joint Gizmos", addGizmos);
            gizmoSize = EditorGUILayout.Slider("Gizmo Size (m)", gizmoSize, 0.005f, 0.1f);

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Rig Naming", EditorStyles.boldLabel);
            naming = (RigNaming)EditorGUILayout.EnumPopup("Naming Scheme", naming);
            using (new EditorGUI.DisabledScope(naming != RigNaming.Mixamo))
            {
                mixamoPrefix = EditorGUILayout.Toggle("Use 'mixamorig:' prefix", mixamoPrefix);
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Build Modes", EditorStyles.boldLabel);
            onlyParenting = EditorGUILayout.Toggle("Only Parenting (no reposition)", onlyParenting);
            deferMeshParenting = EditorGUILayout.Toggle("Defer Mesh Parenting (attach later)", deferMeshParenting);
            using (new EditorGUI.DisabledScope(onlyParenting))
            {
                useJointOverrides = EditorGUILayout.Toggle("Use JNT_* Overrides", useJointOverrides);
                snapToContacts = EditorGUILayout.Toggle("Snap To Segment Contacts", snapToContacts);
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(root == null))
            {
                if (GUILayout.Button("BUILD / REBUILD BONES")) Queue(DoBuild);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("ATTACH MESHES NOW")) Queue(DoAttachMeshesNow);
                if (GUILayout.Button("DETACH MESHES TO GEO GROUP")) Queue(DoDetachMeshesNow);
                EditorGUILayout.EndHorizontal();

                if (!onlyParenting && GUILayout.Button("SNAP JOINTS NOW")) Queue(DoSnapAllContacts);
                if (GUILayout.Button("VALIDATE NAMING")) Queue(DoValidateNaming);
            }

            if (root == null)
                EditorGUILayout.HelpBox("Assign a Character Root to enable actions.", MessageType.Warning);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }
    }

    // === Safe deferral ===
    private void Queue(Action act)
    {
        // Defer to after IMGUI event to avoid editor assertions
        EditorApplication.delayCall += () =>
        {
            if (this == null) return; // window closed
            try { act?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        };
    }

    // === Actions (deferred) ===
    private void DoBuild()
    {
        if (!root) return;
        Undo.IncrementCurrentGroup();
        int grp = Undo.GetCurrentGroup();

        var segments = CollectSegments();
        if (!keepMeshNames)
        {
            foreach (var go in segments)
            {
                string norm = NormalizeName(go.name);
                if (!string.IsNullOrEmpty(norm)) go.name = norm + "_Geo";
            }
        }

        var order = GetBoneOrder();
        var edges = GetEdges();

        // Create/find bone objects
        var bones = new Dictionary<string, Transform>();
        foreach (var boneName in order)
        {
            var t = FindChild(root.transform, boneName);
            if (t == null)
            {
                var go = new GameObject(boneName);
                Undo.RegisterCreatedObjectUndo(go, "Create Bone");
                t = go.transform;
                t.SetParent(root.transform, false);
            }
            bones[boneName] = t;
        }

        // Parent bones according to edges (preserve world)
        foreach (var (parent, child) in edges)
        {
            var p = FindChild(root.transform, parent);
            var c = FindChild(root.transform, child);
            if (p != null && c != null && c.parent != p)
            {
                // keep world pos manually
                var wp = c.position; var wr = c.rotation; var ws = c.lossyScale;
                c.SetParent(p, true);
                c.position = wp; c.rotation = wr; // keep scale implicitly by true
            }
        }

        if (!onlyParenting)
        {
            // Heuristics: compute centers once
            var segDict = segments.ToDictionary(s => s.name, s => s);

            Vector3 hipsPos = CenterOfAny(segDict, new[] { "Hips", "Pelvis", "hips", "pelvis", "Torso", "Spine" }, root.transform.position);
            Bone(B("Hips")).position = hipsPos;

            Vector3 chestPos = CenterOfAny(segDict, new[] { "Chest", "Torso", "Spine", "chest", "torso", "spine" }, hipsPos + Vector3.up * 0.3f);
            Bone(B("Chest")).position = chestPos;

            var spineT = Bone(B("Spine"));
            if (spineT) spineT.position = Vector3.Lerp(hipsPos, chestPos, 0.5f);

            if (naming == RigNaming.Mixamo)
            {
                var s1 = Bone(B("Spine1"));
                var s2 = Bone(B("Spine2"));
                if (s1) s1.position = Vector3.Lerp(spineT ? spineT.position : hipsPos, chestPos, 0.5f);
                if (s2) s2.position = chestPos;
            }

            Bone(B("Neck")).position = GetOverridePos("Neck", chestPos + Vector3.up * 0.15f);
            Bone(B("Head")).position = GetOverridePos("Head", Bone(B("Neck")).position + Vector3.up * 0.15f);

            PlaceLimbCoarse(B("UpperArm_L"), B("LowerArm_L"), B("Hand_L"), segDict, chestPos, Vector3.left);
            PlaceLimbCoarse(B("UpperArm_R"), B("LowerArm_R"), B("Hand_R"), segDict, chestPos, Vector3.right);

            PlaceLimbCoarseLeg(B("UpperLeg_L"), B("LowerLeg_L"), B("Foot_L"), B("Toes_L"), segDict, hipsPos, Vector3.left);
            PlaceLimbCoarseLeg(B("UpperLeg_R"), B("LowerLeg_R"), B("Foot_R"), B("Toes_R"), segDict, hipsPos, Vector3.right);

            if (snapToContacts) DoSnapAllContacts();
        }

        if (!deferMeshParenting)
        {
            DoAttachMeshesNow();
        }

        if (addGizmos)
        {
            foreach (var name in order)
            {
                var t = FindChild(root.transform, name);
                if (!t) continue;
                var g = t.GetComponent<VoxelJointGizmo>();
                if (!g) g = t.gameObject.AddComponent<VoxelJointGizmo>();
                g.size = gizmoSize;
            }
        }

        Undo.CollapseUndoOperations(grp);
        EditorSceneManager.MarkAllScenesDirty();
        EditorGUIUtility.PingObject(root);
        }

    private void DoAttachMeshesNow()
    {
        if (!root) return;
        var segments = CollectSegments();
        var bonesDict = GetBoneOrder().ToDictionary(b => b, b => FindChild(root.transform, b));
        AssignSegmentsToBones(segments, bonesDict);
        Debug.Log("VoxelHumanoidBuilder: Meshes attached to bones.");
    }

    private void DoDetachMeshesNow()
    {
        if (!root) return;
        var segments = CollectSegments();
        var geoGroup = FindChild(root.transform, "GRP_Geo");
        if (!geoGroup)
        {
            var go = new GameObject("GRP_Geo");
            Undo.RegisterCreatedObjectUndo(go, "Create Geo Group");
            geoGroup = go.transform;
            geoGroup.SetParent(root.transform, false);
        }

        var boneSet = new HashSet<string>(GetBoneOrder());
        foreach (var seg in segments)
        {
            Transform p = seg.transform.parent;
            if (p != null && boneSet.Contains(p.name))
            {
                seg.transform.SetParent(geoGroup, true);
            }
        }
        Debug.Log("VoxelHumanoidBuilder: Meshes detached to GRP_Geo.");
    }

    private void DoSnapAllContacts()
    {
        if (!root) return;
        // Chest/Neck/Head (Chest maps to Spine2 in Mixamo)
        SnapJointBySegments(B("Chest"), B("Neck"));
        SnapJointBySegments(B("Neck"), B("Head"));

        // Shoulders if Mixamo
        if (naming == RigNaming.Mixamo)
        {
            if (Bone(B("Shoulder_L"))) SnapJointBySegments(B("Chest"), B("Shoulder_L"), overrideKey: "Shoulder_L");
            if (Bone(B("Shoulder_R"))) SnapJointBySegments(B("Chest"), B("Shoulder_R"), overrideKey: "Shoulder_R");
            if (Bone(B("Shoulder_L"))) SnapJointBySegments(B("Shoulder_L"), B("UpperArm_L"));
            if (Bone(B("Shoulder_R"))) SnapJointBySegments(B("Shoulder_R"), B("UpperArm_R"));
        }
        else
        {
            SnapJointBySegments(B("Chest"), B("UpperArm_L"), overrideKey: "Shoulder_L");
            SnapJointBySegments(B("Chest"), B("UpperArm_R"), overrideKey: "Shoulder_R");
        }

        SnapJointBySegments(B("UpperArm_L"), B("LowerArm_L"), overrideKey: "Elbow_L");
        SnapJointBySegments(B("LowerArm_L"), B("Hand_L"), overrideKey: "Wrist_L");

        SnapJointBySegments(B("UpperArm_R"), B("LowerArm_R"), overrideKey: "Elbow_R");
        SnapJointBySegments(B("LowerArm_R"), B("Hand_R"), overrideKey: "Wrist_R");

        SnapJointBySegments(B("Hips"), B("UpperLeg_L"), overrideKey: "Hip_L");
        SnapJointBySegments(B("UpperLeg_L"), B("LowerLeg_L"), overrideKey: "Knee_L");
        SnapJointBySegments(B("LowerLeg_L"), B("Foot_L"), overrideKey: "Ankle_L");
        SnapJointBySegments(B("Foot_L"), B("Toes_L"), overrideKey: "Toes_L", forwardFallback: true);

        SnapJointBySegments(B("Hips"), B("UpperLeg_R"), overrideKey: "Hip_R");
        SnapJointBySegments(B("UpperLeg_R"), B("LowerLeg_R"), overrideKey: "Knee_R");
        SnapJointBySegments(B("LowerLeg_R"), B("Foot_R"), overrideKey: "Ankle_R");
        SnapJointBySegments(B("Foot_R"), B("Toes_R"), overrideKey: "Toes_R", forwardFallback: true);

        var hips = Bone(B("Hips"));
        var chest = Bone(B("Chest"));
        if (hips && chest)
        {
            var rootT = Bone(B("Root"));
            if (rootT) rootT.position = hips.position;
            var spine = Bone(B("Spine"));
            if (spine) spine.position = Vector3.Lerp(hips.position, chest.position, 0.5f);
            if (naming == RigNaming.Mixamo)
            {
                var s1 = Bone(B("Spine1"));
                var s2 = Bone(B("Spine2"));
                if (s1) s1.position = Vector3.Lerp(spine ? spine.position : hips.position, chest.position, 0.5f);
                if (s2 && chest) s2.position = chest.position;
            }
        }

        SceneView.RepaintAll();
        Debug.Log("VoxelHumanoidBuilder: Joints snapped.");
    }

    private void DoValidateNaming()
    {
        if (!root) return;
        var issues = new List<string>();
        string[] requiredUnity = { "Hips", "Spine", "Chest", "Neck", "Head", "UpperArm_L", "LowerArm_L", "Hand_L", "UpperArm_R", "LowerArm_R", "Hand_R", "UpperLeg_L", "LowerLeg_L", "Foot_L", "UpperLeg_R", "LowerLeg_R", "Foot_R" };
        string[] requiredMixamo = { "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head", "Shoulder_L", "UpperArm_L", "LowerArm_L", "Hand_L", "Shoulder_R", "UpperArm_R", "LowerArm_R", "Hand_R", "UpperLeg_L", "LowerLeg_L", "Foot_L", "UpperLeg_R", "LowerLeg_R", "Foot_R" };

        foreach (var r in (naming == RigNaming.Mixamo ? requiredMixamo : requiredUnity))
        {
            if (FindChild(root.transform, B(r)) == null) issues.Add($"Missing bone: {B(r)}");
        }

        if (issues.Count == 0)
            Debug.Log("VoxelHumanoidBuilder: Naming looks good for " + naming + ".");
        else
            Debug.LogWarning("VoxelHumanoidBuilder: Issues\n" + string.Join("\n", issues));
    }

    // ===== Low-level helpers =====
    private Transform Bone(string actualName) => FindChild(root ? root.transform : null, actualName);

    private string B(string logical)
    {
        if (naming == RigNaming.Mixamo)
        {
            string baseName = MIXAMO_MAP.TryGetValue(logical, out var m) ? m : logical;
            if (mixamoPrefix && baseName != "Root") baseName = "mixamorig:" + baseName;
            return baseName;
        }
        return logical;
    }

    private List<GameObject> CollectSegments()
    {
        if (!root) return new List<GameObject>();
        return root.GetComponentsInChildren<MeshRenderer>(true)
            .Select(r => r.gameObject)
            .Where(go => go.transform != null && go != root)
            .ToList();
    }

    private static string NormalizeName(string name)
    {
        string n = name.Replace(' ', '_').ToLowerInvariant();
        foreach (var (token, target) in NameMap)
        {
            if (n.Contains(token)) return target;
        }
        return null;
    }

    private static Transform FindChild(Transform root, string name)
    {
        if (!root) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.Equals(name, StringComparison.Ordinal)) return t;
        }
        return null;
    }

    private Vector3 GetOverridePos(string jointKey, Vector3 fallback)
    {
        if (!useJointOverrides || !root) return fallback;
        var t = FindOverride(jointKey);
        return t ? t.position : fallback;
    }

    private Transform FindOverride(string jointKey)
    {
        if (!root) return null;
        string[] candidates = { jointKey, "JNT_" + jointKey };
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            foreach (var c in candidates)
            {
                if (t.name.Equals(c, StringComparison.OrdinalIgnoreCase)) return t;
            }
        }
        return null;
    }

    private static Vector3 CenterOfAny(Dictionary<string, GameObject> dict, string[] keys, Vector3 fallback)
    {
        foreach (var k in keys)
        {
            var kv = dict.FirstOrDefault(p => p.Key.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);
            if (kv.Value != null)
            {
                var r = kv.Value.GetComponent<Renderer>();
                if (r) return r.bounds.center;
            }
        }
        return fallback;
    }

    private void PlaceLimbCoarse(string upper, string lower, string end, Dictionary<string, GameObject> segs, Vector3 chestPos, Vector3 sideDir)
    {
        Vector3 upperC = CenterOfAny(segs, new[] { upper, "UpperArm_L", "UpperArm_R", "LeftArm", "RightArm" }, chestPos + sideDir * 0.15f);
        Vector3 lowerC = CenterOfAny(segs, new[] { lower, "LowerArm_L", "LowerArm_R", "LeftForeArm", "RightForeArm" }, upperC + Vector3.down * 0.1f);
        Vector3 endC = CenterOfAny(segs, new[] { end, "Hand_L", "Hand_R", "LeftHand", "RightHand" }, lowerC + Vector3.down * 0.05f);

        var upperT = Bone(upper); var lowerT = Bone(lower); var endT = Bone(end);
        if (upperT) upperT.position = GetOverridePos((upper.Contains("Left") || upper.EndsWith("_L")) ? "Shoulder_L" : "Shoulder_R", upperC);
        if (lowerT) lowerT.position = GetOverridePos((lower.Contains("Left") || lower.EndsWith("_L")) ? "Elbow_L" : "Elbow_R", Vector3.Lerp(upperC, lowerC, 0.6f));
        if (endT) endT.position = GetOverridePos((end.Contains("Left") || end.EndsWith("_L")) ? "Wrist_L" : "Wrist_R", Vector3.Lerp(lowerC, endC, 0.7f));
    }

    private void PlaceLimbCoarseLeg(string upper, string lower, string foot, string toes, Dictionary<string, GameObject> segs, Vector3 hipsPos, Vector3 sideDir)
    {
        Vector3 upperC = CenterOfAny(segs, new[] { upper, "UpperLeg_L", "UpperLeg_R", "LeftUpLeg", "RightUpLeg" }, hipsPos + Vector3.down * 0.1f + sideDir * 0.08f);
        Vector3 lowerC = CenterOfAny(segs, new[] { lower, "LowerLeg_L", "LowerLeg_R", "LeftLeg", "RightLeg" }, upperC + Vector3.down * 0.15f);
        Vector3 footC = CenterOfAny(segs, new[] { foot, "Foot_L", "Foot_R", "LeftFoot", "RightFoot" }, lowerC + Vector3.down * 0.1f);
        Vector3 toesC = CenterOfAny(segs, new[] { toes, "Toes_L", "Toes_R", "LeftToeBase", "RightToeBase" }, footC + root.transform.forward * 0.05f);

        var upperT = Bone(upper); var lowerT = Bone(lower); var footT = Bone(foot); var toesT = Bone(toes);
        if (upperT) upperT.position = GetOverridePos((upper.Contains("Left") || upper.EndsWith("_L")) ? "Hip_L" : "Hip_R", upperC);
        if (lowerT) lowerT.position = GetOverridePos((lower.Contains("Left") || lower.EndsWith("_L")) ? "Knee_L" : "Knee_R", Vector3.Lerp(upperC, lowerC, 0.6f));
        if (footT) footT.position = GetOverridePos((foot.Contains("Left") || foot.EndsWith("_L")) ? "Ankle_L" : "Ankle_R", Vector3.Lerp(lowerC, footC, 0.7f));
        if (toesT) toesT.position = GetOverridePos((toes.Contains("Left") || toes.EndsWith("_L")) ? "Toes_L" : "Toes_R", toesC);
    }

    private void AssignSegmentsToBones(List<GameObject> segments, Dictionary<string, Transform> bones)
    {
        foreach (var seg in segments)
        {
            var n = seg.name.ToLowerInvariant();
            string targetKey = null;
            if (n.Contains("head")) targetKey = B("Head");
            else if (n.Contains("neck")) targetKey = B("Neck");
            else if (n.Contains("chest") || n.Contains("torso")) targetKey = B("Chest");
            else if (n.Contains("spine")) targetKey = naming == RigNaming.Mixamo ? B("Spine2") : B("Spine");
            else if (n.Contains("hips") || n.Contains("pelvis")) targetKey = B("Hips");
            else if (n.Contains("upperarm_l") || n.Contains("leftarm")) targetKey = B("UpperArm_L");
            else if (n.Contains("lowerarm_l") || n.Contains("leftforearm")) targetKey = B("LowerArm_L");
            else if (n.Contains("hand_l") || n.Contains("lefthand")) targetKey = B("Hand_L");
            else if (n.Contains("upperarm_r") || n.Contains("rightarm")) targetKey = B("UpperArm_R");
            else if (n.Contains("lowerarm_r") || n.Contains("rightforearm")) targetKey = B("LowerArm_R");
            else if (n.Contains("hand_r") || n.Contains("righthand")) targetKey = B("Hand_R");
            else if (n.Contains("upperleg_l") || n.Contains("leftupleg")) targetKey = B("UpperLeg_L");
            else if (n.Contains("lowerleg_l") || n.Contains("leftleg")) targetKey = B("LowerLeg_L");
            else if (n.Contains("foot_l") || n.Contains("leftfoot")) targetKey = B("Foot_L");
            else if (n.Contains("toes_l") || n.Contains("lefttoe")) targetKey = B("Toes_L");
            else if (n.Contains("upperleg_r") || n.Contains("rightupleg")) targetKey = B("UpperLeg_R");
            else if (n.Contains("lowerleg_r") || n.Contains("rightleg")) targetKey = B("LowerLeg_R");
            else if (n.Contains("foot_r") || n.Contains("rightfoot")) targetKey = B("Foot_R");
            else if (n.Contains("toes_r") || n.Contains("righttoe")) targetKey = B("Toes_R");

            if (!string.IsNullOrEmpty(targetKey) && bones.TryGetValue(targetKey, out var t) && seg.transform.parent != t)
            {
                seg.transform.SetParent(t, true); // preserve world transform
            }
        }
    }

    // ===== Contact snapping =====
    private void SnapJointBySegments(string parentBoneActual, string childBoneActual, string overrideKey = null, bool forwardFallback = false)
    {
        var p = Bone(parentBoneActual);
        var c = Bone(childBoneActual);
        if (!p || !c) return;

        if (useJointOverrides && !string.IsNullOrEmpty(overrideKey))
        {
            var o = FindOverride(overrideKey);
            if (o)
            {
                c.position = o.position;
                return;
            }
        }

        var pr = FirstRendererUnder(p);
        var cr = FirstRendererUnder(c);
        if (pr == null && cr == null) return;

        Vector3 pC = pr ? pr.bounds.center : p.position;
        Vector3 cC = cr ? cr.bounds.center : c.position;
        Vector3 dir = (cC - pC);
        if (dir.sqrMagnitude < 1e-6f)
        {
            dir = (c.position - p.position);
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.up;
        }

        Vector3 newPos;
        if (pr != null && cr != null)
        {
            Vector3 pFace = SupportPoint(pr.bounds, dir);
            Vector3 cFace = SupportPoint(cr.bounds, -dir);
            newPos = (pFace + cFace) * 0.5f;
        }
        else if (pr != null)
        {
            Vector3 pFace = SupportPoint(pr.bounds, dir);
            newPos = pFace;
        }
        else
        {
            Vector3 cFace = SupportPoint(cr.bounds, -dir);
            newPos = cFace;
        }

        if (forwardFallback && pr != null && cr != null)
            newPos += root.transform.forward * 0.02f;

        c.position = newPos;
    }

    private static MeshRenderer FirstRendererUnder(Transform t)
    {
        if (!t) return null;
        foreach (Transform ch in t)
        {
            var r = ch.GetComponent<MeshRenderer>();
            if (r) return r;
        }
        return t.GetComponentInChildren<MeshRenderer>();
    }

    private static Vector3 SupportPoint(Bounds b, Vector3 dir)
    {
        Vector3 s = new Vector3(Mathf.Sign(dir.x), Mathf.Sign(dir.y), Mathf.Sign(dir.z));
        return b.center + Vector3.Scale(s, b.extents);
    }

    [ExecuteAlways]
    public class VoxelJointGizmo : MonoBehaviour
    {
        public float size = 0.02f;
        private void OnDrawGizmos()
        {
            Gizmos.DrawWireSphere(transform.position, size);
        }
    }

    // Helpers to compute scheme-specific names/edges
    private List<string> GetBoneOrder()
    {
        var logical = naming == RigNaming.Mixamo ? LOGICAL_ORDER_MIXAMO : LOGICAL_ORDER_UNITY;
        var list = new List<string>(logical.Length);
        foreach (var k in logical)
        {
            var actual = B(k);
            if (!list.Contains(actual)) list.Add(actual);
        }
        return list;
    }

    private List<(string parent, string child)> GetEdges()
    {
        var logical = naming == RigNaming.Mixamo ? LOGICAL_EDGES_MIXAMO : LOGICAL_EDGES_UNITY;
        var res = new List<(string, string)>(logical.Length);
        foreach (var (p, c) in logical)
        {
            res.Add((B(p), B(c)));
        }
        return res;
    }
}
