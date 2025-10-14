using UnityEngine;

public class GameEntryPoint : MonoBehaviour
{
    [SerializeField] private Inventory _inventory;
    [SerializeField] private PlayerManager _playerManager;
    [SerializeField] private QuickAccessManager _quickAccess;
    [SerializeField] private GameObject _ui;
    [SerializeField] private GameObject _scene;
    [SerializeField] private ElementTypeMultiplaer _elements;

    private void Start()
    {
        //Instantiate(_playerManager);
        Instantiate(_inventory).Init();
        Instantiate(_quickAccess).Init();
        Instantiate(_elements);
        Instantiate(_ui);
        _scene.SetActive(true);
        G.GameLoader.ShowLoadingScreen(false);
    }
}
