using UnityEngine;
using UnityEngine.UI;

public class ShopRow : MonoBehaviour
{
    private int _maxItems;
    private RectTransform _rectTransform;
    private HorizontalLayoutGroup _horizontal;
    private float _lastAppliedWidth = float.NaN;

    public int MaxItems => _maxItems;
    public int ItemsCount => transform.childCount;

    private void Awake()
    {
        ApplyLayout();
    }

    private void OnRectTransformDimensionsChange()
    {
        RefreshItemWidths();
    }

    public void setMaxItems(int amount)
    {
        _maxItems = Mathf.Max(1, amount);
        _lastAppliedWidth = float.NaN;
        RefreshItemWidths();
    }

    public void RefreshHeight()
    {
        float contentHeight = 250f;
        for (int i = 0; i < transform.childCount; i++)
        {
            var childLayout = transform.GetChild(i).GetComponent<LayoutElement>();
            if (childLayout != null)
                contentHeight = Mathf.Max(contentHeight, childLayout.preferredHeight);
        }

        var layout = GetComponent<LayoutElement>();
        if (layout == null)
            layout = gameObject.AddComponent<LayoutElement>();
        layout.minHeight = contentHeight + 14f;
        layout.preferredHeight = contentHeight + 14f;
        RefreshItemWidths();
    }

    [ContextMenu("Apply shop row layout")]
    public void ApplyLayout()
    {
        var layout = GetComponent<LayoutElement>();
        if (layout == null)
            layout = gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 264f;
        layout.preferredHeight = 264f;

        _rectTransform = transform as RectTransform;
        _horizontal = GetComponent<HorizontalLayoutGroup>();
        if (_horizontal != null)
        {
            _horizontal.padding = new RectOffset(6, 6, 7, 7);
            _horizontal.spacing = 14f;
            _horizontal.childAlignment = TextAnchor.UpperLeft;
            _horizontal.childControlWidth = true;
            _horizontal.childControlHeight = true;
            _horizontal.childForceExpandWidth = false;
            _horizontal.childForceExpandHeight = false;
        }

        RefreshHeight();
    }

    private void RefreshItemWidths()
    {
        if (_maxItems <= 0)
            return;

        _rectTransform ??= transform as RectTransform;
        _horizontal ??= GetComponent<HorizontalLayoutGroup>();
        if (_rectTransform == null || _horizontal == null)
            return;

        float rowWidth = _rectTransform.rect.width;
        if (rowWidth <= 1f || Mathf.Abs(rowWidth - _lastAppliedWidth) < 0.25f)
            return;

        int columns = Mathf.Max(1, _maxItems);
        float innerWidth = Mathf.Max(
            1f,
            rowWidth - _horizontal.padding.horizontal - _horizontal.spacing * (columns - 1));
        float itemWidth = innerWidth / columns;

        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            var childLayout = child.GetComponent<LayoutElement>();
            if (childLayout == null)
                childLayout = child.gameObject.AddComponent<LayoutElement>();
            childLayout.minWidth = itemWidth;
            childLayout.preferredWidth = itemWidth;
            childLayout.flexibleWidth = 0f;
        }

        _lastAppliedWidth = rowWidth;
        LayoutRebuilder.MarkLayoutForRebuild(_rectTransform);
    }
}
