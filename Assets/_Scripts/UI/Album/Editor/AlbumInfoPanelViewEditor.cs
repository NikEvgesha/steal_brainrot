#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AlbumInfoPanelView))]
public sealed class AlbumInfoPanelViewEditor : Editor
{
    private SerializedProperty _editorPreviewMode;
    private SerializedProperty _eggModeSlots;
    private SerializedProperty _animalModeSlots;

    private SerializedProperty _infoIcon;
    private SerializedProperty _infoIconFxImage;
    private SerializedProperty _createInfoIconFxIfMissing;
    private SerializedProperty _infoIconFxScale;
    private SerializedProperty _lockedInfoIconFxColor;
    private SerializedProperty _titleText;
    private SerializedProperty _lockedOverlay;
    private SerializedProperty _lockedText;

    private SerializedProperty _eggDateText;
    private SerializedProperty _eggPriceText;
    private SerializedProperty _eggSourcesText;
    private SerializedProperty _eggOnlyObjects;

    private SerializedProperty _animalDescriptionText;
    private SerializedProperty _animalIncomeText;
    private SerializedProperty _animalSourcesText;
    private SerializedProperty _animalOnlyObjects;

    private SerializedProperty _eggHatchSection;
    private SerializedProperty _eggHatchIconsRoot;
    private SerializedProperty _eggHatchIconPrefab;
    private SerializedProperty _eggHatchIconTemplate;
    private SerializedProperty _eggHatchMaxIcons;
    private SerializedProperty _eggHatchIconSpacing;

    private void OnEnable()
    {
        _editorPreviewMode = serializedObject.FindProperty("editorPreviewMode");
        _eggModeSlots = serializedObject.FindProperty("eggModeSlots");
        _animalModeSlots = serializedObject.FindProperty("animalModeSlots");

        _infoIcon = serializedObject.FindProperty("infoIcon");
        _infoIconFxImage = serializedObject.FindProperty("infoIconFxImage");
        _createInfoIconFxIfMissing = serializedObject.FindProperty("createInfoIconFxIfMissing");
        _infoIconFxScale = serializedObject.FindProperty("infoIconFxScale");
        _lockedInfoIconFxColor = serializedObject.FindProperty("lockedInfoIconFxColor");
        _titleText = serializedObject.FindProperty("titleText");
        _lockedOverlay = serializedObject.FindProperty("lockedOverlay");
        _lockedText = serializedObject.FindProperty("lockedText");

        _eggDateText = serializedObject.FindProperty("eggDateText");
        _eggPriceText = serializedObject.FindProperty("eggPriceText");
        _eggSourcesText = serializedObject.FindProperty("eggSourcesText");
        _eggOnlyObjects = serializedObject.FindProperty("eggOnlyObjects");

        _animalDescriptionText = serializedObject.FindProperty("animalDescriptionText");
        _animalIncomeText = serializedObject.FindProperty("animalIncomeText");
        _animalSourcesText = serializedObject.FindProperty("animalSourcesText");
        _animalOnlyObjects = serializedObject.FindProperty("animalOnlyObjects");

        _eggHatchSection = serializedObject.FindProperty("eggHatchSection");
        _eggHatchIconsRoot = serializedObject.FindProperty("eggHatchIconsRoot");
        _eggHatchIconPrefab = serializedObject.FindProperty("eggHatchIconPrefab");
        _eggHatchIconTemplate = serializedObject.FindProperty("eggHatchIconTemplate");
        _eggHatchMaxIcons = serializedObject.FindProperty("eggHatchMaxIcons");
        _eggHatchIconSpacing = serializedObject.FindProperty("eggHatchIconSpacing");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.PropertyField(_editorPreviewMode, new GUIContent("Preview Without Play"));
        EditorGUILayout.Space(8f);

        var previewMode = _editorPreviewMode.enumValueIndex;
        if (previewMode == 0 || previewMode == 1)
            DrawModeSlots("Egg Mode", _eggModeSlots, true);
        if (previewMode == 0 || previewMode == 2)
            DrawModeSlots("Animal Mode", _animalModeSlots, false);

        EditorGUILayout.Space(8f);
        DrawAvailableSlots();

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            ApplyPreviewToTargets();
            return;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawModeSlots(string title, SerializedProperty slots, bool eggMode)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        using (new EditorGUI.IndentLevelScope())
        {
            DrawBool(slots, "showIcon", "Icon");
            DrawBool(slots, "showTitle", "Title");
            DrawBool(slots, "showDescription", eggMode ? "Description / Date" : "Description");
            DrawBool(slots, "showIncome", eggMode ? "Income / Price" : "Income");
            DrawBool(slots, "showSources", "Sources");
            DrawBool(slots, "showModeObjects", eggMode ? "Egg Only Objects" : "Animal Only Objects");
            if (eggMode)
                DrawBool(slots, "showHatchPreview", "Hatch Preview");
        }
        EditorGUILayout.Space(5f);
    }

