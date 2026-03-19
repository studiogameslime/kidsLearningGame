using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Unified Image Gallery — shows child drawings + parent-uploaded images.
/// Reusable: any game can open this, get back the selected image via GameContext.CustomTexture.
/// Landscape layout with vertical scroll, sections for drawings and parent images.
/// </summary>
public class ImageGalleryController : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform contentContainer; // inside ScrollRect, holds both sections
    public Button homeButton;
    public Button startButton;
    public TextMeshProUGUI startButtonText;
    public GameObject emptyStateText;

    [Header("Section Headers")]
    public TextMeshProUGUI drawingsSectionTitle;
    public RectTransform drawingsGrid;
    public TextMeshProUGUI parentSectionTitle;
    public RectTransform parentGrid;
    public GameObject parentEmptyMessage;

    [Header("Sprites")]
    public Sprite roundedRectSprite;

    [Header("Settings")]
    public float thumbnailSize = 280f;

    private static readonly Color SelectedBorder = HexColor("#66BB6A");
    private static readonly Color DefaultBorder  = HexColor("#E0E0E0");
    private static readonly Color DrawingBadge   = HexColor("#F48FB1");
    private static readonly Color ParentBadge    = HexColor("#90CAF9");

    private List<Texture2D> _loadedTextures = new List<Texture2D>();
    private List<GameObject> _allThumbnails = new List<GameObject>();
    private GameObject _selectedThumbnail;
    private Texture2D _selectedTexture;
    private int _drawingCount;
    private int _parentImageCount;

    private void Start()
    {
        if (homeButton != null)
            homeButton.onClick.AddListener(OnHomePressed);
        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartPressed);
            startButton.interactable = false;
        }

        LoadAllImages();
    }

    // ── LOADING ──

    private void LoadAllImages()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null)
        {
            ShowEmptyState();
            return;
        }

        string basePath = Application.persistentDataPath;

        // ── Section 1: Child Drawings ──
        _drawingCount = 0;
        if (profile.savedDrawings != null && profile.savedDrawings.Count > 0)
        {
            if (drawingsSectionTitle != null)
                drawingsSectionTitle.gameObject.SetActive(true);

            for (int i = profile.savedDrawings.Count - 1; i >= 0; i--)
            {
                var drawing = profile.savedDrawings[i];
                string fullPath = Path.Combine(basePath, drawing.imagePath);
                if (!File.Exists(fullPath)) continue;

                var tex = LoadTexture(fullPath);
                if (tex == null) continue;

                CreateThumbnail(drawingsGrid, tex, true);
                _drawingCount++;
            }
        }

        if (_drawingCount == 0 && drawingsSectionTitle != null)
            drawingsSectionTitle.gameObject.SetActive(false);

        // ── Section 2: Parent Images ──
        _parentImageCount = 0;
        if (profile.parentImages != null && profile.parentImages.Count > 0)
        {
            if (parentSectionTitle != null)
                parentSectionTitle.gameObject.SetActive(true);
            if (parentEmptyMessage != null)
                parentEmptyMessage.SetActive(false);

            for (int i = profile.parentImages.Count - 1; i >= 0; i--)
            {
                var img = profile.parentImages[i];
                string fullPath = Path.Combine(basePath, img.imagePath);
                if (!File.Exists(fullPath)) continue;

                var tex = LoadTexture(fullPath);
                if (tex == null) continue;

                CreateThumbnail(parentGrid, tex, false);
                _parentImageCount++;
            }
        }

        if (_parentImageCount == 0)
        {
            if (parentSectionTitle != null)
                parentSectionTitle.gameObject.SetActive(true);
            if (parentEmptyMessage != null)
                parentEmptyMessage.SetActive(true);
        }

        // Global empty state
        if (_drawingCount == 0 && _parentImageCount == 0)
            ShowEmptyState();
        else if (emptyStateText != null)
            emptyStateText.SetActive(false);
    }

    private Texture2D LoadTexture(string fullPath)
    {
        byte[] data = File.ReadAllBytes(fullPath);
        var tex = new Texture2D(2, 2);
        if (!tex.LoadImage(data))
        {
            Destroy(tex);
            return null;
        }
        _loadedTextures.Add(tex);
        return tex;
    }

    // ── THUMBNAIL CREATION ──

    private void CreateThumbnail(RectTransform grid, Texture2D tex, bool isDrawing)
    {
        var go = new GameObject("Thumb");
        go.transform.SetParent(grid, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(thumbnailSize, thumbnailSize);

        // Border (will be recolored on selection)
        var borderGO = new GameObject("Border");
        borderGO.transform.SetParent(go.transform, false);
        var brt = borderGO.AddComponent<RectTransform>();
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(-5, -5);
        brt.offsetMax = new Vector2(5, 5);
        borderGO.transform.SetAsFirstSibling();
        var borderImg = borderGO.AddComponent<Image>();
        if (roundedRectSprite != null) { borderImg.sprite = roundedRectSprite; borderImg.type = Image.Type.Sliced; }
        borderImg.color = DefaultBorder;
        borderImg.raycastTarget = false;

        // Background
        var bgImg = go.AddComponent<Image>();
        if (roundedRectSprite != null) { bgImg.sprite = roundedRectSprite; bgImg.type = Image.Type.Sliced; }
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

        // Type badge (small colored dot in corner)
        var badgeGO = new GameObject("Badge");
        badgeGO.transform.SetParent(go.transform, false);
        var badgeRT = badgeGO.AddComponent<RectTransform>();
        badgeRT.anchorMin = new Vector2(0, 1);
        badgeRT.anchorMax = new Vector2(0, 1);
        badgeRT.pivot = new Vector2(0, 1);
        badgeRT.anchoredPosition = new Vector2(8, -8);
        badgeRT.sizeDelta = new Vector2(24, 24);
        var badgeImg = badgeGO.AddComponent<Image>();
        badgeImg.color = isDrawing ? DrawingBadge : ParentBadge;
        badgeImg.raycastTarget = false;

        // Button
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bgImg;
        GameObject capturedGO = go;
        Texture2D capturedTex = tex;
        btn.onClick.AddListener(() => OnThumbnailTapped(capturedGO, capturedTex));

        _allThumbnails.Add(go);
    }

    // ── SELECTION ──

    private void OnThumbnailTapped(GameObject thumb, Texture2D tex)
    {
        // Deselect previous
        if (_selectedThumbnail != null)
        {
            var prevBorder = _selectedThumbnail.transform.Find("Border");
            if (prevBorder != null)
            {
                var img = prevBorder.GetComponent<Image>();
                if (img != null) img.color = DefaultBorder;
            }
            _selectedThumbnail.transform.localScale = Vector3.one;
        }

        // Select new
        _selectedThumbnail = thumb;
        _selectedTexture = tex;

        var border = thumb.transform.Find("Border");
        if (border != null)
        {
            var img = border.GetComponent<Image>();
            if (img != null) img.color = SelectedBorder;
        }
        thumb.transform.localScale = Vector3.one * 1.05f;

        // Enable start button
        if (startButton != null)
            startButton.interactable = true;
    }

    // ── ACTIONS ──

    private void OnStartPressed()
    {
        if (_selectedTexture == null) return;

        // Pass selected image via GameContext
        GameContext.CustomTexture = _selectedTexture;

        // Navigate to the game that requested the gallery
        // The calling game should have set GameContext.CurrentGame before navigating here
        if (GameContext.CurrentGame != null)
        {
            NavigationManager.GoToGame(GameContext.CurrentGame);
        }
        else
        {
            NavigationManager.GoToMainMenu();
        }
    }

    private void OnHomePressed()
    {
        NavigationManager.GoToMainMenu();
    }

    private void ShowEmptyState()
    {
        if (emptyStateText != null)
            emptyStateText.SetActive(true);
        if (drawingsSectionTitle != null)
            drawingsSectionTitle.gameObject.SetActive(false);
        if (parentSectionTitle != null)
            parentSectionTitle.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        // Don't destroy _selectedTexture if it was passed to GameContext
        foreach (var tex in _loadedTextures)
        {
            if (tex != null && tex != _selectedTexture)
                Destroy(tex);
        }
        _loadedTextures.Clear();
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
