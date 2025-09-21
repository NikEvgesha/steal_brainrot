using UnityEngine;

public class FieldCell : MonoBehaviour
{
    [SerializeField] private GameObject _triggerIndicator;


    private bool _playerOnCell;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _triggerIndicator.SetActive(true);
            _playerOnCell = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _triggerIndicator.SetActive(false);
            _playerOnCell = false;
        }
    }
}
