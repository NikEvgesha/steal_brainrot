#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AdaptiveGridSpawner))]
[CanEditMultipleObjects]
public sealed class AdaptiveGridSpawnerEditor : Editor
{
    private SerializedProperty itemPrefab;
    private SerializedProperty skipPrefabWhenItIsChild;
    private SerializedProperty includeInactiveChildren;

    private SerializedProperty sizingMode;
    private SerializedProperty startCorner;
    private SerializedProperty fillDirection;
    private SerializedProperty padding;
    private SerializedProperty spacing;
    private SerializedProperty paddingUnits;
    private SerializedProperty paddingPercent;
    private SerializedProperty spacingUnits;
    private SerializedProperty spacingPercent;

    private SerializedProperty fixedCellSize;
    private SerializedProperty hideFixedOverflow;

    private SerializedProperty targetItemCount;
    private SerializedProperty preferredColumns;
    private SerializedProperty preferredRows;
    private SerializedProperty preserveAspectRatio;
    private SerializedProperty cellAspectRatio;
    private SerializedProperty useMinCellSize;
    private SerializedProperty minCellSize;
    private SerializedProperty useMaxCellSize;
    private SerializedProperty maxCellSize;
    private SerializedProperty allowBelowMinWhenNeeded;

    private SerializedProperty rebuildOnEnable;
    private SerializedProperty rebuildOnRectChange;
    private SerializedProperty deferRuntimeRebuildOneFrame;

    private void OnEnable()
    {
        itemPrefab = serializedObject.FindProperty("itemPrefab");
        skipPrefabWhenItIsChild = serializedObject.FindProperty("skipPrefabWhenItIsChild");
        includeInactiveChildren = serializedObject.FindProperty("includeInactiveChildren");

        sizingMode = serializedObject.FindProperty("sizingMode");
        startCorner = serializedObject.FindProperty("startCorner");
        fillDirection = serializedObject.FindProperty("fillDirection");
        padding = serializedObject.FindProperty("padding");
        spacing = serializedObject.FindProperty("spacing");
        paddingUnits = serializedObject.FindProperty("paddingUnits");
        paddingPercent = serializedObject.FindProperty("paddingPercent");
        spacingUnits = serializedObject.FindProperty("spacingUnits");
        spacingPercent = serializedObject.FindProperty("spacingPercent");

        fixedCellSize = serializedObject.FindProperty("fixedCellSize");
        hideFixedOverflow = serializedObject.FindProperty("hideFixedOverflow");

        targetItemCount = serializedObject.FindProperty("targetItemCount");
        preferredColumns = serializedObject.FindProperty("preferredColumns");
        preferredRows = serializedObject.FindProperty("preferredRows");
        preserveAspectRatio = serializedObject.FindProperty("preserveAspectRatio");
        cellAspectRatio = serializedObject.FindProperty("cellAspectRatio");
        useMinCellSize = serializedObject.FindProperty("useMinCellSize");
        minCellSize = serializedObject.FindProperty("minCellSize");
        useMaxCellSize = serializedObject.FindProperty("useMaxCellSize");
        maxCellSize = serializedObject.FindProperty("maxCellSize");
        allowBelowMinWhenNeeded = serializedObject.FindProperty("allowBelowMinWhenNeeded");

        rebuildOnEnable = serializedObject.FindProperty("rebuildOnEnable");
        rebuildOnRectChange = serializedObject.FindProperty("rebuildOnRectChange");
        deferRuntimeRebuildOneFrame = serializedObject.FindProperty("deferRuntimeRebuildOneFrame");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawSource();
        DrawLayout();
        DrawSizingMode();
        DrawRuntime();
        DrawReadOnlyMetrics();

        if (serializedObject.ApplyModifiedProperties())
            RebuildTargets();
    }

    private void DrawSource()
    {
        EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(itemPrefab);
        EditorGUILayout.PropertyField(skipPrefabWhenItIsChild);
        EditorGUILayout.PropertyField(includeInactiveChildren);
        EditorGUILayout.Space(8f);
    }

