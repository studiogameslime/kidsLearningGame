using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Reusable in-game leaderboard overlay. Attach to any game scene canvas.
/// Shows a single-game leaderboard for all local profiles when opened.
/// Call Open(gameId) from a trophy button. Call Close() or press X to dismiss.
/// </summary>
public class InGameLeaderboard : MonoBehaviour
{
    public Sprite roundedRect;
    public Sprite trophySprite;
    public Button trophyButton;
    public string gameId;

    private GameObject _modal;
    private static InGameLeaderboard _instance;

    // Colors
    private static readonly Color BgColor = HC("#F0F2F5");
    private static readonly Color HeaderBg = HC("#2C3E50");
    private static readonly Color CardColor = HC("#FFFFFF");
    private static readonly Color Primary = HC("#3498DB");
    private static readonly Color TextDark = HC("#2D3436");
    private static readonly Color TextMedium = HC("#636E72");
    private static readonly Color TextLight = HC("#B2BEC3");
    private static readonly Color GoldAccent = HC("#F1C40F");
    private static readonly Color HighlightBg = HC("#E3F2FD");
    private static readonly Color AccentGreen = HC("#27AE60");

    private void Awake()
    {
        _instance = this;
    }

    private void Start()
    {
        if (trophyButton != null)
            trophyButton.onClick.AddListener(OnTrophyPressed);
    }

    private void OnTrophyPressed()
    {
        string id = gameId;
        if (string.IsNullOrEmpty(id) && GameContext.CurrentGame != null)
            id = GameContext.CurrentGame.id;
        if (!string.IsNullOrEmpty(id))
            Open(id);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>Static accessor for opening from anywhere.</summary>
    public static void Show(string gameId)
    {
        if (_instance != null)
            _instance.Open(gameId);
    }

    public void Open(string gameId)
    {
        if (_modal != null) return;

        var board = LeaderboardBuilder.BuildForGame(gameId);
        if (board == null) return;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas == null) return;

        // Pause game while leaderboard is open
        Time.timeScale = 0f;

        // ── Modal overlay ──
        _modal = new GameObject("InGameLeaderboard");
        _modal.transform.SetParent(canvas.transform, false);
        var modalRT = _modal.AddComponent<RectTransform>();
        modalRT.anchorMin = Vector2.zero;
        modalRT.anchorMax = Vector2.one;
        modalRT.offsetMin = Vector2.zero;
        modalRT.offsetMax = Vector2.zero;

        // Dim background
        var dimImg = _modal.AddComponent<Image>();
        dimImg.color = new Color(0, 0, 0, 0.6f);
        dimImg.raycastTarget = true;

        // ── Panel ──
        var panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(_modal.transform, false);
        var panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = new Vector2(24, 50);
        panelRT.offsetMax = new Vector2(-24, -50);
        var panelImg = panelGO.AddComponent<Image>();
        if (roundedRect != null) panelImg.sprite = roundedRect;
        panelImg.type = Image.Type.Sliced;
        panelImg.color = BgColor;

        // ── Header ──
        var headerGO = new GameObject("Header");
        headerGO.transform.SetParent(panelGO.transform, false);
        var headerRT = headerGO.AddComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0, 1);
        headerRT.anchorMax = new Vector2(1, 1);
        headerRT.pivot = new Vector2(0.5f, 1);
        headerRT.sizeDelta = new Vector2(0, 70);
        var headerImg = headerGO.AddComponent<Image>();
        if (roundedRect != null) headerImg.sprite = roundedRect;
        headerImg.type = Image.Type.Sliced;
        headerImg.color = HeaderBg;

        var headerLayout = headerGO.AddComponent<HorizontalLayoutGroup>();
        headerLayout.padding = new RectOffset(14, 14, 8, 8);
        headerLayout.spacing = 10;
        headerLayout.childAlignment = TextAnchor.MiddleCenter;
        headerLayout.childForceExpandWidth = false;
        headerLayout.childForceExpandHeight = true;
        headerLayout.childControlWidth = true;
        headerLayout.childControlHeight = false;

        // Close button
        var closeBtnGO = new GameObject("CloseBtn");
        closeBtnGO.transform.SetParent(headerGO.transform, false);
        closeBtnGO.AddComponent<LayoutElement>().preferredWidth = 40;
        var closeBtnImg = closeBtnGO.AddComponent<Image>();
        if (roundedRect != null) closeBtnImg.sprite = roundedRect;
        closeBtnImg.type = Image.Type.Sliced;
        closeBtnImg.color = new Color(1, 1, 1, 0.2f);
        var closeBtn = closeBtnGO.AddComponent<Button>();
        closeBtn.targetGraphic = closeBtnImg;
        closeBtn.onClick.AddListener(Close);
        var closeTMP = MakeTMP(closeBtnGO.transform, "\u2715", 20, Color.white); // ✕

