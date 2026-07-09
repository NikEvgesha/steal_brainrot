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

    public void Init(BigPetSetUI ui, Brainrot pet)
    {
        _ui = ui;
        _pet = pet;
        EnsureClickTarget();

        if (_ui != null)
            _ui.ActiveChanged.AddListener(CheckActiveSlot);
        if (_icon != null)
            _icon.sprite = pet != null ? pet.Icon : null;
        if (_background == null)
            Debug.LogWarning("[BigPetSetSlot] Background is not assigned.", this);
    }

    public void OnClick()
    {
        if (_active || _ui == null || _pet == null) return;
        _ui.OnPetClicked(_pet);
    }

    private void OnDestroy()
    {
        if (_ui != null)
            _ui.ActiveChanged.RemoveListener(CheckActiveSlot);
    }

    private void CheckActiveSlot(Brainrot activePet)
    {
        _active = activePet != null && activePet == _pet;
        if (_activeIndicator != null)
            _activeIndicator.SetActive(_active);
    }

    private void EnsureClickTarget()
    {
        var button = GetComponent<Button>();
        var targetGraphic = button != null ? button.targetGraphic : _background;
        if (button != null && targetGraphic == null && _background != null)
        {
            targetGraphic = _background;
            button.targetGraphic = _background;
        }

        var graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            var graphic = graphics[i];
            if (graphic == null)
                continue;

            graphic.raycastTarget = graphic == targetGraphic;
        }
    }
}
