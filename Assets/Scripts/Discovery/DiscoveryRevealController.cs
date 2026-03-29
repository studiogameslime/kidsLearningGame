using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Scratch-to-reveal discovery scene. Presented as a playful scratch ticket/card.
/// Player scratches a textured, colorful surface to reveal the discovered content.
/// The scratch layer feels like a physical material with grain and sparkle spots.
/// At 60% cleared, auto-reveals with celebration.
/// </summary>
public class DiscoveryRevealController : MonoBehaviour
{
    [Header("UI References")]
    public RawImage overlayImage;      // scratch layer (textured surface)
    public Image revealImage;          // the content being revealed
    public TextMeshProUGUI nameText;   // name shown after full reveal
    public Image backgroundImage;      // full-screen background
    public RectTransform cardContainer; // the card/ticket container (for shadow/border)

    [Header("Settings")]
    public int textureWidth = 960;
    public int textureHeight = 540;
    public int brushRadius = 96;
    public float revealThreshold = 0.70f;

    private Texture2D scratchTex;
    private Color32[] pixels;
    private int totalPixels;
    private int clearedPixels;
    private bool isRevealed;
    private bool isComplete;
    private int lastTexX = -1;
    private int lastTexY = -1;

    // Scratch dust particles
    private Canvas _dustCanvas;
    private Sprite _dustSprite;

    private void Start()
    {
        // Get the latest discovery from profile queue
        var profile = ProfileManager.ActiveProfile;
        var queue = profile?.journey?.discoveryQueue;
        DiscoveryEntry discovery = null;
        if (queue != null && queue.Count > 0)
            discovery = queue[queue.Count - 1];

        if (discovery == null)
        {
            Debug.LogError("DiscoveryReveal: No active discovery.");
            NavigationManager.GoToHome();
            return;
        }

        CreateDustSprite();
        SetupRevealContent(discovery);
        CreateScratchOverlay();

        if (nameText != null)
        {
            nameText.text = "";
            nameText.gameObject.SetActive(false);
        }
    }

