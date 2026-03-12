using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Scratch-to-reveal discovery scene. Player touches to clear a gold overlay,
/// revealing the discovered content underneath. At 70% cleared, auto-reveals.
/// </summary>
public class DiscoveryRevealController : MonoBehaviour
{
    [Header("UI References")]
    public RawImage overlayImage;      // gold scratch layer
    public Image revealImage;          // the content being revealed (animal/color/game icon)
    public TextMeshProUGUI nameText;   // name shown after full reveal
    public Image backgroundImage;      // full-screen background

    [Header("Settings")]
    public int textureWidth = 540;
    public int textureHeight = 960;
    public int brushRadius = 80;
    public float revealThreshold = 0.7f;

    private Texture2D scratchTex;
    private Color32[] pixels;
    private int totalPixels;
    private int clearedPixels;
    private bool isRevealed;
    private bool isComplete;

    private void Start()
    {
        var discovery = JourneyManager.Instance?.ActiveDiscovery;
        if (discovery == null)
        {
            Debug.LogError("DiscoveryReveal: No active discovery.");
            JourneyManager.Instance?.ContinueAfterDiscovery();
            return;
        }

        SetupRevealContent(discovery);
        CreateScratchOverlay();

        if (nameText != null)
        {
            nameText.text = "";
            nameText.gameObject.SetActive(false);
        }
    }

    private void SetupRevealContent(DiscoveryEntry discovery)
    {
        switch (discovery.type)
        {
            case "animal":
                SetupAnimalReveal(discovery.id);
                break;
            case "color":
                SetupColorReveal(discovery.id);
                break;
            case "game":
                SetupGameReveal(discovery.id);
                break;
        }
    }

    private void SetupAnimalReveal(string animalId)
    {
        // Try to load the animal sprite
        string[] paths = {
            $"Animals/{animalId}/{animalId}Sprite",
            $"Animals/{animalId}/{animalId}"
        };

        Sprite sprite = null;
        foreach (var path in paths)
        {
            sprite = Resources.Load<Sprite>(path);
            if (sprite != null) break;
        }

        if (sprite != null && revealImage != null)
        {
            revealImage.sprite = sprite;
            revealImage.preserveAspect = true;
        }

        if (backgroundImage != null)
            backgroundImage.color = new Color(0.93f, 0.96f, 0.99f); // light blue
    }

    private void SetupColorReveal(string colorId)
    {
        Color revealColor = GetColorById(colorId);

        if (revealImage != null)
        {
            revealImage.color = revealColor;
            revealImage.sprite = null;
        }

        if (backgroundImage != null)
            backgroundImage.color = new Color(0.98f, 0.95f, 0.90f); // warm cream
    }

    private void SetupGameReveal(string gameId)
    {
        if (revealImage != null)
            revealImage.color = new Color(0.6f, 0.4f, 0.9f); // purple placeholder

        if (backgroundImage != null)
            backgroundImage.color = new Color(0.95f, 0.93f, 0.99f); // light purple
    }

    private void CreateScratchOverlay()
    {
        scratchTex = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        scratchTex.filterMode = FilterMode.Bilinear;

        totalPixels = textureWidth * textureHeight;
        clearedPixels = 0;

        // Fill with gold color
        pixels = new Color32[totalPixels];
        var gold = new Color32(218, 165, 32, 255);
        for (int i = 0; i < totalPixels; i++)
            pixels[i] = gold;

        scratchTex.SetPixels32(pixels);
        scratchTex.Apply();

        if (overlayImage != null)
            overlayImage.texture = scratchTex;
    }

    private void Update()
    {
        if (isRevealed || isComplete) return;

        // Handle touch/mouse input
        if (Input.GetMouseButton(0) && overlayImage != null)
        {
            Vector2 localPoint;
            RectTransform rt = overlayImage.rectTransform;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt, Input.mousePosition, null, out localPoint))
            {
                // Convert local point to texture coordinates
                Rect rect = rt.rect;
                float normX = (localPoint.x - rect.x) / rect.width;
                float normY = (localPoint.y - rect.y) / rect.height;

                int texX = Mathf.RoundToInt(normX * textureWidth);
                int texY = Mathf.RoundToInt(normY * textureHeight);

                ScratchAt(texX, texY);
            }
        }
    }

    private void ScratchAt(int cx, int cy)
    {
        bool changed = false;
        int r = brushRadius;
        int rSq = r * r;

        int minX = Mathf.Max(0, cx - r);
        int maxX = Mathf.Min(textureWidth - 1, cx + r);
        int minY = Mathf.Max(0, cy - r);
        int maxY = Mathf.Min(textureHeight - 1, cy + r);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - cx;
                int dy = y - cy;
                if (dx * dx + dy * dy <= rSq)
                {
                    int idx = y * textureWidth + x;
                    if (pixels[idx].a > 0)
                    {
                        pixels[idx].a = 0;
                        clearedPixels++;
                        changed = true;
                    }
                }
            }
        }

        if (changed)
        {
            scratchTex.SetPixels32(pixels);
            scratchTex.Apply();

            float ratio = (float)clearedPixels / totalPixels;
            if (ratio >= revealThreshold)
                StartCoroutine(AutoReveal());
        }
    }

    private IEnumerator AutoReveal()
    {
        if (isRevealed) yield break;
        isRevealed = true;

        // Clear remaining overlay with a quick fade
        float fadeTime = 0.5f;
        float elapsed = 0f;
        Color overlayColor = overlayImage.color;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / fadeTime);
            overlayImage.color = new Color(overlayColor.r, overlayColor.g, overlayColor.b, alpha);
            yield return null;
        }

        overlayImage.gameObject.SetActive(false);

        // Show name
        var discovery = JourneyManager.Instance?.ActiveDiscovery;
        if (nameText != null && discovery != null)
        {
            nameText.text = discovery.id;
            nameText.gameObject.SetActive(true);
        }

        // Confetti
        ConfettiController.Instance.Play();

        // Wait then continue journey
        yield return new WaitForSeconds(3f);

        isComplete = true;
        JourneyManager.Instance?.ContinueAfterDiscovery();
    }

    private Color GetColorById(string colorId)
    {
        switch (colorId)
        {
            case "Red":    return new Color(0.94f, 0.27f, 0.27f);
            case "Blue":   return new Color(0.23f, 0.51f, 0.96f);
            case "Yellow": return new Color(0.98f, 0.80f, 0.08f);
            case "Green":  return new Color(0.13f, 0.77f, 0.37f);
            case "Orange": return new Color(0.98f, 0.45f, 0.09f);
            case "Purple": return new Color(0.55f, 0.36f, 0.96f);
            case "Pink":   return new Color(0.93f, 0.29f, 0.60f);
            case "Cyan":   return new Color(0.02f, 0.71f, 0.83f);
            case "Brown":  return new Color(0.47f, 0.33f, 0.28f);
            case "Black":  return new Color(0.12f, 0.12f, 0.12f);
            default:       return Color.white;
        }
    }
}
