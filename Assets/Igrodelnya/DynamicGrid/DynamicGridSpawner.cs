using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(VerticalLayoutGroup))]
public class DynamicGridSpawner : MonoBehaviour
{
    [SerializeField] private int maxItemsPerRow = 3; // Максимум элементов в строке
    //[SerializeField] private Vector2 spacing = new Vector2(10f, 10f); // Отступы между элементами
    [SerializeField] private GameObject rowPrefab; // Префаб строки (с HorizontalLayoutGroup)

    private VerticalLayoutGroup verticalLayout;

    void Awake()
    {
        verticalLayout = GetComponent<VerticalLayoutGroup>();
        //verticalLayout.spacing = spacing.y;

        // Если rowPrefab не задан, создаём его автоматически
        if (rowPrefab == null)
        {
            rowPrefab = CreateRowPrefab();
        }
    }

    // Метод для спавна объекта
    public GameObject SpawnObject(GameObject prefab)
    {
        Transform targetRow = GetOrCreateAvailableRow();
        GameObject spawnedObject = Instantiate(prefab, targetRow);
        return spawnedObject;
    }
    public T SpawnObject<T>(GameObject prefab) where T : Component
    {
        Transform targetRow = GetOrCreateAvailableRow();
        GameObject spawnedObject = Instantiate(prefab, targetRow);
        return spawnedObject.GetComponent<T>();
    }
    // Получение или создание строки с местом
    private Transform GetOrCreateAvailableRow()
    {
        // Проверяем существующие строки
        foreach (Transform row in transform)
        {
            if (row.childCount < maxItemsPerRow)
            {
                return row;
            }
        }
        // Если нет свободной строки, создаём новую
        GameObject newRow = Instantiate(rowPrefab, transform);
        HorizontalLayoutGroup horizontalLayout = newRow.GetComponent<HorizontalLayoutGroup>();
        //horizontalLayout.spacing = spacing.x;
        return newRow.transform;
    }

    // Создание шаблона строки по умолчанию
    private GameObject CreateRowPrefab()
    {
        GameObject row = new GameObject("Row", typeof(RectTransform));
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return row;
    }

    // Метод для пересчёта layout (если нужно вручную обновить)
    public void RefreshLayout()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
    }
}