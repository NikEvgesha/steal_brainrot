using System.Collections;
using System.Linq;
using UnityEngine;

public class GiftChestController : MonoBehaviour
{
    public static bool Disabled = true;

    [Header("Mode")]
    [SerializeField] private bool isOwnerChest = false;
    [SerializeField] private string targetFriendCode;
    [SerializeField] private bool requireTargetOnline = true;

    [Header("UI")]
    [SerializeField] private InteractionPanel interactionPanel;
    [SerializeField] private GameObject hasItemsIndicator;
    [SerializeField] private GiftChestUI chestUI;
    [SerializeField] private string depositText = "Положить";
    [SerializeField] private string claimText = "Забрать";
    [SerializeField] private string emptyText = "Пусто";
    [SerializeField] private string noSpaceText = "Нет места";
    [SerializeField] private string offlineText = "Игрок оффлайн";

    [Header("Owner Polling")]
    [SerializeField] private float pollIntervalSec = 10f;

    private ZooBackendClient backend;
    private bool playerInArea;
    private bool hasItems;
    private bool targetOnline = true;
    private bool actionInFlight;
    private bool pollInFlight;
    private ChestStateResponse lastChest;
    private Coroutine pollRoutine;
    private Coroutine tempRoutine;

    private void Awake()
    {
        if (Disabled)
        {
            gameObject.SetActive(false);
            return;
        }
        if (backend == null) backend = G.Backend;
        if (interactionPanel == null) interactionPanel = GetComponentInChildren<InteractionPanel>(true);
    }

    private void OnEnable()
    {
        if (interactionPanel != null)
            interactionPanel.InteractionComplete.AddListener(OnInteract);

        if (isOwnerChest && pollIntervalSec > 0f)
            pollRoutine = StartCoroutine(PollChest());
    }

    private void OnDisable()
    {
        if (interactionPanel != null)
            interactionPanel.InteractionComplete.RemoveListener(OnInteract);
        if (pollRoutine != null)
        {
            StopCoroutine(pollRoutine);
            pollRoutine = null;
        }
        if (tempRoutine != null)
        {
            StopCoroutine(tempRoutine);
            tempRoutine = null;
        }
    }

    public void SetOwnerChest(bool owner)
    {
        isOwnerChest = owner;
    }

    public void SetRemoteTarget(string friendCode, bool online)
    {
        isOwnerChest = false;
        targetFriendCode = friendCode;
        targetOnline = online;
        if (requireTargetOnline)
            gameObject.SetActive(online);
        UpdatePanel();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInArea = true;
        SubscribeHand();
        UpdatePanel();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInArea = false;
        UnsubscribeHand();
        if (interactionPanel != null) interactionPanel.gameObject.SetActive(false);
    }

    private void SubscribeHand()
    {
        if (G.QuickAccess != null)
            G.QuickAccess.SwitchActiveItem.AddListener(OnHandChanged);
    }

    private void UnsubscribeHand()
    {
        if (G.QuickAccess != null)
            G.QuickAccess.SwitchActiveItem.RemoveListener(OnHandChanged);
    }

    private void OnHandChanged(InventoryItem _)
    {
        UpdatePanel();
    }

    private void UpdatePanel()
    {
        if (interactionPanel == null) return;

        if (!playerInArea)
        {
            interactionPanel.gameObject.SetActive(false);
            return;
        }

        if (isOwnerChest)
        {
            interactionPanel.gameObject.SetActive(true);
            interactionPanel.SetInfo(hasItems ? claimText : emptyText);
            return;
        }

        if (requireTargetOnline && !targetOnline)
        {
            interactionPanel.gameObject.SetActive(false);
            return;
        }

        var current = G.QuickAccess != null ? G.QuickAccess.CurrentActive : null;
        bool canDeposit = current != null && (current.Type == Item.Egg || current.Type == Item.Brainrot);
        interactionPanel.gameObject.SetActive(canDeposit);
        if (canDeposit)
            interactionPanel.SetInfo(depositText);
    }

