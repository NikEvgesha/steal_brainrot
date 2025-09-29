using UnityEngine;
using UnityEngine.UI;

public class QuickSlot : MonoBehaviour
{
    [SerializeField] private Image _img;
    [SerializeField] private Text _index;
    [SerializeField] private Image _activeFrame;

    private int _idx;
    private InventoryItem _item;

    public void SetIndex(int idx)
    {
        _index.text = idx.ToString();
        _idx = idx;
    }


    public void Init(InventoryItem item = null)
    {
        _item = item;
        if (item == null)
        {
            _img.sprite = null;
            return;
        }
        
        _img.sprite = item.Icon;
        QuickAccessManager.Instance.SwitchActiveItem.AddListener(OnActiveItemSwitch);
    }

    private void Update()
    {
        if (Input.GetKeyDown((KeyCode)(48 + _idx)))
        {
            QuickAccessManager.Instance.SwitchActive(_item);
        }
    }


    private void OnActiveItemSwitch(InventoryItem item)
    {
        _activeFrame.gameObject.SetActive(item == _item);
    }

}