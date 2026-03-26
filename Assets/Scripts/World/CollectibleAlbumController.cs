using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Collectible album that opens as a storybook overlay in the World scene.
/// Shows Animals, Colors, and Stickers across page spreads with page-turn animation.
/// Discovered items appear vivid; undiscovered show as grey silhouettes.
/// Swipe left/right to turn pages.
/// </summary>
public class CollectibleAlbumController : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite circleSprite;
    public Sprite roundedRect;
    public GameDatabase gameDatabase;
    public Sprite[] stickerSprites; // sliced from Sticker.png, wired by setup

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
    private RectTransform _pageTurnOverlay;

    // Swipe detection
    private bool _swipeTracking;
    private Vector2 _swipeStart;
    private const float SwipeThreshold = 80f;

    // Data
    private static readonly string[] AllAnimalIds = {
        "Dog", "Cat", "Bear", "Duck", "Fish", "Frog", "Bird", "Cow", "Horse", "Lion",
        "Monkey", "Elephant", "Giraffe", "Zebra", "Turtle", "Snake", "Sheep", "Chicken", "Donkey"
    };

    private static readonly string[] AllColorIds = {
        "Red", "Blue", "Yellow", "Green", "Orange", "Purple", "Pink", "Cyan", "Brown", "Black", "White", "Grey"
    };

    private const int StickersPerSpread = 18; // 9 left + 9 right per book spread
    private int TotalPages => 2 + StickerPageCount; // Animals + Colors + N sticker pages
    private int StickerPageCount =>
        (stickerSprites != null && stickerSprites.Length > 0)
            ? Mathf.CeilToInt((float)stickerSprites.Length / StickersPerSpread)
            : 1;

    // Colors
    private static readonly Color BookCover = new Color(0.36f, 0.22f, 0.14f);
    private static readonly Color PageColor = new Color(0.98f, 0.96f, 0.90f);
    private static readonly Color SpineColor = new Color(0.30f, 0.18f, 0.10f);
    private static readonly Color TitleColor = new Color(0.25f, 0.15f, 0.08f);
    private static readonly Color SilhouetteColor = new Color(0.08f, 0.08f, 0.08f, 1f);
    private static readonly Color LabelColor = new Color(0.45f, 0.38f, 0.30f);

    private static readonly Dictionary<string, Color> ColorMap = new Dictionary<string, Color>
    {
        {"Red", new Color(0.94f, 0.27f, 0.27f)}, {"Blue", new Color(0.23f, 0.51f, 0.96f)},
        {"Yellow", new Color(0.98f, 0.80f, 0.08f)}, {"Green", new Color(0.13f, 0.77f, 0.37f)},
        {"Orange", new Color(0.98f, 0.45f, 0.09f)}, {"Purple", new Color(0.55f, 0.36f, 0.96f)},
        {"Pink", new Color(0.93f, 0.29f, 0.60f)}, {"Cyan", new Color(0.02f, 0.71f, 0.83f)},
        {"Brown", new Color(0.47f, 0.33f, 0.28f)}, {"Black", new Color(0.12f, 0.12f, 0.12f)},
        {"White", new Color(0.95f, 0.95f, 0.95f)}, {"Grey", new Color(0.6f, 0.6f, 0.6f)}
    };

    private static readonly Dictionary<string, string> AnimalNames = new Dictionary<string, string>
    {
        {"Dog","\u05DB\u05DC\u05D1"}, {"Cat","\u05D7\u05EA\u05D5\u05DC"}, {"Bear","\u05D3\u05D5\u05D1"},
        {"Duck","\u05D1\u05E8\u05D5\u05D5\u05D6"}, {"Fish","\u05D3\u05D2"}, {"Frog","\u05E6\u05E4\u05E8\u05D3\u05E2"},
        {"Bird","\u05E6\u05D9\u05E4\u05D5\u05E8"}, {"Cow","\u05E4\u05E8\u05D4"}, {"Horse","\u05E1\u05D5\u05E1"},
        {"Lion","\u05D0\u05E8\u05D9\u05D4"}, {"Monkey","\u05E7\u05D5\u05E3"}, {"Elephant","\u05E4\u05D9\u05DC"},
        {"Giraffe","\u05D2\u05D9\u05E8\u05E4\u05D4"}, {"Zebra","\u05D6\u05D1\u05E8\u05D4"},
        {"Turtle","\u05E6\u05D1"}, {"Snake","\u05E0\u05D7\u05E9"}, {"Sheep","\u05DB\u05D1\u05E9\u05D4"},
        {"Chicken","\u05EA\u05E8\u05E0\u05D2\u05D5\u05DC"}, {"Donkey","\u05D7\u05DE\u05D5\u05E8"}
    };

    private static readonly Dictionary<string, string> ColorNames = new Dictionary<string, string>
    {
        {"Red","\u05D0\u05D3\u05D5\u05DD"}, {"Blue","\u05DB\u05D7\u05D5\u05DC"},
        {"Yellow","\u05E6\u05D4\u05D5\u05D1"}, {"Green","\u05D9\u05E8\u05D5\u05E7"},
        {"Orange","\u05DB\u05EA\u05D5\u05DD"}, {"Purple","\u05E1\u05D2\u05D5\u05DC"},
        {"Pink","\u05D5\u05E8\u05D5\u05D3"}, {"Cyan","\u05EA\u05DB\u05DC\u05EA"},
        {"Brown","\u05D7\u05D5\u05DD"}, {"Black","\u05E9\u05D7\u05D5\u05E8"},
        {"White","\u05DC\u05D1\u05DF"}, {"Grey","\u05D0\u05E4\u05D5\u05E8"}
    };

    private Dictionary<string, Sprite> _animalSprites;

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
            {
                // Swipe left (finger moves left) = next page
                // Swipe right (finger moves right) = prev page
                TurnPage(dx < 0 ? 1 : -1);
            }
        }
    }

    public void Open()
    {
        if (_isOpen || _isAnimating) return;
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
        BuildAnimalSpriteLookup();

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
        _bookRT.sizeDelta = new Vector2(1500, 820);
        var coverImg = bookGO.AddComponent<Image>();
        if (roundedRect != null) { coverImg.sprite = roundedRect; coverImg.type = Image.Type.Sliced; }
        // Use the child's chosen color for the book cover
        Color coverColor = BookCover;
        var profile = ProfileManager.ActiveProfile;
        if (profile != null && !string.IsNullOrEmpty(profile.avatarColorHex))
        {
            Color parsed;
            if (ColorUtility.TryParseHtmlString(profile.avatarColorHex, out parsed))
            {
                // Darken slightly for a rich book cover feel
                coverColor = new Color(parsed.r * 0.7f, parsed.g * 0.7f, parsed.b * 0.7f);
            }
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

        // Page dots (bottom center)
        var piGO = new GameObject("PageDots");
        piGO.transform.SetParent(bookGO.transform, false);
        var piRT = piGO.AddComponent<RectTransform>();
        piRT.anchorMin = new Vector2(0.5f, 0); piRT.anchorMax = new Vector2(0.5f, 0);
        piRT.pivot = new Vector2(0.5f, 1); piRT.anchoredPosition = new Vector2(0, -8);
        piRT.sizeDelta = new Vector2(200, 30);
        _pageIndicatorTMP = piGO.AddComponent<TextMeshProUGUI>();
        _pageIndicatorTMP.fontSize = 20; _pageIndicatorTMP.color = new Color(1, 1, 1, 0.7f);
        _pageIndicatorTMP.alignment = TextAlignmentOptions.Center; _pageIndicatorTMP.raycastTarget = false;

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

        // Nav arrows at bottom corners of book
        _prevButton = MakeNavButton(bookGO.transform, "PrevPage", "\u25C0",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, 20));
        _prevButton.onClick.AddListener(() => TurnPage(-1));

        _nextButton = MakeNavButton(bookGO.transform, "NextPage", "\u25B6",
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 20));
        _nextButton.onClick.AddListener(() => TurnPage(1));

        _overlayRoot.SetActive(false);
    }

    // ── Pages ──

    private void PopulatePages(int page)
    {
        ClearPage(_leftPageContent);
        ClearPage(_rightPageContent);

        var profile = ProfileManager.ActiveProfile;
        var jp = profile?.journey;
        var unlockedAnimals = new HashSet<string>(jp?.unlockedAnimalIds ?? new List<string>());
        var unlockedColors = new HashSet<string>(jp?.unlockedColorIds ?? new List<string>());

        if (page == 0)
        {
            // Animals — 12 left (4×3), 7 right
            HebrewText.SetText(_leftTitleTMP, "\u05D7\u05D9\u05D5\u05EA"); // חיות
            HebrewText.SetText(_rightTitleTMP, "\u05D7\u05D9\u05D5\u05EA");
            BuildAnimalGrid(_leftPageContent, 0, 12, unlockedAnimals, 4);
            BuildAnimalGrid(_rightPageContent, 12, 7, unlockedAnimals, 4);
        }
        else if (page == 1)
        {
            // Colors — 9 left (3×3), 3 right
            HebrewText.SetText(_leftTitleTMP, "\u05E6\u05D1\u05E2\u05D9\u05DD"); // צבעים
            HebrewText.SetText(_rightTitleTMP, "\u05E6\u05D1\u05E2\u05D9\u05DD");
            BuildColorGrid(_leftPageContent, 0, 9, unlockedColors);
            BuildColorGrid(_rightPageContent, 9, 3, unlockedColors);
        }
        else
        {
            // Sticker pages (dynamic) — 9 left + 9 right per spread
            HebrewText.SetText(_leftTitleTMP, "\u05DE\u05D3\u05D1\u05E7\u05D5\u05EA"); // מדבקות
            HebrewText.SetText(_rightTitleTMP, "\u05DE\u05D3\u05D1\u05E7\u05D5\u05EA");
            int stickerPage = page - 2;
            int start = stickerPage * StickersPerSpread;
            BuildStickerGrid(_leftPageContent, start, 9);
            BuildStickerGrid(_rightPageContent, start + 9, 9);
        }

        // Page dots: ● ○ ○
        string dots = "";
        for (int i = 0; i < TotalPages; i++)
            dots += (i == page) ? "\u25CF " : "\u25CB "; // ● ○
        _pageIndicatorTMP.text = dots.Trim();

        _prevButton.gameObject.SetActive(page > 0);
        _nextButton.gameObject.SetActive(page < TotalPages - 1);
    }

    private void PopulateLeftPage(int page)
    {
        ClearPage(_leftPageContent);
        var profile = ProfileManager.ActiveProfile;
        var jp = profile?.journey;
        var animals = new HashSet<string>(jp?.unlockedAnimalIds ?? new List<string>());
        var colors = new HashSet<string>(jp?.unlockedColorIds ?? new List<string>());

        switch (page)
        {
            case 0:
                HebrewText.SetText(_leftTitleTMP, "\u05D7\u05D9\u05D5\u05EA");
                BuildAnimalGrid(_leftPageContent, 0, 12, animals, 4); break;
            case 1:
                HebrewText.SetText(_leftTitleTMP, "\u05E6\u05D1\u05E2\u05D9\u05DD");
                BuildColorGrid(_leftPageContent, 0, 9, colors); break;
            case 2:
                HebrewText.SetText(_leftTitleTMP, "\u05DE\u05D3\u05D1\u05E7\u05D5\u05EA");
                BuildStickerGrid(_leftPageContent, 0, 9); break;
        }
    }

    private void PopulateRightPage(int page)
    {
        ClearPage(_rightPageContent);
        var profile = ProfileManager.ActiveProfile;
        var jp = profile?.journey;
        var animals = new HashSet<string>(jp?.unlockedAnimalIds ?? new List<string>());
        var colors = new HashSet<string>(jp?.unlockedColorIds ?? new List<string>());

        switch (page)
        {
            case 0:
                HebrewText.SetText(_rightTitleTMP, "\u05D7\u05D9\u05D5\u05EA");
                BuildAnimalGrid(_rightPageContent, 12, 7, animals, 4); break;
            case 1:
                HebrewText.SetText(_rightTitleTMP, "\u05E6\u05D1\u05E2\u05D9\u05DD");
                BuildColorGrid(_rightPageContent, 9, 3, colors); break;
            case 2:
                HebrewText.SetText(_rightTitleTMP, "\u05DE\u05D3\u05D1\u05E7\u05D5\u05EA");
                BuildStickerGrid(_rightPageContent, 9, 3); break;
        }
    }

    private void BuildAnimalGrid(RectTransform parent, int start, int count, HashSet<string> unlocked, int cols)
    {
        var gridGO = new GameObject("Grid");
        gridGO.transform.SetParent(parent, false);
        Stretch(gridGO.AddComponent<RectTransform>());
        var grid = gridGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(155, 195);
        grid.spacing = new Vector2(4, 2);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = cols;

        for (int i = start; i < start + count && i < AllAnimalIds.Length; i++)
        {
            string id = AllAnimalIds[i];
            bool found = unlocked.Contains(id);

            var cell = new GameObject($"A_{id}");
            cell.transform.SetParent(gridGO.transform, false);
            var layout = cell.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 0; layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            layout.childControlWidth = true; layout.childControlHeight = true;

            var imgGO = new GameObject("Img");
            imgGO.transform.SetParent(cell.transform, false);
            var img = imgGO.AddComponent<Image>();
            img.preserveAspect = true; img.raycastTarget = false;
            imgGO.AddComponent<LayoutElement>().preferredHeight = 155;

            Sprite spr = null;
            if (_animalSprites != null) _animalSprites.TryGetValue(id.ToLower(), out spr);

            if (found && spr != null)
            {
                img.sprite = spr;
                img.color = Color.white;
            }
            else if (spr != null)
            {
                img.sprite = spr;
                img.color = SilhouetteColor; // fully grey, opaque
            }
            else
            {
                if (circleSprite != null) img.sprite = circleSprite;
                img.color = SilhouetteColor;
            }

            string label = found ? (AnimalNames.ContainsKey(id) ? AnimalNames[id] : id) : "?";
            var tmp = AddTMP(cell.transform, "", 16, found ? LabelColor : SilhouetteColor);
            if (found) HebrewText.SetText(tmp, label); else tmp.text = "?";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
        }
    }

    private void BuildColorGrid(RectTransform parent, int start, int count, HashSet<string> unlocked)
    {
        var gridGO = new GameObject("Grid");
        gridGO.transform.SetParent(parent, false);
        Stretch(gridGO.AddComponent<RectTransform>());
        var grid = gridGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(180, 190);
        grid.spacing = new Vector2(10, 10);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;

        for (int i = start; i < start + count && i < AllColorIds.Length; i++)
        {
            string id = AllColorIds[i];
            bool found = unlocked.Contains(id);

            var cell = new GameObject($"C_{id}");
            cell.transform.SetParent(gridGO.transform, false);
            var layout = cell.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6; layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            layout.childControlWidth = true; layout.childControlHeight = true;

            // Force circle: use circleSprite, set equal width/height
            var circGO = new GameObject("Circle");
            circGO.transform.SetParent(cell.transform, false);
            var circImg = circGO.AddComponent<Image>();
            if (circleSprite != null) circImg.sprite = circleSprite;
            circImg.raycastTarget = false;
            var circLE = circGO.AddComponent<LayoutElement>();
            circLE.preferredWidth = 120;
            circLE.preferredHeight = 120;

            if (found)
            {
                Color c;
                circImg.color = ColorMap.TryGetValue(id, out c) ? c : Color.gray;
            }
            else
            {
                circImg.color = SilhouetteColor;
            }

            circGO.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.12f);

            string label = found ? (ColorNames.ContainsKey(id) ? ColorNames[id] : id) : "?";
            var tmp = AddTMP(cell.transform, "", 22, found ? LabelColor : SilhouetteColor);
            if (found) HebrewText.SetText(tmp, label); else tmp.text = "?";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
        }
    }

    private void BuildStickerGrid(RectTransform parent, int start, int count)
    {
        var gridGO = new GameObject("Grid");
        gridGO.transform.SetParent(parent, false);
        Stretch(gridGO.AddComponent<RectTransform>());
        var grid = gridGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(180, 190);
        grid.spacing = new Vector2(8, 8);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;

        int stickerCount = (stickerSprites != null && stickerSprites.Length > 0) ? stickerSprites.Length : 12;
        if (stickerSprites == null || stickerSprites.Length == 0)
            Debug.LogWarning("[Album] No sticker sprites loaded — run Setup World Scene");

        // Get collected stickers from profile
        var profile = ProfileManager.ActiveProfile;
        var collected = profile?.journey?.collectedStickerIds ?? new List<string>();

        for (int i = start; i < start + count && i < stickerCount; i++)
        {
            string stickerId = $"sticker_{i}";
            bool isCollected = collected.Contains(stickerId);

            var cell = new GameObject($"S_{i}");
            cell.transform.SetParent(gridGO.transform, false);
            var layout = cell.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 2; layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            layout.childControlWidth = true; layout.childControlHeight = true;

            var imgGO = new GameObject("Img");
            imgGO.transform.SetParent(cell.transform, false);
            var img = imgGO.AddComponent<Image>();
            img.preserveAspect = true; img.raycastTarget = false;
            imgGO.AddComponent<LayoutElement>().preferredHeight = 150;

            if (stickerSprites != null && i < stickerSprites.Length && stickerSprites[i] != null)
            {
                img.sprite = stickerSprites[i];
                img.color = isCollected ? Color.white : SilhouetteColor;
            }
            else
            {
                if (circleSprite != null) img.sprite = circleSprite;
                img.color = SilhouetteColor;
            }

            var tmp = AddTMP(cell.transform, isCollected ? "" : "?", 18,
                isCollected ? TitleColor : SilhouetteColor);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
        }
    }

    // ── Page Turn ──

    private void TurnPage(int dir)
    {
        int target = _currentPage + dir;
        if (target < 0 || target >= TotalPages || _isAnimating) return;
        StartCoroutine(PageTurnAnimation(dir, target));
    }

    private IEnumerator PageTurnAnimation(int dir, int targetPage)
    {
        _isAnimating = true;

        bool goingRight = dir > 0;
        var turnImg = _pageTurnOverlay.GetComponent<Image>();
        float halfDur = 0.25f;

        // ── Like a real book: page lifts from one side, crosses spine, lands on other side ──
        // Phase 1: Page folds from the source side toward the spine.
        //          New content on that side is underneath, gets revealed.
        // Phase 2: Page crosses spine and folds onto the other side, covering old content.
        //          When it lands, content underneath switches to new, then page lifts off.

        if (goingRight)
        {
            // ── PHASE 1: Right page folds toward spine, revealing new right content ──
            // Place new right content underneath
            PopulateRightPage(targetPage);
            // Overlay covers right half, pivots at spine
            SetTurnOverlay(new Vector2(0.5f, 0), Vector2.one, new Vector2(0, 0.5f));
            _pageTurnOverlay.localScale = Vector3.one;
            turnImg.color = PageColor;
            _pageTurnOverlay.gameObject.SetActive(true);

            // Fold: scaleX 1 → 0 (reveals new right page)
            yield return AnimateFold(turnImg, 1f, 0f, halfDur);

            // ── PHASE 2: Page lands on left side ──
            // Pivot at spine — page unfolds from spine outward onto left page
            SetTurnOverlay(Vector2.zero, new Vector2(0.5f, 1), new Vector2(1, 0.5f));
            yield return AnimateFold(turnImg, 0f, 1f, halfDur);

            // Page has landed — switch content and remove overlay
            PopulateLeftPage(targetPage);
        }
        else
        {
            // ── PHASE 1: Left page folds toward spine, revealing new left content ──
            PopulateLeftPage(targetPage);
            SetTurnOverlay(Vector2.zero, new Vector2(0.5f, 1), new Vector2(1, 0.5f));
            _pageTurnOverlay.localScale = Vector3.one;
            turnImg.color = PageColor;
            _pageTurnOverlay.gameObject.SetActive(true);

            yield return AnimateFold(turnImg, 1f, 0f, halfDur);

            // ── PHASE 2: Page lands on right side ──
            // Pivot at spine — page unfolds from spine outward onto right page
            SetTurnOverlay(new Vector2(0.5f, 0), Vector2.one, new Vector2(0, 0.5f));
            yield return AnimateFold(turnImg, 0f, 1f, halfDur);

            // Page has landed — switch content and remove overlay
            PopulateRightPage(targetPage);
        }

        _currentPage = targetPage;
        // Refresh nav buttons and page dots
        _prevButton.gameObject.SetActive(_currentPage > 0);
        _nextButton.gameObject.SetActive(_currentPage < TotalPages - 1);
        string dots = "";
        for (int i = 0; i < TotalPages; i++)
            dots += (i == _currentPage) ? "\u25CF " : "\u25CB ";
        _pageIndicatorTMP.text = dots.Trim();

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
            // Ease: smooth in-out
            e = e * e * (3f - 2f * e);
            float sx = Mathf.Lerp(fromScale, toScale, e);
            _pageTurnOverlay.localScale = new Vector3(sx, 1, 1);
            // Darken as it folds flat, brighten as it opens
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

    private void BuildAnimalSpriteLookup()
    {
        if (_animalSprites != null) return;
        _animalSprites = new Dictionary<string, Sprite>();
        if (gameDatabase == null) return;
        foreach (var game in gameDatabase.games)
        {
            if (game.subItems == null) continue;
            foreach (var sub in game.subItems)
            {
                if (sub.thumbnail == null && sub.contentAsset == null) continue;
                string key = sub.categoryKey;
                if (string.IsNullOrEmpty(key)) continue;
                string lower = key.ToLower();
                if (!_animalSprites.ContainsKey(lower))
                    _animalSprites[lower] = sub.thumbnail != null ? sub.thumbnail : sub.contentAsset;
            }
        }
    }

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
