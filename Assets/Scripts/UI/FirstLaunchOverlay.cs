using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Full-screen onboarding overlay shown ONCE on first app install.
/// Displays 3 swipeable slides explaining the app to parents,
/// then dismisses forever via a "Let's start" button on the last slide.
/// Hooks into ProfileSelectionController — call TryShow() from Start().
/// </summary>
public class FirstLaunchOverlay : MonoBehaviour
{
    private const string PrefKey = "first_launch_shown";
    private const int SlideCount = 3;

    // Slide data
    private static readonly string[] Icons = { "\U0001F3AE", "\U0001F4CA", "\U0001F512" };
    private static readonly string[] Titles =
    {
        "למעלה מ-30 משחקים",
        "אזור הורים",
        "בטוח לילדים"
    };
    private static readonly string[] Subtitles =
    {
        "משחקים לימודיים מגוונים לגילאי 2-5",
        "עקבו אחרי ההתקדמות, שנו רמת קושי ושתפו הישגים",
        "ללא פרסומות בזמן המשחק, תוכן מותאם לגיל"
    };

    // Runtime references
    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private RectTransform _slidesParent;
    private Image[] _dots;
    private GameObject _startButton;
    private int _currentSlide;

    // Swipe detection
    private Vector2 _swipeStart;
    private bool _isSwiping;
    private bool _isTransitioning;
    private bool _isFirstLaunch;

    // Colors
    private static readonly Color DotActive = new Color(1f, 1f, 1f, 1f);
    private static readonly Color DotInactive = new Color(1f, 1f, 1f, 0.35f);
    private static readonly Color BgSlide0 = HexColor("#5C6BC0"); // indigo
    private static readonly Color BgSlide1 = HexColor("#26A69A"); // teal
    private static readonly Color BgSlide2 = HexColor("#EF5350"); // red-ish

    private static readonly Color[] SlideBgColors = { BgSlide0, BgSlide1, BgSlide2 };

    private Image _bgImage;

    /// <summary>
    /// Call from ProfileSelectionController.Start().
    /// Shows only on first launch. Returns true if shown.
    /// </summary>
    public static bool TryShow(Transform canvasRoot)
    {
        if (PlayerPrefs.GetInt(PrefKey, 0) == 1)
            return false;

        Show(canvasRoot, isFirstLaunch: true);
        return true;
    }

    /// <summary>
    /// Show the overlay (always, regardless of first launch).
    /// Called from the info button.
    /// </summary>
    public static void Show(Transform canvasRoot, bool isFirstLaunch = false)
    {
        var go = new GameObject("FirstLaunchOverlay");
        go.transform.SetParent(canvasRoot, false);
        go.transform.SetAsLastSibling();
        var overlay = go.AddComponent<FirstLaunchOverlay>();
        overlay._isFirstLaunch = isFirstLaunch;
        overlay.Build(canvasRoot);
    }

    private void Build(Transform canvasRoot)
    {
        // Full-screen RectTransform
        var rt = gameObject.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // CanvasGroup for fade-out
        _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Semi-transparent background
        _bgImage = gameObject.AddComponent<Image>();
        _bgImage.color = SlideBgColors[0];
        _bgImage.raycastTarget = true;

        // Build slide content area (centered)
        var contentGO = CreateChild("Content", gameObject.transform);
        var contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0.1f, 0.15f);
        contentRT.anchorMax = new Vector2(0.9f, 0.85f);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;
        _slidesParent = contentRT;

        // Create slides
        for (int i = 0; i < SlideCount; i++)
            CreateSlide(i, contentRT);

        // Show only first slide
        ShowSlide(0, immediate: true);

        // Dots navigation at bottom
        CreateDots(rt);

        // Start button (hidden until last slide)
        CreateStartButton(rt);

