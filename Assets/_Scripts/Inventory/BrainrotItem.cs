using UnityEngine;

[RequireComponent(typeof(Brainrot))]
public class BrainrotItem : InventoryItem
{
    private Brainrot _brainrot;

    private void Awake()
    {
        _brainrot = GetComponent<Brainrot>();
    }
}
