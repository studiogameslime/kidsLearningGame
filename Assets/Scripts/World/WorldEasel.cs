using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interactive painting easel in the World scene.
/// Tapping opens a gallery panel that expands from the easel canvas.
/// Reuses saved drawings from the player profile.
/// </summary>
public class WorldEasel : MonoBehaviour
{
    [Header("Gallery Overlay")]
    public GameObject overlayRoot;       // full-screen overlay (starts inactive)
    public Image dimBackground;          // semi-transparent dim
    public RectTransform panelRT;        // the gallery panel
    public RectTransform gridContainer;  // grid content inside scroll view
    public Button closeButton;
    public GameObject emptyText;

    [Header("Sprites")]
    public Sprite roundedRectSprite;

    [Header("New Drawing")]
    public Button newDrawingButton;
    public GameDatabase gameDatabase;

    [Header("Settings")]
    public float thumbnailSize = 280f;

    private RectTransform rt;
    private bool isOpen;
    private bool isAnimating;
    private List<Texture2D> loadedTextures = new List<Texture2D>();

    // Fullscreen view
    private GameObject fullscreenPanel;
    private RawImage fullscreenImage;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    private void Start()
    {
        if (overlayRoot != null)
            overlayRoot.SetActive(false);
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
        if (newDrawingButton != null)
            newDrawingButton.onClick.AddListener(OnNewDrawing);
        if (dimBackground != null)
        {
            var dimBtn = dimBackground.GetComponent<Button>();
            if (dimBtn == null) dimBtn = dimBackground.gameObject.AddComponent<Button>();
            dimBtn.targetGraphic = dimBackground;
            dimBtn.onClick.AddListener(Close);
        }

        StartCoroutine(IdleSway());
    }

    public void OnTap()
    {
        if (isAnimating || isOpen) return;
        StartCoroutine(OpenSequence());
    }

    private IEnumerator OpenSequence()
    {
        isAnimating = true;

        // Bounce the easel
        yield return BounceAnim();

        // Load drawings if not loaded yet
        if (loadedTextures.Count == 0)
            LoadDrawings();

        // Show overlay and animate panel expanding
        if (overlayRoot != null)
            overlayRoot.SetActive(true);

        // Fade in dim
        if (dimBackground != null)
            dimBackground.color = new Color(0, 0, 0, 0);

        // Start panel small
        if (panelRT != null)
            panelRT.localScale = Vector3.one * 0.05f;

        float dur = 0.4f;
        float elapsed = 0f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            // EaseOutBack for playful overshoot
            float scale = EaseOutBack(t);

            if (panelRT != null)
                panelRT.localScale = Vector3.one * scale;

            if (dimBackground != null)
                dimBackground.color = new Color(0, 0, 0, 0.45f * Mathf.Clamp01(t * 2f));

            yield return null;
        }

        if (panelRT != null)
            panelRT.localScale = Vector3.one;
        if (dimBackground != null)
            dimBackground.color = new Color(0, 0, 0, 0.45f);

