using UnityEngine;
using UnityEngine.UI;

public class ShopRow : MonoBehaviour
{
    private int _maxItems;

    public int MaxItems => _maxItems;
    public int ItemsCount => transform.childCount;

    private void Awake()
    {
        ApplyLayout();
    }

    public void setMaxItems(int amount)
    {
        _maxItems = amount;
    }

    [ContextMenu("Apply shop row layout")]
    public void ApplyLayout()
    {
        var layout = GetComponent<LayoutElement>();
        if (layout == null)
            layout = gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 264f;
        layout.preferredHeight = 264f;

        var horizontal = GetComponent<HorizontalLayoutGroup>();
        if (horizontal != null)
        {
            horizontal.padding = new RectOffset(6, 6, 7, 7);
            horizontal.spacing = 14f;
            horizontal.childAlignment = TextAnchor.UpperCenter;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = true;
            horizontal.childForceExpandHeight = false;
        }
    }
}