        // Title
        var titleTMP = MakeTMP(headerGO.transform,
            H("\u05D8\u05D1\u05DC\u05EA \u05D0\u05DC\u05D9\u05E4\u05D5\u05D9\u05D5\u05EA"), // טבלת אליפויות
            20, Color.white);
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        // Trophy icon
        if (trophySprite != null)
        {
            var trGO = new GameObject("Trophy");
            trGO.transform.SetParent(headerGO.transform, false);
            trGO.AddComponent<LayoutElement>().preferredWidth = 32;
            var trImg = trGO.AddComponent<Image>();
            trImg.sprite = trophySprite;
            trImg.preserveAspect = true;
            trImg.raycastTarget = false;
        }

        // ── Game name sub-header ──
        var subHeaderGO = new GameObject("SubHeader");
        subHeaderGO.transform.SetParent(panelGO.transform, false);
        var subRT = subHeaderGO.AddComponent<RectTransform>();
        subRT.anchorMin = new Vector2(0, 1);
        subRT.anchorMax = new Vector2(1, 1);
        subRT.pivot = new Vector2(0.5f, 1);
        subRT.anchoredPosition = new Vector2(0, -70);
        subRT.sizeDelta = new Vector2(0, 36);
        var gameNameTMP = MakeTMP(subHeaderGO.transform, H(board.gameName), 17, Primary);
        gameNameTMP.fontStyle = FontStyles.Bold;
        var gnRT = gameNameTMP.GetComponent<RectTransform>();
        gnRT.anchorMin = Vector2.zero;
        gnRT.anchorMax = Vector2.one;
        gnRT.offsetMin = new Vector2(16, 0);
        gnRT.offsetMax = new Vector2(-16, 0);

        // ── ScrollView ──
        var svGO = new GameObject("ScrollView");
        svGO.transform.SetParent(panelGO.transform, false);
        var svRT = svGO.AddComponent<RectTransform>();
        svRT.anchorMin = Vector2.zero;
        svRT.anchorMax = Vector2.one;
        svRT.offsetMin = Vector2.zero;
        svRT.offsetMax = new Vector2(0, -106); // below header + sub
        var svBg = svGO.AddComponent<Image>();
        svBg.color = BgColor;
        svGO.AddComponent<Mask>().showMaskGraphic = true;
        var sv = svGO.AddComponent<ScrollRect>();
        sv.horizontal = false;
        sv.vertical = true;
        sv.movementType = ScrollRect.MovementType.Elastic;

        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(svGO.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;

        var contentLayout = contentGO.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 4;
        contentLayout.padding = new RectOffset(14, 14, 10, 20);
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sv.content = contentRT;

        // ── Column headers ──
        var hdrRow = MakeRow(contentGO.transform, 24);
        MakeCell(hdrRow.transform, "#", 30, TextLight, 11, true);
        MakeCell(hdrRow.transform, H("\u05E9\u05DD"), 0, TextLight, 11, true, true); // שם
        MakeCell(hdrRow.transform, H("\u05E6\u05D9\u05D5\u05DF"), 50, TextLight, 11, true); // ציון
        MakeCell(hdrRow.transform, H("\u05E8\u05DE\u05D4"), 40, TextLight, 11, true); // רמה
        MakeCell(hdrRow.transform, H("\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD"), 55, TextLight, 11, true); // משחקים

        // ── Rows ──
        foreach (var entry in board.entries)
            BuildRow(contentGO.transform, entry);

        // Spacer at bottom
        var spacer = new GameObject("Spacer");
        spacer.transform.SetParent(contentGO.transform, false);
        spacer.AddComponent<RectTransform>();
        spacer.AddComponent<LayoutElement>().preferredHeight = 16;
    }

    public void Close()
    {
        if (_modal != null)
        {
            Destroy(_modal);
            _modal = null;
        }
        Time.timeScale = 1f;
    }