        isOpen = true;
        isAnimating = false;
    }

    public void Close()
    {
        if (isAnimating || !isOpen) return;
        StartCoroutine(CloseSequence());
    }

    private IEnumerator CloseSequence()
    {
        isAnimating = true;

        // Close fullscreen if open
        if (fullscreenPanel != null)
            fullscreenPanel.SetActive(false);

        float dur = 0.3f;
        float elapsed = 0f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float scale = 1f - EaseInBack(t);

            if (panelRT != null)
                panelRT.localScale = Vector3.one * Mathf.Max(0f, scale);

            if (dimBackground != null)
                dimBackground.color = new Color(0, 0, 0, 0.45f * (1f - t));

            yield return null;
        }

        if (overlayRoot != null)
            overlayRoot.SetActive(false);

        isOpen = false;
        isAnimating = false;
    }

    private void LoadDrawings()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null || profile.savedDrawings == null)
        {
            if (emptyText != null) emptyText.SetActive(true);
            return;
        }

        string basePath = Application.persistentDataPath;

        // Newest first
        for (int i = profile.savedDrawings.Count - 1; i >= 0; i--)
        {
            var drawing = profile.savedDrawings[i];
            string fullPath = Path.Combine(basePath, drawing.imagePath);

            if (!File.Exists(fullPath)) continue;

            byte[] data = File.ReadAllBytes(fullPath);
            var tex = new Texture2D(2, 2);
            if (!tex.LoadImage(data)) continue;

            loadedTextures.Add(tex);
            CreateThumbnail(tex);
        }

        if (emptyText != null)
            emptyText.SetActive(loadedTextures.Count == 0);
    }

    private void OnNewDrawing()
    {
        if (gameDatabase == null) return;

        // Find the Coloring game in the database
        GameItemData coloringGame = null;
        foreach (var game in gameDatabase.games)
        {
            if (game.targetSceneName == "ColoringGame" || game.id == "Coloring")
            {
                coloringGame = game;
                break;
            }
        }

        if (coloringGame != null)
            NavigationManager.GoToSelectionMenu(coloringGame);
    }

    private void CreateThumbnail(Texture2D tex)
    {
        if (gridContainer == null) return;

        var go = new GameObject("Drawing");
        go.transform.SetParent(gridContainer, false);

        var thumbRT = go.AddComponent<RectTransform>();
        thumbRT.sizeDelta = new Vector2(thumbnailSize, thumbnailSize);

        var bgImg = go.AddComponent<Image>();
        bgImg.color = Color.white;
        if (roundedRectSprite != null) bgImg.sprite = roundedRectSprite;

        // Drawing image — preserve aspect ratio
        var imgGO = new GameObject("Image");
        imgGO.transform.SetParent(go.transform, false);
        var imgRT = imgGO.AddComponent<RectTransform>();
        imgRT.anchorMin = new Vector2(0.05f, 0.05f);
        imgRT.anchorMax = new Vector2(0.95f, 0.95f);
        imgRT.offsetMin = Vector2.zero;
        imgRT.offsetMax = Vector2.zero;
        var rawImg = imgGO.AddComponent<RawImage>();
        rawImg.texture = tex;
        rawImg.raycastTarget = false;
        var arf = imgGO.AddComponent<AspectRatioFitter>();
        arf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        arf.aspectRatio = (tex.height > 0) ? (float)tex.width / tex.height : 1f;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bgImg;
        Texture2D capturedTex = tex;
        btn.onClick.AddListener(() => ShowFullscreen(capturedTex));
    }

    private void ShowFullscreen(Texture2D tex)
    {
        if (fullscreenPanel == null)
            CreateFullscreenView();

        fullscreenImage.texture = tex;
        var arf = fullscreenImage.GetComponent<AspectRatioFitter>();
        if (arf != null)
            arf.aspectRatio = (tex.height > 0) ? (float)tex.width / tex.height : 1f;
        fullscreenPanel.SetActive(true);
    }

    private void CreateFullscreenView()
    {
        // Create fullscreen panel as sibling of the gallery panel (inside overlay)
        fullscreenPanel = new GameObject("FullscreenView");
        fullscreenPanel.transform.SetParent(overlayRoot.transform, false);
        var fsRT = fullscreenPanel.AddComponent<RectTransform>();
        fsRT.anchorMin = Vector2.zero;
        fsRT.anchorMax = Vector2.one;
        fsRT.offsetMin = Vector2.zero;
        fsRT.offsetMax = Vector2.zero;

        // Dark background
        var fsBg = fullscreenPanel.AddComponent<Image>();
        fsBg.color = new Color(0, 0, 0, 0.85f);
        fsBg.raycastTarget = true;

        // Image container (centered, large)
        var imgContainer = new GameObject("ImageContainer");
        imgContainer.transform.SetParent(fullscreenPanel.transform, false);
        var imgContRT = imgContainer.AddComponent<RectTransform>();
        imgContRT.anchorMin = new Vector2(0.05f, 0.1f);
        imgContRT.anchorMax = new Vector2(0.95f, 0.9f);
        imgContRT.offsetMin = Vector2.zero;
        imgContRT.offsetMax = Vector2.zero;

        fullscreenImage = imgContainer.AddComponent<RawImage>();
        fullscreenImage.raycastTarget = false;
        var fsArf = imgContainer.AddComponent<AspectRatioFitter>();
        fsArf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fsArf.aspectRatio = 1f; // updated when showing

        // Close button (tap anywhere)
        var closeBtn = fullscreenPanel.AddComponent<Button>();
        closeBtn.targetGraphic = fsBg;
        closeBtn.onClick.AddListener(() => fullscreenPanel.SetActive(false));
    }

    private IEnumerator BounceAnim()
    {
        float dur = 0.15f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 1.15f, elapsed / dur);
            yield return null;
        }

        elapsed = 0f;
        dur = 0.2f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
            transform.localScale = Vector3.Lerp(Vector3.one * 1.15f, Vector3.one, t);
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

    private IEnumerator IdleSway()
    {
        // Subtle gentle rotation sway
        while (true)
        {
            if (!isOpen && !isAnimating)
            {
                float angle = Mathf.Sin(Time.time * 1.2f) * 1.5f;
                rt.localRotation = Quaternion.Euler(0, 0, angle);
            }
            yield return null;
        }
    }

    private static float EaseOutBack(float t)
    {
        float c = 1.4f;
        return 1f + (c + 1f) * Mathf.Pow(t - 1f, 3f) + c * Mathf.Pow(t - 1f, 2f);
    }

    private static float EaseInBack(float t)
    {
        float c = 1.4f;
        return (c + 1f) * t * t * t - c * t * t;
    }

    private void OnDestroy()
    {
        foreach (var tex in loadedTextures)
            if (tex != null) Destroy(tex);
        loadedTextures.Clear();
    }
}
