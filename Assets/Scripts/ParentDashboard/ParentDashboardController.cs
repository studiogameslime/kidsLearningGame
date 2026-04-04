using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

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

    [Header("Settings")]
    public Button settingsButton;
    public Sprite gearSprite;

    [Header("UI Kit Sprites")]
    public Sprite uiCardBlue;       // UI_2_0 — blue card background
    public Sprite uiCardPurple;     // UI_2_2 — purple card background
    public Sprite uiToggleGreen;    // UI_2_14
    public Sprite uiToggleRed;      // UI_2_15
    public Sprite uiBtnRounded;     // UI_1_41 — rounded button
    public Sprite uiBtnRoundedAlt;  // UI_1_42
    public Sprite uiPlus;           // UI_1_16
    public Sprite uiMinus;          // UI_1_30
    public Sprite uiBarBlue;        // UI_1_33
    public Sprite uiBarGreen;       // UI_1_34
    public Sprite uiBarYellow;      // UI_1_35
    public Sprite uiSectionBg;      // UI_1_49 — section background
    public Sprite uiCheckIcon;      // UI_1_5  — checkmark icon (active indicator)
    public Sprite uiBarGray;        // UI_1_44 — gray bar/button
    public Sprite uiPlaceholder;    // UI_2_6  — question mark card (fallback thumbnail)
    public Sprite uiShareIcon;      // UI_1_69 — share icon

    [Header("Assets")]
    public Sprite roundedRect;
    public Sprite circleSprite;
    public GameDatabase gameDatabase;

    // ── Colors (Dark Mode) ──
    private static readonly Color CardColor = HexColor("#1E2A3A");
    private static readonly Color BgColor = HexColor("#0F1923");
    private static readonly Color Primary = HexColor("#4FC3F7");
    private static readonly Color TextDark = HexColor("#E8EDF2");
    private static readonly Color TextMedium = HexColor("#A0B0C0");
    private static readonly Color TextLight = HexColor("#607080");
    private static readonly Color BarBg = HexColor("#1A2636");
    private static readonly Color Divider = HexColor("#2A3A4A");
    private static readonly Color AccentGreen = HexColor("#66BB6A");
    private static readonly Color AccentOrange = HexColor("#FFB74D");
    private static readonly Color AccentRed = HexColor("#EF5350");
    private static readonly Color StrengthBg = HexColor("#1B3A2A");
    private static readonly Color PracticeBg = HexColor("#3A2A10");
    private static readonly Color InsightBg = HexColor("#152A3A");
    private static readonly Color BadgeBg = HexColor("#FFF8E1");
    private static readonly Color GoldAccent = HexColor("#F1C40F");
    private static readonly Color HighlightBg = HexColor("#E3F2FD");

    private ParentDashboardData _data;
    private int _correctAnswer;
    private GameObject _leaderboardModal;
    private GameObject _settingsModal;

    // Dynamic content tab state
    private const int TabStatistics = 0;
    private const int TabGames = 1;
    private const int TabGallery = 2;
    private int _activeContentTab = TabStatistics;
    private Button _statsTabBtn;
    private Button _gamesTabBtn;
    private Button _galleryTabBtn;
    private GameObject _statsTabBar; // runtime-created tab bar

    // Gallery state
    private List<GameObject> _galleryThumbnails = new List<GameObject>();
    private List<Texture2D> _galleryTextures = new List<Texture2D>();

    private void Start()
    {
        // Increase drag threshold so taps on buttons inside ScrollRects aren't stolen by scroll
        var es = EventSystem.current;
        if (es != null) es.pixelDragThreshold = Mathf.Max(es.pixelDragThreshold, (int)(Screen.dpi * 0.12f));

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
        if (settingsButton != null)
            settingsButton.onClick.AddListener(ShowSettings);
    }

    // ═══════════════════════════════════════════════════════════════
    //  PARENTAL GATE
    // ═══════════════════════════════════════════════════════════════

    private void GenerateQuestion()
    {
        // Harder question: multiplication or larger addition
        int op = Random.Range(0, 3); // 0=add, 1=subtract, 2=multiply
        int a, b;
        string question;

        switch (op)
        {
            case 0: // addition with larger numbers
                a = Random.Range(12, 30);
                b = Random.Range(8, 25);
                _correctAnswer = a + b;
                question = $"{a} + {b} = ?";
                break;
            case 1: // subtraction
                a = Random.Range(20, 50);
                b = Random.Range(5, a - 3);
                _correctAnswer = a - b;
                question = $"{a} - {b} = ?";
                break;
            default: // multiplication
                a = Random.Range(3, 10);
                b = Random.Range(3, 10);
                _correctAnswer = a * b;
                question = $"{a} × {b} = ?";
                break;
        }

        // Math equation displayed LTR (not Hebrew RTL processed)
        questionText.isRightToLeftText = false;
        questionText.text = question;

        var answers = new List<int> { _correctAnswer };
        while (answers.Count < 4)
        {
            int wrong = _correctAnswer + Random.Range(-5, 6);
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
        // Show interstitial ad when entering parent dashboard
        if (InterstitialAdManager.Instance != null)
        {
            InterstitialAdManager.Instance.ShowAd(OpenDashboard);
        }
        else
        {
            OpenDashboard();
        }
    }

    private void OpenDashboard()
    {
        FirebaseAnalyticsManager.LogParentDashboardOpened();
        gatePanel.gameObject.SetActive(false);
        dashboardPanel.gameObject.SetActive(true);
        LoadData();
        BuildAllTabs();

        // Request store review only after sufficient engagement (3+ visits)
        int visitCount = PlayerPrefs.GetInt("dashboard_visit_count", 0) + 1;
        PlayerPrefs.SetInt("dashboard_visit_count", visitCount);
        if (visitCount >= 3)
            StoreReviewManager.TryRequestReview();

        // Disable kerning on all newly created TMP components
        // (the runtime cleaner only caught the gate panel components at scene load)
        DisableKerningOnDashboard();

        // Show banner ad only after parental gate is passed
        if (BannerAdManager.Instance != null)
            BannerAdManager.Instance.ShowBanner();
    }

    private void DisableKerningOnDashboard()
    {
        var allTmp = dashboardPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var tmp in allTmp)
        {
            #pragma warning disable 0618
            if (tmp.enableKerning)
                tmp.enableKerning = false;
            #pragma warning restore 0618
        }
        Debug.Log($"[GlyphCleaner] Dashboard: disabled kerning on {allTmp.Length} TMP component(s)");
    }

    // ═══════════════════════════════════════════════════════════════
    //  DATA
    // ═══════════════════════════════════════════════════════════════

    private void LoadData()
    {
        _data = ParentDashboardViewModel.Build(gameDatabase);
        if (_data == null) return;

        if (headerNameText != null)
        {
            HebrewText.SetText(headerNameText, _data.profileName);
            headerNameText.enableWordWrapping = false;
        }
        if (headerAgeText != null)
        {
            string ageLabel = _data.ageDisplay != "---"
                ? $"\u05D2\u05D9\u05DC {_data.ageDisplay}" // גיל X
                : "";
            HebrewText.SetText(headerAgeText, ageLabel);
            headerAgeText.enableWordWrapping = false;
        }
        if (headerSessionsText != null)
        {
            string sessLabel = $"{_data.totalSessions} \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD"; // X משחקים
            HebrewText.SetText(headerSessionsText, sessLabel);
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
        // Clean up previous content (for re-entry)
        if (_statsTabBar != null) Destroy(_statsTabBar);
        for (int i = 0; i < tabContents.Length; i++)
        {
            // Destroy all children of each scroll content
            for (int c = tabContents[i].childCount - 1; c >= 0; c--)
                Destroy(tabContents[i].GetChild(c).gameObject);
        }
        // Clean gallery textures from previous session
        foreach (var tex in _galleryTextures)
            if (tex != null) Destroy(tex);
        _galleryTextures.Clear();
        _galleryThumbnails.Clear();

        // Create tab bar between header and scroll content
        BuildContentTabBar();

        // Hide all scroll views initially
        for (int i = 0; i < tabContents.Length; i++)
            tabContents[i].parent.gameObject.SetActive(false);

        // ScrollView 0 = Statistics (overview + categories + trends)
        tabContents[0].parent.gameObject.SetActive(true);
        BuildOverviewTab();

        // ScrollView 1 = Games (access + recommendation cards)
        BuildGamesTabContent();

        // ScrollView 2 = Gallery (parent image uploads)
        BuildGalleryTabContent();

        // Show statistics tab by default
        SwitchContentTab(TabStatistics);
    }

    private void BuildContentTabBar()
    {
        if (tabContents.Length < 2) return;

        // Insert tab bar between header and scroll views by parenting it to the dashboard panel
        var dashPanel = dashboardPanel;
        _statsTabBar = new GameObject("ContentTabBar");
        _statsTabBar.transform.SetParent(dashPanel, false);
        var barRT = _statsTabBar.AddComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0, 1);
        barRT.anchorMax = new Vector2(1, 1);
        barRT.pivot = new Vector2(0.5f, 1);
        barRT.anchoredPosition = new Vector2(0, -130); // below 130px header
        barRT.sizeDelta = new Vector2(0, 70);

        // Background
        var barBg = _statsTabBar.AddComponent<Image>();
        barBg.color = HexColor("#162030");
        _statsTabBar.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.3f);

        // Layout
        var barLayout = _statsTabBar.AddComponent<HorizontalLayoutGroup>();
        barLayout.spacing = 8;
        barLayout.padding = new RectOffset(20, 20, 8, 8);
        barLayout.childAlignment = TextAnchor.MiddleCenter;
        barLayout.childForceExpandWidth = true;
        barLayout.childForceExpandHeight = true;
        barLayout.childControlWidth = true;
        barLayout.childControlHeight = true;

        // Statistics tab button
        _statsTabBtn = MakeContentTabButton(_statsTabBar.transform,
            H("\u05E1\u05D8\u05D8\u05D9\u05E1\u05D8\u05D9\u05E7\u05D5\u05EA"), true); // סטטיסטיקות
        _statsTabBtn.onClick.AddListener(() => SwitchContentTab(TabStatistics));

        // Games tab button
        _gamesTabBtn = MakeContentTabButton(_statsTabBar.transform,
            H("\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD"), false); // משחקים
        _gamesTabBtn.onClick.AddListener(() => SwitchContentTab(TabGames));

        // Gallery tab button
        _galleryTabBtn = MakeContentTabButton(_statsTabBar.transform,
            H("\u05D2\u05DC\u05E8\u05D9\u05D4"), false); // גלריה
        _galleryTabBtn.onClick.AddListener(() => SwitchContentTab(TabGallery));

        // Push scroll views down by tab bar height (130 header + 70 tab bar = 200)
        float topOffset = 200;
        for (int i = 0; i < tabContents.Length; i++)
        {
            var svRT = tabContents[i].parent.GetComponent<RectTransform>();
            svRT.offsetMax = new Vector2(0, -topOffset);
        }
    }

    private Button MakeContentTabButton(Transform parent, string label, bool active)
    {
        var go = new GameObject("Tab");
        go.transform.SetParent(parent, false);
        go.AddComponent<LayoutElement>().flexibleWidth = 1;

        var img = go.AddComponent<Image>();
        img.sprite = uiBtnRounded != null ? uiBtnRounded : roundedRect;
        img.type = Image.Type.Sliced;
        img.color = active ? Primary : new Color(1f, 1f, 1f, 0.08f);
        img.raycastTarget = true;

        // Bottom indicator (thicker)
        var indicator = new GameObject("Indicator");
        indicator.transform.SetParent(go.transform, false);
        var indRT = indicator.AddComponent<RectTransform>();
        indRT.anchorMin = new Vector2(0.1f, 0);
        indRT.anchorMax = new Vector2(0.9f, 0);
        indRT.pivot = new Vector2(0.5f, 0);
        indRT.sizeDelta = new Vector2(0, 4);
        var indImg = indicator.AddComponent<Image>();
        indImg.color = active ? Color.white : Color.clear;
        indImg.raycastTarget = false;
        if (roundedRect != null) { indImg.sprite = roundedRect; indImg.type = Image.Type.Sliced; }

        // Label (larger)
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(tmp, label);
        tmp.fontSize = 28;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = active ? Color.white : TextMedium;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;

        return go.AddComponent<Button>();
    }

    private void SwitchContentTab(int tab)
    {
        _activeContentTab = tab;
        string tabName = tab == TabStatistics ? "statistics" : tab == TabGames ? "games" : "gallery";
        FirebaseAnalyticsManager.LogParentTabSwitched(tabName);

        // Toggle scroll views
        if (tabContents.Length > 0) tabContents[0].parent.gameObject.SetActive(tab == TabStatistics);
        if (tabContents.Length > 1) tabContents[1].parent.gameObject.SetActive(tab == TabGames);
        if (tabContents.Length > 2) tabContents[2].parent.gameObject.SetActive(tab == TabGallery);

        // Update button visuals
        UpdateContentTabButton(_statsTabBtn, tab == TabStatistics);
        UpdateContentTabButton(_gamesTabBtn, tab == TabGames);
        UpdateContentTabButton(_galleryTabBtn, tab == TabGallery);
    }

    private void UpdateContentTabButton(Button btn, bool active)
    {
        if (btn == null) return;

        // Update tab background
        var bgImg = btn.GetComponent<Image>();
        if (bgImg != null) bgImg.color = active ? Primary : new Color(1f, 1f, 1f, 0.08f);

        // Update indicator
        var indicator = btn.transform.Find("Indicator");
        if (indicator != null)
        {
            var indImg = indicator.GetComponent<Image>();
            if (indImg != null) indImg.color = active ? Color.white : Color.clear;
        }

        // Update label color
        var label = btn.transform.Find("Label");
        if (label != null)
        {
            var tmp = label.GetComponent<TextMeshProUGUI>();
            if (tmp != null) tmp.color = active ? Color.white : TextMedium;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  GALLERY TAB
    // ═══════════════════════════════════════════════════════════════

    private void BuildGalleryTabContent()
    {
        if (tabContents.Length < 3) return;
        var parent = tabContents[2];

        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        // ── Add Image button ──
        var addBtnGO = new GameObject("AddImageButton");
        addBtnGO.transform.SetParent(parent, false);
        var addBtnRT = addBtnGO.AddComponent<RectTransform>();
        addBtnRT.sizeDelta = new Vector2(0, 60);

        var addBtnImg = addBtnGO.AddComponent<Image>();
        if (roundedRect != null) { addBtnImg.sprite = roundedRect; addBtnImg.type = Image.Type.Sliced; }
        addBtnImg.color = Primary;
        addBtnImg.raycastTarget = true;

        var addBtnComp = addBtnGO.AddComponent<Button>();
        addBtnComp.targetGraphic = addBtnImg;
        addBtnComp.onClick.AddListener(OnAddParentImage);

        var addTextGO = new GameObject("Label");
        addTextGO.transform.SetParent(addBtnGO.transform, false);
        var addTextRT = addTextGO.AddComponent<RectTransform>();
        addTextRT.anchorMin = Vector2.zero;
        addTextRT.anchorMax = Vector2.one;
        addTextRT.offsetMin = Vector2.zero;
        addTextRT.offsetMax = Vector2.zero;
        var addTMP = addTextGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(addTMP, "+ \u05D4\u05D5\u05E1\u05E3 \u05EA\u05DE\u05D5\u05E0\u05D4"); // + הוסף תמונה
        addTMP.fontSize = 31;
        addTMP.fontStyle = FontStyles.Bold;
        addTMP.color = Color.white;
        addTMP.alignment = TextAlignmentOptions.Center;
        addTMP.raycastTarget = false;

        // ── Image grid container ──
        var gridGO = new GameObject("ImageGrid");
        gridGO.transform.SetParent(parent, false);
        var gridRT = gridGO.AddComponent<RectTransform>();
        var gridLayout = gridGO.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(200, 200);
        gridLayout.spacing = new Vector2(16, 16);
        gridLayout.padding = new RectOffset(10, 10, 10, 10);
        gridLayout.childAlignment = TextAnchor.UpperCenter;
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperRight; // RTL
        var gridCSF = gridGO.AddComponent<ContentSizeFitter>();
        gridCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ── Load existing parent images ──
        string basePath = Application.persistentDataPath;
        bool hasImages = false;

        if (profile.parentImages != null)
        {
            for (int i = profile.parentImages.Count - 1; i >= 0; i--)
            {
                var img = profile.parentImages[i];
                string fullPath = System.IO.Path.Combine(basePath, img.imagePath);
                if (!System.IO.File.Exists(fullPath)) continue;

                byte[] data = System.IO.File.ReadAllBytes(fullPath);
                var tex = new Texture2D(2, 2);
                if (!tex.LoadImage(data)) { Destroy(tex); continue; }

                _galleryTextures.Add(tex);
                CreateGalleryThumbnail(gridRT, tex, i);
                hasImages = true;
            }
        }

        // ── Empty state ──
        if (!hasImages)
        {
            var emptyGO = new GameObject("EmptyState");
            emptyGO.transform.SetParent(parent, false);
            var emptyRT = emptyGO.AddComponent<RectTransform>();
            emptyRT.sizeDelta = new Vector2(0, 100);
            var emptyTMP = emptyGO.AddComponent<TextMeshProUGUI>();
            HebrewText.SetText(emptyTMP,
                "\u05D0\u05D9\u05DF \u05E2\u05D3\u05D9\u05D9\u05DF \u05EA\u05DE\u05D5\u05E0\u05D5\u05EA.\n\u05D4\u05D5\u05E1\u05D9\u05E4\u05D5 \u05EA\u05DE\u05D5\u05E0\u05D5\u05EA \u05DC\u05D9\u05DC\u05D3 \u05DC\u05E9\u05D7\u05E7 \u05D0\u05D9\u05EA\u05DF.");
            // אין עדיין תמונות.\nהוסיפו תמונות לילד לשחק איתן.
            emptyTMP.fontSize = 31;
            emptyTMP.color = TextMedium;
            emptyTMP.alignment = TextAlignmentOptions.Center;
            emptyTMP.raycastTarget = false;
        }
    }

    private void CreateGalleryThumbnail(RectTransform grid, Texture2D tex, int imageIndex)
    {
        var go = new GameObject($"ParentImg_{imageIndex}");
        go.transform.SetParent(grid, false);

        var bgImg = go.AddComponent<Image>();
        if (roundedRect != null) { bgImg.sprite = roundedRect; bgImg.type = Image.Type.Sliced; }
        bgImg.color = Color.white;
        bgImg.raycastTarget = true;

        // Image
        var imgGO = new GameObject("Image");
        imgGO.transform.SetParent(go.transform, false);
        var irt = imgGO.AddComponent<RectTransform>();
        irt.anchorMin = new Vector2(0.05f, 0.05f);
        irt.anchorMax = new Vector2(0.95f, 0.95f);
        irt.offsetMin = Vector2.zero;
        irt.offsetMax = Vector2.zero;
        var rawImg = imgGO.AddComponent<RawImage>();
        rawImg.texture = tex;
        rawImg.raycastTarget = false;

        // Delete button (small X in corner)
        var delGO = new GameObject("DeleteBtn");
        delGO.transform.SetParent(go.transform, false);
        var delRT = delGO.AddComponent<RectTransform>();
        delRT.anchorMin = new Vector2(0, 1);
        delRT.anchorMax = new Vector2(0, 1);
        delRT.pivot = new Vector2(0, 1);
        delRT.anchoredPosition = new Vector2(4, -4);
        delRT.sizeDelta = new Vector2(32, 32);
        var delImg = delGO.AddComponent<Image>();
        if (circleSprite != null) delImg.sprite = circleSprite;
        delImg.color = new Color(0.9f, 0.3f, 0.3f, 0.85f);
        delImg.raycastTarget = true;

        var delTextGO = new GameObject("X");
        delTextGO.transform.SetParent(delGO.transform, false);
        var dtRT = delTextGO.AddComponent<RectTransform>();
        dtRT.anchorMin = Vector2.zero;
        dtRT.anchorMax = Vector2.one;
        dtRT.offsetMin = Vector2.zero;
        dtRT.offsetMax = Vector2.zero;
        var dtTMP = delTextGO.AddComponent<TextMeshProUGUI>();
        dtTMP.text = "\u00D7"; // ×
        dtTMP.fontSize = 28;
        dtTMP.fontStyle = FontStyles.Bold;
        dtTMP.color = Color.white;
        dtTMP.alignment = TextAlignmentOptions.Center;
        dtTMP.raycastTarget = false;

        var delBtn = delGO.AddComponent<Button>();
        delBtn.targetGraphic = delImg;
        int capturedIdx = imageIndex;
        delBtn.onClick.AddListener(() => OnDeleteParentImage(capturedIdx));

        _galleryThumbnails.Add(go);
    }

    private void OnAddParentImage()
    {
#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel("Select Image", "", "png,jpg,jpeg");
        if (!string.IsNullOrEmpty(path))
        {
            byte[] data = System.IO.File.ReadAllBytes(path);
            SaveParentImage(data);
        }
#else
        NativeGallery.GetImageFromGallery((path) =>
        {
            if (string.IsNullOrEmpty(path)) return;
            byte[] data = System.IO.File.ReadAllBytes(path);
            SaveParentImage(data);
        }, "Select Image");
#endif
    }

    private void SaveParentImage(byte[] imageData)
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        // Resize if needed (max 1024px)
        var tex = new Texture2D(2, 2);
        if (!tex.LoadImage(imageData)) { Destroy(tex); return; }

        if (tex.width > 1024 || tex.height > 1024)
        {
            float scale = 1024f / Mathf.Max(tex.width, tex.height);
            var rt = RenderTexture.GetTemporary((int)(tex.width * scale), (int)(tex.height * scale));
            Graphics.Blit(tex, rt);
            var resized = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            resized.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            resized.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            Destroy(tex);
            tex = resized;
        }

        byte[] png = tex.EncodeToPNG();
        Destroy(tex);

        // Save to disk
        string profileFolder = ProfileManager.Instance.GetProfileFolder(profile.id);
        string imagesDir = System.IO.Path.Combine(profileFolder, "parent_images");
        if (!System.IO.Directory.Exists(imagesDir))
            System.IO.Directory.CreateDirectory(imagesDir);

        string fileName = $"parent_{System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.png";
        string fullPath = System.IO.Path.Combine(imagesDir, fileName);
        System.IO.File.WriteAllBytes(fullPath, png);

        // Add to profile
        string relativePath = $"profiles/{profile.id}/parent_images/{fileName}";
        profile.parentImages.Add(new ParentImage
        {
            imagePath = relativePath,
            label = "",
            createdAt = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
        ProfileManager.Instance.Save();

        // Refresh gallery
        RefreshGalleryTab();
    }

    private void OnDeleteParentImage(int imageIndex)
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null || profile.parentImages == null) return;
        if (imageIndex < 0 || imageIndex >= profile.parentImages.Count) return;

        // Delete file
        string basePath = Application.persistentDataPath;
        string fullPath = System.IO.Path.Combine(basePath, profile.parentImages[imageIndex].imagePath);
        if (System.IO.File.Exists(fullPath))
            System.IO.File.Delete(fullPath);

        // Remove from profile
        profile.parentImages.RemoveAt(imageIndex);
        ProfileManager.Instance.Save();

        // Refresh gallery
        RefreshGalleryTab();
    }

    private void RefreshGalleryTab()
    {
        // Clean up old thumbnails
        foreach (var go in _galleryThumbnails)
            if (go != null) Destroy(go);
        _galleryThumbnails.Clear();
        foreach (var tex in _galleryTextures)
            if (tex != null) Destroy(tex);
        _galleryTextures.Clear();

        // Clear scroll content for gallery tab
        if (tabContents.Length > 2)
        {
            var content = tabContents[2];
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);
        }

        // Rebuild
        BuildGalleryTabContent();
    }

    // ═══════════════════════════════════════════════════════════════
    //  OVERVIEW TAB
    // ═══════════════════════════════════════════════════════════════

    private void BuildOverviewTab()
    {
        if (_data == null) return;
        var parent = tabContents[0];
        var profile = ProfileManager.ActiveProfile;
        int totalStars = profile?.journey?.totalStars ?? 0;

        // Disable outer scroll/CSF so flexibleHeight works (content fits viewport)
        var parentCSF = parent.GetComponent<ContentSizeFitter>();
        if (parentCSF != null) parentCSF.enabled = false;
        var parentVLG = parent.GetComponent<VerticalLayoutGroup>();
        if (parentVLG != null)
        {
            parentVLG.childForceExpandHeight = false;
            parentVLG.padding = new RectOffset(20, 20, 16, 16);
            parentVLG.spacing = 14;
        }
        var outerScroll = parent.parent != null ? parent.parent.GetComponent<ScrollRect>() : null;
        if (outerScroll != null) outerScroll.enabled = false;
        parent.anchorMin = Vector2.zero;
        parent.anchorMax = Vector2.one;
        parent.pivot = new Vector2(0.5f, 0.5f);
        parent.offsetMin = new Vector2(0, 120); // bottom margin for ad banner
        parent.offsetMax = Vector2.zero;

        // Find favorite game for top row
        GameDashboardData favGame = null;
        if (_data.games != null && _data.games.Count > 0)
        {
            favGame = _data.games[0];
            foreach (var g in _data.games)
                if (g.sessionsPlayed > favGame.sessionsPlayed) favGame = g;
        }

        // ═══════════════════════════════════════════════════════════
        //  ROW 1: TOP SUMMARY — 4 items (stats + favorite game)
        // ═══════════════════════════════════════════════════════════
        var topRow = MakeHRow(parent, 120, TextAnchor.MiddleCenter);
        var trHL = topRow.GetComponent<HorizontalLayoutGroup>();
        trHL.spacing = 12;
        trHL.childForceExpandWidth = true;

        MakeStatsSummaryBlock(topRow.transform,
            $"{_data.totalSessions}",
            H("\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD")); // משחקים
        MakeStatsSummaryBlock(topRow.transform,
            H(_data.totalPlayTimeDisplay),
            H("\u05D6\u05DE\u05DF \u05DE\u05E9\u05D7\u05E7")); // זמן משחק
        MakeStatsSummaryBlock(topRow.transform,
            $"{totalStars}",
            H("\u05DB\u05D5\u05DB\u05D1\u05D9\u05DD")); // כוכבים

        // Favorite game block (4th item in top row)
        BuildFavoriteGameBlock(topRow.transform, favGame);

        // ═══════════════════════════════════════════════════════════
        //  ROW 2: CHART (left 50%) + MOST PLAYED (right 50%)
        // ═══════════════════════════════════════════════════════════
        var midRow = MakeHRow(parent, 0, TextAnchor.UpperCenter);
        midRow.AddComponent<LayoutElement>().flexibleHeight = 3; // takes most space but not all
        var mrHL = midRow.GetComponent<HorizontalLayoutGroup>();
        mrHL.spacing = 14;
        mrHL.childForceExpandWidth = true;
        mrHL.childForceExpandHeight = true;

        BuildWeeklyChart(midRow.transform);
        BuildMostPlayedList(midRow.transform);

        // ═══════════════════════════════════════════════════════════
        //  ROW 3: SHARE CARD
        // ═══════════════════════════════════════════════════════════
        BuildStatsShareCard(parent);
    }

    // ── Summary stat block (compact, large text) ──
    private void MakeStatsSummaryBlock(Transform parent, string value, string label)
    {
        var go = new GameObject("SummaryBlock");
        go.transform.SetParent(parent, false);
        var bgImg = go.AddComponent<Image>();
        bgImg.sprite = uiSectionBg != null ? uiSectionBg : roundedRect;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = CardColor;
        bgImg.raycastTarget = false;
        go.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.12f);
        go.AddComponent<LayoutElement>().flexibleWidth = 1;

        var vl = go.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 2;
        vl.padding = new RectOffset(8, 8, 12, 12);
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight = false;
        vl.childControlWidth = true;
        vl.childControlHeight = true;
        vl.childAlignment = TextAnchor.MiddleCenter;

        var valTMP = AddChildTMP(go.transform, value, 48, TextDark, TextAlignmentOptions.Center);
        valTMP.fontStyle = FontStyles.Bold;
        valTMP.enableAutoSizing = true; valTMP.fontSizeMin = 32; valTMP.fontSizeMax = 48;
        valTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 54;

        var lblTMP = AddChildTMP(go.transform, label, 20, TextMedium, TextAlignmentOptions.Center);
        lblTMP.enableAutoSizing = true; lblTMP.fontSizeMin = 16; lblTMP.fontSizeMax = 20;
        lblTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
    }

    // ── Favorite game block (compact, fits in top summary row) ──
    private void BuildFavoriteGameBlock(Transform parent, GameDashboardData favGame)
    {
        var go = new GameObject("FavBlock");
        go.transform.SetParent(parent, false);
        var bgImg = go.AddComponent<Image>();
        bgImg.sprite = uiSectionBg != null ? uiSectionBg : roundedRect;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = CardColor;
        bgImg.raycastTarget = false;
        go.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.12f);
        go.AddComponent<LayoutElement>().flexibleWidth = 1;

        var vl = go.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 2;
        vl.padding = new RectOffset(8, 8, 8, 8);
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight = false;
        vl.childControlWidth = true;
        vl.childControlHeight = true;
        vl.childAlignment = TextAnchor.MiddleCenter;

        if (favGame == null || favGame.sessionsPlayed <= 0)
        {
            AddChildTMP(go.transform, H("\u05D4\u05DE\u05E9\u05D7\u05E7 \u05D4\u05D0\u05D4\u05D5\u05D1"), 18, TextMedium, TextAlignmentOptions.Center)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
            AddChildTMP(go.transform, "---", 32, TextLight, TextAlignmentOptions.Center)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
            return;
        }

        // Small game icon
        var iconHolder = new GameObject("FavIcon");
        iconHolder.transform.SetParent(go.transform, false);
        iconHolder.AddComponent<RectTransform>();
        iconHolder.AddComponent<LayoutElement>().preferredHeight = 44;

        var gameItem = FindGameItemFromDb(favGame.gameId);
        Sprite thumbSprite = (gameItem != null && gameItem.thumbnail != null)
            ? gameItem.thumbnail : uiPlaceholder;
        if (thumbSprite != null)
        {
            var thumbGO = new GameObject("Thumb");
            thumbGO.transform.SetParent(iconHolder.transform, false);
            var thumbRT = thumbGO.AddComponent<RectTransform>();
            thumbRT.anchorMin = new Vector2(0.5f, 0.5f);
            thumbRT.anchorMax = new Vector2(0.5f, 0.5f);
            thumbRT.sizeDelta = new Vector2(40, 40);
            var thumbImg = thumbGO.AddComponent<Image>();
            thumbImg.sprite = thumbSprite;
            thumbImg.preserveAspect = true;
            thumbImg.raycastTarget = false;
        }

        // Game name (large, auto-size)
        var nameTMP = AddChildTMP(go.transform, H(favGame.gameName), 22, TextDark, TextAlignmentOptions.Center);
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.enableAutoSizing = true; nameTMP.fontSizeMin = 16; nameTMP.fontSizeMax = 22;
        nameTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

        // Play count subtitle
        string playText = H($"{favGame.sessionsPlayed} \u05E4\u05E2\u05DE\u05D9\u05DD"); // X פעמים
        var playTMP = AddChildTMP(go.transform, playText, 16, TextMedium, TextAlignmentOptions.Center);
        playTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
    }

    // ── Section card helper (dark mode) ──
    private GameObject MakeStatsSection(Transform parent, string title)
    {
        var go = new GameObject("Section");
        go.transform.SetParent(parent, false);
        var bgImg = go.AddComponent<Image>();
        bgImg.sprite = uiSectionBg != null ? uiSectionBg : roundedRect;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = CardColor;
        bgImg.raycastTarget = false;
        go.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.15f);
        go.AddComponent<LayoutElement>().flexibleWidth = 1;

        var vl = go.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 10;
        vl.padding = new RectOffset(18, 18, 14, 14);
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight = false;
        vl.childControlWidth = true;
        vl.childControlHeight = true;

        if (!string.IsNullOrEmpty(title))
        {
            var t = AddChildTMP(go.transform, title, 26, TextDark, TextAlignmentOptions.Center);
            t.fontStyle = FontStyles.Bold;
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;
        }
        return go;
    }

    // ── Weekly chart (RTL order: ש ו ה ד ג ב א) ──
    private void BuildWeeklyChart(Transform parent)
    {
        var go = MakeStatsSection(parent, H("\u05D4\u05EA\u05E7\u05D3\u05DE\u05D5\u05EA \u05E9\u05D1\u05D5\u05E2\u05D9\u05EA")); // התקדמות שבועית

        var chartArea = new GameObject("ChartArea");
        chartArea.transform.SetParent(go.transform, false);
        chartArea.AddComponent<RectTransform>();
        chartArea.AddComponent<LayoutElement>().flexibleHeight = 1;

        var chartHL = chartArea.AddComponent<HorizontalLayoutGroup>();
        chartHL.spacing = 8;
        chartHL.childAlignment = TextAnchor.LowerCenter;
        chartHL.childForceExpandWidth = true;
        chartHL.childForceExpandHeight = false;
        chartHL.padding = new RectOffset(8, 8, 10, 4);

        // Day labels and indices in RTL display order (right-to-left: א ב ג ד ה ו ש)
        // Hebrew week: Sun=0(א), Mon=1(ב), Tue=2(ג), Wed=3(ד), Thu=4(ה), Fri=5(ו), Sat=6(ש)
        // RTL display: rightmost = first day = א(Sun), leftmost = last day = ש(Sat)
        string[] dayLabels = { "\u05E9", "\u05D5", "\u05D4", "\u05D3", "\u05D2", "\u05D1", "\u05D0" };
        int[] dayIndices = { 6, 5, 4, 3, 2, 1, 0 }; // Sat, Fri, Thu, Wed, Tue, Mon, Sun

        int[] dayCounts = new int[7];
        if (_data.games != null)
            foreach (var g in _data.games)
                if (g.recentSessions != null)
                    foreach (var s in g.recentSessions)
                        if (s.timestamp > 0)
                        {
                            var dt = System.DateTimeOffset.FromUnixTimeSeconds(s.timestamp).LocalDateTime;
                            dayCounts[(int)dt.DayOfWeek]++;
                        }

        int maxCount = 1;
        foreach (int c in dayCounts) if (c > maxCount) maxCount = c;

        for (int i = 0; i < 7; i++)
        {
            int di = dayIndices[i];
            var colGO = MakeVCol(chartArea.transform);
            colGO.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.LowerCenter;
            colGO.GetComponent<VerticalLayoutGroup>().spacing = 4;
            colGO.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Count label above bar
            if (dayCounts[di] > 0)
            {
                var cntTMP = AddChildTMP(colGO.transform, $"{dayCounts[di]}", 22, Primary, TextAlignmentOptions.Center);
                cntTMP.fontStyle = FontStyles.Bold;
                cntTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
            }

            // Thick bar
            float barHeight = Mathf.Max((float)dayCounts[di] / maxCount * 160f, 10f);
            var barGO = new GameObject("Bar");
            barGO.transform.SetParent(colGO.transform, false);
            barGO.AddComponent<RectTransform>();
            var barLE = barGO.AddComponent<LayoutElement>();
            barLE.preferredHeight = barHeight;
            barLE.minWidth = 20;
            var barImg = barGO.AddComponent<Image>();
            barImg.sprite = uiBarBlue != null ? uiBarBlue : roundedRect;
            barImg.type = Image.Type.Sliced;
            barImg.color = dayCounts[di] > 0 ? Primary : new Color(Primary.r, Primary.g, Primary.b, 0.3f);
            barImg.raycastTarget = false;

            // Day label
            var dayTMP = AddChildTMP(colGO.transform, dayLabels[i], 22, TextDark, TextAlignmentOptions.Center);
            dayTMP.fontStyle = FontStyles.Bold;
            dayTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
        }
    }

    // ── Favorite game card ──
    private void BuildFavoriteGameCard(Transform parent)
    {
        var go = MakeStatsSection(parent, H("\u05D4\u05DE\u05E9\u05D7\u05E7 \u05D4\u05D0\u05D4\u05D5\u05D1")); // המשחק האהוב

        if (_data.games == null || _data.games.Count == 0)
        {
            AddChildTMP(go.transform, H("\u05E2\u05D5\u05D3 \u05DC\u05D0 \u05E9\u05D5\u05D7\u05E7"), 20, TextLight, TextAlignmentOptions.Center)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
            return;
        }

        // Find most played game
        GameDashboardData fav = _data.games[0];
        foreach (var g in _data.games)
            if (g.sessionsPlayed > fav.sessionsPlayed) fav = g;

        // Game icon
        var iconHolder = new GameObject("FavIcon");
        iconHolder.transform.SetParent(go.transform, false);
        iconHolder.AddComponent<RectTransform>();
        var favIconLE = iconHolder.AddComponent<LayoutElement>();
        favIconLE.preferredHeight = 100;
        favIconLE.flexibleHeight = 1;

        var gameItem = FindGameItemFromDb(fav.gameId);
        Sprite thumbSprite = (gameItem != null && gameItem.thumbnail != null)
            ? gameItem.thumbnail : uiPlaceholder;
        if (thumbSprite != null)
        {
            var thumbGO = new GameObject("Thumb");
            thumbGO.transform.SetParent(iconHolder.transform, false);
            var thumbRT = thumbGO.AddComponent<RectTransform>();
            thumbRT.anchorMin = new Vector2(0.5f, 0.5f);
            thumbRT.anchorMax = new Vector2(0.5f, 0.5f);
            thumbRT.sizeDelta = new Vector2(90, 90);
            var thumbImg = thumbGO.AddComponent<Image>();
            thumbImg.sprite = thumbSprite;
            thumbImg.preserveAspect = true;
            thumbImg.raycastTarget = false;
        }

        // Game name (very large)
        var nameTMP = AddChildTMP(go.transform, H(fav.gameName), 30, TextDark, TextAlignmentOptions.Center);
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.enableAutoSizing = true; nameTMP.fontSizeMin = 22; nameTMP.fontSizeMax = 30;
        nameTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 38;

        // Play count (large)
        string playText = H($"\u05E9\u05D5\u05D7\u05E7 {fav.sessionsPlayed} \u05E4\u05E2\u05DE\u05D9\u05DD"); // שוחק X פעמים
        var playTMP = AddChildTMP(go.transform, playText, 22, TextMedium, TextAlignmentOptions.Center);
        playTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
    }

    // ── Most played games (big number left, game name right) ──
    private void BuildMostPlayedList(Transform parent)
    {
        var go = MakeStatsSection(parent,
            H("\u05D4\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05D4\u05DB\u05D9 \u05E4\u05D5\u05E4\u05D5\u05DC\u05E8\u05D9\u05D9\u05DD")); // המשחקים הכי פופולריים
        var listVL = go.GetComponent<VerticalLayoutGroup>();
        listVL.spacing = 8;
        listVL.childForceExpandHeight = true; // rows stretch to fill

        if (_data.games == null) return;

        var sorted = new List<GameDashboardData>(_data.games);
        sorted.Sort((a, b) => b.sessionsPlayed.CompareTo(a.sessionsPlayed));

        int count = Mathf.Min(sorted.Count, 5);
        for (int i = 0; i < count; i++)
        {
            var g = sorted[i];
            if (g.sessionsPlayed <= 0) continue;

            var row = MakeHRow(go.transform, 0, TextAnchor.MiddleCenter);
            row.AddComponent<LayoutElement>().flexibleHeight = 1; // stretch equally
            var rowHL = row.GetComponent<HorizontalLayoutGroup>();
            rowHL.spacing = 16;
            rowHL.childForceExpandWidth = false;
            rowHL.padding = new RectOffset(8, 12, 0, 0);

            // Big play count number (left side)
            var numTMP = AddChildTMP(row.transform, $"{g.sessionsPlayed}", 40, Primary, TextAlignmentOptions.Center);
            numTMP.fontStyle = FontStyles.Bold;
            numTMP.gameObject.AddComponent<LayoutElement>().preferredWidth = 60;

            // Game name (right side, large)
            var nameTMP = AddChildTMP(row.transform, H(g.gameName), 32, TextDark, TextAlignmentOptions.Right);
            nameTMP.fontStyle = FontStyles.Bold;
            nameTMP.enableAutoSizing = true; nameTMP.fontSizeMin = 22; nameTMP.fontSizeMax = 32;
            nameTMP.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        }
    }

    // ── Share card (dark mode, with share icon) ──
    private void BuildStatsShareCard(Transform parent)
    {
        var go = new GameObject("ShareCard");
        go.transform.SetParent(parent, false);
        var bgImg = go.AddComponent<Image>();
        bgImg.sprite = uiSectionBg != null ? uiSectionBg : roundedRect;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = CardColor;
        bgImg.raycastTarget = false;
        go.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.15f);
        var goLE = go.AddComponent<LayoutElement>();
        goLE.flexibleHeight = 1; // takes remaining space below chart

        var hl = go.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 16;
        hl.padding = new RectOffset(24, 24, 14, 14);
        hl.childAlignment = TextAnchor.MiddleCenter;
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = false;
        hl.childControlWidth = true;
        hl.childControlHeight = true;

        // Share icon
        var shareIcon = uiShareIcon;
        if (shareIcon != null)
        {
            var iconGO = new GameObject("ShareIcon");
            iconGO.transform.SetParent(go.transform, false);
            var iconLE = iconGO.AddComponent<LayoutElement>();
            iconLE.preferredWidth = 48;
            iconLE.preferredHeight = 48;
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = shareIcon;
            iconImg.preserveAspect = true;
            iconImg.color = Color.white;
            iconImg.raycastTarget = false;
        }

        // Text column
        var textCol = new GameObject("TextCol");
        textCol.transform.SetParent(go.transform, false);
        textCol.AddComponent<RectTransform>();
        var textVL = textCol.AddComponent<VerticalLayoutGroup>();
        textVL.spacing = 4;
        textVL.childForceExpandWidth = true;
        textVL.childForceExpandHeight = false;
        textVL.childControlWidth = true;
        textVL.childControlHeight = true;
        textVL.childAlignment = TextAnchor.MiddleRight;
        textCol.AddComponent<LayoutElement>().flexibleWidth = 1;

        var titleTMP = AddChildTMP(textCol.transform,
            H("\u05E1\u05E4\u05E8\u05D5 \u05DC\u05D7\u05D1\u05E8\u05D9\u05DD!"), // ספרו לחברים!
            22, TextDark, TextAlignmentOptions.Right);
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

        string statsLine = BuildShareStatsLine();
        var subTMP = AddChildTMP(textCol.transform, H(statsLine),
            16, TextMedium, TextAlignmentOptions.Right);
        subTMP.enableAutoSizing = true; subTMP.fontSizeMin = 13; subTMP.fontSizeMax = 16;
        subTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;

        // Share button
        var btnGO = new GameObject("ShareBtn");
        btnGO.transform.SetParent(go.transform, false);
        var btnLE = btnGO.AddComponent<LayoutElement>();
        btnLE.preferredWidth = 160;
        btnLE.preferredHeight = 50;
        btnLE.minHeight = 44;

        var btnImg = btnGO.AddComponent<Image>();
        btnImg.sprite = uiBarGreen != null ? uiBarGreen : roundedRect;
        btnImg.type = Image.Type.Sliced;
        btnImg.color = AccentGreen;
        btnImg.raycastTarget = true;

        var btnLabelTMP = AddChildTMP(btnGO.transform,
            H("\u05E9\u05EA\u05E4\u05D5"), // שתפו
            22, Color.white, TextAlignmentOptions.Center);
        btnLabelTMP.fontStyle = FontStyles.Bold;
        var blrt = btnLabelTMP.GetComponent<RectTransform>();
        blrt.anchorMin = Vector2.zero; blrt.anchorMax = Vector2.one;
        blrt.offsetMin = Vector2.zero; blrt.offsetMax = Vector2.zero;

        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(OnSharePressed);
    }

    // ── Recent activity (clear list) ──
    private void BuildRecentActivity(Transform parent)
    {
        var go = MakeStatsSection(parent,
            H("\u05E4\u05E2\u05D9\u05DC\u05D5\u05EA \u05D0\u05D7\u05E8\u05D5\u05E0\u05D5\u05EA")); // פעילויות אחרונות

        if (_data.games == null) return;

        var recent = new List<(string gameName, SessionSummary session)>();
        foreach (var g in _data.games)
            if (g.recentSessions != null)
                foreach (var s in g.recentSessions)
                    recent.Add((g.gameName, s));
        recent.Sort((a, b) => b.session.timestamp.CompareTo(a.session.timestamp));

        int count = Mathf.Min(recent.Count, 5);
        if (count == 0)
        {
            AddChildTMP(go.transform, H("\u05D0\u05D9\u05DF \u05E4\u05E2\u05D9\u05DC\u05D5\u05EA"), 20, TextLight, TextAlignmentOptions.Center)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
            return;
        }

        for (int i = 0; i < count; i++)
        {
            var (gameName, session) = recent[i];
            string action = session.completed
                ? H("\u05D4\u05E9\u05DC\u05D9\u05DD") : H("\u05E9\u05D9\u05D7\u05E7"); // השלים / שיחק
            string timeAgo = FormatTimeAgo(session.timestamp);

            var row = MakeHRow(go.transform, 32, TextAnchor.MiddleRight);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            row.GetComponent<HorizontalLayoutGroup>().spacing = 8;

            // Game name
            var nameTMP = AddChildTMP(row.transform, H(gameName), 19, TextDark, TextAlignmentOptions.Right);
            nameTMP.fontStyle = FontStyles.Bold;
            nameTMP.enableAutoSizing = true; nameTMP.fontSizeMin = 15; nameTMP.fontSizeMax = 19;
            nameTMP.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Action
            var actTMP = AddChildTMP(row.transform, action, 17,
                session.completed ? AccentGreen : TextMedium, TextAlignmentOptions.Center);
            actTMP.gameObject.AddComponent<LayoutElement>().preferredWidth = 70;

            // Time ago
            var timeTMP = AddChildTMP(row.transform, H(timeAgo), 16, TextLight, TextAlignmentOptions.Left);
            timeTMP.gameObject.AddComponent<LayoutElement>().preferredWidth = 80;
        }
    }

    // ── Weekly total summary ──
    private void BuildWeeklySummary(Transform parent)
    {
        // Count this week's sessions and time
        int weekSessions = 0;
        float weekSeconds = 0f;
        var now = System.DateTime.Now;
        int todayDow = (int)now.DayOfWeek;
        // Start of week (Sunday)
        var weekStart = now.Date.AddDays(-todayDow);
        long weekStartTs = new System.DateTimeOffset(weekStart).ToUnixTimeSeconds();

        if (_data.games != null)
            foreach (var g in _data.games)
                if (g.recentSessions != null)
                    foreach (var s in g.recentSessions)
                        if (s.timestamp >= weekStartTs)
                        {
                            weekSessions++;
                            weekSeconds += s.durationSeconds;
                        }

        var go = MakeStatsSection(parent,
            H("\u05E1\u05D9\u05DB\u05D5\u05DD \u05E9\u05D1\u05D5\u05E2\u05D9")); // סיכום שבועי

        var row = MakeHRow(go.transform, 80, TextAnchor.MiddleCenter);
        var rHL = row.GetComponent<HorizontalLayoutGroup>();
        rHL.spacing = 14;
        rHL.childForceExpandWidth = true;

        // Games this week
        MakeStatsSummaryBlock(row.transform, $"{weekSessions}",
            H("\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05D4\u05E9\u05D1\u05D5\u05E2")); // משחקים השבוע

        // Time this week
        string weekTime = weekSeconds < 60 ? H("\u05E4\u05D7\u05D5\u05EA \u05DE\u05D3\u05E7\u05D4")
            : weekSeconds < 3600 ? $"{(int)(weekSeconds / 60)} " + H("\u05D3\u05E7\u05D5\u05EA")
            : $"{weekSeconds / 3600f:F1} " + H("\u05E9\u05E2\u05D5\u05EA");
        MakeStatsSummaryBlock(row.transform, weekTime,
            H("\u05D6\u05DE\u05DF \u05DE\u05E9\u05D7\u05E7 \u05D4\u05E9\u05D1\u05D5\u05E2")); // זמן משחק השבוע

        // Average per day
        int daysElapsed = Mathf.Max(todayDow + 1, 1);
        float avgPerDay = (float)weekSessions / daysElapsed;
        MakeStatsSummaryBlock(row.transform, $"{avgPerDay:F1}",
            H("\u05DE\u05DE\u05D5\u05E6\u05E2 \u05DC\u05D9\u05D5\u05DD")); // ממוצע ליום
    }

    /// <summary>Formats a timestamp as relative time (היום, אתמול, לפני X ימים).</summary>
    private static string FormatTimeAgo(long timestamp)
    {
        if (timestamp <= 0) return "---";
        var dt = System.DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;
        var diff = System.DateTime.Now - dt;
        if (diff.TotalHours < 24) return "\u05D4\u05D9\u05D5\u05DD"; // היום
        if (diff.TotalHours < 48) return "\u05D0\u05EA\u05DE\u05D5\u05DC"; // אתמול
        if (diff.TotalDays < 7) return $"\u05DC\u05E4\u05E0\u05D9 {(int)diff.TotalDays} \u05D9\u05DE\u05D9\u05DD"; // לפני X ימים
        return $"\u05DC\u05E4\u05E0\u05D9 {(int)(diff.TotalDays / 7)} \u05E9\u05D1\u05D5\u05E2\u05D5\u05EA"; // לפני X שבועות
    }

    // ── Pie chart colors (warm, kid-friendly palette) ──
    private static readonly Color[] PieColors = {
        HexColor("#3498DB"), HexColor("#E74C3C"), HexColor("#2ECC71"), HexColor("#F39C12"),
        HexColor("#9B59B6"), HexColor("#1ABC9C"), HexColor("#E67E22"), HexColor("#34495E"),
        HexColor("#E91E63"), HexColor("#00BCD4"), HexColor("#8BC34A"), HexColor("#FF5722"),
        HexColor("#607D8B"), HexColor("#795548"), HexColor("#CDDC39"), HexColor("#FF9800"),
    };

    private void BuildPlayDistributionChart(Transform parent)
    {
        if (_data == null || _data.games == null) return;

        // Collect games with sessions > 0, sorted by sessions descending
        var played = new List<GameDashboardData>();
        int totalSessions = 0;
        foreach (var g in _data.games)
        {
            if (g.sessionsPlayed > 0)
            {
                played.Add(g);
                totalSessions += g.sessionsPlayed;
            }
        }
        if (played.Count == 0 || totalSessions == 0) return;

        played.Sort((a, b) => b.sessionsPlayed.CompareTo(a.sessionsPlayed));

        var card = MakeCard(parent);
        MakeSectionTitle(card, "\u05D7\u05DC\u05D5\u05E7\u05EA \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD"); // חלוקת משחקים

        var row = MakeHRow(card, 0, TextAnchor.MiddleCenter);
        row.GetComponent<HorizontalLayoutGroup>().spacing = 31;
        row.AddComponent<LayoutElement>().preferredHeight = 240;

        // ── Pie chart (left side) ──
        var pieContainer = new GameObject("PieContainer");
        pieContainer.transform.SetParent(row.transform, false);
        var pieContRT = pieContainer.GetComponent<RectTransform>();
        if (pieContRT == null) pieContRT = pieContainer.AddComponent<RectTransform>();
        pieContRT.sizeDelta = new Vector2(220, 220);
        var pieContLE = pieContainer.AddComponent<LayoutElement>();
        pieContLE.preferredWidth = 220;
        pieContLE.preferredHeight = 220;

        // Build slices as filled circle images (back to front, largest first)
        // Each slice fills from 12 o'clock clockwise with cumulative fillAmount
        float cumulativeFill = 0f;
        // Draw in reverse order so the first (largest) slice is on top visually
        for (int i = played.Count - 1; i >= 0; i--)
        {
            // Calculate this slice's cumulative end
            float sliceEnd = 0f;
            for (int j = 0; j <= i; j++)
                sliceEnd += (float)played[j].sessionsPlayed / totalSessions;

            var sliceGO = new GameObject($"Slice_{i}");
            sliceGO.transform.SetParent(pieContainer.transform, false);
            var sliceRT = sliceGO.AddComponent<RectTransform>();
            sliceRT.anchorMin = Vector2.zero;
            sliceRT.anchorMax = Vector2.one;
            sliceRT.offsetMin = Vector2.zero;
            sliceRT.offsetMax = Vector2.zero;

            var sliceImg = sliceGO.AddComponent<Image>();
            sliceImg.sprite = circleSprite;
            sliceImg.type = Image.Type.Filled;
            sliceImg.fillMethod = Image.FillMethod.Radial360;
            sliceImg.fillOrigin = (int)Image.Origin360.Top;
            sliceImg.fillClockwise = true;
            sliceImg.fillAmount = sliceEnd;
            sliceImg.color = PieColors[i % PieColors.Length];
            sliceImg.raycastTarget = false;
        }

        // ── Legend (right side) ──
        var legend = MakeVCol(row.transform);
        legend.AddComponent<LayoutElement>().flexibleWidth = 1;
        legend.GetComponent<VerticalLayoutGroup>().spacing = 8;
        legend.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperRight;

        int maxLegendItems = played.Count < 8 ? played.Count : 8;
        for (int i = 0; i < maxLegendItems; i++)
        {
            var g = played[i];
            float pct = (float)g.sessionsPlayed / totalSessions * 100f;
            Color sliceColor = PieColors[i % PieColors.Length];

            var legendRow = MakeHRow(legend.transform, 26, TextAnchor.MiddleRight);
            legendRow.GetComponent<HorizontalLayoutGroup>().spacing = 8;

            // Percentage + count
            var pctTMP = AddChildTMP(legendRow.transform, $"({g.sessionsPlayed}) %{pct:F0}",
                20, TextMedium, TextAlignmentOptions.Left);
            pctTMP.gameObject.AddComponent<LayoutElement>().preferredWidth = 100;

            // Game name
            var nameTMP = AddChildTMP(legendRow.transform, H(g.gameName),
                20, TextDark, TextAlignmentOptions.Right);
            nameTMP.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Color dot
            var dotGO = new GameObject("Dot");
            dotGO.transform.SetParent(legendRow.transform, false);
            var dotImg = dotGO.AddComponent<Image>();
            dotImg.sprite = circleSprite;
            dotImg.color = sliceColor;
            dotImg.raycastTarget = false;
            var dotLE = dotGO.AddComponent<LayoutElement>();
            dotLE.preferredWidth = 16;
            dotLE.preferredHeight = 22;
        }

        // Show "others" if more than 8
        if (played.Count > 8)
        {
            int otherSessions = 0;
            for (int i = 8; i < played.Count; i++)
                otherSessions += played[i].sessionsPlayed;
            float otherPct = (float)otherSessions / totalSessions * 100f;

            var otherRow = MakeHRow(legend.transform, 26, TextAnchor.MiddleRight);
            otherRow.GetComponent<HorizontalLayoutGroup>().spacing = 8;
            AddChildTMP(otherRow.transform, $"%{otherPct:F0}",
                20, TextMedium, TextAlignmentOptions.Left)
                .gameObject.AddComponent<LayoutElement>().preferredWidth = 52;
            AddChildTMP(otherRow.transform, H("\u05D0\u05D7\u05E8\u05D9\u05DD"), // אחרים
                20, TextLight, TextAlignmentOptions.Right)
                .gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var dotGO2 = new GameObject("Dot");
            dotGO2.transform.SetParent(otherRow.transform, false);
            var dotImg2 = dotGO2.AddComponent<Image>();
            dotImg2.sprite = circleSprite;
            dotImg2.color = HexColor("#BDBDBD");
            dotImg2.raycastTarget = false;
            var dotLE2 = dotGO2.AddComponent<LayoutElement>();
            dotLE2.preferredWidth = 16;
            dotLE2.preferredHeight = 22;
        }

        FitCard(card);
    }

    private void BuildPlayTimeDistributionChart(Transform parent)
    {
        if (_data == null || _data.games == null) return;

        var played = new List<GameDashboardData>();
        float totalTime = 0f;
        foreach (var g in _data.games)
        {
            if (g.totalPlayTime > 0f)
            {
                played.Add(g);
                totalTime += g.totalPlayTime;
            }
        }
        if (played.Count == 0 || totalTime <= 0f) return;

        played.Sort((a, b) => b.totalPlayTime.CompareTo(a.totalPlayTime));

        var card = MakeCard(parent);
        MakeSectionTitle(card, "\u05D6\u05DE\u05DF \u05DE\u05E9\u05D7\u05E7 \u05DC\u05E4\u05D9 \u05DE\u05E9\u05D7\u05E7"); // זמן משחק לפי משחק

        var row = MakeHRow(card, 0, TextAnchor.MiddleCenter);
        row.GetComponent<HorizontalLayoutGroup>().spacing = 31;
        row.AddComponent<LayoutElement>().preferredHeight = 240;

        // Pie chart
        var pieContainer = new GameObject("TimePieContainer");
        pieContainer.transform.SetParent(row.transform, false);
        var pieContRT = pieContainer.GetComponent<RectTransform>();
        if (pieContRT == null) pieContRT = pieContainer.AddComponent<RectTransform>();
        pieContRT.sizeDelta = new Vector2(220, 220);
        var pieContLE = pieContainer.AddComponent<LayoutElement>();
        pieContLE.preferredWidth = 220;
        pieContLE.preferredHeight = 220;

        int maxSlices = played.Count < 8 ? played.Count : 8;
        float otherTime = 0f;
        for (int i = 8; i < played.Count; i++)
            otherTime += played[i].totalPlayTime;
        float chartTotal = totalTime; // includes all

        // Draw slices (reverse order so largest on top)
        int sliceCount = maxSlices + (otherTime > 0f ? 1 : 0);
        for (int i = sliceCount - 1; i >= 0; i--)
        {
            float sliceEnd = 0f;
            for (int j = 0; j <= i; j++)
            {
                if (j < maxSlices)
                    sliceEnd += played[j].totalPlayTime / chartTotal;
                else
                    sliceEnd += otherTime / chartTotal;
            }

            var sliceGO = new GameObject($"TimeSlice_{i}");
            sliceGO.transform.SetParent(pieContainer.transform, false);
            var sliceRT = sliceGO.AddComponent<RectTransform>();
            sliceRT.anchorMin = Vector2.zero;
            sliceRT.anchorMax = Vector2.one;
            sliceRT.offsetMin = Vector2.zero;
            sliceRT.offsetMax = Vector2.zero;

            var sliceImg = sliceGO.AddComponent<Image>();
            sliceImg.sprite = circleSprite;
            sliceImg.type = Image.Type.Filled;
            sliceImg.fillMethod = Image.FillMethod.Radial360;
            sliceImg.fillOrigin = (int)Image.Origin360.Top;
            sliceImg.fillClockwise = true;
            sliceImg.fillAmount = sliceEnd;
            sliceImg.color = (i < maxSlices) ? PieColors[i % PieColors.Length] : HexColor("#BDBDBD");
            sliceImg.raycastTarget = false;
        }

        // Legend
        var legend = MakeVCol(row.transform);
        legend.AddComponent<LayoutElement>().flexibleWidth = 1;
        legend.GetComponent<VerticalLayoutGroup>().spacing = 8;
        legend.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperRight;

        for (int i = 0; i < maxSlices; i++)
        {
            var g = played[i];
            float pct = g.totalPlayTime / totalTime * 100f;
            string timeStr = ParentDashboardViewModel.FormatPlayTime(g.totalPlayTime);

            var legendRow = MakeHRow(legend.transform, 26, TextAnchor.MiddleRight);
            legendRow.GetComponent<HorizontalLayoutGroup>().spacing = 8;

            var timeTMP = AddChildTMP(legendRow.transform, timeStr,
                18, TextMedium, TextAlignmentOptions.Left);
            timeTMP.gameObject.AddComponent<LayoutElement>().preferredWidth = 120;

            var nameTMP = AddChildTMP(legendRow.transform, H(g.gameName),
                20, TextDark, TextAlignmentOptions.Right);
            nameTMP.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var dotGO = new GameObject("Dot");
            dotGO.transform.SetParent(legendRow.transform, false);
            var dotImg = dotGO.AddComponent<Image>();
            dotImg.sprite = circleSprite;
            dotImg.color = PieColors[i % PieColors.Length];
            dotImg.raycastTarget = false;
            var dotLE = dotGO.AddComponent<LayoutElement>();
            dotLE.preferredWidth = 16;
            dotLE.preferredHeight = 22;
        }

        if (otherTime > 0f)
        {
            string otherTimeStr = ParentDashboardViewModel.FormatPlayTime(otherTime);
            var otherRow = MakeHRow(legend.transform, 26, TextAnchor.MiddleRight);
            otherRow.GetComponent<HorizontalLayoutGroup>().spacing = 8;
            AddChildTMP(otherRow.transform, otherTimeStr,
                18, TextMedium, TextAlignmentOptions.Left)
                .gameObject.AddComponent<LayoutElement>().preferredWidth = 120;
            AddChildTMP(otherRow.transform, H("\u05D0\u05D7\u05E8\u05D9\u05DD"), // אחרים
                20, TextLight, TextAlignmentOptions.Right)
                .gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var dotGO2 = new GameObject("Dot");
            dotGO2.transform.SetParent(otherRow.transform, false);
            var dotImg2 = dotGO2.AddComponent<Image>();
            dotImg2.sprite = circleSprite;
            dotImg2.color = HexColor("#BDBDBD");
            dotImg2.raycastTarget = false;
            var dotLE2 = dotGO2.AddComponent<LayoutElement>();
            dotLE2.preferredWidth = 16;
            dotLE2.preferredHeight = 22;
        }

        FitCard(card);
    }

    private static readonly Color NeedDataColor = HexColor("#9E9E9E");
    private static readonly string NeedDataText = "\u05E6\u05E8\u05D9\u05DA \u05DC\u05E9\u05D7\u05E7 \u05E2\u05D5\u05D3 \u05DB\u05D3\u05D9 \u05DC\u05E8\u05D0\u05D5\u05EA \u05E0\u05EA\u05D5\u05E0\u05D9\u05DD";
    // צריך לשחק עוד כדי לראות נתונים

    private void BuildStorySections(Transform parent, DashboardStoryBuilder.StoryData story)
    {
        // ── 0. Child Intro (personal) ──
        if (!string.IsNullOrEmpty(story.childIntro))
        {
            var introCard = MakeInlineCard(parent, HexColor("#E8EAF6")); // soft indigo
            introCard.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(30, 30, 20, 20);
            var introTMP = AddChildTMP(introCard.transform, H(story.childIntro), 28, TextDark, TextAlignmentOptions.Right);
            introTMP.fontStyle = FontStyles.Bold;
            introTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 54;
            FitCard(introCard.transform);
        }

        // ── 1. Weekly Summary (Hero) ──
        {
            var heroCard = MakeCard(parent);
            var heroTMP = AddChildTMP(heroCard, H(story.weeklySummary), 26, TextDark, TextAlignmentOptions.Right);
            heroTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 95;
            heroTMP.lineSpacing = 8;
            FitCard(heroCard);
        }

        // ── Section 2: Focus Right Now ──
        if (!string.IsNullOrEmpty(story.focusNow))
        {
            var focusCard = MakeInlineCard(parent, HexColor("#FFF3E0")); // warm orange bg
            var focusLayout = focusCard.GetComponent<VerticalLayoutGroup>();
            focusLayout.padding = new RectOffset(30, 30, 20, 20);

            var focusTMP = AddChildTMP(focusCard.transform, H(story.focusNow), 24, HexColor("#E65100"), TextAlignmentOptions.Right);
            focusTMP.fontStyle = FontStyles.Bold;
            focusTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 49;
            FitCard(focusCard.transform);
        }

        // ── Section 3: Grouped Insights ──
        if (story.strengthInsights.Count > 0)
            BuildInsightGroup(parent, "\u05D7\u05D6\u05E7\u05D5\u05EA", story.strengthInsights, StrengthBg, AccentGreen);
        else
            ShowEmptySection(parent, "\u05D7\u05D6\u05E7\u05D5\u05EA", NeedDataText);

        if (story.practiceInsights.Count > 0)
            BuildInsightGroup(parent, "\u05E6\u05E8\u05D9\u05DA \u05EA\u05E8\u05D2\u05D5\u05DC", story.practiceInsights, PracticeBg, AccentOrange);

        if (story.behaviorInsights.Count > 0)
            BuildInsightGroup(parent, "\u05D3\u05E4\u05D5\u05E1\u05D9 \u05DE\u05E9\u05D7\u05E7", story.behaviorInsights, InsightBg, Primary);

        // ── Section 4: Confusion ──
        if (story.confusionPairs.Count > 0)
        {
            var confCard = MakeCard(parent);
            MakeSectionTitle(confCard, "\u05DE\u05EA\u05D1\u05DC\u05D1\u05DC \u05DC\u05E2\u05D9\u05EA\u05D9\u05DD \u05D1\u05D9\u05DF");
            foreach (var pair in story.confusionPairs)
                AddChildTMP(confCard, $"\u2022 {pair}", 22, TextDark, TextAlignmentOptions.Right)
                    .gameObject.AddComponent<LayoutElement>().preferredHeight = 41;
            FitCard(confCard);
        }

        // ── Letters ──
        if (string.IsNullOrEmpty(story.strongLetters) && string.IsNullOrEmpty(story.weakLetters))
            ShowEmptySection(parent, "\u05D0\u05D5\u05EA\u05D9\u05D5\u05EA",
                "\u05E6\u05E8\u05D9\u05DA \u05DC\u05E9\u05D7\u05E7 \u05D1\u05DE\u05E9\u05D7\u05E7 \u05D4\u05D0\u05D5\u05EA \u05D4\u05D7\u05E1\u05E8\u05D4 \u05DB\u05D3\u05D9 \u05DC\u05E8\u05D0\u05D5\u05EA \u05E0\u05EA\u05D5\u05E0\u05D9\u05DD");
            // צריך לשחק במשחק האות החסרה כדי לראות נתונים
        else if (!string.IsNullOrEmpty(story.strongLetters) || !string.IsNullOrEmpty(story.weakLetters))
        {
            var letterCard = MakeCard(parent);
            MakeSectionTitle(letterCard, "\u05D0\u05D5\u05EA\u05D9\u05D5\u05EA");
            if (!string.IsNullOrEmpty(story.strongLetters))
                AddChildTMP(letterCard, $"\u05E9\u05D5\u05DC\u05D8 \u05D1: {story.strongLetters}", 22, AccentGreen, TextAlignmentOptions.Right)
                    .gameObject.AddComponent<LayoutElement>().preferredHeight = 41;
            if (!string.IsNullOrEmpty(story.weakLetters))
                AddChildTMP(letterCard, $"\u05E6\u05E8\u05D9\u05DA \u05EA\u05E8\u05D2\u05D5\u05DC: {story.weakLetters}", 22, AccentOrange, TextAlignmentOptions.Right)
                    .gameObject.AddComponent<LayoutElement>().preferredHeight = 41;
            FitCard(letterCard);
        }

        if (story.levelUpGames.Count > 0 || story.easierLevelGames.Count > 0)
        {
            var diffCard = MakeCard(parent);
            MakeSectionTitle(diffCard, "\u05D4\u05DE\u05DC\u05E6\u05EA \u05E8\u05DE\u05EA \u05E7\u05D5\u05E9\u05D9");
            if (story.levelUpGames.Count > 0)
            {
                var upTMP = AddChildTMP(diffCard, "\u05DE\u05D5\u05DB\u05DF \u05DC\u05D4\u05E2\u05DC\u05D5\u05EA \u05E8\u05DE\u05D4:", 20, AccentGreen, TextAlignmentOptions.Right);
                upTMP.fontStyle = FontStyles.Bold;
                upTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 38;
                AddChildTMP(diffCard, H(string.Join(", ", story.levelUpGames)), 22, TextDark, TextAlignmentOptions.Right)
                    .gameObject.AddComponent<LayoutElement>().preferredHeight = 38;
            }
            if (story.easierLevelGames.Count > 0)
            {
                var dnTMP = AddChildTMP(diffCard, "\u05E6\u05E8\u05D9\u05DA \u05E8\u05DE\u05D4 \u05E7\u05DC\u05D4 \u05D9\u05D5\u05EA\u05E8:", 20, AccentOrange, TextAlignmentOptions.Right);
                dnTMP.fontStyle = FontStyles.Bold;
                dnTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 38;
                AddChildTMP(diffCard, H(string.Join(", ", story.easierLevelGames)), 22, TextDark, TextAlignmentOptions.Right)
                    .gameObject.AddComponent<LayoutElement>().preferredHeight = 38;
            }
            FitCard(diffCard);
        }

        // Compact metrics block
        {
            var metricsCard = MakeCard(parent);
            bool hasMetrics = false;
            if (!string.IsNullOrEmpty(story.improvementMetric1)) { AddBulletLine(metricsCard, story.improvementMetric1, AccentGreen); hasMetrics = true; }
            if (!string.IsNullOrEmpty(story.improvementMetric2)) { AddBulletLine(metricsCard, story.improvementMetric2, AccentGreen); hasMetrics = true; }
            if (!string.IsNullOrEmpty(story.learningSpeed)) { AddBulletLine(metricsCard, story.learningSpeed, Primary); hasMetrics = true; }
            foreach (var m in story.masteryLines) { AddBulletLine(metricsCard, m, Primary); hasMetrics = true; }
            if (!string.IsNullOrEmpty(story.coloringPrecision)) { AddBulletLine(metricsCard, story.coloringPrecision, TextMedium); hasMetrics = true; }
            if (hasMetrics) FitCard(metricsCard);
            else Object.DestroyImmediate(metricsCard.gameObject);
        }

        // ── Section 5: Strengths vs Practice Areas ──
        if (story.strengths.Count > 0 || story.practiceAreas.Count > 0)
        {
            var dualRow = MakeHRow(parent, 0, TextAnchor.UpperCenter);
            dualRow.GetComponent<HorizontalLayoutGroup>().spacing = 16;
            dualRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            dualRow.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;
            dualRow.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (story.strengths.Count > 0)
            {
                var sCard = MakeInlineCard(dualRow.transform, StrengthBg);
                sCard.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(25, 25, 20, 20);
                sCard.GetComponent<VerticalLayoutGroup>().spacing = 8;
                var sTitle = AddChildTMP(sCard.transform, H("\u05D7\u05D6\u05E7\u05D5\u05EA"), 28, AccentGreen, TextAlignmentOptions.Right);
                sTitle.fontStyle = FontStyles.Bold;
                sTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 49;
                foreach (var s in story.strengths)
                {
                    var sTMP = AddChildTMP(sCard.transform, H(s), 22, TextDark, TextAlignmentOptions.Right);
                    sTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 41;
                }
            }

            if (story.practiceAreas.Count > 0)
            {
                var pCard = MakeInlineCard(dualRow.transform, PracticeBg);
                pCard.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(25, 25, 20, 20);
                pCard.GetComponent<VerticalLayoutGroup>().spacing = 8;
                var pTitle = AddChildTMP(pCard.transform, H("\u05E6\u05E8\u05D9\u05DA \u05EA\u05E8\u05D2\u05D5\u05DC"), 28, AccentOrange, TextAlignmentOptions.Right);
                pTitle.fontStyle = FontStyles.Bold;
                pTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 49;
                foreach (var p in story.practiceAreas)
                {
                    var pTMP = AddChildTMP(pCard.transform, H(p), 22, TextDark, TextAlignmentOptions.Right);
                    pTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 41;
                }
            }
        }

        // ── Development Overview (text bars) ──
        if (story.categoryBars == null || story.categoryBars.Count == 0)
            ShowEmptySection(parent, "\u05E1\u05E7\u05D9\u05E8\u05EA \u05D4\u05EA\u05E4\u05EA\u05D7\u05D5\u05EA", NeedDataText);
        else if (story.categoryBars.Count > 0)
        {
            var barCard = MakeCard(parent);
            MakeSectionTitle(barCard, "\u05E1\u05E7\u05D9\u05E8\u05EA \u05D4\u05EA\u05E4\u05EA\u05D7\u05D5\u05EA"); // סקירת התפתחות
            foreach (var (catName, score) in story.categoryBars)
            {
                var row = MakeHRow(barCard, 28, TextAnchor.MiddleRight);
                row.GetComponent<HorizontalLayoutGroup>().spacing = 10;
                // Score number
                var scoreTMP = AddChildTMP(row.transform, $"{score}", 20, Primary, TextAlignmentOptions.Center);
                scoreTMP.fontStyle = FontStyles.Bold;
                scoreTMP.gameObject.AddComponent<LayoutElement>().preferredWidth = 40;
                // Bar
                MakeProgressBar(row.transform, score / 100f, ParentDashboardViewModel.ScoreColor(score), 14f);
                row.transform.GetChild(row.transform.childCount - 1).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                // Name
                AddChildTMP(row.transform, H(catName), 20, TextDark, TextAlignmentOptions.Right)
                    .gameObject.AddComponent<LayoutElement>().preferredWidth = 200;
            }
            FitCard(barCard);
        }

        // ── What Improved ──
        if (story.improvements == null || story.improvements.Count == 0)
            ShowEmptySection(parent, "\u05DE\u05D4 \u05D4\u05E9\u05EA\u05E4\u05E8",
                "\u05E6\u05E8\u05D9\u05DA \u05DC\u05E9\u05D7\u05E7 \u05E2\u05D5\u05D3 \u05DB\u05D3\u05D9 \u05DC\u05D6\u05D4\u05D5\u05EA \u05E9\u05D9\u05E4\u05D5\u05E8\u05D9\u05DD");
            // צריך לשחק עוד כדי לזהות שיפורים
        else if (story.improvements.Count > 0)
        {
            var impCard = MakeInlineCard(parent, HexColor("#E8F5E9"));
            impCard.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(25, 25, 18, 18);
            impCard.GetComponent<VerticalLayoutGroup>().spacing = 5;
            var impTitle = AddChildTMP(impCard.transform, H("\u05DE\u05D4 \u05D4\u05E9\u05EA\u05E4\u05E8"), 24, AccentGreen, TextAlignmentOptions.Right);
            // מה השתפר
            impTitle.fontStyle = FontStyles.Bold;
            impTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 43;
            foreach (var imp in story.improvements)
                AddChildTMP(impCard.transform, $"\u2022 {H(imp)}", 20, TextDark, TextAlignmentOptions.Right)
                    .gameObject.AddComponent<LayoutElement>().preferredHeight = 35;
            FitCard(impCard.transform);
        }

        // ── Recommended Games ──
        if (story.recommendedGames == null || story.recommendedGames.Count == 0)
            ShowEmptySection(parent, "\u05DE\u05D4 \u05DC\u05E9\u05D7\u05E7 \u05D4\u05DC\u05D0\u05D4",
                "\u05E9\u05D7\u05E7\u05D5 \u05E2\u05D5\u05D3 \u05DB\u05D3\u05D9 \u05E9\u05E0\u05D5\u05DB\u05DC \u05DC\u05D4\u05DE\u05DC\u05D9\u05E5");
            // שחקו עוד כדי שנוכל להמליץ
        else if (story.recommendedGames.Count > 0)
        {
            var recCard = MakeCard(parent);
            MakeSectionTitle(recCard, "\u05DE\u05D4 \u05DC\u05E9\u05D7\u05E7 \u05D4\u05DC\u05D0\u05D4"); // מה לשחק הלאה
            foreach (var g in story.recommendedGames)
                AddChildTMP(recCard, $"\u2022 {H(g)}", 22, TextDark, TextAlignmentOptions.Right)
                    .gameObject.AddComponent<LayoutElement>().preferredHeight = 38;
            FitCard(recCard);
        }

        // ── Progress Snapshot ──
        if (!string.IsNullOrEmpty(story.accuracyTrend) || !string.IsNullOrEmpty(story.lastScores))
        {
            var snapCard = MakeCard(parent);
            MakeSectionTitle(snapCard, "\u05DE\u05D2\u05DE\u05EA \u05D4\u05EA\u05E7\u05D3\u05DE\u05D5\u05EA"); // מגמת התקדמות
            if (!string.IsNullOrEmpty(story.accuracyTrend))
                AddChildTMP(snapCard, $"\u05D3\u05D9\u05D5\u05E7: {story.accuracyTrend}", 22, Primary, TextAlignmentOptions.Right)
                    .gameObject.AddComponent<LayoutElement>().preferredHeight = 38;
            if (!string.IsNullOrEmpty(story.lastScores))
                AddChildTMP(snapCard, $"\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05D0\u05D7\u05E8\u05D5\u05E0\u05D9\u05DD: {story.lastScores}", 20, TextMedium, TextAlignmentOptions.Right)
                    .gameObject.AddComponent<LayoutElement>().preferredHeight = 35;
            FitCard(snapCard);
        }

        // ── Section 6: Progress Highlight ──
        if (!string.IsNullOrEmpty(story.progressHighlight))
        {
            var progressCard = MakeInlineCard(parent, HexColor("#E8F5E9"));
            progressCard.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(30, 30, 18, 18);
            var progTMP = AddChildTMP(progressCard.transform, H(story.progressHighlight), 22, AccentGreen, TextAlignmentOptions.Right);
            progTMP.fontStyle = FontStyles.Bold;
            progTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 43;
            FitCard(progressCard.transform);
        }

        // ── Section 7: Suggested Next Step ──
        if (!string.IsNullOrEmpty(story.suggestedNextStep))
        {
            var nextCard = MakeInlineCard(parent, HexColor("#E3F2FD"));
            nextCard.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(30, 30, 18, 18);
            var nextTitle = AddChildTMP(nextCard.transform, H("\u05D4\u05E6\u05E2\u05D3 \u05D4\u05D1\u05D0"), 20, TextMedium, TextAlignmentOptions.Right);
            // הצעד הבא
            nextTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 38;
            var nextTMP = AddChildTMP(nextCard.transform, H(story.suggestedNextStep), 24, Primary, TextAlignmentOptions.Right);
            nextTMP.fontStyle = FontStyles.Bold;
            nextTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 46;
            FitCard(nextCard.transform);
        }

        MakeSpacer(parent, 20f);

        // ── Share App Card ──
        BuildShareCard(parent);

        MakeSpacer(parent, 40f);
    }

    // ═══════════════════════════════════════════════════════════════
    //  SHARE
    // ═══════════════════════════════════════════════════════════════

    private void BuildShareCard(Transform parent)
    {
        var card = MakeCard(parent);
        var cardLayout = card.GetComponent<VerticalLayoutGroup>();
        cardLayout.padding = new RectOffset(35, 35, 30, 30);
        cardLayout.spacing = 16;
        cardLayout.childAlignment = TextAnchor.MiddleCenter;

        // Card background — soft gradient feel
        card.GetComponent<Image>().color = HexColor("#EBF5FB");

        // Title
        var titleTMP = AddChildTMP(card, H("\u05D0\u05D4\u05D1\u05EA\u05DD \u05D0\u05EA \u05D4\u05D0\u05E4\u05DC\u05D9\u05E7\u05E6\u05D9\u05D4? \u05E1\u05E4\u05E8\u05D5 \u05DC\u05D7\u05D1\u05E8\u05D9\u05DD!"),
            // אהבתם את האפליקציה? ספרו לחברים!
            24, Primary, TextAlignmentOptions.Center);
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 46;

        // Subtitle with child stats
        string statsLine = BuildShareStatsLine();
        var subtitleTMP = AddChildTMP(card, H(statsLine),
            20, TextMedium, TextAlignmentOptions.Center);
        subtitleTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 38;

        // Share button
        var btnGO = new GameObject("ShareButton");
        btnGO.transform.SetParent(card, false);
        var btnLE = btnGO.AddComponent<LayoutElement>();
        btnLE.preferredHeight = 76;
        btnLE.preferredWidth = 280;

        var btnImg = btnGO.AddComponent<Image>();
        if (roundedRect != null) { btnImg.sprite = roundedRect; btnImg.type = Image.Type.Sliced; }
        btnImg.color = Primary;

        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        var colors = btn.colors;
        colors.highlightedColor = HexColor("#2980B9");
        colors.pressedColor = HexColor("#1F6DA0");
        btn.colors = colors;
        btn.onClick.AddListener(OnSharePressed);

        // Button label
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(btnGO.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(labelTMP, "\u05E9\u05EA\u05E4\u05D5 \u05E2\u05DD \u05D7\u05D1\u05E8\u05D9\u05DD"); // שתפו עם חברים
        labelTMP.fontSize = 34;
        labelTMP.color = Color.white;
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.enableWordWrapping = false;
        labelTMP.fontStyle = FontStyles.Bold;
        labelTMP.raycastTarget = false;
    }

    private string BuildShareStatsLine()
    {
        if (_data == null) return "";

        // מתן כבר שיחק ב-X משחקים וגילה Y חיות, Z צבעים ו-W מדבקות!
        string name = _data.profileName;
        int sessions = _data.totalSessions;
        int animals = _data.discoveredAnimals;
        int colors = _data.discoveredColors;
        int stickers = _data.collectedStickers;

        return $"{name} \u05DB\u05D1\u05E8 \u05E9\u05D9\u05D7\u05E7 \u05D1-{sessions} \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05D5\u05D2\u05D9\u05DC\u05D4 {animals} \u05D7\u05D9\u05D5\u05EA, {colors} \u05E6\u05D1\u05E2\u05D9\u05DD \u05D5-{stickers} \u05DE\u05D3\u05D1\u05E7\u05D5\u05EA!";
    }

    private void OnSharePressed()
    {
        StartCoroutine(CertificateGenerator.GenerateAndShare(_data, roundedRect));
    }

    // ═══════════════════════════════════════════════════════════════
    //  GAMES TAB — 3-column grid (75%) + persistent settings panel (25%)
    // ═══════════════════════════════════════════════════════════════

    private GameObject _settingsPanelContent; // right panel content (updated on game select)
    private int _selectedGameIndex = -1;
    private readonly List<Image> _cardBackgrounds = new List<Image>();          // card bg images for selection tint
    private TextMeshProUGUI _gamesCounterTMP;  // "X משחקים פעילים מתוך Y" text
    private readonly List<Outline[]> _cardOutlines = new List<Outline[]>();     // outline components for selection border

    private void BuildGamesTabContent()
    {
        if (_data == null || tabContents.Length < 2) return;
        var parent = tabContents[1];

        if (_data.games.Count == 0)
        {
            AddChildTMP(parent, H("\u05E2\u05D5\u05D3 \u05D0\u05D9\u05DF \u05E0\u05EA\u05D5\u05E0\u05D9\u05DD"),
                24, TextMedium, TextAlignmentOptions.Center)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 80;
            return;
        }

        // ── Disable outer scroll/layout for Games tab (has internal scrolling) ──
        var parentCSF = parent.GetComponent<ContentSizeFitter>();
        if (parentCSF != null) parentCSF.enabled = false;
        var parentVLG = parent.GetComponent<VerticalLayoutGroup>();
        if (parentVLG != null) parentVLG.enabled = false;
        var outerScroll = parent.parent != null ? parent.parent.GetComponent<ScrollRect>() : null;
        if (outerScroll != null) outerScroll.enabled = false;
        // Stretch content to fill viewport instead of growing with children
        parent.anchorMin = Vector2.zero;
        parent.anchorMax = Vector2.one;
        parent.pivot = new Vector2(0.5f, 0.5f);
        parent.offsetMin = Vector2.zero;
        parent.offsetMax = Vector2.zero;

        // ── Main horizontal split: Grid (75%) | Settings Panel (25%) ──
        var splitRow = new GameObject("SplitLayout");
        splitRow.transform.SetParent(parent, false);
        var splitRT = splitRow.AddComponent<RectTransform>();
        splitRT.anchorMin = Vector2.zero; splitRT.anchorMax = Vector2.one;
        splitRT.offsetMin = new Vector2(8, 80); splitRT.offsetMax = new Vector2(-8, -8); // 80px bottom for ad banner
        var splitHL = splitRow.AddComponent<HorizontalLayoutGroup>();
        splitHL.spacing = 12;
        splitHL.childForceExpandHeight = true;
        splitHL.childControlWidth = true;
        splitHL.childControlHeight = true;
        splitHL.padding = new RectOffset(8, 8, 8, 8);

        // ── LEFT: Games grid (75%) ──
        var gridContainer = new GameObject("GridContainer");
        gridContainer.transform.SetParent(splitRow.transform, false);
        gridContainer.AddComponent<RectTransform>();
        var gridContainerLE = gridContainer.AddComponent<LayoutElement>();
        gridContainerLE.flexibleWidth = 3; // 75%

        // Scroll for grid
        var gridScrollGO = new GameObject("GridScroll");
        gridScrollGO.transform.SetParent(gridContainer.transform, false);
        var gsRT = gridScrollGO.AddComponent<RectTransform>();
        gsRT.anchorMin = Vector2.zero; gsRT.anchorMax = Vector2.one;
        gsRT.offsetMin = Vector2.zero; gsRT.offsetMax = Vector2.zero;
        gridScrollGO.AddComponent<Image>().color = Color.clear;
        gridScrollGO.GetComponent<Image>().raycastTarget = true;
        gridScrollGO.AddComponent<RectMask2D>();
        var gridScroll = gridScrollGO.AddComponent<ScrollRect>();
        gridScroll.horizontal = false; gridScroll.vertical = true;

        var gridContentGO = new GameObject("GridContent");
        gridContentGO.transform.SetParent(gridScrollGO.transform, false);
        var gcRT = gridContentGO.AddComponent<RectTransform>();
        gcRT.anchorMin = new Vector2(0, 1); gcRT.anchorMax = new Vector2(1, 1);
        gcRT.pivot = new Vector2(0.5f, 1);
        gcRT.sizeDelta = Vector2.zero;
        gridContentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        gridScroll.content = gcRT;

        // GridContent needs a VerticalLayoutGroup so children stretch to fill width
        var gcVL = gridContentGO.AddComponent<VerticalLayoutGroup>();
        gcVL.childForceExpandWidth = true;
        gcVL.childForceExpandHeight = false;
        gcVL.childControlWidth = true;
        gcVL.childControlHeight = true;

        // ── Active games counter ──
        {
            int activeCount = 0;
            int totalCount = _data.games.Count;
            foreach (var g in _data.games)
            {
                bool vis = g.recommendation != null ? g.recommendation.finalVisible : g.systemVisibility;
                if (vis) activeCount++;
            }
            _gamesCounterTMP = AddChildTMP(gridContentGO.transform,
                H($"{activeCount} \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05E4\u05E2\u05D9\u05DC\u05D9\u05DD \u05DE\u05EA\u05D5\u05DA {totalCount}"), // X משחקים פעילים מתוך Y
                22, TextMedium, TextAlignmentOptions.Center);
            _gamesCounterTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
        }

        var gridGO = new GameObject("GamesGrid");
        gridGO.transform.SetParent(gridContentGO.transform, false);
        var gridRT = gridGO.AddComponent<RectTransform>();
        // Stretch horizontally to fill parent
        gridRT.anchorMin = new Vector2(0, 1);
        gridRT.anchorMax = new Vector2(1, 1);
        gridRT.pivot = new Vector2(0.5f, 1);
        gridRT.offsetMin = Vector2.zero;
        gridRT.offsetMax = Vector2.zero;
        var grid = gridGO.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.cellSize = new Vector2(420, 350);
        grid.spacing = new Vector2(14, 14);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.padding = new RectOffset(6, 6, 6, 6);
        gridGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _cardBackgrounds.Clear();
        _cardOutlines.Clear();
        _selectedGameIndex = -1;

        for (int i = 0; i < _data.games.Count; i++)
        {
            MakeGameCard3Col(gridGO.transform, _data.games[i], i);
        }

        // ── RIGHT: Persistent settings panel (25%) — styled like a large card ──
        var settingsPanel = new GameObject("SettingsPanel");
        settingsPanel.transform.SetParent(splitRow.transform, false);
        settingsPanel.AddComponent<RectTransform>();
        var spLE = settingsPanel.AddComponent<LayoutElement>();
        spLE.flexibleWidth = 1; // 25%
        var spImg = settingsPanel.AddComponent<Image>();
        spImg.sprite = uiCardBlue != null ? uiCardBlue : roundedRect;
        spImg.type = Image.Type.Sliced;
        spImg.color = new Color(0.7f, 0.85f, 1f, 1f); // slightly muted blue card in dark mode
        spImg.raycastTarget = true;
        settingsPanel.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.3f);

        // Settings panel scroll
        var spScrollGO = new GameObject("SPScroll");
        spScrollGO.transform.SetParent(settingsPanel.transform, false);
        var spScrollRT = spScrollGO.AddComponent<RectTransform>();
        spScrollRT.anchorMin = Vector2.zero; spScrollRT.anchorMax = Vector2.one;
        spScrollRT.offsetMin = new Vector2(4, 4); spScrollRT.offsetMax = new Vector2(-4, -4);
        spScrollGO.AddComponent<Image>().color = Color.clear;
        spScrollGO.GetComponent<Image>().raycastTarget = true; // needed for scroll events
        spScrollGO.AddComponent<RectMask2D>();
        var spScroll = spScrollGO.AddComponent<ScrollRect>();
        spScroll.horizontal = false; spScroll.vertical = true;

        var spContentGO = new GameObject("SPContent");
        spContentGO.transform.SetParent(spScrollGO.transform, false);
        var spContentRT = spContentGO.AddComponent<RectTransform>();
        spContentRT.anchorMin = new Vector2(0, 1); spContentRT.anchorMax = new Vector2(1, 1);
        spContentRT.pivot = new Vector2(0.5f, 1);
        spContentRT.sizeDelta = Vector2.zero;
        var spContentVL = spContentGO.AddComponent<VerticalLayoutGroup>();
        spContentVL.spacing = 10;
        spContentVL.padding = new RectOffset(10, 10, 12, 16);
        spContentVL.childForceExpandWidth = true;
        spContentVL.childForceExpandHeight = false;
        spContentVL.childControlWidth = true;
        spContentVL.childControlHeight = true;
        spContentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        spScroll.content = spContentRT;

        _settingsPanelContent = spContentGO;

        // Default: show hint to select a game
        var hintTMP = AddChildTMP(spContentGO.transform,
            H("\u05DC\u05D7\u05E6\u05D5 \u05E2\u05DC \u05DE\u05E9\u05D7\u05E7 \u05DC\u05E0\u05D9\u05D4\u05D5\u05DC"), // לחצו על משחק לניהול
            22, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Center);
        hintTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 100;

        // Select first game by default if available
        if (_data.games.Count > 0)
            SelectGameCard(0, _data.games[0]);
    }

    /// <summary>Updates the persistent right settings panel with the selected game's controls.</summary>
    private void UpdateSettingsPanel(GameDashboardData game)
    {
        if (_settingsPanelContent == null) return;

        // Clear previous content
        for (int i = _settingsPanelContent.transform.childCount - 1; i >= 0; i--)
            Destroy(_settingsPanelContent.transform.GetChild(i).gameObject);

        var parent = _settingsPanelContent.transform;
        var rec = game.recommendation;
        string gameId = game.gameId;

        // ═══════════════════════════════════════════════════════════
        //  HEADER: Icon + Title + Status
        // ═══════════════════════════════════════════════════════════

        // Game icon (centered)
        var iconHolder = new GameObject("PanelIcon");
        iconHolder.transform.SetParent(parent, false);
        iconHolder.AddComponent<RectTransform>();
        iconHolder.AddComponent<LayoutElement>().preferredHeight = 80;
        var gameItem = FindGameItemFromDb(gameId);
        Sprite thumbSprite = (gameItem != null && gameItem.thumbnail != null)
            ? gameItem.thumbnail : uiPlaceholder;
        if (thumbSprite != null)
        {
            var thumbGO = new GameObject("Thumb");
            thumbGO.transform.SetParent(iconHolder.transform, false);
            var thumbRT = thumbGO.AddComponent<RectTransform>();
            thumbRT.anchorMin = new Vector2(0.5f, 0.5f);
            thumbRT.anchorMax = new Vector2(0.5f, 0.5f);
            thumbRT.sizeDelta = new Vector2(70, 70);
            var thumbImg = thumbGO.AddComponent<Image>();
            thumbImg.sprite = thumbSprite;
            thumbImg.preserveAspect = true;
            thumbImg.raycastTarget = false;
        }

        // Title (bold, white — auto-size to fit narrow panel)
        var titleTMP = AddChildTMP(parent, H(game.gameName), 28, Color.white, TextAlignmentOptions.Center);
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.enableAutoSizing = true; titleTMP.fontSizeMin = 20; titleTMP.fontSizeMax = 28;
        titleTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 38;

        // Status chip
        bool finalVisible = rec != null ? rec.finalVisible : game.systemVisibility;
        string statusText = finalVisible
            ? H("\u05E4\u05E2\u05D9\u05DC") : H("\u05DE\u05D5\u05E1\u05EA\u05E8"); // פעיל / מוסתר
        var statusChipGO = new GameObject("StatusChip");
        statusChipGO.transform.SetParent(parent, false);
        statusChipGO.AddComponent<RectTransform>();
        var statusChipLE = statusChipGO.AddComponent<LayoutElement>();
        statusChipLE.preferredHeight = 30;
        // Center the chip by using a horizontal layout
        var statusChipHL = statusChipGO.AddComponent<HorizontalLayoutGroup>();
        statusChipHL.childAlignment = TextAnchor.MiddleCenter;
        statusChipHL.childForceExpandWidth = false;
        statusChipHL.childControlWidth = true;
        statusChipHL.childControlHeight = true;

        var chipInnerGO = new GameObject("ChipInner");
        chipInnerGO.transform.SetParent(statusChipGO.transform, false);
        var chipInnerImg = chipInnerGO.AddComponent<Image>();
        chipInnerImg.sprite = roundedRect;
        chipInnerImg.type = Image.Type.Sliced;
        chipInnerImg.color = finalVisible ? new Color(1f, 1f, 1f, 0.25f) : new Color(1f, 0.3f, 0.3f, 0.25f);
        var chipInnerLE = chipInnerGO.AddComponent<LayoutElement>();
        chipInnerLE.preferredWidth = 110;
        chipInnerLE.preferredHeight = 34;
        var visChipTMP = AddChildTMP(chipInnerGO.transform, statusText,
            18, finalVisible ? Color.white : new Color(1f, 0.85f, 0.85f), TextAlignmentOptions.Center);
        visChipTMP.fontStyle = FontStyles.Bold;
        var vcrt = visChipTMP.GetComponent<RectTransform>();
        vcrt.anchorMin = Vector2.zero; vcrt.anchorMax = Vector2.one;
        vcrt.offsetMin = Vector2.zero; vcrt.offsetMax = Vector2.zero;
        var visChipImg = chipInnerImg; // for wiring

        // ═══════════════════════════════════════════════════════════
        //  SCORE BADGE (Hero)
        // ═══════════════════════════════════════════════════════════
        if (game.sessionsPlayed > 0)
        {
            var scoreSectionGO = new GameObject("ScoreSection");
            scoreSectionGO.transform.SetParent(parent, false);
            scoreSectionGO.AddComponent<RectTransform>();
            scoreSectionGO.AddComponent<LayoutElement>().preferredHeight = 80;

            // Large circle badge
            var badgeGO = new GameObject("ScoreBadge");
            badgeGO.transform.SetParent(scoreSectionGO.transform, false);
            var badgeRT = badgeGO.AddComponent<RectTransform>();
            badgeRT.anchorMin = new Vector2(0.5f, 0.5f);
            badgeRT.anchorMax = new Vector2(0.5f, 0.5f);
            badgeRT.sizeDelta = new Vector2(70, 70);
            var badgeImg = badgeGO.AddComponent<Image>();
            badgeImg.sprite = circleSprite;
            badgeImg.color = ParentDashboardViewModel.ScoreColor(game.score);
            badgeImg.raycastTarget = false;

            // Score number (large, centered inside badge)
            var scoreTMP = AddChildTMP(badgeGO.transform, $"{game.score:F0}",
                32, Color.white, TextAlignmentOptions.Center);
            scoreTMP.fontStyle = FontStyles.Bold;
            var srt = scoreTMP.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;

            // "ציון" label below
            var scoreLabelTMP = AddChildTMP(parent, H("\u05E6\u05D9\u05D5\u05DF"), // ציון
                18, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Center);
            scoreLabelTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
        }

        // ═══════════════════════════════════════════════════════════
        //  SECTION 1: ACCESS CONTROL — mini-card style
        // ═══════════════════════════════════════════════════════════
        var accessSection = MakePanelSection(parent, H("\u05E0\u05D9\u05E8\u05D0\u05D5\u05EA")); // נראות

        // System recommendation label
        bool sysVisible = rec != null ? rec.systemRecommendsVisible : game.isInBaselineBucket;
        string sysAccessLabel = sysVisible
            ? H("\u05DE\u05D5\u05DE\u05DC\u05E5 \u05E2\u05DC \u05D9\u05D3\u05D9 \u05D4\u05DE\u05E2\u05E8\u05DB\u05EA")
            : H("\u05DC\u05D0 \u05DE\u05D5\u05DE\u05DC\u05E5 \u05DB\u05E8\u05D2\u05E2");
        var sysLabelTMP = AddChildTMP(accessSection, sysAccessLabel, 16,
            sysVisible ? HexColor("#C8E6C9") : new Color(1f, 1f, 1f, 0.5f), TextAlignmentOptions.Center);
        sysLabelTMP.enableAutoSizing = true; sysLabelTMP.fontSizeMin = 13; sysLabelTMP.fontSizeMax = 16;
        sysLabelTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;

        // 3 mode buttons (using UI kit rounded buttons)
        var accessRow = MakeHRow(accessSection, 50, TextAnchor.MiddleCenter);
        var arHL = accessRow.GetComponent<HorizontalLayoutGroup>();
        arHL.spacing = 8;
        arHL.childForceExpandWidth = true;
        arHL.childForceExpandHeight = true; // buttons fill row height

        ParentGameAccessMode currentMode = rec != null
            ? rec.accessOverrideMode : game.visibilityMode;

        var autoBtn = MakePanelToggleButton(accessRow.transform,
            H("\u05D0\u05D5\u05D8\u05D5\u05DE\u05D8\u05D9"), currentMode == ParentGameAccessMode.Default);
        var onBtn = MakePanelToggleButton(accessRow.transform,
            H("\u05E4\u05E2\u05D9\u05DC"), currentMode == ParentGameAccessMode.ForcedEnabled);
        var offBtn = MakePanelToggleButton(accessRow.transform,
            H("\u05DE\u05D5\u05E1\u05EA\u05E8"), currentMode == ParentGameAccessMode.ForcedDisabled);

        // Access explanation
        string accessExplain = rec != null
            ? ParentDashboardViewModel.GetExplanationLabel(rec.accessExplanation)
            : game.visibilityReasonDisplay;
        var accessExplainTMP = AddChildTMP(accessSection, H(accessExplain), 15,
            new Color(1f, 1f, 1f, 0.5f), TextAlignmentOptions.Center);
        accessExplainTMP.enableAutoSizing = true; accessExplainTMP.fontSizeMin = 12; accessExplainTMP.fontSizeMax = 15;
        accessExplainTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;

        // Wire access buttons
        var capturedCard = accessSection;
        var capturedScrollContent = parent;
        autoBtn.onClick.AddListener(() => OnAccessModeChanged(
            gameId, ParentGameAccessMode.Default,
            autoBtn, onBtn, offBtn,
            visChipImg, visChipTMP, accessExplainTMP,
            capturedCard, capturedScrollContent));
        onBtn.onClick.AddListener(() => OnAccessModeChanged(
            gameId, ParentGameAccessMode.ForcedEnabled,
            autoBtn, onBtn, offBtn,
            visChipImg, visChipTMP, accessExplainTMP,
            capturedCard, capturedScrollContent));
        offBtn.onClick.AddListener(() => OnAccessModeChanged(
            gameId, ParentGameAccessMode.ForcedDisabled,
            autoBtn, onBtn, offBtn,
            visChipImg, visChipTMP, accessExplainTMP,
            capturedCard, capturedScrollContent));

        // ═══════════════════════════════════════════════════════════
        //  SECTION 2: DIFFICULTY — 3 tiers (easy/medium/hard)
        //  (hidden for coloring — no meaningful difficulty)
        // ═══════════════════════════════════════════════════════════
        if (gameId != "coloring")
        {
        bool hasRec = rec != null;
        int currentDiff = hasRec ? rec.finalDifficulty : game.currentDifficulty;
        // Map 1-10 to tier: 0=easy(1-3), 1=medium(4-6), 2=hard(7-10)
        int currentTier = currentDiff <= 3 ? 0 : currentDiff <= 6 ? 1 : 2;

        var diffSection = MakePanelSection(parent, H("\u05E8\u05DE\u05EA \u05E7\u05D5\u05E9\u05D9")); // רמת קושי

        // 3 tier buttons
        var diffRow = MakeHRow(diffSection, 46, TextAnchor.MiddleCenter);
        var drHL = diffRow.GetComponent<HorizontalLayoutGroup>();
        drHL.spacing = 8;
        drHL.childForceExpandWidth = true;
        drHL.childForceExpandHeight = true;

        var easyBtn = MakePanelToggleButton(diffRow.transform,
            H("\u05E7\u05DC"), currentTier == 0); // קל
        var medBtn = MakePanelToggleButton(diffRow.transform,
            H("\u05D1\u05D9\u05E0\u05D5\u05E0\u05D9"), currentTier == 1); // בינוני
        var hardBtn = MakePanelToggleButton(diffRow.transform,
            H("\u05E7\u05E9\u05D4"), currentTier == 2); // קשה

        // Description of what this tier means for this game
        string tierDesc = GetDifficultyDescription(gameId, currentTier);
        var descTMP = AddChildTMP(diffSection, H(tierDesc), 14,
            new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Center);
        descTMP.enableAutoSizing = true; descTMP.fontSizeMin = 11; descTMP.fontSizeMax = 14;
        descTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;

        // Wire tier buttons
        var capturedDescTMP = descTMP;
        easyBtn.onClick.AddListener(() => OnDifficultyTierChanged(
            gameId, 0, easyBtn, medBtn, hardBtn, capturedDescTMP,
            diffSection, capturedScrollContent));
        medBtn.onClick.AddListener(() => OnDifficultyTierChanged(
            gameId, 1, easyBtn, medBtn, hardBtn, capturedDescTMP,
            diffSection, capturedScrollContent));
        hardBtn.onClick.AddListener(() => OnDifficultyTierChanged(
            gameId, 2, easyBtn, medBtn, hardBtn, capturedDescTMP,
            diffSection, capturedScrollContent));
        } // end if (gameId != "coloring") — difficulty

        // ═══════════════════════════════════════════════════════════
        //  SECTION 2.5: COLORING MODE (only for coloring game)
        // ═══════════════════════════════════════════════════════════
        if (gameId == "coloring")
        {
            var colorSection = MakePanelSection(parent, H("\u05DE\u05E6\u05D1 \u05E6\u05D1\u05D9\u05E2\u05D4")); // מצב צביעה
            MakeColoringModeControl(colorSection);

            MakeDrawingsGallery(parent);
        }

        // ═══════════════════════════════════════════════════════════
        //  SECTION 3: QUICK STATS — mini blocks
        //  (hidden for coloring)
        // ═══════════════════════════════════════════════════════════
        if (gameId != "coloring" && game.sessionsPlayed > 0)
        {
            var statsSection = MakePanelSection(parent, H("\u05E1\u05D8\u05D8\u05D9\u05E1\u05D8\u05D9\u05E7\u05D4")); // סטטיסטיקה

            var statsRow = MakeHRow(statsSection, 58, TextAnchor.MiddleCenter);
            var srHL = statsRow.GetComponent<HorizontalLayoutGroup>();
            srHL.spacing = 6;
            srHL.childForceExpandWidth = true;

            MakePanelStatBlock(statsRow.transform, $"{game.accuracy:P0}", H("\u05D3\u05D9\u05D5\u05E7"));
            MakePanelStatBlock(statsRow.transform, $"{game.completionRate:P0}", H("\u05D4\u05E9\u05DC\u05DE\u05D4"));
            MakePanelStatBlock(statsRow.transform, H(game.lastPlayedDisplay), H("\u05D0\u05D7\u05E8\u05D5\u05DF"));
        }
    }

    /// <summary>Creates a titled mini-card section inside the settings panel.</summary>
    private Transform MakePanelSection(Transform parent, string title)
    {
        var go = new GameObject("Section");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = uiSectionBg != null ? uiSectionBg : roundedRect;
        img.type = Image.Type.Sliced;
        img.color = new Color(1f, 1f, 1f, 0.12f);
        go.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.05f);

        var vl = go.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 6;
        vl.padding = new RectOffset(12, 12, 10, 12);
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight = false;
        vl.childControlWidth = true;
        vl.childControlHeight = true;
        go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Section title
        var titleTMP = AddChildTMP(go.transform, title, 19, Color.white, TextAlignmentOptions.Center);
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

        return go.transform;
    }

    /// <summary>Creates a toggle button styled for the settings panel (white text on colored bg).</summary>
    private Button MakePanelToggleButton(Transform parent, string label, bool active)
    {
        var go = new GameObject("PToggle");
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1;
        le.minHeight = 44;      // ensure proper mobile tap target
        le.preferredHeight = 46;

        var img = go.AddComponent<Image>();
        img.sprite = uiBtnRounded != null ? uiBtnRounded : roundedRect;
        img.type = Image.Type.Sliced;
        img.color = active ? Color.white : new Color(1f, 1f, 1f, 0.15f);

        var tmp = AddChildTMP(go.transform, label, 17,
            active ? Primary : new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Center);
        tmp.fontStyle = FontStyles.Bold;
        tmp.enableAutoSizing = true; tmp.fontSizeMin = 13; tmp.fontSizeMax = 17;
        // Stretch text to fill button
        var trt = tmp.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        return btn;
    }

    /// <summary>Creates a +/- stepper button using UI kit sprites.</summary>
    private Button MakePanelStepperButton(Transform parent, bool isPlus)
    {
        var go = new GameObject(isPlus ? "PlusBtn" : "MinusBtn");
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = 48;
        le.preferredWidth = 48;
        le.minHeight = 48;
        le.preferredHeight = 48;

        var img = go.AddComponent<Image>();
        Sprite icon = isPlus ? uiPlus : uiMinus;
        img.sprite = icon != null ? icon : roundedRect;
        if (icon != null) img.preserveAspect = true;
        img.color = Color.white;
        img.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        return btn;
    }

    /// <summary>Creates a stat block (big number + label) for the settings panel.</summary>
    private void MakePanelStatBlock(Transform parent, string value, string label)
    {
        var go = new GameObject("StatBlock");
        go.transform.SetParent(parent, false);
        go.AddComponent<LayoutElement>().flexibleWidth = 1;
        var bgImg = go.AddComponent<Image>();
        bgImg.sprite = roundedRect;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = new Color(1f, 1f, 1f, 0.1f);
        bgImg.raycastTarget = false;

        var vl = go.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 2;
        vl.padding = new RectOffset(4, 4, 6, 6);
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight = false;
        vl.childControlWidth = true;
        vl.childControlHeight = true;
        vl.childAlignment = TextAnchor.MiddleCenter;

        var valTMP = AddChildTMP(go.transform, value, 22, Color.white, TextAlignmentOptions.Center);
        valTMP.fontStyle = FontStyles.Bold;
        valTMP.enableAutoSizing = true; valTMP.fontSizeMin = 16; valTMP.fontSizeMax = 22;
        valTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

        var lblTMP = AddChildTMP(go.transform, label, 14,
            new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Center);
        lblTMP.enableAutoSizing = true; lblTMP.fontSizeMin = 11; lblTMP.fontSizeMax = 14;
        lblTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;
    }

    private static readonly Color SelectionBorder = HexColor("#64B5F6");   // bright blue highlight for selected card
    private static readonly Color CardNormalTint  = new Color(0.85f, 0.92f, 1f, 1f); // slightly muted in dark mode
    private static readonly Color CardSelectedTint = Color.white; // full brightness when selected

    private void MakeGameCard3Col(Transform parent, GameDashboardData game, int index)
    {
        string gameId = game.gameId;
        bool isVisible = game.recommendation != null ? game.recommendation.finalVisible : game.systemVisibility;

        // ── Card container: unified background ──
        var cardGO = new GameObject($"GameCard_{gameId}");
        cardGO.transform.SetParent(parent, false);
        cardGO.AddComponent<RectTransform>();
        var cardImg = cardGO.AddComponent<Image>();
        cardImg.sprite = uiCardBlue != null ? uiCardBlue : roundedRect;
        cardImg.type = Image.Type.Sliced;
        cardImg.color = CardNormalTint;
        cardImg.raycastTarget = true;

        // Track for selection management
        _cardBackgrounds.Add(cardImg);

        var cardVL = cardGO.AddComponent<VerticalLayoutGroup>();
        cardVL.spacing = 8;
        cardVL.childForceExpandWidth = true;
        cardVL.childForceExpandHeight = false;
        cardVL.childControlWidth = true;
        cardVL.childControlHeight = true;
        cardVL.childAlignment = TextAnchor.UpperCenter;
        cardVL.padding = new RectOffset(20, 20, 20, 18);

        // ── Selection highlight: dual outline on card (disabled by default) ──
        var cardOutline1 = cardGO.AddComponent<Outline>();
        cardOutline1.effectColor = SelectionBorder;
        cardOutline1.effectDistance = new Vector2(3, 3);
        cardOutline1.enabled = false;
        var cardOutline2 = cardGO.AddComponent<Outline>();
        cardOutline2.effectColor = new Color(SelectionBorder.r, SelectionBorder.g, SelectionBorder.b, 0.35f);
        cardOutline2.effectDistance = new Vector2(6, 6);
        cardOutline2.enabled = false;
        _cardOutlines.Add(new[] { cardOutline1, cardOutline2 });

        // ── Status indicator (top-right corner, non-interactive) ──
        var indicatorGO = new GameObject("StatusIndicator");
        indicatorGO.transform.SetParent(cardGO.transform, false);
        var indRT = indicatorGO.AddComponent<RectTransform>();
        indRT.anchorMin = new Vector2(1, 1); indRT.anchorMax = new Vector2(1, 1);
        indRT.pivot = new Vector2(1, 1);
        indRT.anchoredPosition = new Vector2(-12, -12);
        indRT.sizeDelta = new Vector2(32, 32);
        indicatorGO.AddComponent<LayoutElement>().ignoreLayout = true;
        var indImg = indicatorGO.AddComponent<Image>();
        indImg.sprite = circleSprite;
        indImg.color = isVisible ? HexColor("#4CAF50") : HexColor("#BDBDBD");
        indImg.raycastTarget = false;

        // Icon inside indicator (checkmark or minus)
        if (uiCheckIcon != null || uiMinus != null)
        {
            var indIconGO = new GameObject("IndIcon");
            indIconGO.transform.SetParent(indicatorGO.transform, false);
            var iiRT = indIconGO.AddComponent<RectTransform>();
            iiRT.anchorMin = new Vector2(0.15f, 0.15f); iiRT.anchorMax = new Vector2(0.85f, 0.85f);
            iiRT.offsetMin = Vector2.zero; iiRT.offsetMax = Vector2.zero;
            var iiImg = indIconGO.AddComponent<Image>();
            iiImg.sprite = isVisible ? uiCheckIcon : uiMinus;
            iiImg.preserveAspect = true;
            iiImg.color = Color.white;
            iiImg.raycastTarget = false;
        }

        // ── Game icon area (soft background + centered image with fallback) ──
        var iconHolder = new GameObject("IconHolder");
        iconHolder.transform.SetParent(cardGO.transform, false);
        iconHolder.AddComponent<RectTransform>();
        var iconHolderLE = iconHolder.AddComponent<LayoutElement>();
        iconHolderLE.preferredHeight = 130;

        // Soft container background behind the thumbnail
        var iconBgGO = new GameObject("IconBg");
        iconBgGO.transform.SetParent(iconHolder.transform, false);
        var ibRT = iconBgGO.AddComponent<RectTransform>();
        ibRT.anchorMin = new Vector2(0.5f, 0.5f);
        ibRT.anchorMax = new Vector2(0.5f, 0.5f);
        ibRT.sizeDelta = new Vector2(118, 118);
        var ibImg = iconBgGO.AddComponent<Image>();
        ibImg.sprite = uiSectionBg != null ? uiSectionBg : roundedRect;
        ibImg.type = Image.Type.Sliced;
        ibImg.color = new Color(1f, 1f, 1f, 0.25f);
        ibImg.raycastTarget = false;

        // Resolve thumbnail — use fallback if missing
        var gameItem = FindGameItemFromDb(gameId);
        Sprite thumbSprite = (gameItem != null && gameItem.thumbnail != null)
            ? gameItem.thumbnail
            : uiPlaceholder; // UI_2_6 question mark fallback

        if (thumbSprite != null)
        {
            var thumbGO = new GameObject("Thumb");
            thumbGO.transform.SetParent(iconHolder.transform, false);
            var thumbRT = thumbGO.AddComponent<RectTransform>();
            thumbRT.anchorMin = new Vector2(0.5f, 0.5f);
            thumbRT.anchorMax = new Vector2(0.5f, 0.5f);
            thumbRT.sizeDelta = new Vector2(110, 110);
            var thumbImg = thumbGO.AddComponent<Image>();
            thumbImg.sprite = thumbSprite;
            thumbImg.preserveAspect = true;
            thumbImg.raycastTarget = false;
        }

        // ── Game name (very large, bold, centered) ──
        var nameTMP = AddChildTMP(cardGO.transform, H(game.gameName), 34, Color.white, TextAlignmentOptions.Center);
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;

        // ── Play count subtitle ──
        string playText = game.sessionsPlayed > 0
            ? H($"\u05E9\u05D5\u05D7\u05E7 {game.sessionsPlayed} \u05E4\u05E2\u05DE\u05D9\u05DD")
            : H("\u05E2\u05D3\u05D9\u05D9\u05DF \u05DC\u05D0 \u05E9\u05D5\u05D7\u05E7");
        var subTMP = AddChildTMP(cardGO.transform, playText, 24, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Center);
        subTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;

        // ── Spacer ──
        var spacer = new GameObject("Spacer");
        spacer.transform.SetParent(cardGO.transform, false);
        spacer.AddComponent<RectTransform>();
        spacer.AddComponent<LayoutElement>().flexibleHeight = 1;

        // ── "ניהול" (Manage) button — single primary action ──
        var manageBtnGO = new GameObject("ManageBtn");
        manageBtnGO.transform.SetParent(cardGO.transform, false);
        manageBtnGO.AddComponent<RectTransform>();
        var manageLE = manageBtnGO.AddComponent<LayoutElement>();
        manageLE.preferredHeight = 56;

        var manageBgImg = manageBtnGO.AddComponent<Image>();
        manageBgImg.sprite = uiBarGreen != null ? uiBarGreen : roundedRect;
        manageBgImg.type = Image.Type.Sliced;
        manageBgImg.color = HexColor("#4CAF50");
        manageBgImg.raycastTarget = true;

        var manageLabelTMP = AddChildTMP(manageBtnGO.transform,
            H("\u05E0\u05D9\u05D4\u05D5\u05DC"), 30, Color.white, TextAlignmentOptions.Center); // ניהול
        manageLabelTMP.fontStyle = FontStyles.Bold;
        var mrt = manageLabelTMP.GetComponent<RectTransform>();
        mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one;
        mrt.offsetMin = Vector2.zero; mrt.offsetMax = Vector2.zero;

        // Button selects this card and updates the right settings panel
        var manageBtn = manageBtnGO.AddComponent<Button>();
        manageBtn.targetGraphic = manageBgImg;
        int capturedIndex = index;
        GameDashboardData capturedGame = game;
        manageBtn.onClick.AddListener(() => SelectGameCard(capturedIndex, capturedGame));

        // Tapping the card body also selects it
        var cardBtn = cardGO.AddComponent<Button>();
        cardBtn.targetGraphic = cardImg;
        cardBtn.transition = Selectable.Transition.None;
        cardBtn.onClick.AddListener(() => SelectGameCard(capturedIndex, capturedGame));
    }

    /// <summary>Selects a game card: highlights it, deselects previous, updates settings panel.</summary>
    private void SelectGameCard(int index, GameDashboardData game)
    {
        // Deselect previous
        if (_selectedGameIndex >= 0 && _selectedGameIndex < _cardOutlines.Count)
        {
            foreach (var o in _cardOutlines[_selectedGameIndex]) o.enabled = false;
            if (_selectedGameIndex < _cardBackgrounds.Count)
                _cardBackgrounds[_selectedGameIndex].color = CardNormalTint;
        }

        // Select new
        _selectedGameIndex = index;
        if (index >= 0 && index < _cardOutlines.Count)
        {
            foreach (var o in _cardOutlines[index]) o.enabled = true;
            if (index < _cardBackgrounds.Count)
                _cardBackgrounds[index].color = CardSelectedTint;
        }

        UpdateSettingsPanel(game);
    }

    private void MakeGameListRow(Transform parent, GameDashboardData game)
    {
        string gameId = game.gameId;
        var rec = game.recommendation;

        // Use MakeCard (proven to work) then put a horizontal row inside it
        var card = MakeCard(parent);
        FitCard(card); // mark as auto-height

        // Single horizontal row inside the card
        var row = MakeHRow(card, 50, TextAnchor.MiddleRight);
        row.GetComponent<HorizontalLayoutGroup>().spacing = 16;

        // ── Score badge ──
        var badgeGO = new GameObject("Badge");
        badgeGO.transform.SetParent(row.transform, false);
        var badgeImg = badgeGO.AddComponent<Image>();
        if (circleSprite != null) badgeImg.sprite = circleSprite;
        badgeImg.color = game.sessionsPlayed > 0
            ? ParentDashboardViewModel.ScoreColor(game.score)
            : HexColor("#E0E0E0");
        badgeImg.raycastTarget = false;
        var badgeLE = badgeGO.AddComponent<LayoutElement>();
        badgeLE.minWidth = 36;
        badgeLE.preferredWidth = 36;
        badgeLE.flexibleWidth = 0;
        badgeLE.preferredHeight = 49;

        // Score text inside badge
        var btTMP = AddChildTMP(badgeGO.transform,
            game.sessionsPlayed > 0 ? $"{game.score:F0}" : "\u2014",
            13, Color.white, TextAlignmentOptions.Center);
        btTMP.fontStyle = FontStyles.Bold;
        var btRT = btTMP.rectTransform;
        btRT.anchorMin = Vector2.zero; btRT.anchorMax = Vector2.one;
        btRT.offsetMin = Vector2.zero; btRT.offsetMax = Vector2.zero;

        // ── Game name (fixed width for uniform look) ──
        var nameTMP = AddChildTMP(row.transform, H(game.gameName), 15, TextDark, TextAlignmentOptions.Right);
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.enableAutoSizing = true;
        nameTMP.fontSizeMin = 11;
        nameTMP.fontSizeMax = 15;
        var nameLE = nameTMP.gameObject.AddComponent<LayoutElement>();
        nameLE.minWidth = 120;
        nameLE.preferredWidth = 120;
        nameLE.flexibleWidth = 0;

        // ── Level info ──
        string info = game.sessionsPlayed > 0
            ? H($"\u05E8\u05DE\u05D4 {game.currentDifficulty}")
            : H("\u2014");
        var infoTMP = AddChildTMP(row.transform, info, 13, TextMedium, TextAlignmentOptions.Center);
        var infoLE = infoTMP.gameObject.AddComponent<LayoutElement>();
        infoLE.minWidth = 50;
        infoLE.preferredWidth = 50;
        infoLE.flexibleWidth = 0;

        // ── Toggle ON/OFF ──
        bool isEnabled = rec != null ? rec.finalVisible : game.systemVisibility;
        var toggleGO = new GameObject("Toggle");
        toggleGO.transform.SetParent(row.transform, false);
        var toggleBg = toggleGO.AddComponent<Image>();
        toggleBg.sprite = null; // plain rect, no rounded corners
        toggleBg.color = isEnabled ? HexColor("#66BB6A") : HexColor("#BDBDBD");
        toggleBg.raycastTarget = true;
        var toggleLE = toggleGO.AddComponent<LayoutElement>();
        toggleLE.minWidth = 50;
        toggleLE.preferredWidth = 50;
        toggleLE.flexibleWidth = 0;
        toggleLE.preferredHeight = 35;

        // Toggle knob
        var knobGO = new GameObject("Knob");
        knobGO.transform.SetParent(toggleGO.transform, false);
        var knobRT = knobGO.AddComponent<RectTransform>();
        knobRT.anchorMin = new Vector2(isEnabled ? 0.55f : 0.05f, 0.1f);
        knobRT.anchorMax = new Vector2(isEnabled ? 0.95f : 0.45f, 0.9f);
        knobRT.offsetMin = Vector2.zero;
        knobRT.offsetMax = Vector2.zero;
        var knobImg = knobGO.AddComponent<Image>();
        knobImg.sprite = null; // plain rect
        knobImg.color = Color.white;
        knobImg.raycastTarget = false;

        // Toggle button action
        var toggleBtn = toggleGO.AddComponent<Button>();
        toggleBtn.targetGraphic = toggleBg;
        toggleBtn.transition = Selectable.Transition.None;
        string capturedId = gameId;
        bool capturedState = isEnabled;
        toggleBtn.onClick.AddListener(() =>
        {
            bool newState = !capturedState;
            var profile = ProfileManager.ActiveProfile;
            if (profile == null) return;

            // Prevent disabling the last visible game
            if (!newState && WouldLeaveZeroVisibleGames(capturedId)) return;

            var mode = newState ? ParentGameAccessMode.ForcedEnabled : ParentGameAccessMode.ForcedDisabled;
            GameVisibilityService.SetOverride(profile, capturedId, mode);
            ProfileManager.Instance.Save();

            // Update visuals
            toggleBg.color = newState ? HexColor("#66BB6A") : HexColor("#BDBDBD");
            knobRT.anchorMin = new Vector2(newState ? 0.55f : 0.05f, 0.1f);
            knobRT.anchorMax = new Vector2(newState ? 0.95f : 0.45f, 0.9f);
            capturedState = newState;
        });

        // ── Game preview thumbnail (leftmost visually in RTL = last in hierarchy) ──
        var gameItem = FindGameItemFromDb(gameId);
        if (gameItem != null && gameItem.thumbnail != null)
        {
            var thumbGO = new GameObject("Preview");
            thumbGO.transform.SetParent(row.transform, false);
            var thumbImg = thumbGO.AddComponent<Image>();
            thumbImg.sprite = gameItem.thumbnail;
            thumbImg.preserveAspect = true;
            thumbImg.raycastTarget = false;
            var thumbLE = thumbGO.AddComponent<LayoutElement>();
            thumbLE.minWidth = 42;
            thumbLE.preferredWidth = 42;
            thumbLE.flexibleWidth = 0;
            thumbLE.preferredHeight = 57;
        }

        // ── "Too hard" recommendation if game is visible but above age bucket ──
        if (!game.isInBaselineBucket && game.sessionsPlayed > 0)
        {
            var warnTMP = AddChildTMP(card,
                H("\u26A0 \u05DE\u05E9\u05D7\u05E7 \u05D6\u05D4 \u05E7\u05E6\u05EA \u05E7\u05E9\u05D4 \u05DC\u05D9\u05DC\u05D3. \u05DE\u05D5\u05DE\u05DC\u05E5 \u05DC\u05D4\u05E1\u05EA\u05D9\u05E8."),
                // ⚠ משחק זה קצת קשה לילד. מומלץ להסתיר.
                11, AccentOrange, TextAlignmentOptions.Right);
            warnTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
        }
    }

    // (Game details overlay removed — settings panel is the single UI for game settings)
    private void MakeColoringModeControl(Transform card)
    {
        // Title row
        var titleRow = MakeHRow(card, 28, TextAnchor.MiddleRight);
        titleRow.GetComponent<HorizontalLayoutGroup>().spacing = 8;
        var titleTMP = AddChildTMP(titleRow.transform,
            H("\u05E1\u05D2\u05E0\u05D5\u05DF \u05E6\u05D1\u05D9\u05E2\u05D4"), // סגנון צביעה
            15, TextMedium, TextAlignmentOptions.Right);
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        MakeSpacer(card, 4f);

        // Segmented control: Auto / Fill / Brush
        var segRow = MakeHRow(card, 38, TextAnchor.MiddleCenter);
        segRow.GetComponent<HorizontalLayoutGroup>().spacing = 0;
        segRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;

        // Background
        var segBgImg = segRow.AddComponent<Image>();
        if (roundedRect != null) { segBgImg.sprite = roundedRect; segBgImg.type = Image.Type.Sliced; }
        segBgImg.color = BarBg;
        segBgImg.raycastTarget = false;

        var current = AppSettings.ColoringMode;

        string[] labels = {
            "\u05DC\u05E4\u05D9 \u05D2\u05D9\u05DC",  // לפי גיל
            "\u05DE\u05D9\u05DC\u05D5\u05D9",          // מילוי
            "\u05DE\u05D1\u05E8\u05E9\u05D5\u05EA"     // מברשות
        };
        var modes = new[] { ColoringModeOption.Auto, ColoringModeOption.AreaFill, ColoringModeOption.Brush };

        var btnImages = new Image[3];
        var btnTexts = new TextMeshProUGUI[3];

        for (int i = 0; i < 3; i++)
        {
            var btnGO = new GameObject($"ColorMode_{i}");
            btnGO.transform.SetParent(segRow.transform, false);
            btnGO.AddComponent<RectTransform>();

            var btnImg = btnGO.AddComponent<Image>();
            if (roundedRect != null) { btnImg.sprite = roundedRect; btnImg.type = Image.Type.Sliced; }
            bool active = current == modes[i];
            btnImg.color = active ? Primary : Color.clear;
            btnImg.raycastTarget = true;
            btnImages[i] = btnImg;

            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(btnGO.transform, false);
            var lblRT = lblGO.AddComponent<RectTransform>();
            lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = Vector2.zero; lblRT.offsetMax = Vector2.zero;
            var lblTMP = lblGO.AddComponent<TextMeshProUGUI>();
            HebrewText.SetText(lblTMP, labels[i]);
            lblTMP.fontSize = 20;
            lblTMP.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            lblTMP.color = active ? Color.white : TextDark;
            lblTMP.alignment = TextAlignmentOptions.Center;
            lblTMP.raycastTarget = false;
            btnTexts[i] = lblTMP;

            int idx = i;
            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(() =>
            {
                AppSettings.ColoringMode = modes[idx];
                for (int j = 0; j < 3; j++)
                {
                    bool sel = j == idx;
                    btnImages[j].color = sel ? Primary : Color.clear;
                    btnTexts[j].color = sel ? Color.white : TextDark;
                    btnTexts[j].fontStyle = sel ? FontStyles.Bold : FontStyles.Normal;
                }
            });
        }

        // Explanation
        MakeSpacer(card, 2f);
        string explain = current == ColoringModeOption.Auto
            ? H("\u05D2\u05D9\u05DC 2\u20134: \u05DE\u05D9\u05DC\u05D5\u05D9 \u05D0\u05D6\u05D5\u05E8\u05D9\u05DD | \u05D2\u05D9\u05DC 5+: \u05DE\u05D1\u05E8\u05E9\u05D5\u05EA") // גיל 2–4: מילוי אזורים | גיל 5+: מברשות
            : current == ColoringModeOption.AreaFill
                ? H("\u05DC\u05D7\u05D9\u05E6\u05D4 \u05E2\u05DC \u05D0\u05D6\u05D5\u05E8 \u05DE\u05DE\u05DC\u05D0\u05EA \u05D0\u05D5\u05EA\u05D5 \u05D1\u05E6\u05D1\u05E2") // לחיצה על אזור ממלאת אותו בצבע
                : H("\u05E6\u05D9\u05D5\u05E8 \u05D7\u05D5\u05E4\u05E9\u05D9 \u05E2\u05DD \u05DE\u05D1\u05E8\u05E9\u05D5\u05EA"); // ציור חופשי עם מברשות
        var explainTMP = AddChildTMP(card, explain, 12, TextLight, TextAlignmentOptions.Right);
        explainTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
    }

    private void MakeCustomColorsSection(Transform card)
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile?.colorStudio?.savedColors == null || profile.colorStudio.savedColors.Count == 0)
        {
            AddChildTMP(card, H("\u05D0\u05D9\u05DF \u05E6\u05D1\u05E2\u05D9\u05DD \u05DE\u05D5\u05EA\u05D0\u05DE\u05D9\u05DD \u05E2\u05D3\u05D9\u05D9\u05DF"), // אין צבעים מותאמים עדיין
                14, TextLight, TextAlignmentOptions.Center)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 35;
            return;
        }

        var titleTMP = AddChildTMP(card,
            H($"\u05E6\u05D1\u05E2\u05D9\u05DD \u05DE\u05D5\u05EA\u05D0\u05DE\u05D9\u05DD ({profile.colorStudio.savedColors.Count})"), // צבעים מותאמים (X)
            16, TextDark, TextAlignmentOptions.Right);
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

        var subtitleTMP = AddChildTMP(card,
            H("\u05E6\u05D1\u05E2\u05D9\u05DD \u05E9\u05E0\u05D5\u05E6\u05E8\u05D5 \u05D1\u05E1\u05D8\u05D5\u05D3\u05D9\u05D5 \u05DE\u05D5\u05E4\u05D9\u05E2\u05D9\u05DD \u05D1\u05E4\u05DC\u05D8\u05EA \u05D4\u05E6\u05D1\u05D9\u05E2\u05D4"), // צבעים שנוצרו בסטודיו מופיעים בפלטת הצביעה
            12, TextLight, TextAlignmentOptions.Right);
        subtitleTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;

        // Color circles grid
        var gridGO = new GameObject("CustomColorsGrid");
        gridGO.transform.SetParent(card, false);
        gridGO.AddComponent<RectTransform>();
        var gridLE = gridGO.AddComponent<LayoutElement>();
        gridLE.preferredHeight = 50;
        gridLE.flexibleWidth = 1;
        var grid = gridGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(38, 38);
        grid.spacing = new Vector2(6, 6);
        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.constraint = GridLayoutGroup.Constraint.Flexible;

        var circleSprite = Resources.Load<Sprite>("Circle");
        int maxShow = Mathf.Min(profile.colorStudio.savedColors.Count, 16);
        for (int i = 0; i < maxShow; i++)
        {
            var cc = profile.colorStudio.savedColors[i];
            var cellGO = new GameObject($"CC_{i}");
            cellGO.transform.SetParent(gridGO.transform, false);
            var img = cellGO.AddComponent<Image>();
            if (circleSprite != null) img.sprite = circleSprite;
            ColorUtility.TryParseHtmlString(cc.hex, out Color c);
            img.color = c;
            img.raycastTarget = false;
            var outline = cellGO.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.2f);
            outline.effectDistance = new Vector2(1, -1);
        }

        if (profile.colorStudio.savedColors.Count > maxShow)
        {
            AddChildTMP(card,
                H($"+{profile.colorStudio.savedColors.Count - maxShow} \u05E2\u05D5\u05D3"), // +X עוד
                12, TextLight, TextAlignmentOptions.Center)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
        }
    }

    private void MakeDrawingsGallery(Transform card)
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null || profile.savedDrawings == null || profile.savedDrawings.Count == 0)
        {
            AddChildTMP(card, H("\u05D0\u05D9\u05DF \u05E6\u05D9\u05D5\u05E8\u05D9\u05DD \u05E2\u05D3\u05D9\u05D9\u05DF"), // אין ציורים עדיין
                14, TextLight, TextAlignmentOptions.Center)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 41;
            return;
        }

        var titleTMP = AddChildTMP(card, H("\u05E6\u05D9\u05D5\u05E8\u05D9\u05DD"), // ציורים
            16, Color.white, TextAlignmentOptions.Right);
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;

        // Grid of recent drawings — 3 per row, tap to share
        var gridGO = new GameObject("DrawingsGrid");
        gridGO.transform.SetParent(card, false);
        gridGO.AddComponent<RectTransform>();
        var gridLE = gridGO.AddComponent<LayoutElement>();
        gridLE.flexibleWidth = 1;
        var drawGrid = gridGO.AddComponent<GridLayoutGroup>();
        drawGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        drawGrid.constraintCount = 3;
        drawGrid.cellSize = new Vector2(130, 130);
        drawGrid.spacing = new Vector2(6, 6);
        drawGrid.childAlignment = TextAnchor.UpperCenter;
        gridGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        string appLink = "https://play.google.com/store/apps/details?id=com.elroey.kidslearning";
        string childName = profile.displayName ?? "";

        int count = Mathf.Min(profile.savedDrawings.Count, 9);
        for (int i = profile.savedDrawings.Count - 1; i >= profile.savedDrawings.Count - count; i--)
        {
            var drawing = profile.savedDrawings[i];
            string fullPath = System.IO.Path.Combine(Application.persistentDataPath, drawing.imagePath);

            if (!System.IO.File.Exists(fullPath)) continue;

            // Cell = just the drawing image, tap to share
            var cellGO = new GameObject($"Drawing_{i}");
            cellGO.transform.SetParent(gridGO.transform, false);

            var img = cellGO.AddComponent<Image>();
            img.raycastTarget = true;

            // Load drawing texture
            byte[] bytes = System.IO.File.ReadAllBytes(fullPath);
            var tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);
            img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f);
            img.preserveAspect = true;

            // Tap drawing to share
            var shareBtn = cellGO.AddComponent<Button>();
            shareBtn.targetGraphic = img;

            string capturedPath = fullPath;
            string capturedName = childName;
            shareBtn.onClick.AddListener(() => ShareDrawing(capturedPath, capturedName, appLink));
        }
    }

    private void ShareDrawing(string imagePath, string childName, string appLink)
    {
        FirebaseAnalyticsManager.LogDrawingShared();
        string text = $"\u05EA\u05E8\u05D0\u05D5 \u05D0\u05D9\u05D6\u05D4 \u05E6\u05D9\u05D5\u05E8 \u05D9\u05E4\u05D4 {childName} \u05E2\u05E9\u05D4 \u05D1\u05DC\u05D5\u05DE\u05D3\u05D9\u05DD \u05E2\u05DD \u05D0\u05DC\u05D9\u05DF \uD83C\uDFA8\n{appLink}";
        // תראו איזה ציור יפה X עשה בלומדים עם אלין 🎨

        #if UNITY_ANDROID && !UNITY_EDITOR
        CertificateGenerator.ShareImageWithTextAndroid(imagePath, text);
        #elif UNITY_IOS && !UNITY_EDITOR
        CertificateGenerator.ShareImageWithTextIOS(imagePath, text);
        #else
        GUIUtility.systemCopyBuffer = imagePath;
        Debug.Log($"[ShareDrawing] Editor — path: {imagePath}\nText: {text}");
        Application.OpenURL("file://" + imagePath);
        #endif
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

        // 2-column layout for landscape (domain cards are tall, 2 fits better)
        for (int i = 0; i < _data.categories.Count; i += 2)
        {
            if (_data.categories[i].contributingGamesCount == 0 && (i + 1 >= _data.categories.Count || _data.categories[i + 1].contributingGamesCount == 0)) continue;

            var domainRow = MakeHRow(parent, 0, TextAnchor.UpperRight);
            var domainLayout = domainRow.GetComponent<HorizontalLayoutGroup>();
            domainLayout.spacing = 21;
            domainLayout.childForceExpandWidth = true;
            domainLayout.childControlWidth = true;
            domainLayout.childForceExpandHeight = false;
            domainLayout.childControlHeight = true;
            domainRow.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (_data.categories[i].contributingGamesCount > 0)
                MakeCategoryCard(domainRow.transform, _data.categories[i]);
            if (i + 1 < _data.categories.Count && _data.categories[i + 1].contributingGamesCount > 0)
                MakeCategoryCard(domainRow.transform, _data.categories[i + 1]);
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
        accent.AddComponent<LayoutElement>().preferredHeight = 5;

        // Header row
        var headerRow = MakeHRow(card, 56, TextAnchor.MiddleRight);
        headerRow.GetComponent<HorizontalLayoutGroup>().spacing = 16;

        // Score pill
        var scoreGO = new GameObject("Score");
        scoreGO.transform.SetParent(headerRow.transform, false);
        var scoreLEComp = scoreGO.AddComponent<LayoutElement>();
        scoreLEComp.preferredWidth = 52;
        scoreLEComp.preferredHeight = 41;
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
        catNameTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;

        string trendStr = $"{ParentDashboardViewModel.TrendArrow(cat.trend)} {H(cat.trendLabel)}";
        var trendTMP = AddChildTMP(nameCol.transform, trendStr, 13, TextMedium, TextAlignmentOptions.Right);
        trendTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;

        // Progress bar
        MakeProgressBar(card, cat.score / 100f, cat.color, 10f);

        // Confidence + summary
        var confLine = $"{H(cat.confidenceLabel)} | {H(cat.insightText)}";
        var confTMP = AddChildTMP(card, confLine, 12, TextLight, TextAlignmentOptions.Right);
        confTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;

        // Details (hidden)
        var detailsGO = new GameObject("Details");
        detailsGO.transform.SetParent(card, false);
        detailsGO.SetActive(false);
        var detailsL = detailsGO.AddComponent<VerticalLayoutGroup>();
        detailsL.spacing = 8;
        detailsL.padding = new RectOffset(2, 2, 10, 10);
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
            summaryTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;
        }

        if (cat.contributions.Count > 0)
        {
            var gTitle = AddChildTMP(detailsGO.transform,
                H("\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05EA\u05D5\u05E8\u05DE\u05D9\u05DD"), // משחקים תורמים
                16, TextDark, TextAlignmentOptions.Right);
            gTitle.fontStyle = FontStyles.Bold;
            gTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;

            foreach (var contrib in cat.contributions)
            {
                var cRow = MakeHRow(detailsGO.transform, 24, TextAnchor.MiddleRight);
                cRow.GetComponent<HorizontalLayoutGroup>().spacing = 10;

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
        trendTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 43;

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
            row.GetComponent<HorizontalLayoutGroup>().spacing = 10;

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
            row.GetComponent<HorizontalLayoutGroup>().spacing = 10;

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

    private void OnDifficultyTierChanged(string gameId, int tier,
        Button easyBtn, Button medBtn, Button hardBtn,
        TextMeshProUGUI descTMP, Transform card, Transform scrollContent)
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        // Map tier to difficulty value: easy=2, medium=5, hard=8
        int newDiff = tier == 0 ? 2 : tier == 1 ? 5 : 8;

        var gp = profile.analytics.GetOrCreateGame(gameId);
        if (!gp.manualDifficultyOverride)
            gp.lastAutoDifficulty = gp.currentDifficulty;

        gp.currentDifficulty = newDiff;
        gp.manualDifficultyOverride = true;
        gp.consecutiveStrongResults = 0;
        gp.consecutiveWeakResults = 0;
        ProfileManager.Instance.Save();

        // Update button states
        UpdateToggleButton(easyBtn, tier == 0);
        UpdateToggleButton(medBtn, tier == 1);
        UpdateToggleButton(hardBtn, tier == 2);

        // Update description
        string desc = GetDifficultyDescription(gameId, tier);
        HebrewText.SetText(descTMP, H(desc));

        // Sync stale data
        SyncGameDifficulty(gameId, newDiff, true);

        LayoutRebuilder.ForceRebuildLayoutImmediate(card.GetComponent<RectTransform>());
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent.GetComponent<RectTransform>());
    }

    private static string GetDifficultyDescription(string gameId, int tier)
    {
        // tier: 0=easy, 1=medium, 2=hard
        switch (gameId)
        {
            case "memory":
                return tier == 0 ? "4\u00D72 \u05DB\u05E8\u05D8\u05D9\u05E1\u05D9\u05DD"   // 4×2 כרטיסים
                     : tier == 1 ? "4\u00D73 \u05DB\u05E8\u05D8\u05D9\u05E1\u05D9\u05DD"   // 4×3 כרטיסים
                     :              "4\u00D74 \u05DB\u05E8\u05D8\u05D9\u05E1\u05D9\u05DD";  // 4×4 כרטיסים
            case "puzzle":
                return tier == 0 ? "3\u00D73 \u05D7\u05DC\u05E7\u05D9\u05DD"  // 3×3 חלקים
                     : tier == 1 ? "4\u00D74 \u05D7\u05DC\u05E7\u05D9\u05DD"  // 4×4 חלקים
                     :              "5\u00D75 \u05D7\u05DC\u05E7\u05D9\u05DD"; // 5×5 חלקים
            case "shadows":
                return tier == 0 ? "3 \u05D7\u05D9\u05D5\u05EA"   // 3 חיות
                     : tier == 1 ? "4 \u05D7\u05D9\u05D5\u05EA"   // 4 חיות
                     :              "5 \u05D7\u05D9\u05D5\u05EA";  // 5 חיות
            case "fishing":
                return tier == 0 ? "4 \u05D3\u05D2\u05D9\u05DD, \u05D0\u05D9\u05D8\u05D9"     // 4 דגים, איטי
                     : tier == 1 ? "6 \u05D3\u05D2\u05D9\u05DD, \u05D1\u05D9\u05E0\u05D5\u05E0\u05D9" // 6 דגים, בינוני
                     :              "8 \u05D3\u05D2\u05D9\u05DD, \u05DE\u05D4\u05D9\u05E8";   // 8 דגים, מהיר
            case "laundrysorting":
                return tier == 0 ? "9 \u05E4\u05E8\u05D9\u05D8\u05D9\u05DD"   // 9 פריטים
                     : tier == 1 ? "15 \u05E4\u05E8\u05D9\u05D8\u05D9\u05DD"  // 15 פריטים
                     :              "21 \u05E4\u05E8\u05D9\u05D8\u05D9\u05DD"; // 21 פריטים
            case "oddoneout":
                return tier == 0 ? "4 \u05D7\u05D9\u05D5\u05EA"   // 4 חיות
                     : tier == 1 ? "6 \u05D7\u05D9\u05D5\u05EA"   // 6 חיות
                     :              "8 \u05D7\u05D9\u05D5\u05EA";  // 8 חיות
            case "quantitymatch":
                return tier == 0 ? "\u05DE\u05E1\u05E4\u05E8\u05D9\u05DD 1-3"  // מספרים 1-3
                     : tier == 1 ? "\u05DE\u05E1\u05E4\u05E8\u05D9\u05DD 1-5"  // מספרים 1-5
                     :              "\u05DE\u05E1\u05E4\u05E8\u05D9\u05DD 1-8"; // מספרים 1-8
            case "numbertrain":
                return tier == 0 ? "6 \u05E7\u05E8\u05D5\u05E0\u05D5\u05EA, 3 \u05D7\u05E1\u05E8\u05D9\u05DD"  // 6 קרונות, 3 חסרים
                     : tier == 1 ? "8 \u05E7\u05E8\u05D5\u05E0\u05D5\u05EA, 5 \u05D7\u05E1\u05E8\u05D9\u05DD"  // 8 קרונות, 5 חסרים
                     :              "10 \u05E7\u05E8\u05D5\u05E0\u05D5\u05EA, 7 \u05D7\u05E1\u05E8\u05D9\u05DD"; // 10 קרונות, 7 חסרים
            case "lettertrain":
                return tier == 0 ? "6 \u05E7\u05E8\u05D5\u05E0\u05D5\u05EA, 3 \u05D7\u05E1\u05E8\u05D5\u05EA"  // 6 קרונות, 3 חסרות
                     : tier == 1 ? "8 \u05E7\u05E8\u05D5\u05E0\u05D5\u05EA, 5 \u05D7\u05E1\u05E8\u05D5\u05EA"  // 8 קרונות, 5 חסרות
                     :              "10 \u05E7\u05E8\u05D5\u05E0\u05D5\u05EA, 7 \u05D7\u05E1\u05E8\u05D5\u05EA"; // 10 קרונות, 7 חסרות
            case "flappybird":
                return tier == 0 ? "\u05E8\u05D5\u05D5\u05D7 \u05E8\u05D7\u05D1, \u05D0\u05D9\u05D8\u05D9"  // רווח רחב, איטי
                     : tier == 1 ? "\u05E8\u05D5\u05D5\u05D7 \u05D1\u05D9\u05E0\u05D5\u05E0\u05D9"           // רווח בינוני
                     :              "\u05E8\u05D5\u05D5\u05D7 \u05E6\u05E8, \u05DE\u05D4\u05D9\u05E8";       // רווח צר, מהיר
            case "simonsays":
                return tier == 0 ? "\u05E8\u05E6\u05E3 \u05E7\u05E6\u05E8"   // רצף קצר
                     : tier == 1 ? "\u05E8\u05E6\u05E3 \u05D1\u05D9\u05E0\u05D5\u05E0\u05D9" // רצף בינוני
                     :              "\u05E8\u05E6\u05E3 \u05D0\u05E8\u05D5\u05DA";            // רצף ארוך
            case "bakery":
                return tier == 0 ? "4 \u05E2\u05D5\u05D2\u05D9\u05D5\u05EA"  // 4 עוגיות
                     : tier == 1 ? "6 \u05E2\u05D5\u05D2\u05D9\u05D5\u05EA"  // 6 עוגיות
                     :              "8 \u05E2\u05D5\u05D2\u05D9\u05D5\u05EA"; // 8 עוגיות
            case "sockmatch":
                return tier == 0 ? "4 \u05D6\u05D5\u05D2\u05D5\u05EA \u05D2\u05E8\u05D1\u05D9\u05D9\u05DD"  // 4 זוגות גרביים
                     : tier == 1 ? "6 \u05D6\u05D5\u05D2\u05D5\u05EA"                                          // 6 זוגות
                     :              "10 \u05D6\u05D5\u05D2\u05D5\u05EA";                                         // 10 זוגות
            case "ballmaze":
                return tier == 0 ? "\u05DE\u05D1\u05D5\u05DA \u05E4\u05E9\u05D5\u05D8"           // מבוך פשוט
                     : tier == 1 ? "\u05DE\u05D1\u05D5\u05DA \u05E2\u05DD \u05DE\u05DB\u05E9\u05D5\u05DC\u05D9\u05DD" // מבוך עם מכשולים
                     :              "\u05DE\u05D1\u05D5\u05DA \u05DE\u05D5\u05E8\u05DB\u05D1";   // מבוך מורכב
            case "patterncopy":
                return tier == 0 ? "\u05D3\u05D5\u05D2\u05DE\u05D4 \u05E7\u05D8\u05E0\u05D4"   // דוגמה קטנה
                     : tier == 1 ? "\u05D3\u05D5\u05D2\u05DE\u05D4 \u05D1\u05D9\u05E0\u05D5\u05E0\u05D9\u05EA" // דוגמה בינונית
                     :              "\u05D3\u05D5\u05D2\u05DE\u05D4 \u05D2\u05D3\u05D5\u05DC\u05D4"; // דוגמה גדולה
            case "numbermaze":
                return tier == 0 ? "\u05DC\u05D5\u05D7 \u05E7\u05D8\u05DF"   // לוח קטן
                     : tier == 1 ? "\u05DC\u05D5\u05D7 \u05D1\u05D9\u05E0\u05D5\u05E0\u05D9" // לוח בינוני
                     :              "\u05DC\u05D5\u05D7 \u05D2\u05D3\u05D5\u05DC"; // לוח גדול
            case "connectmatch":
                return tier == 0 ? "\u05DE\u05E1\u05DC\u05D5\u05DC \u05E7\u05E6\u05E8"   // מסלול קצר
                     : tier == 1 ? "\u05DE\u05E1\u05DC\u05D5\u05DC \u05D1\u05D9\u05E0\u05D5\u05E0\u05D9" // מסלול בינוני
                     :              "\u05DE\u05E1\u05DC\u05D5\u05DC \u05D0\u05E8\u05D5\u05DA"; // מסלול ארוך
            case "towerbuilder":
                return tier == 0 ? "3-4 \u05DC\u05D1\u05E0\u05D9\u05DD"   // 3-4 לבנים
                     : tier == 1 ? "6-8 \u05DC\u05D1\u05E0\u05D9\u05DD"   // 6-8 לבנים
                     :              "10+ \u05DC\u05D1\u05E0\u05D9\u05DD";  // 10+ לבנים
            default:
                return tier == 0 ? "\u05E7\u05DC" : tier == 1 ? "\u05D1\u05D9\u05E0\u05D5\u05E0\u05D9" : "\u05E7\u05E9\u05D4"; // קל/בינוני/קשה
        }
    }

    private void ChangeDifficultyFull(string gameId, int delta,
        TextMeshProUGUI displayTMP, TextMeshProUGUI modeTMP, TextMeshProUGUI finalValTMP,
        Transform card, Transform scrollContent)
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        var gp = profile.analytics.GetOrCreateGame(gameId);

        // Preserve lastAutoDifficulty before first manual change
        if (!gp.manualDifficultyOverride)
            gp.lastAutoDifficulty = gp.currentDifficulty;

        int newDiff = Mathf.Clamp(gp.currentDifficulty + delta, 1, 10);
        if (newDiff == gp.currentDifficulty) return;

        gp.currentDifficulty = newDiff;
        gp.manualDifficultyOverride = true;
        gp.consecutiveStrongResults = 0;
        gp.consecutiveWeakResults = 0;
        ProfileManager.Instance.Save();

        displayTMP.text = $"{newDiff}";
        HebrewText.SetText(modeTMP, "\u05D9\u05D3\u05E0\u05D9"); // ידני
        modeTMP.color = AccentOrange;

        // Update final value label
        if (finalValTMP != null)
        {
            string variantLabel = GameRecommendationService.GetVariantLabel(gameId, newDiff);
            string finalPrefix = "\u05D4\u05E2\u05E8\u05DA \u05D1\u05E4\u05D5\u05E2\u05DC:"; // הערך בפועל:
            HebrewText.SetText(finalValTMP, $"{finalPrefix} {variantLabel}");
        }

        // Sync stale _data entry so re-selecting shows correct difficulty
        SyncGameDifficulty(gameId, newDiff, true);

        LayoutRebuilder.ForceRebuildLayoutImmediate(card.GetComponent<RectTransform>());
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent.GetComponent<RectTransform>());
    }

    private void ResetDifficultyOverride(string gameId,
        TextMeshProUGUI displayTMP, TextMeshProUGUI modeTMP, TextMeshProUGUI finalValTMP,
        GameObject resetBtnGO, GameObject recCardGO,
        Transform card, Transform scrollContent)
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        var gp = profile.analytics.GetOrCreateGame(gameId);
        gp.manualDifficultyOverride = false;
        gp.consecutiveStrongResults = 0;
        gp.consecutiveWeakResults = 0;

        // Restore to last auto difficulty if available
        if (gp.lastAutoDifficulty > 0)
            gp.currentDifficulty = gp.lastAutoDifficulty;

        ProfileManager.Instance.Save();

        displayTMP.text = $"{gp.currentDifficulty}";
        HebrewText.SetText(modeTMP, "\u05D0\u05D5\u05D8\u05D5\u05DE\u05D8\u05D9"); // אוטומטי
        modeTMP.color = HexColor("#A5D6A7"); // light green on card bg

        // Update final value label
        if (finalValTMP != null)
        {
            string variantLabel = GameRecommendationService.GetVariantLabel(gameId, gp.currentDifficulty);
            string finalPrefix = "\u05D4\u05E2\u05E8\u05DA \u05D1\u05E4\u05D5\u05E2\u05DC:"; // הערך בפועל:
            HebrewText.SetText(finalValTMP, $"{finalPrefix} {variantLabel}");
        }

        // Hide the recommended card and reset button
        if (resetBtnGO != null) resetBtnGO.SetActive(false);
        if (recCardGO != null) recCardGO.SetActive(false);

        // Sync stale _data entry
        SyncGameDifficulty(gameId, gp.currentDifficulty, false);

        LayoutRebuilder.ForceRebuildLayoutImmediate(card.GetComponent<RectTransform>());
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent.GetComponent<RectTransform>());
    }

    // ═══════════════════════════════════════════════════════════════
    //  ACCESS CONTROL
    // ═══════════════════════════════════════════════════════════════

    private void OnAccessModeChanged(string gameId, ParentGameAccessMode newMode,
        Button autoBtn, Button onBtn, Button offBtn,
        Image visChipImg, TextMeshProUGUI visChipTMP, TextMeshProUGUI explanationTMP,
        Transform card, Transform scrollContent)
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        // Prevent disabling the last visible game
        if (newMode == ParentGameAccessMode.ForcedDisabled && WouldLeaveZeroVisibleGames(gameId)) return;

        // Update override & persist
        GameVisibilityService.SetOverride(profile, gameId, newMode);
        ProfileManager.Instance.Save();

        // Recompute visibility
        GameItemData gameItem = FindGameItemFromDb(gameId);
        var evalResult = gameItem != null
            ? GameVisibilityService.Evaluate(profile, gameItem)
            : new GameVisibilityResult(false, VisibilityReasonCode.Hidden_MissingData, VisibilitySource.MissingData);
        bool nowVisible = evalResult.isVisible;
        FirebaseAnalyticsManager.LogGameVisibilityChanged(gameId,
            ParentDashboardViewModel.GetGameName(gameId), nowVisible);

        // Update toggle button states in settings panel
        UpdateToggleButton(autoBtn, newMode == ParentGameAccessMode.Default);
        UpdateToggleButton(onBtn, newMode == ParentGameAccessMode.ForcedEnabled);
        UpdateToggleButton(offBtn, newMode == ParentGameAccessMode.ForcedDisabled);

        // Update visibility chip (panel uses semi-transparent tints on card bg)
        visChipImg.color = nowVisible
            ? new Color(1f, 1f, 1f, 0.25f)
            : new Color(1f, 0.3f, 0.3f, 0.25f);
        HebrewText.SetText(visChipTMP,
            nowVisible
                ? H("\u05E4\u05E2\u05D9\u05DC")    // פעיל
                : H("\u05DE\u05D5\u05E1\u05EA\u05E8")); // מוסתר
        visChipTMP.color = nowVisible ? Color.white : new Color(1f, 0.85f, 0.85f);

        // Update explanation
        var rec = gameItem != null
            ? GameRecommendationService.GetRecommendation(profile, gameItem)
            : null;
        string explanation = rec != null
            ? ParentDashboardViewModel.GetExplanationLabel(rec.accessExplanation)
            : ParentDashboardViewModel.GetVisibilityReasonLabel(evalResult.reasonCode);
        HebrewText.SetText(explanationTMP, explanation);

        LayoutRebuilder.ForceRebuildLayoutImmediate(card.GetComponent<RectTransform>());
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent.GetComponent<RectTransform>());

        // ── Sync stale _data and grid card indicators ──
        SyncGameDataAndGridCard(gameId, newMode, nowVisible);
    }

    /// <summary>
    /// After a visibility change, updates the stale _data entry and refreshes
    /// the corresponding grid card's status indicator so the grid stays in sync.
    /// </summary>
    private void SyncGameDataAndGridCard(string gameId, ParentGameAccessMode newMode, bool nowVisible)
    {
        if (_data == null) return;

        // Find and update the stale data entry
        int cardIndex = -1;
        for (int i = 0; i < _data.games.Count; i++)
        {
            if (_data.games[i].gameId == gameId)
            {
                _data.games[i].visibilityMode = newMode;
                _data.games[i].systemVisibility = nowVisible;
                if (_data.games[i].recommendation != null)
                {
                    _data.games[i].recommendation.accessOverrideMode = newMode;
                    _data.games[i].recommendation.finalVisible = nowVisible;
                }
                cardIndex = i;
                break;
            }
        }

        // Update the grid card's status indicator
        if (cardIndex < 0) return;
        var gridContainer = tabContents != null && tabContents.Length > 1
            ? tabContents[1] : null;
        if (gridContainer == null) return;

        // Find the card's StatusIndicator and its icon
        var cardName = $"GameCard_{gameId}";
        Transform cardTransform = null;

        // Search through the grid hierarchy: tabContent → SplitLayout → GridContainer → GridScroll → GridContent → GamesGrid → cards
        var splitLayout = gridContainer.childCount > 0 ? gridContainer.GetChild(0) : null;
        if (splitLayout == null) return;
        var gridContainerT = splitLayout.childCount > 0 ? splitLayout.GetChild(0) : null;
        if (gridContainerT == null) return;

        // Traverse: GridContainer → GridScroll → GridContent → GamesGrid
        Transform gamesGrid = null;
        var grids = gridContainerT.GetComponentsInChildren<GridLayoutGroup>();
        if (grids.Length > 0)
            gamesGrid = grids[0].transform;
        if (gamesGrid == null) return;

        for (int i = 0; i < gamesGrid.childCount; i++)
        {
            if (gamesGrid.GetChild(i).name == cardName)
            {
                cardTransform = gamesGrid.GetChild(i);
                break;
            }
        }
        if (cardTransform == null) return;

        // Update the StatusIndicator circle color and icon
        var indicator = cardTransform.Find("StatusIndicator");
        if (indicator != null)
        {
            var indImg = indicator.GetComponent<Image>();
            if (indImg != null)
                indImg.color = nowVisible ? HexColor("#4CAF50") : HexColor("#BDBDBD");

            var indIcon = indicator.Find("IndIcon");
            if (indIcon != null)
            {
                var iiImg = indIcon.GetComponent<Image>();
                if (iiImg != null)
                    iiImg.sprite = nowVisible ? uiCheckIcon : uiMinus;
            }
        }

        // Update the active games counter
        UpdateGamesCounter();
    }

    private void UpdateGamesCounter()
    {
        if (_gamesCounterTMP == null || _data == null) return;
        int activeCount = 0;
        int totalCount = _data.games.Count;
        foreach (var g in _data.games)
        {
            bool vis = g.recommendation != null ? g.recommendation.finalVisible : g.systemVisibility;
            if (vis) activeCount++;
        }
        HebrewText.SetText(_gamesCounterTMP,
            H($"{activeCount} \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05E4\u05E2\u05D9\u05DC\u05D9\u05DD \u05DE\u05EA\u05D5\u05DA {totalCount}"));
    }

    /// <summary>Syncs stale _data difficulty fields after a manual change.</summary>
    private void SyncGameDifficulty(string gameId, int newDifficulty, bool isManual)
    {
        if (_data == null) return;
        for (int i = 0; i < _data.games.Count; i++)
        {
            if (_data.games[i].gameId == gameId)
            {
                _data.games[i].currentDifficulty = newDifficulty;
                _data.games[i].manualDifficultyOverride = isManual;
                if (_data.games[i].recommendation != null)
                    _data.games[i].recommendation.finalDifficulty = newDifficulty;
                break;
            }
        }
        FirebaseAnalyticsManager.LogDifficultyChanged(gameId,
            ParentDashboardViewModel.GetGameName(gameId), newDifficulty, isManual);
    }

    private GameItemData FindGameItemFromDb(string gameId)
    {
        // Use the serialized gameDatabase field first (most reliable at runtime)
        GameDatabase gameDb = gameDatabase;
        if (gameDb == null)
            gameDb = Resources.Load<GameDatabase>("GameDatabase");
        if (gameDb == null)
        {
            var dbs = Resources.FindObjectsOfTypeAll<GameDatabase>();
            if (dbs.Length > 0) gameDb = dbs[0];
        }
        if (gameDb == null || gameDb.games == null) return null;
        foreach (var g in gameDb.games)
            if (g != null && g.id == gameId) return g;
        return null;
    }

    /// <summary>
    /// Returns true if disabling this game would leave zero visible games.
    /// Used to prevent the parent from disabling ALL games.
    /// </summary>
    private bool WouldLeaveZeroVisibleGames(string gameIdToDisable)
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null || gameDatabase == null) return false;

        int visibleCount = 0;
        foreach (var g in gameDatabase.games)
        {
            if (g == null || g.id == gameIdToDisable) continue;
            var result = GameVisibilityService.Evaluate(profile, g);
            if (result.isVisible) visibleCount++;
        }
        return visibleCount == 0;
    }

    // ── Story helpers ──

    private void ShowEmptySection(Transform parent, string title, string detail)
    {
        var card = MakeCard(parent);
        MakeSectionTitle(card, title);
        AddChildTMP(card, H(detail), 20, NeedDataColor, TextAlignmentOptions.Right)
            .gameObject.AddComponent<LayoutElement>().preferredHeight = 38;
        FitCard(card);
    }

    private void BuildInsightGroup(Transform parent, string title, List<string> items, Color bg, Color titleColor)
    {
        if (items == null || items.Count == 0) return;
        var card = MakeInlineCard(parent, bg);
        card.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(25, 25, 18, 18);
        card.GetComponent<VerticalLayoutGroup>().spacing = 8;
        var tTMP = AddChildTMP(card.transform, H(title), 24, titleColor, TextAlignmentOptions.Right);
        tTMP.fontStyle = FontStyles.Bold;
        tTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 43;
        foreach (var item in items)
            AddChildTMP(card.transform, H(item), 20, TextDark, TextAlignmentOptions.Right)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 35;
        FitCard(card.transform);
    }

    private void AddBulletLine(Transform parent, string text, Color color)
    {
        var row = MakeHRow(parent, 30, TextAnchor.MiddleRight);
        AddChildTMP(row.transform, "\u2022", 20, color, TextAlignmentOptions.Center)
            .gameObject.AddComponent<LayoutElement>().preferredWidth = 24;
        AddChildTMP(row.transform, H(text), 22, TextDark, TextAlignmentOptions.Right)
            .gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
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
        layout.spacing = 10;
        layout.padding = new RectOffset(25, 25, 20, 20);
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
        layout.spacing = 5;
        layout.padding = new RectOffset(15, 15, 13, 13);
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
        layout.spacing = 10;
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
        layout.spacing = 3;
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
        HebrewText.SetText(tmp, text);
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    private void AddMiniStat(Transform parent, string value, string label)
    {
        var col = MakeVCol(parent);
        col.AddComponent<LayoutElement>().flexibleWidth = 1;
        col.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;

        var valTMP = AddChildTMP(col.transform, value, 14, TextDark, TextAlignmentOptions.Center);
        valTMP.fontStyle = FontStyles.Bold;
        valTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;

        AddChildTMP(col.transform, label, 10, TextLight, TextAlignmentOptions.Center)
            .gameObject.AddComponent<LayoutElement>().preferredHeight = 19;
    }

    private void MakeSectionDivider(Transform parent, string rawHebrew)
    {
        MakeSpacer(parent, 8f);
        var tmp = AddChildTMP(parent, H(rawHebrew), 38, TextDark, TextAlignmentOptions.Right);
        tmp.fontStyle = FontStyles.Bold;
        tmp.gameObject.AddComponent<LayoutElement>().preferredHeight = 68;
    }

    private void MakeSectionTitle(Transform parent, string rawHebrew)
    {
        var tmp = AddChildTMP(parent, H(rawHebrew), 36, TextDark, TextAlignmentOptions.Right);
        tmp.fontStyle = FontStyles.Bold;
        tmp.gameObject.AddComponent<LayoutElement>().preferredHeight = 65;
    }

    private void MakeStatRow(Transform parent, string rawLabel, string value)
    {
        var row = MakeHRow(parent, 30, TextAnchor.MiddleRight);
        row.GetComponent<HorizontalLayoutGroup>().spacing = 10;

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
        layout.spacing = 3;
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var valTMP = AddChildTMP(go.transform, value, 38, TextDark, TextAlignmentOptions.Center);
        valTMP.fontStyle = FontStyles.Bold;
        valTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 65;

        var lblTMP = AddChildTMP(go.transform, H(rawLabel), 24, TextMedium, TextAlignmentOptions.Center);
        lblTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 46;
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
        var go = new GameObject("CatCard");
        go.transform.SetParent(parent, false);

        // Card background
        var cardImg = go.AddComponent<Image>();
        if (roundedRect != null) { cardImg.sprite = roundedRect; cardImg.type = Image.Type.Sliced; }
        cardImg.color = HexColor("#F8F9FA");
        cardImg.raycastTarget = false;

        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.padding = new RectOffset(10, 10, 13, 13);

        // Category name on top
        var nmTMP = AddChildTMP(go.transform, H(cat.categoryName), 20, TextDark, TextAlignmentOptions.Center);
        nmTMP.fontStyle = FontStyles.Bold;
        nmTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 38;

        // Score circle below (fixed 50x50 inside a centered container)
        var circContainer = new GameObject("CircleContainer");
        circContainer.transform.SetParent(go.transform, false);
        circContainer.AddComponent<LayoutElement>().preferredHeight = 73;

        var circGO = new GameObject("ScoreCircle");
        circGO.transform.SetParent(circContainer.transform, false);
        var circRT = circGO.AddComponent<RectTransform>();
        circRT.anchorMin = new Vector2(0.5f, 0.5f);
        circRT.anchorMax = new Vector2(0.5f, 0.5f);
        circRT.pivot = new Vector2(0.5f, 0.5f);
        circRT.sizeDelta = new Vector2(50, 50);
        var circImg = circGO.AddComponent<Image>();
        if (circleSprite != null) circImg.sprite = circleSprite;
        circImg.color = cat.color;
        circImg.raycastTarget = false;
        circImg.preserveAspect = true;
        var circTMP = AddChildTMP(circGO.transform, $"{cat.score:F0}", 20, Color.white, TextAlignmentOptions.Center);
        circTMP.fontStyle = FontStyles.Bold;
    }

    private void MakeMiniChip(Transform parent, CategoryDashboardData cat, Color accentColor)
    {
        var row = MakeHRow(parent, 42, TextAnchor.MiddleRight);
        row.GetComponent<HorizontalLayoutGroup>().spacing = 13;

        var scTMP = AddChildTMP(row.transform, $"{cat.score:F0}", 28, accentColor, TextAlignmentOptions.Center);
        scTMP.fontStyle = FontStyles.Bold;
        scTMP.gameObject.AddComponent<LayoutElement>().preferredWidth = 50;

        var nmTMP = AddChildTMP(row.transform, H(cat.categoryName), 28, TextDark, TextAlignmentOptions.Right);
        nmTMP.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
    }

    private Button MakeToggleButton(Transform parent, string label, bool active)
    {
        var go = new GameObject("Toggle");
        go.transform.SetParent(parent, false);
        go.AddComponent<LayoutElement>().flexibleWidth = 1;

        var img = go.AddComponent<Image>();
        img.sprite = roundedRect;
        img.type = Image.Type.Sliced;
        img.color = active ? Primary : HexColor("#E8EBED");

        var tmp = AddChildTMP(go.transform, label, 13,
            active ? Color.white : TextMedium, TextAlignmentOptions.Center);
        tmp.fontStyle = FontStyles.Bold;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        return btn;
    }

    private void UpdateToggleButton(Button btn, bool active)
    {
        var img = btn.GetComponent<Image>();
        if (img != null)
            img.color = active ? Color.white : new Color(1f, 1f, 1f, 0.15f);

        var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
            tmp.color = active ? Primary : new Color(1f, 1f, 1f, 0.7f);
    }

    private Button MakeSmallButton(Transform parent, string label, int fontSize)
    {
        var go = new GameObject("Btn");
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = 42;
        le.preferredHeight = 57;

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
        headerLayout.padding = new RectOffset(20, 20, 10, 10);
        headerLayout.spacing = 16;
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
            trLE.preferredHeight = 49;
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
        contentLayout.spacing = 26;
        contentLayout.padding = new RectOffset(20, 20, 20, 38);
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
        cardLayout.spacing = 8;
        cardLayout.padding = new RectOffset(20, 20, 18, 18);

        // Game title
        var titleTMP = AddChildTMP(card, H(gameBoard.gameName), 20, TextDark, TextAlignmentOptions.Right);
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 41;

        MakeDivider(card);

        // Column headers
        var headerRow = MakeHRow(card, 24, TextAnchor.MiddleRight);
        headerRow.GetComponent<HorizontalLayoutGroup>().spacing = 8;
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
        rowLayout.spacing = 8;
        rowLayout.padding = new RectOffset(10, 10, 6, 6);
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
        nameTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 27;

        if (entry.isCurrentProfile)
        {
            var badgeTMP = AddChildTMP(nameCol.transform,
                H("\u05D4\u05E4\u05E8\u05D5\u05E4\u05D9\u05DC \u05D4\u05E0\u05D5\u05DB\u05D7\u05D9"), // הפרופיל הנוכחי
                10, Primary, TextAlignmentOptions.Right);
            badgeTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 19;
        }
        else if (!entry.hasPlayedGame)
        {
            var statusTMP = AddChildTMP(nameCol.transform,
                H("\u05E2\u05D3\u05D9\u05D9\u05DF \u05DC\u05D0 \u05E9\u05D9\u05D7\u05E7"), // עדיין לא שיחק
                10, TextLight, TextAlignmentOptions.Right);
            statusTMP.fontStyle = FontStyles.Italic;
            statusTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 19;
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

    // ═══════════════════════════════════════════════════════════════
    //  SETTINGS POPUP
    // ═══════════════════════════════════════════════════════════════

    private void ShowSettings()
    {
        if (_settingsModal != null) return;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        // ── Modal overlay ──
        _settingsModal = new GameObject("SettingsModal");
        _settingsModal.transform.SetParent(canvas.transform, false);
        var modalRT = _settingsModal.AddComponent<RectTransform>();
        modalRT.anchorMin = Vector2.zero;
        modalRT.anchorMax = Vector2.one;
        modalRT.offsetMin = Vector2.zero;
        modalRT.offsetMax = Vector2.zero;

        var dimImg = _settingsModal.AddComponent<Image>();
        dimImg.color = new Color(0, 0, 0, 0.5f);
        dimImg.raycastTarget = true;

        // Tap dim area to close
        var dimBtn = _settingsModal.AddComponent<Button>();
        dimBtn.targetGraphic = dimImg;
        dimBtn.onClick.AddListener(CloseSettings);

        // ── Center card ──
        var cardGO = new GameObject("Card");
        cardGO.transform.SetParent(_settingsModal.transform, false);
        var cardRT = cardGO.AddComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.5f, 0.05f);
        cardRT.anchorMax = new Vector2(0.5f, 0.95f);
        cardRT.pivot = new Vector2(0.5f, 0.5f);
        cardRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 620);

        var cardImg = cardGO.AddComponent<Image>();
        if (roundedRect != null) { cardImg.sprite = roundedRect; cardImg.type = Image.Type.Sliced; }
        cardImg.color = CardColor;
        cardImg.raycastTarget = true; // block tap-through to dim

        var cardLayout = cardGO.AddComponent<VerticalLayoutGroup>();
        cardLayout.spacing = 0;
        cardLayout.padding = new RectOffset(2, 2, 2, 2);
        cardLayout.childForceExpandWidth = true;
        cardLayout.childForceExpandHeight = false;
        cardLayout.childControlWidth = true;
        cardLayout.childControlHeight = true;

        // ── Header ──
        var headerGO = new GameObject("Header");
        headerGO.transform.SetParent(cardGO.transform, false);
        headerGO.AddComponent<RectTransform>();
        var headerImg = headerGO.AddComponent<Image>();
        if (roundedRect != null) { headerImg.sprite = roundedRect; headerImg.type = Image.Type.Sliced; }
        headerImg.color = HexColor("#2C3E50");
        var headerLE = headerGO.AddComponent<LayoutElement>();
        headerLE.preferredHeight = 52;
        headerLE.flexibleHeight = 0; // do NOT expand

        var headerLayout = headerGO.AddComponent<HorizontalLayoutGroup>();
        headerLayout.padding = new RectOffset(16, 16, 4, 4);
        headerLayout.spacing = 12;
        headerLayout.childAlignment = TextAnchor.MiddleCenter;
        headerLayout.childForceExpandWidth = false;
        headerLayout.childForceExpandHeight = true;
        headerLayout.childControlWidth = true;
        headerLayout.childControlHeight = false;

        // Close button
        var closeBtnGO = new GameObject("CloseBtn");
        closeBtnGO.transform.SetParent(headerGO.transform, false);
        closeBtnGO.AddComponent<RectTransform>();
        var closeTMP = closeBtnGO.AddComponent<TextMeshProUGUI>();
        closeTMP.text = "\u2715"; // ✕
        closeTMP.fontSize = 28;
        closeTMP.color = Color.white;
        closeTMP.alignment = TextAlignmentOptions.Center;
        closeBtnGO.AddComponent<LayoutElement>().preferredWidth = 40;
        var closeBtn = closeBtnGO.AddComponent<Button>();
        closeBtn.targetGraphic = closeTMP;
        closeBtn.onClick.AddListener(CloseSettings);

        // Title (flexible center)
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(headerGO.transform, false);
        titleGO.AddComponent<RectTransform>();
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05D4\u05D2\u05D3\u05E8\u05D5\u05EA"); // הגדרות
        titleTMP.fontSize = 24;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;
        titleGO.AddComponent<LayoutElement>().flexibleWidth = 1;

        // Spacer (balance close button)
        var spacerGO = new GameObject("Spacer");
        spacerGO.transform.SetParent(headerGO.transform, false);
        spacerGO.AddComponent<RectTransform>();
        spacerGO.AddComponent<LayoutElement>().preferredWidth = 40;

        // ── Scrollable content area ──
        var scrollGO = new GameObject("Scroll");
        scrollGO.transform.SetParent(cardGO.transform, false);
        var scrollRT = scrollGO.AddComponent<RectTransform>();
        scrollGO.AddComponent<Image>().color = Color.clear;
        scrollGO.GetComponent<Image>().raycastTarget = true;
        scrollGO.AddComponent<RectMask2D>();
        var scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true;
        scrollGO.AddComponent<LayoutElement>().flexibleHeight = 1;

        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(scrollGO.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1); contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.sizeDelta = Vector2.zero;
        var contentLayout = contentGO.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 0;
        contentLayout.padding = new RectOffset(30, 30, 20, 20);
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = contentRT;

        // ── Profile actions ──
        MakeSettingsActionButton(contentGO.transform,
            H("\u05D4\u05D7\u05DC\u05E4\u05EA \u05E4\u05E8\u05D5\u05E4\u05D9\u05DC"), // החלפת פרופיל
            H("\u05DE\u05E2\u05D1\u05E8 \u05DC\u05DE\u05E1\u05DA \u05D1\u05D7\u05D9\u05E8\u05EA \u05D3\u05DE\u05D5\u05EA"), // מעבר למסך בחירת דמות
            () => { CloseSettings(); NavigationManager.GoToProfileSelection(); });

        MakeSettingsDivider(contentGO.transform);

        MakeSettingsActionButton(contentGO.transform,
            H("\u05E9\u05D9\u05E0\u05D5\u05D9 \u05E9\u05DD \u05D4\u05D9\u05DC\u05D3"), // שינוי שם הילד
            H("\u05E2\u05E8\u05D9\u05DB\u05EA \u05E9\u05DD \u05D4\u05EA\u05E6\u05D5\u05D2\u05D4 \u05E9\u05DC \u05D4\u05E4\u05E8\u05D5\u05E4\u05D9\u05DC"), // עריכת שם התצוגה של הפרופיל
            () => ShowRenameChildDialog());

        MakeSettingsDivider(contentGO.transform);

        // ── Toggles ──
        // Music toggle
        MakeSettingsToggle(contentGO.transform,
            H("\u05DE\u05D5\u05D6\u05D9\u05E7\u05D4"), // מוזיקה
            H("\u05DE\u05D5\u05D6\u05D9\u05E7\u05EA \u05E8\u05E7\u05E2"), // מוזיקת רקע
            AppSettings.MusicEnabled,
            val => AppSettings.MusicEnabled = val);

        MakeSettingsDivider(contentGO.transform);

        // Voice toggle
        MakeSettingsToggle(contentGO.transform,
            H("\u05E7\u05D5\u05DC \u05D0\u05DC\u05D9\u05DF"), // קול אלין
            H("\u05E9\u05DE\u05D5\u05EA \u05D7\u05D9\u05D5\u05EA, \u05DE\u05E9\u05D5\u05D1\u05D9\u05DD"), // שמות חיות, משובים
            AppSettings.VoiceEnabled,
            val => AppSettings.VoiceEnabled = val);

        MakeSettingsDivider(contentGO.transform);

        // Sticker tree notifications toggle
        MakeSettingsToggle(contentGO.transform,
            H("\u05D4\u05EA\u05E8\u05D0\u05D5\u05EA \u05E2\u05E5 \u05D4\u05DE\u05D3\u05D1\u05E7\u05D5\u05EA"), // התראות עץ המדבקות
            H("\u05EA\u05D6\u05DB\u05D5\u05E8\u05EA \u05DB\u05E9\u05DE\u05D3\u05D1\u05E7\u05D4 \u05D7\u05D3\u05E9\u05D4 \u05DE\u05D5\u05DB\u05E0\u05D4 \u05DC\u05D0\u05D9\u05E1\u05D5\u05E3 )\u05DB\u05DC 6 \u05E9\u05E2\u05D5\u05EA("), // תזכורת כשמדבקה חדשה מוכנה לאיסוף )כל 6 שעות( — reversed parens for RTL
            AppSettings.NotificationsEnabled,
            val => AppSettings.NotificationsEnabled = val);
    }

    private void MakeSettingsToggle(Transform parent, string title, string subtitle,
        bool currentValue, System.Action<bool> onChanged)
    {
        var rowGO = new GameObject("ToggleRow");
        rowGO.transform.SetParent(parent, false);
        rowGO.AddComponent<RectTransform>();
        var rowLayout = rowGO.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 16;
        rowLayout.padding = new RectOffset(2, 2, 13, 13);
        rowLayout.childAlignment = TextAnchor.MiddleRight;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = false;
        rowGO.AddComponent<LayoutElement>().preferredHeight = 95;

        // Toggle (right side in RTL = first in hierarchy)
        var toggleGO = new GameObject("Toggle");
        toggleGO.transform.SetParent(rowGO.transform, false);
        toggleGO.AddComponent<RectTransform>();

        var toggleBgGO = new GameObject("Background");
        toggleBgGO.transform.SetParent(toggleGO.transform, false);
        var toggleBgRT = toggleBgGO.AddComponent<RectTransform>();
        toggleBgRT.anchorMin = toggleBgRT.anchorMax = new Vector2(0.5f, 0.5f);
        toggleBgRT.sizeDelta = new Vector2(80, 42);
        var toggleBgImg = toggleBgGO.AddComponent<Image>();
        if (roundedRect != null) { toggleBgImg.sprite = roundedRect; toggleBgImg.type = Image.Type.Sliced; }
        toggleBgImg.color = currentValue ? Primary : BarBg;
        toggleBgImg.raycastTarget = true;

        var knobGO = new GameObject("Knob");
        knobGO.transform.SetParent(toggleBgGO.transform, false);
        var knobRT = knobGO.AddComponent<RectTransform>();
        knobRT.anchorMin = knobRT.anchorMax = new Vector2(currentValue ? 0.8f : 0.2f, 0.5f);
        knobRT.sizeDelta = new Vector2(32, 32);
        var knobImg = knobGO.AddComponent<Image>();
        if (circleSprite != null) knobImg.sprite = circleSprite;
        knobImg.color = Color.white;
        knobImg.raycastTarget = false;

        toggleGO.AddComponent<LayoutElement>().preferredWidth = 60;

        // Click handler
        var toggleBtn = toggleBgGO.AddComponent<Button>();
        toggleBtn.targetGraphic = toggleBgImg;
        bool state = currentValue;
        toggleBtn.onClick.AddListener(() =>
        {
            state = !state;
            onChanged(state);
            toggleBgImg.color = state ? Primary : BarBg;
            knobRT.anchorMin = knobRT.anchorMax = new Vector2(state ? 0.8f : 0.2f, 0.5f);
        });

        // Text column (fills remaining space)
        var textCol = new GameObject("TextCol");
        textCol.transform.SetParent(rowGO.transform, false);
        textCol.AddComponent<RectTransform>();
        var textLayout = textCol.AddComponent<VerticalLayoutGroup>();
        textLayout.spacing = 3;
        textLayout.childAlignment = TextAnchor.MiddleRight;
        textLayout.childForceExpandWidth = true;
        textLayout.childForceExpandHeight = false;
        textLayout.childControlWidth = true;
        textLayout.childControlHeight = true;
        textCol.AddComponent<LayoutElement>().flexibleWidth = 1;

        var titleText = new GameObject("Title");
        titleText.transform.SetParent(textCol.transform, false);
        titleText.AddComponent<RectTransform>();
        var titleTMP = titleText.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, title);
        titleTMP.fontSize = 28;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = TextDark;
        titleTMP.alignment = TextAlignmentOptions.Right;
        titleTMP.raycastTarget = false;
        titleText.AddComponent<LayoutElement>().preferredHeight = 38;

        var subtitleText = new GameObject("Subtitle");
        subtitleText.transform.SetParent(textCol.transform, false);
        subtitleText.AddComponent<RectTransform>();
        var subTMP = subtitleText.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(subTMP, subtitle);
        subTMP.fontSize = 21;
        subTMP.color = TextMedium;
        subTMP.alignment = TextAlignmentOptions.Right;
        subTMP.raycastTarget = false;
        subtitleText.AddComponent<LayoutElement>().preferredHeight = 30;
    }

    private void MakeSettingsDivider(Transform parent)
    {
        var divGO = new GameObject("Divider");
        divGO.transform.SetParent(parent, false);
        divGO.AddComponent<RectTransform>();
        var divImg = divGO.AddComponent<Image>();
        divImg.color = Divider;
        divImg.raycastTarget = false;
        divGO.AddComponent<LayoutElement>().preferredHeight = 1;
    }

    private void MakeSettingsActionButton(Transform parent, string title, string subtitle, System.Action onClick)
    {
        var rowGO = new GameObject("ActionRow");
        rowGO.transform.SetParent(parent, false);
        rowGO.AddComponent<RectTransform>();
        var rowLayout = rowGO.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 16;
        rowLayout.padding = new RectOffset(2, 2, 13, 13);
        rowLayout.childAlignment = TextAnchor.MiddleRight;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = false;
        rowGO.AddComponent<LayoutElement>().preferredHeight = 85;

        // Arrow icon (left in RTL = first in hierarchy)
        var arrowGO = new GameObject("Arrow");
        arrowGO.transform.SetParent(rowGO.transform, false);
        arrowGO.AddComponent<RectTransform>();
        var arrowTMP = arrowGO.AddComponent<TextMeshProUGUI>();
        arrowTMP.text = "\u25C0"; // ◀
        arrowTMP.fontSize = 24;
        arrowTMP.color = Primary;
        arrowTMP.alignment = TextAlignmentOptions.Center;
        arrowTMP.raycastTarget = false;
        arrowGO.AddComponent<LayoutElement>().preferredWidth = 32;

        // Text column
        var textCol = new GameObject("TextCol");
        textCol.transform.SetParent(rowGO.transform, false);
        textCol.AddComponent<RectTransform>();
        var textLayout = textCol.AddComponent<VerticalLayoutGroup>();
        textLayout.spacing = 3;
        textLayout.childAlignment = TextAnchor.MiddleRight;
        textLayout.childForceExpandWidth = true;
        textLayout.childForceExpandHeight = false;
        textLayout.childControlWidth = true;
        textLayout.childControlHeight = true;
        textCol.AddComponent<LayoutElement>().flexibleWidth = 1;

        var titleText = new GameObject("Title");
        titleText.transform.SetParent(textCol.transform, false);
        titleText.AddComponent<RectTransform>();
        var titleTMP = titleText.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, title);
        titleTMP.fontSize = 26;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Primary;
        titleTMP.alignment = TextAlignmentOptions.Right;
        titleTMP.raycastTarget = false;
        titleText.AddComponent<LayoutElement>().preferredHeight = 34;

        var subtitleText = new GameObject("Subtitle");
        subtitleText.transform.SetParent(textCol.transform, false);
        subtitleText.AddComponent<RectTransform>();
        var subTMP = subtitleText.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(subTMP, subtitle);
        subTMP.fontSize = 19;
        subTMP.color = TextMedium;
        subTMP.alignment = TextAlignmentOptions.Right;
        subTMP.raycastTarget = false;
        subtitleText.AddComponent<LayoutElement>().preferredHeight = 26;

        // Make entire row clickable
        var rowImg = rowGO.AddComponent<Image>();
        rowImg.color = Color.clear;
        rowImg.raycastTarget = true;
        var rowBtn = rowGO.AddComponent<Button>();
        rowBtn.targetGraphic = rowImg;
        rowBtn.onClick.AddListener(() => onClick?.Invoke());
    }

    private void ShowRenameChildDialog()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        // Close settings first
        CloseSettings();

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        // ── Modal overlay ──
        _settingsModal = new GameObject("RenameModal");
        _settingsModal.transform.SetParent(canvas.transform, false);
        var modalRT = _settingsModal.AddComponent<RectTransform>();
        modalRT.anchorMin = Vector2.zero; modalRT.anchorMax = Vector2.one;
        modalRT.offsetMin = Vector2.zero; modalRT.offsetMax = Vector2.zero;

        var dimImg = _settingsModal.AddComponent<Image>();
        dimImg.color = new Color(0, 0, 0, 0.5f);
        dimImg.raycastTarget = true;
        var dimBtn = _settingsModal.AddComponent<Button>();
        dimBtn.targetGraphic = dimImg;
        dimBtn.onClick.AddListener(CloseSettings);

        // ── Dialog card ──
        var cardGO = new GameObject("Card");
        cardGO.transform.SetParent(_settingsModal.transform, false);
        var cardRT = cardGO.AddComponent<RectTransform>();
        cardRT.anchorMin = cardRT.anchorMax = new Vector2(0.5f, 0.5f);
        cardRT.pivot = new Vector2(0.5f, 0.5f);
        cardRT.sizeDelta = new Vector2(500, 300);

        var cardImg = cardGO.AddComponent<Image>();
        if (roundedRect != null) { cardImg.sprite = roundedRect; cardImg.type = Image.Type.Sliced; }
        cardImg.color = CardColor;
        cardImg.raycastTarget = true;

        var cardVL = cardGO.AddComponent<VerticalLayoutGroup>();
        cardVL.spacing = 16;
        cardVL.padding = new RectOffset(30, 30, 24, 24);
        cardVL.childForceExpandWidth = true;
        cardVL.childForceExpandHeight = false;
        cardVL.childControlWidth = true;
        cardVL.childControlHeight = true;
        cardVL.childAlignment = TextAnchor.MiddleCenter;

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(cardGO.transform, false);
        titleGO.AddComponent<RectTransform>();
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, H("\u05E9\u05D9\u05E0\u05D5\u05D9 \u05E9\u05DD")); // שינוי שם
        titleTMP.fontSize = 30;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = TextDark;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;
        titleGO.AddComponent<LayoutElement>().preferredHeight = 40;

        // Input field
        var inputGO = new GameObject("InputField");
        inputGO.transform.SetParent(cardGO.transform, false);
        var inputRT = inputGO.AddComponent<RectTransform>();
        inputGO.AddComponent<LayoutElement>().preferredHeight = 60;

        var inputBg = inputGO.AddComponent<Image>();
        if (roundedRect != null) { inputBg.sprite = roundedRect; inputBg.type = Image.Type.Sliced; }
        inputBg.color = BgColor;

        // Text area for TMP input
        var textAreaGO = new GameObject("TextArea");
        textAreaGO.transform.SetParent(inputGO.transform, false);
        var textAreaRT = textAreaGO.AddComponent<RectTransform>();
        textAreaRT.anchorMin = Vector2.zero; textAreaRT.anchorMax = Vector2.one;
        textAreaRT.offsetMin = new Vector2(12, 4); textAreaRT.offsetMax = new Vector2(-12, -4);
        textAreaGO.AddComponent<RectMask2D>();

        var inputTextGO = new GameObject("Text");
        inputTextGO.transform.SetParent(textAreaGO.transform, false);
        var inputTextRT = inputTextGO.AddComponent<RectTransform>();
        inputTextRT.anchorMin = Vector2.zero; inputTextRT.anchorMax = Vector2.one;
        inputTextRT.offsetMin = Vector2.zero; inputTextRT.offsetMax = Vector2.zero;
        var inputTMP = inputTextGO.AddComponent<TextMeshProUGUI>();
        inputTMP.fontSize = 26;
        inputTMP.color = TextDark;
        inputTMP.alignment = TextAlignmentOptions.Right;
        inputTMP.isRightToLeftText = true;

        var placeholderGO = new GameObject("Placeholder");
        placeholderGO.transform.SetParent(textAreaGO.transform, false);
        var placeholderRT = placeholderGO.AddComponent<RectTransform>();
        placeholderRT.anchorMin = Vector2.zero; placeholderRT.anchorMax = Vector2.one;
        placeholderRT.offsetMin = Vector2.zero; placeholderRT.offsetMax = Vector2.zero;
        var placeholderTMP = placeholderGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(placeholderTMP, H("\u05D4\u05E7\u05DC\u05D3 \u05E9\u05DD \u05D7\u05D3\u05E9")); // הקלד שם חדש
        placeholderTMP.fontSize = 26;
        placeholderTMP.color = TextLight;
        placeholderTMP.alignment = TextAlignmentOptions.Right;
        placeholderTMP.isRightToLeftText = true;

        var inputField = inputGO.AddComponent<TMP_InputField>();
        inputField.textViewport = textAreaRT;
        inputField.textComponent = inputTMP;
        inputField.placeholder = placeholderTMP;
        inputField.text = profile.displayName;
        inputField.characterLimit = 20;
        inputField.contentType = TMP_InputField.ContentType.Standard;

        // Button row
        var btnRow = MakeHRow(cardGO.transform, 50, TextAnchor.MiddleCenter);
        btnRow.GetComponent<HorizontalLayoutGroup>().spacing = 16;
        btnRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;

        // Cancel button
        var cancelGO = new GameObject("CancelBtn");
        cancelGO.transform.SetParent(btnRow.transform, false);
        var cancelImg = cancelGO.AddComponent<Image>();
        if (roundedRect != null) { cancelImg.sprite = roundedRect; cancelImg.type = Image.Type.Sliced; }
        cancelImg.color = BarBg;
        cancelGO.AddComponent<LayoutElement>().flexibleWidth = 1;
        var cancelTMP = AddChildTMP(cancelGO.transform, H("\u05D1\u05D9\u05D8\u05D5\u05DC"), 22, TextDark, TextAlignmentOptions.Center); // ביטול
        var crt = cancelTMP.GetComponent<RectTransform>();
        crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
        crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
        var cancelBtn = cancelGO.AddComponent<Button>();
        cancelBtn.targetGraphic = cancelImg;
        cancelBtn.onClick.AddListener(CloseSettings);

        // Save button
        var saveGO = new GameObject("SaveBtn");
        saveGO.transform.SetParent(btnRow.transform, false);
        var saveImg = saveGO.AddComponent<Image>();
        if (roundedRect != null) { saveImg.sprite = roundedRect; saveImg.type = Image.Type.Sliced; }
        saveImg.color = AccentGreen;
        saveGO.AddComponent<LayoutElement>().flexibleWidth = 1;
        var saveTMP = AddChildTMP(saveGO.transform, H("\u05E9\u05DE\u05D5\u05E8"), 22, Color.white, TextAlignmentOptions.Center); // שמור
        saveTMP.fontStyle = FontStyles.Bold;
        var srt = saveTMP.GetComponent<RectTransform>();
        srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
        srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
        var saveBtn = saveGO.AddComponent<Button>();
        saveBtn.targetGraphic = saveImg;
        saveBtn.onClick.AddListener(() =>
        {
            string newName = inputField.text.Trim();
            if (!string.IsNullOrEmpty(newName) && newName != profile.displayName)
            {
                profile.displayName = newName;
                ProfileManager.Instance.Save();
                // Update header display
                if (headerNameText != null)
                    HebrewText.SetText(headerNameText, profile.displayName);
            }
            CloseSettings();
        });
    }

    private void CloseSettings()
    {
        if (_settingsModal != null)
        {
            Destroy(_settingsModal);
            _settingsModal = null;
        }
    }

    // ── Helpers ──

    private static string H(string raw) => raw;

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }

    private void OnDestroy()
    {
        foreach (var tex in _galleryTextures)
            if (tex != null) Destroy(tex);
        _galleryTextures.Clear();

        if (_settingsModal != null) Destroy(_settingsModal);
        if (_leaderboardModal != null) Destroy(_leaderboardModal);
    }
}
