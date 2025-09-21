using System;
using UnityEngine;
using UnityEngine.UI;

public class PushPlatform : MonoBehaviour
{
    [SerializeField] private GameObject _incomeCanvas;
    [SerializeField] private Text _incomeText;

    public Action<bool> PlayerOnPlatform;

    public void SetText(ulong income)
    {
        // TODO: format string (1K, 2.2M ...)
        _incomeText.text = income.ToString(); 
    }

    public void SetVisible(bool visible)
    {
        _incomeCanvas.SetActive(visible);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerOnPlatform?.Invoke(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerOnPlatform?.Invoke(false);
        }
    }

}
