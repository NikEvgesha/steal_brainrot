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

        yield return api.EnsureGuest();

        var p = api.LocalProfile();
        myNameText.text = $"Ник: {p.displayName}";
        myCodeText.text = $"Код: {p.friendCode}";

        // “онлайн” — пингуем при открытии и потом можно раз в 20 сек в отдельном месте
        yield return api.PresencePing();

        yield return RefreshFriends();
    }

    IEnumerator RefreshFriends()
    {
        ClearList();

        List<FriendsApi.FriendItem> list = null;
        yield return api.GetFriends(items => list = items, (code, err) => {
        switch (code)
        {
            case 400:
                addStatusText.text = "Ошибка загрузки списка друзей";
                break;
            case 404:
                addStatusText.text = "Ошибка загрузки списка друзей";
                break;
            default:
                addStatusText.text = "Ошибка загрузки списка друзей";
                break;
        }
    });

        
            

        if (list == null) yield break;

        foreach (var f in list)
        {
            var row = Instantiate(rowPrefab, listContent);
            row.Bind(f,
                onRemove: () => StartCoroutine(RemoveFriendFlow(f.friendCode)),
                onView: () => StartCoroutine(ViewFriendBaseStub(f.friendCode))
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
            addStatusText.text = "Не удалось добавить (проверь код).";
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

    IEnumerator RenameFlow()
    {
        renameStatusText.text = "";
        var name = (renameInput.text ?? "").Trim();
        if (name.Length < 3) { renameStatusText.text = "Минимум 3 символа"; yield break; }

        bool ok = false;
        string fail = null;

        //yield return api.RenameMe(name, success => ok = success, msg => fail = msg);
        yield return api.RenameMePaid(name, success => ok = success, msg => fail = msg);

        if (!ok)
        {
            renameStatusText.text = fail ?? "Не удалось сменить ник";
            yield break;
        }

        renameInput.text = "";
        var p = api.LocalProfile();
        myNameText.text = $"Ник: {p.displayName}";
        renameStatusText.text = "Ник изменён!";
    }

    IEnumerator ViewFriendBaseStub(string friendCode)
    {
        addStatusText.text = "";

        FriendBaseResponse resp = null;
        yield return backend.GetFriendBase(friendCode,
            ok => resp = ok,
            (code, err) => addStatusText.text = "Не удалось загрузить базу друга");

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
}
