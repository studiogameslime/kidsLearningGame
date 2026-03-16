using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Parent Dashboard controller. Manages parental gate, tab navigation,
/// and dynamically builds all dashboard content from analytics data.
/// Production-ready, Hebrew RTL, premium parent-facing UI.
/// </summary>
public class ParentDashboardController : MonoBehaviour
{
    [Header("Gate")]
    public RectTransform gatePanel;
    public TextMeshProUGUI questionText;
    public Button[] answerButtons;
    public TextMeshProUGUI[] answerLabels;

    [Header("Dashboard")]
    public RectTransform dashboardPanel;
    public TextMeshProUGUI headerNameText;
    public TextMeshProUGUI headerAgeText;
    public TextMeshProUGUI headerSessionsText;
    public Button backButton;

    [Header("Tabs")]
    public Button[] tabButtons;
    public Image[] tabIndicators;
    public RectTransform[] tabContents; // scroll content areas

    [Header("Leaderboard")]
    public Button trophyButton;
    public Sprite trophySprite;

    [Header("Assets")]
    public Sprite roundedRect;
    public Sprite circleSprite;

    // ── Colors ──
    private static readonly Color CardColor = HexColor("#FFFFFF");
    private static readonly Color BgColor = HexColor("#F0F2F5");
    private static readonly Color Primary = HexColor("#3498DB");
    private static readonly Color TextDark = HexColor("#2D3436");
    private static readonly Color TextMedium = HexColor("#636E72");
    private static readonly Color TextLight = HexColor("#B2BEC3");
    private static readonly Color BarBg = HexColor("#E8EBED");
    private static readonly Color Divider = HexColor("#E8EBED");
    private static readonly Color AccentGreen = HexColor("#27AE60");
    private static readonly Color AccentOrange = HexColor("#F39C12");
    private static readonly Color AccentRed = HexColor("#E74C3C");
    private static readonly Color StrengthBg = HexColor("#E8F5E9");
    private static readonly Color PracticeBg = HexColor("#FFF3E0");
    private static readonly Color InsightBg = HexColor("#E3F2FD");
    private static readonly Color BadgeBg = HexColor("#FFF8E1");
    private static readonly Color GoldAccent = HexColor("#F1C40F");
    private static readonly Color HighlightBg = HexColor("#E3F2FD");

    private ParentDashboardData _data;
    private int _correctAnswer;
    private GameObject _leaderboardModal;

    private void Start()
    {
        // Gate
        dashboardPanel.gameObject.SetActive(false);
        gatePanel.gameObject.SetActive(true);
        GenerateQuestion();

        for (int i = 0; i < answerButtons.Length; i++)
        {
            int idx = i;
            answerButtons[i].onClick.AddListener(() => OnAnswerTapped(idx));
        }

        if (backButton != null)
            backButton.onClick.AddListener(OnBackPressed);
        if (trophyButton != null)
            trophyButton.onClick.AddListener(ShowLeaderboard);
    }

    // ═══════════════════════════════════════════════════════════════
    //  PARENTAL GATE
    // ═══════════════════════════════════════════════════════════════

    private void GenerateQuestion()
    {
        int a = Random.Range(3, 9);
        int b = Random.Range(2, 8);
        _correctAnswer = a + b;

        questionText.text = $"? = {b} + {a}";
        questionText.isRightToLeftText = false;

        var answers = new List<int> { _correctAnswer };
        while (answers.Count < 4)
        {
            int wrong = _correctAnswer + Random.Range(-3, 4);
            if (wrong != _correctAnswer && wrong > 0 && !answers.Contains(wrong))
                answers.Add(wrong);
        }

        for (int i = answers.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = answers[i]; answers[i] = answers[j]; answers[j] = tmp;
        }

        for (int i = 0; i < 4; i++)
            answerLabels[i].text = answers[i].ToString();
    }

    private void OnAnswerTapped(int idx)
    {
        int answer = int.Parse(answerLabels[idx].text);
        if (answer == _correctAnswer)
            OnGatePassed();
        else
        {
            StartCoroutine(ShakeButton(answerButtons[idx].GetComponent<RectTransform>()));
            GenerateQuestion();
        }
    }

    private IEnumerator ShakeButton(RectTransform rt)
    {
        Vector2 orig = rt.anchoredPosition;
        for (int i = 0; i < 4; i++)
        {
            rt.anchoredPosition = orig + new Vector2(i % 2 == 0 ? 8 : -8, 0);
            yield return new WaitForSeconds(0.05f);
        }
        rt.anchoredPosition = orig;
    }

    private void OnGatePassed()
    {
        gatePanel.gameObject.SetActive(false);
        dashboardPanel.gameObject.SetActive(true);
        LoadData();
        BuildAllTabs();
    }

    // ═══════════════════════════════════════════════════════════════
    //  DATA
    // ═══════════════════════════════════════════════════════════════

