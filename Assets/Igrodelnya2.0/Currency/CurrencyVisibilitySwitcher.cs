using UnityEngine;

public class CurrencyVisibilitySwitcher : MonoBehaviour
{
    [SerializeField] private GameObject _gems;
    [SerializeField] private GameObject _coins;

    private void Start()
    {
        G.Currency.ShowGems.AddListener(SwitchGemsVisibility);
        LoadingManager.Instance.LocationChanged += OnLocationChange;
    }

    private void OnDisable()
    {
        G.Currency.ShowGems.RemoveListener(SwitchGemsVisibility);
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
                SwitchCoinsVisibility(true);
                break;
            case Location.Game:
                SwitchGemsVisibility(true);
                SwitchCoinsVisibility(true);
                break;
            default:
                break;
        }
    }
}
