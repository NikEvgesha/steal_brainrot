#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class CreateCharacterHierarchy
{
    [MenuItem("GameObject/Characters/Create Character Hierarchy", false, 10)]
    public static void Create()
    {
        // Куда вставлять иерархию: под выделенный объект или в корень сцены
        Transform parent = Selection.activeTransform;

        // Создаём CharacterRoot
        GameObject characterRootGO = new GameObject("CharacterRoot");
        Undo.RegisterCreatedObjectUndo(characterRootGO, "Create Character Hierarchy");
        Transform characterRoot = characterRootGO.transform;

        if (parent != null)
        {
            characterRoot.SetParent(parent, worldPositionStays: false);
            characterRoot.position = parent.position; // стартовая позиция
        }

        // Root
        Transform root = CreateUnder(characterRoot, "Root");

        // Hips (таз) — базовая точка. Её позицию используем как целевой пивот CharacterRoot.
        Transform hips = CreateUnder(root, "Hips");

        // Spine → Chest
        Transform spine = CreateUnder(hips, "Spine");
        Transform chest = CreateUnder(spine, "Chest");

        // Шея/голова
        Transform neck = CreateUnder(chest, "Neck");
        Transform head = CreateUnder(neck, "Head");

        // Руки
        Transform upperArmL = CreateUnder(chest, "UpperArm_L");
        Transform lowerArmL = CreateUnder(upperArmL, "LowerArm_L");
        Transform handL = CreateUnder(lowerArmL, "Hand_L");

        Transform upperArmR = CreateUnder(chest, "UpperArm_R");
        Transform lowerArmR = CreateUnder(upperArmR, "LowerArm_R");
        Transform handR = CreateUnder(lowerArmR, "Hand_R");

        // Ноги
        Transform upperLegL = CreateUnder(hips, "UpperLeg_L");
        Transform lowerLegL = CreateUnder(upperLegL, "LowerLeg_L");
        Transform footL = CreateUnder(lowerLegL, "Foot_L");
        Transform toesL = CreateUnder(footL, "Toes_L");

        Transform upperLegR = CreateUnder(hips, "UpperLeg_R");
        Transform lowerLegR = CreateUnder(upperLegR, "LowerLeg_R");
        Transform footR = CreateUnder(lowerLegR, "Foot_R");
        Transform toesR = CreateUnder(footR, "Toes_R");

        // Ставим пивот CharacterRoot в центр таза: позицию Hips переносим на CharacterRoot
        // (в Unity "пивот" объекта = его transform.position)
        characterRoot.position = hips.position;

        // Выделим результат
        Selection.activeObject = characterRootGO;
        EditorGUIUtility.PingObject(characterRootGO);
    }

    private static Transform CreateUnder(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create Character Node");
        Transform t = go.transform;
        t.SetParent(parent, worldPositionStays: false);
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;
        return t;
    }

    // Включаем доступность пункта меню только когда в редакторе есть сцена
    [MenuItem("GameObject/Characters/Create Character Hierarchy", true)]
    public static bool ValidateCreate()
    {
        return true; // всегда доступно; создастся или под выделенным, или в корне
    }
}
#endif
