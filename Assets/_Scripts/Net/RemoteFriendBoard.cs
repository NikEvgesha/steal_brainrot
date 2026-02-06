using System.Collections;
using TMPro;
using UnityEngine;

public class RemoteFriendBoard : MonoBehaviour
{
    [SerializeField] private InteractionPanel interactionPanel;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private string friendLabel = "Друг";
    [SerializeField] private string addLabel = "Добавить в друзья";
    [SerializeField] private string sentLabel = "Запрос отправлен";
    [SerializeField] private string giftLabel = "Подарить";
    [SerializeField] private string offlineLabel = "Оффлайн";

    private FriendsApi api;
    private bool playerInArea;
    private bool isFriend;
    private bool isOnline;
    private string friendCode;
    private string displayName;
    private string playerId;
    private bool requestInFlight;

    private void Awake()
    {
        if (api == null) api = G.Backend.FriendsApi;
        if (interactionPanel == null) interactionPanel = GetComponentInChildren<InteractionPanel>(true);
    }

    private void OnEnable()
    {
        if (interactionPanel != null)
            interactionPanel.InteractionComplete.AddListener(OnInteract);
    }

    private void OnDisable()
    {
        if (interactionPanel != null)
            interactionPanel.InteractionComplete.RemoveListener(OnInteract);
    }

    public void SetRemote(string code, string name, bool friend, bool online)
    {
        playerId = null;
        SetRemoteInternal(code, name, friend, online);
    }

    public void SetRemoteWithId(string pid, string code, string name, bool friend, bool online)
    {
        playerId = pid;
        SetRemoteInternal(code, name, friend, online);
    }

    private void SetRemoteInternal(string code, string name, bool friend, bool online)
    {
        friendCode = code;
        displayName = name;
        isFriend = friend;
        isOnline = online;

        if (nameText != null)
            nameText.text = string.IsNullOrEmpty(displayName) ? "Player" : displayName;

        gameObject.SetActive(!string.IsNullOrEmpty(friendCode) || !string.IsNullOrEmpty(displayName));
        UpdatePanel();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInArea = true;
        UpdatePanel();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInArea = false;
        if (interactionPanel != null) interactionPanel.gameObject.SetActive(false);
    }

    private void UpdatePanel()
    {
        if (interactionPanel == null) return;
        if (!playerInArea)
        {
            interactionPanel.gameObject.SetActive(false);
            return;
        }

        if (statusText != null)
        {
            var baseText = CanGiftFromHand() ? giftLabel : (isFriend ? friendLabel : addLabel);
            if (!isOnline)
                baseText = $"{baseText} • {offlineLabel}";
            statusText.text = baseText;
        }

        var canGift = CanGiftFromHand();
        interactionPanel.gameObject.SetActive(isOnline && (canGift || !isFriend));
        if (isOnline)
            interactionPanel.SetInfo(canGift ? giftLabel : addLabel);
    }

    private void OnInteract()
    {
        if (requestInFlight) return;
        if (!isOnline) return;

        if (CanGiftFromHand())
        {
            if (string.IsNullOrWhiteSpace(playerId)) return;
            StartCoroutine(SendGift());
            return;
        }

        if (isFriend) return;
        if (string.IsNullOrWhiteSpace(friendCode)) return;
        StartCoroutine(SendRequest());
    }

    private IEnumerator SendRequest()
    {
        requestInFlight = true;
        bool ok = false;
        yield return api.SendFriendRequest(friendCode, success => ok = success);
        if (ok)
        {
            if (statusText != null) statusText.text = sentLabel;
            if (interactionPanel != null) interactionPanel.gameObject.SetActive(false);
        }
        requestInFlight = false;
    }

    private IEnumerator SendGift()
    {
        requestInFlight = true;
        bool ok = false;

        var current = G.QuickAccess != null ? G.QuickAccess.CurrentActive : null;
        if (current == null)
        {
            requestInFlight = false;
            yield break;
        }

        var itemType = current.Type == Item.Egg ? "egg" :
            current.Type == Item.Brainrot ? "brainrot" : null;
        if (string.IsNullOrEmpty(itemType))
        {
            requestInFlight = false;
            yield break;
        }

        yield return LobbyClient.Instance.SendGift(playerId, itemType, current.Name, success => ok = success);
        if (ok)
        {
            G.Inventory?.Remove(current);
            Destroy(current.gameObject);
            if (statusText != null) statusText.text = giftLabel;
        }
        requestInFlight = false;
    }

    private bool CanGiftFromHand()
    {
        var current = G.QuickAccess != null ? G.QuickAccess.CurrentActive : null;
        if (current == null) return false;
        return current.Type == Item.Egg || current.Type == Item.Brainrot;
    }
}
