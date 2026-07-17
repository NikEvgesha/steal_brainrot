using UnityEngine;
using UnityEngine.UI;

public class QuickSlot : MonoBehaviour
{
    [SerializeField] private Image _img;
    [SerializeField] private Text _index;
    [SerializeField] private Image _activeFrame;

    private int _idx;
    private InventoryItem _item;
    private Button _button;

    public InventoryItem Item => _item;

    public void Select()
    {
        if (_item != null)
            G.QuickAccess.SwitchActive(_item);
    }

    private void Awake()
    {
        _button = GetComponent<Button>();
        if (_button == null)
            _button = gameObject.AddComponent<Button>();
        if (_button.targetGraphic == null)
            _button.targetGraphic = _img;
        _button.onClick.RemoveListener(Select);
        _button.onClick.AddListener(Select);
    }

    public void SetIndex(int idx)
    {
        _index.text = idx.ToString();
        _idx = idx;
        BlockyUITheme.StyleQuickSlot(gameObject, _activeFrame);
    }


    public void Init(InventoryItem item = null)
    {
        if (G.QuickAccess != null)
            G.QuickAccess.SwitchActiveItem.RemoveListener(OnActiveItemSwitch);
        _item = item;
        BlockyUITheme.StyleQuickSlot(gameObject, _activeFrame);
        if (item == null)
        {
            _img.sprite = null;
            return;
        }
        
        _img.sprite = item.Icon;
        G.QuickAccess.SwitchActiveItem.AddListener(OnActiveItemSwitch);
    }

    private void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(Select);
        if (G.QuickAccess != null)
            G.QuickAccess.SwitchActiveItem.RemoveListener(OnActiveItemSwitch);
    }

    private void Update()
    {
        if (Input.GetKeyDown((KeyCode)(48 + _idx)))
        {
            G.QuickAccess.SwitchActive(_item);
        }
    }


    private void OnActiveItemSwitch(InventoryItem item)
    {
        _activeFrame.gameObject.SetActive(item == _item);
    }

}
