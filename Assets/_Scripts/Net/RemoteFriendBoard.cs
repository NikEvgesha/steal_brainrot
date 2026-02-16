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

        var action = ResolveAction();
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
        var action = ResolveAction();
        switch (action)
        {
            case BoardAction.Gift:
                if (!isOnline) return;
                if (string.IsNullOrWhiteSpace(playerId)) return;
                if (LobbyClient.Instance == null) return;
                StartCoroutine(SendGift());
                break;
            case BoardAction.AddFriend:
                if (!isOnline) return;
                if (string.IsNullOrWhiteSpace(friendCode)) return;
                if (api == null) return;
                StartCoroutine(SendRequest());
                break;
            case BoardAction.ShowStats:
                RemoteProfilePopup.Instance.Show(displayName, stats);
                break;
        }
    }

    private BoardAction ResolveAction()
    {
        if (requestInFlight)
            return BoardAction.None;

        if (CanGiftFromHand())
            return isOnline ? BoardAction.Gift : BoardAction.None;

        if (!isFriend)
            return isOnline ? BoardAction.AddFriend : BoardAction.None;

        if (showStatsForFriends)
            return BoardAction.ShowStats;

        return BoardAction.None;
    }

    private string GetActionLabel(BoardAction action)
    {
        return action switch
        {
            BoardAction.AddFriend => addLabel,
            BoardAction.Gift => giftLabel,
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
            current.Type == Item.Brainrot ? "brainrot" :
            current.Type == Item.Food ? "food" : null;
        if (string.IsNullOrEmpty(itemType))
        {
            requestInFlight = false;
            UpdatePanel();
            yield break;
        }

        var payloadId = BuildGiftItemPayload(current, itemType);
        yield return LobbyClient.Instance.SendGift(playerId, itemType, payloadId, success => ok = success);
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
        return current.Type == Item.Egg || current.Type == Item.Brainrot || current.Type == Item.Food;
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

public sealed class RemoteProfilePopup : MonoBehaviour
{
    private static RemoteProfilePopup _instance;

    private Canvas _canvas;
    private GameObject _panel;
    private Text _title;
    private Text _body;
    private Button _closeButton;

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

    public void Show(string displayName, PlayerPublicStatsDto stats)
    {
        if (_panel == null)
            BuildUI();

        var playerName = string.IsNullOrWhiteSpace(displayName) ? L("UI/Common/Player", "Player") : displayName;
        var safeStats = stats ?? new PlayerPublicStatsDto();

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

        if (_panel != null)
            _panel.SetActive(true);
    }

    public void Hide()
    {
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
        _body = CreateText("Body", _panel.transform, new Vector2(0.06f, 0.2f), new Vector2(0.94f, 0.74f), TextAnchor.UpperLeft, 28);

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