    private void LoadData()
    {
        _data = ParentDashboardViewModel.Build();
        if (_data == null) return;

        if (headerNameText != null)
        {
            headerNameText.text = H(_data.profileName);
            headerNameText.isRightToLeftText = false;
            headerNameText.enableWordWrapping = false;
        }
        if (headerAgeText != null)
        {
            string ageLabel = _data.ageDisplay != "---"
                ? $"\u05D2\u05D9\u05DC {_data.ageDisplay}" // גיל X
                : "";
            headerAgeText.text = H(ageLabel);
            headerAgeText.isRightToLeftText = false;
            headerAgeText.enableWordWrapping = false;
        }
        if (headerSessionsText != null)
        {
            string sessLabel = $"{_data.totalSessions} \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD"; // X משחקים
            headerSessionsText.text = H(sessLabel);
            headerSessionsText.isRightToLeftText = false;
            headerSessionsText.enableWordWrapping = false;
        }

        // Force layout rebuild so ContentSizeFitter recalculates after text changes
        if (headerNameText != null)
        {
            var subRow = headerNameText.transform.parent;
            if (subRow != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(subRow.GetComponent<RectTransform>());
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  TAB NAVIGATION
    // ═══════════════════════════════════════════════════════════════

    private void BuildAllTabs()
    {
        // Build all tab content into a single combined scroll view (index 0)
        // and hide the separate scroll views (1-3)
        for (int i = 0; i < tabContents.Length; i++)
        {
            if (i == 0)
            {
                tabContents[i].parent.gameObject.SetActive(true);
                BuildOverviewTab();
                // Append other tab content into the same scroll view
                BuildGamesTab();
                BuildCategoriesTab();
                BuildTrendsTab();
            }
            else
            {
                tabContents[i].parent.gameObject.SetActive(false);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  OVERVIEW TAB
    // ═══════════════════════════════════════════════════════════════

    private void BuildOverviewTab()
    {
        if (_data == null) return;
        var parent = tabContents[0];

        // ── Overall Score Card ──
        var scoreCard = MakeCard(parent);
        var scoreRow = MakeHRow(scoreCard, 120, TextAnchor.MiddleCenter);

        // Large circular score
        var scoreBadgeGO = new GameObject("ScoreBadge");
        scoreBadgeGO.transform.SetParent(scoreRow.transform, false);
        var scoreBadgeLE = scoreBadgeGO.AddComponent<LayoutElement>();
        scoreBadgeLE.preferredWidth = 100;
        scoreBadgeLE.preferredHeight = 100;
        var scoreBadgeImg = scoreBadgeGO.AddComponent<Image>();
        if (circleSprite != null) scoreBadgeImg.sprite = circleSprite;
        scoreBadgeImg.color = ParentDashboardViewModel.ScoreColor(_data.overallScore);
        var scoreValTMP = AddChildTMP(scoreBadgeGO.transform, $"{_data.overallScore:F0}",
            36, Color.white, TextAlignmentOptions.Center);
        scoreValTMP.fontStyle = FontStyles.Bold;

        // Score info column
        var scoreInfoGO = MakeVCol(scoreRow.transform);
        var scoreInfoLE = scoreInfoGO.AddComponent<LayoutElement>();
        scoreInfoLE.flexibleWidth = 1;
        scoreInfoLE.preferredHeight = 100;

        var scoreTitleTMP = AddChildTMP(scoreInfoGO.transform, H("\u05E6\u05D9\u05D5\u05DF \u05DB\u05DC\u05DC\u05D9"), // ציון כללי
            22, TextDark, TextAlignmentOptions.Right);
        scoreTitleTMP.fontStyle = FontStyles.Bold;
        scoreTitleTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;

        MakeProgressBar(scoreInfoGO.transform, _data.overallScore / 100f,
            ParentDashboardViewModel.ScoreColor(_data.overallScore), 12f);

        var statusTMP = AddChildTMP(scoreInfoGO.transform, H(_data.overallScoreLabel),
            16, TextMedium, TextAlignmentOptions.Right);
        statusTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;

        var trendTMP2 = AddChildTMP(scoreInfoGO.transform,
            H($"{ParentDashboardViewModel.TrendArrow(_data.overallTrend)} {_data.overallTrendLabel}"),
            14, TextMedium, TextAlignmentOptions.Right);
        trendTMP2.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

        FitCard(scoreCard);

        // ── Quick Stats Card ──
        var statsCard = MakeCard(parent);
        MakeSectionTitle(statsCard, "\u05E1\u05D8\u05D8\u05D9\u05E1\u05D8\u05D9\u05E7\u05D5\u05EA"); // סטטיסטיקות

        var statsGrid = new GameObject("StatsGrid");
        statsGrid.transform.SetParent(statsCard, false);
        var gridLayout = statsGrid.AddComponent<GridLayoutGroup>();
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 2;
        gridLayout.cellSize = new Vector2(230, 70);
        gridLayout.spacing = new Vector2(12, 8);
        gridLayout.childAlignment = TextAnchor.UpperCenter;
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperRight;
        var gridLE = statsGrid.AddComponent<LayoutElement>();
        gridLE.preferredHeight = 314;

        MakeStatCell(statsGrid.transform, $"{_data.totalSessions}", "\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD"); // משחקים
        MakeStatCell(statsGrid.transform, H(_data.totalPlayTimeDisplay), "\u05D6\u05DE\u05DF \u05DE\u05E9\u05D7\u05E7 \u05DB\u05D5\u05DC\u05DC"); // זמן משחק כולל
        MakeStatCell(statsGrid.transform, $"{_data.gamesPlayedCount}", "\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05E9\u05D5\u05E0\u05D9\u05DD"); // משחקים שונים
        MakeStatCell(statsGrid.transform, H(_data.favoriteGameName), "\u05DE\u05E9\u05D7\u05E7 \u05D0\u05D4\u05D5\u05D1"); // משחק אהוב
        MakeStatCell(statsGrid.transform, $"{_data.totalBubblesPopped}", "\u05D1\u05D5\u05E2\u05D5\u05EA \u05E9\u05E4\u05E7\u05E2\u05D5"); // בועות שפקעו
        MakeStatCell(statsGrid.transform, H(_data.thisWeekPlayTimeDisplay), "\u05D6\u05DE\u05DF \u05DE\u05E9\u05D7\u05E7 \u05D4\u05E9\u05D1\u05D5\u05E2"); // זמן משחק השבוע
        MakeStatCell(statsGrid.transform, H(_data.engagementLabel), "\u05DE\u05E2\u05D5\u05E8\u05D1\u05D5\u05EA"); // מעורבות

        FitCard(statsCard);

        // ── Insights Card ──
        if (_data.insights.Count > 0)
        {
            var insightCard = MakeCard(parent);
            MakeSectionTitle(insightCard, "\u05EA\u05D5\u05D1\u05E0\u05D5\u05EA \u05E2\u05DC \u05D4\u05D9\u05DC\u05D3"); // תובנות על הילד

            foreach (var insight in _data.insights)
            {
                var insightRow = new GameObject("InsightRow");
                insightRow.transform.SetParent(insightCard, false);
                var insightImg = insightRow.AddComponent<Image>();
                insightImg.sprite = roundedRect;
                insightImg.type = Image.Type.Sliced;
                insightImg.color = InsightBg;
                insightImg.raycastTarget = false;

                var insightLayout = insightRow.AddComponent<VerticalLayoutGroup>();
                insightLayout.spacing = 2;
                insightLayout.padding = new RectOffset(16, 16, 10, 10);
                insightLayout.childForceExpandWidth = true;
                insightLayout.childForceExpandHeight = false;
                insightLayout.childControlWidth = true;
                insightLayout.childControlHeight = true;

                var titleTMP = AddChildTMP(insightRow.transform,
                    H(insight.titleHebrew), 15, Primary, TextAlignmentOptions.Right);
                titleTMP.fontStyle = FontStyles.Bold;
                titleTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;

                var descTMP = AddChildTMP(insightRow.transform,
                    H(insight.descriptionHebrew), 14, TextDark, TextAlignmentOptions.Right);
                descTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;

                insightRow.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            FitCard(insightCard);
        }

        // ── Badges ──
        if (_data.badges.Count > 0)
        {
            var badgeCard = MakeCard(parent);
            MakeSectionTitle(badgeCard, "\u05D4\u05D9\u05E9\u05D2\u05D9\u05DD"); // הישגים

            var badgeGrid = new GameObject("BadgeGrid");
            badgeGrid.transform.SetParent(badgeCard, false);
            var bGridLayout = badgeGrid.AddComponent<GridLayoutGroup>();
            bGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            bGridLayout.constraintCount = 2;
            bGridLayout.cellSize = new Vector2(230, 72);
            bGridLayout.spacing = new Vector2(8, 8);
            bGridLayout.childAlignment = TextAnchor.UpperCenter;
            bGridLayout.startCorner = GridLayoutGroup.Corner.UpperRight;

            int rows = Mathf.CeilToInt(_data.badges.Count / 2f);
            badgeGrid.AddComponent<LayoutElement>().preferredHeight = rows * 80;

            foreach (var badge in _data.badges)
            {
                var badgeGO = new GameObject("Badge");
                badgeGO.transform.SetParent(badgeGrid.transform, false);
                var bImg = badgeGO.AddComponent<Image>();
                bImg.sprite = roundedRect;
                bImg.type = Image.Type.Sliced;
                bImg.color = BadgeBg;
                bImg.raycastTarget = false;

                var bLayout = badgeGO.AddComponent<VerticalLayoutGroup>();
                bLayout.spacing = 2;
                bLayout.padding = new RectOffset(10, 10, 8, 8);
                bLayout.childAlignment = TextAnchor.MiddleCenter;
                bLayout.childForceExpandWidth = true;
                bLayout.childForceExpandHeight = false;
                bLayout.childControlWidth = true;
                bLayout.childControlHeight = true;

                var bTitle = AddChildTMP(badgeGO.transform, H(badge.titleHebrew),
                    14, TextDark, TextAlignmentOptions.Center);
                bTitle.fontStyle = FontStyles.Bold;
                bTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

                var bSub = AddChildTMP(badgeGO.transform, H(badge.subtitleHebrew),
                    11, TextMedium, TextAlignmentOptions.Center);
                bSub.gameObject.AddComponent<LayoutElement>().preferredHeight = 16;
            }

            FitCard(badgeCard);
        }

        // ── Strengths + Practice Areas (side by side) ──
        if (_data.strongestCategories.Count > 0 || _data.weakestCategories.Count > 0)
        {
            var dualRow = MakeHRow(parent, 0, TextAnchor.UpperCenter);
            dualRow.GetComponent<HorizontalLayoutGroup>().spacing = 12;
            dualRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            dualRow.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;
            var dualFit = dualRow.AddComponent<ContentSizeFitter>();
            dualFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (_data.strongestCategories.Count > 0)
            {
                var strengthGO = MakeInlineCard(dualRow.transform, StrengthBg);
                var strengthLayout = strengthGO.GetComponent<VerticalLayoutGroup>();
                strengthLayout.padding = new RectOffset(16, 16, 12, 12);
                strengthLayout.spacing = 6;

                var stTitle = AddChildTMP(strengthGO.transform,
                    H("\u05D7\u05D6\u05E7\u05D5\u05EA"), // חזקות
                    18, AccentGreen, TextAlignmentOptions.Right);
                stTitle.fontStyle = FontStyles.Bold;
                stTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

                foreach (var cat in _data.strongestCategories)
                    MakeMiniChip(strengthGO.transform, cat, AccentGreen);
            }

            if (_data.weakestCategories.Count > 0)
            {
                var practiceGO = MakeInlineCard(dualRow.transform, PracticeBg);
                var practiceLayout = practiceGO.GetComponent<VerticalLayoutGroup>();
                practiceLayout.padding = new RectOffset(16, 16, 12, 12);
                practiceLayout.spacing = 6;

                var prTitle = AddChildTMP(practiceGO.transform,
                    H("\u05EA\u05D7\u05D5\u05DE\u05D9 \u05EA\u05E8\u05D2\u05D5\u05DC"), // תחומי תרגול
                    18, AccentOrange, TextAlignmentOptions.Right);
                prTitle.fontStyle = FontStyles.Bold;
                prTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

                foreach (var cat in _data.weakestCategories)
                    MakeMiniChip(practiceGO.transform, cat, AccentOrange);
            }
        }

        // ── Development Categories ──
        var catCard = MakeCard(parent);
        MakeSectionTitle(catCard, "\u05E7\u05D8\u05D2\u05D5\u05E8\u05D9\u05D5\u05EA \u05D4\u05EA\u05E4\u05EA\u05D7\u05D5\u05EA"); // קטגוריות התפתחות

        foreach (var cat in _data.categories)
        {
            if (cat.contributingGamesCount == 0) continue;
            MakeCategoryRow(catCard, cat);
        }

        FitCard(catCard);
        MakeSpacer(parent, 40f);
    }

    // ═══════════════════════════════════════════════════════════════
    //  GAMES TAB
    // ═══════════════════════════════════════════════════════════════

    private void BuildGamesTab()
    {
        if (_data == null) return;
        var parent = tabContents[0];

        // Section header
        MakeSectionDivider(parent, "\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD"); // משחקים

        if (_data.games.Count == 0)
        {
            var noData = AddChildTMP(parent, H("\u05E2\u05D5\u05D3 \u05D0\u05D9\u05DF \u05E0\u05EA\u05D5\u05E0\u05D9\u05DD"), // עוד אין נתונים
                20, TextMedium, TextAlignmentOptions.Center);
            noData.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
            return;
        }

        foreach (var game in _data.games)
            MakeGameCard(parent, game);

        MakeSpacer(parent, 40f);
    }

    private void MakeGameCard(Transform parent, GameDashboardData game)
    {
        var card = MakeCard(parent);

        // ── Header row: score badge + name + trend ──
        var headerRow = MakeHRow(card, 60, TextAnchor.MiddleRight);
        headerRow.GetComponent<HorizontalLayoutGroup>().spacing = 12;

        // Score badge
        var badgeGO = new GameObject("Badge");
        badgeGO.transform.SetParent(headerRow.transform, false);
        var badgeLE = badgeGO.AddComponent<LayoutElement>();
        badgeLE.preferredWidth = 48;
        badgeLE.preferredHeight = 48;
        var badgeImg = badgeGO.AddComponent<Image>();
        if (circleSprite != null) badgeImg.sprite = circleSprite;
        badgeImg.color = ParentDashboardViewModel.ScoreColor(game.score);
        var badgeTMP = AddChildTMP(badgeGO.transform, $"{game.score:F0}",
            16, Color.white, TextAlignmentOptions.Center);
        badgeTMP.fontStyle = FontStyles.Bold;

        // Name + subtitle column
        var infoCol = MakeVCol(headerRow.transform);
        var infoLE = infoCol.AddComponent<LayoutElement>();
        infoLE.flexibleWidth = 1;
        infoLE.preferredHeight = 50;

        var nameTMP = AddChildTMP(infoCol.transform, H(game.gameName), 19, TextDark, TextAlignmentOptions.Right);
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;

        string sub = $"{ParentDashboardViewModel.TrendArrow(game.trend)} " +
            H($"{game.sessionsPlayed} \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD"); // X משחקים
        var subTMP = AddChildTMP(infoCol.transform, sub, 14, TextMedium, TextAlignmentOptions.Right);
        subTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

        // ── Quick stats row ──
        MakeDivider(card);
        var quickRow = MakeHRow(card, 28, TextAnchor.MiddleCenter);
        quickRow.GetComponent<HorizontalLayoutGroup>().spacing = 16;
        quickRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;

        AddMiniStat(quickRow.transform, $"{game.accuracy:P0}",
            H("\u05D3\u05D9\u05D5\u05E7")); // דיוק
        AddMiniStat(quickRow.transform, $"{game.completionRate:P0}",
            H("\u05D0\u05D7\u05D5\u05D6 \u05D4\u05E9\u05DC\u05DE\u05D4")); // אחוז השלמה
        AddMiniStat(quickRow.transform, H(game.lastPlayedDisplay),
            H("\u05E9\u05D5\u05D7\u05E7 \u05DC\u05D0\u05D7\u05E8\u05D5\u05E0\u05D4")); // שוחק לאחרונה

        // ── Difficulty panel ──
        MakeDivider(card);
        string gameId = game.gameId;
        bool isManual = game.manualDifficultyOverride;

        // Row 1: Active difficulty + control
        var diffRow = MakeHRow(card, 50, TextAnchor.MiddleRight);
        diffRow.GetComponent<HorizontalLayoutGroup>().spacing = 8;
        diffRow.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 4, 4);

        // Minus button
        var minusBtn = MakeSmallButton(diffRow.transform, "\u2212", 28); // −
        // Difficulty display
        var diffDisplayGO = new GameObject("DiffDisplay");
        diffDisplayGO.transform.SetParent(diffRow.transform, false);
        var diffDisplayLE = diffDisplayGO.AddComponent<LayoutElement>();
        diffDisplayLE.preferredWidth = 60;
        diffDisplayLE.preferredHeight = 40;
        var diffBgImg = diffDisplayGO.AddComponent<Image>();
        diffBgImg.sprite = roundedRect;
        diffBgImg.type = Image.Type.Sliced;
        diffBgImg.color = HexColor("#EBF5FB");
        var diffValTMP = AddChildTMP(diffDisplayGO.transform, $"{game.currentDifficulty}",
            22, Primary, TextAlignmentOptions.Center);
        diffValTMP.fontStyle = FontStyles.Bold;
        // Plus button
        var plusBtn = MakeSmallButton(diffRow.transform, "+", 28);

        // Label
        var diffLabelTMP = AddChildTMP(diffRow.transform,
            H("\u05E8\u05DE\u05EA \u05E7\u05D5\u05E9\u05D9"), // רמת קושי
            15, TextMedium, TextAlignmentOptions.Right);
        diffLabelTMP.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        // Auto/manual chip
        var modeLabelTMP = AddChildTMP(diffRow.transform,
            isManual ? H("\u05E7\u05D5\u05E9\u05D9 \u05D9\u05D3\u05E0\u05D9") : H("\u05E7\u05D5\u05E9\u05D9 \u05D0\u05D5\u05D8\u05D5\u05DE\u05D8\u05D9"), // קושי ידני / קושי אוטומטי
            12, isManual ? AccentOrange : AccentGreen, TextAlignmentOptions.Center);
        modeLabelTMP.gameObject.AddComponent<LayoutElement>().preferredWidth = 90;

        // Row 2: Active difficulty impact label
        var impactTMP = AddChildTMP(card, H(game.activeDifficultyImpact),
            14, Primary, TextAlignmentOptions.Right);
        impactTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;

        // Row 3: If manual override, show recommended + reset button
        GameObject resetBtnGO = null;
        TextMeshProUGUI recTMP = null;
        TextMeshProUGUI recImpactTMP = null;

        if (isManual)
        {
            MakeSpacer(card, 4f);

            // Recommended info row
            var recCard = MakeInlineCard(card, HexColor("#F0FFF0"));
            recCard.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(12, 12, 8, 8);
            recCard.GetComponent<VerticalLayoutGroup>().spacing = 2;

            // "רמה מומלצת: X"
            string recLabel = $"\u05E8\u05DE\u05D4 \u05DE\u05D5\u05DE\u05DC\u05E6\u05EA: {game.recommendedDifficulty}"; // רמה מומלצת: X
            recTMP = AddChildTMP(recCard.transform, H(recLabel), 14, AccentGreen, TextAlignmentOptions.Right);
            recTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

            // Recommended impact
            recImpactTMP = AddChildTMP(recCard.transform, H(game.recommendedDifficultyImpact), 13, TextMedium, TextAlignmentOptions.Right);
            recImpactTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;

            MakeSpacer(card, 4f);

            // Reset button
            resetBtnGO = new GameObject("ResetBtn");
            resetBtnGO.transform.SetParent(card, false);
            var resetLE = resetBtnGO.AddComponent<LayoutElement>();
            resetLE.preferredHeight = 36;
            var resetImg = resetBtnGO.AddComponent<Image>();
            if (roundedRect != null) resetImg.sprite = roundedRect;
            resetImg.type = Image.Type.Sliced;
            resetImg.color = HexColor("#E8F5E9");
            var resetBtn = resetBtnGO.AddComponent<Button>();
            resetBtn.targetGraphic = resetImg;
            var resetColors = resetBtn.colors;
            resetColors.highlightedColor = HexColor("#C8E6C9");
            resetColors.pressedColor = HexColor("#A5D6A7");
            resetBtn.colors = resetColors;

            // Reset button label
            var resetLabelGO = new GameObject("Label");
            resetLabelGO.transform.SetParent(resetBtnGO.transform, false);
            var resetLabelRT = resetLabelGO.AddComponent<RectTransform>();
            resetLabelRT.anchorMin = Vector2.zero;
            resetLabelRT.anchorMax = Vector2.one;
            resetLabelRT.offsetMin = Vector2.zero;
            resetLabelRT.offsetMax = Vector2.zero;
            var resetLabelTMP = resetLabelGO.AddComponent<TextMeshProUGUI>();
            resetLabelTMP.text = H("\u05D7\u05D6\u05E8\u05D4 \u05DC\u05E8\u05DE\u05D4 \u05D4\u05DE\u05D5\u05DE\u05DC\u05E6\u05EA"); // חזרה לרמה המומלצת
            resetLabelTMP.fontSize = 14;
            resetLabelTMP.color = AccentGreen;
            resetLabelTMP.alignment = TextAlignmentOptions.Center;
            resetLabelTMP.isRightToLeftText = false;
            resetLabelTMP.enableWordWrapping = false;
            resetLabelTMP.raycastTarget = false;
        }

        // Wire difficulty buttons — capture references for live update
        var capturedImpactTMP = impactTMP;
        var capturedRecTMP = recTMP;
        var capturedRecImpactTMP = recImpactTMP;
        var capturedResetGO = resetBtnGO;
        var capturedCard = card;
        var capturedScrollContent = parent;

        minusBtn.onClick.AddListener(() => ChangeDifficultyFull(
            gameId, -1, diffValTMP, modeLabelTMP, capturedImpactTMP,
            capturedCard, capturedScrollContent));
        plusBtn.onClick.AddListener(() => ChangeDifficultyFull(
            gameId, +1, diffValTMP, modeLabelTMP, capturedImpactTMP,
            capturedCard, capturedScrollContent));

        if (resetBtnGO != null)
        {
            resetBtnGO.GetComponent<Button>().onClick.AddListener(() => ResetDifficultyOverride(
                gameId, diffValTMP, modeLabelTMP, capturedImpactTMP,
                capturedResetGO, capturedRecTMP != null ? capturedRecTMP.transform.parent.gameObject : null,
                capturedCard, capturedScrollContent));
        }

        // ── Details panel (hidden) ──
        var detailsGO = new GameObject("Details");
        detailsGO.transform.SetParent(card, false);
        detailsGO.SetActive(false);
        var detailsLayout = detailsGO.AddComponent<VerticalLayoutGroup>();
        detailsLayout.spacing = 6;
        detailsLayout.padding = new RectOffset(0, 0, 8, 8);
        detailsLayout.childForceExpandWidth = true;
        detailsLayout.childForceExpandHeight = false;
        detailsGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        MakeDivider(detailsGO.transform);

        // Extended metrics
        MakeDetailRow(detailsGO.transform, "\u05D6\u05DE\u05DF \u05DE\u05E9\u05D7\u05E7 \u05DB\u05D5\u05DC\u05DC", H(game.totalPlayTimeDisplay)); // זמן משחק כולל
        MakeDetailRow(detailsGO.transform, "\u05DE\u05E9\u05DA \u05DE\u05E9\u05D7\u05E7 \u05DE\u05DE\u05D5\u05E6\u05E2", H(game.averageSessionDisplay)); // משך משחק ממוצע
        MakeDetailRow(detailsGO.transform, "\u05D3\u05D9\u05D5\u05E7", $"{game.accuracy:P0}"); // דיוק
        MakeDetailRow(detailsGO.transform, "\u05E9\u05D2\u05D9\u05D0\u05D5\u05EA \u05DE\u05DE\u05D5\u05E6\u05E2", $"{game.mistakeRate:F1}"); // שגיאות ממוצע
        MakeDetailRow(detailsGO.transform, "\u05E7\u05E6\u05D1 \u05D4\u05E9\u05DC\u05DE\u05D4", $"{game.completionRate:P0}"); // קצב השלמה
        MakeDetailRow(detailsGO.transform, "\u05E6\u05D9\u05D5\u05DF \u05DE\u05D4\u05D9\u05E8\u05D5\u05EA", $"{game.speedScore:F0}"); // ציון מהירות
        MakeDetailRow(detailsGO.transform, "\u05E2\u05E6\u05DE\u05D0\u05D5\u05EA", $"{game.independenceScore:F0}"); // עצמאות
        MakeDetailRow(detailsGO.transform, "\u05E8\u05DE\u05D4 \u05D2\u05D1\u05D5\u05D4\u05D4 \u05D1\u05D9\u05D5\u05EA\u05E8", $"{game.highestDifficulty}/10"); // רמה גבוהה ביותר
        MakeDetailRow(detailsGO.transform, "\u05E8\u05E6\u05E3 \u05D4\u05E6\u05DC\u05D7\u05D5\u05EA \u05D4\u05DB\u05D9 \u05D0\u05E8\u05D5\u05DA", $"{game.maxStreak}"); // רצף הצלחות הכי ארוך

        // Label-based metrics
        if (!string.IsNullOrEmpty(game.hintUsageLabel))
            MakeDetailRow(detailsGO.transform, "\u05E9\u05D9\u05DE\u05D5\u05E9 \u05D1\u05E8\u05DE\u05D6\u05D9\u05DD", H(game.hintUsageLabel)); // שימוש ברמזים
        if (!string.IsNullOrEmpty(game.persistenceLabel))
            MakeDetailRow(detailsGO.transform, "\u05D4\u05EA\u05DE\u05D3\u05D4", H(game.persistenceLabel)); // התמדה
        if (!string.IsNullOrEmpty(game.difficultyBalanceLabel))
            MakeDetailRow(detailsGO.transform, "\u05D0\u05D9\u05D6\u05D5\u05DF \u05E7\u05D5\u05E9\u05D9", H(game.difficultyBalanceLabel)); // איזון קושי
        if (!string.IsNullOrEmpty(game.trendLabel))
            MakeDetailRow(detailsGO.transform, "\u05DE\u05D2\u05DE\u05D4", H(game.trendLabel)); // מגמה

        MakeDetailRow(detailsGO.transform, "\u05DE\u05E9\u05D7\u05E7 \u05D0\u05D7\u05E8\u05D5\u05DF",
            H(game.lastPlayedDisplay)); // משחק אחרון

        if (!string.IsNullOrEmpty(game.insightText))
        {
            var insight = AddChildTMP(detailsGO.transform, H(game.insightText), 15, Primary, TextAlignmentOptions.Right);
            insight.fontStyle = FontStyles.Italic;
            insight.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
        }

        // Recent sessions
        if (game.recentSessions.Count > 0)
        {
            MakeDivider(detailsGO.transform);
            var sessTitle = AddChildTMP(detailsGO.transform,
                H("\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05D0\u05D7\u05E8\u05D5\u05E0\u05D9\u05DD"), // משחקים אחרונים
                16, TextDark, TextAlignmentOptions.Right);
            sessTitle.fontStyle = FontStyles.Bold;
            sessTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;

            int showCount = Mathf.Min(5, game.recentSessions.Count);
            for (int i = game.recentSessions.Count - 1; i >= game.recentSessions.Count - showCount; i--)
            {
                var s = game.recentSessions[i];
                string status = s.completed ? "\u2713" : "\u2717";
                string line = $"{status} {ParentDashboardViewModel.FormatDate(s.timestamp)} | " +
                    $"\u05E8\u05DE\u05D4 {s.difficulty} | {s.accuracy:P0} | {s.mistakes} \u05E9\u05D2\u05D9\u05D0\u05D5\u05EA";
                var sessTMP = AddChildTMP(detailsGO.transform, line, 13, TextMedium, TextAlignmentOptions.Right);
                sessTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
            }
        }

        // Tap header to expand/collapse
        var expandBtn = headerRow.AddComponent<Button>();
        expandBtn.transition = Selectable.Transition.None;
        var scrollContent = parent;
        expandBtn.onClick.AddListener(() =>
        {
            detailsGO.SetActive(!detailsGO.activeSelf);
            LayoutRebuilder.ForceRebuildLayoutImmediate(card.GetComponent<RectTransform>());
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent.GetComponent<RectTransform>());
        });

        FitCard(card);
    }

    // ═══════════════════════════════════════════════════════════════
    //  CATEGORIES TAB
    // ═══════════════════════════════════════════════════════════════

    private void BuildCategoriesTab()
    {
        if (_data == null) return;
        var parent = tabContents[0];

        // Section header
        MakeSectionDivider(parent, "\u05EA\u05D7\u05D5\u05DE\u05D9\u05DD"); // תחומים

        foreach (var cat in _data.categories)
        {
            if (cat.contributingGamesCount == 0) continue;
            MakeCategoryCard(parent, cat);
        }

        MakeSpacer(parent, 40f);
    }

    private void MakeCategoryCard(Transform parent, CategoryDashboardData cat)
    {
        var card = MakeCard(parent);

        // Color accent line at top
        var accent = new GameObject("Accent");
        accent.transform.SetParent(card, false);
        accent.transform.SetAsFirstSibling();
        var accentImg = accent.AddComponent<Image>();
        accentImg.color = cat.color;
        accentImg.raycastTarget = false;
        accent.AddComponent<LayoutElement>().preferredHeight = 4;

        // Header row
        var headerRow = MakeHRow(card, 56, TextAnchor.MiddleRight);
        headerRow.GetComponent<HorizontalLayoutGroup>().spacing = 12;

        // Score pill
        var scoreGO = new GameObject("Score");
        scoreGO.transform.SetParent(headerRow.transform, false);
        var scoreLEComp = scoreGO.AddComponent<LayoutElement>();
        scoreLEComp.preferredWidth = 52;
        scoreLEComp.preferredHeight = 30;
        var scoreImg = scoreGO.AddComponent<Image>();
        if (roundedRect != null) scoreImg.sprite = roundedRect;
        scoreImg.type = Image.Type.Sliced;
        scoreImg.color = cat.color;
        var scoreTMP = AddChildTMP(scoreGO.transform, $"{cat.score:F0}", 16, Color.white, TextAlignmentOptions.Center);
        scoreTMP.fontStyle = FontStyles.Bold;

        // Name + trend + confidence
        var nameCol = MakeVCol(headerRow.transform);
        nameCol.AddComponent<LayoutElement>().flexibleWidth = 1;

        var catNameTMP = AddChildTMP(nameCol.transform, H(cat.categoryName), 18, TextDark, TextAlignmentOptions.Right);
        catNameTMP.fontStyle = FontStyles.Bold;
        catNameTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;

        string trendStr = $"{ParentDashboardViewModel.TrendArrow(cat.trend)} {H(cat.trendLabel)}";
        var trendTMP = AddChildTMP(nameCol.transform, trendStr, 13, TextMedium, TextAlignmentOptions.Right);
        trendTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;

        // Progress bar
        MakeProgressBar(card, cat.score / 100f, cat.color, 10f);

        // Confidence + summary
        var confLine = $"{H(cat.confidenceLabel)} | {H(cat.insightText)}";
        var confTMP = AddChildTMP(card, confLine, 12, TextLight, TextAlignmentOptions.Right);
        confTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;

        // Details (hidden)
        var detailsGO = new GameObject("Details");
        detailsGO.transform.SetParent(card, false);
        detailsGO.SetActive(false);
        var detailsL = detailsGO.AddComponent<VerticalLayoutGroup>();
        detailsL.spacing = 6;
        detailsL.padding = new RectOffset(0, 0, 8, 8);
        detailsL.childForceExpandWidth = true;
        detailsL.childForceExpandHeight = false;
        detailsGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        MakeDivider(detailsGO.transform);

        // Summary text
        if (!string.IsNullOrEmpty(cat.summaryText))
        {
            var summaryTMP = AddChildTMP(detailsGO.transform, H(cat.summaryText),
                14, TextDark, TextAlignmentOptions.Right);
            summaryTMP.fontStyle = FontStyles.Italic;
            summaryTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
        }

        if (cat.contributions.Count > 0)
        {
            var gTitle = AddChildTMP(detailsGO.transform,
                H("\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05EA\u05D5\u05E8\u05DE\u05D9\u05DD"), // משחקים תורמים
                16, TextDark, TextAlignmentOptions.Right);
            gTitle.fontStyle = FontStyles.Bold;
            gTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;

            foreach (var contrib in cat.contributions)
            {
                var cRow = MakeHRow(detailsGO.transform, 24, TextAnchor.MiddleRight);
                cRow.GetComponent<HorizontalLayoutGroup>().spacing = 8;

                var cScTMP = AddChildTMP(cRow.transform, $"{contrib.gameScore:F0}",
                    14, cat.color, TextAlignmentOptions.Center);
                cScTMP.fontStyle = FontStyles.Bold;
                cScTMP.gameObject.AddComponent<LayoutElement>().preferredWidth = 30;

                AddChildTMP(cRow.transform, H(contrib.gameName),
                    14, TextMedium, TextAlignmentOptions.Right)
                    .gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            }
        }

        // Tap to expand
        var expandBtn = headerRow.AddComponent<Button>();
        expandBtn.transition = Selectable.Transition.None;
        var scrollContent = parent;
        expandBtn.onClick.AddListener(() =>
        {
            detailsGO.SetActive(!detailsGO.activeSelf);
            LayoutRebuilder.ForceRebuildLayoutImmediate(card.GetComponent<RectTransform>());
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent.GetComponent<RectTransform>());
        });

        FitCard(card);
    }

    // ═══════════════════════════════════════════════════════════════
    //  TRENDS TAB
    // ═══════════════════════════════════════════════════════════════

    private void BuildTrendsTab()
    {
        if (_data == null) return;
        var parent = tabContents[0];

        // Section header
        MakeSectionDivider(parent, "\u05DE\u05D2\u05DE\u05D5\u05EA"); // מגמות

        // Overall trend
        var overallCard = MakeCard(parent);
        MakeSectionTitle(overallCard, "\u05DE\u05D2\u05DE\u05D4 \u05DB\u05DC\u05DC\u05D9\u05EA"); // מגמה כללית

        string overallTrend = $"{ParentDashboardViewModel.TrendArrow(_data.overallTrend)} {H(_data.overallTrendLabel)}";
        var trendTMP = AddChildTMP(overallCard, overallTrend, 22, Primary, TextAlignmentOptions.Center);
        trendTMP.fontStyle = FontStyles.Bold;
        trendTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;

        MakeStatRow(overallCard, "\u05E1\u05D4\"\u05DB \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD", $"{_data.totalSessions}"); // סה"כ משחקים
        MakeStatRow(overallCard, "\u05D6\u05DE\u05DF \u05DE\u05E9\u05D7\u05E7 \u05DB\u05D5\u05DC\u05DC", H(_data.totalPlayTimeDisplay)); // זמן משחק כולל
        MakeStatRow(overallCard, "\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05D4\u05E9\u05D1\u05D5\u05E2", $"{_data.thisWeekSessions}"); // משחקים השבוע
        MakeStatRow(overallCard, "\u05D6\u05DE\u05DF \u05DE\u05E9\u05D7\u05E7 \u05D4\u05E9\u05D1\u05D5\u05E2", H(_data.thisWeekPlayTimeDisplay)); // זמן משחק השבוע

        // Exploration + play style
        if (!string.IsNullOrEmpty(_data.explorationLabel))
            MakeStatRow(overallCard, "\u05D7\u05E7\u05D9\u05E8\u05D4", H(_data.explorationLabel)); // חקירה
        if (!string.IsNullOrEmpty(_data.playStyleLabel))
            MakeStatRow(overallCard, "\u05E1\u05D2\u05E0\u05D5\u05DF \u05DE\u05E9\u05D7\u05E7", H(_data.playStyleLabel)); // סגנון משחק

        FitCard(overallCard);

        // Game trend groups
        var improving = new List<GameDashboardData>();
        var stable = new List<GameDashboardData>();
        var declining = new List<GameDashboardData>();

        foreach (var g in _data.games)
        {
            if (g.trend > 2f) improving.Add(g);
            else if (g.trend < -2f) declining.Add(g);
            else stable.Add(g);
        }

        if (improving.Count > 0)
            MakeTrendGroup(parent, "\u05DE\u05E9\u05EA\u05E4\u05E8\u05D9\u05DD \u2191", improving, AccentGreen); // משתפרים ↑
        if (stable.Count > 0)
            MakeTrendGroup(parent, "\u05D9\u05E6\u05D9\u05D1\u05D9\u05DD \u2194", stable, TextMedium); // יציבים ↔
        if (declining.Count > 0)
            MakeTrendGroup(parent, "\u05E6\u05E8\u05D9\u05DB\u05D9\u05DD \u05EA\u05E8\u05D2\u05D5\u05DC \u2193", declining, AccentRed); // צריכים תרגול ↓

        // Category trends
        var catCard = MakeCard(parent);
        MakeSectionTitle(catCard, "\u05DE\u05D2\u05DE\u05D5\u05EA \u05DC\u05E4\u05D9 \u05EA\u05D7\u05D5\u05DD"); // מגמות לפי תחום

        foreach (var cat in _data.categories)
        {
            if (cat.contributingGamesCount == 0) continue;
            var row = MakeHRow(catCard, 30, TextAnchor.MiddleRight);
            row.GetComponent<HorizontalLayoutGroup>().spacing = 8;

            var arrTMP = AddChildTMP(row.transform, ParentDashboardViewModel.TrendArrow(cat.trend),
                14, cat.color, TextAlignmentOptions.Center);
            arrTMP.gameObject.AddComponent<LayoutElement>().preferredWidth = 24;

            var scTMP = AddChildTMP(row.transform, $"{cat.score:F0}",
                15, cat.color, TextAlignmentOptions.Center);
            scTMP.fontStyle = FontStyles.Bold;
            scTMP.gameObject.AddComponent<LayoutElement>().preferredWidth = 35;

            var nmTMP = AddChildTMP(row.transform, H(cat.categoryName),
                15, TextDark, TextAlignmentOptions.Right);
            nmTMP.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var catTrendLbl = AddChildTMP(row.transform, H(cat.trendLabel),
                12, TextMedium, TextAlignmentOptions.Left);
            catTrendLbl.gameObject.AddComponent<LayoutElement>().preferredWidth = 90;
        }

        FitCard(catCard);
        MakeSpacer(parent, 40f);
    }

    private void MakeTrendGroup(Transform parent, string title, List<GameDashboardData> games, Color color)
    {
        var card = MakeCard(parent);
        MakeSectionTitle(card, title);

        foreach (var g in games)
        {
            var row = MakeHRow(card, 30, TextAnchor.MiddleRight);
            row.GetComponent<HorizontalLayoutGroup>().spacing = 8;

            var scTMP = AddChildTMP(row.transform, $"{g.score:F0}", 15, color, TextAlignmentOptions.Center);
            scTMP.fontStyle = FontStyles.Bold;
            scTMP.gameObject.AddComponent<LayoutElement>().preferredWidth = 35;

            var nmTMP = AddChildTMP(row.transform, H(g.gameName), 15, TextDark, TextAlignmentOptions.Right);
            nmTMP.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var tLbl = AddChildTMP(row.transform, H(g.trendLabel), 12, TextMedium, TextAlignmentOptions.Left);
            tLbl.gameObject.AddComponent<LayoutElement>().preferredWidth = 90;
        }

        FitCard(card);
    }

    // ═══════════════════════════════════════════════════════════════
    //  DIFFICULTY CONTROL
    // ═══════════════════════════════════════════════════════════════

    private void ChangeDifficultyFull(string gameId, int delta,
        TextMeshProUGUI displayTMP, TextMeshProUGUI modeTMP, TextMeshProUGUI impactTMP,
        Transform card, Transform scrollContent)
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        var gp = profile.analytics.GetOrCreateGame(gameId);
        int newDiff = Mathf.Clamp(gp.currentDifficulty + delta, 1, 10);
        if (newDiff == gp.currentDifficulty) return;

        gp.currentDifficulty = newDiff;
        gp.manualDifficultyOverride = true;
        gp.sessionsSinceDifficultyChange = 0;
        ProfileManager.Instance.Save();

        displayTMP.text = $"{newDiff}";
        modeTMP.text = H("\u05E7\u05D5\u05E9\u05D9 \u05D9\u05D3\u05E0\u05D9"); // קושי ידני
        modeTMP.color = AccentOrange;

        // Update impact label
        string impact = GameDifficultyConfig.GetDifficultyImpactLabel(gameId, newDiff);
        impactTMP.text = H(impact);

        LayoutRebuilder.ForceRebuildLayoutImmediate(card.GetComponent<RectTransform>());
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent.GetComponent<RectTransform>());
    }

    private void ResetDifficultyOverride(string gameId,
        TextMeshProUGUI displayTMP, TextMeshProUGUI modeTMP, TextMeshProUGUI impactTMP,
        GameObject resetBtnGO, GameObject recCardGO,
        Transform card, Transform scrollContent)
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        var gp = profile.analytics.GetOrCreateGame(gameId);
        gp.manualDifficultyOverride = false;
        gp.sessionsSinceDifficultyChange = 0;
        ProfileManager.Instance.Save();

        displayTMP.text = $"{gp.currentDifficulty}";
        modeTMP.text = H("\u05E7\u05D5\u05E9\u05D9 \u05D0\u05D5\u05D8\u05D5\u05DE\u05D8\u05D9"); // קושי אוטומטי
        modeTMP.color = AccentGreen;

        // Update impact label
        string impact = GameDifficultyConfig.GetDifficultyImpactLabel(gameId, gp.currentDifficulty);
        impactTMP.text = H(impact);

        // Hide the recommended card and reset button
        if (resetBtnGO != null) resetBtnGO.SetActive(false);
        if (recCardGO != null) recCardGO.SetActive(false);

        LayoutRebuilder.ForceRebuildLayoutImmediate(card.GetComponent<RectTransform>());
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent.GetComponent<RectTransform>());
    }

