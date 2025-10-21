using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ConveyorLevelTab : MonoBehaviour
{
    private Text _name;
    private ConveyorLevel _level;

    [HideInInspector]
    public UnityEvent<ConveyorLevel> OnClick = new();

    public void Init(ConveyorLevel level)
    {
        _name = GetComponentInChildren<Text>();
        _level = level;
        _name.text = _level.Name;
    }

    public void _OnClick()
    {
        OnClick?.Invoke(_level);
    }
}