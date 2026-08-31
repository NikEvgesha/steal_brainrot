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
        new(LeaderboardService.DonationsBoardId, "UI/Leaderboards/Donations", "Топ донатов", new Color(1f, 0.72f, 0.05f)),
        new(LeaderboardService.WeeklyIncomeBoardId, "UI/Leaderboards/WeeklyIncome", "Доход за неделю", new Color(1f, 0.82f, 0.08f)),
        new(LeaderboardService.WeeklyHatchesBoardId, "UI/Leaderboards/WeeklyHatches", "Вылупления за неделю", new Color(0.95f, 0.9f, 0.78f)),
        new(LeaderboardService.MonthlyBestPetBoardId, "UI/Leaderboards/BestPetMonthly", "Лучший питомец месяца", new Color(0.88f, 0.05f, 0.85f)),
        new(LeaderboardService.AllTimeHatchesBoardId, "UI/Leaderboards/AllTimeHatches", "Вылуплено за всё время", new Color(0.12f, 0.82f, 0.95f))
    };

    public static IReadOnlyList<BoardDefinition> BoardDefinitions => Definitions;

    private readonly List<LeaderboardWorldBoard> _boards = new();

    public static void EnsureExists()
    {
        if (!LeaderboardService.RuntimeEnabled)
            return;

        if (FindAnyObjectByType<LeaderboardWorldInstaller>() != null)
            return;
        new GameObject("LeaderboardWorldInstaller").AddComponent<LeaderboardWorldInstaller>();
    }

    private IEnumerator Start()
    {
        while (G.Leaderboards == null)
            yield return null;

        RegisterSceneBoards();
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

    private void RegisterSceneBoards()
    {
        _boards.Clear();
        var sceneBoards = FindObjectsByType<LeaderboardWorldBoard>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .Where(board => board != null && !string.IsNullOrWhiteSpace(board.BoardId))
            .OrderBy(board => board.transform.position.x);

        foreach (var board in sceneBoards)
        {
            if (_boards.Any(existing => existing.BoardId == board.BoardId))
            {
                Debug.LogWarning($"[Leaderboards] Duplicate scene board '{board.BoardId}' ignored.", board);
                continue;
            }

            _boards.Add(board);
        }

        if (_boards.Count == 0)
            Debug.LogError("[Leaderboards] No LeaderboardWorldBoard prefabs are placed in the active scene.");
    }

    private void Render(LeaderboardsResponse snapshot)
    {
        for (int i = 0; i < _boards.Count; i++)
        {
            var board = snapshot?.boards?.FirstOrDefault(item => item != null && item.boardId == _boards[i].BoardId);
            _boards[i].Render(board, G.Leaderboards != null && G.Leaderboards.IsUsingCachedSnapshot);
        }
    }

    [Serializable]
    public struct BoardDefinition
    {
        public string BoardId;
        public string TitleKey;
        public string TitleFallback;
        public Color Accent;

        public BoardDefinition(string boardId, string titleKey, string titleFallback, Color accent)
        {
            BoardId = boardId;
            TitleKey = titleKey;
            TitleFallback = titleFallback;
            Accent = accent;
        }
    }
}

public class LeaderboardWorldBoardBase : MonoBehaviour
{
    private const int VisibleRows = 10;
    [SerializeField] private LeaderboardWorldInstaller.BoardDefinition _definition;
    [SerializeField] private Material _outerMaterial;
    [SerializeField] private Material _innerMaterial;
    [SerializeField] private Material _accentMaterial;
    [SerializeField] private Canvas _canvas;
    [SerializeField] private Image _header;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _rankHeader;
    [SerializeField] private TMP_Text _nameHeader;
    [SerializeField] private TMP_Text _scoreHeader;
    [SerializeField] private TMP_Text[] _rankRows;
    [SerializeField] private TMP_Text[] _nameRows;
    [SerializeField] private TMP_Text[] _scoreRows;
    [SerializeField] private TMP_Text _footer;
    [SerializeField] private Button _donateButton;
    private LeaderboardBoardDto _lastBoard;
    private bool _lastCached;

    public string BoardId => _definition.BoardId;

