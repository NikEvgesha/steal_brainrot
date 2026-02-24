using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LobbyDebugPanel : MonoBehaviour
{
    [SerializeField] private float refreshSec = 1f;

    private Canvas _canvas;
    private GameObject _panel;
    private Text _text;
    private LobbyClient _lobby;

    private void Awake()
    {
        StartCoroutine(InitNextFrame());
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
        DontDestroyOnLoad(go);
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
}
