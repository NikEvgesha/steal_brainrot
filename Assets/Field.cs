using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Field : MonoBehaviour
{
    [SerializeField] private GameObject _grassObj;
    [SerializeField] private Transform _cellsParent;
    [SerializeField] private bool _unblocked;

    private List<FieldCell> _cells;
    private void Start()
    {
        _cells = _cellsParent.GetComponentsInChildren<FieldCell>().ToList();

        if (_unblocked)
        {
            Destroy(_grassObj);
        } else
        {
            foreach (FieldCell cell in _cells)
            {
                cell.gameObject.SetActive(false);
            }
        }


    }
}
