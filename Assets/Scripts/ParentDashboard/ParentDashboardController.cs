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

    [Header("Settings")]
    public Button settingsButton;
    public Sprite gearSprite;

    [Header("Assets")]
    public Sprite roundedRect;
    public Sprite circleSprite;
    public GameDatabase gameDatabase;

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
                question = $"? = {b} + {a}";
                break;
            case 1: // subtraction
                a = Random.Range(20, 50);
                b = Random.Range(5, a - 3);
                _correctAnswer = a - b;
                question = $"? = {b} - {a}";
                break;
            default: // multiplication
                a = Random.Range(3, 10);
                b = Random.Range(3, 10);
                _correctAnswer = a * b;
                question = $"? = {b} × {a}";
                break;
        }

        HebrewText.SetText(questionText, question);

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
        // Show rewarded ad after solving the math gate, then open dashboard
        if (RewardedAdManager.Instance != null)
        {
            RewardedAdManager.Instance.ShowAd(OpenDashboard);
        }
        else
        {
            OpenDashboard();
        }
    }

    private void OpenDashboard()
    {
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
        if (_gameDetailsOverlay != null) Destroy(_gameDetailsOverlay);
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
        barRT.sizeDelta = new Vector2(0, 52);

        // Background
        var barBg = _statsTabBar.AddComponent<Image>();
        barBg.color = HexColor("#FFFFFF");
        _statsTabBar.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.06f);

        // Layout
        var barLayout = _statsTabBar.AddComponent<HorizontalLayoutGroup>();
        barLayout.spacing = 0;
        barLayout.padding = new RectOffset(20, 20, 4, 4);
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

        // Push scroll views down by tab bar height (130 header + 52 tab bar = 182)
        float topOffset = 182;
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

        var img = go.AddComponent<Image>();
        img.color = Color.clear; // transparent bg
        img.raycastTarget = true;

        // Bottom indicator line
        var indicator = new GameObject("Indicator");
        indicator.transform.SetParent(go.transform, false);
        var indRT = indicator.AddComponent<RectTransform>();
        indRT.anchorMin = new Vector2(0.15f, 0);
        indRT.anchorMax = new Vector2(0.85f, 0);
        indRT.pivot = new Vector2(0.5f, 0);
        indRT.sizeDelta = new Vector2(0, 3);
        var indImg = indicator.AddComponent<Image>();
        indImg.color = active ? Primary : Color.clear;
        indImg.raycastTarget = false;
        if (roundedRect != null) { indImg.sprite = roundedRect; indImg.type = Image.Type.Sliced; }

        // Label
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(tmp, label);
        tmp.fontSize = 17;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = active ? Primary : TextMedium;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;

        return go.AddComponent<Button>();
    }

    private void SwitchContentTab(int tab)
    {
        _activeContentTab = tab;

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

        // Update indicator
        var indicator = btn.transform.Find("Indicator");
        if (indicator != null)
        {
            var indImg = indicator.GetComponent<Image>();
            if (indImg != null) indImg.color = active ? Primary : Color.clear;
        }

        // Update label color
        var label = btn.transform.Find("Label");
        if (label != null)
        {
            var tmp = label.GetComponent<TextMeshProUGUI>();
            if (tmp != null) tmp.color = active ? Primary : TextMedium;
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
        addTMP.fontSize = 22;
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
        gridLayout.padding = new RectOffset(8, 8, 8, 8);
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
            emptyTMP.fontSize = 22;
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
        dtTMP.fontSize = 20;
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

        // ── Overall Score Card ──
        var scoreCard = MakeCard(parent);
        var scoreRow = MakeHRow(scoreCard, 150, TextAnchor.MiddleCenter);

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

        // Score info column (no progress bar)
        var scoreInfoGO = MakeVCol(scoreRow.transform);
        var scoreInfoLE = scoreInfoGO.AddComponent<LayoutElement>();
        scoreInfoLE.flexibleWidth = 1;
        scoreInfoLE.preferredHeight = 140;

        var scoreTitleTMP = AddChildTMP(scoreInfoGO.transform, H("\u05E6\u05D9\u05D5\u05DF \u05DB\u05DC\u05DC\u05D9"), // ציון כללי
            36, TextDark, TextAlignmentOptions.Right);
        scoreTitleTMP.fontStyle = FontStyles.Bold;
        scoreTitleTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;

        var statusTMP = AddChildTMP(scoreInfoGO.transform, H(_data.overallScoreLabel),
            28, TextMedium, TextAlignmentOptions.Right);
        statusTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;

        var trendTMP2 = AddChildTMP(scoreInfoGO.transform,
            H($"{ParentDashboardViewModel.TrendArrow(_data.overallTrend)} {_data.overallTrendLabel}"),
            24, TextMedium, TextAlignmentOptions.Right);
        trendTMP2.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;

        FitCard(scoreCard);

        // ── Quick Stats Card ──
        var statsCard = MakeCard(parent);
        MakeSectionTitle(statsCard, "\u05E1\u05D8\u05D8\u05D9\u05E1\u05D8\u05D9\u05E7\u05D5\u05EA"); // סטטיסטיקות

        var statsGrid = new GameObject("StatsGrid");
        statsGrid.transform.SetParent(statsCard, false);
        var gridLayout = statsGrid.AddComponent<GridLayoutGroup>();
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 4;
        gridLayout.cellSize = new Vector2(400, 120);
        gridLayout.spacing = new Vector2(16, 12);
        gridLayout.childAlignment = TextAnchor.UpperCenter;
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperRight;
        var gridLE = statsGrid.AddComponent<LayoutElement>();
        gridLE.preferredHeight = 260;

        MakeStatCell(statsGrid.transform, $"{_data.totalSessions}", "\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD"); // משחקים
        MakeStatCell(statsGrid.transform, H(_data.totalPlayTimeDisplay), "\u05D6\u05DE\u05DF \u05DE\u05E9\u05D7\u05E7 \u05DB\u05D5\u05DC\u05DC"); // זמן משחק כולל
        MakeStatCell(statsGrid.transform, $"{_data.gamesPlayedCount}", "\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05E9\u05D5\u05E0\u05D9\u05DD"); // משחקים שונים
        MakeStatCell(statsGrid.transform, H(_data.favoriteGameName), "\u05DE\u05E9\u05D7\u05E7 \u05D0\u05D4\u05D5\u05D1"); // משחק אהוב
        MakeStatCell(statsGrid.transform, $"{_data.discoveredAnimals}", "\u05D7\u05D9\u05D5\u05EA \u05E9\u05D2\u05D9\u05DC\u05D4"); // חיות שגילה
        MakeStatCell(statsGrid.transform, $"{_data.discoveredColors}", "\u05E6\u05D1\u05E2\u05D9\u05DD \u05E9\u05D2\u05D9\u05DC\u05D4"); // צבעים שגילה
        MakeStatCell(statsGrid.transform, $"{_data.collectedStickers}", "\u05DE\u05D3\u05D1\u05E7\u05D5\u05EA \u05E9\u05E0\u05D0\u05E1\u05E4\u05D5"); // מדבקות שנאספו
        MakeStatCell(statsGrid.transform, H(_data.thisWeekPlayTimeDisplay), "\u05D6\u05DE\u05DF \u05DE\u05E9\u05D7\u05E7 \u05D4\u05E9\u05D1\u05D5\u05E2"); // זמן משחק השבוע
        // מעורבות removed

        FitCard(statsCard);

        // ── Play Distribution Pie Chart ──
        BuildPlayDistributionChart(parent);

        // ── Play Time Distribution Pie Chart ──
        BuildPlayTimeDistributionChart(parent);

        // ═══════════════════════════════════════════════════════════
        //  STORY-DRIVEN SECTIONS (below statistics)
        // ═══════════════════════════════════════════════════════════

        var profile = ProfileManager.ActiveProfile;
        var story = DashboardStoryBuilder.Build(_data, profile != null ? profile.analytics : null);

        // ── Story sections ──
        BuildStorySections(parent, story);
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
        row.GetComponent<HorizontalLayoutGroup>().spacing = 24;
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
        legend.GetComponent<VerticalLayoutGroup>().spacing = 6;
        legend.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperRight;

        int maxLegendItems = played.Count < 8 ? played.Count : 8;
        for (int i = 0; i < maxLegendItems; i++)
        {
            var g = played[i];
            float pct = (float)g.sessionsPlayed / totalSessions * 100f;
            Color sliceColor = PieColors[i % PieColors.Length];

            var legendRow = MakeHRow(legend.transform, 26, TextAnchor.MiddleRight);
            legendRow.GetComponent<HorizontalLayoutGroup>().spacing = 6;

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
            dotLE.preferredHeight = 16;
        }

        // Show "others" if more than 8
        if (played.Count > 8)
        {
            int otherSessions = 0;
            for (int i = 8; i < played.Count; i++)
                otherSessions += played[i].sessionsPlayed;
            float otherPct = (float)otherSessions / totalSessions * 100f;

            var otherRow = MakeHRow(legend.transform, 26, TextAnchor.MiddleRight);
            otherRow.GetComponent<HorizontalLayoutGroup>().spacing = 6;
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
            dotLE2.preferredHeight = 16;
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
        row.GetComponent<HorizontalLayoutGroup>().spacing = 24;
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
        legend.GetComponent<VerticalLayoutGroup>().spacing = 6;
        legend.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperRight;

        for (int i = 0; i < maxSlices; i++)
        {
            var g = played[i];
            float pct = g.totalPlayTime / totalTime * 100f;
            string timeStr = ParentDashboardViewModel.FormatPlayTime(g.totalPlayTime);

            var legendRow = MakeHRow(legend.transform, 26, TextAnchor.MiddleRight);
            legendRow.GetComponent<HorizontalLayoutGroup>().spacing = 6;

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
            dotLE.preferredHeight = 16;
        }

        if (otherTime > 0f)
        {
            string otherTimeStr = ParentDashboardViewModel.FormatPlayTime(otherTime);
            var otherRow = MakeHRow(legend.transform, 26, TextAnchor.MiddleRight);
            otherRow.GetComponent<HorizontalLayoutGroup>().spacing = 6;
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
            dotLE2.preferredHeight = 16;
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
            introCard.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(24, 24, 16, 16);
            var introTMP = AddChildTMP(introCard.transform, H(story.childIntro), 28, TextDark, TextAlignmentOptions.Right);
            introTMP.fontStyle = FontStyles.Bold;
            introTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
            FitCard(introCard.transform);
        }

        // ── 1. Weekly Summary (Hero) ──
        {
            var heroCard = MakeCard(parent);
            var heroTMP = AddChildTMP(heroCard, H(story.weeklySummary), 26, TextDark, TextAlignmentOptions.Right);
            heroTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 70;
            heroTMP.lineSpacing = 8;
            FitCard(heroCard);
        }

        // ── Section 2: Focus Right Now ──
        if (!string.IsNullOrEmpty(story.focusNow))
        {
            var focusCard = MakeInlineCard(parent, HexColor("#FFF3E0")); // warm orange bg
            var focusLayout = focusCard.GetComponent<VerticalLayoutGroup>();
            focusLayout.padding = new RectOffset(24, 24, 16, 16);

            var focusTMP = AddChildTMP(focusCard.transform, H(story.focusNow), 24, HexColor("#E65100"), TextAlignmentOptions.Right);
            focusTMP.fontStyle = FontStyles.Bold;
            focusTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;
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
                    .gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
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
                    .gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            if (!string.IsNullOrEmpty(story.weakLetters))
                AddChildTMP(letterCard, $"\u05E6\u05E8\u05D9\u05DA \u05EA\u05E8\u05D2\u05D5\u05DC: {story.weakLetters}", 22, AccentOrange, TextAlignmentOptions.Right)
                    .gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
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
                upTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
                AddChildTMP(diffCard, H(string.Join(", ", story.levelUpGames)), 22, TextDark, TextAlignmentOptions.Right)
                    .gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
            }
            if (story.easierLevelGames.Count > 0)
            {
                var dnTMP = AddChildTMP(diffCard, "\u05E6\u05E8\u05D9\u05DA \u05E8\u05DE\u05D4 \u05E7\u05DC\u05D4 \u05D9\u05D5\u05EA\u05E8:", 20, AccentOrange, TextAlignmentOptions.Right);
                dnTMP.fontStyle = FontStyles.Bold;
                dnTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
                AddChildTMP(diffCard, H(string.Join(", ", story.easierLevelGames)), 22, TextDark, TextAlignmentOptions.Right)
                    .gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
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
            dualRow.GetComponent<HorizontalLayoutGroup>().spacing = 12;
            dualRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            dualRow.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;
            dualRow.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (story.strengths.Count > 0)
            {
                var sCard = MakeInlineCard(dualRow.transform, StrengthBg);
                sCard.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(20, 20, 16, 16);
                sCard.GetComponent<VerticalLayoutGroup>().spacing = 6;
                var sTitle = AddChildTMP(sCard.transform, H("\u05D7\u05D6\u05E7\u05D5\u05EA"), 28, AccentGreen, TextAlignmentOptions.Right);
                sTitle.fontStyle = FontStyles.Bold;
                sTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;
                foreach (var s in story.strengths)
                {
                    var sTMP = AddChildTMP(sCard.transform, H(s), 22, TextDark, TextAlignmentOptions.Right);
                    sTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
                }
            }

            if (story.practiceAreas.Count > 0)
            {
                var pCard = MakeInlineCard(dualRow.transform, PracticeBg);
                pCard.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(20, 20, 16, 16);
                pCard.GetComponent<VerticalLayoutGroup>().spacing = 6;
                var pTitle = AddChildTMP(pCard.transform, H("\u05E6\u05E8\u05D9\u05DA \u05EA\u05E8\u05D2\u05D5\u05DC"), 28, AccentOrange, TextAlignmentOptions.Right);
                pTitle.fontStyle = FontStyles.Bold;
                pTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;
                foreach (var p in story.practiceAreas)
                {
                    var pTMP = AddChildTMP(pCard.transform, H(p), 22, TextDark, TextAlignmentOptions.Right);
                    pTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
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
                row.GetComponent<HorizontalLayoutGroup>().spacing = 8;
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
            impCard.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(20, 20, 14, 14);
            impCard.GetComponent<VerticalLayoutGroup>().spacing = 4;
            var impTitle = AddChildTMP(impCard.transform, H("\u05DE\u05D4 \u05D4\u05E9\u05EA\u05E4\u05E8"), 24, AccentGreen, TextAlignmentOptions.Right);
            // מה השתפר
            impTitle.fontStyle = FontStyles.Bold;
            impTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;
            foreach (var imp in story.improvements)
                AddChildTMP(impCard.transform, $"\u2022 {H(imp)}", 20, TextDark, TextAlignmentOptions.Right)
                    .gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
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
                    .gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
            FitCard(recCard);
        }

        // ── Progress Snapshot ──
        if (!string.IsNullOrEmpty(story.accuracyTrend) || !string.IsNullOrEmpty(story.lastScores))
        {
            var snapCard = MakeCard(parent);
            MakeSectionTitle(snapCard, "\u05DE\u05D2\u05DE\u05EA \u05D4\u05EA\u05E7\u05D3\u05DE\u05D5\u05EA"); // מגמת התקדמות
            if (!string.IsNullOrEmpty(story.accuracyTrend))
                AddChildTMP(snapCard, $"\u05D3\u05D9\u05D5\u05E7: {story.accuracyTrend}", 22, Primary, TextAlignmentOptions.Right)
                    .gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
            if (!string.IsNullOrEmpty(story.lastScores))
                AddChildTMP(snapCard, $"\u05DE\u05E9\u05D7\u05E7\u05D9\u05DD \u05D0\u05D7\u05E8\u05D5\u05E0\u05D9\u05DD: {story.lastScores}", 20, TextMedium, TextAlignmentOptions.Right)
                    .gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
            FitCard(snapCard);
        }

        // ── Section 6: Progress Highlight ──
        if (!string.IsNullOrEmpty(story.progressHighlight))
        {
            var progressCard = MakeInlineCard(parent, HexColor("#E8F5E9"));
            progressCard.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(24, 24, 14, 14);
            var progTMP = AddChildTMP(progressCard.transform, H(story.progressHighlight), 22, AccentGreen, TextAlignmentOptions.Right);
            progTMP.fontStyle = FontStyles.Bold;
            progTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;
            FitCard(progressCard.transform);
        }

        // ── Section 7: Suggested Next Step ──
        if (!string.IsNullOrEmpty(story.suggestedNextStep))
        {
            var nextCard = MakeInlineCard(parent, HexColor("#E3F2FD"));
            nextCard.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(24, 24, 14, 14);
            var nextTitle = AddChildTMP(nextCard.transform, H("\u05D4\u05E6\u05E2\u05D3 \u05D4\u05D1\u05D0"), 20, TextMedium, TextAlignmentOptions.Right);
            // הצעד הבא
            nextTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
            var nextTMP = AddChildTMP(nextCard.transform, H(story.suggestedNextStep), 24, Primary, TextAlignmentOptions.Right);
            nextTMP.fontStyle = FontStyles.Bold;
            nextTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;
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
        cardLayout.padding = new RectOffset(28, 28, 24, 24);
        cardLayout.spacing = 12;
        cardLayout.childAlignment = TextAnchor.MiddleCenter;

        // Card background — soft gradient feel
        card.GetComponent<Image>().color = HexColor("#EBF5FB");

        // Title
        var titleTMP = AddChildTMP(card, H("\u05D0\u05D4\u05D1\u05EA\u05DD \u05D0\u05EA \u05D4\u05D0\u05E4\u05DC\u05D9\u05E7\u05E6\u05D9\u05D4? \u05E1\u05E4\u05E8\u05D5 \u05DC\u05D7\u05D1\u05E8\u05D9\u05DD!"),
            // אהבתם את האפליקציה? ספרו לחברים!
            24, Primary, TextAlignmentOptions.Center);
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;

        // Subtitle with child stats
        string statsLine = BuildShareStatsLine();
        var subtitleTMP = AddChildTMP(card, H(statsLine),
            20, TextMedium, TextAlignmentOptions.Center);
        subtitleTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

        // Share button
        var btnGO = new GameObject("ShareButton");
        btnGO.transform.SetParent(card, false);
        var btnLE = btnGO.AddComponent<LayoutElement>();
        btnLE.preferredHeight = 56;
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
        labelTMP.fontSize = 24;
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
    //  GAMES TAB
    // ═══════════════════════════════════════════════════════════════

    private void BuildGamesTabContent()
    {
        if (_data == null || tabContents.Length < 2) return;
        var parent = tabContents[1]; // scroll view 1 = Games tab

        if (_data.games.Count == 0)
        {
            var noData = AddChildTMP(parent, H("\u05E2\u05D5\u05D3 \u05D0\u05D9\u05DF \u05E0\u05EA\u05D5\u05E0\u05D9\u05DD"), // עוד אין נתונים
                20, TextMedium, TextAlignmentOptions.Center);
            noData.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
            return;
        }

        // Game list — 3 per row for landscape
        for (int i = 0; i < _data.games.Count; i += 3)
        {
            var pairRow = MakeHRow(parent, 70, TextAnchor.MiddleRight);
            var pairLayout = pairRow.GetComponent<HorizontalLayoutGroup>();
            pairLayout.spacing = 12;
            pairLayout.childForceExpandWidth = true;
            pairLayout.childControlWidth = true;

            MakeGameListRow(pairRow.transform, _data.games[i]);

            if (i + 1 < _data.games.Count)
                MakeGameListRow(pairRow.transform, _data.games[i + 1]);
            if (i + 2 < _data.games.Count)
                MakeGameListRow(pairRow.transform, _data.games[i + 2]);
        }

        MakeSpacer(parent, 40f);
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
        row.GetComponent<HorizontalLayoutGroup>().spacing = 12;

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
        badgeLE.preferredHeight = 36;

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
        toggleLE.preferredHeight = 26;

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

        // ── "ניהול" manage button ──
        GameDashboardData capturedGame = game;
        var manageBtnGO = new GameObject("ManageBtn");
        manageBtnGO.transform.SetParent(row.transform, false);
        var manageBgImg = manageBtnGO.AddComponent<Image>();
        manageBgImg.sprite = null; // plain rect
        manageBgImg.color = Primary;
        manageBgImg.raycastTarget = true;
        var manageLE = manageBtnGO.AddComponent<LayoutElement>();
        manageLE.minWidth = 52;
        manageLE.preferredWidth = 52;
        manageLE.flexibleWidth = 0;
        manageLE.preferredHeight = 28;

        var manageTMP = AddChildTMP(manageBtnGO.transform, H("\u05E0\u05D9\u05D4\u05D5\u05DC"), 12, Color.white, TextAlignmentOptions.Center); // ניהול
        manageTMP.fontStyle = FontStyles.Bold;
        var mrt = manageTMP.rectTransform;
        mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one;
        mrt.offsetMin = Vector2.zero; mrt.offsetMax = Vector2.zero;

        var manageBtn = manageBtnGO.AddComponent<Button>();
        manageBtn.targetGraphic = manageBgImg;
        manageBtn.onClick.AddListener(() => ShowGameDetails(capturedGame));

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
            thumbLE.preferredHeight = 42;
        }

        // ── "Too hard" recommendation if game is visible but above age bucket ──
        if (!game.isInBaselineBucket && game.sessionsPlayed > 0)
        {
            var warnTMP = AddChildTMP(card,
                H("\u26A0 \u05DE\u05E9\u05D7\u05E7 \u05D6\u05D4 \u05E7\u05E6\u05EA \u05E7\u05E9\u05D4 \u05DC\u05D9\u05DC\u05D3. \u05DE\u05D5\u05DE\u05DC\u05E5 \u05DC\u05D4\u05E1\u05EA\u05D9\u05E8."),
                // ⚠ משחק זה קצת קשה לילד. מומלץ להסתיר.
                11, AccentOrange, TextAlignmentOptions.Right);
            warnTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  GAME DETAILS SCREEN (overlay on Games tab)
    // ═══════════════════════════════════════════════════════════════

    private GameObject _gameDetailsOverlay;

    private void ShowGameDetails(GameDashboardData game)
    {
        // Create overlay on top of the dashboard
        if (_gameDetailsOverlay != null) Destroy(_gameDetailsOverlay);

        _gameDetailsOverlay = new GameObject("GameDetailsOverlay");
        _gameDetailsOverlay.transform.SetParent(dashboardPanel, false);
        var overlayRT = _gameDetailsOverlay.AddComponent<RectTransform>();
        overlayRT.anchorMin = Vector2.zero;
        overlayRT.anchorMax = Vector2.one;
        overlayRT.offsetMin = Vector2.zero;
        overlayRT.offsetMax = Vector2.zero;

        // Semi-transparent background
        var overlayBg = _gameDetailsOverlay.AddComponent<Image>();
        overlayBg.color = new Color(1, 1, 1, 0.98f);
        overlayBg.raycastTarget = true;

        // Scroll view for details
        var scrollGO = new GameObject("Scroll");
        scrollGO.transform.SetParent(_gameDetailsOverlay.transform, false);
        var scrollRT = scrollGO.AddComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0.02f, 0.02f);
        scrollRT.anchorMax = new Vector2(0.98f, 0.88f);
        scrollRT.offsetMin = Vector2.zero;
        scrollRT.offsetMax = Vector2.zero;
        scrollGO.AddComponent<Image>().color = Color.clear;
        scrollGO.AddComponent<UnityEngine.UI.RectMask2D>();
        var scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;

        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(scrollGO.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.sizeDelta = Vector2.zero;
        var contentVL = contentGO.AddComponent<VerticalLayoutGroup>();
        contentVL.spacing = 12;
        contentVL.padding = new RectOffset(12, 12, 12, 12);
        contentVL.childForceExpandWidth = true;
        contentVL.childForceExpandHeight = false;
        contentVL.childControlWidth = true;
        contentVL.childControlHeight = true;
        contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = contentRT;

        // ── Header: back button + game name ──
        var headerGO = new GameObject("Header");
        headerGO.transform.SetParent(_gameDetailsOverlay.transform, false);
        var headerRT = headerGO.AddComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0, 0.90f);
        headerRT.anchorMax = new Vector2(1, 1f);
        headerRT.offsetMin = new Vector2(12, 0);
        headerRT.offsetMax = new Vector2(-12, -8);
        var headerHL = headerGO.AddComponent<HorizontalLayoutGroup>();
        headerHL.spacing = 12;
        headerHL.childAlignment = TextAnchor.MiddleRight;
        headerHL.childForceExpandWidth = false;
        headerHL.childControlWidth = false;
        headerHL.childControlHeight = true;

        // Game name (large)
        var nameGO = new GameObject("Name");
        nameGO.transform.SetParent(headerGO.transform, false);
        nameGO.AddComponent<LayoutElement>().flexibleWidth = 1;
        var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(nameTMP, game.gameName);
        nameTMP.fontSize = 26;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.color = TextDark;
        nameTMP.alignment = TextAlignmentOptions.Right;
        nameTMP.raycastTarget = false;

        // Back button
        var backGO = new GameObject("BackBtn");
        backGO.transform.SetParent(headerGO.transform, false);
        backGO.AddComponent<LayoutElement>().preferredWidth = 80;
        var backBg = backGO.AddComponent<Image>();
        if (roundedRect != null) { backBg.sprite = roundedRect; backBg.type = Image.Type.Sliced; }
        backBg.color = HexColor("#E0E0E0");
        var backBtn = backGO.AddComponent<Button>();
        backBtn.targetGraphic = backBg;
        backBtn.onClick.AddListener(CloseGameDetails);
        var backTextGO = new GameObject("Label");
        backTextGO.transform.SetParent(backGO.transform, false);
        var bkRT = backTextGO.AddComponent<RectTransform>();
        bkRT.anchorMin = Vector2.zero; bkRT.anchorMax = Vector2.one;
        bkRT.offsetMin = Vector2.zero; bkRT.offsetMax = Vector2.zero;
        var bkTMP = backTextGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(bkTMP, "\u2190 \u05D7\u05D6\u05E8\u05D4"); // ← חזרה
        bkTMP.fontSize = 16;
        bkTMP.color = TextDark;
        bkTMP.alignment = TextAlignmentOptions.Center;
        bkTMP.raycastTarget = false;

        // ── Build the full game card inside the scroll (reuse existing MakeGameCard) ──
        MakeGameCard(contentRT, game);
    }

    private void MakeColoringModeControl(Transform card)
    {
        // Title row
        var titleRow = MakeHRow(card, 28, TextAnchor.MiddleRight);
        titleRow.GetComponent<HorizontalLayoutGroup>().spacing = 6;
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
            lblTMP.fontSize = 14;
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
        explainTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;
    }

    private void CloseGameDetails()
    {
        if (_gameDetailsOverlay != null)
        {
            Destroy(_gameDetailsOverlay);
            _gameDetailsOverlay = null;
        }
    }

    private void MakeGameCard(Transform parent, GameDashboardData game)
    {
        var rec = game.recommendation;
        var card = MakeCard(parent);
        string gameId = game.gameId;

        // ── Header row: score badge + name + visibility chip ──
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
        badgeImg.color = game.sessionsPlayed > 0
            ? ParentDashboardViewModel.ScoreColor(game.score)
            : TextLight;
        if (game.sessionsPlayed > 0)
        {
            var badgeTMP = AddChildTMP(badgeGO.transform, $"{game.score:F0}",
                16, Color.white, TextAlignmentOptions.Center);
            badgeTMP.fontStyle = FontStyles.Bold;
        }
        else
        {
            AddChildTMP(badgeGO.transform, "\u2014", 16, Color.white, TextAlignmentOptions.Center); // —
        }

        // Name + subtitle column
        var infoCol = MakeVCol(headerRow.transform);
        var infoLE = infoCol.AddComponent<LayoutElement>();
        infoLE.flexibleWidth = 1;
        infoLE.preferredHeight = 50;

        var nameTMP = AddChildTMP(infoCol.transform, H(game.gameName), 19, TextDark, TextAlignmentOptions.Right);
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;

        string sub = game.sessionsPlayed > 0
            ? $"{ParentDashboardViewModel.TrendArrow(game.trend)} " +
                H($"{game.sessionsPlayed} \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD") // X משחקים
            : H("\u05E2\u05D5\u05D3 \u05DC\u05D0 \u05E9\u05D5\u05D7\u05E7"); // עוד לא שוחק
        var subTMP = AddChildTMP(infoCol.transform, sub, 14, TextMedium, TextAlignmentOptions.Right);
        subTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

        // Visibility status chip
        bool finalVisible = rec != null ? rec.finalVisible : game.systemVisibility;
        var visChipGO = new GameObject("VisChip");
        visChipGO.transform.SetParent(headerRow.transform, false);
        var visChipLE = visChipGO.AddComponent<LayoutElement>();
        visChipLE.preferredWidth = 72;
        visChipLE.preferredHeight = 28;
        var visChipImg = visChipGO.AddComponent<Image>();
        visChipImg.sprite = roundedRect;
        visChipImg.type = Image.Type.Sliced;
        visChipImg.color = finalVisible ? HexColor("#E8F5E9") : HexColor("#FFEBEE");
        var visChipTMP = AddChildTMP(visChipGO.transform,
            finalVisible
                ? H("\u05DE\u05D5\u05E6\u05D2")  // מוצג
                : H("\u05DE\u05D5\u05E1\u05EA\u05E8"), // מוסתר
            12, finalVisible ? AccentGreen : AccentRed, TextAlignmentOptions.Center);

        // ═══════════════════════════════════════════════════════════
        //  SECTION 1: ACCESS CONTROL
        // ═══════════════════════════════════════════════════════════
        MakeDivider(card);

        // System access recommendation
        bool sysVisible = rec != null ? rec.systemRecommendsVisible : game.isInBaselineBucket;
        string sysAccessLabel = sysVisible
            ? H("\u05DE\u05D5\u05DE\u05DC\u05E5 \u05E2\u05DC \u05D9\u05D3\u05D9 \u05D4\u05DE\u05E2\u05E8\u05DB\u05EA") // מומלץ על ידי המערכת
            : H("\u05DC\u05D0 \u05DE\u05D5\u05DE\u05DC\u05E5 \u05DB\u05E8\u05D2\u05E2"); // לא מומלץ כרגע
        var sysAccessRow = MakeHRow(card, 24, TextAnchor.MiddleRight);
        sysAccessRow.GetComponent<HorizontalLayoutGroup>().spacing = 6;
        var sysAccessIcon = AddChildTMP(sysAccessRow.transform,
            sysVisible ? "\u25CF" : "\u25CB", // ● or ○
            12, sysVisible ? AccentGreen : TextLight, TextAlignmentOptions.Center);
        sysAccessIcon.gameObject.AddComponent<LayoutElement>().preferredWidth = 16;
        var sysAccessTMP = AddChildTMP(sysAccessRow.transform, sysAccessLabel,
            13, TextMedium, TextAlignmentOptions.Right);
        sysAccessTMP.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        // Access control: 3 toggle buttons (Auto / On / Off)
        var accessRow = MakeHRow(card, 38, TextAnchor.MiddleCenter);
        accessRow.GetComponent<HorizontalLayoutGroup>().spacing = 8;
        accessRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;

        ParentGameAccessMode currentMode = rec != null
            ? rec.accessOverrideMode : game.visibilityMode;

        var autoBtn = MakeToggleButton(accessRow.transform,
            H("\u05D0\u05D5\u05D8\u05D5\u05DE\u05D8\u05D9"), // אוטומטי
            currentMode == ParentGameAccessMode.Default);
        var onBtn = MakeToggleButton(accessRow.transform,
            H("\u05E4\u05E2\u05D9\u05DC"), // פעיל
            currentMode == ParentGameAccessMode.ForcedEnabled);
        var offBtn = MakeToggleButton(accessRow.transform,
            H("\u05DE\u05D5\u05E1\u05EA\u05E8"), // מוסתר
            currentMode == ParentGameAccessMode.ForcedDisabled);

        // Access explanation
        string accessExplain = rec != null
            ? ParentDashboardViewModel.GetExplanationLabel(rec.accessExplanation)
            : game.visibilityReasonDisplay;
        var accessExplainTMP = AddChildTMP(card, H(accessExplain), 12, TextLight, TextAlignmentOptions.Right);
        accessExplainTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;

        // Wire access toggle buttons
        var capturedCard = card;
        var capturedScrollContent = parent;
        var capturedVisChipImg = visChipImg;
        var capturedVisChipTMP = visChipTMP;
        var capturedAccessExplainTMP = accessExplainTMP;
        var capturedAutoBtn = autoBtn;
        var capturedOnBtn = onBtn;
        var capturedOffBtn = offBtn;

        autoBtn.onClick.AddListener(() => OnAccessModeChanged(
            gameId, ParentGameAccessMode.Default,
            capturedAutoBtn, capturedOnBtn, capturedOffBtn,
            capturedVisChipImg, capturedVisChipTMP, capturedAccessExplainTMP,
            capturedCard, capturedScrollContent));
        onBtn.onClick.AddListener(() => OnAccessModeChanged(
            gameId, ParentGameAccessMode.ForcedEnabled,
            capturedAutoBtn, capturedOnBtn, capturedOffBtn,
            capturedVisChipImg, capturedVisChipTMP, capturedAccessExplainTMP,
            capturedCard, capturedScrollContent));
        offBtn.onClick.AddListener(() => OnAccessModeChanged(
            gameId, ParentGameAccessMode.ForcedDisabled,
            capturedAutoBtn, capturedOnBtn, capturedOffBtn,
            capturedVisChipImg, capturedVisChipTMP, capturedAccessExplainTMP,
            capturedCard, capturedScrollContent));

        // ═══════════════════════════════════════════════════════════
        //  SECTION 2: CONTENT / DIFFICULTY RECOMMENDATION + CONTROL
        // ═══════════════════════════════════════════════════════════
        MakeDivider(card);

        bool hasRec = rec != null;
        bool hasVariant = hasRec && rec.hasScalableVariant;
        bool isManual = game.manualDifficultyOverride;

        // ── Recommendation chain display ──
        if (hasRec)
        {
            // Baseline by age
            var baseRow = MakeHRow(card, 22, TextAnchor.MiddleRight);
            baseRow.GetComponent<HorizontalLayoutGroup>().spacing = 6;
            AddChildTMP(baseRow.transform, "\u25B8", 10, TextLight, TextAlignmentOptions.Center) // ▸
                .gameObject.AddComponent<LayoutElement>().preferredWidth = 14;
            AddChildTMP(baseRow.transform,
                H($"\u05D1\u05E8\u05D9\u05E8\u05EA \u05DE\u05D7\u05D3\u05DC \u05DC\u05E4\u05D9 \u05D2\u05D9\u05DC: {rec.baselineVariantLabel}"), // ברירת מחדל לפי גיל: X
                13, TextLight, TextAlignmentOptions.Right)
                .gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            // System recommendation (if different from baseline)
            if (rec.systemRecommendedDifficulty != rec.baselineDifficulty)
            {
                var sysRow = MakeHRow(card, 22, TextAnchor.MiddleRight);
                sysRow.GetComponent<HorizontalLayoutGroup>().spacing = 6;
                AddChildTMP(sysRow.transform, "\u25B8", 10, Primary, TextAlignmentOptions.Center) // ▸
                    .gameObject.AddComponent<LayoutElement>().preferredWidth = 14;
                string sysLabel = rec.recommendationSource == ContentRecommendationSource.Adaptive
                    ? H($"\u05D4\u05DE\u05DC\u05E6\u05EA \u05D4\u05DE\u05E2\u05E8\u05DB\u05EA: {rec.systemRecommendedVariantLabel}") // המלצת המערכת: X
                    : H($"\u05D4\u05DE\u05DC\u05E6\u05EA \u05D4\u05DE\u05E2\u05E8\u05DB\u05EA: {rec.systemRecommendedVariantLabel}");
                AddChildTMP(sysRow.transform, sysLabel,
                    13, Primary, TextAlignmentOptions.Right)
                    .gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            }

            // Content explanation
            string contentExplain = ParentDashboardViewModel.GetExplanationLabel(rec.contentExplanation);
            AddChildTMP(card, H(contentExplain), 12, TextLight, TextAlignmentOptions.Right)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 18;
        }

        MakeSpacer(card, 4f);

        // ── Difficulty control row ──
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
        int displayDiff = hasRec ? rec.finalDifficulty : game.currentDifficulty;
        var diffValTMP = AddChildTMP(diffDisplayGO.transform, $"{displayDiff}",
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
            isManual ? H("\u05D9\u05D3\u05E0\u05D9") : H("\u05D0\u05D5\u05D8\u05D5\u05DE\u05D8\u05D9"), // ידני / אוטומטי
            12, isManual ? AccentOrange : AccentGreen, TextAlignmentOptions.Center);
        modeLabelTMP.gameObject.AddComponent<LayoutElement>().preferredWidth = 72;

        // ── Final value display (prominent) ──
        string finalLabel = hasRec ? rec.finalVariantLabel
            : (game.activeDifficultyImpact ?? "");
        var finalRow = MakeHRow(card, 28, TextAnchor.MiddleRight);
        finalRow.GetComponent<HorizontalLayoutGroup>().spacing = 6;
        var finalIcon = AddChildTMP(finalRow.transform, "\u25BA", 12, Primary, TextAlignmentOptions.Center); // ►
        finalIcon.gameObject.AddComponent<LayoutElement>().preferredWidth = 16;
        string finalPrefix = H("\u05D4\u05E2\u05E8\u05DA \u05D1\u05E4\u05D5\u05E2\u05DC:"); // הערך בפועל:
        var finalValTMP = AddChildTMP(finalRow.transform,
            $"{finalPrefix} {H(finalLabel)}",
            15, Primary, TextAlignmentOptions.Right);
        finalValTMP.fontStyle = FontStyles.Bold;
        finalValTMP.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        // ── Reset button (shown when manual override) ──
        GameObject resetBtnGO = null;

        if (isManual)
        {
            MakeSpacer(card, 4f);

            // Recommended info row
            if (hasRec)
            {
                var recCard = MakeInlineCard(card, HexColor("#F0FFF0"));
                recCard.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(12, 12, 8, 8);
                recCard.GetComponent<VerticalLayoutGroup>().spacing = 2;

                string recLabel = H($"\u05D4\u05DE\u05DC\u05E6\u05EA \u05D4\u05DE\u05E2\u05E8\u05DB\u05EA: {rec.systemRecommendedVariantLabel}"); // המלצת המערכת: X
                AddChildTMP(recCard.transform, recLabel, 14, AccentGreen, TextAlignmentOptions.Right)
                    .gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
            }

            MakeSpacer(card, 4f);

            // Reset button
            resetBtnGO = new GameObject("ResetBtn");
            resetBtnGO.transform.SetParent(card, false);
            var resetLE2 = resetBtnGO.AddComponent<LayoutElement>();
            resetLE2.preferredHeight = 36;
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

            var resetLabelGO = new GameObject("Label");
            resetLabelGO.transform.SetParent(resetBtnGO.transform, false);
            var resetLabelRT = resetLabelGO.AddComponent<RectTransform>();
            resetLabelRT.anchorMin = Vector2.zero;
            resetLabelRT.anchorMax = Vector2.one;
            resetLabelRT.offsetMin = Vector2.zero;
            resetLabelRT.offsetMax = Vector2.zero;
            var resetLabelTMP = resetLabelGO.AddComponent<TextMeshProUGUI>();
            HebrewText.SetText(resetLabelTMP, "\u05D7\u05D6\u05E8\u05D4 \u05DC\u05E8\u05DE\u05D4 \u05D4\u05DE\u05D5\u05DE\u05DC\u05E6\u05EA"); // חזרה לרמה המומלצת
            resetLabelTMP.fontSize = 14;
            resetLabelTMP.color = AccentGreen;
            resetLabelTMP.alignment = TextAlignmentOptions.Center;
            resetLabelTMP.enableWordWrapping = false;
            resetLabelTMP.raycastTarget = false;
        }

        // Wire difficulty buttons
        var capturedFinalValTMP = finalValTMP;
        var capturedResetGO = resetBtnGO;

        minusBtn.onClick.AddListener(() => ChangeDifficultyFull(
            gameId, -1, diffValTMP, modeLabelTMP, capturedFinalValTMP,
            capturedCard, capturedScrollContent));
        plusBtn.onClick.AddListener(() => ChangeDifficultyFull(
            gameId, +1, diffValTMP, modeLabelTMP, capturedFinalValTMP,
            capturedCard, capturedScrollContent));

        if (resetBtnGO != null)
        {
            resetBtnGO.GetComponent<Button>().onClick.AddListener(() => ResetDifficultyOverride(
                gameId, diffValTMP, modeLabelTMP, capturedFinalValTMP,
                capturedResetGO, null,
                capturedCard, capturedScrollContent));
        }

        // ═══════════════════════════════════════════════════════════
        //  SECTION 2.5: COLORING MODE (only for coloring game)
        // ═══════════════════════════════════════════════════════════
        if (gameId == "coloring")
        {
            MakeDivider(card);
            MakeColoringModeControl(card);
        }

        // ═══════════════════════════════════════════════════════════
        //  SECTION 3: QUICK STATS (only if played)
        // ═══════════════════════════════════════════════════════════
        if (game.sessionsPlayed > 0)
        {
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
        }

        // ═══════════════════════════════════════════════════════════
        //  SECTION 4: EXPANDABLE DETAILS (only if played)
        // ═══════════════════════════════════════════════════════════
        if (game.sessionsPlayed > 0)
        {
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

            MakeDetailRow(detailsGO.transform, "\u05D6\u05DE\u05DF \u05DE\u05E9\u05D7\u05E7 \u05DB\u05D5\u05DC\u05DC", H(game.totalPlayTimeDisplay)); // זמן משחק כולל
            MakeDetailRow(detailsGO.transform, "\u05DE\u05E9\u05DA \u05DE\u05E9\u05D7\u05E7 \u05DE\u05DE\u05D5\u05E6\u05E2", H(game.averageSessionDisplay)); // משך משחק ממוצע
            MakeDetailRow(detailsGO.transform, "\u05D3\u05D9\u05D5\u05E7", $"{game.accuracy:P0}"); // דיוק
            MakeDetailRow(detailsGO.transform, "\u05E9\u05D2\u05D9\u05D0\u05D5\u05EA \u05DE\u05DE\u05D5\u05E6\u05E2", $"{game.mistakeRate:F1}"); // שגיאות ממוצע
            MakeDetailRow(detailsGO.transform, "\u05E7\u05E6\u05D1 \u05D4\u05E9\u05DC\u05DE\u05D4", $"{game.completionRate:P0}"); // קצב השלמה
            MakeDetailRow(detailsGO.transform, "\u05E6\u05D9\u05D5\u05DF \u05DE\u05D4\u05D9\u05E8\u05D5\u05EA", $"{game.speedScore:F0}"); // ציון מהירות
            MakeDetailRow(detailsGO.transform, "\u05E2\u05E6\u05DE\u05D0\u05D5\u05EA", $"{game.independenceScore:F0}"); // עצמאות
            MakeDetailRow(detailsGO.transform, "\u05E8\u05DE\u05D4 \u05D2\u05D1\u05D5\u05D4\u05D4 \u05D1\u05D9\u05D5\u05EA\u05E8", $"{game.highestDifficulty}/10"); // רמה גבוהה ביותר
            MakeDetailRow(detailsGO.transform, "\u05E8\u05E6\u05E3 \u05D4\u05E6\u05DC\u05D7\u05D5\u05EA \u05D4\u05DB\u05D9 \u05D0\u05E8\u05D5\u05DA", $"{game.maxStreak}"); // רצף הצלחות הכי ארוך

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
        }

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

        // 2-column layout for landscape (domain cards are tall, 2 fits better)
        for (int i = 0; i < _data.categories.Count; i += 2)
        {
            if (_data.categories[i].contributingGamesCount == 0 && (i + 1 >= _data.categories.Count || _data.categories[i + 1].contributingGamesCount == 0)) continue;

            var domainRow = MakeHRow(parent, 0, TextAnchor.UpperRight);
            var domainLayout = domainRow.GetComponent<HorizontalLayoutGroup>();
            domainLayout.spacing = 16;
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
        string variantLabel = GameRecommendationService.GetVariantLabel(gameId, newDiff);
        string finalPrefix = "\u05D4\u05E2\u05E8\u05DA \u05D1\u05E4\u05D5\u05E2\u05DC:"; // הערך בפועל:
        HebrewText.SetText(finalValTMP, $"{finalPrefix} {variantLabel}");

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
        modeTMP.color = AccentGreen;

        // Update final value label
        string variantLabel = GameRecommendationService.GetVariantLabel(gameId, gp.currentDifficulty);
        string finalPrefix = "\u05D4\u05E2\u05E8\u05DA \u05D1\u05E4\u05D5\u05E2\u05DC:"; // הערך בפועל:
        HebrewText.SetText(finalValTMP, $"{finalPrefix} {variantLabel}");

        // Hide the recommended card and reset button
        if (resetBtnGO != null) resetBtnGO.SetActive(false);
        if (recCardGO != null) recCardGO.SetActive(false);

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

        // Update override
        GameVisibilityService.SetOverride(profile, gameId, newMode);
        ProfileManager.Instance.Save();

        // Recompute visibility
        GameItemData gameItem = FindGameItemFromDb(gameId);
        var evalResult = gameItem != null
            ? GameVisibilityService.Evaluate(profile, gameItem)
            : new GameVisibilityResult(false, VisibilityReasonCode.Hidden_MissingData, VisibilitySource.MissingData);
        bool nowVisible = evalResult.isVisible;

        // Update toggle button states
        UpdateToggleButton(autoBtn, newMode == ParentGameAccessMode.Default);
        UpdateToggleButton(onBtn, newMode == ParentGameAccessMode.ForcedEnabled);
        UpdateToggleButton(offBtn, newMode == ParentGameAccessMode.ForcedDisabled);

        // Update visibility chip
        visChipImg.color = nowVisible ? HexColor("#E8F5E9") : HexColor("#FFEBEE");
        HebrewText.SetText(visChipTMP,
            nowVisible
                ? "\u05DE\u05D5\u05E6\u05D2"  // מוצג
                : "\u05DE\u05D5\u05E1\u05EA\u05E8"); // מוסתר

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
    }

    private GameItemData FindGameItemFromDb(string gameId)
    {
        var gameDb = Resources.Load<GameDatabase>("GameDatabase");
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
            .gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
        FitCard(card);
    }

    private void BuildInsightGroup(Transform parent, string title, List<string> items, Color bg, Color titleColor)
    {
        if (items == null || items.Count == 0) return;
        var card = MakeInlineCard(parent, bg);
        card.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(20, 20, 14, 14);
        card.GetComponent<VerticalLayoutGroup>().spacing = 6;
        var tTMP = AddChildTMP(card.transform, H(title), 24, titleColor, TextAlignmentOptions.Right);
        tTMP.fontStyle = FontStyles.Bold;
        tTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;
        foreach (var item in items)
            AddChildTMP(card.transform, H(item), 20, TextDark, TextAlignmentOptions.Right)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
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
        valTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 16;

        AddChildTMP(col.transform, label, 10, TextLight, TextAlignmentOptions.Center)
            .gameObject.AddComponent<LayoutElement>().preferredHeight = 14;
    }

    private void MakeSectionDivider(Transform parent, string rawHebrew)
    {
        MakeSpacer(parent, 8f);
        var tmp = AddChildTMP(parent, H(rawHebrew), 38, TextDark, TextAlignmentOptions.Right);
        tmp.fontStyle = FontStyles.Bold;
        tmp.gameObject.AddComponent<LayoutElement>().preferredHeight = 50;
    }

    private void MakeSectionTitle(Transform parent, string rawHebrew)
    {
        var tmp = AddChildTMP(parent, H(rawHebrew), 36, TextDark, TextAlignmentOptions.Right);
        tmp.fontStyle = FontStyles.Bold;
        tmp.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;
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

        var valTMP = AddChildTMP(go.transform, value, 38, TextDark, TextAlignmentOptions.Center);
        valTMP.fontStyle = FontStyles.Bold;
        valTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;

        var lblTMP = AddChildTMP(go.transform, H(rawLabel), 24, TextMedium, TextAlignmentOptions.Center);
        lblTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;
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
        layout.spacing = 6;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.padding = new RectOffset(8, 8, 10, 10);

        // Category name on top
        var nmTMP = AddChildTMP(go.transform, H(cat.categoryName), 20, TextDark, TextAlignmentOptions.Center);
        nmTMP.fontStyle = FontStyles.Bold;
        nmTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

        // Score circle below (fixed 50x50 inside a centered container)
        var circContainer = new GameObject("CircleContainer");
        circContainer.transform.SetParent(go.transform, false);
        circContainer.AddComponent<LayoutElement>().preferredHeight = 54;

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
        row.GetComponent<HorizontalLayoutGroup>().spacing = 10;

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
        if (img != null) img.color = active ? Primary : HexColor("#E8EBED");

        var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) tmp.color = active ? Color.white : TextMedium;
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
        cardRT.anchorMin = cardRT.anchorMax = new Vector2(0.5f, 0.5f);
        cardRT.pivot = new Vector2(0.5f, 0.5f);
        cardRT.sizeDelta = new Vector2(500, 380);

        var cardImg = cardGO.AddComponent<Image>();
        if (roundedRect != null) { cardImg.sprite = roundedRect; cardImg.type = Image.Type.Sliced; }
        cardImg.color = Color.white;
        cardImg.raycastTarget = true; // block tap-through to dim

        var cardLayout = cardGO.AddComponent<VerticalLayoutGroup>();
        cardLayout.spacing = 0;
        cardLayout.padding = new RectOffset(0, 0, 0, 0);
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
        headerGO.AddComponent<LayoutElement>().preferredHeight = 60;

        var headerLayout = headerGO.AddComponent<HorizontalLayoutGroup>();
        headerLayout.padding = new RectOffset(16, 16, 8, 8);
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
        closeTMP.fontSize = 26;
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
        titleTMP.fontSize = 22;
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

        // ── Toggle rows ──
        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(cardGO.transform, false);
        contentGO.AddComponent<RectTransform>();
        var contentLayout = contentGO.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 0;
        contentLayout.padding = new RectOffset(24, 24, 16, 16);
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentGO.AddComponent<LayoutElement>().flexibleHeight = 1;

        // Music toggle
        MakeSettingsToggle(contentGO.transform,
            "\u05DE\u05D5\u05D6\u05D9\u05E7\u05D4", // מוזיקה
            "\u05DE\u05D5\u05D6\u05D9\u05E7\u05EA \u05E8\u05E7\u05E2", // מוזיקת רקע
            AppSettings.MusicEnabled,
            val => AppSettings.MusicEnabled = val);

        MakeSettingsDivider(contentGO.transform);

        // Voice toggle
        MakeSettingsToggle(contentGO.transform,
            "\u05E7\u05D5\u05DC \u05D0\u05DC\u05D9\u05DF", // קול אלין
            "\u05E9\u05DE\u05D5\u05EA \u05D7\u05D9\u05D5\u05EA, \u05DE\u05E9\u05D5\u05D1\u05D9\u05DD", // שמות חיות, משובים
            AppSettings.VoiceEnabled,
            val => AppSettings.VoiceEnabled = val);

        MakeSettingsDivider(contentGO.transform);

        // Notifications toggle
        MakeSettingsToggle(contentGO.transform,
            "\u05D4\u05EA\u05E8\u05D0\u05D5\u05EA", // התראות
            "\u05EA\u05D6\u05DB\u05D5\u05E8\u05EA \u05DB\u05E9\u05DE\u05D3\u05D1\u05E7\u05D4 \u05DE\u05D5\u05DB\u05E0\u05D4", // תזכורת כשמדבקה מוכנה
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
        rowLayout.spacing = 12;
        rowLayout.padding = new RectOffset(0, 0, 10, 10);
        rowLayout.childAlignment = TextAnchor.MiddleRight;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = false;
        rowGO.AddComponent<LayoutElement>().preferredHeight = 70;

        // Toggle (right side in RTL = first in hierarchy)
        var toggleGO = new GameObject("Toggle");
        toggleGO.transform.SetParent(rowGO.transform, false);
        toggleGO.AddComponent<RectTransform>();

        var toggleBgGO = new GameObject("Background");
        toggleBgGO.transform.SetParent(toggleGO.transform, false);
        var toggleBgRT = toggleBgGO.AddComponent<RectTransform>();
        toggleBgRT.anchorMin = toggleBgRT.anchorMax = new Vector2(0.5f, 0.5f);
        toggleBgRT.sizeDelta = new Vector2(60, 32);
        var toggleBgImg = toggleBgGO.AddComponent<Image>();
        if (roundedRect != null) { toggleBgImg.sprite = roundedRect; toggleBgImg.type = Image.Type.Sliced; }
        toggleBgImg.color = currentValue ? Primary : BarBg;
        toggleBgImg.raycastTarget = true;

        var knobGO = new GameObject("Knob");
        knobGO.transform.SetParent(toggleBgGO.transform, false);
        var knobRT = knobGO.AddComponent<RectTransform>();
        knobRT.anchorMin = knobRT.anchorMax = new Vector2(currentValue ? 0.8f : 0.2f, 0.5f);
        knobRT.sizeDelta = new Vector2(24, 24);
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
        textLayout.spacing = 2;
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
        titleTMP.fontSize = 20;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = TextDark;
        titleTMP.alignment = TextAlignmentOptions.Right;
        titleTMP.raycastTarget = false;
        titleText.AddComponent<LayoutElement>().preferredHeight = 28;

        var subtitleText = new GameObject("Subtitle");
        subtitleText.transform.SetParent(textCol.transform, false);
        subtitleText.AddComponent<RectTransform>();
        var subTMP = subtitleText.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(subTMP, subtitle);
        subTMP.fontSize = 15;
        subTMP.color = TextMedium;
        subTMP.alignment = TextAlignmentOptions.Right;
        subTMP.raycastTarget = false;
        subtitleText.AddComponent<LayoutElement>().preferredHeight = 22;
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