    private void OnInteract()
    {
        if (actionInFlight) return;

        if (isOwnerChest)
        {
            if (chestUI != null)
            {
                StartCoroutine(OpenOwnerUI());
            }
            else
            {
                if (!hasItems)
                {
                    ShowTemp(emptyText);
                    return;
                }
                StartCoroutine(ClaimFirstAvailable());
            }
            return;
        }

        if (requireTargetOnline && !targetOnline)
        {
            ShowTemp(offlineText);
            return;
        }

        if (string.IsNullOrWhiteSpace(targetFriendCode))
        {
            ShowTemp(offlineText);
            return;
        }

        var current = G.QuickAccess != null ? G.QuickAccess.CurrentActive : null;
        if (current == null) return;
        if (current.Type != Item.Egg && current.Type != Item.Brainrot) return;
        StartCoroutine(DepositCurrent(current));
    }

    private IEnumerator PollChest()
    {
        while (true)
        {
            yield return FetchChest(true);
            yield return new WaitForSeconds(pollIntervalSec);
        }
    }

    private IEnumerator FetchChest(bool fromPoll = false)
    {
        if (backend == null) yield break;
        if (fromPoll)
        {
            if (pollInFlight) yield break;
            pollInFlight = true;
        }
        yield return backend.GetChest(
            resp =>
            {
                lastChest = resp;
                hasItems = resp != null && resp.slots != null && resp.slots.Any(s => !string.IsNullOrEmpty(s.itemType));
                if (hasItemsIndicator != null) hasItemsIndicator.SetActive(hasItems);
                UpdatePanel();
                if (fromPoll) pollInFlight = false;
            },
            (_, __) =>
            {
                if (fromPoll) pollInFlight = false;
            }
        );
    }

    private IEnumerator ClaimFirstAvailable()
    {
        if (backend == null) yield break;
        actionInFlight = true;

        if (lastChest == null || lastChest.slots == null || lastChest.slots.Count == 0)
            yield return FetchChest();

        var slot = lastChest?.slots?.FirstOrDefault(s => !string.IsNullOrEmpty(s.itemType));
        if (slot == null)
        {
            hasItems = false;
            if (hasItemsIndicator != null) hasItemsIndicator.SetActive(false);
            UpdatePanel();
            actionInFlight = false;
            yield break;
        }

        yield return backend.ClaimChest(slot.slotIndex,
            resp =>
            {
                if (resp != null)
                {
                    AddItemToInventory(resp.itemType, resp.itemData);
                    if (lastChest != null)
                    {
                        var targetSlot = lastChest.slots.FirstOrDefault(s => s.slotIndex == slot.slotIndex);
                        if (targetSlot != null)
                        {
                            targetSlot.itemType = null;
                            targetSlot.itemData = null;
                            targetSlot.itemDataRaw = null;
                        }
                    }
                }
                hasItems = lastChest != null && lastChest.slots.Any(s => !string.IsNullOrEmpty(s.itemType));
                if (hasItemsIndicator != null) hasItemsIndicator.SetActive(hasItems);
                UpdatePanel();
                actionInFlight = false;
            },
            (code, _) =>
            {
                if (code == 404)
                    ShowTemp(emptyText);
                actionInFlight = false;
            }
        );
    }

    private IEnumerator OpenOwnerUI()
    {
        if (backend == null) yield break;
        actionInFlight = true;

        ChestStateResponse state = null;
        yield return backend.GetChest(
            resp => state = resp,
            (_, __) => { }
        );

        lastChest = state;
        hasItems = lastChest != null && lastChest.slots != null && lastChest.slots.Any(s => !string.IsNullOrEmpty(s.itemType));
        if (hasItemsIndicator != null) hasItemsIndicator.SetActive(hasItems);
        UpdatePanel();

        if (chestUI != null)
            chestUI.Open(lastChest, slot => StartCoroutine(ClaimSlot(slot)));

        actionInFlight = false;
    }

