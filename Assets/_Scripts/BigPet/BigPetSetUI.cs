using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class BigPetSetUI : MonoBehaviour
{
    [SerializeField] private GameObject _uiPanel;
    [SerializeField] private BigPetSetSlot _slotPrefab;
    [SerializeField] private Transform _slotParent;
    [SerializeField] private bool _remoteMode;

    private List<BigPetSetSlot> _slots = new();

    [HideInInspector]
    public UnityEvent<Brainrot> PetSlotClicked;
    [HideInInspector]
    public UnityEvent<Brainrot> ActiveChanged;

    public void InitUI(List<Brainrot> petList)
    {
        foreach (var pet in petList)
        {
            BigPetSetSlot slot = Instantiate(_slotPrefab, _slotParent);
            slot.Init(this, pet);
            _slots.Add(slot);
        }
    }

    public void SetMaxAvailablePet(int idx)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            _slots[i].gameObject.SetActive(i <= idx);
        }
    }


    //public void UpdateUI(Dictionary<Food, int> stock)
    //{
    //    foreach (BigPetSetSlot slot in _slots)
    //    {
    //        if (stock.ContainsKey(slot.Food))
    //        {
    //            int amount = stock[slot.Food];
    //            slot.SetAmount(amount);
    //            slot.SetAvailability(amount > 0);
    //        }
    //    }
    //}


    public void OnPetClicked(Brainrot pet)
    {
        PetSlotClicked?.Invoke(pet);
    }


    public void OpenUI(bool open)
    {
        if (_remoteMode)
        {
            if (_uiPanel != null)
                _uiPanel.SetActive(false);
            return;
        }
        _uiPanel.SetActive(open);
    }

    public void ChangeActivePet(Brainrot pet)
    {
        ActiveChanged?.Invoke(pet);
    }

    public void SetRemoteMode(bool remote)
    {
        _remoteMode = remote;
        if (_remoteMode && _uiPanel != null)
            _uiPanel.SetActive(false);
    }
}
