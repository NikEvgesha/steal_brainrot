using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

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

    public bool IsOpen => _uiPanel != null && _uiPanel.activeSelf;

    public void InitUI(IReadOnlyList<Brainrot> petList)
    {
        ClearSlots();

        if (petList == null || _slotParent == null || _slotPrefab == null)
            return;

        foreach (var pet in petList)
        {
            if (pet == null)
                continue;

            BigPetSetSlot slot = Instantiate(_slotPrefab, _slotParent);
            slot.Init(this, pet);
            _slots.Add(slot);
        }

        RebuildGrid();
    }

    private void ClearSlots()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            if (slot == null)
                continue;

            slot.gameObject.SetActive(false);
            Destroy(slot.gameObject);
        }

        _slots.Clear();
    }

    public void SetMaxAvailablePet(int idx)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] != null)
                _slots[i].gameObject.SetActive(i <= idx);
        }

        RebuildGrid();
    }

    private void RebuildGrid()
    {
        if (_slotParent != null && _slotParent.TryGetComponent<AdaptiveGridSpawner>(out var grid))
            grid.Rebuild();

        if (_slotParent is RectTransform rectTransform)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
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
        if (_remoteMode || pet == null)
            return;

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
        if (_uiPanel == null)
            return;

        _uiPanel.SetActive(open);
        if (open)
        {
            _uiPanel.transform.SetAsLastSibling();
            CanvasGroup canvasGroup = _uiPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = _uiPanel.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            RebuildGrid();
            Canvas.ForceUpdateCanvases();
        }
    }

    public void SetWorldTargeted(bool targeted)
    {
        if (_remoteMode)
            return;

        // Losing the world ray is expected as soon as the player moves the cursor
        // onto the screen-space menu. Keep the menu pinned until its explicit close
        // button calls OpenUI(false), otherwise the opening click races the close.
        if (targeted)
            OpenUI(true);
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