    private IEnumerator ClaimSlot(int slotIndex)
    {
        if (backend == null) yield break;
        if (actionInFlight) yield break;
        actionInFlight = true;

        yield return backend.ClaimChest(slotIndex,
            resp =>
            {
                if (resp != null)
                {
                    AddItemToInventory(resp.itemType, resp.itemData);
                    if (lastChest != null)
                    {
                        var targetSlot = lastChest.slots.FirstOrDefault(s => s.slotIndex == slotIndex);
                        if (targetSlot != null)
                        {
                            targetSlot.itemType = null;
                            targetSlot.itemData = null;
                            targetSlot.itemDataRaw = null;
                        }
                    }
                }
                hasItems = lastChest != null && lastChest.slots.Any(s => !string.IsNullOrEmpty(s.itemType));
                if (hasItemsIndicator != null) hasItemsIndicator.SetActive(hasItems);
                UpdatePanel();
                if (chestUI != null) chestUI.Open(lastChest, slot => StartCoroutine(ClaimSlot(slot)));
                actionInFlight = false;
            },
            (code, _) =>
            {
                if (code == 404)
                    ShowTemp(emptyText);
                actionInFlight = false;
            }
        );
    }

    private IEnumerator DepositCurrent(InventoryItem current)
    {
        if (backend == null) yield break;
        actionInFlight = true;

        string itemType = current.Type == Item.Egg ? "egg" : "animal";
        ChestItemData data = BuildItemData(current);
        if (data == null)
        {
            actionInFlight = false;
            yield break;
        }

        yield return backend.DepositToChest(targetFriendCode, itemType, data,
            _ =>
            {
                G.Inventory.Remove(current);
                Destroy(current.gameObject);
                actionInFlight = false;
                UpdatePanel();
            },
            (code, _) =>
            {
                if (code == 409)
                    ShowTemp(noSpaceText);
                else if (code == 403)
                    ShowTemp(offlineText);
                actionInFlight = false;
            }
        );
    }

    private ChestItemData BuildItemData(InventoryItem item)
    {
        if (item == null) return null;
        if (item.Type == Item.Egg && item.TryGetComponent(out Egg egg))
        {
            return new ChestItemData
            {
                id = egg.Name,
                dinamic = egg.Data.DinamicData
            };
        }
        if (item.Type == Item.Brainrot && item.TryGetComponent(out Brainrot pet))
        {
            return new ChestItemData
            {
                id = pet.Name,
                dinamic = pet.DinamicData
            };
        }
        return null;
    }

    private void AddItemToInventory(string itemType, ChestItemData data)
    {
        if (data == null) return;
        if (G.Storage == null)
        {
            Debug.LogWarning("[GiftChest] ItemPrefabStorage is not initialized.");
            return;
        }

        if (itemType == "egg")
        {
            var prefab = G.Storage.GetEgg(data.id);
            if (prefab == null)
            {
                Debug.LogWarning($"[GiftChest] Egg prefab not found: {data.id}");
                return;
            }
            var egg = Instantiate(prefab);
            egg.SetData(data.dinamic);
            G.Inventory.Add(egg);
            return;
        }

        if (itemType == "animal")
        {
            var prefab = G.Storage.GetPet(data.id);
            if (prefab == null)
            {
                Debug.LogWarning($"[GiftChest] Brainrot prefab not found: {data.id}");
                return;
            }
            var pet = Instantiate(prefab);
            pet.Init(data.dinamic);
            G.Inventory.Add(pet);
        }
    }

    private void ShowTemp(string text, float seconds = 1.5f)
    {
        if (interactionPanel == null) return;
        if (tempRoutine != null)
        {
            StopCoroutine(tempRoutine);
            tempRoutine = null;
        }
        tempRoutine = StartCoroutine(TempMessage(text, seconds));
    }

    private IEnumerator TempMessage(string text, float seconds)
    {
        interactionPanel.gameObject.SetActive(true);
        interactionPanel.SetInfo(text);
        yield return new WaitForSeconds(seconds);
        UpdatePanel();
    }
}
