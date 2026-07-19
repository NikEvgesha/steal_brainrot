using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyDebugPanel : MonoBehaviour
{
    [SerializeField] private float refreshSec = 1f;
    [SerializeField] private KeyCode toggleKey = KeyCode.None;

    private static LobbyDebugPanel _instance;

    private Transform _menuParent;
    private GameObject _panel;
    private TMP_Text _text;
    private Button _offlineToggleButton;
    private TMP_Text _offlineToggleText;
    private LobbyClient _lobby;
    private bool _menuOpen;

    public static LobbyDebugPanel EnsureExists()
    {
        if (_instance != null)
            return _instance;

        var existing = FindAnyObjectByType<LobbyDebugPanel>();
        if (existing != null)
            return existing;

        var go = new GameObject("LobbyDebugPanel");
        return go.AddComponent<LobbyDebugPanel>();
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
        StartCoroutine(InitNextFrame());
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
            SetMenuOpen(!_menuOpen);

        if (_menuParent == null || _panel == null)
            TryBindToExistingMenu();

        if (_lobby == null)
            _lobby = LobbyClient.Instance;

        ApplyOfflineToggleState();
    }

    private IEnumerator InitNextFrame()
    {
        yield return null;
        var tries = 0;
        while (UnityEngine.EventSystems.EventSystem.current == null && tries < 10)
        {
            tries++;
            yield return null;
        }

        TryBindToExistingMenu();
        _lobby = LobbyClient.Instance;
        Debug.Log("[LobbyDebugPanel] Created");
        ApplyOfflineToggleState();
        StartCoroutine(RefreshLoop());
    }

    private IEnumerator RefreshLoop()
    {
        while (true)
        {
            UpdateText();
            yield return new WaitForSeconds(refreshSec);
        }
    }

    private void UpdateText()
    {
        if (_text == null) return;
        if (_lobby == null) _lobby = LobbyClient.Instance;

        var pid = G.Save != null ? G.Save.LoadBackendProfile().playerId : "";
        var code = G.Save != null ? G.Save.LoadBackendProfile().friendCode : "";

        if (_lobby == null)
        {
            _text.text = $"Lobby: none\nPlayerId: {pid}\nFriendCode: {code}";
            return;
        }

        var lines = new List<string>
        {
            $"Lobby: {(_lobby.IsOnline ? _lobby.LobbyId : "offline")}",
            $"PlayerId: {pid}",
            $"FriendCode: {code}",
            $"SimOffline: {(_lobby.DebugSimulateOffline ? "ON" : "OFF")}",
            $"Members: {_lobby.LastMembers.Count} (online: {CountOnlineMembers(_lobby.LastMembers)})"
        };

        foreach (var m in _lobby.LastMembers)
        {
            var name = string.IsNullOrEmpty(m.displayName) ? "Player" : m.displayName;
            var online = m.isOnline ? "online" : "offline";
            var friend = m.isFriend ? "friend" : "not-friend";
            lines.Add($"- {name} ({m.playerId}) [{online}, {friend}]");
        }

        _text.text = string.Join("\n", lines);
    }

    public void BindToMenu(Transform menuParent)
    {
        if (menuParent == null)
            return;

        _menuParent = menuParent;
        if (_panel == null)
        {
            if (!TryBindExistingView(_menuParent))
                CreateUI(_menuParent);
        }
        else if (_panel.transform.parent != _menuParent)
        {
            _panel.transform.SetParent(_menuParent, false);
            ResolveViewReferences();
        }

        SetMenuOpen(_menuOpen);
        UpdateText();
        ApplyOfflineToggleState();
    }

    public void SetMenuOpen(bool isOpen)
    {
        _menuOpen = isOpen;
        if (_panel != null)
            _panel.SetActive(isOpen);
    }

    private void TryBindToExistingMenu()
    {
        var settingUi = FindFirstObjectByType<SettingUI>(FindObjectsInactive.Include);
        if (settingUi == null)
            return;

        BindToMenu(settingUi.transform);
    }

    private static int CountOnlineMembers(IReadOnlyList<LobbyMemberStateDto> members)
    {
        if (members == null)
            return 0;

        var count = 0;
        for (int i = 0; i < members.Count; i++)
        {
            if (members[i] != null && members[i].isOnline)
                count++;
        }

        return count;
    }

    private bool TryBindExistingView(Transform parent)
    {
        var view = parent.Find("LobbyDebugPanelView");
        if (view == null)
        {
            var allChildren = parent.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allChildren.Length; i++)
            {
                if (allChildren[i] != null && allChildren[i].name == "LobbyDebugPanelView")
                {
                    view = allChildren[i];
                    break;
                }
            }
        }

        if (view == null)
            return false;

        _panel = view.gameObject;
        ResolveViewReferences();
        return true;
    }

    private void ResolveViewReferences()
    {
        if (_panel == null)
            return;

        _text = FindText(_panel.transform, "DebugText");
        _offlineToggleButton = FindChildComponent<Button>(_panel.transform, "OfflineToggleButton");
        _offlineToggleText = _offlineToggleButton != null
            ? FindText(_offlineToggleButton.transform, "OfflineToggleText")
            : FindText(_panel.transform, "OfflineToggleText");

        if (_offlineToggleButton != null)
        {
            _offlineToggleButton.onClick.RemoveListener(ToggleSimulatedOffline);
            _offlineToggleButton.onClick.AddListener(ToggleSimulatedOffline);
        }
    }

    private void CreateUI(Transform parent)
    {
        _panel = new GameObject("LobbyDebugPanelView", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _panel.transform.SetParent(parent, false);
        var img = _panel.GetComponent<Image>();
        img.color = new Color(0.05f, 0.035f, 0.025f, 0.9f);

        var rect = _panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.58f, 0.05f);
        rect.anchorMax = new Vector2(0.96f, 0.32f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var outline = _panel.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(3f, -3f);
        outline.useGraphicAlpha = true;

        var title = CreateText("Title", _panel.transform, new Vector2(0.03f, 0.82f), new Vector2(0.97f, 0.98f));
        title.alignment = TextAlignmentOptions.Left;
        title.fontSize = 18;
        title.fontStyle = FontStyles.Bold;
        title.text = "Lobby Debug";

        _text = CreateText("DebugText", _panel.transform, new Vector2(0.03f, 0.24f), new Vector2(0.97f, 0.80f));
        _text.alignment = TextAlignmentOptions.TopLeft;

        _offlineToggleButton = CreateButton("OfflineToggleButton", _panel.transform, new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.20f));
        _offlineToggleText = CreateText("OfflineToggleText", _offlineToggleButton.transform, Vector2.zero, Vector2.one);
        _offlineToggleText.alignment = TextAlignmentOptions.Center;
        _offlineToggleText.color = Color.white;
        _offlineToggleButton.onClick.AddListener(ToggleSimulatedOffline);

        _panel.SetActive(_menuOpen);
    }

    private static TMP_Text FindText(Transform root, string childName)
    {
        return FindChildComponent<TMP_Text>(root, childName);
    }

    private static T FindChildComponent<T>(Transform root, string childName) where T : Component
    {
        if (root == null)
            return null;

        var direct = root.Find(childName);
        if (direct != null && direct.TryGetComponent<T>(out var directComponent))
            return directComponent;

        var components = root.GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null && components[i].name == childName)
                return components[i];
        }

        return null;
    }

    private TMP_Text CreateText(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var text = TmpUiTextFactory.Add(go);
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        var rect = text.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return text;
    }

    private Button CreateButton(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.75f);
        var button = go.AddComponent<Button>();
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return button;
    }

    private void ToggleSimulatedOffline()
    {
        if (_lobby == null)
            _lobby = LobbyClient.Instance;
        if (_lobby == null)
            return;

        _lobby.SetDebugSimulateOffline(!_lobby.DebugSimulateOffline);
        ApplyOfflineToggleState();
    }

    private void ApplyOfflineToggleState()
    {
        if (_offlineToggleText == null)
            return;

        var enabled = _lobby != null && _lobby.DebugSimulateOffline;
        _offlineToggleText.text = enabled ? "[x] NET OFF" : "[ ] NET OFF";
    }
}
