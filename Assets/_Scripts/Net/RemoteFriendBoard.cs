using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using Newtonsoft.Json;

public class RemoteFriendBoard : MonoBehaviour
{
    private enum BoardAction
    {
        None,
        AddFriend,
        Gift,
        ShowStats
    }

    [SerializeField] private InteractionPanel interactionPanel;
    [SerializeField] private InteractionRaycastListener raycastListener;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text hatchedText;
    [SerializeField] private float interactionMaxDistance = 2.5f;
    [SerializeField] private bool allowTriggerFallback = false;
    [SerializeField] private bool showStatsForFriends = true;
    [SerializeField] private string friendLabel = "Friend";
    [SerializeField] private string addLabel = "Add Friend";
    [SerializeField] private string sentLabel = "Request Sent";
    [SerializeField] private string giftLabel = "Gift";
    [SerializeField] private string giftLocalizationKey = "UI/Friends/Gift";
    [SerializeField] private string statsLabel = "Stats";
    [SerializeField] private string offlineLabel = "Offline";
    [SerializeField] private string playerLabel = "Player";
    [SerializeField] private string hatchedFormat = "Hatched: {0}";

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
    private PlayerPublicStatsDto stats = new();
    private bool requestInFlight;
    private BoardAction _lockedAction = BoardAction.None;
    private InventoryItem _lockedGiftItem;

