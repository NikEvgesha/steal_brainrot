using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FieldManager : MonoBehaviour
{
    [SerializeField] private Transform _fieldParent;

    private List<List<Field>> _fields = new();


    public void Start()
    {
        int rowsCount = _fieldParent.childCount;
        int fieldId = 0;
        for (int i = 0; i < rowsCount; i++)
        {
            Transform row = _fieldParent.GetChild(i);
            _fields.Add(row.GetComponentsInChildren<Field>().ToList());
            _fields[i].ForEach(x =>
            {
                x.SetID(fieldId);
                if (G.Save.LoadFieldUnblockStatus(fieldId))
                {
                    x.Unblock();
                    
                }
                x.LoadData();
                fieldId++;
            });
        }
    }

}