using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FieldManager : MonoBehaviour
{
    [SerializeField] private Transform _fieldParent;

    private List<List<Field>> _fields = new();
    private bool _initialized;

    public void Start()
    {
        InitFields();
    }

    public void EnsureInitialized()
    {
        if (_initialized) return;
        InitFields();
    }

    private void InitFields()
    {
        if (_initialized) return;
        int rowsCount = _fieldParent.childCount;
        int fieldId = 0;
        for (int i = 0; i < rowsCount; i++)
        {
            Transform row = _fieldParent.GetChild(i);
            _fields.Add(row.GetComponentsInChildren<Field>().ToList());
            _fields[i].ForEach(x =>
            {
                if (!x.isActiveAndEnabled) { fieldId++; return; }
                x.SetID(fieldId);
                x.Init();
                if (G.Save.LoadFieldUnblockStatus(fieldId))
                {
                    x.Unblock();
                    
                }
                x.LoadData();
                fieldId++;
            });
        }
        _initialized = true;
    }

}