    private void DrawLayout()
    {
        EditorGUILayout.LabelField("Layout", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(sizingMode);
        EditorGUILayout.PropertyField(startCorner);
        EditorGUILayout.PropertyField(fillDirection);

        EditorGUILayout.PropertyField(paddingUnits);
        DrawPaddingBySelectedUnits();

        EditorGUILayout.PropertyField(spacingUnits);
        DrawSpacingBySelectedUnits();
        EditorGUILayout.Space(8f);
    }

    private void DrawPaddingBySelectedUnits()
    {
        if (IsMixed(paddingUnits))
        {
            EditorGUILayout.PropertyField(padding);
            EditorGUILayout.PropertyField(paddingPercent, true);
            return;
        }

        if ((AdaptiveGridSpawner.LayoutValueMode)paddingUnits.enumValueIndex == AdaptiveGridSpawner.LayoutValueMode.Percent)
            EditorGUILayout.PropertyField(paddingPercent, true);
        else
            EditorGUILayout.PropertyField(padding);
    }

    private void DrawSpacingBySelectedUnits()
    {
        if (IsMixed(spacingUnits))
        {
            EditorGUILayout.PropertyField(spacing);
            EditorGUILayout.PropertyField(spacingPercent);
            return;
        }

        if ((AdaptiveGridSpawner.LayoutValueMode)spacingUnits.enumValueIndex == AdaptiveGridSpawner.LayoutValueMode.Percent)
            EditorGUILayout.PropertyField(spacingPercent);
        else
            EditorGUILayout.PropertyField(spacing);
    }

    private void DrawSizingMode()
    {
        if (IsMixed(sizingMode))
        {
            DrawFixedSettings();
            DrawFitSettings();
            return;
        }

        var mode = (AdaptiveGridSpawner.CellSizingMode)sizingMode.enumValueIndex;
        if (mode == AdaptiveGridSpawner.CellSizingMode.FixedCellSize)
            DrawFixedSettings();
        else
            DrawFitSettings();
    }

    private void DrawFixedSettings()
    {
        EditorGUILayout.LabelField("Fixed Cell Size", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(fixedCellSize);
        EditorGUILayout.PropertyField(hideFixedOverflow);
        EditorGUILayout.Space(8f);
    }

    private void DrawFitSettings()
    {
        EditorGUILayout.LabelField("Fit Item Count", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(targetItemCount);
        EditorGUILayout.PropertyField(preferredColumns);
        EditorGUILayout.PropertyField(preferredRows);
        EditorGUILayout.PropertyField(preserveAspectRatio);

        if (IsMixed(preserveAspectRatio) || preserveAspectRatio.boolValue)
            EditorGUILayout.PropertyField(cellAspectRatio);

        EditorGUILayout.PropertyField(useMinCellSize);
        if (IsMixed(useMinCellSize) || useMinCellSize.boolValue)
        {
            EditorGUILayout.PropertyField(minCellSize);
            EditorGUILayout.PropertyField(allowBelowMinWhenNeeded);
        }

        EditorGUILayout.PropertyField(useMaxCellSize);
        if (IsMixed(useMaxCellSize) || useMaxCellSize.boolValue)
            EditorGUILayout.PropertyField(maxCellSize);

        EditorGUILayout.Space(8f);
    }

    private void DrawRuntime()
    {
        EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(rebuildOnEnable);
        EditorGUILayout.PropertyField(rebuildOnRectChange);
        EditorGUILayout.PropertyField(deferRuntimeRebuildOneFrame);
        EditorGUILayout.Space(8f);
    }

    private void DrawReadOnlyMetrics()
    {
        if (targets.Length != 1)
            return;

        var grid = (AdaptiveGridSpawner)target;
        EditorGUILayout.LabelField("Calculated", EditorStyles.boldLabel);
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.IntField("Columns", grid.Columns);
        EditorGUILayout.IntField("Rows", grid.Rows);
        EditorGUILayout.IntField("Capacity", grid.Capacity);
        EditorGUILayout.Vector2Field("Cell Size", grid.CellSize);
        EditorGUI.EndDisabledGroup();
    }

    private static bool IsMixed(SerializedProperty property)
    {
        return property != null && property.hasMultipleDifferentValues;
    }

    private void RebuildTargets()
    {
        for (var i = 0; i < targets.Length; i++)
        {
            if (targets[i] is AdaptiveGridSpawner grid)
                grid.Rebuild();
        }
    }
}
#endif