    private static void DrawBool(SerializedProperty root, string propertyName, string label)
    {
        var prop = root.FindPropertyRelative(propertyName);
        if (prop != null)
            EditorGUILayout.PropertyField(prop, new GUIContent(label));
    }

    private void DrawAvailableSlots()
    {
        EditorGUILayout.LabelField("Available UI Slots", EditorStyles.boldLabel);
        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.LabelField("Common", EditorStyles.miniBoldLabel);
            DrawSlotRef(_infoIcon, "Icon");
            DrawSlotRef(_infoIconFxImage, "Icon FX");
            EditorGUILayout.PropertyField(_createInfoIconFxIfMissing, new GUIContent("Create Icon FX If Missing"));
            EditorGUILayout.PropertyField(_infoIconFxScale, new GUIContent("Icon FX Scale"));
            EditorGUILayout.PropertyField(_lockedInfoIconFxColor, new GUIContent("Locked Icon FX Color"));
            DrawSlotRef(_titleText, "Title");
            DrawSlotRef(_lockedOverlay, "Locked Overlay");
            DrawSlotRef(_lockedText, "Locked Text");

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Egg Slots", EditorStyles.miniBoldLabel);
            DrawSlotRef(_eggDateText, "Description / Date");
            DrawSlotRef(_eggPriceText, "Income / Price");
            DrawSlotRef(_eggSourcesText, "Sources");
            DrawArraySlotRef(_eggOnlyObjects, "Egg Only Objects");

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Animal Slots", EditorStyles.miniBoldLabel);
            DrawSlotRef(_animalDescriptionText, "Description");
            DrawSlotRef(_animalIncomeText, "Income");
            DrawSlotRef(_animalSourcesText, "Sources");
            DrawArraySlotRef(_animalOnlyObjects, "Animal Only Objects");

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Egg Hatch Preview", EditorStyles.miniBoldLabel);
            DrawSlotRef(_eggHatchSection, "Section");
            DrawSlotRef(_eggHatchIconsRoot, "Icons Root");
            DrawSlotRef(_eggHatchIconPrefab, "Icon Prefab");
            DrawSlotRef(_eggHatchIconTemplate, "Legacy Image Template");
            EditorGUILayout.PropertyField(_eggHatchMaxIcons, new GUIContent("Max Icons"));
            EditorGUILayout.PropertyField(_eggHatchIconSpacing, new GUIContent("Icon Spacing"));
        }
    }

    private static void DrawSlotRef(SerializedProperty property, string label)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.Toggle(property != null && property.objectReferenceValue != null, GUILayout.Width(18f));
            EditorGUILayout.PropertyField(property, new GUIContent(label));
        }
    }

    private static void DrawArraySlotRef(SerializedProperty property, string label)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.Toggle(property != null && property.arraySize > 0, GUILayout.Width(18f));
            EditorGUILayout.PropertyField(property, new GUIContent(label), true);
        }
    }

    private void ApplyPreviewToTargets()
    {
        for (var i = 0; i < targets.Length; i++)
        {
            if (targets[i] is not AlbumInfoPanelView view)
                continue;

            view.ApplyEditorPreview();
            EditorUtility.SetDirty(view);
        }
    }
}
#endif