    private void BuildRow(Transform parent, GameLeaderboardEntryData entry)
    {
        var rowGO = MakeRow(parent, entry.hasPlayedGame ? 46 : 38);
        var rowImg = rowGO.AddComponent<Image>();
        if (roundedRect != null) rowImg.sprite = roundedRect;
        rowImg.type = Image.Type.Sliced;
        rowImg.color = entry.isCurrentProfile ? HighlightBg : CardColor;
        rowImg.raycastTarget = false;
        rowGO.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(8, 8, 4, 4);

        Color textCol = entry.hasPlayedGame ? TextDark : TextLight;
        int fs = entry.hasPlayedGame ? 15 : 13;

        // Rank
        string rankStr = entry.rank > 0 ? $"{entry.rank}" : "-";
        var rankTMP = MakeCell(rowGO.transform, rankStr, 30, textCol, fs);
        if (entry.rank == 1 && entry.hasPlayedGame) rankTMP.color = GoldAccent;
        if (entry.rank <= 3 && entry.hasPlayedGame) rankTMP.fontStyle = FontStyles.Bold;

        // Name column (vertical: name + badge/status)
        var nameColGO = new GameObject("NameCol");
        nameColGO.transform.SetParent(rowGO.transform, false);
        var nameColLayout = nameColGO.AddComponent<VerticalLayoutGroup>();
        nameColLayout.spacing = 0;
        nameColLayout.childAlignment = TextAnchor.MiddleRight;
        nameColLayout.childForceExpandWidth = true;
        nameColLayout.childForceExpandHeight = false;
        nameColLayout.childControlWidth = true;
        nameColLayout.childControlHeight = true;
        nameColGO.AddComponent<LayoutElement>().flexibleWidth = 1;

        var nameTMP = MakeTMP(nameColGO.transform, H(entry.profileName), fs, textCol);
        nameTMP.alignment = TextAlignmentOptions.Right;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

        if (entry.isCurrentProfile)
        {
            var badge = MakeTMP(nameColGO.transform,
                H("\u05D4\u05E4\u05E8\u05D5\u05E4\u05D9\u05DC \u05D4\u05E0\u05D5\u05DB\u05D7\u05D9"), // הפרופיל הנוכחי
                10, Primary);
            badge.alignment = TextAlignmentOptions.Right;
            badge.gameObject.AddComponent<LayoutElement>().preferredHeight = 14;
        }
        else if (!entry.hasPlayedGame)
        {
            var status = MakeTMP(nameColGO.transform,
                H("\u05E2\u05D3\u05D9\u05D9\u05DF \u05DC\u05D0 \u05E9\u05D9\u05D7\u05E7"), // עדיין לא שיחק
                10, TextLight);
            status.alignment = TextAlignmentOptions.Right;
            status.fontStyle = FontStyles.Italic;
            status.gameObject.AddComponent<LayoutElement>().preferredHeight = 14;
        }

        if (entry.hasPlayedGame)
        {
            // Score
            var scTMP = MakeCell(rowGO.transform, $"{entry.score:F0}", 50,
                ScoreColor(entry.score), fs);
            scTMP.fontStyle = FontStyles.Bold;

            // Difficulty
            MakeCell(rowGO.transform, $"{entry.currentDifficulty}", 40, TextMedium, fs);

            // Sessions
            MakeCell(rowGO.transform, $"{entry.sessionsPlayed}", 55, TextMedium, fs);
        }
        else
        {
            // Empty spacers for alignment
            MakeEmptyCell(rowGO.transform, 50);
            MakeEmptyCell(rowGO.transform, 40);
            MakeEmptyCell(rowGO.transform, 55);
        }
    }

    // ── Helpers ──

    private static GameObject MakeRow(Transform parent, float height)
    {
        var go = new GameObject("Row");
        go.transform.SetParent(parent, false);
        var layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6;
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        go.AddComponent<LayoutElement>().preferredHeight = height;
        return go;
    }

    private static TextMeshProUGUI MakeCell(Transform parent, string text, float width,
        Color color, int fontSize, bool isBold = false, bool flex = false)
    {
        var tmp = MakeTMP(parent, text, fontSize, color);
        tmp.alignment = TextAlignmentOptions.Center;
        if (isBold) tmp.fontStyle = FontStyles.Bold;
        var le = tmp.gameObject.AddComponent<LayoutElement>();
        if (flex) le.flexibleWidth = 1;
        else le.preferredWidth = width;
        return tmp;
    }

    private static void MakeEmptyCell(Transform parent, float width)
    {
        var go = new GameObject("Empty");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        go.AddComponent<LayoutElement>().preferredWidth = width;
    }

    private static TextMeshProUGUI MakeTMP(Transform parent, string text, int fontSize, Color color)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.isRightToLeftText = true;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static string H(string raw) => HebrewFixer.Fix(raw);

    private static Color ScoreColor(float score)
    {
        if (score >= 70f) return HC("#27AE60");
        if (score >= 40f) return HC("#F39C12");
        return HC("#E74C3C");
    }

    private static Color HC(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
