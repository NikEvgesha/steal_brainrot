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
    private Button _button;

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
        if (_button != null)
            _button.onClick.RemoveListener(OnClick);
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
        _button = GetComponent<Button>();
        if (_button == null)
            _button = gameObject.AddComponent<Button>();

        Graphic targetGraphic = _background != null ? _background : GetComponent<Graphic>();
        if (targetGraphic != null)
        {
            targetGraphic.raycastTarget = true;
            _button.targetGraphic = targetGraphic;
        }

        _button.interactable = true;
        _button.navigation = new Navigation { mode = Navigation.Mode.None };
        _button.onClick.RemoveListener(OnClick);

        bool hasPersistentClick = false;
        for (int i = 0; i < _button.onClick.GetPersistentEventCount(); i++)
        {
            if (_button.onClick.GetPersistentTarget(i) == this &&
                _button.onClick.GetPersistentMethodName(i) == nameof(OnClick))
            {
                hasPersistentClick = true;
                break;
            }
        }

        if (!hasPersistentClick)
            _button.onClick.AddListener(OnClick);

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
