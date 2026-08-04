using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LeaderboardWorldInstaller : MonoBehaviour
{
    private static readonly BoardDefinition[] Definitions =
    {
        new("donations_all_time", "UI/Leaderboards/Donations", "Топ донатов", new Color(1f, 0.72f, 0.05f)),
        new("income_weekly", "UI/Leaderboards/WeeklyIncome", "Доход за неделю", new Color(1f, 0.82f, 0.08f)),
        new("hatches_weekly", "UI/Leaderboards/WeeklyHatches", "Вылупления за неделю", new Color(0.95f, 0.9f, 0.78f)),
        new("best_pet_monthly", "UI/Leaderboards/BestPetMonthly", "Лучший питомец месяца", new Color(0.88f, 0.05f, 0.85f)),
        new("hatches_all_time", "UI/Leaderboards/AllTimeHatches", "Вылуплено за всё время", new Color(0.12f, 0.82f, 0.95f))
    };

    private readonly List<LeaderboardWorldBoard> _boards = new();

    public static void EnsureExists()
    {
        if (FindAnyObjectByType<LeaderboardWorldInstaller>() != null)
            return;
        new GameObject("LeaderboardWorldInstaller").AddComponent<LeaderboardWorldInstaller>();
    }

    private IEnumerator Start()
    {
        Transform shop = null;
        while (shop == null)
        {
            var shopObject = GameObject.Find("Shop");
            shop = shopObject != null ? shopObject.transform : null;
            if (shop == null)
                yield return null;
        }

        while (G.Leaderboards == null)
            yield return null;

        BuildBoards(shop);
        G.Leaderboards.SnapshotUpdated += Render;
        Render(G.Leaderboards.Snapshot);

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnDestroy()
    {
        if (G.Leaderboards != null)
            G.Leaderboards.SnapshotUpdated -= Render;
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(string _)
    {
        for (int i = 0; i < _boards.Count; i++)
            _boards[i]?.RefreshLocalizedText();
        Render(G.Leaderboards != null ? G.Leaderboards.Snapshot : null);
    }

    private void BuildBoards(Transform shop)
    {
        if (_boards.Count > 0)
            return;

        Bounds bounds = ResolveBounds(shop);
        const float boardWidth = 4.8f;
        const float spacing = 0.35f;
        float totalWidth = Definitions.Length * boardWidth + (Definitions.Length - 1) * spacing;
        float left = bounds.center.x - totalWidth * 0.5f + boardWidth * 0.5f;
        float y = bounds.min.y + 2.25f;
        float z = bounds.min.z - 0.45f;

        var root = new GameObject("WorldLeaderboards").transform;
        root.SetParent(shop.parent, true);

        for (int i = 0; i < Definitions.Length; i++)
        {
            var boardObject = new GameObject($"Leaderboard_{Definitions[i].BoardId}");
            boardObject.transform.SetParent(root, true);
            boardObject.transform.position = new Vector3(left + i * (boardWidth + spacing), y, z);
            boardObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var view = boardObject.AddComponent<LeaderboardWorldBoard>();
            view.Build(Definitions[i]);
            _boards.Add(view);
        }
    }

    private void Render(LeaderboardsResponse snapshot)
    {
        for (int i = 0; i < _boards.Count; i++)
        {
            var board = snapshot?.boards?.FirstOrDefault(item => item != null && item.boardId == _boards[i].BoardId);
            _boards[i].Render(board, G.Leaderboards != null && G.Leaderboards.IsUsingCachedSnapshot);
        }
    }

    private static Bounds ResolveBounds(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(root.position, new Vector3(24f, 5f, 8f));

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    public readonly struct BoardDefinition
    {
        public readonly string BoardId;
        public readonly string TitleKey;
        public readonly string TitleFallback;
        public readonly Color Accent;

        public BoardDefinition(string boardId, string titleKey, string titleFallback, Color accent)
        {
            BoardId = boardId;
            TitleKey = titleKey;
            TitleFallback = titleFallback;
            Accent = accent;
        }
    }
}

public sealed class LeaderboardWorldBoard : MonoBehaviour
{
    private const int VisibleRows = 10;
    private LeaderboardWorldInstaller.BoardDefinition _definition;
    private Canvas _canvas;
    private TMP_Text _title;
    private TMP_Text _rankColumn;
    private TMP_Text _nameColumn;
    private TMP_Text _scoreColumn;
    private TMP_Text _footer;
    private Button _donateButton;
    private LeaderboardBoardDto _lastBoard;
    private bool _lastCached;

    public string BoardId => _definition.BoardId;

    public void Build(LeaderboardWorldInstaller.BoardDefinition definition)
    {
        _definition = definition;
        CreateBacking(new Vector3(5.05f, 4.48f, 0.18f), Color.black, 0.08f);
        CreateBacking(new Vector3(4.85f, 4.28f, 0.2f), new Color(0.16f, 0.055f, 0.025f), 0f);

        var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = new Vector3(0f, 0f, -0.115f);
        canvasObject.transform.localRotation = Quaternion.identity;
        canvasObject.transform.localScale = Vector3.one * 0.008f;
        _canvas = canvasObject.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;

        var rect = canvasObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(600f, 520f);

        CreatePanel(rect, "Header", new Vector2(0f, 220f), new Vector2(590f, 70f), definition.Accent);
        CreatePanel(rect, "Columns", new Vector2(0f, 170f), new Vector2(570f, 36f), new Color(0.04f, 0.025f, 0.02f, 0.96f));
        CreatePanel(rect, "Rows", new Vector2(0f, -2f), new Vector2(570f, 305f), new Color(0.07f, 0.025f, 0.012f, 0.94f));
        CreatePanel(rect, "Footer", new Vector2(0f, -201f), new Vector2(570f, 66f), new Color(0.12f, 0.05f, 0.025f, 0.98f));

        _title = CreateText(rect, "Title", new Vector2(0f, 220f), new Vector2(560f, 66f), 34f, TextAlignmentOptions.Center);
        _rankColumn = CreateText(rect, "Ranks", new Vector2(-250f, -1f), new Vector2(55f, 305f), 24f, TextAlignmentOptions.TopRight);
        _nameColumn = CreateText(rect, "Names", new Vector2(-72f, -1f), new Vector2(285f, 305f), 24f, TextAlignmentOptions.TopLeft);
        _scoreColumn = CreateText(rect, "Scores", new Vector2(205f, -1f), new Vector2(190f, 305f), 24f, TextAlignmentOptions.TopRight);
        _footer = CreateText(rect, "FooterText", new Vector2(0f, -201f), new Vector2(550f, 60f), 22f, TextAlignmentOptions.Center);

        var header = CreateText(rect, "ColumnHeader", new Vector2(0f, 170f), new Vector2(550f, 34f), 18f, TextAlignmentOptions.Center);
        header.text = LocalizationUtils.T("UI/Leaderboards/Columns", "МЕСТО     ИГРОК                         РЕЗУЛЬТАТ");

        if (definition.BoardId == "donations_all_time")
        {
            _donateButton = CreateButton(rect, new Vector2(0f, -250f), new Vector2(260f, 48f), definition.Accent);
            _donateButton.onClick.AddListener(LeaderboardDonationPopup.Open);
            _donateButton.GetComponentInChildren<TMP_Text>().text = LocalizationUtils.T("UI/Leaderboards/Donate", "ПОДДЕРЖАТЬ ИГРУ");
        }

        RefreshLocalizedText();
    }

    private void LateUpdate()
    {
        if (_canvas != null && _canvas.worldCamera == null && Camera.main != null)
            _canvas.worldCamera = Camera.main;
    }

    public void RefreshLocalizedText()
    {
        if (_title != null)
            _title.text = LocalizationUtils.T(_definition.TitleKey, _definition.TitleFallback);
        if (_donateButton != null)
            _donateButton.GetComponentInChildren<TMP_Text>().text = LocalizationUtils.T("UI/Leaderboards/Donate", "ПОДДЕРЖАТЬ ИГРУ");
        if (_lastBoard != null || _rankColumn != null)
            Render(_lastBoard, _lastCached);
    }

    public void Render(LeaderboardBoardDto board, bool cached)
    {
        _lastBoard = board;
        _lastCached = cached;
        if (_rankColumn == null)
            return;

        if (board?.entries == null)
        {
            _rankColumn.text = string.Empty;
            _nameColumn.text = LocalizationUtils.T("UI/Leaderboards/Loading", "Загрузка...");
            _scoreColumn.text = string.Empty;
            _footer.text = string.Empty;
            return;
        }

        var ranks = new List<string>();
        var names = new List<string>();
        var scores = new List<string>();
        foreach (var entry in board.entries.Take(VisibleRows))
        {
            ranks.Add(entry.rank.ToString(CultureInfo.InvariantCulture));
            names.Add(Ellipsize(entry.displayName, 18));
            scores.Add(FormatScore(entry.score));
        }

        if (ranks.Count == 0)
        {
            names.Add(LocalizationUtils.T("UI/Leaderboards/NoResults", "Пока нет результатов"));
        }

        _rankColumn.text = string.Join("\n", ranks);
        _nameColumn.text = string.Join("\n", names);
        _scoreColumn.text = string.Join("\n", scores);

        string footer = board.currentPlayer != null
            ? LocalizationUtils.Format(
                "UI/Leaderboards/YourPlace",
                "Твоё место: {0}  •  {1}",
                board.currentPlayer.rank,
                FormatScore(board.currentPlayer.score))
            : LocalizationUtils.T("UI/Leaderboards/NotRanked", "Твоего результата пока нет");
        if (board.currentPlayer != null && board.currentPlayer.rank <= 0)
        {
            footer = LocalizationUtils.Format(
                "UI/Leaderboards/YourScore",
                "Your score: {0}",
                FormatScore(board.currentPlayer.score));
        }
        if (cached)
            footer += "  •  " + LocalizationUtils.T("UI/Leaderboards/Cached", "офлайн-копия");
        _footer.text = footer;
    }

    private string FormatScore(double score)
    {
        if (_definition.BoardId.Contains("hatches", StringComparison.Ordinal) ||
            _definition.BoardId == "donations_all_time")
            return Math.Round(score).ToString("N0", CultureInfo.InvariantCulture);
        return G.Currency != null ? G.Currency.ToString(score) : score.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private void CreateBacking(Vector3 size, Color color, float localZ)
    {
        var backing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backing.name = "Backing";
        backing.transform.SetParent(transform, false);
        backing.transform.localPosition = new Vector3(0f, 0f, localZ);
        backing.transform.localScale = size;
        var collider = backing.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);
        var renderer = backing.GetComponent<Renderer>();
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        renderer.material = new Material(shader) { color = color };
    }

    private static Image CreatePanel(RectTransform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = gameObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text CreateText(
        RectTransform parent,
        string name,
        Vector2 position,
        Vector2 size,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var text = gameObject.GetComponent<TextMeshProUGUI>();
        text.font = ResolveFont();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.outlineColor = Color.black;
        text.outlineWidth = 0.18f;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(RectTransform parent, Vector2 position, Vector2 size, Color color)
    {
        var gameObject = new GameObject("DonateButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = gameObject.GetComponent<Image>();
        image.color = color;
        var outline = gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(4f, -4f);
        CreateText(rect, "Label", Vector2.zero, size - new Vector2(12f, 8f), 22f, TextAlignmentOptions.Center);
        return gameObject.GetComponent<Button>();
    }

    internal static TMP_FontAsset ResolveFont()
    {
        var russo = Resources.FindObjectsOfTypeAll<TMP_FontAsset>()
            .FirstOrDefault(font => font != null && font.name.Contains("RussoOne", StringComparison.OrdinalIgnoreCase));
        return russo != null ? russo : TMP_Settings.defaultFontAsset;
    }

    private static string Ellipsize(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Player";
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..(maxLength - 1)] + "…";
    }
}

public static class LeaderboardDonationPopup
{
    private static GameObject _root;
    private static TMP_Text _status;
    private static bool _purchasePending;
    private static readonly (string ProductId, long Score)[] Products =
    {
        ("Donation_25", 25),
        ("Donation_100", 100),
        ("Donation_500", 500)
    };

    public static void Open()
    {
        if (_root != null)
        {
            _root.SetActive(true);
            return;
        }

        _root = new GameObject("LeaderboardDonationPopup", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = _root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;
        var scaler = _root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var rootRect = _root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        var dim = CreateImage(rootRect, "Dim", Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.72f));
        dim.rectTransform.anchorMin = Vector2.zero;
        dim.rectTransform.anchorMax = Vector2.one;
        dim.rectTransform.offsetMin = Vector2.zero;
        dim.rectTransform.offsetMax = Vector2.zero;

        var panel = CreateImage(rootRect, "Panel", Vector2.zero, new Vector2(820f, 540f), new Color(0.16f, 0.055f, 0.025f, 1f));
        panel.gameObject.AddComponent<Outline>().effectColor = Color.black;
        var header = CreateImage(panel.rectTransform, "Header", new Vector2(0f, 218f), new Vector2(790f, 78f), new Color(0.98f, 0.7f, 0.05f));
        CreatePopupText(header.rectTransform, "Title", Vector2.zero, new Vector2(620f, 72f), 38f)
            .text = LocalizationUtils.T("UI/Leaderboards/DonationTitle", "Поддержать игру");

        var close = CreatePopupButton(header.rectTransform, "Close", new Vector2(340f, 0f), new Vector2(70f, 62f), new Color(0.82f, 0.08f, 0.06f));
        close.GetComponentInChildren<TMP_Text>().text = "X";
        close.onClick.AddListener(Close);

        CreatePopupText(panel.rectTransform, "Description", new Vector2(0f, 130f), new Vector2(700f, 80f), 25f)
            .text = LocalizationUtils.T(
                "UI/Leaderboards/DonationDescription",
                "Выбери сумму поддержки. После подтверждения она попадёт в мировой рейтинг.");

        for (int i = 0; i < Products.Length; i++)
        {
            var product = Products[i];
            var button = CreatePopupButton(
                panel.rectTransform,
                product.ProductId,
                new Vector2((i - 1) * 235f, 10f),
                new Vector2(205f, 145f),
                new Color(0.05f, 0.65f, 0.15f));
            var purchase = G.Purchases != null ? G.Purchases.GetPurchaseData(product.ProductId) : null;
            string price = purchase != null && !string.IsNullOrWhiteSpace(purchase.Price)
                ? purchase.Price
                : product.Score.ToString(CultureInfo.InvariantCulture);
            button.GetComponentInChildren<TMP_Text>().text = $"+{product.Score}\n<size=70%>{price}</size>";
            button.onClick.AddListener(() => Buy(product.ProductId, product.Score));
        }

        _status = CreatePopupText(panel.rectTransform, "Status", new Vector2(0f, -150f), new Vector2(720f, 70f), 22f);
        _status.text = G.Purchases != null && G.Purchases.PurchasesAvailable()
            ? LocalizationUtils.T("UI/Leaderboards/DonationReady", "Выбери сумму")
            : LocalizationUtils.T("UI/Leaderboards/PurchasesUnavailable", "Покупки на этой площадке недоступны");

        if (G.Control != null)
            G.Control.CursorActive = true;
    }

    private static void Buy(string productId, long score)
    {
        if (_purchasePending || G.Purchases == null || !G.Purchases.PurchasesAvailable())
            return;

        _purchasePending = true;
        _status.text = LocalizationUtils.T("UI/Leaderboards/PurchaseInProgress", "Открываем покупку...");
        PauseManager.Instance?.SetPause(true, true);
        G.Purchases.BuyPurchase(productId, success =>
        {
            PauseManager.Instance?.SetPause(false, true);
            if (!success)
            {
                _purchasePending = false;
                _status.text = LocalizationUtils.T("UI/Leaderboards/PurchaseFailed", "Покупка отменена или не завершена");
                return;
            }

            if (G.Leaderboards == null)
            {
                _purchasePending = false;
                _status.text = LocalizationUtils.T("UI/Leaderboards/ServerUnavailable", "Сервер временно недоступен");
                return;
            }

            G.Leaderboards.SubmitDonation(productId, score, accepted =>
            {
                _purchasePending = false;
                _status.text = accepted
                    ? LocalizationUtils.T("UI/Leaderboards/DonationAccepted", "Покупка принята. Обновляем рейтинг...")
                    : LocalizationUtils.T("UI/Leaderboards/ServerUnavailable", "Покупка сохранена площадкой, сервер временно недоступен");
            });
        });
    }

    private static void Close()
    {
        if (_purchasePending)
            return;
        if (_root != null)
            UnityEngine.Object.Destroy(_root);
        _root = null;
        _status = null;
        if (G.Control != null)
            G.Control.CursorActive = false;
    }

    private static Image CreateImage(RectTransform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = gameObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreatePopupText(RectTransform parent, string name, Vector2 position, Vector2 size, float fontSize)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var text = gameObject.GetComponent<TextMeshProUGUI>();
        text.font = LeaderboardWorldBoard.ResolveFont();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.enableWordWrapping = true;
        text.outlineColor = Color.black;
        text.outlineWidth = 0.18f;
        return text;
    }

    private static Button CreatePopupButton(RectTransform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        var image = CreateImage(parent, name, position, size, color);
        var outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(4f, -4f);
        var button = image.gameObject.AddComponent<Button>();
        CreatePopupText(image.rectTransform, "Label", Vector2.zero, size - new Vector2(16f, 12f), 30f);
        return button;
    }
}
