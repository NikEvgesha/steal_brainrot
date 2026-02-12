using System.Collections;
using TMPro;
using UnityEngine;

public class RemoteFriendBoard : MonoBehaviour
{
    [SerializeField] private InteractionPanel interactionPanel;
    [SerializeField] private InteractionRaycastListener raycastListener;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private float interactionMaxDistance = 2.5f;
    [SerializeField] private bool allowTriggerFallback = false;
    [SerializeField] private string friendLabel = "Friend";
    [SerializeField] private string addLabel = "Add Friend";
    [SerializeField] private string sentLabel = "Request Sent";
    [SerializeField] private string giftLabel = "Gift";
    [SerializeField] private string offlineLabel = "Offline";

    private FriendsApi api;
    private InteractionRaycastListener _boundRaycastListener;
    private bool _isFocused;
    private bool _isInTriggerArea;
    private bool _hasRemoteData;
    private bool isFriend;
    private bool isOnline;
    private string friendCode;
    private string displayName;
    private string playerId;
    private bool requestInFlight;

    public InteractionPanel InteractionPanel => interactionPanel;

    private void Awake()
    {
        if (api == null) api = G.Backend.FriendsApi;
        if (interactionPanel == null) interactionPanel = GetComponentInChildren<InteractionPanel>(true);
        if (raycastListener == null) raycastListener = GetComponent<InteractionRaycastListener>();
    }

    private void OnEnable()
    {
        SubscribeInteraction();
        SubscribeHand();
        BindRaycastListener(raycastListener, interactionMaxDistance);
        UpdatePanel();
    }

    private void OnDisable()
    {
        UnsubscribeInteraction();
        UnsubscribeHand();
        BindRaycastListener(null);
        HidePanel();
    }

    private void OnDestroy()
    {
        BindRaycastListener(null);
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
        _hasRemoteData = !string.IsNullOrEmpty(playerId) || !string.IsNullOrEmpty(friendCode) || !string.IsNullOrEmpty(displayName);

        if (nameText != null)
            nameText.text = string.IsNullOrEmpty(displayName) ? "Player" : displayName;

        UpdatePanel();
    }

    public void SetInteractionPanel(InteractionPanel panel)
    {
        if (interactionPanel == panel)
            return;

        UnsubscribeInteraction();
        interactionPanel = panel;
        SubscribeInteraction();
        UpdatePanel();
    }

    public void BindRaycastListener(InteractionRaycastListener listener, float maxDistance = -1f)
    {
        if (maxDistance > 0f)
            interactionMaxDistance = maxDistance;

        if (_boundRaycastListener != null)
        {
            _boundRaycastListener._hitEvent.RemoveListener(OnRaycastHit);
            _boundRaycastListener._noHitEvent.RemoveListener(OnRaycastFail);
        }

        _boundRaycastListener = listener;
        raycastListener = listener;

        if (_boundRaycastListener == null)
            return;

        _boundRaycastListener.MaxDistance = Mathf.Max(0.1f, interactionMaxDistance);
        _boundRaycastListener._hitEvent.AddListener(OnRaycastHit);
        _boundRaycastListener._noHitEvent.AddListener(OnRaycastFail);
    }

    public void OnRaycastHit()
    {
        _isFocused = true;
        UpdatePanel();
    }

    public void OnRaycastFail()
    {
        _isFocused = false;
        if (_isInTriggerArea)
        {
            UpdatePanel();
            return;
        }

        HidePanel();
    }

    // Compatibility with existing interaction event names.
    public void _OnPlayerEnter()
    {
        OnRaycastHit();
    }

    // Compatibility with existing interaction event names.
    public void _OnPlayerExit()
    {
        OnRaycastFail();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!allowTriggerFallback) return;
        if (!other.CompareTag("Player")) return;
        _isInTriggerArea = true;
        UpdatePanel();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!allowTriggerFallback) return;
        if (!other.CompareTag("Player")) return;
        _isInTriggerArea = false;
        if (!_isFocused)
            HidePanel();
    }

    private void OnHandChanged(InventoryItem _)
    {
        UpdatePanel();
    }

    private void UpdatePanel()
    {
        if (interactionPanel == null)
            return;

        if (!_hasRemoteData || !HasFocus())
        {
            HidePanel();
            return;
        }

        var canGift = CanGiftFromHand();
        var canInteract = !requestInFlight && isOnline && (canGift || !isFriend);

        if (statusText != null)
        {
            var baseText = canGift ? giftLabel : (isFriend ? friendLabel : addLabel);
            if (!isOnline)
                baseText = $"{baseText} - {offlineLabel}";
            statusText.text = baseText;
        }

        interactionPanel.gameObject.SetActive(canInteract);
        if (canInteract)
            interactionPanel.SetInfo(canGift ? giftLabel : addLabel);
    }

    private void OnInteract()
    {
        if (requestInFlight) return;
        if (!isOnline) return;

        if (CanGiftFromHand())
        {
            if (string.IsNullOrWhiteSpace(playerId)) return;
            if (LobbyClient.Instance == null) return;
            StartCoroutine(SendGift());
            return;
        }

        if (isFriend) return;
        if (string.IsNullOrWhiteSpace(friendCode)) return;
        if (api == null) return;

        StartCoroutine(SendRequest());
    }

    private IEnumerator SendRequest()
    {
        requestInFlight = true;
        UpdatePanel();

        bool ok = false;
        yield return api.SendFriendRequest(friendCode, success => ok = success);
        if (ok)
        {
            if (statusText != null) statusText.text = sentLabel;
            HidePanel();
        }

        requestInFlight = false;
        UpdatePanel();
    }

    private IEnumerator SendGift()
    {
        requestInFlight = true;
        UpdatePanel();

        bool ok = false;

        var current = G.QuickAccess != null ? G.QuickAccess.CurrentActive : null;
        if (current == null)
        {
            requestInFlight = false;
            UpdatePanel();
            yield break;
        }

        var itemType = current.Type == Item.Egg ? "egg" :
            current.Type == Item.Brainrot ? "brainrot" : null;
        if (string.IsNullOrEmpty(itemType))
        {
            requestInFlight = false;
            UpdatePanel();
            yield break;
        }

        yield return LobbyClient.Instance.SendGift(playerId, itemType, current.Name, success => ok = success);
        if (ok)
        {
            G.Inventory?.Remove(current);
            if (current != null && current.gameObject != null)
                Destroy(current.gameObject);
            if (statusText != null) statusText.text = giftLabel;
        }

        requestInFlight = false;
        UpdatePanel();
    }

    private bool CanGiftFromHand()
    {
        var current = G.QuickAccess != null ? G.QuickAccess.CurrentActive : null;
        if (current == null) return false;
        return current.Type == Item.Egg || current.Type == Item.Brainrot;
    }

    private bool HasFocus()
    {
        return _isFocused || _isInTriggerArea;
    }

    private void SubscribeInteraction()
    {
        if (interactionPanel == null)
            return;

        interactionPanel.InteractionComplete.AddListener(OnInteract);
    }

    private void UnsubscribeInteraction()
    {
        if (interactionPanel == null)
            return;

        interactionPanel.InteractionComplete.RemoveListener(OnInteract);
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

    private void HidePanel()
    {
        if (interactionPanel != null)
            interactionPanel.gameObject.SetActive(false);
    }
}

