using System;
using UnityEngine;

public class TriggerChecker : MonoBehaviour
{
    public Action<Brainrot> EnterArea;

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<Brainrot>(out Brainrot brainrot))
        {
            EnterArea?.Invoke(brainrot);
        }
    }
}
