using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Unified game completion panel. Self-creating singleton (like ConfettiController).
/// Shows 1-3 stars based on accuracy, Play Again and Home buttons.
/// Call Show() after game completion — blocks until the player taps a button.
/// </summary>
public class SuccessPanel : MonoBehaviour
{
    private static SuccessPanel _instance;

    private Canvas _canvas;
    private CanvasGroup _panelGroup;
    private RectTransform _panelRect;
    private Image[] _stars;
    private Button _playAgainButton;
    private Button _homeButton;
    private Action _onPlayAgain;
    private Action _onHome;
    private bool _buttonPressed;

    // Star thresholds: 0 mistakes = 3 stars, <=2 = 2 stars, else 1
    private const int PerfectThreshold = 0;
    private const int GoodThreshold = 2;

    // Colors
    private static readonly Color StarActive = new Color(1f, 0.84f, 0f);       // gold
    private static readonly Color StarInactive = new Color(0.85f, 0.85f, 0.85f, 0.4f); // faded gray
    private static readonly Color PanelBg = new Color(0f, 0f, 0f, 0.55f);      // semi-transparent overlay
    private static readonly Color CardBg = new Color(1f, 1f, 1f, 0.95f);
    private static readonly Color BtnPlayAgain = new Color(0.30f, 0.69f, 0.31f); // green
    private static readonly Color BtnHome = new Color(0.38f, 0.71f, 0.91f);      // blue
    private static readonly Color BtnTextColor = Color.white;

    public static SuccessPanel Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("SuccessPanel");
                _instance = go.AddComponent<SuccessPanel>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        _canvas.gameObject.SetActive(false);