    // ── Content Setup ──────────────────────────────────────────────

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
        }
    }

    private void SetupAnimalReveal(string animalId)
    {
        Sprite sprite = null;

        var animData = AnimalAnimData.Load(animalId);
        if (animData != null && animData.idleFrames != null && animData.idleFrames.Length > 0)
            sprite = animData.idleFrames[0];

        if (sprite == null)
            sprite = Resources.Load<Sprite>($"AnimalSprites/{animalId}");

        if (sprite != null && revealImage != null)
        {
            revealImage.sprite = sprite;
            revealImage.preserveAspect = true;
        }

        if (backgroundImage != null)
            backgroundImage.color = new Color(0.93f, 0.96f, 0.99f);
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
            backgroundImage.color = new Color(0.98f, 0.95f, 0.90f);
    }

    // ── Scratch Overlay (Textured Surface) ─────────────────────────

    private void CreateScratchOverlay()
    {
        Color tint = Color.white;
        var profile = ProfileManager.ActiveProfile;
        if (profile != null)
            tint = profile.AvatarColor;

        // Try loading base texture
        var baseTex = Resources.Load<Texture2D>("ScratchCard");

        scratchTex = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        scratchTex.filterMode = FilterMode.Bilinear;

        totalPixels = textureWidth * textureHeight;
        clearedPixels = 0;
        pixels = new Color32[totalPixels];

        byte tR = (byte)(tint.r * 255);
        byte tG = (byte)(tint.g * 255);
        byte tB = (byte)(tint.b * 255);

        if (baseTex != null)
        {
            // Use base texture tinted with profile color
            var sourcePixels = baseTex.GetPixels32();
            int srcLen = sourcePixels.Length;
            for (int i = 0; i < totalPixels; i++)
            {
                var s = i < srcLen ? sourcePixels[i] : new Color32(255, 255, 255, 255);
                pixels[i] = new Color32(
                    (byte)(s.r * tR / 255),
                    (byte)(s.g * tG / 255),
                    (byte)(s.b * tB / 255),
                    s.a);
            }
        }
        else
        {
            // Generate textured surface with grain and sparkles
            for (int i = 0; i < totalPixels; i++)
            {
                int x = i % textureWidth;
                int y = i / textureWidth;

                // Base color with subtle noise for grain texture
                float noise = Random.Range(0.85f, 1.0f);
                byte r = (byte)(tR * noise);
                byte g = (byte)(tG * noise);
                byte b = (byte)(tB * noise);

                // Random sparkle spots (2% chance)
                if (Random.value < 0.02f)
                {
                    float sparkle = Random.Range(1.1f, 1.4f);
                    r = (byte)Mathf.Min(255, r * sparkle);
                    g = (byte)Mathf.Min(255, g * sparkle);
                    b = (byte)Mathf.Min(255, b * sparkle);
                }

                pixels[i] = new Color32(r, g, b, 255);
            }
        }

        scratchTex.SetPixels32(pixels);
        scratchTex.Apply();

        if (overlayImage != null)
            overlayImage.texture = scratchTex;
    }

    // ── Touch Input ────────────────────────────────────────────────

    private void Update()
    {
        if (isRevealed || isComplete) return;

        bool hasInput = Input.touchCount > 0 || Input.GetMouseButton(0);

        if (hasInput && overlayImage != null)
        {
            Vector2 inputPos = Input.touchCount > 0
                ? (Vector2)Input.GetTouch(0).position
                : (Vector2)Input.mousePosition;

            Vector2 localPoint;
            RectTransform rt = overlayImage.rectTransform;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt, inputPos, null, out localPoint))
            {
                Rect rect = rt.rect;
                float normX = (localPoint.x - rect.x) / rect.width;
                float normY = (localPoint.y - rect.y) / rect.height;

                int texX = Mathf.RoundToInt(normX * textureWidth);
                int texY = Mathf.RoundToInt(normY * textureHeight);

                // Spawn dust particles at touch position
                SpawnDust(inputPos);

                // Interpolate between last position and current for smooth strokes
                if (lastTexX >= 0 && lastTexY >= 0)
                {
                    int dx = texX - lastTexX;
                    int dy = texY - lastTexY;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    int steps = Mathf.Max(1, Mathf.RoundToInt(dist / (brushRadius * 0.3f)));
                    for (int i = 0; i <= steps; i++)
                    {
                        float t = (float)i / steps;
                        int ix = Mathf.RoundToInt(Mathf.Lerp(lastTexX, texX, t));
                        int iy = Mathf.RoundToInt(Mathf.Lerp(lastTexY, texY, t));
                        ScratchAt(ix, iy);
                    }
                }
                else
                {
                    ScratchAt(texX, texY);
                }

                lastTexX = texX;
                lastTexY = texY;
            }
        }
        else
        {
            lastTexX = -1;
            lastTexY = -1;
        }
    }

    // ── Scratch Mechanic ───────────────────────────────────────────

    private void ScratchAt(int cx, int cy)
    {
        bool changed = false;
        int r = brushRadius;
        int rSq = r * r;
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
                            clearedPixels++;
                            pixels[idx].a = 0;
                        }
                        else
                        {
                            float dist = Mathf.Sqrt(distSq);
                            float fade = 1f - (dist - softStart) / (r - softStart);
                            byte newAlpha = (byte)(pixels[idx].a * (1f - fade));
                            if (newAlpha < 8)
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

    // ── Dust/Sparkle Particles During Scratch ──────────────────────

    private void CreateDustSprite()
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(1.5f, 1.5f));
                float a = Mathf.Clamp01(1f - dist / 2f);
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        tex.Apply();
        _dustSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
    }

    private int _dustFrame;

    private void SpawnDust(Vector2 screenPos)
    {
        _dustFrame++;
        if (_dustFrame % 3 != 0) return; // every 3rd frame

        if (_dustCanvas == null)
        {
            var canvasGo = new GameObject("DustCanvas");
            canvasGo.transform.SetParent(transform);
            _dustCanvas = canvasGo.AddComponent<Canvas>();
            _dustCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _dustCanvas.sortingOrder = 500;
            var cg = canvasGo.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }

        for (int i = 0; i < 2; i++)
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(_dustCanvas.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = screenPos + Random.insideUnitCircle * 20f;
            float size = Random.Range(8f, 18f);
            rt.sizeDelta = new Vector2(size, size);

            var img = go.AddComponent<Image>();
            img.sprite = _dustSprite;
            img.raycastTarget = false;

            // Gold/white sparkle colors
            img.color = Random.value > 0.5f
                ? new Color(1f, 0.9f, 0.5f, 0.8f)
                : new Color(1f, 1f, 1f, 0.7f);

            StartCoroutine(FadeDust(go, rt));
        }
    }

    private IEnumerator FadeDust(GameObject go, RectTransform rt)
    {
        Vector2 startPos = rt.anchoredPosition;
        Vector2 drift = Random.insideUnitCircle * 40f + Vector2.up * 30f;
        var img = go.GetComponent<Image>();
        Color startColor = img.color;
        float dur = 0.4f;
        float elapsed = 0f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float ease = t * (2f - t);

            rt.anchoredPosition = startPos + drift * ease;
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.2f, ease);
            startColor.a = Mathf.Lerp(0.8f, 0f, ease);
            img.color = startColor;

            yield return null;
        }

        Destroy(go);
    }

    // ── Auto Reveal ────────────────────────────────────────────────

    private IEnumerator AutoReveal()
    {
        if (isRevealed) yield break;
        isRevealed = true;

        // Fade out remaining overlay
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

        // Bounce the revealed content
        if (revealImage != null)
            StartCoroutine(BounceReveal(revealImage.rectTransform));

        // Play sound — get discovery from profile queue
        var profile2 = ProfileManager.ActiveProfile;
        var queue2 = profile2?.journey?.discoveryQueue;
        DiscoveryEntry discovery = null;
        if (queue2 != null && queue2.Count > 0)
            discovery = queue2[queue2.Count - 1];
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
            HebrewText.SetText(nameText, GetHebrewName(discovery));
            nameText.gameObject.SetActive(true);
        }

        // Sparkles on revealed content
        if (revealImage != null)
            UIEffects.SpawnSparkles(revealImage.rectTransform, 12);

        // Big confetti
        if (ConfettiController.Instance != null)
            ConfettiController.Instance.PlayBig();

        // Wait then return to world (gift box will appear there)
        yield return new WaitForSeconds(3f);

        isComplete = true;
        NavigationManager.GoToHome();
    }

    private IEnumerator BounceReveal(RectTransform rt)
    {
        float dur = 0.5f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float bounce = 1f + 0.15f * Mathf.Sin(t * Mathf.PI * 2f) * (1f - t);
            rt.localScale = Vector3.one * bounce;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    // ── Cleanup ────────────────────────────────────────────────────

    private void OnDestroy()
    {
        if (scratchTex != null)
            Destroy(scratchTex);
    }

    // ── Hebrew Names ───────────────────────────────────────────────

    private string GetHebrewName(DiscoveryEntry discovery)
    {
        switch (discovery.type)
        {
            case "animal": return GetAnimalHebrew(discovery.id);
            case "color":  return GetColorHebrew(discovery.id);
            default:       return discovery.id;
        }
    }

    private string GetAnimalHebrew(string id)
    {
        switch (id)
        {
            case "Cat":      return "\u05D7\u05EA\u05D5\u05DC";
            case "Dog":      return "\u05DB\u05DC\u05D1";
            case "Bear":     return "\u05D3\u05D5\u05D1";
            case "Duck":     return "\u05D1\u05E8\u05D5\u05D6";
            case "Fish":     return "\u05D3\u05D2";
            case "Frog":     return "\u05E6\u05E4\u05E8\u05D3\u05E2";
            case "Bird":     return "\u05E6\u05D9\u05E4\u05D5\u05E8";
            case "Cow":      return "\u05E4\u05E8\u05D4";
            case "Horse":    return "\u05E1\u05D5\u05E1";
            case "Lion":     return "\u05D0\u05E8\u05D9\u05D4";
            case "Monkey":   return "\u05E7\u05D5\u05E3";
            case "Elephant": return "\u05E4\u05D9\u05DC";
            case "Giraffe":  return "\u05D2\u05F3\u05D9\u05E8\u05E4\u05D4";
            case "Zebra":    return "\u05D6\u05D1\u05E8\u05D4";
            case "Turtle":   return "\u05E6\u05D1";
            case "Snake":    return "\u05E0\u05D7\u05E9";
            case "Sheep":    return "\u05DB\u05D1\u05E9\u05D4";
            case "Chicken":  return "\u05EA\u05E8\u05E0\u05D2\u05D5\u05DC";
            case "Donkey":   return "\u05D7\u05DE\u05D5\u05E8";
            default:         return id;
        }
    }

    private string GetColorHebrew(string id)
    {
        switch (id)
        {
            case "Red":    return "\u05D0\u05D3\u05D5\u05DD";
            case "Blue":   return "\u05DB\u05D7\u05D5\u05DC";
            case "Yellow": return "\u05E6\u05D4\u05D5\u05D1";
            case "Green":  return "\u05D9\u05E8\u05D5\u05E7";
            case "Orange": return "\u05DB\u05EA\u05D5\u05DD";
            case "Purple": return "\u05E1\u05D2\u05D5\u05DC";
            case "Pink":   return "\u05D5\u05E8\u05D5\u05D3";
            case "Cyan":   return "\u05EA\u05DB\u05DC\u05EA";
            case "Brown":  return "\u05D7\u05D5\u05DD";
            case "Black":  return "\u05E9\u05D7\u05D5\u05E8";
            default:       return id;
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
