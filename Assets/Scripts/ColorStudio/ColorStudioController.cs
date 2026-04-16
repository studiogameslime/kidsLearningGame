using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Color Studio — magical color mixing lab for kids.
/// Drag colors into a cauldron, watch them swirl and mix,
/// pour the result into a bottle to save.
/// </summary>
public class ColorStudioController : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform baseColorsArea;   // color circles on the left
    public RectTransform cauldronArea;     // the cauldron in the center
    public RectTransform bottlesArea;      // saved bottles at bottom
    public Button backButton;
    public Button pourButton;
    public Image cauldronFillImage;        // the "liquid" inside the cauldron
    public RectTransform cauldronRT;       // the cauldron body for particle spawning

    [Header("History Panel")]
    public GameObject historyPanel;
    public RectTransform historyContent;
    public Button historyCloseButton;

    [Header("Ambient")]
    public RectTransform fxArea;           // full screen FX layer

    private Sprite circleSprite;
    private Canvas rootCanvas;

    // Cauldron state
    private string cauldronColorA;
    private string cauldronColorB;
    private string mixedResultHex;
    private bool isMixing;
    private bool hasResult;
    private int cauldronFillCount; // 0, 1, or 2

    // Drag
    private bool isDragging;
    private GameObject dragPreview;
    private string dragHex;
    private float trailTimer;

    // Ambient
    private float ambientBubbleTimer;
    private float ambientSparkleTimer;
    private float _sessionStart;

    private void Start()
    {
        _sessionStart = Time.realtimeSinceStartup;
        circleSprite = Resources.Load<Sprite>("Circle");
        rootCanvas = GetComponentInParent<Canvas>();

        if (backButton != null)
            backButton.onClick.AddListener(() =>
            {
                float duration = Time.realtimeSinceStartup - _sessionStart;
                FirebaseAnalyticsManager.LogSandboxSession("color_studio", duration);
                FingerTrail.SetEnabled(true);
                NavigationManager.GoToHome();
            });
        if (pourButton != null)
        {
            pourButton.onClick.AddListener(OnPourPressed);
            pourButton.gameObject.SetActive(false);
        }
        if (historyCloseButton != null)
            historyCloseButton.onClick.AddListener(() => historyPanel.SetActive(false));
        if (historyPanel != null)
            historyPanel.SetActive(false);

        FingerTrail.SetEnabled(false);
        FirebaseAnalyticsManager.LogScreenView("color_studio");
        ResetCauldron();
        BuildBaseColors();
        RebuildBottles();

        // Start ambient effects
        StartCoroutine(AmbientMagicLoop());
    }

    private void Update()
    {
        if (!isDragging || dragPreview == null) return;

        dragPreview.GetComponent<RectTransform>().position = Input.mousePosition;

        trailTimer += Time.deltaTime;
        if (trailTimer >= 0.04f)
        {
            trailTimer = 0f;
            SpawnTrailParticle(Input.mousePosition, ColorMixLookup.HexToColor(dragHex), 0.5f);
        }

        if (Input.GetMouseButtonUp(0))
            EndDrag(Input.mousePosition);
    }

    // ══════════════════════════════════
    //  BASE COLORS
    // ══════════════════════════════════

    private void BuildBaseColors()
    {
        if (baseColorsArea == null) return;
        foreach (var hex in ColorMixLookup.BaseColorHexes)
            CreateDraggableColor(baseColorsArea, hex, 100f, false);
    }

    // ══════════════════════════════════
    //  BOTTLES (sorted by hue)
    // ══════════════════════════════════

    private void RebuildBottles()
    {
        if (bottlesArea == null) return;
        for (int i = bottlesArea.childCount - 1; i >= 0; i--)
            Destroy(bottlesArea.GetChild(i).gameObject);

        var profile = ProfileManager.ActiveProfile;
        if (profile?.colorStudio?.savedColors == null) return;

        var sorted = new List<CreatedColor>(profile.colorStudio.savedColors);
        sorted.Sort((a, b) =>
        {
            Color.RGBToHSV(ColorMixLookup.HexToColor(a.hex), out float hA, out _, out _);
            Color.RGBToHSV(ColorMixLookup.HexToColor(b.hex), out float hB, out _, out _);
            return hA.CompareTo(hB);
        });

        foreach (var cc in sorted)
            CreateBottle(cc);
    }

    private void CreateBottle(CreatedColor cc)
    {
        var go = new GameObject($"Bottle_{cc.hex}");
        go.transform.SetParent(bottlesArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(55f, 75f);

        // Bottle body (rounded rect)
        var bodyImg = go.AddComponent<Image>();
        var roundedRect = Resources.Load<Sprite>("UI/RoundedRect");
        if (roundedRect != null) { bodyImg.sprite = roundedRect; bodyImg.type = Image.Type.Sliced; }
        Color c = ColorMixLookup.HexToColor(cc.hex);
        bodyImg.color = new Color(c.r, c.g, c.b, 0.85f);
        bodyImg.raycastTarget = true;

        // Bottle neck (small rect on top)
        var neckGO = new GameObject("Neck");
        neckGO.transform.SetParent(go.transform, false);
        var neckRT = neckGO.AddComponent<RectTransform>();
        neckRT.anchorMin = new Vector2(0.3f, 1f); neckRT.anchorMax = new Vector2(0.7f, 1f);
        neckRT.pivot = new Vector2(0.5f, 0f);
        neckRT.sizeDelta = new Vector2(0, 14);
        var neckImg = neckGO.AddComponent<Image>();
        neckImg.color = new Color(c.r * 0.8f, c.g * 0.8f, c.b * 0.8f, 0.9f);
        neckImg.raycastTarget = false;

        // Shine
        var shineGO = new GameObject("Shine");
        shineGO.transform.SetParent(go.transform, false);
        var shineRT = shineGO.AddComponent<RectTransform>();
        shineRT.anchorMin = new Vector2(0.15f, 0.4f); shineRT.anchorMax = new Vector2(0.35f, 0.85f);
        shineRT.offsetMin = Vector2.zero; shineRT.offsetMax = Vector2.zero;
        var shineImg = shineGO.AddComponent<Image>();
        shineImg.color = new Color(1f, 1f, 1f, 0.2f); shineImg.raycastTarget = false;

        // Events: tap = history, drag = mix
        string capturedHex = cc.hex;
        var trigger = go.AddComponent<EventTrigger>();

        var beginDrag = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
        beginDrag.callback.AddListener((_) => StartDrag(capturedHex, rt));
        trigger.triggers.Add(beginDrag);

        var dragEv = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
        dragEv.callback.AddListener((_) => { });
        trigger.triggers.Add(dragEv);

        var endDragEv = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
        endDragEv.callback.AddListener((_) => EndDrag(Input.mousePosition));
        trigger.triggers.Add(endDragEv);

        var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        click.callback.AddListener((_) => { if (!isDragging) ShowHistory(capturedHex); });
        trigger.triggers.Add(click);
    }

    // ══════════════════════════════════
    //  DRAGGABLE COLOR CIRCLE
    // ══════════════════════════════════

    private GameObject CreateDraggableColor(RectTransform parent, string hex, float size, bool isBottle)
    {
        var go = new GameObject($"Color_{hex}");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);

        var img = go.AddComponent<Image>();
        if (circleSprite != null) img.sprite = circleSprite;
        img.color = ColorMixLookup.HexToColor(hex);
        img.raycastTarget = true;

        // Glow behind
        var glowGO = new GameObject("Glow");
        glowGO.transform.SetParent(go.transform, false);
        glowGO.transform.SetAsFirstSibling();
        var glowRT = glowGO.AddComponent<RectTransform>();
        glowRT.anchorMin = new Vector2(-0.25f, -0.25f); glowRT.anchorMax = new Vector2(1.25f, 1.25f);
        glowRT.offsetMin = Vector2.zero; glowRT.offsetMax = Vector2.zero;
        var glowImg = glowGO.AddComponent<Image>();
        if (circleSprite != null) glowImg.sprite = circleSprite;
        Color gc = ColorMixLookup.HexToColor(hex);
        glowImg.color = new Color(gc.r, gc.g, gc.b, 0.15f);
        glowImg.raycastTarget = false;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.3f);
        outline.effectDistance = new Vector2(2, -2);

        string capturedHex = hex;
        var trigger = go.AddComponent<EventTrigger>();

        var beginDrag = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
        beginDrag.callback.AddListener((_) => StartDrag(capturedHex, rt));
        trigger.triggers.Add(beginDrag);

        var dragEv = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
        dragEv.callback.AddListener((_) => { });
        trigger.triggers.Add(dragEv);

        var endDragEv = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
        endDragEv.callback.AddListener((_) => EndDrag(Input.mousePosition));
        trigger.triggers.Add(endDragEv);

        return go;
    }

    // ══════════════════════════════════
    //  DRAG & DROP INTO CAULDRON
    // ══════════════════════════════════

    private void StartDrag(string hex, RectTransform sourceRT)
    {
        if (isMixing) return;
        isDragging = true;
        dragHex = hex;
        trailTimer = 0f;

        dragPreview = new GameObject("DragPreview");
        dragPreview.transform.SetParent(rootCanvas != null ? rootCanvas.transform : transform, false);
        var rt = dragPreview.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(90, 90);
        rt.position = Input.mousePosition;
        var img = dragPreview.AddComponent<Image>();
        if (circleSprite != null) img.sprite = circleSprite;
        img.color = ColorMixLookup.HexToColor(hex);
        img.raycastTarget = false;
        var cg = dragPreview.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.alpha = 0.85f;

        // Scale up with glow
        rt.localScale = Vector3.one * 1.15f;
    }

    private void EndDrag(Vector2 screenPos)
    {
        if (!isDragging) return;
        isDragging = false;
        if (dragPreview != null) Destroy(dragPreview);

        // Check if dropped on cauldron
        if (cauldronRT != null && RectTransformUtility.RectangleContainsScreenPoint(cauldronRT, screenPos, null))
        {
            AddToCauldron(dragHex);
        }
        dragHex = null;
    }

    // ══════════════════════════════════
    //  CAULDRON LOGIC
    // ══════════════════════════════════

    private void ResetCauldron()
    {
        cauldronColorA = null;
        cauldronColorB = null;
        mixedResultHex = null;
        cauldronFillCount = 0;
        hasResult = false;
        isMixing = false;

        if (cauldronFillImage != null)
            cauldronFillImage.color = new Color(0.2f, 0.15f, 0.25f, 0.4f); // dark empty

        if (pourButton != null)
            pourButton.gameObject.SetActive(false);
    }

    private void AddToCauldron(string hex)
    {
        if (isMixing || hasResult) return;

        if (cauldronFillCount == 0)
        {
            cauldronColorA = hex;
            cauldronFillCount = 1;
            StartCoroutine(PourIntoCauldron(hex, true));
        }
        else if (cauldronFillCount == 1)
        {
            cauldronColorB = hex;
            cauldronFillCount = 2;
            StartCoroutine(PourIntoCauldron(hex, false));
        }
    }

    private IEnumerator PourIntoCauldron(string hex, bool isFirst)
    {
        Color pourColor = ColorMixLookup.HexToColor(hex);
        Transform fxParent = rootCanvas != null ? rootCanvas.transform : transform;
        Vector2 cauldronCenter = cauldronRT != null ? (Vector2)cauldronRT.position : Vector2.zero;

        // Splash particles entering cauldron
        for (int i = 0; i < 8; i++)
        {
            SpawnSplashParticle(cauldronCenter + Random.insideUnitCircle * 30f, pourColor, fxParent);
            yield return new WaitForSeconds(0.03f);
        }

        // Fill cauldron with color
        if (cauldronFillImage != null)
        {
            if (isFirst)
            {
                // First color fills
                float t = 0f;
                Color startColor = cauldronFillImage.color;
                while (t < 0.4f)
                {
                    t += Time.deltaTime;
                    cauldronFillImage.color = Color.Lerp(startColor, pourColor, t / 0.4f);
                    yield return null;
                }
                cauldronFillImage.color = pourColor;
            }
            else
            {
                // Second color → start mixing!
                yield return StartCoroutine(MagicMixAnimation(pourColor));
            }
        }
    }

    // ══════════════════════════════════
    //  MAGIC MIX ANIMATION
    // ══════════════════════════════════

    private IEnumerator MagicMixAnimation(Color colorB)
    {
        isMixing = true;
        Color colorA = ColorMixLookup.HexToColor(cauldronColorA);
        mixedResultHex = ColorMixLookup.MixHex(cauldronColorA, cauldronColorB);
        Color resultColor = ColorMixLookup.HexToColor(mixedResultHex);
        Transform fxParent = rootCanvas != null ? rootCanvas.transform : transform;
        Vector2 center = cauldronRT != null ? (Vector2)cauldronRT.position : Vector2.zero;

        // ── Phase 1: Swirl — 2 colors spiraling ──
        float swirlDur = 1.5f;
        float t = 0f;

        // Spawn swirl particles
        while (t < swirlDur)
        {
            t += Time.deltaTime;
            float p = t / swirlDur;
            float angle = p * Mathf.PI * 6f; // 3 full rotations
            float radius = 60f * (1f - p * 0.5f);

            // Color A particle
            Vector2 posA = center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            SpawnSwirlParticle(posA, colorA, fxParent);

            // Color B particle (opposite side)
            Vector2 posB = center + new Vector2(-Mathf.Cos(angle) * radius, -Mathf.Sin(angle) * radius);
            SpawnSwirlParticle(posB, colorB, fxParent);

            // Cauldron liquid transitions
            float blend = p;
            cauldronFillImage.color = Color.Lerp(colorA, resultColor, blend);

            // Occasional big bubble
            if (Random.value < 0.1f)
                SpawnCauldronBubble(center, Color.Lerp(colorA, colorB, Random.value), fxParent);

            yield return null;
        }

        // ── Phase 2: Magic burst ──
        cauldronFillImage.color = resultColor;

        // Big sparkle explosion
        for (int i = 0; i < 25; i++)
            SpawnBurstParticle(center, resultColor, fxParent, 300f, 600f);

        // Color wave rings
        for (int i = 0; i < 3; i++)
        {
            StartCoroutine(ColorWaveRing(center, resultColor, fxParent, 0.4f + i * 0.15f));
        }

        // Smoke puffs
        for (int i = 0; i < 5; i++)
            StartCoroutine(SmokePuff(center + Random.insideUnitCircle * 40f, resultColor, fxParent));

        // Rising stars
        for (int i = 0; i < 8; i++)
            StartCoroutine(RisingStar(center, resultColor, fxParent));

        yield return new WaitForSeconds(0.8f);

        // ── Phase 3: Cauldron glows and pulses ──
        t = 0f;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            float pulse = 1f + 0.05f * Mathf.Sin(t * 15f);
            if (cauldronRT != null)
                cauldronRT.localScale = Vector3.one * pulse;
            yield return null;
        }
        if (cauldronRT != null) cauldronRT.localScale = Vector3.one;

        isMixing = false;
        hasResult = true;

        // Show pour button
        if (pourButton != null)
        {
            pourButton.gameObject.SetActive(true);
            StartCoroutine(PulseButton(pourButton.GetComponent<RectTransform>()));
        }
    }

    // ══════════════════════════════════
    //  POUR TO BOTTLE
    // ══════════════════════════════════

    private void OnPourPressed()
    {
        if (!hasResult || string.IsNullOrEmpty(mixedResultHex)) return;
        StartCoroutine(PourAnimation());
    }

    private IEnumerator PourAnimation()
    {
        if (pourButton != null) pourButton.gameObject.SetActive(false);

        Transform fxParent = rootCanvas != null ? rootCanvas.transform : transform;
        Vector2 cauldronCenter = cauldronRT != null ? (Vector2)cauldronRT.position : Vector2.zero;
        Color resultColor = ColorMixLookup.HexToColor(mixedResultHex);

        // Pour stream particles from cauldron to bottles area
        Vector2 bottlesPos = bottlesArea != null ? (Vector2)bottlesArea.position : cauldronCenter + new Vector2(0, -200);

        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            float p = t / 0.6f;
            Vector2 streamPos = Vector2.Lerp(cauldronCenter, bottlesPos, p);
            SpawnSplashParticle(streamPos + Random.insideUnitCircle * 8f, resultColor, fxParent);
            yield return null;
        }

        // Save
        SaveColor(mixedResultHex, cauldronColorA, cauldronColorB);
        RebuildBottles();

        // Sparkles at bottle
        for (int i = 0; i < 10; i++)
            SpawnBurstParticle(bottlesPos, resultColor, fxParent, 100f, 250f);

        yield return new WaitForSeconds(0.3f);

        // Reset cauldron for next mix
        ResetCauldron();
    }

    private void SaveColor(string hex, string parentA, string parentB)
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;
        foreach (var cc in profile.colorStudio.savedColors)
            if (cc.hex == hex) return; // duplicate
        profile.colorStudio.savedColors.Add(new CreatedColor
        { hex = hex, parentAHex = parentA, parentBHex = parentB });
        ProfileManager.Instance.Save();
        FirebaseAnalyticsManager.LogColorMixed(parentA, parentB, hex);
    }

    // ══════════════════════════════════
    //  PARTICLE EFFECTS
    // ══════════════════════════════════

    private void SpawnTrailParticle(Vector2 pos, Color color, float alpha)
    {
        if (circleSprite == null) return;
        Transform parent = rootCanvas != null ? rootCanvas.transform : transform;
        var go = new GameObject("Trail");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        float size = Random.Range(6f, 16f);
        rt.sizeDelta = new Vector2(size, size);
        rt.position = pos + (Vector2)Random.insideUnitCircle * 8f;
        var img = go.AddComponent<Image>();
        img.sprite = circleSprite;
        img.color = new Color(color.r, color.g, color.b, alpha);
        img.raycastTarget = false;
        StartCoroutine(FadeAndDrift(rt, img, 0.35f, new Vector2(0, 15f)));
    }

    private void SpawnSplashParticle(Vector2 pos, Color color, Transform parent)
    {
        if (circleSprite == null) return;
        var go = new GameObject("Splash");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        float size = Random.Range(10f, 22f);
        rt.sizeDelta = new Vector2(size, size);
        rt.position = pos;
        var img = go.AddComponent<Image>();
        img.sprite = circleSprite;
        img.color = new Color(color.r, color.g, color.b, 0.8f);
        img.raycastTarget = false;
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float speed = Random.Range(80f, 180f);
        Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
        StartCoroutine(PhysicsParticle(rt, img, vel, 0.5f));
    }

    private void SpawnSwirlParticle(Vector2 pos, Color color, Transform parent)
    {
        if (circleSprite == null) return;
        var go = new GameObject("Swirl");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        float size = Random.Range(8f, 18f);
        rt.sizeDelta = new Vector2(size, size);
        rt.position = pos;
        var img = go.AddComponent<Image>();
        img.sprite = circleSprite;
        img.color = new Color(color.r, color.g, color.b, 0.7f);
        img.raycastTarget = false;
        StartCoroutine(FadeAndDrift(rt, img, 0.3f, Random.insideUnitCircle * 20f));
    }

    private void SpawnCauldronBubble(Vector2 center, Color color, Transform parent)
    {
        if (circleSprite == null) return;
        var go = new GameObject("Bubble");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        float size = Random.Range(15f, 35f);
        rt.sizeDelta = new Vector2(size, size);
        rt.position = center + new Vector2(Random.Range(-40f, 40f), 0);
        var img = go.AddComponent<Image>();
        img.sprite = circleSprite;
        img.color = new Color(color.r, color.g, color.b, 0.4f);
        img.raycastTarget = false;
        StartCoroutine(BubbleRise(rt, img, Random.Range(0.8f, 1.5f)));
    }

    private void SpawnBurstParticle(Vector2 center, Color color, Transform parent,
        float minSpeed, float maxSpeed)
    {
        if (circleSprite == null) return;
        var go = new GameObject("Burst");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        float size = Random.Range(8f, 20f);
        rt.sizeDelta = new Vector2(size, size);
        rt.position = center;
        var img = go.AddComponent<Image>();
        img.sprite = circleSprite;
        // Mix with white for sparkle variety
        Color sparkle = Random.value > 0.3f ? color : Color.Lerp(color, Color.white, 0.5f);
        img.color = sparkle;
        img.raycastTarget = false;
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float speed = Random.Range(minSpeed, maxSpeed);
        Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
        StartCoroutine(PhysicsParticle(rt, img, vel, Random.Range(0.5f, 0.9f)));
    }

    private IEnumerator ColorWaveRing(Vector2 center, Color color, Transform parent, float maxSize)
    {
        if (circleSprite == null) yield break;
        var go = new GameObject("Wave");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.position = center;
        var img = go.AddComponent<Image>();
        img.sprite = circleSprite;
        img.color = new Color(color.r, color.g, color.b, 0.25f);
        img.raycastTarget = false;
        float t = 0f;
        float dur = 0.6f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float size = Mathf.Lerp(10f, maxSize * 500f, p);
            rt.sizeDelta = new Vector2(size, size);
            img.color = new Color(color.r, color.g, color.b, 0.25f * (1f - p));
            yield return null;
        }
        Destroy(go);
    }

    private IEnumerator SmokePuff(Vector2 pos, Color color, Transform parent)
    {
        if (circleSprite == null) yield break;
        var go = new GameObject("Smoke");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        float size = Random.Range(30f, 60f);
        rt.sizeDelta = new Vector2(size, size);
        rt.position = pos;
        var img = go.AddComponent<Image>();
        img.sprite = circleSprite;
        img.color = new Color(color.r * 0.8f, color.g * 0.8f, color.b * 0.8f, 0.3f);
        img.raycastTarget = false;
        float t = 0f;
        float dur = Random.Range(0.8f, 1.5f);
        Vector2 drift = new Vector2(Random.Range(-20f, 20f), Random.Range(40f, 80f));
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            rt.position = pos + drift * p;
            float s = Mathf.Lerp(size, size * 2f, p);
            rt.sizeDelta = new Vector2(s, s);
            img.color = new Color(img.color.r, img.color.g, img.color.b, 0.3f * (1f - p));
            yield return null;
        }
        Destroy(go);
    }

    private IEnumerator RisingStar(Vector2 center, Color color, Transform parent)
    {
        if (circleSprite == null) yield break;
        yield return new WaitForSeconds(Random.Range(0f, 0.3f));
        var go = new GameObject("Star");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        float size = Random.Range(6f, 14f);
        rt.sizeDelta = new Vector2(size, size);
        rt.position = center + new Vector2(Random.Range(-50f, 50f), 0);
        var img = go.AddComponent<Image>();
        img.sprite = circleSprite;
        img.color = Color.Lerp(color, Color.white, 0.6f);
        img.raycastTarget = false;
        float t = 0f;
        float dur = Random.Range(0.7f, 1.2f);
        float riseH = Random.Range(100f, 200f);
        float swayAmp = Random.Range(15f, 30f);
        Vector2 start = rt.position;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float y = start.y + riseH * p;
            float x = start.x + Mathf.Sin(p * Mathf.PI * 3f) * swayAmp;
            rt.position = new Vector2(x, y);
            float alpha = p < 0.3f ? p / 0.3f : (1f - (p - 0.3f) / 0.7f);
            img.color = new Color(img.color.r, img.color.g, img.color.b, alpha);
            float pulse = 1f + 0.3f * Mathf.Sin(p * Mathf.PI * 5f);
            rt.localScale = Vector3.one * pulse;
            yield return null;
        }
        Destroy(go);
    }

    // ── Particle helpers ──

    private IEnumerator FadeAndDrift(RectTransform rt, Image img, float dur, Vector2 drift)
    {
        Vector2 start = rt.position;
        Color c = img.color;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            rt.position = start + drift * p;
            rt.localScale = Vector3.one * (1f - p * 0.5f);
            img.color = new Color(c.r, c.g, c.b, c.a * (1f - p));
            yield return null;
        }
        Destroy(rt.gameObject);
    }

    private IEnumerator PhysicsParticle(RectTransform rt, Image img, Vector2 vel, float dur)
    {
        Vector2 pos = rt.position;
        Color c = img.color;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            vel.y -= 400f * Time.deltaTime; // gravity
            vel *= 0.97f; // drag
            pos += vel * Time.deltaTime;
            rt.position = pos;
            rt.localScale = Vector3.one * (1f - p * 0.6f);
            img.color = new Color(c.r, c.g, c.b, c.a * (1f - p));
            yield return null;
        }
        Destroy(rt.gameObject);
    }

    private IEnumerator BubbleRise(RectTransform rt, Image img, float dur)
    {
        Vector2 start = rt.position;
        Color c = img.color;
        float t = 0f;
        float swayPhase = Random.Range(0f, Mathf.PI * 2f);
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float y = start.y + 100f * p;
            float x = start.x + Mathf.Sin(swayPhase + p * Mathf.PI * 3f) * 15f;
            rt.position = new Vector2(x, y);
            float grow = 1f + p * 0.3f;
            rt.localScale = Vector3.one * grow;
            img.color = new Color(c.r, c.g, c.b, c.a * (1f - p * 0.7f));
            yield return null;
        }
        // Pop!
        rt.localScale = Vector3.one * 1.5f;
        img.color = new Color(c.r, c.g, c.b, 0.8f);
        yield return new WaitForSeconds(0.05f);
        Destroy(rt.gameObject);
    }

    private IEnumerator PulseButton(RectTransform btnRT)
    {
        while (btnRT != null && hasResult)
        {
            float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 4f);
            btnRT.localScale = Vector3.one * pulse;
            yield return null;
        }
        if (btnRT != null) btnRT.localScale = Vector3.one;
    }

    // ══════════════════════════════════
    //  AMBIENT MAGIC
    // ══════════════════════════════════

    private IEnumerator AmbientMagicLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(1f, 2.5f));

            if (cauldronRT == null) continue;
            Vector2 center = cauldronRT.position;
            Transform parent = rootCanvas != null ? rootCanvas.transform : transform;

            // Ambient bubble from cauldron
            Color ambColor = cauldronFillImage != null ? cauldronFillImage.color : Color.grey;
            SpawnCauldronBubble(center, new Color(ambColor.r, ambColor.g, ambColor.b, 0.2f), parent);

            // Occasional sparkle
            if (Random.value < 0.4f)
            {
                Vector2 sparkPos = center + Random.insideUnitCircle * 60f;
                StartCoroutine(RisingStar(sparkPos, Color.white, parent));
            }
        }
    }

    // ══════════════════════════════════
    //  HISTORY
    // ══════════════════════════════════

    private void ShowHistory(string hex)
    {
        if (historyPanel == null || historyContent == null || isDragging) return;
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        var history = new List<CreatedColor>();
        BuildHistoryRecursive(hex, profile.colorStudio.savedColors, history, new HashSet<string>());
        if (history.Count == 0) return;

        historyPanel.SetActive(true);
        for (int i = historyContent.childCount - 1; i >= 0; i--)
            Destroy(historyContent.GetChild(i).gameObject);

        StartCoroutine(AnimateHistory(history));
    }

    private void BuildHistoryRecursive(string hex, List<CreatedColor> all,
        List<CreatedColor> result, HashSet<string> visited)
    {
        if (ColorMixLookup.IsBaseColor(hex) || visited.Contains(hex)) return;
        visited.Add(hex);
        CreatedColor found = null;
        foreach (var cc in all) if (cc.hex == hex) { found = cc; break; }
        if (found == null) return;
        BuildHistoryRecursive(found.parentAHex, all, result, visited);
        BuildHistoryRecursive(found.parentBHex, all, result, visited);
        result.Add(found);
    }

    private IEnumerator AnimateHistory(List<CreatedColor> history)
    {
        foreach (var step in history)
        {
            var row = new GameObject("Step");
            row.transform.SetParent(historyContent, false);
            row.AddComponent<RectTransform>().sizeDelta = new Vector2(400, 70);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            MakeHistoryCircle(row.transform, step.parentAHex, 55f);
            MakeHistoryLabel(row.transform, "+");
            MakeHistoryCircle(row.transform, step.parentBHex, 55f);
            MakeHistoryLabel(row.transform, "→");
            MakeHistoryCircle(row.transform, step.hex, 60f);

            var rowRT = row.GetComponent<RectTransform>();
            rowRT.localScale = Vector3.zero;
            float t = 0f;
            while (t < 0.25f)
            {
                t += Time.deltaTime;
                rowRT.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, t / 0.25f);
                yield return null;
            }
            rowRT.localScale = Vector3.one;
            yield return new WaitForSeconds(0.25f);
        }
        if (historyContent.childCount > 0)
            UIEffects.SpawnSparkles(historyContent.GetChild(historyContent.childCount - 1).GetComponent<RectTransform>(), 10);
    }

    private void MakeHistoryCircle(Transform parent, string hex, float size)
    {
        var go = new GameObject($"H_{hex}");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        if (circleSprite != null) img.sprite = circleSprite;
        img.color = ColorMixLookup.HexToColor(hex);
        img.raycastTarget = false;
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = size; le.preferredHeight = size;
    }

    private void MakeHistoryLabel(Transform parent, string text)
    {
        var go = new GameObject("L");
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = 30; tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white; tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        go.AddComponent<LayoutElement>().preferredWidth = 30;
    }
}
