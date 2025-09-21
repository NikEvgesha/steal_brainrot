using UnityEngine;

public class FieldCell : MonoBehaviour
{
    [SerializeField] private GameObject _triggerIndicator;


    private bool _playerOnCell;

    public void _OnPlayerEnter()
    {
        _triggerIndicator.SetActive(true);
        _playerOnCell = true;
    }

    public void _OnPlayerExit()
    {
        _triggerIndicator.SetActive(false);
        _playerOnCell = false;
    }
}
