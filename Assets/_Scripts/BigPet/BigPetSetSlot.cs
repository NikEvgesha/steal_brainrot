using UnityEngine;
using UnityEngine.UI;

public class BigPetSetSlot : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private Image _background;
    [SerializeField] private GameObject _activeIndicator;

    private BigPetSetUI _ui;
    private Brainrot _pet;
    private bool _active;

    public void Init(BigPetSetUI ui, Brainrot pet) { 
        _ui = ui;
        _ui.ActiveChanged.AddListener(CheckActiveSlot);
        _icon.sprite = pet.Icon;
        _pet = pet;
        switch (_pet.RareType)
        {
            case RareType.Common:
                _background.color = Color.gray;
                break;
            case RareType.Uncommon:
                _background.color = Color.green;
                break;
            case RareType.Rare:
                _background.color = Color.blue;
                break;
            case RareType.Epic:
                _background.color = Color.yellow;
                break;
            case RareType.Legendary:
                _background.color = Color.magenta;
                break;
            case RareType.Mythic:
                _background.color = Color.red;
                break;
            default:
                break;
        }
    }

    public void OnClick()
    {
        if (_active) return;
        _ui.OnPetClicked(_pet);
    }

    private void CheckActiveSlot(Brainrot activePet)
    {
        _active = activePet == _pet;
        _activeIndicator.SetActive(activePet == _pet);
    }


}
