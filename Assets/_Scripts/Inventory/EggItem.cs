using UnityEngine;

[RequireComponent(typeof(Egg))]
public class EggItem : InventoryItem
{
    private Egg _egg;

    private void Awake()
    {
        _egg = GetComponent<Egg>();
    }
}
