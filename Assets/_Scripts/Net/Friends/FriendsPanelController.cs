using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FriendsPanelController : MonoBehaviour
{
    private static FriendsPanelController _instance;

    [Header("Refs")]
    private ZooBackendClient backend;
    private SaveManager save;

    [Header("UI")]
    [SerializeField] private GameObject _ui;
    [SerializeField] private UniversalDecisionPopup yenOrNotPopup;

    [Header("Deps")]
    private FriendsApi api;
    [SerializeField] private RemoteBasesApplier remoteBases;
    [SerializeField] private int remoteSlotIndex = 0;

    [Header("Top")]
    [SerializeField] private TMP_Text myNameText;
    [SerializeField] private TMP_Text myCodeText;
    [SerializeField] private Button copyCodeButton;

    [Header("Rename")]
    [SerializeField] private TMP_InputField renameInput;
    [SerializeField] private Button renameButton;
    [SerializeField] private TMP_Text renameStatusText;

    [Header("Add friend")]
    [SerializeField] private TMP_InputField addCodeInput;
    [SerializeField] private Button addButton;
    [SerializeField] private TMP_Text addStatusText;

    [Header("List")]
    [SerializeField] private Transform listContent;
    [SerializeField] private FriendRowView rowPrefab;

    [Header("Requests")]
    [SerializeField] private Transform requestsContent;
    [SerializeField] private FriendRequestRowView requestRowPrefab;
    [SerializeField] private TMP_Text requestsStatusText;
    [SerializeField] private GameObject requestsBadge;

    [Header("Close")]
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _toggleButton;

    [Header("Availability")]
    [SerializeField, Min(0.1f)] private float serverStateRefreshInterval = 0.5f;

    private Coroutine _openFlow;
    private Coroutine _externalRefreshFlow;
    private Coroutine _availabilityFlow;
    private bool _isOpen;

    public static void RequestLiveRefresh()
    {
        if (_instance != null)
            _instance.TryScheduleExternalRefresh();
    }

    public static bool TryShowPopup(UniversalDecisionPopup.Request request)
    {
        if (_instance == null)
            return false;

        _instance.EnsureDecisionPopup();
        if (_instance.yenOrNotPopup == null)
            return false;
        if (_instance.yenOrNotPopup.IsOpen)
            return false;

        _instance.yenOrNotPopup.Show(request);
        return true;
    }

    public static bool IsPopupOpen()
    {
        if (_instance == null)
            return false;

        _instance.EnsureDecisionPopup();
        return _instance.yenOrNotPopup != null && _instance.yenOrNotPopup.IsOpen;
    }

    private void Awake()
    {
        _instance = this;
        if (backend == null) backend = G.Backend;
        if (save == null) save = G.Save;
        if (api == null) api = G.Backend.FriendsApi;
        EnsureRemoteBases();
        EnsureDecisionPopup();

        renameButton.onClick.AddListener(() => StartCoroutine(RenameFlow()));

        _closeButton.onClick.AddListener(() => ToggleOpen());
        _toggleButton.onClick.AddListener(() => ToggleOpen()) ;
        copyCodeButton.onClick.AddListener(CopyMyCode);
        addButton.onClick.AddListener(() => StartCoroutine(AddFriendFlow()));

        RefreshAvailability();
    }

    public void ToggleOpen()
    {
        //if (!(LoadingManager.Instance.CurrentLocation == Location.Lobby)) return;
        if (!_isOpen && !IsFriendsAvailable())
            return;

        _isOpen = !_isOpen;
        G.Control.CursorActive = _isOpen;
        _ui.SetActive(_isOpen);
        if (!_isOpen)
        {
            if (_openFlow != null) StopCoroutine(_openFlow);
            _openFlow = null;
            if (_externalRefreshFlow != null) StopCoroutine(_externalRefreshFlow);
            _externalRefreshFlow = null;
        }
        else
        {
            _openFlow = StartCoroutine(OpenFlow());
            G.Input.AOpenWindow?.Invoke(this);
        }

    }
    private void OnEnable()
    {
        G.Initialized.AddListener(OnGameInitialized);
        if (G.Input != null)
        {
            G.Input.AFriends += ToggleOpen;
            G.Input.AOpenWindow += Close;
        }

        RefreshAvailability();
        if (_availabilityFlow == null)
            _availabilityFlow = StartCoroutine(AvailabilityFlow());
    }
    private void OnDisable()
    {
        G.Initialized.RemoveListener(OnGameInitialized);
        if (G.Input != null)
        {
            G.Input.AFriends -= ToggleOpen;
            //LoadingManager.Instance.LocationChanged -= ToggleButtonVisibility;
            G.Input.AOpenWindow -= Close;
        }
        if (_externalRefreshFlow != null)
        {
            StopCoroutine(_externalRefreshFlow);
            _externalRefreshFlow = null;
        }
        if (_availabilityFlow != null)
        {
            StopCoroutine(_availabilityFlow);
            _availabilityFlow = null;
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
    private void OnGameInitialized()
    {
        EnsureRemoteBases();
        EnsureDecisionPopup();
        RefreshAvailability();
    }
    public void Close(MonoBehaviour ui)
    {
        if (ui != this && _isOpen)
            ToggleOpen();
    }
    IEnumerator OpenFlow()
    {
        addStatusText.text = "";
        renameStatusText.text = "";
        if (requestsStatusText != null) requestsStatusText.text = "";

        yield return api.EnsureGuest();

        var p = api.LocalProfile();
        myNameText.text = LocalizationUtils.Format("UI/Friends/NicknameFormat", "Nickname: {0}", p.displayName);
        myCodeText.text = LocalizationUtils.Format("UI/Friends/IdFormat", "ID: {0}", p.friendCode);

        // вЂњРѕРЅР»Р°Р№РЅвЂќ вЂ” РїРёРЅРіСѓРµРј РїСЂРё РѕС‚РєСЂС‹С‚РёРё Рё РїРѕС‚РѕРј РјРѕР¶РЅРѕ СЂР°Р· РІ 20 СЃРµРє РІ РѕС‚РґРµР»СЊРЅРѕРј РјРµСЃС‚Рµ
        yield return api.PresencePing();

        yield return RefreshFriends();
        yield return RefreshRequests();
    }

    IEnumerator RefreshFriends()
    {
        ClearList();

        List<FriendsApi.FriendItem> list = null;
        yield return api.GetFriends(items => list = items, (code, err) => {
        switch (code)
        {
            case 400:
                addStatusText.text = LocalizationUtils.T("UI/Friends/FailedLoadList", "Failed to load friends list");
                break;
            case 404:
                addStatusText.text = LocalizationUtils.T("UI/Friends/FailedLoadList", "Failed to load friends list");
                break;
            default:
                addStatusText.text = LocalizationUtils.T("UI/Friends/FailedLoadList", "Failed to load friends list");
                break;
        }
    });

        
            

        if (list == null) yield break;
        var lobbyOnlineCodes = CollectLobbyOnlineCodes();

        foreach (var f in list)
        {
            if (!string.IsNullOrEmpty(f.friendCode) && lobbyOnlineCodes.Contains(f.friendCode))
                f.isOnline = true;

            var row = Instantiate(rowPrefab, listContent);
            row.Bind(f,
                onRemove: () => StartCoroutine(RemoveFriendFlow(f.friendCode)),
                onView: () => StartCoroutine(ViewFriendBaseStub(f.friendCode))
            );
        }
    }

    private HashSet<string> CollectLobbyOnlineCodes()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (LobbyClient.Instance == null)
            return result;

        var members = LobbyClient.Instance.LastMembers;
        if (members == null)
            return result;

        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m == null || string.IsNullOrWhiteSpace(m.friendCode))
                continue;
            if (!string.IsNullOrWhiteSpace(m.playerId))
                result.Add(m.friendCode);
        }

        return result;
    }

    IEnumerator RefreshRequests()
    {
        if (requestsContent == null || requestRowPrefab == null) yield break;

        ClearRequests();

        List<FriendsApi.FriendRequestItem> list = null;
        yield return api.GetFriendRequests(items => list = items, (code, err) =>
        {
            if (requestsStatusText != null)
                requestsStatusText.text = LocalizationUtils.T("UI/Friends/FailedLoadRequests", "Failed to load requests");
        });

        if (list == null) yield break;

        if (requestsBadge != null)
            requestsBadge.SetActive(list.Count > 0);

        foreach (var r in list)
        {
            var row = Instantiate(requestRowPrefab, requestsContent);
            row.Bind(r,
                onAccept: () => StartCoroutine(AcceptRequestFlow(r.requestId)),
                onDecline: () => StartCoroutine(DeclineRequestFlow(r.requestId))
            );
        }
    }

    IEnumerator AddFriendFlow()
    {
        addStatusText.text = "";
        var code = (addCodeInput.text ?? "").Trim().ToUpperInvariant();
        if (code.Length == 0) yield break;

        bool ok = false;
        yield return api.AddFriend(code, success => ok = success);

        if (!ok)
        {
            addStatusText.text = LocalizationUtils.T("UI/Friends/FailedAddFriend", "Failed to add friend (check ID)");
            yield break;
        }

        addCodeInput.text = "";
        yield return RefreshFriends();
        yield return RefreshRequests();
    }

    IEnumerator RemoveFriendFlow(string friendCode)
    {
        bool ok = false;
        yield return api.RemoveFriend(friendCode, success => ok = success);
        if (ok) yield return RefreshFriends();
    }

    IEnumerator AcceptRequestFlow(string requestId)
    {
        bool ok = false;
        yield return api.AcceptFriendRequest(requestId, success => ok = success);
        if (ok)
        {
            yield return RefreshRequests();
            yield return RefreshFriends();
        }
    }

    IEnumerator DeclineRequestFlow(string requestId)
    {
        bool ok = false;
        yield return api.DeclineFriendRequest(requestId, success => ok = success);
        if (ok)
        {
            yield return RefreshRequests();
        }
    }

    IEnumerator RenameFlow()
    {
        renameStatusText.text = "";
        var name = (renameInput.text ?? "").Trim();
        if (name.Length < 3) { renameStatusText.text = LocalizationUtils.T("UI/Friends/MinNameLength", "Minimum 3 characters"); yield break; }

        bool ok = false;
        string fail = null;

        //yield return api.RenameMe(name, success => ok = success, msg => fail = msg);
        yield return api.RenameMePaid(name, success => ok = success, msg => fail = msg);

        if (!ok)
        {
            renameStatusText.text = fail ?? LocalizationUtils.T("UI/Friends/FailedRename", "Failed to rename");
            yield break;
        }

        renameInput.text = "";
        var p = api.LocalProfile();
        myNameText.text = LocalizationUtils.Format("UI/Friends/NicknameFormat", "Nickname: {0}", p.displayName);
        renameStatusText.text = LocalizationUtils.T("UI/Friends/NicknameUpdated", "Nickname updated");
    }

    IEnumerator ViewFriendBaseStub(string friendCode)
    {
        addStatusText.text = "";

        FriendBaseResponse resp = null;
        yield return backend.GetFriendBase(friendCode,
            ok => resp = ok,
            (code, err) => addStatusText.text = LocalizationUtils.T("UI/Friends/FailedLoadBase", "Failed to load friend's base"));

        if (resp == null)
            yield break;

        EnsureRemoteBases();
        if (remoteBases == null)
        {
            Debug.LogWarning("[Friends] RemoteBasesApplier not found in scene.");
        }
        else if (resp.data == null)
        {
            Debug.LogWarning($"[Friends] Friend base data is empty. raw={resp.dataRaw}");
        }
        else
        {
            remoteBases.ApplyFriendBase(resp.data, remoteSlotIndex);
            remoteBases.TeleportPlayerToSlot(remoteSlotIndex);
        }
        Debug.Log($"[Friends] Loaded base for {friendCode}");
    }

    private void EnsureRemoteBases()
    {
        if (remoteBases != null) return;
        var found = FindObjectsByType<RemoteBasesApplier>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (found != null && found.Length > 0)
            remoteBases = found[0];
    }

    private void EnsureDecisionPopup()
    {
        if (yenOrNotPopup != null)
            return;

        yenOrNotPopup = GetComponentInChildren<UniversalDecisionPopup>(true);
        if (yenOrNotPopup != null)
            return;

        // Popup can be placed as a sibling under GameCanvas, not only inside FriendsPanel.
        var canvas = GetComponentInParent<Canvas>(true);
        if (canvas != null)
            yenOrNotPopup = canvas.GetComponentInChildren<UniversalDecisionPopup>(true);

        if (yenOrNotPopup != null)
            return;

        Transform popupRoot = null;
        if (canvas != null)
            popupRoot = FindChildByNameRecursive(canvas.transform, "YenOrNot");
        if (popupRoot == null)
            popupRoot = FindChildByNameRecursive(transform, "YenOrNot");
        if (popupRoot == null)
        {
            var anyPopup = FindObjectsByType<UniversalDecisionPopup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (anyPopup != null && anyPopup.Length > 0)
                yenOrNotPopup = anyPopup[0];
        }

        if (popupRoot == null || yenOrNotPopup != null)
            return;

        yenOrNotPopup = popupRoot.GetComponent<UniversalDecisionPopup>();
        if (yenOrNotPopup == null)
            yenOrNotPopup = popupRoot.gameObject.AddComponent<UniversalDecisionPopup>();
    }

    void CopyMyCode()
    {
        var p = api.LocalProfile();
        GUIUtility.systemCopyBuffer = p.friendCode;
    }

    void ClearList()
    {
        for (int i = listContent.childCount - 1; i >= 0; i--)
            Destroy(listContent.GetChild(i).gameObject);
    }

    void ClearRequests()
    {
        if (requestsContent == null) return;
        for (int i = requestsContent.childCount - 1; i >= 0; i--)
            Destroy(requestsContent.GetChild(i).gameObject);
    }

    private void TryScheduleExternalRefresh()
    {
        if (!_isOpen || !isActiveAndEnabled || api == null)
            return;

        if (_externalRefreshFlow != null)
            return;

        _externalRefreshFlow = StartCoroutine(ExternalRefreshFlow());
    }

    private IEnumerator ExternalRefreshFlow()
    {
        yield return null;

        if (!_isOpen || !isActiveAndEnabled || api == null)
        {
            _externalRefreshFlow = null;
            yield break;
        }

        yield return api.PresencePing();
        yield return RefreshFriends();
        yield return RefreshRequests();
        _externalRefreshFlow = null;
    }

    private IEnumerator AvailabilityFlow()
    {
        var wait = new WaitForSecondsRealtime(Mathf.Max(0.1f, serverStateRefreshInterval));
        while (isActiveAndEnabled)
        {
            RefreshAvailability();
            yield return wait;
        }

        _availabilityFlow = null;
    }

    private void RefreshAvailability()
    {
        bool available = IsFriendsAvailable();

        if (_toggleButton != null && _toggleButton.gameObject.activeSelf != available)
            _toggleButton.gameObject.SetActive(available);

        if (!available && _isOpen)
            ToggleOpen();
    }

    private static bool IsFriendsAvailable()
    {
        return LobbyClient.Instance != null
            && LobbyClient.Instance.IsOnline
            && !LobbyClient.Instance.DebugSimulateOffline;
    }

    private static Transform FindChildByNameRecursive(Transform parent, string targetName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(targetName))
            return null;

        if (string.Equals(parent.name, targetName, StringComparison.Ordinal))
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            var found = FindChildByNameRecursive(parent.GetChild(i), targetName);
            if (found != null)
                return found;
        }

        return null;
    }
}
