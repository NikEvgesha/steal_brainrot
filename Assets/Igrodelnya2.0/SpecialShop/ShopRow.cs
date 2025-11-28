using UnityEngine;

public class ShopRow : MonoBehaviour
{
    private int _maxItems;

    public int MaxItems => _maxItems;
    public int ItemsCount => transform.childCount;

    public void setMaxItems(int amount)
    {
        _maxItems = amount;
    }
}