        // Fade in
        _canvasGroup.alpha = 0f;
        StartCoroutine(FadeIn());
    }

    private void CreateSlide(int index, RectTransform parent)
    {
        var slideGO = CreateChild($"Slide_{index}", parent);
        var slideRT = slideGO.GetComponent<RectTransform>();
        slideRT.anchorMin = Vector2.zero;
        slideRT.anchorMax = Vector2.one;
        slideRT.offsetMin = Vector2.zero;
        slideRT.offsetMax = Vector2.zero;

        // Icon (emoji)
        var iconGO = CreateChild("Icon", slideRT);
        var iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.5f, 0.65f);
        iconRT.anchorMax = new Vector2(0.5f, 0.65f);
        iconRT.sizeDelta = new Vector2(120, 120);
        var iconText = iconGO.AddComponent<TextMeshProUGUI>();
        iconText.text = Icons[index];
        iconText.fontSize = 72;
        iconText.alignment = TextAlignmentOptions.Center;
        iconText.raycastTarget = false;

        // Title
        var titleGO = CreateChild("Title", slideRT);
        var titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 0.45f);
        titleRT.anchorMax = new Vector2(0.5f, 0.45f);
        titleRT.sizeDelta = new Vector2(600, 60);
        var titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.fontSize = 42;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        titleText.fontStyle = FontStyles.Bold;
        titleText.raycastTarget = false;
        HebrewText.SetText(titleText, Titles[index]);

        // Subtitle
        var subGO = CreateChild("Subtitle", slideRT);
        var subRT = subGO.GetComponent<RectTransform>();
        subRT.anchorMin = new Vector2(0.5f, 0.32f);
        subRT.anchorMax = new Vector2(0.5f, 0.32f);
        subRT.sizeDelta = new Vector2(600, 80);
        var subText = subGO.AddComponent<TextMeshProUGUI>();
        subText.fontSize = 28;
        subText.alignment = TextAlignmentOptions.Center;
        subText.color = new Color(1f, 1f, 1f, 0.85f);
        subText.raycastTarget = false;
        HebrewText.SetText(subText, Subtitles[index]);

        slideGO.SetActive(index == 0);
    }

    private void CreateDots(RectTransform parent)
    {
        var dotsGO = CreateChild("Dots", parent);
        var dotsRT = dotsGO.GetComponent<RectTransform>();
        dotsRT.anchorMin = new Vector2(0.5f, 0.1f);
        dotsRT.anchorMax = new Vector2(0.5f, 0.1f);
        dotsRT.sizeDelta = new Vector2(120, 20);

        var hlg = dotsGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        _dots = new Image[SlideCount];
        for (int i = 0; i < SlideCount; i++)
        {
            var dotGO = CreateChild($"Dot_{i}", dotsRT);
            var dotLE = dotGO.AddComponent<LayoutElement>();
            dotLE.preferredWidth = 14;
            dotLE.preferredHeight = 14;

            var dotImg = dotGO.AddComponent<Image>();
            dotImg.color = i == 0 ? DotActive : DotInactive;

            // Make circular — no sprite needed, just use a white circle approach
            // We'll make it look circular by using the default UI sprite
            _dots[i] = dotImg;

            // Add click handler for dot navigation
            int slideIndex = i;
            var dotBtn = dotGO.AddComponent<Button>();
            dotBtn.targetGraphic = dotImg;
            var colors = dotBtn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            dotBtn.colors = colors;
            dotBtn.onClick.AddListener(() => GoToSlide(slideIndex));
        }
    }

    private void CreateStartButton(RectTransform parent)
    {
        _startButton = CreateChild("StartButton", parent);
        var btnRT = _startButton.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0.5f, 0.1f);
        btnRT.anchorMax = new Vector2(0.5f, 0.1f);
        btnRT.sizeDelta = new Vector2(300, 70);

        var btnImg = _startButton.AddComponent<Image>();
        btnImg.color = Color.white;

        var btnComp = _startButton.AddComponent<Button>();
        btnComp.targetGraphic = btnImg;
        btnComp.onClick.AddListener(OnStartPressed);

        var textGO = CreateChild("Text", btnRT);
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        var btnText = textGO.AddComponent<TextMeshProUGUI>();
        btnText.fontSize = 32;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color = BgSlide2;
        btnText.fontStyle = FontStyles.Bold;
        btnText.raycastTarget = false;
        HebrewText.SetText(btnText, "!בואו נתחיל");

        _startButton.SetActive(false);
    }

    // ── Navigation ──

    private void GoToSlide(int index)
    {
        if (_isTransitioning || index == _currentSlide || index < 0 || index >= SlideCount) return;
        StartCoroutine(TransitionToSlide(index));
    }

    private void ShowSlide(int index, bool immediate = false)
    {
        _currentSlide = index;

        for (int i = 0; i < SlideCount; i++)
        {
            if (_slidesParent.childCount > i)
                _slidesParent.GetChild(i).gameObject.SetActive(i == index);
        }

        // Update dots
        if (_dots != null)
        {
            for (int i = 0; i < _dots.Length; i++)
            {
                if (_dots[i] != null)
                    _dots[i].color = i == index ? DotActive : DotInactive;
            }
        }

        // Show start button only on last slide
        if (_startButton != null)
            _startButton.SetActive(index == SlideCount - 1);

        // Update background color
        if (_bgImage != null && !immediate)
            _bgImage.color = SlideBgColors[index];
    }

    private IEnumerator TransitionToSlide(int index)
    {
        _isTransitioning = true;

        // Quick fade
        float dur = 0.25f;
        float elapsed = 0f;

        Color startColor = _bgImage.color;
        Color endColor = SlideBgColors[index];

        // Fade out current content
        CanvasGroup slideGroup = GetOrAddSlideCanvasGroup(_currentSlide);
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            if (slideGroup != null) slideGroup.alpha = 1f - t;
            _bgImage.color = Color.Lerp(startColor, endColor, t);
            yield return null;
        }

        ShowSlide(index);

        // Fade in new content
        CanvasGroup newSlideGroup = GetOrAddSlideCanvasGroup(index);
        if (newSlideGroup != null) newSlideGroup.alpha = 0f;
        elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            if (newSlideGroup != null) newSlideGroup.alpha = t;
            yield return null;
        }
        if (newSlideGroup != null) newSlideGroup.alpha = 1f;

        _isTransitioning = false;
    }

    private CanvasGroup GetOrAddSlideCanvasGroup(int index)
    {
        if (_slidesParent == null || index < 0 || index >= _slidesParent.childCount) return null;
        var go = _slidesParent.GetChild(index).gameObject;
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }

    // ── Swipe Detection ──

    private void Update()
    {
        if (_isTransitioning) return;

        // Touch / mouse swipe detection
        if (Input.GetMouseButtonDown(0))
        {
            _swipeStart = Input.mousePosition;
            _isSwiping = true;
        }
        else if (Input.GetMouseButtonUp(0) && _isSwiping)
        {
            _isSwiping = false;
            Vector2 swipeEnd = Input.mousePosition;
            float dx = swipeEnd.x - _swipeStart.x;
            float dy = swipeEnd.y - _swipeStart.y;

            // Only count horizontal swipes with sufficient magnitude
            if (Mathf.Abs(dx) > 50f && Mathf.Abs(dx) > Mathf.Abs(dy))
            {
                // RTL: swipe left = next, swipe right = prev
                if (dx < 0)
                    GoToSlide(_currentSlide + 1);
                else
                    GoToSlide(_currentSlide - 1);
            }
        }
    }

    // ── Actions ──

    private void OnStartPressed()
    {
        if (_isFirstLaunch)
        {
            PlayerPrefs.SetInt(PrefKey, 1);
            PlayerPrefs.Save();
        }
        StartCoroutine(DismissAndDestroy());
    }

    private IEnumerator FadeIn()
    {
        float elapsed = 0f;
        float dur = 0.4f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Clamp01(elapsed / dur);
            yield return null;
        }
        _canvasGroup.alpha = 1f;
    }

    private IEnumerator DismissAndDestroy()
    {
        float elapsed = 0f;
        float dur = 0.35f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / dur);
            yield return null;
        }
        Destroy(gameObject);
    }

    // ── Helpers ──

    private static GameObject CreateChild(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(100, 100);
        return go;
    }

    private static Color HexColor(string hex)
    {
        Color c;
        ColorUtility.TryParseHtmlString(hex, out c);
        return c;
    }
}
