using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LobbyDebugPanel : MonoBehaviour
{
    [SerializeField] private float refreshSec = 1f;
    [SerializeField] private bool startCollapsed = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.F3;

    private const string CollapsedPrefKey = "lobby_debug_collapsed";
    private static LobbyDebugPanel _instance;

    private Canvas _canvas;
    private GameObject _panel;
    private Text _text;
    private Button _toggleButton;
    private Text _toggleText;
    private LobbyClient _lobby;
    private bool _collapsed;

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
            ToggleCollapsed();
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

        CreateUI();
        _collapsed = PlayerPrefs.GetInt(CollapsedPrefKey, startCollapsed ? 1 : 0) == 1;
        ApplyCollapsedState();
        _lobby = LobbyClient.Instance;
        Debug.Log("[LobbyDebugPanel] Created");
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

    private void CreateUI()
    {
        var go = new GameObject("LobbyDebugCanvas");
        go.transform.SetParent(transform, false);
        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 1000;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        go.AddComponent<GraphicRaycaster>();

        _panel = new GameObject("Panel");
        _panel.transform.SetParent(go.transform, false);
        var img = _panel.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.6f);

        var rect = _panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.6f, 0.75f);
        rect.anchorMax = new Vector2(0.99f, 0.99f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _text = CreateText("DebugText", _panel.transform, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f));
        _text.alignment = TextAnchor.UpperLeft;

        _toggleButton = CreateButton("ToggleButton", go.transform, new Vector2(0.95f, 0.95f), new Vector2(0.99f, 0.99f));
        _toggleText = CreateText("ToggleText", _toggleButton.transform, Vector2.zero, Vector2.one);
        _toggleText.alignment = TextAnchor.MiddleCenter;
        _toggleText.color = Color.white;
        _toggleText.text = "DBG";
        _toggleButton.onClick.AddListener(ToggleCollapsed);
    }

    private Text CreateText(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
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

    private void ToggleCollapsed()
    {
        _collapsed = !_collapsed;
        PlayerPrefs.SetInt(CollapsedPrefKey, _collapsed ? 1 : 0);
        PlayerPrefs.Save();
        ApplyCollapsedState();
    }

    private void ApplyCollapsedState()
    {
        if (_panel != null)
            _panel.SetActive(!_collapsed);

        if (_toggleText != null)
            _toggleText.text = _collapsed ? "DBG" : "X";
    }
}