    private void Awake()
    {
        if (_donateButton != null)
        {
            _donateButton.onClick.RemoveListener(LeaderboardDonationPopup.Open);
            _donateButton.onClick.AddListener(LeaderboardDonationPopup.Open);
        }

        RefreshLocalizedText();
    }

    private void OnValidate()
    {
        if (_header != null)
            _header.color = _definition.Accent;
        if (_donateButton != null && _donateButton.image != null)
            _donateButton.image.color = _definition.Accent;
        ApplyMaterialColor(_accentMaterial, _definition.Accent);
        if (!Application.isPlaying && _title != null)
            _title.text = string.IsNullOrWhiteSpace(_definition.TitleFallback)
                ? _definition.BoardId
                : _definition.TitleFallback;
    }

    public void Build(
        LeaderboardWorldInstaller.BoardDefinition definition,
        Material outerMaterial,
        Material innerMaterial,
        Material accentMaterial)
    {
        if (transform.childCount > 0)
        {
            Debug.LogWarning($"[Leaderboards] '{name}' already has generated visuals.", this);
            return;
        }

        _definition = definition;
        _outerMaterial = outerMaterial;
        _innerMaterial = innerMaterial;
        _accentMaterial = accentMaterial;
        ApplyMaterialColor(_accentMaterial, definition.Accent);

        CreateBacking("OuterFrame", new Vector3(5.18f, 7.48f, 0.24f), _outerMaterial, 0.10f, new Vector2(0f, -1.47f));
        CreateBacking("BoardBody", new Vector3(4.72f, 7.02f, 0.26f), _innerMaterial, -0.01f, new Vector2(0f, -1.47f));
        CreateBacking("TopCap", new Vector3(5.48f, 0.32f, 0.42f), _accentMaterial, -0.02f, new Vector2(0f, 2.35f));
        CreateBacking("BottomBeam", new Vector3(5.32f, 0.28f, 0.40f), _accentMaterial, -0.02f, new Vector2(0f, -5.18f));
        CreateBacking("LeftPost", new Vector3(0.30f, 7.35f, 0.38f), _accentMaterial, -0.02f, new Vector2(-2.53f, -1.48f));
        CreateBacking("RightPost", new Vector3(0.30f, 7.35f, 0.38f), _accentMaterial, -0.02f, new Vector2(2.53f, -1.48f));
        CreateBacking("LeftFoot", new Vector3(1.28f, 0.22f, 0.82f), _outerMaterial, -0.18f, new Vector2(-1.62f, -5.34f));
        CreateBacking("RightFoot", new Vector3(1.28f, 0.22f, 0.82f), _outerMaterial, -0.18f, new Vector2(1.62f, -5.34f));
        for (int i = -2; i <= 2; i++)
            CreateStud(i * 0.86f, 2.58f);

        var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = new Vector3(0f, -1.47f, -0.235f);
        canvasObject.transform.localRotation = Quaternion.identity;
        canvasObject.transform.localScale = Vector3.one * 0.0075f;
        _canvas = canvasObject.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;

        var rect = canvasObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(600f, 920f);

        Sprite studSprite = Resources.Load<Sprite>("BlockyUI/BlockyStudPanel");
        Sprite plainSprite = Resources.Load<Sprite>("BlockyUI/BlockyPlainPanel");
        bool isDonationBoard = definition.BoardId == LeaderboardService.DonationsBoardId;

        _header = CreatePanel(rect, "Header", new Vector2(0f, 404f), new Vector2(574f, 102f), definition.Accent, studSprite, true, true);
        CreatePanel(rect, "Columns", new Vector2(0f, 326f), new Vector2(570f, 46f), new Color(0.075f, 0.028f, 0.012f, 1f), plainSprite, true, true);
        CreatePanel(rect, "Rows", new Vector2(0f, -1f), new Vector2(570f, 628f), new Color(0.115f, 0.042f, 0.017f, 0.99f), studSprite, true, true);

        Vector2 footerPosition = isDonationBoard ? new Vector2(0f, -346f) : new Vector2(0f, -389f);
        Vector2 footerSize = isDonationBoard ? new Vector2(570f, 38f) : new Vector2(570f, 70f);
        CreatePanel(rect, "Footer", footerPosition, footerSize, new Color(0.105f, 0.045f, 0.018f, 1f), plainSprite, true, true);

        _title = CreateText(rect, "Title", new Vector2(0f, 404f), new Vector2(536f, 90f), 44f, TextAlignmentOptions.Center, false, true);
        _rankHeader = CreateText(rect, "RankHeader", new Vector2(-236f, 326f), new Vector2(72f, 40f), 22f, TextAlignmentOptions.Center);
        _nameHeader = CreateText(rect, "NameHeader", new Vector2(-46f, 326f), new Vector2(292f, 40f), 22f, TextAlignmentOptions.Center, false, true);
        _scoreHeader = CreateText(rect, "ScoreHeader", new Vector2(192f, 326f), new Vector2(168f, 40f), 20f, TextAlignmentOptions.Center, false, true);
        CreateLeaderboardRows(rect, plainSprite, definition.Accent);
        _footer = CreateText(rect, "FooterText", footerPosition, footerSize - new Vector2(18f, 8f), 24f, TextAlignmentOptions.Center, true);

        if (isDonationBoard)
        {
            _donateButton = CreateButton(rect, new Vector2(0f, -414f), new Vector2(360f, 54f), definition.Accent, studSprite);
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
        if (_rankHeader != null)
            _rankHeader.text = LocalizationUtils.T("UI/Leaderboards/RankColumn", "№");
        if (_nameHeader != null)
            _nameHeader.text = LocalizationUtils.T("UI/Leaderboards/PlayerColumn", "ИГРОК");
        if (_scoreHeader != null)
            _scoreHeader.text = LocalizationUtils.T("UI/Leaderboards/ScoreColumn", "РЕЗУЛЬТАТ");
        if (_donateButton != null)
            _donateButton.GetComponentInChildren<TMP_Text>().text = LocalizationUtils.T("UI/Leaderboards/Donate", "ПОДДЕРЖАТЬ ИГРУ");
        if (_lastBoard != null || HasRows())
            Render(_lastBoard, _lastCached);
    }

    public void Render(LeaderboardBoardDto board, bool cached)
    {
        _lastBoard = board;
        _lastCached = cached;
        if (!HasRows())
            return;

        ClearRows();

        if (board?.entries == null)
        {
            _nameRows[0].text = LocalizationUtils.T("UI/Leaderboards/Loading", "Загрузка...");
            _footer.text = string.Empty;
            return;
        }

        int rowIndex = 0;
        foreach (var entry in board.entries.Take(VisibleRows))
        {
            string color = ResolvePlaceColor(entry.rank);
            _rankRows[rowIndex].text = Colorize(entry.rank.ToString(CultureInfo.InvariantCulture), color);
            _nameRows[rowIndex].text = Colorize(Ellipsize(entry.displayName, 18), color);
            _scoreRows[rowIndex].text = Colorize(FormatScore(entry.score), color);
            rowIndex++;
        }

        if (rowIndex == 0)
            _nameRows[0].text = LocalizationUtils.T("UI/Leaderboards/NoResults", "Пока нет результатов");

        string footer = board.currentPlayer != null
            ? LocalizationUtils.Format(
                "UI/Leaderboards/YourPlace",
                "Твоё место: {0}  |  {1}",
                board.currentPlayer.rank,
                FormatScore(board.currentPlayer.score))
            : LocalizationUtils.T("UI/Leaderboards/NotRanked", "Твоего результата пока нет");
        if (board.currentPlayer != null && board.currentPlayer.rank <= 0)
        {
            footer = LocalizationUtils.Format(
                "UI/Leaderboards/YourScore",
                "Твой результат: {0}",
                FormatScore(board.currentPlayer.score));
        }
        if (cached)
            footer += "  |  " + LocalizationUtils.T("UI/Leaderboards/Cached", "офлайн-копия");
        _footer.text = footer;
    }

    private string FormatScore(double score)
    {
        if (_definition.BoardId.Contains("hatches", StringComparison.Ordinal) ||
            _definition.BoardId == LeaderboardService.DonationsBoardId)
            return Math.Round(score).ToString("N0", CultureInfo.InvariantCulture);
        return G.Currency != null ? G.Currency.ToString(score) : score.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private void CreateBacking(
        string objectName,
        Vector3 size,
        Material material,
        float localZ,
        Vector2? localPosition = null)
    {
        var backing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backing.name = objectName;
        backing.transform.SetParent(transform, false);
        Vector2 position = localPosition ?? Vector2.zero;
        backing.transform.localPosition = new Vector3(position.x, position.y, localZ);
        backing.transform.localScale = size;
        var collider = backing.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying)
                Destroy(collider);
            else
                DestroyImmediate(collider);
        }
        var renderer = backing.GetComponent<Renderer>();
        if (material != null)
        {
            renderer.sharedMaterial = material;
            return;
        }

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        renderer.material = new Material(shader) { color = Color.magenta };
    }

