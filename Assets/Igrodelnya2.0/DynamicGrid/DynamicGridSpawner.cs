using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(VerticalLayoutGroup))]
public class DynamicGridSpawner : MonoBehaviour
{
    [SerializeField] private int maxItemsPerRow = 3;
    [SerializeField] private GameObject rowPrefab;

    [Header("Optional Compact Layout")]
    [SerializeField] private bool compactLayout = false;
    [SerializeField] private float horizontalSpacing = 8f;
    [SerializeField] private float verticalSpacing = 8f;

    private VerticalLayoutGroup verticalLayout;

    private void Awake()
    {
        verticalLayout = GetComponent<VerticalLayoutGroup>();
        ApplyVerticalLayoutDefaults();

        if (rowPrefab == null)
            rowPrefab = CreateRowPrefab();
    }

    public GameObject SpawnObject(GameObject prefab)
    {
        var targetRow = GetOrCreateAvailableRow();
        var spawnedObject = Instantiate(prefab, targetRow);
        if (!spawnedObject.activeSelf)
            spawnedObject.SetActive(true);
        UpdateRowHeightFromChild(targetRow, spawnedObject);
        return spawnedObject;
    }

    public T SpawnObject<T>(GameObject prefab) where T : Component
    {
        var targetRow = GetOrCreateAvailableRow();
        var spawnedObject = Instantiate(prefab, targetRow);
        if (!spawnedObject.activeSelf)
            spawnedObject.SetActive(true);
        UpdateRowHeightFromChild(targetRow, spawnedObject);
        return spawnedObject.GetComponent<T>();
    }

    private Transform GetOrCreateAvailableRow()
    {
        var itemsPerRow = Mathf.Max(1, maxItemsPerRow);

        foreach (Transform row in transform)
        {
            if (row == null || !row.gameObject.activeSelf)
                continue;

            if (!row.TryGetComponent<HorizontalLayoutGroup>(out var existingLayout))
                continue;

            if (compactLayout)
                ApplyRowLayoutDefaults(existingLayout);

            if (row.childCount < itemsPerRow)
                return row;
        }

        var newRow = Instantiate(rowPrefab, transform);
        if (compactLayout && newRow.TryGetComponent<HorizontalLayoutGroup>(out var horizontalLayout))
            ApplyRowLayoutDefaults(horizontalLayout);

        return newRow.transform;
    }

    private GameObject CreateRowPrefab()
    {
        var row = new GameObject("Row", typeof(RectTransform));
        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return row;
    }

    public void RefreshLayout()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
    }

    private void ApplyVerticalLayoutDefaults()
    {
        if (verticalLayout == null || !compactLayout)
            return;

        verticalLayout.childAlignment = TextAnchor.UpperLeft;
        verticalLayout.childControlWidth = true;
        verticalLayout.childControlHeight = true;
        verticalLayout.childForceExpandWidth = false;
        verticalLayout.childForceExpandHeight = false;
        verticalLayout.spacing = Mathf.Max(0f, verticalSpacing);
    }

    private void ApplyRowLayoutDefaults(HorizontalLayoutGroup layout)
    {
        if (layout == null || !compactLayout)
            return;

        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = Mathf.Max(0f, horizontalSpacing);
    }

    private void UpdateRowHeightFromChild(Transform row, GameObject child)
    {
        if (!compactLayout || row == null || child == null)
            return;

        var childLayout = child.GetComponent<LayoutElement>();
        var preferred = 0f;
        if (childLayout != null)
        {
            preferred = Mathf.Max(preferred, childLayout.preferredHeight);
            preferred = Mathf.Max(preferred, childLayout.minHeight);
        }

        if (preferred <= 0f && child.transform is RectTransform childRt)
            preferred = LayoutUtility.GetPreferredHeight(childRt);

        if (preferred <= 0f && child.transform is RectTransform fallbackRt)
            preferred = Mathf.Min(256f, Mathf.Abs(fallbackRt.sizeDelta.y));

        if (preferred <= 0f)
            return;

        var rowLayout = row.GetComponent<LayoutElement>();
        if (rowLayout == null)
            rowLayout = row.gameObject.AddComponent<LayoutElement>();

        rowLayout.minHeight = Mathf.Max(rowLayout.minHeight, preferred);
        rowLayout.preferredHeight = Mathf.Max(rowLayout.preferredHeight, preferred);
        rowLayout.flexibleHeight = 0f;
    }
}
