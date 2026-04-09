using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Collectible album that opens as a storybook overlay in the World scene.
/// Shows stickers organized by category across page spreads with page-turn animation.
/// Collected stickers appear vivid; uncollected show as grey silhouettes.
/// Swipe left/right to turn pages.
/// </summary>
public class CollectibleAlbumController : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite circleSprite;
    public Sprite roundedRect;

    [Header("Game Data")]
    public GameDatabase gameDatabase;

    [Header("Tab Icons")]
    public Sprite achievementTabSprite; // Games Collection icon for achievement tabs

    [Header("Sticker Sheets (sliced)")]
    public Sprite[] animalsStickers;   // 19 — dog..donkey
    public Sprite[] lettersStickers;   // 22 — א..ת
    public Sprite[] numbersStickers;   // 10 — 0..9
    public Sprite[] balloonsStickers;  // 12 — colors
    public Sprite[] aquariumStickers;  // 10 — sea creatures
    public Sprite[] carsStickers;      // 8  — vehicles
    public Sprite[] foodStickers;      // 8  — food
    public Sprite[] artStickers;       // 8  — art & crafts
    public Sprite[] natureStickers;    // 20 — nature & world

    // Runtime UI
    private GameObject _overlayRoot;
    private Image _dimBg;
    private RectTransform _bookRT;
    private RectTransform _leftPageContent;
    private RectTransform _rightPageContent;
    private TextMeshProUGUI _leftTitleTMP;
    private TextMeshProUGUI _rightTitleTMP;
    private TextMeshProUGUI _pageIndicatorTMP;
    private Button _prevButton;
    private Button _nextButton;
    private Color _coverColor;
    private bool _isOpen;
    private bool _isAnimating;
    private int _currentPage;
    private readonly List<Image> _tabImages = new List<Image>();
    private readonly List<Image> _tabBgs = new List<Image>();
    private RectTransform _pageTurnOverlay;

    // Swipe detection
    private bool _swipeTracking;
    private Vector2 _swipeStart;
    private const float SwipeThreshold = 80f;

    // ── Sticker Categories ──

    private struct StickerCategory
    {
        public string title;      // Hebrew title
        public string prefix;     // sticker ID prefix (e.g. "animal_")
        public Sprite[] sprites;
        public bool rtl;          // if true, right page shows first half (Hebrew order)
        public bool isAchievements; // special page — shows game thumbnails with frames
    }

    private StickerCategory[] _categories;
    private int _totalPages;

    private void Awake()
    {
        // Register all sprite arrays so any scene can look up sticker sprites
        StickerSpriteBank.Register("animal_",  animalsStickers);
        StickerSpriteBank.Register("letter_",  lettersStickers);
        StickerSpriteBank.Register("number_",  numbersStickers);
        StickerSpriteBank.Register("balloon_", balloonsStickers);
        StickerSpriteBank.Register("ocean_",   aquariumStickers);
        StickerSpriteBank.Register("vehicle_", carsStickers);
        StickerSpriteBank.Register("food_",    foodStickers);
        StickerSpriteBank.Register("art_",     artStickers);
        StickerSpriteBank.Register("nature_",  natureStickers);
    }

    private void BuildCategories()
    {
        // Sort letters by Hebrew Unicode order (א=0x05D0 → ת=0x05EA)
        var sortedLetters = SortByName(lettersStickers, (name) =>
        {
            if (string.IsNullOrEmpty(name)) return 9999;
            return (int)name[0]; // Unicode value gives correct Hebrew order
        });

        // Sort numbers by numeric value
        var sortedNumbers = SortByName(numbersStickers, (name) =>
        {
            int val;
            return int.TryParse(name, out val) ? val : 9999;
        });

        _categories = new StickerCategory[]
        {
            new StickerCategory { title = "\u05D7\u05D9\u05D5\u05EA",              prefix = "animal_",  sprites = animalsStickers },   // חיות
            new StickerCategory { title = "\u05D0\u05D5\u05EA\u05D9\u05D5\u05EA",  prefix = "letter_",  sprites = sortedLetters, rtl = true },  // אותיות
            new StickerCategory { title = "\u05DE\u05E1\u05E4\u05E8\u05D9\u05DD",  prefix = "number_",  sprites = sortedNumbers },     // מספרים
            new StickerCategory { title = "\u05D1\u05DC\u05D5\u05E0\u05D9\u05DD",  prefix = "balloon_", sprites = balloonsStickers },  // בלונים
            new StickerCategory { title = "\u05D9\u05DD",                          prefix = "ocean_",   sprites = aquariumStickers },  // ים
            new StickerCategory { title = "\u05DB\u05DC\u05D9 \u05EA\u05D7\u05D1\u05D5\u05E8\u05D4", prefix = "vehicle_", sprites = carsStickers }, // כלי תחבורה
            new StickerCategory { title = "\u05D0\u05D5\u05DB\u05DC",              prefix = "food_",    sprites = foodStickers },      // אוכל
            new StickerCategory { title = "\u05D9\u05E6\u05D9\u05E8\u05D4",        prefix = "art_",     sprites = artStickers },       // יצירה
            new StickerCategory { title = "\u05D8\u05D1\u05E2",                    prefix = "nature_",  sprites = natureStickers },    // טבע
        };

        // Build achievements page from game thumbnails
        _achievementGames = new List<GameItemData>();
        if (gameDatabase != null)
        {
            foreach (var game in gameDatabase.games)
                if (game != null && game.thumbnail != null)
                    _achievementGames.Add(game);
        }

        // Each category = 1 page spread (left + right)
        _totalPages = 0;
        foreach (var cat in _categories)
        {
            if (cat.sprites != null && cat.sprites.Length > 0)
                _totalPages++;
        }
        // Add achievements pages (2 spreads for ~30 games: 15 left+right each)
        if (_achievementGames.Count > 0)
        {
            _achievementStartPage = _totalPages;
            int achievementPages = Mathf.CeilToInt(_achievementGames.Count / 18f); // 9 per page side
            _totalPages += achievementPages;
        }
        if (_totalPages == 0) _totalPages = 1;
    }

    private List<GameItemData> _achievementGames;
    private int _achievementStartPage = -1;

    // Colors
    private static readonly Color BookCover = new Color(0.36f, 0.22f, 0.14f);
    private static readonly Color PageColor = new Color(0.98f, 0.96f, 0.90f);
    private static readonly Color TitleColor = new Color(0.25f, 0.15f, 0.08f);
    private static readonly Color SilhouetteColor = new Color(0.08f, 0.08f, 0.08f, 1f);

    // ── Swipe Input ──

    private void Update()
    {
        if (!_isOpen || _isAnimating) return;

        if (Input.GetMouseButtonDown(0))
        {
            _swipeTracking = true;
            _swipeStart = Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(0) && _swipeTracking)
        {
            _swipeTracking = false;
            float dx = Input.mousePosition.x - _swipeStart.x;
            if (Mathf.Abs(dx) > SwipeThreshold)
                TurnPage(dx < 0 ? 1 : -1);
        }
    }

    public void Open()
    {
        if (_isOpen || _isAnimating) return;
        FirebaseAnalyticsManager.LogAlbumOpened();
        BuildCategories();
        // Destroy old overlay so new sprite arrays take effect
        if (_overlayRoot != null) { Destroy(_overlayRoot); _overlayRoot = null; }
        BuildUI();
        _currentPage = 0;
        PopulatePages(_currentPage);
        StartCoroutine(OpenAnimation());
    }

    public void Close()
    {
        if (!_isOpen || _isAnimating) return;
        StartCoroutine(CloseAnimation());
    }

    // ── Build UI ──

    private void BuildUI()
    {
        if (_overlayRoot != null) return;

        var canvas = GetComponentInParent<Canvas>();

        _overlayRoot = new GameObject("AlbumOverlay");
        _overlayRoot.transform.SetParent(canvas.transform, false);
        _overlayRoot.transform.SetAsLastSibling();
        Stretch(_overlayRoot.AddComponent<RectTransform>());

        // Dim
        var dimGO = new GameObject("Dim");
        dimGO.transform.SetParent(_overlayRoot.transform, false);
        Stretch(dimGO.AddComponent<RectTransform>());
        _dimBg = dimGO.AddComponent<Image>();
        _dimBg.color = new Color(0, 0, 0, 0);
        _dimBg.raycastTarget = true;
        var dimBtn = dimGO.AddComponent<Button>();
        dimBtn.targetGraphic = _dimBg;
        dimBtn.onClick.AddListener(Close);

        // Book
        var bookGO = new GameObject("Book");
        bookGO.transform.SetParent(_overlayRoot.transform, false);
        _bookRT = bookGO.AddComponent<RectTransform>();
        _bookRT.anchorMin = new Vector2(0.5f, 0.5f);
        _bookRT.anchorMax = new Vector2(0.5f, 0.5f);
        _bookRT.sizeDelta = new Vector2(1560, 750);
        _bookRT.anchoredPosition = new Vector2(0, -70);
        var coverImg = bookGO.AddComponent<Image>();
        if (roundedRect != null) { coverImg.sprite = roundedRect; coverImg.type = Image.Type.Sliced; }
        Color coverColor = BookCover;
        var profile = ProfileManager.ActiveProfile;
        if (profile != null && !string.IsNullOrEmpty(profile.avatarColorHex))
        {
            Color parsed;
            if (ColorUtility.TryParseHtmlString(profile.avatarColorHex, out parsed))
                coverColor = new Color(parsed.r * 0.7f, parsed.g * 0.7f, parsed.b * 0.7f);
        }
        _coverColor = coverColor;
        coverImg.color = _coverColor;
        coverImg.raycastTarget = true;
        var shadow = bookGO.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.35f);
        shadow.effectDistance = new Vector2(4, -6);

        // Inner pages
        var innerGO = new GameObject("Inner");
        innerGO.transform.SetParent(bookGO.transform, false);
        var innerRT = innerGO.AddComponent<RectTransform>();
        innerRT.anchorMin = Vector2.zero; innerRT.anchorMax = Vector2.one;
        innerRT.offsetMin = new Vector2(18, 18); innerRT.offsetMax = new Vector2(-18, -18);
        var innerImg = innerGO.AddComponent<Image>();
        if (roundedRect != null) { innerImg.sprite = roundedRect; innerImg.type = Image.Type.Sliced; }
        innerImg.color = PageColor;

        // Spine
        var spineGO = new GameObject("Spine");
        spineGO.transform.SetParent(innerGO.transform, false);
        var spineRT = spineGO.AddComponent<RectTransform>();
        spineRT.anchorMin = new Vector2(0.5f, 0); spineRT.anchorMax = new Vector2(0.5f, 1);
        spineRT.sizeDelta = new Vector2(6, 0);
        spineRT.offsetMin = new Vector2(-3, 12); spineRT.offsetMax = new Vector2(3, -12);
        spineGO.AddComponent<Image>().color = new Color(coverColor.r * 0.8f, coverColor.g * 0.8f, coverColor.b * 0.8f);

        // Spine shadows
        for (int s = -1; s <= 1; s += 2)
        {
            var sl = new GameObject("SpineShadow");
            sl.transform.SetParent(innerGO.transform, false);
            var slRT = sl.AddComponent<RectTransform>();
            slRT.anchorMin = new Vector2(0.5f, 0); slRT.anchorMax = new Vector2(0.5f, 1);
            slRT.sizeDelta = new Vector2(12, 0);
            slRT.anchoredPosition = new Vector2(s * 8, 0);
            slRT.offsetMin = new Vector2(slRT.offsetMin.x, 14); slRT.offsetMax = new Vector2(slRT.offsetMax.x, -14);
            sl.AddComponent<Image>().color = new Color(0.85f, 0.82f, 0.75f, 0.3f);
        }

        // Left page
        var leftGO = new GameObject("LeftPage");
        leftGO.transform.SetParent(innerGO.transform, false);
        var leftRT = leftGO.AddComponent<RectTransform>();
        leftRT.anchorMin = Vector2.zero; leftRT.anchorMax = new Vector2(0.5f, 1);
        leftRT.offsetMin = new Vector2(16, 16); leftRT.offsetMax = new Vector2(-20, -16);

        var ltGO = new GameObject("LeftTitle"); ltGO.transform.SetParent(leftRT, false);
        var ltRT = ltGO.AddComponent<RectTransform>();
        ltRT.anchorMin = new Vector2(0, 1); ltRT.anchorMax = new Vector2(1, 1);
        ltRT.pivot = new Vector2(0.5f, 1); ltRT.anchoredPosition = new Vector2(0, -4); ltRT.sizeDelta = new Vector2(0, 52);
        _leftTitleTMP = ltGO.AddComponent<TextMeshProUGUI>();
        _leftTitleTMP.fontSize = 38; _leftTitleTMP.fontStyle = FontStyles.Bold;
        _leftTitleTMP.color = TitleColor; _leftTitleTMP.alignment = TextAlignmentOptions.Center;
        _leftTitleTMP.raycastTarget = false;

        var lcGO = new GameObject("LeftContent"); lcGO.transform.SetParent(leftRT, false);
        _leftPageContent = lcGO.AddComponent<RectTransform>();
        _leftPageContent.anchorMin = Vector2.zero; _leftPageContent.anchorMax = Vector2.one;
        _leftPageContent.offsetMin = new Vector2(8, 8); _leftPageContent.offsetMax = new Vector2(-8, -56);

        // Right page
        var rightGO = new GameObject("RightPage");
        rightGO.transform.SetParent(innerGO.transform, false);
        var rightRT = rightGO.AddComponent<RectTransform>();
        rightRT.anchorMin = new Vector2(0.5f, 0); rightRT.anchorMax = Vector2.one;
        rightRT.offsetMin = new Vector2(20, 16); rightRT.offsetMax = new Vector2(-16, -16);

        var rtGO = new GameObject("RightTitle"); rtGO.transform.SetParent(rightRT, false);
        var rtRT = rtGO.AddComponent<RectTransform>();
        rtRT.anchorMin = new Vector2(0, 1); rtRT.anchorMax = new Vector2(1, 1);
        rtRT.pivot = new Vector2(0.5f, 1); rtRT.anchoredPosition = new Vector2(0, -4); rtRT.sizeDelta = new Vector2(0, 52);
        _rightTitleTMP = rtGO.AddComponent<TextMeshProUGUI>();
        _rightTitleTMP.fontSize = 38; _rightTitleTMP.fontStyle = FontStyles.Bold;
        _rightTitleTMP.color = TitleColor; _rightTitleTMP.alignment = TextAlignmentOptions.Center;
        _rightTitleTMP.raycastTarget = false;

        var rcGO = new GameObject("RightContent"); rcGO.transform.SetParent(rightRT, false);
        _rightPageContent = rcGO.AddComponent<RectTransform>();
        _rightPageContent.anchorMin = Vector2.zero; _rightPageContent.anchorMax = Vector2.one;
        _rightPageContent.offsetMin = new Vector2(8, 8); _rightPageContent.offsetMax = new Vector2(-8, -56);

        // Page turn overlay
        var turnGO = new GameObject("PageTurn");
        turnGO.transform.SetParent(innerGO.transform, false);
        _pageTurnOverlay = turnGO.AddComponent<RectTransform>();
        _pageTurnOverlay.anchorMin = new Vector2(0.5f, 0); _pageTurnOverlay.anchorMax = Vector2.one;
        _pageTurnOverlay.offsetMin = Vector2.zero; _pageTurnOverlay.offsetMax = Vector2.zero;
        turnGO.AddComponent<Image>().color = PageColor;
        turnGO.SetActive(false);

        // Page dots
        var piGO = new GameObject("PageDots");
        piGO.transform.SetParent(bookGO.transform, false);
        var piRT = piGO.AddComponent<RectTransform>();
        piRT.anchorMin = new Vector2(0.5f, 0); piRT.anchorMax = new Vector2(0.5f, 0);
        piRT.pivot = new Vector2(0.5f, 1); piRT.anchoredPosition = new Vector2(0, -8);
        piRT.sizeDelta = new Vector2(200, 30);
        _pageIndicatorTMP = piGO.AddComponent<TextMeshProUGUI>();
        _pageIndicatorTMP.fontSize = 20; _pageIndicatorTMP.color = new Color(1, 1, 1, 0.7f);
        _pageIndicatorTMP.alignment = TextAlignmentOptions.Center; _pageIndicatorTMP.raycastTarget = false;

        // Category tabs (top of book)
        BuildCategoryTabs(bookGO.transform);

        // Close button
        var closeGO = new GameObject("Close");
        closeGO.transform.SetParent(bookGO.transform, false);
        var closeRT = closeGO.AddComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1, 1); closeRT.anchorMax = new Vector2(1, 1);
        closeRT.pivot = new Vector2(1, 1); closeRT.anchoredPosition = new Vector2(10, 10);
        closeRT.sizeDelta = new Vector2(56, 56);
        var closeImg = closeGO.AddComponent<Image>();
        if (circleSprite != null) closeImg.sprite = circleSprite;
        closeImg.color = new Color(0.85f, 0.25f, 0.2f);
        var closeBtn = closeGO.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        closeBtn.onClick.AddListener(Close);
        var xTMP = AddTMP(closeGO.transform, "\u2715", 28, Color.white);
        Stretch(xTMP.GetComponent<RectTransform>());
        xTMP.alignment = TextAlignmentOptions.Center;

        // Nav arrows
        _prevButton = MakeNavButton(bookGO.transform, "PrevPage", "\u25C0",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, 20));
        _prevButton.onClick.AddListener(() => TurnPage(-1));

        _nextButton = MakeNavButton(bookGO.transform, "NextPage", "\u25B6",
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 20));
        _nextButton.onClick.AddListener(() => TurnPage(1));

        _overlayRoot.SetActive(false);
    }

    // ── Category Tabs ──

    private void BuildCategoryTabs(Transform bookParent)
    {
        _tabImages.Clear();
        _tabBgs.Clear();

        int validCount = 0;
        foreach (var c in _categories)
            if (c.sprites != null && c.sprites.Length > 0) validCount++;
        // Add achievement tabs
        int achievementPages = _achievementStartPage >= 0 ? _totalPages - _achievementStartPage : 0;
        validCount += achievementPages;
        if (validCount == 0) return;

        float bookWidth = 1560f;
        float margin = 40f; // padding from book edges
        float gap = 4f;
        float usableWidth = bookWidth - margin * 2f;
        float tabWidth = (usableWidth - (validCount - 1) * gap) / validCount;
        float tabHeight = 95f;
        float startX = -bookWidth / 2f + margin + tabWidth / 2f;

        int pageIdx = 0;
        for (int i = 0; i < _categories.Length; i++)
        {
            var cat = _categories[i];
            if (cat.sprites == null || cat.sprites.Length == 0) continue;

            int targetPage = pageIdx;
            float xPos = startX + pageIdx * (tabWidth + gap);
            pageIdx++;

            // Tab container — anchored to top of book, sticking out above
            var tabGO = new GameObject($"Tab_{cat.prefix}");
            tabGO.transform.SetParent(bookParent, false);
            var tabRT = tabGO.AddComponent<RectTransform>();
            tabRT.anchorMin = new Vector2(0.5f, 1f);
            tabRT.anchorMax = new Vector2(0.5f, 1f);
            tabRT.pivot = new Vector2(0.5f, 0f);
            tabRT.anchoredPosition = new Vector2(xPos, -10f);
            tabRT.sizeDelta = new Vector2(tabWidth, tabHeight);

            // Tab background — rounded rectangle
            var bgImg = tabGO.AddComponent<Image>();
            if (roundedRect != null) { bgImg.sprite = roundedRect; bgImg.type = Image.Type.Sliced; }
            bgImg.color = new Color(_coverColor.r * 1.1f, _coverColor.g * 1.1f, _coverColor.b * 1.1f, 0.85f);
            bgImg.raycastTarget = true;

            var btn = tabGO.AddComponent<Button>();
            btn.targetGraphic = bgImg;
            int page = targetPage;
            btn.onClick.AddListener(() => JumpToPage(page));

            // Icon sprite inside tab
            Sprite tabSprite = GetTabSprite(cat);
            if (tabSprite != null)
            {
                var iconGO = new GameObject("Icon");
                iconGO.transform.SetParent(tabGO.transform, false);
                var iconRT = iconGO.AddComponent<RectTransform>();
                iconRT.anchorMin = new Vector2(0.05f, 0.06f);
                iconRT.anchorMax = new Vector2(0.95f, 0.94f);
                iconRT.offsetMin = Vector2.zero;
                iconRT.offsetMax = Vector2.zero;
                var iconImg = iconGO.AddComponent<Image>();
                iconImg.sprite = tabSprite;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                _tabImages.Add(iconImg);
            }
            else
            {
                _tabImages.Add(null);
            }

            _tabBgs.Add(bgImg);
        }

        // Achievement tabs (one per achievements page)
        for (int ap = 0; ap < achievementPages; ap++)
        {
            int targetPage = _achievementStartPage + ap;
            float xPos = startX + pageIdx * (tabWidth + gap);
            pageIdx++;

            var tabGO = new GameObject($"Tab_achievements_{ap}");
            tabGO.transform.SetParent(bookParent, false);
            var tabRT = tabGO.AddComponent<RectTransform>();
            tabRT.anchorMin = new Vector2(0.5f, 1f);
            tabRT.anchorMax = new Vector2(0.5f, 1f);
            tabRT.pivot = new Vector2(0.5f, 0f);
            tabRT.anchoredPosition = new Vector2(xPos, -10f);
            tabRT.sizeDelta = new Vector2(tabWidth, tabHeight);

            var bgImg = tabGO.AddComponent<Image>();
            if (roundedRect != null) { bgImg.sprite = roundedRect; bgImg.type = Image.Type.Sliced; }
            bgImg.color = new Color(_coverColor.r * 1.1f, _coverColor.g * 1.1f, _coverColor.b * 1.1f, 0.85f);
            bgImg.raycastTarget = true;

            var btn = tabGO.AddComponent<Button>();
            btn.targetGraphic = bgImg;
            int page = targetPage;
            btn.onClick.AddListener(() => JumpToPage(page));

            // Achievement tab icon — Games Collection image
            Sprite achTabSprite = achievementTabSprite;
            if (achTabSprite != null)
            {
                var iconGO = new GameObject("Icon");
                iconGO.transform.SetParent(tabGO.transform, false);
                var iconRT = iconGO.AddComponent<RectTransform>();
                iconRT.anchorMin = new Vector2(0.05f, 0.06f);
                iconRT.anchorMax = new Vector2(0.95f, 0.94f);
                iconRT.offsetMin = Vector2.zero;
                iconRT.offsetMax = Vector2.zero;
                var iconImg = iconGO.AddComponent<Image>();
                iconImg.sprite = achTabSprite;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                _tabImages.Add(iconImg);
            }
            else
            {
                _tabImages.Add(null);
            }
            _tabBgs.Add(bgImg);
        }
    }

    private Sprite GetTabSprite(StickerCategory cat)
    {
        if (cat.sprites == null || cat.sprites.Length == 0) return null;

        var profile = ProfileManager.ActiveProfile;

        switch (cat.prefix)
        {
            case "animal_":
                // Favorite animal
                string favAnimal = profile?.favoriteAnimalId;
                if (!string.IsNullOrEmpty(favAnimal))
                {
                    string lower = favAnimal.ToLower();
                    foreach (var spr in cat.sprites)
                        if (spr != null && spr.name.ToLower() == lower) return spr;
                }
                return cat.sprites[0];

            case "balloon_":
                // Favorite color (from avatar)
                string avatarHex = profile?.avatarColorHex;
                if (!string.IsNullOrEmpty(avatarHex))
                {
                    string colorId = AvatarHexToColorName(avatarHex);
                    if (!string.IsNullOrEmpty(colorId))
                    {
                        string lower = colorId.ToLower();
                        foreach (var spr in cat.sprites)
                            if (spr != null && spr.name.ToLower() == lower) return spr;
                    }
                }
                return cat.sprites[0];

            case "letter_":
                // א (alef)
                foreach (var spr in cat.sprites)
                    if (spr != null && spr.name == "\u05D0") return spr;
                return cat.sprites[0];

            case "number_":
                // 2
                foreach (var spr in cat.sprites)
                    if (spr != null && spr.name == "2") return spr;
                return cat.sprites[0];

            default:
                return cat.sprites[0];
        }
    }

    private static string AvatarHexToColorName(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return null;
        switch (hex.ToUpper())
        {
            case "#EF9A9A": return "Red";
            case "#F48FB1": return "Pink";
            case "#CE93D8": case "#B39DDB": return "Purple";
            case "#90CAF9": return "Blue";
            case "#80DEEA": return "Cyan";
            case "#80CBC4": case "#A5D6A7": return "Green";
            case "#FFF59D": return "Yellow";
            case "#FFCC80": case "#FFAB91": return "Orange";
            case "#BCAAA4": return "Brown";
            default: return null;
        }
    }

    private void JumpToPage(int targetPage)
    {
        if (_isAnimating || targetPage == _currentPage) return;
        if (targetPage < 0 || targetPage >= _totalPages) return;
        _currentPage = targetPage;
        PopulatePages(_currentPage);
    }

    private void UpdateTabHighlight()
    {
        for (int i = 0; i < _tabBgs.Count; i++)
        {
            if (_tabBgs[i] == null) continue;
            var tabRT = _tabBgs[i].GetComponent<RectTransform>();
            bool active = i == _currentPage;

            // Active tab: taller, page-colored; inactive: shorter, cover-colored
            float w = tabRT.sizeDelta.x;
            tabRT.sizeDelta = new Vector2(w, active ? 100f : 82f);
            tabRT.anchoredPosition = new Vector2(tabRT.anchoredPosition.x, active ? -6f : -14f);

            _tabBgs[i].color = active
                ? PageColor
                : new Color(_coverColor.r * 1.15f, _coverColor.g * 1.15f, _coverColor.b * 1.15f, 0.7f);
        }
    }

    // ── Pages ──

    private StickerCategory GetCategoryForPage(int page)
    {
        int idx = 0;
        for (int i = 0; i < _categories.Length; i++)
        {
            if (_categories[i].sprites == null || _categories[i].sprites.Length == 0) continue;
            if (idx == page) return _categories[i];
            idx++;
        }
        return _categories[0];
    }

    private bool IsAchievementPage(int page) => _achievementStartPage >= 0 && page >= _achievementStartPage;

    private void PopulatePages(int page)
    {
        ClearPage(_leftPageContent);
        ClearPage(_rightPageContent);

        if (IsAchievementPage(page))
        {
            HebrewText.SetText(_leftTitleTMP, "\u05D4\u05D9\u05E9\u05D2\u05D9\u05DD"); // הישגים
            HebrewText.SetText(_rightTitleTMP, "\u05D4\u05D9\u05E9\u05D2\u05D9\u05DD");
            int achPage = page - _achievementStartPage;
            int start = achPage * 18;
            BuildAchievementGrid(_leftPageContent, start, 9);
            BuildAchievementGrid(_rightPageContent, start + 9, 9);
        }
        else
        {
            var cat = GetCategoryForPage(page);
            HebrewText.SetText(_leftTitleTMP, cat.title);
            HebrewText.SetText(_rightTitleTMP, cat.title);

            int halfCount = Mathf.CeilToInt(cat.sprites.Length / 2f);
            int secondHalf = cat.sprites.Length - halfCount;

            if (cat.rtl)
            {
                BuildCategoryStickerGrid(_rightPageContent, cat, 0, halfCount);
                BuildCategoryStickerGrid(_leftPageContent, cat, halfCount, secondHalf);
            }
            else
            {
                BuildCategoryStickerGrid(_leftPageContent, cat, 0, halfCount);
                BuildCategoryStickerGrid(_rightPageContent, cat, halfCount, secondHalf);
            }
        }

        UpdateNav();
    }

    private void PopulateLeftPage(int page)
    {
        ClearPage(_leftPageContent);
        if (IsAchievementPage(page))
        {
            HebrewText.SetText(_leftTitleTMP, "\u05D4\u05D9\u05E9\u05D2\u05D9\u05DD");
            BuildAchievementGrid(_leftPageContent, (page - _achievementStartPage) * 18, 9);
        }
        else
        {
            var cat = GetCategoryForPage(page);
            HebrewText.SetText(_leftTitleTMP, cat.title);
            int halfCount = Mathf.CeilToInt(cat.sprites.Length / 2f);
            if (cat.rtl)
                BuildCategoryStickerGrid(_leftPageContent, cat, halfCount, cat.sprites.Length - halfCount);
            else
                BuildCategoryStickerGrid(_leftPageContent, cat, 0, halfCount);
        }
    }

    private void PopulateRightPage(int page)
    {
        ClearPage(_rightPageContent);
        if (IsAchievementPage(page))
        {
            HebrewText.SetText(_rightTitleTMP, "\u05D4\u05D9\u05E9\u05D2\u05D9\u05DD");
            BuildAchievementGrid(_rightPageContent, (page - _achievementStartPage) * 18 + 9, 9);
        }
        else
        {
            var cat = GetCategoryForPage(page);
            HebrewText.SetText(_rightTitleTMP, cat.title);
            int halfCount = Mathf.CeilToInt(cat.sprites.Length / 2f);
            if (cat.rtl)
                BuildCategoryStickerGrid(_rightPageContent, cat, 0, halfCount);
            else
                BuildCategoryStickerGrid(_rightPageContent, cat, halfCount, cat.sprites.Length - halfCount);
        }
    }

    private static Sprite[] SortByName(Sprite[] source, System.Func<string, int> keyFunc)
    {
        if (source == null || source.Length == 0) return source;
        var list = new List<Sprite>(source);
        list.Sort((a, b) =>
        {
            string na = a != null ? a.name : "";
            string nb = b != null ? b.name : "";
            return keyFunc(na).CompareTo(keyFunc(nb));
        });
        return list.ToArray();
    }

    private void BuildCategoryStickerGrid(RectTransform parent, StickerCategory cat, int start, int count)
    {
        if (cat.sprites == null || count <= 0) return;

        var gridGO = new GameObject("Grid");
        gridGO.transform.SetParent(parent, false);
        Stretch(gridGO.AddComponent<RectTransform>());
        var grid = gridGO.AddComponent<GridLayoutGroup>();

        // Adapt cell size and columns based on how many stickers
        int cols;
        Vector2 cellSize;
        if (count <= 4)
        {
            cols = 2;
            cellSize = new Vector2(200, 210);
        }
        else if (count <= 6)
        {
            cols = 3;
            cellSize = new Vector2(180, 190);
        }
        else if (count <= 9)
        {
            cols = 3;
            cellSize = new Vector2(170, 180);
        }
        else
        {
            cols = 4;
            cellSize = new Vector2(140, 150);
        }

        grid.cellSize = cellSize;
        grid.spacing = new Vector2(6, 4);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = cols;

        if (cat.rtl)
        {
            grid.startCorner = GridLayoutGroup.Corner.UpperRight;
            grid.childAlignment = TextAnchor.UpperRight;
        }
        else
        {
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.childAlignment = TextAnchor.UpperCenter;
        }

        var profile = ProfileManager.ActiveProfile;
        var collected = profile?.journey?.collectedStickerIds ?? new List<string>();

        for (int i = start; i < start + count && i < cat.sprites.Length; i++)
        {
            string spriteName = cat.sprites[i] != null ? cat.sprites[i].name.ToLower() : $"{i}";
            string stickerId = $"{cat.prefix}{spriteName}";
            bool isCollected = collected.Contains(stickerId);

            var cell = new GameObject($"S_{stickerId}");
            cell.transform.SetParent(gridGO.transform, false);
            cell.AddComponent<RectTransform>();

            var imgGO = new GameObject("Img");
            imgGO.transform.SetParent(cell.transform, false);
            var imgRT = imgGO.AddComponent<RectTransform>();
            Stretch(imgRT);
            imgRT.offsetMin = new Vector2(4, 4);
            imgRT.offsetMax = new Vector2(-4, -4);
            var img = imgGO.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;

            if (cat.sprites[i] != null)
            {
                img.sprite = cat.sprites[i];
                img.color = isCollected ? Color.white : SilhouetteColor;
            }
            else
            {
                if (circleSprite != null) img.sprite = circleSprite;
                img.color = SilhouetteColor;
            }
        }
    }

    // ── Achievement Grid ──

    private static readonly Color BronzeFrame = new Color(0.8f, 0.5f, 0.2f);
    private static readonly Color SilverFrame = new Color(0.75f, 0.75f, 0.78f);
    private static readonly Color GoldFrame   = new Color(1f, 0.84f, 0f);

    private void BuildAchievementGrid(RectTransform parent, int start, int count)
    {
        if (_achievementGames == null || _achievementGames.Count == 0) return;

        var gridGO = new GameObject("Grid");
        gridGO.transform.SetParent(parent, false);
        Stretch(gridGO.AddComponent<RectTransform>());
        var grid = gridGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(150, 160);
        grid.spacing = new Vector2(4, 4);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3; // 3x3 = 9 per page side

        var profile = ProfileManager.ActiveProfile;
        var collected = profile?.journey?.collectedStickerIds ?? new List<string>();

        for (int i = start; i < start + count && i < _achievementGames.Count; i++)
        {
            var game = _achievementGames[i];
            int tier = StickerCatalog.GetAchievementTier(game.id, collected);

            var cell = new GameObject($"Ach_{game.id}");
            cell.transform.SetParent(gridGO.transform, false);
            cell.AddComponent<RectTransform>();

            // Thumbnail (always shown — silhouette if not earned, vivid if earned)
            var imgGO = new GameObject("Img");
            imgGO.transform.SetParent(cell.transform, false);
            var imgRT = imgGO.AddComponent<RectTransform>();
            Stretch(imgRT);
            imgRT.offsetMin = new Vector2(8, 20);
            imgRT.offsetMax = new Vector2(-8, -4);
            var img = imgGO.AddComponent<Image>();
            img.sprite = game.thumbnail;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = tier > 0 ? Color.white : SilhouetteColor;

            if (tier > 0)
            {
                // Soft shadow behind image
                var shadowGO = new GameObject("Shadow");
                shadowGO.transform.SetParent(cell.transform, false);
                shadowGO.transform.SetAsFirstSibling();
                var shadowRT = shadowGO.AddComponent<RectTransform>();
                shadowRT.anchorMin = new Vector2(0.15f, 0f);
                shadowRT.anchorMax = new Vector2(0.85f, 0.08f);
                shadowRT.offsetMin = Vector2.zero;
                shadowRT.offsetMax = Vector2.zero;
                var shadowImg = shadowGO.AddComponent<Image>();
                if (circleSprite != null) shadowImg.sprite = circleSprite;
                shadowImg.color = new Color(0, 0, 0, 0.08f);
                shadowImg.raycastTarget = false;
            }

            // Stars bar at bottom — shows tier (1/2/3 stars)
            Color starColor = tier == 3 ? GoldFrame : tier == 2 ? SilverFrame : tier == 1 ? BronzeFrame : SilhouetteColor;
            int starCount = tier > 0 ? tier : 3; // show 3 grey stars if not earned

            var starsGO = new GameObject("Stars");
            starsGO.transform.SetParent(cell.transform, false);
            var starsRT = starsGO.AddComponent<RectTransform>();
            starsRT.anchorMin = new Vector2(0f, 0f);
            starsRT.anchorMax = new Vector2(1f, 0f);
            starsRT.pivot = new Vector2(0.5f, 0f);
            starsRT.anchoredPosition = new Vector2(0, 2);
            starsRT.sizeDelta = new Vector2(0, 18);
            var starsLayout = starsGO.AddComponent<HorizontalLayoutGroup>();
            starsLayout.childAlignment = TextAnchor.MiddleCenter;
            starsLayout.childControlWidth = false;
            starsLayout.childControlHeight = false;
            starsLayout.childForceExpandWidth = false;
            starsLayout.spacing = 2;

            var starSprite = Resources.Load<Sprite>("Icons/star");
            for (int s = 0; s < 3; s++)
            {
                var starGO = new GameObject($"Star_{s}");
                starGO.transform.SetParent(starsGO.transform, false);
                var starRT = starGO.AddComponent<RectTransform>();
                starRT.sizeDelta = new Vector2(18, 18);
                var starImg = starGO.AddComponent<Image>();
                if (starSprite != null) starImg.sprite = starSprite;
                starImg.raycastTarget = false;
                starImg.color = s < tier ? starColor : new Color(0.7f, 0.7f, 0.7f, 0.3f);
            }
        }
    }

    private void UpdateNav()
    {
        string dots = "";
        for (int i = 0; i < _totalPages; i++)
            dots += (i == _currentPage) ? "\u25CF " : "\u25CB ";
        _pageIndicatorTMP.text = dots.Trim();

        _prevButton.gameObject.SetActive(_currentPage > 0);
        _nextButton.gameObject.SetActive(_currentPage < _totalPages - 1);
        UpdateTabHighlight();
    }

    // ── Page Turn ──

    private void TurnPage(int dir)
    {
        int target = _currentPage + dir;
        if (target < 0 || target >= _totalPages || _isAnimating) return;
        StartCoroutine(PageTurnAnimation(dir, target));
    }

    private IEnumerator PageTurnAnimation(int dir, int targetPage)
    {
        _isAnimating = true;

        bool goingRight = dir > 0;
        var turnImg = _pageTurnOverlay.GetComponent<Image>();
        float halfDur = 0.25f;

        if (goingRight)
        {
            PopulateRightPage(targetPage);
            SetTurnOverlay(new Vector2(0.5f, 0), Vector2.one, new Vector2(0, 0.5f));
            _pageTurnOverlay.localScale = Vector3.one;
            turnImg.color = PageColor;
            _pageTurnOverlay.gameObject.SetActive(true);

            yield return AnimateFold(turnImg, 1f, 0f, halfDur);

            SetTurnOverlay(Vector2.zero, new Vector2(0.5f, 1), new Vector2(1, 0.5f));
            yield return AnimateFold(turnImg, 0f, 1f, halfDur);

            PopulateLeftPage(targetPage);
        }
        else
        {
            PopulateLeftPage(targetPage);
            SetTurnOverlay(Vector2.zero, new Vector2(0.5f, 1), new Vector2(1, 0.5f));
            _pageTurnOverlay.localScale = Vector3.one;
            turnImg.color = PageColor;
            _pageTurnOverlay.gameObject.SetActive(true);

            yield return AnimateFold(turnImg, 1f, 0f, halfDur);

            SetTurnOverlay(new Vector2(0.5f, 0), Vector2.one, new Vector2(0, 0.5f));
            yield return AnimateFold(turnImg, 0f, 1f, halfDur);

            PopulateRightPage(targetPage);
        }

        _currentPage = targetPage;
        FirebaseAnalyticsManager.LogAlbumPageViewed(_currentPage);
        UpdateNav();

        _pageTurnOverlay.gameObject.SetActive(false);
        _pageTurnOverlay.localScale = Vector3.one;
        _isAnimating = false;
    }

    private void SetTurnOverlay(Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        _pageTurnOverlay.anchorMin = anchorMin;
        _pageTurnOverlay.anchorMax = anchorMax;
        _pageTurnOverlay.offsetMin = Vector2.zero;
        _pageTurnOverlay.offsetMax = Vector2.zero;
        _pageTurnOverlay.pivot = pivot;
    }

    private IEnumerator AnimateFold(Image turnImg, float fromScale, float toScale, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float e = Mathf.Clamp01(t / duration);
            e = e * e * (3f - 2f * e);
            float sx = Mathf.Lerp(fromScale, toScale, e);
            _pageTurnOverlay.localScale = new Vector3(sx, 1, 1);
            float flatness = 1f - Mathf.Abs(sx);
            float shade = Mathf.Lerp(1f, 0.7f, flatness);
            turnImg.color = new Color(PageColor.r * shade, PageColor.g * shade, PageColor.b * shade);
            yield return null;
        }
        _pageTurnOverlay.localScale = new Vector3(toScale, 1, 1);
    }

    // ── Open / Close ──

    private IEnumerator OpenAnimation()
    {
        _isAnimating = true;
        _overlayRoot.SetActive(true);
        _bookRT.localScale = Vector3.one * 0.05f;
        float dur = 0.4f, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime; float p = Mathf.Clamp01(t / dur);
            _bookRT.localScale = Vector3.one * EaseOutBack(p);
            _dimBg.color = new Color(0, 0, 0, Mathf.Lerp(0, 0.55f, p));
            yield return null;
        }
        _bookRT.localScale = Vector3.one;
        _dimBg.color = new Color(0, 0, 0, 0.55f);
        _isOpen = true; _isAnimating = false;
    }

    private IEnumerator CloseAnimation()
    {
        _isAnimating = true;
        float dur = 0.3f, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime; float p = Mathf.Clamp01(t / dur);
            _bookRT.localScale = Vector3.one * (1f - EaseInBack(p));
            _dimBg.color = new Color(0, 0, 0, Mathf.Lerp(0.55f, 0, p));
            yield return null;
        }
        _overlayRoot.SetActive(false);
        _isOpen = false; _isAnimating = false;
    }

    // ── Helpers ──

    private Button MakeNavButton(Transform parent, string name, string label,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offset)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        rt.anchoredPosition = offset; rt.sizeDelta = new Vector2(55, 55);
        var img = go.AddComponent<Image>();
        if (circleSprite != null) img.sprite = circleSprite;
        img.color = _coverColor;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var tmp = AddTMP(go.transform, label, 24, Color.white);
        tmp.alignment = TextAlignmentOptions.Center;
        Stretch(tmp.GetComponent<RectTransform>());
        go.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.2f);
        return btn;
    }

    private TextMeshProUGUI AddTMP(Transform parent, string text, int size, Color color)
    {
        var go = new GameObject("Text"); go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.color = color;
        tmp.raycastTarget = false; tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    private void ClearPage(RectTransform page)
    {
        for (int i = page.childCount - 1; i >= 0; i--) Destroy(page.GetChild(i).gameObject);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private static float EaseInBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return c3 * t * t * t - c1 * t * t;
    }
}