    public InteractionPanel InteractionPanel => interactionPanel;
    public bool HasRemoteData => _hasRemoteData;
    public bool IsOnline => isOnline;
    public string RemotePlayerId => playerId;
    public string RemoteFriendCode => friendCode;
    public string RemoteDisplayName => displayName;
    public PlayerPublicStatsDto RemoteStats => stats;

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
        ClearInteractionLock();
        HidePanel();
    }

    private void OnDestroy()
    {
        BindRaycastListener(null);
    }

    public void SetRemote(string code, string name, bool friend, bool online, PlayerPublicStatsDto publicStats = null)
    {
        playerId = null;
        SetRemoteInternal(code, name, friend, online, publicStats);
    }

    public void SetRemoteWithId(string pid, string code, string name, bool friend, bool online, PlayerPublicStatsDto publicStats = null)
    {
        playerId = pid;
        SetRemoteInternal(code, name, friend, online, publicStats);
    }

    private void SetRemoteInternal(string code, string name, bool friend, bool online, PlayerPublicStatsDto publicStats)
    {
        friendCode = code;
        displayName = name;
        isFriend = friend;
        isOnline = online;
        stats = publicStats ?? new PlayerPublicStatsDto();
        _hasRemoteData = !string.IsNullOrEmpty(playerId) || !string.IsNullOrEmpty(friendCode) || !string.IsNullOrEmpty(displayName);

        var resolvedName = string.IsNullOrEmpty(displayName) ? playerLabel : displayName;
        var hatchedLine = string.Format(hatchedFormat, Mathf.Max(0, stats.totalHatched));
        if (nameText != null)
            nameText.text = hatchedText != null ? resolvedName : $"{resolvedName}\n{hatchedLine}";
        if (hatchedText != null)
            hatchedText.text = hatchedLine;

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
            UnbindListener(_boundRaycastListener);

        _boundRaycastListener = listener;
        raycastListener = listener;

        if (_boundRaycastListener == null)
            return;

        EnsureListenerEvents(_boundRaycastListener);
        _boundRaycastListener.MaxDistance = Mathf.Max(0.1f, interactionMaxDistance);
        _boundRaycastListener._hitEvent.AddListener(OnRaycastHit);
        _boundRaycastListener._noHitEvent.AddListener(OnRaycastFail);
    }

    private void UnbindListener(InteractionRaycastListener listener)
    {
        if (listener == null)
            return;

        listener._hitEvent?.RemoveListener(OnRaycastHit);
        listener._noHitEvent?.RemoveListener(OnRaycastFail);
    }

    private static void EnsureListenerEvents(InteractionRaycastListener listener)
    {
        if (listener == null)
            return;

        listener._hitEvent ??= new UnityEvent();
        listener._noHitEvent ??= new UnityEvent();
    }

    public void OnRaycastHit()
    {
        _isFocused = true;
        UpdatePanel();
    }

    public void OnRaycastFail()
    {
        _isFocused = false;
        if (IsPanelInteracting())
            return;

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
        if (!_isFocused && !IsPanelInteracting())
            HidePanel();
    }

    private void OnHandChanged(InventoryItem _)
    {
        if (IsPanelInteracting())
            return;

        UpdatePanel();
    }

    private void UpdatePanel()
    {
        if (interactionPanel == null)
            return;

        var panelInteracting = IsPanelInteracting();
        if (!_hasRemoteData)
        {
            if (!panelInteracting)
            {
                HidePanel();
                return;
            }
        }

        if (!HasFocus() && !panelInteracting)
        {
            HidePanel();
            return;
        }

        var action = panelInteracting && _lockedAction != BoardAction.None
            ? _lockedAction
            : ResolveAction();
        var canInteract = action != BoardAction.None;
        var actionLabel = GetActionLabel(action);

        if (statusText != null)
        {
            var baseText = action == BoardAction.None ? L("UI/Friends/Friend", friendLabel) : actionLabel;
            if (!isOnline && action != BoardAction.ShowStats)
                baseText = $"{baseText} - {L("UI/Friends/StatusOffline", offlineLabel)}";
            statusText.text = baseText;
        }

        interactionPanel.gameObject.SetActive(canInteract);
        if (canInteract)
            interactionPanel.SetInfo(actionLabel);
    }

    private void OnInteract()
    {
        if (requestInFlight) return;

        var action = ConsumeLockedAction(out var capturedGiftItem);
        if (action == BoardAction.None)
            action = ResolveAction();

        switch (action)
        {
            case BoardAction.Gift:
                if (!isOnline) return;
                if (string.IsNullOrWhiteSpace(playerId) && string.IsNullOrWhiteSpace(friendCode)) return;
                if (LobbyClient.Instance == null) return;
                StartCoroutine(SendGift(capturedGiftItem));
                break;
            case BoardAction.AddFriend:
                if (!isOnline) return;
                if (string.IsNullOrWhiteSpace(friendCode)) return;
                if (api == null) return;
                StartCoroutine(SendRequest());
                break;
            case BoardAction.ShowStats:
                RemoteProfilePopup.Instance.Show(displayName, stats, playerId, friendCode);
                break;
        }
    }

    private BoardAction ResolveAction()
    {
        if (requestInFlight)
            return BoardAction.None;

        if (CanGiftFromHand() && HasGiftTarget())
            return BoardAction.Gift;

        if (!isFriend && isOnline && !string.IsNullOrWhiteSpace(friendCode))
            return BoardAction.AddFriend;

        if (showStatsForFriends)
            return BoardAction.ShowStats;

        return BoardAction.None;
    }

    private string GetActionLabel(BoardAction action)
    {
        return action switch
        {
            BoardAction.AddFriend => L("UI/Friends/AddFriend", addLabel),
            BoardAction.Gift => L(giftLocalizationKey, giftLabel),
            BoardAction.ShowStats => L("UI/Profile/Stats", statsLabel),
            _ => L("UI/Friends/Friend", friendLabel)
        };
    }

    private IEnumerator SendRequest()
    {
        requestInFlight = true;
        UpdatePanel();

        bool ok = false;
        yield return api.SendFriendRequest(friendCode, success => ok = success);
        if (ok)
        {
            if (statusText != null) statusText.text = L("UI/Friends/RequestSent", sentLabel);
            FriendsPanelController.RequestLiveRefresh();
            HidePanel();
        }

        requestInFlight = false;
        UpdatePanel();
    }

    private IEnumerator SendGift(InventoryItem capturedItem)
    {
        requestInFlight = true;
        UpdatePanel();

        bool ok = false;

        var current = capturedItem != null ? capturedItem : GetCurrentGiftCandidate();
        if (current == null)
        {
            requestInFlight = false;
            UpdatePanel();
            yield break;
        }

        if (!TryResolveGiftItemType(current, out var itemType))
        {
            requestInFlight = false;
            UpdatePanel();
            yield break;
        }

        var targetPlayerId = string.IsNullOrWhiteSpace(playerId) ? null : playerId;
        var targetFriendCode = string.IsNullOrWhiteSpace(friendCode) ? null : friendCode;
        if (string.IsNullOrEmpty(targetPlayerId) && string.IsNullOrEmpty(targetFriendCode))
        {
            requestInFlight = false;
            UpdatePanel();
            yield break;
        }

        var payloadId = BuildGiftItemPayload(current, itemType);
        yield return LobbyClient.Instance.SendGift(targetPlayerId, itemType, payloadId, success => ok = success, targetFriendCode);
        if (ok)
        {
            G.Inventory?.Remove(current);
            if (current != null && current.gameObject != null)
                Destroy(current.gameObject);
            if (statusText != null) statusText.text = L(giftLocalizationKey, giftLabel);
        }

        requestInFlight = false;
        UpdatePanel();
    }

    private bool HasGiftTarget()
    {
        if (!isOnline)
            return false;

        return !string.IsNullOrWhiteSpace(playerId) || !string.IsNullOrWhiteSpace(friendCode);
    }

    private bool CanGiftFromHand()
    {
        return TryResolveGiftItemType(GetCurrentGiftCandidate(), out _);
    }

    private static bool TryResolveGiftItemType(InventoryItem item, out string itemType)
    {
        itemType = null;
        if (item == null)
            return false;

        if (item.Type == Item.Egg)
        {
            itemType = "egg";
            return true;
        }

        if (item.Type == Item.Brainrot)
        {
            itemType = "brainrot";
            return true;
        }

        if (item.Type == Item.Food)
        {
            itemType = "food";
            return true;
        }

        return false;
    }

    private InventoryItem GetCurrentGiftCandidate()
    {
        return G.QuickAccess != null ? G.QuickAccess.CurrentActive : null;
    }

    private static string BuildGiftItemPayload(InventoryItem item, string itemType)
    {
        if (item == null)
            return string.Empty;

        var id = item.Name;
        if (string.IsNullOrWhiteSpace(id))
            return string.Empty;

        if (itemType == "brainrot")
        {
            var brainrot = item as Brainrot ?? item.GetComponent<Brainrot>();
            if (brainrot != null)
            {
                var payload = new GiftDynamicPayload
                {
                    id = id,
                    element = (int)brainrot.DinamicData.ElementType,
                    weight = brainrot.DinamicData.WeightMultiplier,
                    income = brainrot.DinamicData.ResultIncome
                };
                return EncodePayload(payload);
            }
        }
        else if (itemType == "egg")
        {
            var egg = item as Egg ?? item.GetComponent<Egg>();
            if (egg != null)
            {
                var payload = new GiftDynamicPayload
                {
                    id = id,
                    element = (int)egg.Data.DinamicData.ElementType,
                    weight = egg.Data.DinamicData.WeightMultiplier,
                    income = egg.Data.DinamicData.ResultIncome
                };
                return EncodePayload(payload);
            }
        }

        return id;
    }

    private static string EncodePayload(GiftDynamicPayload payload)
    {
        try
        {
            return "dyn:" + JsonConvert.SerializeObject(payload);
        }
        catch
        {
            return payload != null ? payload.id : string.Empty;
        }
    }

    [Serializable]
    private class GiftDynamicPayload
    {
        public string id;
        public int? element;
        public float? weight;
        public double? income;
    }

    private bool HasFocus()
    {
        return _isFocused || _isInTriggerArea;
    }

    private bool IsPanelInteracting()
    {
        return interactionPanel != null && interactionPanel.IsInteracting;
    }

    private void OnInteractionStarted()
    {
        if (requestInFlight)
        {
            ClearInteractionLock();
            return;
        }

        _lockedAction = ResolveAction();
        _lockedGiftItem = _lockedAction == BoardAction.Gift ? GetCurrentGiftCandidate() : null;
    }

    private BoardAction ConsumeLockedAction(out InventoryItem capturedGiftItem)
    {
        capturedGiftItem = _lockedGiftItem;
        var action = _lockedAction;
        ClearInteractionLock();
        return action;
    }

    private void ClearInteractionLock()
    {
        _lockedAction = BoardAction.None;
        _lockedGiftItem = null;
    }

    private void SubscribeInteraction()
    {
        if (interactionPanel == null)
            return;

        interactionPanel.InteractionStarted.AddListener(OnInteractionStarted);
        interactionPanel.InteractionComplete.AddListener(OnInteract);
    }

    private void UnsubscribeInteraction()
    {
        if (interactionPanel == null)
            return;

        interactionPanel.InteractionStarted.RemoveListener(OnInteractionStarted);
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
        if (IsPanelInteracting())
            return;

        if (interactionPanel != null)
            interactionPanel.gameObject.SetActive(false);
    }

    private static string L(string key, string fallback)
    {
        if (LocalizationManager.Instance != null && LocalizationManager.Instance.LocalizationData != null)
        {
            var translated = LocalizationManager.Instance.LocalizationData.GetTranslation(key);
            if (!string.IsNullOrWhiteSpace(translated) && !string.Equals(translated, key, StringComparison.Ordinal))
                return translated;
        }

        return fallback;
    }
}
