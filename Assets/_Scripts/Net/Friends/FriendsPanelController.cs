using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FriendsPanelController : MonoBehaviour
{
    [Header("Refs")]
    private ZooBackendClient backend;
    private SaveManager save;

    [Header("UI")]
    [SerializeField] private GameObject _ui;

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
    private Coroutine _openFlow;
    private bool _isOpen;
    private void Awake()
    {
        if (backend == null) backend = G.Backend;
        if (save == null) save = G.Save;
        if (api == null) api = G.Backend.FriendsApi;
        EnsureRemoteBases();

        renameButton.onClick.AddListener(() => StartCoroutine(RenameFlow()));

        _closeButton.onClick.AddListener(() => ToggleOpen());
        _toggleButton.onClick.AddListener(() => ToggleOpen()) ;
        copyCodeButton.onClick.AddListener(CopyMyCode);
        addButton.onClick.AddListener(() => StartCoroutine(AddFriendFlow()));
    }
    public void ToggleOpen()
    {
        //if (!(LoadingManager.Instance.CurrentLocation == Location.Lobby)) return;
        _isOpen = !_isOpen;
        G.Control.CursorActive = _isOpen;
        _ui.SetActive(_isOpen);
        if (!_isOpen)
        {
            if (_openFlow != null) StopCoroutine(_openFlow);
            _openFlow = null;
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
        G.Input.AFriends += ToggleOpen;
        G.Input.AOpenWindow += Close;
    }
    private void OnDisable()
    {
        G.Initialized.RemoveListener(OnGameInitialized);
        G.Input.AFriends -= ToggleOpen;
        //LoadingManager.Instance.LocationChanged -= ToggleButtonVisibility;
        G.Input.AOpenWindow -= Close;
    }
    private void OnGameInitialized()
    {
        EnsureRemoteBases();
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
        myNameText.text = $"РќРёРє: {p.displayName}";
        myCodeText.text = $"РљРѕРґ: {p.friendCode}";

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
                addStatusText.text = "РћС€РёР±РєР° Р·Р°РіСЂСѓР·РєРё СЃРїРёСЃРєР° РґСЂСѓР·РµР№";
                break;
            case 404:
                addStatusText.text = "РћС€РёР±РєР° Р·Р°РіСЂСѓР·РєРё СЃРїРёСЃРєР° РґСЂСѓР·РµР№";
                break;
            default:
                addStatusText.text = "РћС€РёР±РєР° Р·Р°РіСЂСѓР·РєРё СЃРїРёСЃРєР° РґСЂСѓР·РµР№";
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
                requestsStatusText.text = "Ошибка загрузки заявок";
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
            addStatusText.text = "РќРµ СѓРґР°Р»РѕСЃСЊ РґРѕР±Р°РІРёС‚СЊ (РїСЂРѕРІРµСЂСЊ РєРѕРґ).";
            yield break;
        }

        addCodeInput.text = "";
        yield return RefreshFriends();
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
        if (name.Length < 3) { renameStatusText.text = "РњРёРЅРёРјСѓРј 3 СЃРёРјРІРѕР»Р°"; yield break; }

        bool ok = false;
        string fail = null;

        //yield return api.RenameMe(name, success => ok = success, msg => fail = msg);
        yield return api.RenameMePaid(name, success => ok = success, msg => fail = msg);

        if (!ok)
        {
            renameStatusText.text = fail ?? "РќРµ СѓРґР°Р»РѕСЃСЊ СЃРјРµРЅРёС‚СЊ РЅРёРє";
            yield break;
        }

        renameInput.text = "";
        var p = api.LocalProfile();
        myNameText.text = $"РќРёРє: {p.displayName}";
        renameStatusText.text = "РќРёРє РёР·РјРµРЅС‘РЅ!";
    }

    IEnumerator ViewFriendBaseStub(string friendCode)
    {
        addStatusText.text = "";

        FriendBaseResponse resp = null;
        yield return backend.GetFriendBase(friendCode,
            ok => resp = ok,
            (code, err) => addStatusText.text = "РќРµ СѓРґР°Р»РѕСЃСЊ Р·Р°РіСЂСѓР·РёС‚СЊ Р±Р°Р·Сѓ РґСЂСѓРіР°");

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
}

