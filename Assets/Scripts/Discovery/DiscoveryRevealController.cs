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
    public int textureWidth = 960;
    public int textureHeight = 540;
    public int brushRadius = 160;
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
        Sprite sprite = null;

        // Try animated data first
        var animData = AnimalAnimData.Load(animalId);
        if (animData != null && animData.idleFrames != null && animData.idleFrames.Length > 0)
            sprite = animData.idleFrames[0];

        // Fallback to static sprite in Resources
        if (sprite == null)
            sprite = Resources.Load<Sprite>($"AnimalSprites/{animalId}");

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
        // Load scratch card texture and tint with child's profile color
        var baseTex = Resources.Load<Texture2D>("ScratchCard");
        Color tint = Color.white;
        var profile = ProfileManager.ActiveProfile;
        if (profile != null)
            tint = profile.AvatarColor;

        if (baseTex != null)
        {
            textureWidth = baseTex.width;
            textureHeight = baseTex.height;

            scratchTex = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            scratchTex.filterMode = FilterMode.Bilinear;

            var sourcePixels = baseTex.GetPixels32();
            totalPixels = sourcePixels.Length;
            clearedPixels = 0;
            pixels = new Color32[totalPixels];

            // Tint white texture with profile color
            byte tR = (byte)(tint.r * 255);
            byte tG = (byte)(tint.g * 255);
            byte tB = (byte)(tint.b * 255);
            for (int i = 0; i < totalPixels; i++)
            {
                var s = sourcePixels[i];
                pixels[i] = new Color32(
                    (byte)(s.r * tR / 255),
                    (byte)(s.g * tG / 255),
                    (byte)(s.b * tB / 255),
                    s.a);
            }
        }
        else
        {
            // Fallback: solid color fill
            scratchTex = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            scratchTex.filterMode = FilterMode.Bilinear;

            totalPixels = textureWidth * textureHeight;
            clearedPixels = 0;
            pixels = new Color32[totalPixels];

            byte tR = (byte)(tint.r * 255);
            byte tG = (byte)(tint.g * 255);
            byte tB = (byte)(tint.b * 255);
            for (int i = 0; i < totalPixels; i++)
                pixels[i] = new Color32(tR, tG, tB, 255);
        }

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
        // Soft edge: full clear inside 80% radius, fade to zero at edge
        int softStart = (int)(r * 0.8f);
        int softStartSq = softStart * softStart;

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
                int distSq = dx * dx + dy * dy;
                if (distSq <= rSq)
                {
                    int idx = y * textureWidth + x;
                    if (pixels[idx].a > 0)
                    {
                        if (distSq <= softStartSq)
                        {
                            // Full clear in center
                            clearedPixels++;
                            pixels[idx].a = 0;
                        }
                        else
                        {
                            // Soft fade at edge
                            float dist = Mathf.Sqrt(distSq);
                            float fade = 1f - (dist - softStart) / (r - softStart);
                            byte newAlpha = (byte)(pixels[idx].a * (1f - fade));
                            if (newAlpha < 8) // threshold to count as cleared
                            {
                                newAlpha = 0;
                                clearedPixels++;
                            }
                            pixels[idx].a = newAlpha;
                        }
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

        // Play the discovered item's sound
        var discovery = JourneyManager.Instance?.ActiveDiscovery;
        if (discovery != null)
        {
            switch (discovery.type)
            {
                case "animal": SoundLibrary.PlayAnimalName(discovery.id); break;
                case "color":  SoundLibrary.PlayColorName(discovery.id); break;
            }
        }

        // Show Hebrew name
        if (nameText != null && discovery != null)
        {
            nameText.text = HebrewFixer.Fix(GetHebrewName(discovery));
            nameText.isRightToLeftText = false;
            nameText.gameObject.SetActive(true);
        }

        // Big confetti burst (double the normal amount)
        ConfettiController.Instance.PlayBig();

        // Wait then continue journey
        yield return new WaitForSeconds(3f);

        isComplete = true;
        JourneyManager.Instance?.ContinueAfterDiscovery();
    }

    private string GetHebrewName(DiscoveryEntry discovery)
    {
        switch (discovery.type)
        {
            case "animal": return GetAnimalHebrew(discovery.id);
            case "color":  return GetColorHebrew(discovery.id);
            case "game":   return GetGameHebrew(discovery.id);
            default:       return discovery.id;
        }
    }

    private string GetAnimalHebrew(string id)
    {
        switch (id)
        {
            case "Cat":      return "\u05D7\u05EA\u05D5\u05DC";     // חתול
            case "Dog":      return "\u05DB\u05DC\u05D1";           // כלב
            case "Bear":     return "\u05D3\u05D5\u05D1";           // דוב
            case "Duck":     return "\u05D1\u05E8\u05D5\u05D6";     // ברווז
            case "Fish":     return "\u05D3\u05D2";                 // דג
            case "Frog":     return "\u05E6\u05E4\u05E8\u05D3\u05E2"; // צפרדע
            case "Bird":     return "\u05E6\u05D9\u05E4\u05D5\u05E8"; // ציפור
            case "Cow":      return "\u05E4\u05E8\u05D4";           // פרה
            case "Horse":    return "\u05E1\u05D5\u05E1";           // סוס
            case "Lion":     return "\u05D0\u05E8\u05D9\u05D4";     // אריה
            case "Monkey":   return "\u05E7\u05D5\u05E3";           // קוף
            case "Elephant": return "\u05E4\u05D9\u05DC";           // פיל
            case "Giraffe":  return "\u05D2\u05F3\u05D9\u05E8\u05E4\u05D4"; // ג'ירפה
            case "Zebra":    return "\u05D6\u05D1\u05E8\u05D4";     // זברה
            case "Turtle":   return "\u05E6\u05D1";                 // צב
            case "Snake":    return "\u05E0\u05D7\u05E9";           // נחש
            case "Sheep":    return "\u05DB\u05D1\u05E9\u05D4";     // כבשה
            case "Chicken":  return "\u05EA\u05E8\u05E0\u05D2\u05D5\u05DC"; // תרנגול
            case "Donkey":   return "\u05D7\u05DE\u05D5\u05E8";     // חמור
            default:         return id;
        }
    }

    private string GetColorHebrew(string id)
    {
        switch (id)
        {
            case "Red":    return "\u05D0\u05D3\u05D5\u05DD";   // אדום
            case "Blue":   return "\u05DB\u05D7\u05D5\u05DC";   // כחול
            case "Yellow": return "\u05E6\u05D4\u05D5\u05D1";   // צהוב
            case "Green":  return "\u05D9\u05E8\u05D5\u05E7";   // ירוק
            case "Orange": return "\u05DB\u05EA\u05D5\u05DD";   // כתום
            case "Purple": return "\u05E1\u05D2\u05D5\u05DC";   // סגול
            case "Pink":   return "\u05D5\u05E8\u05D5\u05D3";   // ורוד
            case "Cyan":   return "\u05EA\u05DB\u05DC\u05EA";   // תכלת
            case "Brown":  return "\u05D7\u05D5\u05DD";         // חום
            case "Black":  return "\u05E9\u05D7\u05D5\u05E8";   // שחור
            default:       return id;
        }
    }

    private string GetGameHebrew(string id)
    {
        switch (id)
        {
            case "memory":       return "\u05DE\u05E9\u05D7\u05E7 \u05D6\u05D9\u05DB\u05E8\u05D5\u05DF"; // משחק זיכרון
            case "puzzle":       return "\u05E4\u05D0\u05D6\u05DC";           // פאזל
            case "coloring":     return "\u05E6\u05D1\u05D9\u05E2\u05D4";     // צביעה
            case "fillthedots":  return "\u05D7\u05D1\u05E8 \u05E0\u05E7\u05D5\u05D3\u05D5\u05EA"; // חבר נקודות
            case "shadows":      return "\u05D4\u05EA\u05D0\u05DE\u05EA \u05E6\u05DC\u05DC\u05D9\u05DD"; // התאמת צללים
            case "colormixing":  return "\u05E2\u05E8\u05D1\u05D5\u05D1 \u05E6\u05D1\u05E2\u05D9\u05DD"; // ערבוב צבעים
            case "ballmaze":     return "\u05DE\u05D1\u05D5\u05DA \u05D4\u05DB\u05D3\u05D5\u05E8"; // מבוך הכדור
            case "towerbuilder": return "\u05D1\u05E0\u05D4 \u05DE\u05D2\u05D3\u05DC";       // בנה מגדל
            case "towerstack":   return "\u05D1\u05E0\u05D4 \u05D0\u05EA \u05D4\u05DE\u05D2\u05D3\u05DC"; // בנה את המגדל
            case "colorvoice":   return "\u05D0\u05DE\u05E8\u05D5 \u05D0\u05EA \u05D4\u05E6\u05D1\u05E2"; // אמרו את הצבע
            case "findtheobject":return "\u05DE\u05E6\u05D0 \u05D0\u05EA \u05D4\u05D7\u05D9\u05D4"; // מצא את החיה
            case "findthecount": return "\u05E1\u05E4\u05D9\u05E8\u05D4";     // ספירה
            case "sharedsticker":return "\u05DE\u05E6\u05D0 \u05D0\u05EA \u05D4\u05DE\u05E9\u05D5\u05EA\u05E3"; // מצא את המשותף
            default:             return id;
        }
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