        SceneManager.sceneLoaded += (_, __) => Hide();
    }

    /// <summary>
    /// Show the success panel. Returns a coroutine that waits until the player
    /// taps Play Again or Home. The chosen action fires after the panel hides.
    /// </summary>
    public IEnumerator Show(int mistakes, Action playAgainCallback, Action homeCallback)
    {
        _onPlayAgain = playAgainCallback;
        _onHome = homeCallback;
        _buttonPressed = false;

        int starCount = mistakes <= PerfectThreshold ? 3
                      : mistakes <= GoodThreshold ? 2
                      : 1;

        // Reset stars
        for (int i = 0; i < _stars.Length; i++)
        {
            _stars[i].color = StarInactive;
            _stars[i].transform.localScale = Vector3.zero;
        }

        _panelGroup.alpha = 0f;
        _panelGroup.interactable = false;
        _panelGroup.blocksRaycasts = true;
        _canvas.gameObject.SetActive(true);

        // Fade in overlay
        yield return FadePanel(0f, 1f, 0.3f);

        // Pop in earned stars one by one
        for (int i = 0; i < starCount; i++)
        {
            _stars[i].color = StarActive;
            yield return PopIn(_stars[i].transform, 0.25f);
            yield return new WaitForSeconds(0.1f);
        }

        // Enable buttons
        _panelGroup.interactable = true;

        // Wait for button press
        while (!_buttonPressed)
            yield return null;

        // Fade out
        _panelGroup.interactable = false;
        yield return FadePanel(1f, 0f, 0.2f);
        _canvas.gameObject.SetActive(false);
    }

    /// <summary>Force-hide without animation (e.g. scene change).</summary>
    public void Hide()
    {
        if (_canvas != null)
            _canvas.gameObject.SetActive(false);
        _buttonPressed = true;
    }

    // ── UI Construction ────────────────────────────────────────────

    private void BuildUI()
    {
        // Overlay canvas (above confetti)
        var canvasGO = new GameObject("SuccessCanvas");
        canvasGO.transform.SetParent(transform, false);

        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 1000; // Above confetti (999)

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        var canvasRT = canvasGO.GetComponent<RectTransform>();

        // ── Full-screen dark overlay ──
        var overlay = CreateImage(canvasRT, "Overlay", PanelBg);
        Stretch(overlay);
        _panelGroup = overlay.gameObject.AddComponent<CanvasGroup>();

        // ── Center card ──
        var card = CreateImage(overlay.rectTransform, "Card", CardBg);
        var cardRT = card.rectTransform;
        cardRT.anchorMin = new Vector2(0.5f, 0.5f);
        cardRT.anchorMax = new Vector2(0.5f, 0.5f);
        cardRT.pivot = new Vector2(0.5f, 0.5f);
        cardRT.sizeDelta = new Vector2(580, 380);
        cardRT.anchoredPosition = Vector2.zero;

        // Round the card corners (use RoundedRect sprite if available)
        var roundedSprite = Resources.Load<Sprite>("UI/RoundedRect");
        if (roundedSprite != null)
        {
            card.sprite = roundedSprite;
            card.type = Image.Type.Sliced;
        }

        // ── Stars row ──
        var starsRow = new GameObject("Stars").AddComponent<RectTransform>();
        starsRow.SetParent(cardRT, false);
        starsRow.anchorMin = new Vector2(0.5f, 1f);
        starsRow.anchorMax = new Vector2(0.5f, 1f);
        starsRow.pivot = new Vector2(0.5f, 1f);
        starsRow.anchoredPosition = new Vector2(0, -40f);
        starsRow.sizeDelta = new Vector2(360, 100);

        var hlg = starsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 20f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        _stars = new Image[3];
        var starSprite = GenerateStarSprite();
        for (int i = 0; i < 3; i++)
        {
            var starImg = CreateImage(starsRow, $"Star_{i}", StarInactive);
            starImg.rectTransform.sizeDelta = new Vector2(90, 90);
            starImg.sprite = starSprite;
            starImg.type = Image.Type.Simple;
            starImg.preserveAspect = true;
            starImg.raycastTarget = false;
            _stars[i] = starImg;
        }

        // ── Buttons row ──
        var btnRow = new GameObject("Buttons").AddComponent<RectTransform>();
        btnRow.SetParent(cardRT, false);
        btnRow.anchorMin = new Vector2(0.5f, 0f);
        btnRow.anchorMax = new Vector2(0.5f, 0f);
        btnRow.pivot = new Vector2(0.5f, 0f);
        btnRow.anchoredPosition = new Vector2(0, 40f);
        btnRow.sizeDelta = new Vector2(480, 80);

        var btnHlg = btnRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        btnHlg.spacing = 30f;
        btnHlg.childAlignment = TextAnchor.MiddleCenter;
        btnHlg.childControlWidth = false;
        btnHlg.childControlHeight = false;
        btnHlg.childForceExpandWidth = false;
        btnHlg.childForceExpandHeight = false;

        _homeButton = CreateButton(btnRow, "HomeBtn", BtnHome, 200, 70);
        _playAgainButton = CreateButton(btnRow, "PlayAgainBtn", BtnPlayAgain, 200, 70);

        // Set Hebrew labels
        SetButtonLabel(_homeButton, "\u05D1\u05D9\u05EA");        // בית
        SetButtonLabel(_playAgainButton, "\u05E9\u05D5\u05D1");   // שוב

        _homeButton.onClick.AddListener(OnHomeTapped);
        _playAgainButton.onClick.AddListener(OnPlayAgainTapped);
    }

    private void OnPlayAgainTapped()
    {
        if (_buttonPressed) return;
        _buttonPressed = true;
        _onPlayAgain?.Invoke();
    }

    private void OnHomeTapped()
    {
        if (_buttonPressed) return;
        _buttonPressed = true;
        _onHome?.Invoke();
    }

    // ── Animation Helpers ──────────────────────────────────────────

    private IEnumerator FadePanel(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            _panelGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        _panelGroup.alpha = to;
    }

    private IEnumerator PopIn(Transform target, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            // Overshoot bounce
            float scale = 1f + 0.3f * Mathf.Sin(p * Mathf.PI);
            if (p >= 1f) scale = 1f;
            target.localScale = Vector3.one * scale;
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    // ── Sprite Generation ──────────────────────────────────────────

    private static Sprite _cachedStarSprite;

    private static Sprite GenerateStarSprite()
    {
        if (_cachedStarSprite != null) return _cachedStarSprite;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        float cx = size * 0.5f, cy = size * 0.5f;

        // 5-point star
        const int points = 5;
        float outerR = size * 0.48f;
        float innerR = outerR * 0.38f;
        float rotOffset = -Mathf.PI * 0.5f; // point upward

        // Build star polygon vertices
        var verts = new Vector2[points * 2];
        for (int i = 0; i < points * 2; i++)
        {
            float angle = rotOffset + i * Mathf.PI / points;
            float r = (i % 2 == 0) ? outerR : innerR;
            verts[i] = new Vector2(cx + Mathf.Cos(angle) * r, cy + Mathf.Sin(angle) * r);
        }

        // Fill pixels using point-in-polygon test
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool inside = PointInPolygon(x + 0.5f, y + 0.5f, verts);
                pixels[y * size + x] = inside ? Color.white : Color.clear;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;

        _cachedStarSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return _cachedStarSprite;
    }

    private static bool PointInPolygon(float px, float py, Vector2[] verts)
    {
        bool inside = false;
        int n = verts.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            if (((verts[i].y > py) != (verts[j].y > py)) &&
                (px < (verts[j].x - verts[i].x) * (py - verts[i].y) / (verts[j].y - verts[i].y) + verts[i].x))
                inside = !inside;
        }
        return inside;
    }

    // ── UI Factory Helpers ─────────────────────────────────────────

    private static Image CreateImage(RectTransform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = true;
        return img;
    }

    private static void Stretch(Image img)
    {
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    private static void SetButtonLabel(Button btn, string hebrewText)
    {
        var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
            HebrewText.SetText(tmp, hebrewText);
    }

    private static Button CreateButton(RectTransform parent, string name, Color bgColor, float w, float h)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);

        var img = go.AddComponent<Image>();
        img.color = bgColor;
        var roundedSprite = Resources.Load<Sprite>("UI/RoundedRect");
        if (roundedSprite != null)
        {
            img.sprite = roundedSprite;
            img.type = Image.Type.Sliced;
        }

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.95f, 0.95f, 0.95f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
        btn.colors = colors;

        // Label
        var textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;
        textRT.anchoredPosition = Vector2.zero;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "";
        tmp.color = BtnTextColor;
        tmp.fontSize = 36;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        return btn;
    }
}