    // ═══════════════════════════════════════════════════════════════
    //  UI BUILDERS
    // ═══════════════════════════════════════════════════════════════

    private Transform MakeCard(Transform parent)
    {
        var go = new GameObject("Card");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = roundedRect;
        img.type = Image.Type.Sliced;
        img.color = CardColor;
        go.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.06f);

        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8;
        layout.padding = new RectOffset(20, 20, 16, 16);
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        return go.transform;
    }

    private GameObject MakeInlineCard(Transform parent, Color bgColor)
    {
        var go = new GameObject("InlineCard");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = roundedRect;
        img.type = Image.Type.Sliced;
        img.color = bgColor;

        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 4;
        layout.padding = new RectOffset(12, 12, 10, 10);
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        go.AddComponent<LayoutElement>().flexibleWidth = 1;
        go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return go;
    }

    private void FitCard(Transform card)
    {
        var fitter = card.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = card.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private GameObject MakeHRow(Transform parent, float height, TextAnchor align)
    {
        var go = new GameObject("HRow");
        go.transform.SetParent(parent, false);
        var layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8;
        layout.childAlignment = align;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        if (height > 0)
            go.AddComponent<LayoutElement>().preferredHeight = height;
        return go;
    }

    private GameObject MakeVCol(Transform parent)
    {
        var go = new GameObject("VCol");
        go.transform.SetParent(parent, false);
        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 2;
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        return go;
    }

    private TextMeshProUGUI AddChildTMP(Transform parent, string text, int fontSize, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.isRightToLeftText = false;
        tmp.raycastTarget = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        return tmp;
    }

    private void AddMiniStat(Transform parent, string value, string label)
    {
        var col = MakeVCol(parent);
        col.AddComponent<LayoutElement>().flexibleWidth = 1;
        col.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;

        var valTMP = AddChildTMP(col.transform, value, 14, TextDark, TextAlignmentOptions.Center);
        valTMP.fontStyle = FontStyles.Bold;
        valTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 16;

        AddChildTMP(col.transform, label, 10, TextLight, TextAlignmentOptions.Center)
            .gameObject.AddComponent<LayoutElement>().preferredHeight = 14;
    }

    private void MakeSectionDivider(Transform parent, string rawHebrew)
    {
        MakeSpacer(parent, 8f);
        var tmp = AddChildTMP(parent, H(rawHebrew), 22, TextDark, TextAlignmentOptions.Right);
        tmp.fontStyle = FontStyles.Bold;
        tmp.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;
    }

    private void MakeSectionTitle(Transform parent, string rawHebrew)
    {
        var tmp = AddChildTMP(parent, H(rawHebrew), 20, TextDark, TextAlignmentOptions.Right);
        tmp.fontStyle = FontStyles.Bold;
        tmp.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
    }

    private void MakeStatRow(Transform parent, string rawLabel, string value)
    {
        var row = MakeHRow(parent, 30, TextAnchor.MiddleRight);
        row.GetComponent<HorizontalLayoutGroup>().spacing = 8;

        var valTMP = AddChildTMP(row.transform, value, 16, Primary, TextAlignmentOptions.Left);
        valTMP.fontStyle = FontStyles.Bold;
        valTMP.gameObject.AddComponent<LayoutElement>().preferredWidth = 140;

        var labelTMP = AddChildTMP(row.transform, H(rawLabel), 16, TextMedium, TextAlignmentOptions.Right);
        labelTMP.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
    }

    private void MakeDetailRow(Transform parent, string rawLabel, string value)
    {
        MakeStatRow(parent, rawLabel, value);
    }

    private void MakeStatCell(Transform parent, string value, string rawLabel)
    {
        var go = new GameObject("StatCell");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = roundedRect;
        img.type = Image.Type.Sliced;
        img.color = HexColor("#F8F9FA");
        img.raycastTarget = false;

        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 2;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var valTMP = AddChildTMP(go.transform, value, 20, TextDark, TextAlignmentOptions.Center);
        valTMP.fontStyle = FontStyles.Bold;
        valTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

        var lblTMP = AddChildTMP(go.transform, H(rawLabel), 13, TextMedium, TextAlignmentOptions.Center);
        lblTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
    }

    private void MakeProgressBar(Transform parent, float fill, Color barColor, float height)
    {
        var go = new GameObject("ProgressBar");
        go.transform.SetParent(parent, false);
        go.AddComponent<LayoutElement>().preferredHeight = height;

        var bgImg = go.AddComponent<Image>();
        bgImg.sprite = roundedRect;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = BarBg;
        bgImg.raycastTarget = false;

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(go.transform, false);
        var fillRT = fillGO.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(Mathf.Clamp01(fill), 1);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.sprite = roundedRect;
        fillImg.type = Image.Type.Sliced;
        fillImg.color = barColor;
        fillImg.raycastTarget = false;
    }

    private void MakeCategoryRow(Transform parent, CategoryDashboardData cat)
    {
        var go = new GameObject("CatRow");
        go.transform.SetParent(parent, false);
        var layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.childForceExpandWidth = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        go.AddComponent<LayoutElement>().preferredHeight = 40;

        // Score pill (rounded rect badge with category color)
        var pillGO = new GameObject("ScorePill");
        pillGO.transform.SetParent(go.transform, false);
        var pillLE = pillGO.AddComponent<LayoutElement>();
        pillLE.preferredWidth = 44;
        pillLE.preferredHeight = 26;
        var pillImg = pillGO.AddComponent<Image>();
        if (roundedRect != null) pillImg.sprite = roundedRect;
        pillImg.type = Image.Type.Sliced;
        pillImg.color = cat.color;
        pillImg.raycastTarget = false;
        var pillTMP = AddChildTMP(pillGO.transform, $"{cat.score:F0}", 14, Color.white, TextAlignmentOptions.Center);
        pillTMP.fontStyle = FontStyles.Bold;

        // Progress bar
        var barContainer = new GameObject("BarContainer");
        barContainer.transform.SetParent(go.transform, false);
        var barLE = barContainer.AddComponent<LayoutElement>();
        barLE.preferredWidth = 100;
        barLE.preferredHeight = 8;
        barLE.flexibleWidth = 0;

        var barBgImg = barContainer.AddComponent<Image>();
        barBgImg.sprite = roundedRect;
        barBgImg.type = Image.Type.Sliced;
        barBgImg.color = BarBg;
        barBgImg.raycastTarget = false;

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(barContainer.transform, false);
        var fillRT = fillGO.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(Mathf.Clamp01(cat.score / 100f), 1);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.sprite = roundedRect;
        fillImg.type = Image.Type.Sliced;
        fillImg.color = cat.color;
        fillImg.raycastTarget = false;

        // Trend arrow
        var arrTMP = AddChildTMP(go.transform, ParentDashboardViewModel.TrendArrow(cat.trend),
            13, TextMedium, TextAlignmentOptions.Center);
        arrTMP.gameObject.AddComponent<LayoutElement>().preferredWidth = 20;

        // Name
        var nmTMP = AddChildTMP(go.transform, H(cat.categoryName), 15, TextDark, TextAlignmentOptions.Right);
        nmTMP.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
    }

    private void MakeMiniChip(Transform parent, CategoryDashboardData cat, Color accentColor)
    {
        var row = MakeHRow(parent, 28, TextAnchor.MiddleRight);
        row.GetComponent<HorizontalLayoutGroup>().spacing = 6;

        var scTMP = AddChildTMP(row.transform, $"{cat.score:F0}", 14, accentColor, TextAlignmentOptions.Center);
        scTMP.fontStyle = FontStyles.Bold;
        scTMP.gameObject.AddComponent<LayoutElement>().preferredWidth = 30;

        var nmTMP = AddChildTMP(row.transform, H(cat.categoryName), 14, TextDark, TextAlignmentOptions.Right);
        nmTMP.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
    }

    private Button MakeSmallButton(Transform parent, string label, int fontSize)
    {
        var go = new GameObject("Btn");
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = 42;
        le.preferredHeight = 42;

        var img = go.AddComponent<Image>();
        img.sprite = roundedRect;
        img.type = Image.Type.Sliced;
        img.color = HexColor("#E8EBED");

        var tmp = AddChildTMP(go.transform, label, fontSize, TextDark, TextAlignmentOptions.Center);
        tmp.fontStyle = FontStyles.Bold;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        return btn;
    }

    private void MakeDivider(Transform parent)
    {
        var go = new GameObject("Divider");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = Divider;
        img.raycastTarget = false;
        go.AddComponent<LayoutElement>().preferredHeight = 1;
    }

    private void MakeSpacer(Transform parent, float height)
    {
        var go = new GameObject("Spacer");
        go.transform.SetParent(parent, false);
        go.AddComponent<LayoutElement>().preferredHeight = height;
    }

    // ═══════════════════════════════════════════════════════════════
    //  LEADERBOARD MODAL
    // ═══════════════════════════════════════════════════════════════

    private void ShowLeaderboard()
    {
        if (_leaderboardModal != null) return;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        // Build leaderboard data
        var leaderboardData = LeaderboardBuilder.Build();

        // ── Modal overlay ──
        _leaderboardModal = new GameObject("LeaderboardModal");
        _leaderboardModal.transform.SetParent(canvas.transform, false);
        var modalRT = _leaderboardModal.AddComponent<RectTransform>();
        modalRT.anchorMin = Vector2.zero;
        modalRT.anchorMax = Vector2.one;
        modalRT.offsetMin = Vector2.zero;
        modalRT.offsetMax = Vector2.zero;

        // Dim background
        var dimImg = _leaderboardModal.AddComponent<Image>();
        dimImg.color = new Color(0, 0, 0, 0.5f);
        dimImg.raycastTarget = true;

        // ── Content panel (with safe margins) ──
        var panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(_leaderboardModal.transform, false);
        var panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = new Vector2(20, 40);
        panelRT.offsetMax = new Vector2(-20, -40);
        var panelImg = panelGO.AddComponent<Image>();
        if (roundedRect != null) panelImg.sprite = roundedRect;
        panelImg.type = Image.Type.Sliced;
        panelImg.color = BgColor;

        // ── Header bar ──
        var headerGO = new GameObject("Header");
        headerGO.transform.SetParent(panelGO.transform, false);
        var headerRT2 = headerGO.AddComponent<RectTransform>();
        headerRT2.anchorMin = new Vector2(0, 1);
        headerRT2.anchorMax = new Vector2(1, 1);
        headerRT2.pivot = new Vector2(0.5f, 1);
        headerRT2.sizeDelta = new Vector2(0, 70);
        var headerBg = headerGO.AddComponent<Image>();
        if (roundedRect != null) headerBg.sprite = roundedRect;
        headerBg.type = Image.Type.Sliced;
        headerBg.color = HexColor("#2C3E50");

        var headerLayout = headerGO.AddComponent<HorizontalLayoutGroup>();
        headerLayout.padding = new RectOffset(16, 16, 8, 8);
        headerLayout.spacing = 12;
        headerLayout.childAlignment = TextAnchor.MiddleCenter;
        headerLayout.childForceExpandWidth = false;
        headerLayout.childForceExpandHeight = true;
        headerLayout.childControlWidth = true;
        headerLayout.childControlHeight = false;

        // Close button (left in hierarchy = left visually)
        var closeBtn = MakeSmallButton(headerGO.transform, "\u2715", 22); // ✕
        closeBtn.GetComponent<Image>().color = new Color(1, 1, 1, 0.2f);
        closeBtn.onClick.AddListener(CloseLeaderboard);

        // Title
        var titleTMP = AddChildTMP(headerGO.transform,
            H("\u05D8\u05D1\u05DC\u05EA \u05D0\u05DC\u05D9\u05E4\u05D5\u05D9\u05D5\u05EA"), // טבלת אליפויות
            22, Color.white, TextAlignmentOptions.Center);
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        // Trophy icon
        if (trophySprite != null)
        {
            var trophyGO = new GameObject("Trophy");
            trophyGO.transform.SetParent(headerGO.transform, false);
            var trLE = trophyGO.AddComponent<LayoutElement>();
            trLE.preferredWidth = 36;
            trLE.preferredHeight = 36;
            var trImg = trophyGO.AddComponent<Image>();
            trImg.sprite = trophySprite;
            trImg.preserveAspect = true;
            trImg.raycastTarget = false;
        }

        // ── ScrollView for content ──
        var svGO = new GameObject("ScrollView");
        svGO.transform.SetParent(panelGO.transform, false);
        var svRT = svGO.AddComponent<RectTransform>();
        svRT.anchorMin = Vector2.zero;
        svRT.anchorMax = Vector2.one;
        svRT.offsetMin = Vector2.zero;
        svRT.offsetMax = new Vector2(0, -70); // below header
        var svImg2 = svGO.AddComponent<Image>();
        svImg2.color = BgColor;
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
        contentLayout.spacing = 20;
        contentLayout.padding = new RectOffset(16, 16, 16, 30);
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sv.content = contentRT;

        // ── Build game sections ──
        foreach (var gameBoard in leaderboardData)
        {
            BuildLeaderboardGameSection(contentGO.transform, gameBoard);
        }

        MakeSpacer(contentGO.transform, 20f);
    }

    private void CloseLeaderboard()
    {
        if (_leaderboardModal != null)
        {
            Destroy(_leaderboardModal);
            _leaderboardModal = null;
        }
    }

    private void BuildLeaderboardGameSection(Transform parent, GameLeaderboardData gameBoard)
    {
        var card = MakeCard(parent);
        var cardLayout = card.GetComponent<VerticalLayoutGroup>();
        cardLayout.spacing = 6;
        cardLayout.padding = new RectOffset(16, 16, 14, 14);

        // Game title
        var titleTMP = AddChildTMP(card, H(gameBoard.gameName), 20, TextDark, TextAlignmentOptions.Right);
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;

        MakeDivider(card);

        // Column headers
        var headerRow = MakeHRow(card, 24, TextAnchor.MiddleRight);
        headerRow.GetComponent<HorizontalLayoutGroup>().spacing = 6;
        AddLbHeaderCell(headerRow.transform, "#", 30);
        AddLbHeaderCell(headerRow.transform, "\u05E9\u05DD", 0, true); // שם
        AddLbHeaderCell(headerRow.transform, "\u05E6\u05D9\u05D5\u05DF", 50); // ציון
        AddLbHeaderCell(headerRow.transform, "\u05E8\u05DE\u05D4", 40); // רמה
        AddLbHeaderCell(headerRow.transform, "\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD", 60); // משחקים

        // Entries
        foreach (var entry in gameBoard.entries)
        {
            BuildLeaderboardRow(card, entry);
        }

        FitCard(card);
    }

    private void AddLbHeaderCell(Transform parent, string text, float width, bool flex = false)
    {
        var tmp = AddChildTMP(parent, H(text), 11, TextLight, TextAlignmentOptions.Center);
        tmp.fontStyle = FontStyles.Bold;
        var le = tmp.gameObject.AddComponent<LayoutElement>();
        if (flex) le.flexibleWidth = 1;
        else le.preferredWidth = width;
    }

    private void BuildLeaderboardRow(Transform parent, GameLeaderboardEntryData entry)
    {
        // Row container
        var rowGO = new GameObject("LbRow");
        rowGO.transform.SetParent(parent, false);
        var rowImg = rowGO.AddComponent<Image>();
        if (roundedRect != null) rowImg.sprite = roundedRect;
        rowImg.type = Image.Type.Sliced;
        rowImg.color = entry.isCurrentProfile ? HighlightBg : new Color(1, 1, 1, 0f);
        rowImg.raycastTarget = false;
        rowGO.AddComponent<LayoutElement>().preferredHeight = entry.hasPlayedGame ? 44 : 36;

        var rowLayout = rowGO.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 6;
        rowLayout.padding = new RectOffset(8, 8, 4, 4);
        rowLayout.childAlignment = TextAnchor.MiddleRight;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = false;

        Color textColor = entry.hasPlayedGame ? TextDark : TextLight;
        int fontSize = entry.hasPlayedGame ? 15 : 13;

        // Rank
        string rankStr = entry.rank > 0 ? $"{entry.rank}" : "-";
        var rankTMP = AddChildTMP(rowGO.transform, rankStr, fontSize, textColor, TextAlignmentOptions.Center);
        rankTMP.fontStyle = entry.rank <= 3 && entry.hasPlayedGame ? FontStyles.Bold : FontStyles.Normal;
        if (entry.rank == 1 && entry.hasPlayedGame) rankTMP.color = GoldAccent;
        rankTMP.gameObject.AddComponent<LayoutElement>().preferredWidth = 30;

        // Name column
        var nameCol = MakeVCol(rowGO.transform);
        nameCol.AddComponent<LayoutElement>().flexibleWidth = 1;
        var nameTMP = AddChildTMP(nameCol.transform, H(entry.profileName), fontSize, textColor, TextAlignmentOptions.Right);
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

        if (entry.isCurrentProfile)
        {
            var badgeTMP = AddChildTMP(nameCol.transform,
                H("\u05D4\u05E4\u05E8\u05D5\u05E4\u05D9\u05DC \u05D4\u05E0\u05D5\u05DB\u05D7\u05D9"), // הפרופיל הנוכחי
                10, Primary, TextAlignmentOptions.Right);
            badgeTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 14;
        }
        else if (!entry.hasPlayedGame)
        {
            var statusTMP = AddChildTMP(nameCol.transform,
                H("\u05E2\u05D3\u05D9\u05D9\u05DF \u05DC\u05D0 \u05E9\u05D9\u05D7\u05E7"), // עדיין לא שיחק
                10, TextLight, TextAlignmentOptions.Right);
            statusTMP.fontStyle = FontStyles.Italic;
            statusTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 14;
        }

        if (entry.hasPlayedGame)
        {
            // Score
            var scoreTMP = AddChildTMP(rowGO.transform, $"{entry.score:F0}",
                fontSize, ParentDashboardViewModel.ScoreColor(entry.score), TextAlignmentOptions.Center);
            scoreTMP.fontStyle = FontStyles.Bold;
            scoreTMP.gameObject.AddComponent<LayoutElement>().preferredWidth = 50;

            // Difficulty
            var diffTMP = AddChildTMP(rowGO.transform, $"{entry.currentDifficulty}",
                fontSize, TextMedium, TextAlignmentOptions.Center);
            diffTMP.gameObject.AddComponent<LayoutElement>().preferredWidth = 40;

            // Sessions
            var sessTMP = AddChildTMP(rowGO.transform, $"{entry.sessionsPlayed}",
                fontSize, TextMedium, TextAlignmentOptions.Center);
            sessTMP.gameObject.AddComponent<LayoutElement>().preferredWidth = 60;
        }
        else
        {
            // Empty cells for alignment
            var emptyLE1 = new GameObject("E1");
            emptyLE1.transform.SetParent(rowGO.transform, false);
            emptyLE1.AddComponent<RectTransform>();
            emptyLE1.AddComponent<LayoutElement>().preferredWidth = 50;

            var emptyLE2 = new GameObject("E2");
            emptyLE2.transform.SetParent(rowGO.transform, false);
            emptyLE2.AddComponent<RectTransform>();
            emptyLE2.AddComponent<LayoutElement>().preferredWidth = 40;

            var emptyLE3 = new GameObject("E3");
            emptyLE3.transform.SetParent(rowGO.transform, false);
            emptyLE3.AddComponent<RectTransform>();
            emptyLE3.AddComponent<LayoutElement>().preferredWidth = 60;
        }
    }

    // ── Navigation ──

    public void OnBackPressed() => BubbleTransition.LoadScene("WorldScene");

    // ── Helpers ──

    private static string H(string raw) => HebrewFixer.Fix(raw);

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
