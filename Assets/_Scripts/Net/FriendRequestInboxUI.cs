using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FriendRequestInboxUI : MonoBehaviour
{
    [SerializeField] private float pollIntervalSec = 2f;
    [SerializeField] private bool useUniversalPopup = true;

    private Canvas _canvas;
    private GameObject _panel;
    private Text _text;
    private Button _acceptBtn;
    private Button _declineBtn;

    private FriendsApi _api;
    private FriendsApi.FriendRequestItem _current;
    private bool _inFlight;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        StartCoroutine(InitWhenReady());
    }

    private void Start()
    {
        StartCoroutine(PollLoop());
    }

    private IEnumerator InitWhenReady()
    {
        while (_canvas == null)
        {
            yield return null;
            if (UnityEngine.EventSystems.EventSystem.current == null)
                continue;

            CreateUI();
            Hide();
        }
    }

    private IEnumerator PollLoop()
    {
        while (true)
        {
            if (_canvas == null && UnityEngine.EventSystems.EventSystem.current != null)
            {
                CreateUI();
                Hide();
            }

            var popupOpen = useUniversalPopup && FriendsPanelController.IsPopupOpen();
            if (TryResolveApi(out var api) && !_inFlight)
            {
                if (popupOpen)
                {
                    yield return new WaitForSecondsRealtime(pollIntervalSec);
                    continue;
                }

                _inFlight = true;
                List<FriendsApi.FriendRequestItem> list = null;
                yield return api.GetFriendRequests(items => list = items, (_, __) => list = null);
                _inFlight = false;

                if (list != null && list.Count > 0)
                {
                    var first = list[0];
                    if (!TryShowRequestInUniversalPopup(first))
                        ShowRequest(first);
                }
                else
                {
                    if (!popupOpen)
                        Hide();
                }
            }
            else
            {
                if (!popupOpen)
                    Hide();
            }

            yield return new WaitForSecondsRealtime(pollIntervalSec);
        }
    }

    private bool TryResolveApi(out FriendsApi api)
    {
        if (_api == null && G.Backend != null)
            _api = G.Backend.FriendsApi;
        api = _api;
        return api != null;
    }

    private void ShowRequest(FriendsApi.FriendRequestItem request)
    {
        _current = request;
        if (_panel != null) _panel.SetActive(true);
        if (_text != null)
        {
            var name = string.IsNullOrWhiteSpace(request.displayName) ? "Player" : request.displayName;
            var code = string.IsNullOrWhiteSpace(request.friendCode) ? "-" : request.friendCode;
            _text.text = $"Запрос в друзья от {name} ({code})";
        }
    }

    private void Hide()
    {
        _current = null;
        if (_panel != null) _panel.SetActive(false);
    }

    private void OnAccept()
    {
        if (_current == null || _inFlight || _api == null) return;
        StartCoroutine(AcceptFlow(_current.requestId));
    }

    private void OnDecline()
    {
        if (_current == null || _inFlight || _api == null) return;
        StartCoroutine(DeclineFlow(_current.requestId));
    }

    private IEnumerator AcceptFlow(string requestId)
    {
        _inFlight = true;
        var ok = false;
        yield return _api.AcceptFriendRequest(requestId, success => ok = success);
        _inFlight = false;
        Hide();
        if (ok)
            FriendsPanelController.RequestLiveRefresh();
    }

    private IEnumerator DeclineFlow(string requestId)
    {
        _inFlight = true;
        var ok = false;
        yield return _api.DeclineFriendRequest(requestId, success => ok = success);
        _inFlight = false;
        Hide();
        if (ok)
            FriendsPanelController.RequestLiveRefresh();
    }

    private bool TryShowRequestInUniversalPopup(FriendsApi.FriendRequestItem request)
    {
        if (!useUniversalPopup || request == null || _api == null)
            return false;

        var name = string.IsNullOrWhiteSpace(request.displayName) ? "Player" : request.displayName;
        var code = string.IsNullOrWhiteSpace(request.friendCode) ? "-" : request.friendCode;
        var description = $"Запрос в друзья от {name} ({code})";

        var shown = FriendsPanelController.TryShowPopup(new UniversalDecisionPopup.Request
        {
            title = new UniversalDecisionPopup.LocalizedTextPayload("UI/Popup/FriendRequestTitle", "Запрос в друзья"),
            description = new UniversalDecisionPopup.LocalizedTextPayload(string.Empty, description),
            confirm = new UniversalDecisionPopup.LocalizedTextPayload("UI/Popup/FriendAccept", "Принять"),
            cancel = new UniversalDecisionPopup.LocalizedTextPayload("UI/Popup/FriendDecline", "Отклонить"),
            onConfirm = () => StartCoroutine(AcceptFlow(request.requestId)),
            onCancel = () => StartCoroutine(DeclineFlow(request.requestId)),
            closeOnConfirm = true,
            closeOnCancel = true,
            closeButtonActsAsCancel = true
        });

        if (shown)
            _current = request;

        return shown;
    }

    private void CreateUI()
    {
        var go = new GameObject("FriendRequestInboxCanvas");
        DontDestroyOnLoad(go);
        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        go.AddComponent<GraphicRaycaster>();

        _panel = new GameObject("Panel");
        _panel.transform.SetParent(go.transform, false);
        var img = _panel.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.72f);

        var rect = _panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.25f, 0.07f);
        rect.anchorMax = new Vector2(0.75f, 0.22f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _text = CreateText("RequestText", _panel.transform, new Vector2(0.04f, 0.48f), new Vector2(0.96f, 0.94f));
        _acceptBtn = CreateButton("AcceptButton", _panel.transform, "Принять", new Vector2(0.08f, 0.10f), new Vector2(0.45f, 0.42f));
        _declineBtn = CreateButton("DeclineButton", _panel.transform, "Отклонить", new Vector2(0.55f, 0.10f), new Vector2(0.92f, 0.42f));

        _acceptBtn.onClick.AddListener(OnAccept);
        _declineBtn.onClick.AddListener(OnDecline);
    }

    private Text CreateText(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 16;
        text.resizeTextMaxSize = 44;
        var rect = text.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return text;
    }

    private Button CreateButton(string name, Transform parent, string label, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.92f);
        var btn = go.AddComponent<Button>();

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var text = CreateText("Label", go.transform, Vector2.zero, Vector2.one);
        text.text = label;
        text.color = Color.black;
        text.resizeTextMaxSize = 34;

        return btn;
    }
}
