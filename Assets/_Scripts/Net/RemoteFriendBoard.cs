using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
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
            var baseText = action == BoardAction.None ? friendLabel : actionLabel;
            if (!isOnline && action != BoardAction.ShowStats)
                baseText = $"{baseText} - {offlineLabel}";
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
            BoardAction.AddFriend => addLabel,
            BoardAction.Gift => L(giftLocalizationKey, giftLabel),
            BoardAction.ShowStats => statsLabel,
            _ => friendLabel
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
            if (statusText != null) statusText.text = sentLabel;
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
            if (statusText != null) statusText.text = giftLabel;
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

public sealed class RemoteProfilePopup : MonoBehaviour
{
    private static RemoteProfilePopup _instance;
    private const float ExtraLikeNoticeSeconds = 2.5f;

    private Canvas _canvas;
    private GameObject _panel;
    private Text _title;
    private Text _body;
    private Text _likes;
    private Text _notice;
    private Button _closeButton;
    private Button _likeButton;
    private Text _likeButtonLabel;
    private Coroutine _noticeRoutine;
    private Coroutine _likeStateRoutine;
    private Coroutine _sendLikeRoutine;
    private FriendsApi _api;
    private string _targetPlayerId;
    private string _targetFriendCode;
    private bool _likedToday;
    private bool _likeRequestInFlight;
    private int _likesCount;

    public static RemoteProfilePopup Instance
    {
        get
        {
            if (_instance != null)
                return _instance;

            var go = new GameObject("RemoteProfilePopup");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<RemoteProfilePopup>();
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        Hide();
    }

    private void OnDestroy()
    {
        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(Hide);
        if (_likeButton != null)
            _likeButton.onClick.RemoveListener(OnLikePressed);
    }

    public void Show(string displayName, PlayerPublicStatsDto stats, string targetPlayerId = null, string targetFriendCode = null)
    {
        if (_panel == null)
            BuildUI();

        var playerName = string.IsNullOrWhiteSpace(displayName) ? L("UI/Common/Player", "Player") : displayName;
        var safeStats = stats ?? new PlayerPublicStatsDto();
        _targetPlayerId = string.IsNullOrWhiteSpace(targetPlayerId) ? null : targetPlayerId;
        _targetFriendCode = string.IsNullOrWhiteSpace(targetFriendCode) ? null : targetFriendCode;
        _likedToday = false;
        _likesCount = 0;
        _likeRequestInFlight = false;

        if (_noticeRoutine != null)
        {
            StopCoroutine(_noticeRoutine);
            _noticeRoutine = null;
        }
        if (_sendLikeRoutine != null)
        {
            StopCoroutine(_sendLikeRoutine);
            _sendLikeRoutine = null;
        }
        if (_likeStateRoutine != null)
        {
            StopCoroutine(_likeStateRoutine);
            _likeStateRoutine = null;
        }

        if (_title != null)
            _title.text = playerName;

        if (_body != null)
        {
            _body.text =
                $"{L("UI/Profile/IncomeAllPets", "Income/sec (all pets)")}: {FormatValue(safeStats.petsIncomePerSec)}\n" +
                $"{L("UI/Profile/IncomeBestPet", "Best pet income/sec")}: {FormatValue(safeStats.bestPetIncomePerSec)}\n" +
                $"{L("UI/Profile/HatchedTotal", "Total hatched")}: {safeStats.totalHatched}\n" +
                $"{L("UI/Profile/IncomeBigPet", "Big pet income/sec")}: {FormatValue(safeStats.bigPetIncomePerSec)}";
        }

        UpdateLikeUi();

        if (HasLikeTarget())
        {
            _likeStateRoutine = StartCoroutine(LoadLikeState());
        }

        if (_panel != null)
            _panel.SetActive(true);
    }

    public void Hide()
    {
        if (_noticeRoutine != null)
        {
            StopCoroutine(_noticeRoutine);
            _noticeRoutine = null;
        }
        if (_sendLikeRoutine != null)
        {
            StopCoroutine(_sendLikeRoutine);
            _sendLikeRoutine = null;
        }
        if (_likeStateRoutine != null)
        {
            StopCoroutine(_likeStateRoutine);
            _likeStateRoutine = null;
        }
        _likeRequestInFlight = false;

        if (_notice != null)
            _notice.gameObject.SetActive(false);

        if (_panel != null)
            _panel.SetActive(false);
    }

    private void BuildUI()
    {
        if (_canvas != null)
            return;

        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<GraphicRaycaster>();

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        _panel = new GameObject("Panel");
        _panel.transform.SetParent(canvasGo.transform, false);

        var panelImage = _panel.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.82f);

        var panelRect = _panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.32f, 0.2f);
        panelRect.anchorMax = new Vector2(0.68f, 0.8f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        _title = CreateText("Title", _panel.transform, new Vector2(0.06f, 0.78f), new Vector2(0.82f, 0.95f), TextAnchor.MiddleLeft, 38);
        _body = CreateText("Body", _panel.transform, new Vector2(0.06f, 0.38f), new Vector2(0.94f, 0.74f), TextAnchor.UpperLeft, 28);
        _likes = CreateText("Likes", _panel.transform, new Vector2(0.06f, 0.24f), new Vector2(0.6f, 0.34f), TextAnchor.MiddleLeft, 30);
        _notice = CreateText("Notice", _panel.transform, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.16f), TextAnchor.MiddleCenter, 24);
        _notice.color = new Color(1f, 0.92f, 0.48f, 1f);
        _notice.gameObject.SetActive(false);

