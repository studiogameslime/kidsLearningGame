using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Parent Dashboard controller. Manages parental gate, tab navigation,
/// and dynamically builds all dashboard content from analytics data.
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
    public Button backButton;

    [Header("Tabs")]
    public Button[] tabButtons;
    public Image[] tabIndicators;
    public RectTransform[] tabContents; // scroll content areas

    [Header("Assets")]
    public Sprite roundedRect;

    // ── Colors ──
    private static readonly Color CardColor = HexColor("#FFFFFF");
    private static readonly Color BgColor = HexColor("#F0F2F5");
    private static readonly Color Primary = HexColor("#3498DB");
    private static readonly Color TextDark = HexColor("#2D3436");
    private static readonly Color TextMedium = HexColor("#636E72");
    private static readonly Color TextLight = HexColor("#B2BEC3");
    private static readonly Color TabActive = HexColor("#3498DB");
    private static readonly Color TabInactive = HexColor("#95A5A6");
    private static readonly Color BarBg = HexColor("#E8EBED");
    private static readonly Color Divider = HexColor("#E8EBED");
    private static readonly Color ScoreGreen = HexColor("#27AE60");

    private ParentDashboardData _data;
    private int _activeTab = -1;
    private bool[] _tabBuilt;
    private int _correctAnswer;

    private void Start()
    {
        _tabBuilt = new bool[tabContents.Length];

        // Gate
        dashboardPanel.gameObject.SetActive(false);
        gatePanel.gameObject.SetActive(true);
        GenerateQuestion();

        for (int i = 0; i < answerButtons.Length; i++)
        {
            int idx = i;
            answerButtons[i].onClick.AddListener(() => OnAnswerTapped(idx));
        }

        // Tabs
        for (int i = 0; i < tabButtons.Length; i++)
        {
            int idx = i;
            tabButtons[i].onClick.AddListener(() => SwitchTab(idx));
        }

        if (backButton != null)
            backButton.onClick.AddListener(OnBackPressed);
    }

    // ═══════════════════════════════════════════════════════════════
    //  PARENTAL GATE
    // ═══════════════════════════════════════════════════════════════

    private void GenerateQuestion()
    {
        int a = Random.Range(3, 9);
        int b = Random.Range(2, 8);
        _correctAnswer = a + b;

        string q = $"? = {b} + {a}";
        questionText.text = q;
        questionText.isRightToLeftText = false;

        // Generate 4 answers: 1 correct + 3 wrong
        var answers = new List<int> { _correctAnswer };
        while (answers.Count < 4)
        {
            int wrong = _correctAnswer + Random.Range(-3, 4);
            if (wrong != _correctAnswer && wrong > 0 && !answers.Contains(wrong))
                answers.Add(wrong);
        }

        // Shuffle
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
        {
            OnGatePassed();
        }
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
        SwitchTab(0);
    }

    // ═══════════════════════════════════════════════════════════════
    //  DATA
    // ═══════════════════════════════════════════════════════════════

    private void LoadData()
    {
        _data = ParentDashboardViewModel.Build();
        if (_data == null) return;

        if (headerNameText != null)
            headerNameText.text = _data.profileName;
        if (headerAgeText != null)
        {
            string ageLabel = $"\u05D2\u05D9\u05DC {_data.ageDisplay}";
            headerAgeText.text = HebrewFixer.Fix(ageLabel);
            headerAgeText.isRightToLeftText = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  TAB NAVIGATION
    // ═══════════════════════════════════════════════════════════════

    private void SwitchTab(int idx)
    {
        if (_activeTab == idx) return;
        _activeTab = idx;

        for (int i = 0; i < tabContents.Length; i++)
        {
            tabContents[i].parent.parent.gameObject.SetActive(i == idx); // ScrollView
            if (tabIndicators != null && i < tabIndicators.Length)
                tabIndicators[i].color = i == idx ? TabActive : Color.clear;
            if (tabButtons != null && i < tabButtons.Length)
            {
                var txt = tabButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null) txt.color = i == idx ? TabActive : TabInactive;
            }
        }

        if (!_tabBuilt[idx])
        {
            _tabBuilt[idx] = true;
            switch (idx)
            {
                case 0: BuildOverviewTab(); break;
                case 1: BuildGamesTab(); break;
                case 2: BuildCategoriesTab(); break;
                case 3: BuildTrendsTab(); break;
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
        var scoreCard = MakeCard(parent, 200f);
        var scoreTitle = MakeText(scoreCard, H("\u05E6\u05D9\u05D5\u05DF \u05DB\u05DC\u05DC\u05D9"), 20, TextMedium); // ציון כללי
        scoreTitle.alignment = TextAlignmentOptions.Center;

        var scoreValue = MakeText(scoreCard, $"{_data.overallScore:F0}", 52, ParentDashboardViewModel.ScoreColor(_data.overallScore));
        scoreValue.alignment = TextAlignmentOptions.Center;
        scoreValue.fontStyle = FontStyles.Bold;

        MakeProgressBar(scoreCard, _data.overallScore / 100f, ParentDashboardViewModel.ScoreColor(_data.overallScore), 20f);

        var scoreLabel = MakeText(scoreCard, H(_data.overallScoreLabel), 16, TextMedium);
        scoreLabel.alignment = TextAlignmentOptions.Center;

        // ── Quick Stats ──
        var statsCard = MakeCard(parent, 0f);
        MakeSectionTitle(statsCard, H("\u05E1\u05D8\u05D8\u05D9\u05E1\u05D8\u05D9\u05E7\u05D5\u05EA")); // סטטיסטיקות

        MakeStatRow(statsCard, H("\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05E9\u05E9\u05D5\u05D7\u05E7\u05D5"), $"{_data.totalSessions}"); // משחקים ששוחקו
        MakeStatRow(statsCard, H("\u05D6\u05DE\u05DF \u05DE\u05E9\u05D7\u05E7 \u05DB\u05D5\u05DC\u05DC"), _data.totalPlayTimeDisplay); // זמן משחק כולל
        MakeStatRow(statsCard, H("\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05E9\u05D5\u05E0\u05D9\u05DD"), $"{_data.gamesPlayedCount}"); // משחקים שונים
        MakeStatRow(statsCard, H("\u05DE\u05E9\u05D7\u05E7 \u05D0\u05D4\u05D5\u05D1"), H(_data.favoriteGameName)); // משחק אהוב

        FitCard(statsCard);

        // ── Strengths ──
        if (_data.strongestCategories.Count > 0)
        {
            var strengthCard = MakeCard(parent, 0f);
            MakeSectionTitle(strengthCard, H("\u05D7\u05D6\u05E7\u05D5\u05EA")); // חזקות

            foreach (var cat in _data.strongestCategories)
                MakeCategoryChip(strengthCard, cat);

            FitCard(strengthCard);
        }

        // ── Practice Areas ──
        if (_data.weakestCategories.Count > 0)
        {
            var weakCard = MakeCard(parent, 0f);
            MakeSectionTitle(weakCard, H("\u05EA\u05D7\u05D5\u05DE\u05D9 \u05EA\u05E8\u05D2\u05D5\u05DC")); // תחומי תרגול

            foreach (var cat in _data.weakestCategories)
                MakeCategoryChip(weakCard, cat);

            FitCard(weakCard);
        }

        // ── Category Grid ──
        var catCard = MakeCard(parent, 0f);
        MakeSectionTitle(catCard, H("\u05EA\u05D7\u05D5\u05DE\u05D9 \u05D4\u05EA\u05E4\u05EA\u05D7\u05D5\u05EA")); // תחומי התפתחות

        foreach (var cat in _data.categories)
        {
            if (cat.contributingGamesCount == 0) continue;
            MakeMiniCategoryRow(catCard, cat);
        }

        FitCard(catCard);

        // Bottom spacer
        MakeSpacer(parent, 40f);
    }

    // ═══════════════════════════════════════════════════════════════
    //  GAMES TAB
    // ═══════════════════════════════════════════════════════════════

    private void BuildGamesTab()
    {
        if (_data == null) return;
        var parent = tabContents[1];

        if (_data.games.Count == 0)
        {
            MakeText(parent, H("\u05E2\u05D5\u05D3 \u05D0\u05D9\u05DF \u05E0\u05EA\u05D5\u05E0\u05D9\u05DD"), 20, TextMedium); // עוד אין נתונים
            return;
        }

        foreach (var game in _data.games)
            MakeGameCard(parent, game);

        MakeSpacer(parent, 40f);
    }

    private void MakeGameCard(Transform parent, GameDashboardData game)
    {
        var card = MakeCard(parent, 0f);

        // Summary row
        var summaryGO = new GameObject("Summary");
        summaryGO.transform.SetParent(card, false);
        var summaryRT = summaryGO.AddComponent<RectTransform>();
        summaryRT.sizeDelta = new Vector2(0, 80);
        var summaryLayout = summaryGO.AddComponent<HorizontalLayoutGroup>();
        summaryLayout.spacing = 12;
        summaryLayout.padding = new RectOffset(0, 0, 8, 8);
        summaryLayout.childAlignment = TextAnchor.MiddleRight;
        summaryLayout.childForceExpandWidth = false;
        summaryLayout.childForceExpandHeight = false;

        // Score circle
        var scoreBadge = new GameObject("Score");
        scoreBadge.transform.SetParent(summaryGO.transform, false);
        var scoreBadgeRT = scoreBadge.AddComponent<RectTransform>();
        scoreBadgeRT.sizeDelta = new Vector2(50, 50);
        var scoreBadgeLE = scoreBadge.AddComponent<LayoutElement>();
        scoreBadgeLE.preferredWidth = 50;
        scoreBadgeLE.preferredHeight = 50;
        var scoreBadgeImg = scoreBadge.AddComponent<Image>();
        scoreBadgeImg.sprite = roundedRect;
        scoreBadgeImg.type = Image.Type.Sliced;
        scoreBadgeImg.color = ParentDashboardViewModel.ScoreColor(game.score);
        var scoreTxt = MakeChildText(scoreBadge.transform, $"{game.score:F0}", 18, Color.white);
        scoreTxt.alignment = TextAlignmentOptions.Center;
        scoreTxt.fontStyle = FontStyles.Bold;

        // Info column
        var infoGO = new GameObject("Info");
        infoGO.transform.SetParent(summaryGO.transform, false);
        var infoLE = infoGO.AddComponent<LayoutElement>();
        infoLE.flexibleWidth = 1;
        infoLE.preferredHeight = 60;
        var infoLayout = infoGO.AddComponent<VerticalLayoutGroup>();
        infoLayout.childAlignment = TextAnchor.MiddleRight;
        infoLayout.childForceExpandHeight = false;

        var nameText = MakeChildText(infoGO.transform, H(game.gameName), 20, TextDark);
        nameText.fontStyle = FontStyles.Bold;
        nameText.alignment = TextAlignmentOptions.Right;

        string subLine = $"{ParentDashboardViewModel.TrendArrow(game.trend)} " +
            $"{game.sessionsPlayed} \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD | " +
            $"\u05E8\u05DE\u05D4 {game.currentDifficulty}/10";
        var subText = MakeChildText(infoGO.transform, subLine, 14, TextMedium);
        subText.alignment = TextAlignmentOptions.Right;

        // Details panel (hidden by default)
        var detailsGO = new GameObject("Details");
        detailsGO.transform.SetParent(card, false);
        detailsGO.SetActive(false);
        var detailsLayout = detailsGO.AddComponent<VerticalLayoutGroup>();
        detailsLayout.spacing = 6;
        detailsLayout.padding = new RectOffset(0, 0, 8, 8);
        detailsLayout.childForceExpandWidth = true;
        detailsLayout.childForceExpandHeight = false;
        var detailsFitter = detailsGO.AddComponent<ContentSizeFitter>();
        detailsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        MakeDivider(detailsGO.transform);

        // Performance metrics
        MakeDetailRow(detailsGO.transform, H("\u05D3\u05D9\u05D5\u05E7"), $"{game.accuracy:P0}"); // דיוק
        MakeDetailRow(detailsGO.transform, H("\u05E9\u05D2\u05D9\u05D0\u05D5\u05EA \u05DE\u05DE\u05D5\u05E6\u05E2"), $"{game.mistakeRate:F1}"); // שגיאות ממוצע
        MakeDetailRow(detailsGO.transform, H("\u05E7\u05E6\u05D1 \u05D4\u05E9\u05DC\u05DE\u05D4"), $"{game.completionRate:P0}"); // קצב השלמה
        MakeDetailRow(detailsGO.transform, H("\u05E6\u05D9\u05D5\u05DF \u05DE\u05D4\u05D9\u05E8\u05D5\u05EA"), $"{game.speedScore:F0}"); // ציון מהירות
        MakeDetailRow(detailsGO.transform, H("\u05E2\u05E6\u05DE\u05D0\u05D5\u05EA"), $"{game.independenceScore:F0}"); // עצמאות
        MakeDetailRow(detailsGO.transform, H("\u05E8\u05DE\u05D4 \u05D2\u05D1\u05D5\u05D4\u05D4 \u05D1\u05D9\u05D5\u05EA\u05E8"), $"{game.highestDifficulty}/10"); // רמה גבוהה ביותר
        MakeDetailRow(detailsGO.transform, H("\u05E8\u05E6\u05E3 \u05D4\u05E6\u05DC\u05D7\u05D5\u05EA \u05D4\u05DB\u05D9 \u05D0\u05E8\u05D5\u05DA"), $"{game.maxStreak}"); // רצף הצלחות הכי ארוך
        MakeDetailRow(detailsGO.transform, H("\u05DE\u05E9\u05D7\u05E7 \u05D0\u05D7\u05E8\u05D5\u05DF"),
            ParentDashboardViewModel.FormatDate(game.lastPlayed)); // משחק אחרון

        // Categories
        if (game.categoryNames.Count > 0)
        {
            string cats = string.Join(", ", game.categoryNames);
            MakeDetailRow(detailsGO.transform, H("\u05EA\u05D7\u05D5\u05DE\u05D9\u05DD"), H(cats)); // תחומים
        }

        // Insight
        if (!string.IsNullOrEmpty(game.insightText))
        {
            var insight = MakeChildText(detailsGO.transform, H(game.insightText), 15, Primary);
            insight.fontStyle = FontStyles.Italic;
            insight.alignment = TextAlignmentOptions.Right;
        }

        // Recent sessions header
        if (game.recentSessions.Count > 0)
        {
            MakeDivider(detailsGO.transform);
            var sessTitle = MakeChildText(detailsGO.transform,
                H("\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05D0\u05D7\u05E8\u05D5\u05E0\u05D9\u05DD"), 16, TextDark); // משחקים אחרונים
            sessTitle.fontStyle = FontStyles.Bold;
            sessTitle.alignment = TextAlignmentOptions.Right;

            int showCount = Mathf.Min(5, game.recentSessions.Count);
            for (int i = game.recentSessions.Count - 1; i >= game.recentSessions.Count - showCount; i--)
            {
                var s = game.recentSessions[i];
                string status = s.completed ? "\u2713" : "\u2717";
                string line = $"{status} {ParentDashboardViewModel.FormatDate(s.timestamp)} | " +
                    $"\u05E8\u05DE\u05D4 {s.difficulty} | {s.accuracy:P0} | {s.mistakes} \u05E9\u05D2\u05D9\u05D0\u05D5\u05EA";
                var sessionText = MakeChildText(detailsGO.transform, line, 13, TextMedium);
                sessionText.alignment = TextAlignmentOptions.Right;
            }
        }

        // Tap to expand/collapse
        var cardBtn = card.gameObject.AddComponent<Button>();
        cardBtn.transition = Selectable.Transition.None;
        var scrollContent = parent;
        cardBtn.onClick.AddListener(() =>
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
        var parent = tabContents[2];

        foreach (var cat in _data.categories)
        {
            if (cat.contributingGamesCount == 0) continue;
            MakeCategoryCard(parent, cat);
        }

        MakeSpacer(parent, 40f);
    }

    private void MakeCategoryCard(Transform parent, CategoryDashboardData cat)
    {
        var card = MakeCard(parent, 0f);

        // Color accent bar at top
        var accent = new GameObject("Accent");
        accent.transform.SetParent(card, false);
        accent.transform.SetAsFirstSibling();
        var accentRT = accent.AddComponent<RectTransform>();
        accentRT.sizeDelta = new Vector2(0, 4);
        var accentImg = accent.AddComponent<Image>();
        accentImg.color = cat.color;
        var accentLE = accent.AddComponent<LayoutElement>();
        accentLE.preferredHeight = 4;

        // Header row
        var headerRow = new GameObject("Header");
        headerRow.transform.SetParent(card, false);
        var headerLayout = headerRow.AddComponent<HorizontalLayoutGroup>();
        headerLayout.spacing = 12;
        headerLayout.padding = new RectOffset(0, 0, 4, 4);
        headerLayout.childAlignment = TextAnchor.MiddleRight;
        headerLayout.childForceExpandWidth = false;
        var headerLE = headerRow.AddComponent<LayoutElement>();
        headerLE.preferredHeight = 60;

        // Score
        var scoreText = MakeChildText(headerRow.transform, $"{cat.score:F0}", 28, cat.color);
        scoreText.fontStyle = FontStyles.Bold;
        scoreText.alignment = TextAlignmentOptions.Center;
        var scoreLE = scoreText.gameObject.AddComponent<LayoutElement>();
        scoreLE.preferredWidth = 50;

        // Name + trend
        var nameCol = new GameObject("NameCol");
        nameCol.transform.SetParent(headerRow.transform, false);
        var nameColLE = nameCol.AddComponent<LayoutElement>();
        nameColLE.flexibleWidth = 1;
        var nameColLayout = nameCol.AddComponent<VerticalLayoutGroup>();
        nameColLayout.childAlignment = TextAnchor.MiddleRight;
        nameColLayout.childForceExpandHeight = false;

        var catName = MakeChildText(nameCol.transform, H(cat.categoryName), 20, TextDark);
        catName.fontStyle = FontStyles.Bold;
        catName.alignment = TextAlignmentOptions.Right;

        string trendStr = $"{ParentDashboardViewModel.TrendArrow(cat.trend)} " +
            $"{cat.contributingGamesCount} \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD"; // X משחקים
        var trendText = MakeChildText(nameCol.transform, trendStr, 14, TextMedium);
        trendText.alignment = TextAlignmentOptions.Right;

        // Progress bar
        MakeProgressBar(card, cat.score / 100f, cat.color, 14f);

        // Confidence
        string confStr = cat.confidence >= 0.7f
            ? "\u05DE\u05D9\u05D3\u05E2 \u05DE\u05D4\u05D9\u05DE\u05DF"  // מידע מהימן
            : "\u05E6\u05E8\u05D9\u05DA \u05E2\u05D5\u05D3 \u05E0\u05EA\u05D5\u05E0\u05D9\u05DD"; // צריך עוד נתונים
        var confText = MakeChildText(card, H(confStr), 13, TextLight);
        confText.alignment = TextAlignmentOptions.Right;

        // Details panel (hidden)
        var detailsGO = new GameObject("Details");
        detailsGO.transform.SetParent(card, false);
        detailsGO.SetActive(false);
        var detailsLayout = detailsGO.AddComponent<VerticalLayoutGroup>();
        detailsLayout.spacing = 6;
        detailsLayout.padding = new RectOffset(0, 0, 8, 8);
        detailsLayout.childForceExpandWidth = true;
        detailsLayout.childForceExpandHeight = false;
        var detailsFitter = detailsGO.AddComponent<ContentSizeFitter>();
        detailsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        MakeDivider(detailsGO.transform);

        // Contributing games
        if (cat.contributions.Count > 0)
        {
            var gamesTitle = MakeChildText(detailsGO.transform,
                H("\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05EA\u05D5\u05E8\u05DE\u05D9\u05DD"), 16, TextDark); // משחקים תורמים
            gamesTitle.fontStyle = FontStyles.Bold;
            gamesTitle.alignment = TextAlignmentOptions.Right;

            foreach (var contrib in cat.contributions)
            {
                string line = $"{contrib.gameScore:F0} | {contrib.weight:P0} | {H(contrib.gameName)}";
                var contribText = MakeChildText(detailsGO.transform, line, 14, TextMedium);
                contribText.alignment = TextAlignmentOptions.Right;
            }
        }

        // Insight
        if (!string.IsNullOrEmpty(cat.insightText))
        {
            var insight = MakeChildText(detailsGO.transform, H(cat.insightText), 15, Primary);
            insight.fontStyle = FontStyles.Italic;
            insight.alignment = TextAlignmentOptions.Right;
        }

        // Tap to expand
        var cardBtn = card.gameObject.AddComponent<Button>();
        cardBtn.transition = Selectable.Transition.None;
        var scrollContent = parent;
        cardBtn.onClick.AddListener(() =>
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
        var parent = tabContents[3];

        // ── Overall trend ──
        var overallCard = MakeCard(parent, 0f);
        MakeSectionTitle(overallCard, H("\u05DE\u05D2\u05DE\u05D4 \u05DB\u05DC\u05DC\u05D9\u05EA")); // מגמה כללית

        string overallTrend = _data.overallTrend > 2f
            ? "\u2191 \u05DE\u05E9\u05EA\u05E4\u05E8"       // ↑ משתפר
            : _data.overallTrend < -2f
                ? "\u2193 \u05E6\u05E8\u05D9\u05DA \u05EA\u05E8\u05D2\u05D5\u05DC" // ↓ צריך תרגול
                : "\u2194 \u05D9\u05E6\u05D9\u05D1";         // ↔ יציב
        var trendLabel = MakeText(overallCard, H(overallTrend), 22, Primary);
        trendLabel.alignment = TextAlignmentOptions.Center;
        trendLabel.fontStyle = FontStyles.Bold;

        MakeStatRow(overallCard, H("\u05E1\u05D4\"\u05DB \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD"), $"{_data.totalSessions}"); // סה"כ משחקים
        MakeStatRow(overallCard, H("\u05D6\u05DE\u05DF \u05DE\u05E9\u05D7\u05E7 \u05DB\u05D5\u05DC\u05DC"), _data.totalPlayTimeDisplay); // זמן משחק כולל
        FitCard(overallCard);

        // ── Games improving ──
        var improvingGames = new List<GameDashboardData>();
        var stableGames = new List<GameDashboardData>();
        var decliningGames = new List<GameDashboardData>();

        foreach (var g in _data.games)
        {
            if (g.trend > 2f) improvingGames.Add(g);
            else if (g.trend < -2f) decliningGames.Add(g);
            else stableGames.Add(g);
        }

        if (improvingGames.Count > 0)
        {
            var card = MakeCard(parent, 0f);
            MakeSectionTitle(card, H("\u05DE\u05E9\u05EA\u05E4\u05E8\u05D9\u05DD \u2191")); // משתפרים ↑
            foreach (var g in improvingGames)
                MakeTrendRow(card, g, ScoreGreen);
            FitCard(card);
        }

        if (stableGames.Count > 0)
        {
            var card = MakeCard(parent, 0f);
            MakeSectionTitle(card, H("\u05D9\u05E6\u05D9\u05D1\u05D9\u05DD \u2194")); // יציבים ↔
            foreach (var g in stableGames)
                MakeTrendRow(card, g, TextMedium);
            FitCard(card);
        }

        if (decliningGames.Count > 0)
        {
            var card = MakeCard(parent, 0f);
            MakeSectionTitle(card, H("\u05E6\u05E8\u05D9\u05DB\u05D9\u05DD \u05EA\u05E8\u05D2\u05D5\u05DC \u2193")); // צריכים תרגול ↓
            foreach (var g in decliningGames)
                MakeTrendRow(card, g, HexColor("#E74C3C"));
            FitCard(card);
        }

        // ── Category Trends ──
        var catTrendCard = MakeCard(parent, 0f);
        MakeSectionTitle(catTrendCard, H("\u05DE\u05D2\u05DE\u05D5\u05EA \u05DC\u05E4\u05D9 \u05EA\u05D7\u05D5\u05DD")); // מגמות לפי תחום

        foreach (var cat in _data.categories)
        {
            if (cat.contributingGamesCount == 0) continue;
            string arrow = ParentDashboardViewModel.TrendArrow(cat.trend);
            string line = $"{arrow} {cat.score:F0} - {H(cat.categoryName)}";
            var row = MakeChildText(catTrendCard, line, 16, TextDark);
            row.alignment = TextAlignmentOptions.Right;
        }

        FitCard(catTrendCard);
        MakeSpacer(parent, 40f);
    }

    private void MakeTrendRow(Transform parent, GameDashboardData game, Color color)
    {
        var row = new GameObject("TrendRow");
        row.transform.SetParent(parent, false);
        var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 8;
        rowLayout.childAlignment = TextAnchor.MiddleRight;
        rowLayout.childForceExpandWidth = false;
        var rowLE = row.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 32;

        var score = MakeChildText(row.transform, $"{game.score:F0}", 16, color);
        score.alignment = TextAlignmentOptions.Left;
        var scoreLE2 = score.gameObject.AddComponent<LayoutElement>();
        scoreLE2.preferredWidth = 40;

        var name = MakeChildText(row.transform, H(game.gameName), 16, TextDark);
        name.alignment = TextAlignmentOptions.Right;
        var nameLE = name.gameObject.AddComponent<LayoutElement>();
        nameLE.flexibleWidth = 1;
    }

    // ═══════════════════════════════════════════════════════════════
    //  UI BUILDERS
    // ═══════════════════════════════════════════════════════════════

    private Transform MakeCard(Transform parent, float fixedHeight)
    {
        var go = new GameObject("Card");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();

        var img = go.AddComponent<Image>();
        img.sprite = roundedRect;
        img.type = Image.Type.Sliced;
        img.color = CardColor;
        img.raycastTarget = true;

        go.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.06f);

        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8;
        layout.padding = new RectOffset(20, 20, 16, 16);
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = -1;
        if (fixedHeight > 0)
            le.preferredHeight = fixedHeight;

        return go.transform;
    }

    private void FitCard(Transform card)
    {
        var fitter = card.gameObject.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = card.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private TextMeshProUGUI MakeText(Transform parent, string text, int fontSize, Color color)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.isRightToLeftText = false;
        tmp.raycastTarget = false;
        tmp.alignment = TextAlignmentOptions.Right;

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = fontSize + 12;

        return tmp;
    }

    private TextMeshProUGUI MakeChildText(Transform parent, string text, int fontSize, Color color)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.isRightToLeftText = false;
        tmp.raycastTarget = false;
        return tmp;
    }

    private void MakeSectionTitle(Transform parent, string text)
    {
        var tmp = MakeText(parent, text, 20, TextDark);
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Right;
    }

    private void MakeStatRow(Transform parent, string label, string value)
    {
        var go = new GameObject("StatRow");
        go.transform.SetParent(parent, false);
        var layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8;
        layout.childForceExpandWidth = false;
        layout.childAlignment = TextAnchor.MiddleRight;
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 32;

        var valText = MakeChildText(go.transform, value, 16, Primary);
        valText.alignment = TextAlignmentOptions.Left;
        valText.fontStyle = FontStyles.Bold;
        var valLE = valText.gameObject.AddComponent<LayoutElement>();
        valLE.preferredWidth = 120;

        var labelText = MakeChildText(go.transform, label, 16, TextMedium);
        labelText.alignment = TextAlignmentOptions.Right;
        var labelLE = labelText.gameObject.AddComponent<LayoutElement>();
        labelLE.flexibleWidth = 1;
    }

    private void MakeDetailRow(Transform parent, string label, string value)
    {
        MakeStatRow(parent, label, value);
    }

    private void MakeProgressBar(Transform parent, float fill, Color barColor, float height)
    {
        var go = new GameObject("ProgressBar");
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;

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

    private void MakeCategoryChip(Transform parent, CategoryDashboardData cat)
    {
        var go = new GameObject("Chip");
        go.transform.SetParent(parent, false);
        var layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8;
        layout.padding = new RectOffset(12, 12, 6, 6);
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.childForceExpandWidth = false;
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 40;

        var bg = go.AddComponent<Image>();
        bg.sprite = roundedRect;
        bg.type = Image.Type.Sliced;
        bg.color = new Color(cat.color.r, cat.color.g, cat.color.b, 0.1f);
        bg.raycastTarget = false;

        var score = MakeChildText(go.transform, $"{cat.score:F0}", 16, cat.color);
        score.fontStyle = FontStyles.Bold;
        score.alignment = TextAlignmentOptions.Left;
        var scoreLE = score.gameObject.AddComponent<LayoutElement>();
        scoreLE.preferredWidth = 35;

        var name = MakeChildText(go.transform, H(cat.categoryName), 16, TextDark);
        name.alignment = TextAlignmentOptions.Right;
        var nameLE = name.gameObject.AddComponent<LayoutElement>();
        nameLE.flexibleWidth = 1;
    }

    private void MakeMiniCategoryRow(Transform parent, CategoryDashboardData cat)
    {
        var go = new GameObject("CatRow");
        go.transform.SetParent(parent, false);
        var layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.childForceExpandWidth = false;
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 36;

        // Score
        var score = MakeChildText(go.transform, $"{cat.score:F0}", 16, cat.color);
        score.fontStyle = FontStyles.Bold;
        score.alignment = TextAlignmentOptions.Left;
        var scoreLE = score.gameObject.AddComponent<LayoutElement>();
        scoreLE.preferredWidth = 35;

        // Mini bar
        var barGO = new GameObject("MiniBar");
        barGO.transform.SetParent(go.transform, false);
        var barLE = barGO.AddComponent<LayoutElement>();
        barLE.preferredWidth = 80;
        barLE.preferredHeight = 10;
        barLE.flexibleWidth = 0;
        var barBgImg = barGO.AddComponent<Image>();
        barBgImg.sprite = roundedRect;
        barBgImg.type = Image.Type.Sliced;
        barBgImg.color = BarBg;
        barBgImg.raycastTarget = false;

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(barGO.transform, false);
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
        var arrow = MakeChildText(go.transform, ParentDashboardViewModel.TrendArrow(cat.trend), 14, TextMedium);
        arrow.alignment = TextAlignmentOptions.Center;
        var arrowLE = arrow.gameObject.AddComponent<LayoutElement>();
        arrowLE.preferredWidth = 20;

        // Name
        var name = MakeChildText(go.transform, H(cat.categoryName), 15, TextDark);
        name.alignment = TextAlignmentOptions.Right;
        var nameLE = name.gameObject.AddComponent<LayoutElement>();
        nameLE.flexibleWidth = 1;
    }

    private void MakeDivider(Transform parent)
    {
        var go = new GameObject("Divider");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = Divider;
        img.raycastTarget = false;
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 1;
    }

    private void MakeSpacer(Transform parent, float height)
    {
        var go = new GameObject("Spacer");
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
    }

    // ── Navigation ──

    public void OnBackPressed() => BubbleTransition.LoadScene("HomeScene");

    // ── Helpers ──

    /// <summary>Apply HebrewFixer for visual RTL rendering.</summary>
    private static string H(string raw) => HebrewFixer.Fix(raw);

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
