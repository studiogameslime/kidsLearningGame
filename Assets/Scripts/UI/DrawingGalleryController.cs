using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays saved drawings in a scrollable grid. Tapping a drawing opens it fullscreen.
/// </summary>
public class DrawingGalleryController : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform gridContainer;
    public Button homeButton;
    public GameObject fullscreenPanel;
    public RawImage fullscreenImage;
    public Button fullscreenCloseButton;

    [Header("Settings")]
    public float thumbnailSize = 300f;

    [Header("Sprites")]
    public Sprite roundedRectSprite;

    private List<Texture2D> loadedTextures = new List<Texture2D>();

    private void Start()
    {
        if (homeButton != null)
            homeButton.onClick.AddListener(OnHomePressed);
        if (fullscreenCloseButton != null)
            fullscreenCloseButton.onClick.AddListener(CloseFullscreen);
        if (fullscreenPanel != null)
            fullscreenPanel.SetActive(false);

        LoadDrawings();
    }

    private void LoadDrawings()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null || profile.savedDrawings == null) return;

        string basePath = Application.persistentDataPath;

        // Show newest first
        for (int i = profile.savedDrawings.Count - 1; i >= 0; i--)
        {
            var drawing = profile.savedDrawings[i];
            string fullPath = Path.Combine(basePath, drawing.imagePath);

            if (!File.Exists(fullPath)) continue;

            byte[] data = File.ReadAllBytes(fullPath);
            var tex = new Texture2D(2, 2);
            if (!tex.LoadImage(data)) continue;

            loadedTextures.Add(tex);
            CreateThumbnail(tex, drawing.animalId);
        }
    }

    private void CreateThumbnail(Texture2D tex, string animalId)
    {
        var go = new GameObject("Drawing");
        go.transform.SetParent(gridContainer, false);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(thumbnailSize, thumbnailSize);

        // Background
        var bgImg = go.AddComponent<Image>();
        bgImg.color = Color.white;
        if (roundedRectSprite != null) bgImg.sprite = roundedRectSprite;

        // Drawing image
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

        // Button for tap
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bgImg;
        Texture2D capturedTex = tex;
        btn.onClick.AddListener(() => ShowFullscreen(capturedTex));
    }

    private void ShowFullscreen(Texture2D tex)
    {
        if (fullscreenPanel == null || fullscreenImage == null) return;
        fullscreenImage.texture = tex;
        fullscreenPanel.SetActive(true);
    }

    private void CloseFullscreen()
    {
        if (fullscreenPanel != null)
            fullscreenPanel.SetActive(false);
    }

    private void OnHomePressed()
    {
        NavigationManager.GoToWorld();
    }

    private void OnDestroy()
    {
        foreach (var tex in loadedTextures)
            if (tex != null) Destroy(tex);
        loadedTextures.Clear();
    }
}
