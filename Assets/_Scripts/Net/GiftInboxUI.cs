using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

public class GiftInboxUI : MonoBehaviour
{
    [SerializeField] private float pollIntervalSec = 2f;

    private Canvas _canvas;
    private GameObject _panel;
    private Text _text;
    private Button _acceptBtn;
    private Button _declineBtn;

    private GiftItemDto _current;
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

            if (LobbyClient.Instance != null && LobbyClient.Instance.IsOnline && !_inFlight)
            {
                _inFlight = true;
                List<GiftItemDto> list = null;
                yield return LobbyClient.Instance.GetPendingGifts(r => list = r, (_, __) => list = null);
                _inFlight = false;

                if (list != null && list.Count > 0)
                {
                    ShowGift(list[0]);
                }
                else
                {
                    Hide();
                }
            }
            else
            {
                Hide();
            }

            yield return new WaitForSeconds(pollIntervalSec);
        }
    }

    private void ShowGift(GiftItemDto gift)
    {
        _current = gift;
        if (_panel != null) _panel.SetActive(true);
        if (_text != null)
        {
            var name = string.IsNullOrEmpty(gift.fromDisplayName) ? "Player" : gift.fromDisplayName;
            _text.text = $"Подарок от {name}: {gift.itemType} ({gift.itemId})";
        }
    }

    private void Hide()
    {
        _current = null;
        if (_panel != null) _panel.SetActive(false);
    }

    private void OnAccept()
    {
        if (_current == null) return;
        StartCoroutine(AcceptFlow(_current));
    }

    private void OnDecline()
    {
        if (_current == null) return;
        StartCoroutine(DeclineFlow(_current));
    }

    private IEnumerator AcceptFlow(GiftItemDto gift)
    {
        GiftAcceptResponseDto resp = null;
        yield return LobbyClient.Instance.AcceptGift(gift.giftId, r => resp = r);
        if (resp != null && resp.ok)
        {
            SpawnGiftItem(resp.itemType, resp.itemId);
        }
        Hide();
    }

    private IEnumerator DeclineFlow(GiftItemDto gift)
    {
        bool ok = false;
        yield return LobbyClient.Instance.DeclineGift(gift.giftId, v => ok = v);
        Hide();
    }

    private void SpawnGiftItem(string itemType, string itemId)
    {
        if (G.Storage == null || G.Inventory == null) return;
        var dyn = TryParseDynamicItemId(itemId);
        var lookupId = dyn != null && !string.IsNullOrWhiteSpace(dyn.id) ? dyn.id : itemId;

        InventoryItem prefab = null;
        if (itemType == "egg")
            prefab = G.Storage.GetEgg(lookupId);
        else if (itemType == "brainrot")
            prefab = G.Storage.GetPet(lookupId);
        else if (itemType == "food")
            prefab = G.Storage.GetFood(lookupId);

        if (prefab == null) return;

        var item = Instantiate(prefab);
        ApplyDynamicData(item, itemType, dyn);
        G.Inventory.Add(item);
    }

    private static void ApplyDynamicData(InventoryItem item, string itemType, GiftDynamicPayload dyn)
    {
        if (item == null || dyn == null) return;

        if (itemType == "egg")
        {
            var egg = item as Egg ?? item.GetComponent<Egg>();
            if (egg != null && dyn.element.HasValue && dyn.weight.HasValue)
            {
                var data = new BrainrotDinamicData
                {
                    ElementType = (ElementType)dyn.element.Value,
                    WeightMultiplier = dyn.weight.Value,
                    ResultIncome = dyn.income ?? 0d
                };
                egg.SetData(data);
            }
            return;
        }

        if (itemType == "brainrot")
        {
            var brainrot = item as Brainrot ?? item.GetComponent<Brainrot>();
            if (brainrot != null && dyn.element.HasValue && dyn.weight.HasValue)
            {
                var data = new BrainrotDinamicData
                {
                    ElementType = (ElementType)dyn.element.Value,
                    WeightMultiplier = dyn.weight.Value,
                    ResultIncome = dyn.income ?? 0d
                };
                brainrot.Init(data);
            }
        }
    }

    private static GiftDynamicPayload TryParseDynamicItemId(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || !itemId.StartsWith("dyn:"))
            return null;

        var json = itemId.Substring(4);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonConvert.DeserializeObject<GiftDynamicPayload>(json);
        }
        catch
        {
            return null;
        }
    }

    [System.Serializable]
    private class GiftDynamicPayload
    {
        public string id;
        public int? element;
        public float? weight;
        public double? income;
    }

    private void CreateUI()
    {
        var go = new GameObject("GiftInboxCanvas");
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
        img.color = new Color(0f, 0f, 0f, 0.7f);

        var rect = _panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.3f, 0.75f);
        rect.anchorMax = new Vector2(0.7f, 0.9f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _text = CreateText("GiftText", _panel.transform, new Vector2(0.05f, 0.5f), new Vector2(0.95f, 0.95f));
        _acceptBtn = CreateButton("AcceptButton", _panel.transform, "Принять", new Vector2(0.1f, 0.1f), new Vector2(0.45f, 0.45f));
        _declineBtn = CreateButton("DeclineButton", _panel.transform, "Отказать", new Vector2(0.55f, 0.1f), new Vector2(0.9f, 0.45f));

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
        img.color = new Color(1f, 1f, 1f, 0.9f);
        var btn = go.AddComponent<Button>();

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var text = CreateText("Label", go.transform, new Vector2(0f, 0f), new Vector2(1f, 1f));
        text.text = label;
        text.color = Color.black;

        return btn;
    }

}