    private void CreateStud(float localX, float localY)
    {
        var stud = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stud.name = "TopStud";
        stud.transform.SetParent(transform, false);
        stud.transform.localPosition = new Vector3(localX, localY, -0.02f);
        stud.transform.localScale = new Vector3(0.18f, 0.10f, 0.18f);
        var collider = stud.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying)
                Destroy(collider);
            else
                DestroyImmediate(collider);
        }
        stud.GetComponent<Renderer>().sharedMaterial = _accentMaterial;
    }

    private void CreateLeaderboardRows(RectTransform parent, Sprite sprite, Color accent)
    {
        _rankRows = new TMP_Text[VisibleRows];
        _nameRows = new TMP_Text[VisibleRows];
        _scoreRows = new TMP_Text[VisibleRows];

        for (int i = 0; i < VisibleRows; i++)
        {
            Color rankColor;
            Color nameColor;
            Color scoreColor;
            if (i == 0)
            {
                rankColor = new Color(1f, 0.58f, 0.015f, 1f);
                nameColor = new Color(0.90f, 0.44f, 0.01f, 1f);
                scoreColor = new Color(1f, 0.58f, 0.015f, 1f);
            }
            else if (i == 1)
            {
                rankColor = new Color(0.61f, 0.65f, 0.67f, 1f);
                nameColor = new Color(0.42f, 0.46f, 0.48f, 1f);
                scoreColor = new Color(0.61f, 0.65f, 0.67f, 1f);
            }
            else if (i == 2)
            {
                rankColor = new Color(0.76f, 0.32f, 0.055f, 1f);
                nameColor = new Color(0.57f, 0.22f, 0.035f, 1f);
                scoreColor = new Color(0.76f, 0.32f, 0.055f, 1f);
            }
            else
            {
                Color baseColor = i % 2 == 0
                    ? new Color(0.205f, 0.078f, 0.025f, 0.98f)
                    : new Color(0.145f, 0.047f, 0.016f, 0.98f);
                rankColor = Color.Lerp(baseColor, accent, 0.02f);
                nameColor = Color.Lerp(baseColor, Color.black, 0.07f);
                scoreColor = Color.Lerp(baseColor, accent, 0.02f);
            }

            float rowY = 282f - i * 61.5f;
            CreatePanel(parent, $"RankCell_{i + 1:00}", new Vector2(-236f, rowY), new Vector2(72f, 55f), rankColor, sprite, true, true);
            CreatePanel(parent, $"NameCell_{i + 1:00}", new Vector2(-46f, rowY), new Vector2(292f, 55f), nameColor, sprite, true, true);
            CreatePanel(parent, $"ScoreCell_{i + 1:00}", new Vector2(192f, rowY), new Vector2(168f, 55f), scoreColor, sprite, true, true);

            float rankSize = i < 3 ? 38f : 33f;
            float nameSize = i < 3 ? 31f : 29f;
            float scoreSize = i < 3 ? 32f : 29f;
            _rankRows[i] = CreateText(parent, $"Rank_{i + 1:00}", new Vector2(-236f, rowY), new Vector2(64f, 51f), rankSize, TextAlignmentOptions.Center, false, true);
            _nameRows[i] = CreateText(parent, $"Name_{i + 1:00}", new Vector2(-46f, rowY), new Vector2(274f, 51f), nameSize, TextAlignmentOptions.Center, false, true);
            _scoreRows[i] = CreateText(parent, $"Score_{i + 1:00}", new Vector2(192f, rowY), new Vector2(156f, 51f), scoreSize, TextAlignmentOptions.Center, false, true);
        }
    }

    private bool HasRows()
    {
        return _rankRows != null &&
               _nameRows != null &&
               _scoreRows != null &&
               _rankRows.Length == VisibleRows &&
               _nameRows.Length == VisibleRows &&
               _scoreRows.Length == VisibleRows;
    }

    private void ClearRows()
    {
        for (int i = 0; i < VisibleRows; i++)
        {
            if (_rankRows[i] != null)
                _rankRows[i].text = string.Empty;
            if (_nameRows[i] != null)
                _nameRows[i].text = string.Empty;
            if (_scoreRows[i] != null)
                _scoreRows[i].text = string.Empty;
        }
    }

    private static Image CreatePanel(
        RectTransform parent,
        string name,
        Vector2 position,
        Vector2 size,
        Color color,
        Sprite sprite = null,
        bool tiled = false,
        bool outlined = false)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = gameObject.GetComponent<Image>();
        image.color = color;
        image.sprite = sprite;
        image.type = sprite != null && tiled ? Image.Type.Tiled : Image.Type.Simple;
        image.pixelsPerUnitMultiplier = 2.2f;
        image.raycastTarget = false;
        if (outlined)
        {
            var outline = gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(3f, -3f);
        }
        return image;
    }

    private static TMP_Text CreateText(
        RectTransform parent,
        string name,
        Vector2 position,
        Vector2 size,
        float fontSize,
        TextAlignmentOptions alignment,
        bool wordWrapping = false,
        bool autoSizing = false)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var text = gameObject.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset font = ResolveFont();
        text.font = font;
        Material outlinedMaterial = Resources.Load<Material>("Materials/LeaderboardRussoOutlined");
        if (outlinedMaterial != null)
        {
            text.fontSharedMaterial = outlinedMaterial;
        }
        else if (font != null && font.material != null)
        {
            text.fontSharedMaterial = font.material;
            text.outlineColor = Color.black;
            text.outlineWidth = 0.24f;
        }
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.enableWordWrapping = wordWrapping;
        text.enableAutoSizing = wordWrapping || autoSizing;
        text.fontSizeMin = wordWrapping || autoSizing ? Mathf.Max(18f, fontSize * 0.62f) : fontSize;
        text.fontSizeMax = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.lineSpacing = -8f;
        text.overflowMode = wordWrapping ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(RectTransform parent, Vector2 position, Vector2 size, Color color, Sprite sprite)
    {
        var gameObject = new GameObject("DonateButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = gameObject.GetComponent<Image>();
        image.color = color;
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Tiled : Image.Type.Simple;
        image.pixelsPerUnitMultiplier = 2.2f;
        var outline = gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(4f, -4f);
        CreateText(rect, "Label", Vector2.zero, size - new Vector2(12f, 8f), 22f, TextAlignmentOptions.Center);
        return gameObject.GetComponent<Button>();
    }

    private static string ResolvePlaceColor(int rank)
    {
        return rank switch
        {
            1 => "#FFF7D6",
            2 => "#FFFFFF",
            3 => "#FFF0E2",
            _ => "#FFFFFF"
        };
    }

    private static string Colorize(string value, string color)
    {
        return $"<color={color}>{value}</color>";
    }

    private static void ApplyMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    internal static TMP_FontAsset ResolveFont()
    {
        TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        var russo = fonts.FirstOrDefault(font =>
                        font != null &&
                        font.name.Equals("RussoOne-Regular Cyrillic SDF", StringComparison.OrdinalIgnoreCase))
                    ?? fonts.FirstOrDefault(font =>
                        font != null &&
                        font.name.Equals("RussoOne-Regular SDF", StringComparison.OrdinalIgnoreCase));
        return russo != null ? russo : TMP_Settings.defaultFontAsset;
    }

    private static string Ellipsize(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Player";
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..(maxLength - 3)] + "...";
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