        _likeButton = CreateButton("LikeButton", _panel.transform, L("UI/Profile/LikeButton", "Like"), new Vector2(0.64f, 0.22f), new Vector2(0.94f, 0.36f));
        _likeButtonLabel = _likeButton.GetComponentInChildren<Text>(true);
        _likeButton.onClick.AddListener(OnLikePressed);

        _closeButton = CreateButton("CloseButton", _panel.transform, "X", new Vector2(0.84f, 0.82f), new Vector2(0.95f, 0.95f));
        _closeButton.onClick.AddListener(Hide);
    }

    private static Text CreateText(string name, Transform parent, Vector2 min, Vector2 max, TextAnchor anchor, int fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.color = Color.white;
        text.alignment = anchor;
        text.fontSize = fontSize;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 12;
        text.resizeTextMaxSize = fontSize;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var rect = text.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string label, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.9f);
        var button = go.AddComponent<Button>();

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var labelText = CreateText("Label", go.transform, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, 34);
        labelText.text = label;
        labelText.color = Color.black;
        labelText.resizeTextMaxSize = 34;

        return button;
    }

    private static string FormatValue(double value)
    {
        return Math.Round(Math.Max(0d, value)).ToString("N0", CultureInfo.InvariantCulture);
    }

    private bool HasLikeTarget()
    {
        return !string.IsNullOrWhiteSpace(_targetPlayerId) || !string.IsNullOrWhiteSpace(_targetFriendCode);
    }

    private FriendsApi ResolveApi()
    {
        if (_api != null)
            return _api;

        if (G.Backend != null && G.Backend.FriendsApi != null)
        {
            _api = G.Backend.FriendsApi;
            return _api;
        }

        _api = FindAnyObjectByType<FriendsApi>();
        return _api;
    }

    private IEnumerator LoadLikeState()
    {
        var api = ResolveApi();
        if (api == null)
        {
            UpdateLikeUi();
            yield break;
        }

        var hasResponse = false;
        FriendsApi.LikeStateResponse state = null;
        yield return api.GetLikeState(
            _targetPlayerId,
            _targetFriendCode,
            onOk: resp =>
            {
                hasResponse = true;
                state = resp;
            });

        if (!hasResponse || state == null)
        {
            UpdateLikeUi();
            yield break;
        }

        _likesCount = Mathf.Max(0, state.likesCount);
        _likedToday = state.likedToday || !state.canLike;
        UpdateLikeUi();
    }

    private void OnLikePressed()
    {
        if (!HasLikeTarget())
            return;

        if (_likeRequestInFlight)
            return;

        if (_likedToday)
        {
            ShowNotice(BuildAlreadyLikedText(null), ExtraLikeNoticeSeconds);
            return;
        }

        var api = ResolveApi();
        if (api == null)
        {
            ShowNotice(L("UI/Profile/LikeUnavailable", "Like is temporarily unavailable"), ExtraLikeNoticeSeconds);
            return;
        }

        if (_sendLikeRoutine != null)
        {
            StopCoroutine(_sendLikeRoutine);
            _sendLikeRoutine = null;
        }

        _sendLikeRoutine = StartCoroutine(SendLikeRoutine(api));
    }

    private IEnumerator SendLikeRoutine(FriendsApi api)
    {
        _likeRequestInFlight = true;
        UpdateLikeUi();

        var hasResponse = false;
        FriendsApi.LikeSendResponse response = null;
        long errCode = 0;
        string errText = null;

        yield return api.SendLike(
            _targetPlayerId,
            _targetFriendCode,
            onOk: resp =>
            {
                hasResponse = true;
                response = resp;
            },
            onErr: (code, text) =>
            {
                errCode = code;
                errText = text;
            });

        _likeRequestInFlight = false;

        if (!hasResponse || response == null)
        {
            Debug.LogWarning($"[RemoteProfilePopup] Like request failed: {errCode} {errText}");
            ShowNotice(L("UI/Profile/LikeUnavailable", "Like is temporarily unavailable"), ExtraLikeNoticeSeconds);
            UpdateLikeUi();
            yield break;
        }

        _likesCount = Mathf.Max(0, response.likesCount);
        _likedToday = response.likedToday || !response.canLike || response.ok;
        UpdateLikeUi();

        if (!response.ok)
            ShowNotice(BuildAlreadyLikedText(response.nextLikeAtUtc), ExtraLikeNoticeSeconds);
    }

    private void UpdateLikeUi()
    {
        var hasTarget = HasLikeTarget();

        if (_likes != null)
        {
            _likes.gameObject.SetActive(hasTarget);
            if (hasTarget)
                _likes.text = $"{L("UI/Profile/Likes", "Likes")}: {_likesCount}";
        }

        if (_likeButton != null)
        {
            _likeButton.gameObject.SetActive(hasTarget);
            _likeButton.interactable = hasTarget && !_likeRequestInFlight && !_likedToday;
        }

        if (_likeButtonLabel != null)
        {
            if (!hasTarget)
            {
                _likeButtonLabel.text = string.Empty;
            }
            else if (_likeRequestInFlight)
            {
                _likeButtonLabel.text = L("UI/Common/Loading", "Loading...");
            }
            else if (_likedToday)
            {
                _likeButtonLabel.text = L("UI/Profile/LikedToday", "Liked today");
            }
            else
            {
                _likeButtonLabel.text = L("UI/Profile/LikeButton", "Like");
            }
        }

        if (!hasTarget && _notice != null)
            _notice.gameObject.SetActive(false);
    }

    private void ShowNotice(string text, float seconds)
    {
        if (_notice == null)
            return;

        _notice.text = text ?? string.Empty;
        _notice.gameObject.SetActive(true);

        if (_noticeRoutine != null)
        {
            StopCoroutine(_noticeRoutine);
            _noticeRoutine = null;
        }

        _noticeRoutine = StartCoroutine(HideNoticeAfter(seconds));
    }

    private IEnumerator HideNoticeAfter(float seconds)
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, seconds));
        if (_notice != null)
            _notice.gameObject.SetActive(false);
        _noticeRoutine = null;
    }

    private string BuildAlreadyLikedText(string nextLikeAtUtc)
    {
        var baseText = L("UI/Profile/LikeAlreadyToday", "You already liked this player today");
        if (!TryParseUtc(nextLikeAtUtc, out var nextUtc))
            return baseText;

        var remaining = nextUtc - DateTime.UtcNow;
        if (remaining.TotalSeconds <= 0)
            return baseText;

        var hours = Mathf.Max(0, Mathf.FloorToInt((float)remaining.TotalHours));
        var minutes = Mathf.Max(0, remaining.Minutes);
        return $"{baseText} ({hours:00}:{minutes:00})";
    }

    private static bool TryParseUtc(string value, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dto))
        {
            utc = dto.UtcDateTime;
            return true;
        }

        return false;
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
