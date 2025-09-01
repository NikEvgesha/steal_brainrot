using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CurrencyVisibilitySwitcher : MonoBehaviour
{
    [SerializeField] private GameObject _gems;
    [SerializeField] private GameObject _coins;

    private void Start()
    {
        CurrencyManager.Instance.ShowGems += SwitchGemsVisibility;
        LoadingManager.Instance.LocationChanged += OnLocationChange;
    }

    private void OnDisable()
    {
        CurrencyManager.Instance.ShowGems -= SwitchGemsVisibility;
        LoadingManager.Instance.LocationChanged -= OnLocationChange;
    }

    private void SwitchGemsVisibility(bool visible)
    {
        _gems.SetActive(visible);
    }

    private void SwitchCoinsVisibility(bool visible)
    {
        _coins.SetActive(visible);
    }

    private void OnLocationChange(Location location)
    {
        switch (location)
        {
            case Location.Lobby:
                SwitchGemsVisibility(true);
                SwitchCoinsVisibility(false);
                break;
            case Location.Game:
                SwitchGemsVisibility(false);
                SwitchCoinsVisibility(true);
                break;
            default:
                break;
        }
    }
}
