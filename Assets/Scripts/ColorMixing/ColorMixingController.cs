using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Color Mixing game — tap-to-fill container system (1920×1080 landscape).
///
/// Flow: Tap a color on the palette → paint flies into next small container →
/// when both filled → containers tilt and pour into large mixing container →
/// liquid swirls and settles to result color.
///
/// Combos: Red+Yellow=Orange, Blue+Yellow=Green, Red+Blue=Purple,
///         Red+White=Pink, Blue+White=LightBlue
/// </summary>
public class ColorMixingController : BaseMiniGame
{
    [Header("UI References")]
    public RectTransform playArea;
    public Image targetColorCircle;
    public Image targetGlowImage;
    public Image targetOuterGlowImage;

    [Header("Containers")]
    public Image containerLeftBody;    // small left container glass
    public Image containerLeftFill;    // colored fill inside left
    public Image containerRightBody;
    public Image containerRightFill;
    public Image mixContainerBody;     // large mixing container glass
    public Image mixContainerFill;     // colored fill inside large
    public Image mixContainerGlow;     // glow ring behind large container

    [Header("Palette")]
    public RectTransform colorPalette;

    [Header("Sprites")]
    public Sprite circleSprite;
    public Sprite roundedRectSprite;

    // ── color definitions ────────────────────────────────────────────
    private static readonly Color ColorRed       = new Color(0.92f, 0.18f, 0.18f);
    private static readonly Color ColorBlue      = new Color(0.22f, 0.42f, 0.92f);
    private static readonly Color ColorYellow    = new Color(1.00f, 0.88f, 0.12f);
    private static readonly Color ColorWhite     = new Color(0.97f, 0.97f, 0.97f);
    private static readonly Color ColorOrange    = new Color(1.00f, 0.58f, 0.00f);
    private static readonly Color ColorGreen     = new Color(0.22f, 0.78f, 0.22f);
    private static readonly Color ColorPurple    = new Color(0.58f, 0.22f, 0.78f);
    private static readonly Color ColorPink      = new Color(1.00f, 0.58f, 0.68f);
    private static readonly Color ColorLightBlue = new Color(0.50f, 0.80f, 0.98f);

    private struct PrimaryDef
    {
        public string id;
        public Color color;
        public PrimaryDef(string id, Color c) { this.id = id; color = c; }
    }

    private static readonly PrimaryDef[] Primaries =
    {
        new PrimaryDef("red",    ColorRed),
        new PrimaryDef("blue",   ColorBlue),
        new PrimaryDef("yellow", ColorYellow),
        new PrimaryDef("white",  ColorWhite),
    };

    private struct TargetDef
    {
        public string name;
        public Color color;
        public string primaryA;
        public string primaryB;
        public TargetDef(string n, Color c, string a, string b)
        { name = n; color = c; primaryA = a; primaryB = b; }
    }

    private static readonly TargetDef[] Targets =
    {
        new TargetDef("Orange",     ColorOrange,    "red",  "yellow"),
        new TargetDef("Green",      ColorGreen,     "blue", "yellow"),
        new TargetDef("Purple",     ColorPurple,    "red",  "blue"),
        new TargetDef("Pink",       ColorPink,      "red",  "white"),
        new TargetDef("Light Blue", ColorLightBlue, "blue", "white"),
    };

    private static readonly Dictionary<string, Color> MixMap = new Dictionary<string, Color>
    {
        { "red+yellow",  ColorOrange },    { "yellow+red",  ColorOrange },
        { "blue+yellow", ColorGreen  },    { "yellow+blue", ColorGreen  },
        { "red+blue",    ColorPurple },    { "blue+red",    ColorPurple },
        { "red+white",   ColorPink },      { "white+red",   ColorPink },
        { "blue+white",  ColorLightBlue }, { "white+blue",  ColorLightBlue },
    };

    // ── layout ──────────────────────────────────────────────────────
    private const float BtnSize = 105f;
    private const float BtnSpacing = 50f;

    // ── runtime ─────────────────────────────────────────────────────
    private Canvas canvas;
    private TargetDef currentTarget;
    private string containerLeftColorId;
    private string containerRightColorId;
    private bool isAnimating;
    private List<Button> paletteButtons = new List<Button>();
    private List<Image> paletteBtnImages = new List<Image>();
    private string lastTargetName = "";
    private Coroutine targetPulseRoutine;

    // ── BaseMiniGame Overrides ──

    protected override string GetFallbackGameId() => "colormixing";

    protected override void OnGameInit()
    {
        isEndless = true;
        playConfettiOnRoundWin = true;

        canvas = GetComponentInParent<Canvas>();
        CreatePaletteButtons();
    }

    protected override void OnRoundSetup()
    {
        StartNewRound();
    }

    protected override void OnRoundCleanup()
    {
        // Cleanup handled inline by StartNewRound resets
    }

    // ── palette (tappable buttons) ──────────────────────────────────
    private void CreatePaletteButtons()
    {
        int count = Primaries.Length;
        float totalW = count * BtnSize + (count - 1) * BtnSpacing;
        float startX = -totalW / 2f + BtnSize / 2f;

        for (int i = 0; i < count; i++)
        {
            var def = Primaries[i];

            var go = new GameObject(def.id + "Color");
            go.transform.SetParent(colorPalette, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(BtnSize, BtnSize);
            rt.anchoredPosition = new Vector2(startX + i * (BtnSize + BtnSpacing), 0);

            // Shadow
            var shadowGO = new GameObject("Shadow");
            shadowGO.transform.SetParent(go.transform, false);
            var shadowRT = shadowGO.AddComponent<RectTransform>();
            shadowRT.anchorMin = new Vector2(0.08f, -0.06f);
            shadowRT.anchorMax = new Vector2(0.92f, 0.14f);
            shadowRT.offsetMin = Vector2.zero;
            shadowRT.offsetMax = Vector2.zero;
            var shadowImg = shadowGO.AddComponent<Image>();
            if (circleSprite != null) shadowImg.sprite = circleSprite;
            shadowImg.color = new Color(0f, 0f, 0f, 0.12f);
            shadowImg.raycastTarget = false;

            // Main color blob
            var img = go.AddComponent<Image>();
            if (circleSprite != null) img.sprite = circleSprite;
            img.color = def.color;
            img.raycastTarget = true;

            if (def.id == "white")
            {
                var outline = go.AddComponent<Outline>();
                outline.effectColor = new Color(0.76f, 0.72f, 0.68f);
                outline.effectDistance = new Vector2(2, -2);
            }

            // Glossy shine
            var shineGO = new GameObject("Shine");
            shineGO.transform.SetParent(go.transform, false);
            var shineRT = shineGO.AddComponent<RectTransform>();
            shineRT.anchorMin = new Vector2(0.18f, 0.58f);
            shineRT.anchorMax = new Vector2(0.48f, 0.88f);
            shineRT.offsetMin = Vector2.zero;
            shineRT.offsetMax = Vector2.zero;
            var shineImg = shineGO.AddComponent<Image>();
            if (circleSprite != null) shineImg.sprite = circleSprite;
            shineImg.color = new Color(1f, 1f, 1f, 0.38f);
            shineImg.raycastTarget = false;

            // Button
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;

            string colorId = def.id;
            Color colorVal = def.color;
            btn.onClick.AddListener(() => OnColorTapped(colorId, colorVal, rt));

            paletteButtons.Add(btn);
            paletteBtnImages.Add(img);
        }
    }

    // ── round management ────────────────────────────────────────────
    private void StartNewRound()
    {
        TargetDef next;
        int attempts = 0;
        do
        {
            next = Targets[Random.Range(0, Targets.Length)];
            attempts++;
        } while (next.name == lastTargetName && attempts < 20);

        currentTarget = next;
        lastTargetName = next.name;

        containerLeftColorId = null;
        containerRightColorId = null;

        // Reset small containers
        ResetContainerFill(containerLeftFill);
        ResetContainerFill(containerRightFill);
        ResetContainerTransform(containerLeftBody);
        ResetContainerTransform(containerRightBody);

        // Reset large mixing container
        ResetContainerFill(mixContainerFill);
        ResetContainerTransform(mixContainerBody);
        if (mixContainerGlow != null)
            mixContainerGlow.color = new Color(1, 1, 1, 0);

        // Target with entrance
        if (targetColorCircle != null)
        {
            targetColorCircle.color = currentTarget.color;
            StartCoroutine(TargetEntranceAnim());
        }

        if (targetPulseRoutine != null) StopCoroutine(targetPulseRoutine);
        targetPulseRoutine = StartCoroutine(TargetPulseLoop());

        SetPaletteInteractable(true);
        isAnimating = false;

        // Position tutorial hand: show dragging from a needed color to the left container
        PositionTutorialHandOnPalette();
    }

    private void PositionTutorialHandOnPalette()
    {
        if (TutorialHand == null || paletteButtons.Count == 0) return;

        // Find the palette button matching the first needed primary color
        string neededColor = currentTarget.primaryA;
        RectTransform sourceRT = null;
        for (int i = 0; i < Primaries.Length && i < paletteButtons.Count; i++)
        {
            if (Primaries[i].id == neededColor)
            {
                sourceRT = paletteButtons[i].GetComponent<RectTransform>();
                break;
            }
        }
        if (sourceRT == null) sourceRT = paletteButtons[0].GetComponent<RectTransform>();

        // Convert source button and container positions to hand parent space
        var containerRT = containerLeftBody.GetComponent<RectTransform>();

        Vector2 fromPos = TutorialHand.GetLocalCenter(sourceRT);
        Vector2 toPos = TutorialHand.GetLocalCenter(containerRT);

        TutorialHand.SetMovePath(fromPos, toPos, 0.8f);
    }

    private void ResetContainerFill(Image fill)
    {
        if (fill == null) return;
        fill.color = new Color(1, 1, 1, 0);
        var rt = fill.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.08f, 0.08f);
        rt.anchorMax = new Vector2(0.92f, 0.08f); // 0 height = empty
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Clean up any leftover FillRight layers from previous round
        var body = fill.transform.parent;
        if (body != null)
        {
            var old = body.Find("FillRight");
            if (old != null) Destroy(old.gameObject);
        }
    }

    private void ResetContainerTransform(Image body)
    {
        if (body == null) return;
        body.transform.parent.localEulerAngles = Vector3.zero;
        body.transform.parent.localScale = Vector3.one;
    }

    private void SetPaletteInteractable(bool state)
    {
        foreach (var btn in paletteButtons)
            if (btn != null) btn.interactable = state;
    }

    // ── color tap handler ───────────────────────────────────────────
    private void OnColorTapped(string colorId, Color color, RectTransform btnRT)
    {
        if (IsInputLocked || isAnimating) return;

        DismissTutorial();

        // Prevent picking the same color twice
        if (containerLeftColorId == colorId) return;

        // Determine which container to fill
        bool fillLeft = containerLeftColorId == null;
        bool fillRight = !fillLeft && containerRightColorId == null;

        if (!fillLeft && !fillRight) return; // both full

        isAnimating = true;
        SetPaletteInteractable(false);

        Image targetFill = fillLeft ? containerLeftFill : containerRightFill;
        Image targetBody = fillLeft ? containerLeftBody : containerRightBody;

        if (fillLeft)
            containerLeftColorId = colorId;
        else
            containerRightColorId = colorId;

        // Bounce the tapped button
        StartCoroutine(TapBounce(btnRT));

        StartCoroutine(FillContainerSequence(colorId, color, btnRT, targetFill, targetBody));
    }

    private IEnumerator TapBounce(RectTransform rt)
    {
        Vector3 orig = rt.localScale;
        float dur = 0.15f;
        float t = 0;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float s = 1f + 0.15f * Mathf.Sin(p * Mathf.PI);
            rt.localScale = orig * s;
            yield return null;
        }
        rt.localScale = orig;
    }

    private IEnumerator FillContainerSequence(string colorId, Color color,
        RectTransform fromRT, Image fill, Image body)
    {
        // Play color name
        SoundLibrary.PlayColorName(GetColorSoundName(colorId));

        // Animate a paint blob flying from button to container
        Vector2 fromPos = GetWorldAnchoredPos(fromRT);
        Vector2 toPos = GetWorldAnchoredPos(body.GetComponent<RectTransform>());

        yield return AnimatePaintBlob(fromPos, toPos, color);

        // Fill container from bottom with liquid animation
        yield return AnimateContainerFill(fill, color, 0.35f);

        // Splash on fill
        SpawnSplash(toPos, color, 6);

        // Bounce the container
        yield return ElasticBounce(body.transform.parent, 1.08f, 0.2f);

        // Check if both containers are now filled
        if (containerLeftColorId != null && containerRightColorId != null)
        {
            yield return new WaitForSeconds(0.3f);
            yield return PourAndMixAnimation();
        }
        else
        {
            SetPaletteInteractable(true);
            isAnimating = false;
        }
    }

    /// <summary>
    /// Animate a paint blob arcing from palette button to container.
    /// </summary>
    private IEnumerator AnimatePaintBlob(Vector2 from, Vector2 to, Color color)
    {
        var go = new GameObject("PaintBlob");
        go.transform.SetParent(playArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(40, 40);
        var img = go.AddComponent<Image>();
        if (circleSprite != null) img.sprite = circleSprite;
        img.color = color;
        img.raycastTarget = false;

        // Shine on blob
        var shineGO = new GameObject("Shine");
        shineGO.transform.SetParent(go.transform, false);
        var shineRT = shineGO.AddComponent<RectTransform>();
        shineRT.anchorMin = new Vector2(0.2f, 0.6f);
        shineRT.anchorMax = new Vector2(0.5f, 0.9f);
        shineRT.offsetMin = Vector2.zero;
        shineRT.offsetMax = Vector2.zero;
        var shineImg = shineGO.AddComponent<Image>();
        if (circleSprite != null) shineImg.sprite = circleSprite;
        shineImg.color = new Color(1f, 1f, 1f, 0.35f);
        shineImg.raycastTarget = false;

        float dur = 0.35f;
        float t = 0;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float ease = p * p * (3f - 2f * p);
            Vector2 pos = Vector2.Lerp(from, to, ease);
            // Arc upward
            pos.y += Mathf.Sin(p * Mathf.PI) * 80f;
            rt.anchoredPosition = pos;
            float scale = 1f + 0.3f * Mathf.Sin(p * Mathf.PI); // grow at peak
            rt.localScale = Vector3.one * scale;
            yield return null;
        }

        // Small splash on arrival
        SpawnSplash(to, color, 5);
        Destroy(go);
    }

    /// <summary>
    /// Fill a container from bottom to full over duration.
    /// </summary>
    private IEnumerator AnimateContainerFill(Image fill, Color color, float dur)
    {
        var rt = fill.GetComponent<RectTransform>();
        fill.color = color;
        float t = 0;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float ease = Mathf.Sin(p * Mathf.PI * 0.5f); // ease out
            // Grow fill from bottom: anchorMax.y goes from 0 to 0.85
            rt.anchorMin = new Vector2(0.08f, 0.08f);
            rt.anchorMax = new Vector2(0.92f, 0.08f + 0.78f * ease);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            yield return null;
        }
    }

    // ── pour & mix animation ────────────────────────────────────────
    private IEnumerator PourAndMixAnimation()
    {
        // Compute result color
        string key = containerLeftColorId + "+" + containerRightColorId;
        Color resultColor;
        if (!MixMap.TryGetValue(key, out resultColor))
            resultColor = Color.Lerp(
                GetPrimaryColor(containerLeftColorId),
                GetPrimaryColor(containerRightColorId), 0.5f);

        Color leftColor = GetPrimaryColor(containerLeftColorId);
        Color rightColor = GetPrimaryColor(containerRightColorId);

        var leftParent = containerLeftBody.transform.parent;
        var rightParent = containerRightBody.transform.parent;
        var mixRT = mixContainerBody.GetComponent<RectTransform>();
        Vector2 mixPos = GetWorldAnchoredPos(mixRT);

        var leftFillRT = containerLeftFill.GetComponent<RectTransform>();
        var rightFillRT = containerRightFill.GetComponent<RectTransform>();
        var mixFillRT = mixContainerFill.GetComponent<RectTransform>();

        // ── Phase 1: Tilt containers toward center ──
        float tiltDur = 0.4f;
        float t = 0;
        while (t < tiltDur)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0, 1, t / tiltDur);
            leftParent.localEulerAngles = new Vector3(0, 0, -25f * p);
            rightParent.localEulerAngles = new Vector3(0, 0, 25f * p);
            yield return null;
        }

        // ── Phase 2: Pour streams + fill big jar with TWO visible colors ──
        // Create a second fill image for the right color (layered on top)
        var rightFillLayer = new GameObject("FillRight");
        rightFillLayer.transform.SetParent(mixContainerBody.transform, false);
        var rfRT = rightFillLayer.AddComponent<RectTransform>();
        rfRT.anchorMin = new Vector2(0.50f, 0.06f);
        rfRT.anchorMax = new Vector2(0.94f, 0.06f); // starts empty
        rfRT.offsetMin = Vector2.zero;
        rfRT.offsetMax = Vector2.zero;
        var rfImg = rightFillLayer.AddComponent<Image>();
        if (roundedRectSprite != null) rfImg.sprite = roundedRectSprite;
        rfImg.type = Image.Type.Sliced;
        rfImg.color = rightColor;
        rfImg.raycastTarget = false;

        // Start pour streams
        StartCoroutine(PourStream(leftParent, mixPos, leftColor, 0.7f));
        StartCoroutine(PourStream(rightParent, mixPos, rightColor, 0.7f));

        // Pour phase: drain small, fill big with split colors
        float pourDur = 0.7f;
        mixContainerFill.color = leftColor; // main fill = left color

        t = 0;
        while (t < pourDur)
        {
            t += Time.deltaTime;
            float p = t / pourDur;
            float ease = Mathf.SmoothStep(0, 1, p);

            // Drain small containers
            float drainH = 0.08f + 0.78f * (1f - ease);
            leftFillRT.anchorMax = new Vector2(0.92f, drainH);
            leftFillRT.offsetMax = Vector2.zero;
            rightFillRT.anchorMax = new Vector2(0.92f, drainH);
            rightFillRT.offsetMax = Vector2.zero;

            // Fill big jar — left color fills left half, right color fills right half
            float fillH = 0.06f + 0.72f * ease;
            mixFillRT.anchorMin = new Vector2(0.06f, 0.06f);
            mixFillRT.anchorMax = new Vector2(0.50f, fillH);
            mixFillRT.offsetMin = Vector2.zero;
            mixFillRT.offsetMax = Vector2.zero;

            rfRT.anchorMin = new Vector2(0.50f, 0.06f);
            rfRT.anchorMax = new Vector2(0.94f, fillH);
            rfRT.offsetMin = Vector2.zero;
            rfRT.offsetMax = Vector2.zero;

            yield return null;
        }

        // ── Phase 3: Un-tilt containers ──
        float untiltDur = 0.3f;
        t = 0;
        while (t < untiltDur)
        {
            t += Time.deltaTime;
            float p = t / untiltDur;
            leftParent.localEulerAngles = new Vector3(0, 0, -25f * (1f - p));
            rightParent.localEulerAngles = new Vector3(0, 0, 25f * (1f - p));
            yield return null;
        }
        leftParent.localEulerAngles = Vector3.zero;
        rightParent.localEulerAngles = Vector3.zero;

        yield return new WaitForSeconds(0.15f);

        // ── Phase 4: Visible swirl mixing inside the jar ──
        // Swirl particles on top while the fill colors gradually blend
        yield return SwirlMixAnimation(mixPos, mixFillRT, rfRT, rfImg,
            leftColor, rightColor, resultColor);

        // ── Phase 5: Settle to final color ──
        // Remove the right-side layer, expand main fill to full width with result color
        Destroy(rightFillLayer);
        mixContainerFill.color = resultColor;
        mixFillRT.anchorMin = new Vector2(0.06f, 0.06f);
        mixFillRT.anchorMax = new Vector2(0.94f, 0.78f);
        mixFillRT.offsetMin = Vector2.zero;
        mixFillRT.offsetMax = Vector2.zero;

        // Ripple + glow
        SpawnRipple(mixPos, resultColor);
        SpawnSplash(mixPos, resultColor, 8);

        // Gentle bounce
        yield return ElasticBounce(mixContainerBody.transform.parent, 1.06f, 0.2f);

        // Play color name
        string resultName = GetMixedColorName(key);
        if (!string.IsNullOrEmpty(resultName))
            SoundLibrary.PlayColorName(resultName);

        yield return new WaitForSeconds(0.35f);

        // Check correctness
        bool correct = (containerLeftColorId == currentTarget.primaryA && containerRightColorId == currentTarget.primaryB)
                     || (containerLeftColorId == currentTarget.primaryB && containerRightColorId == currentTarget.primaryA);

        if (correct)
        {
            Stats?.RecordCorrect();
            yield return SuccessSequence(resultColor);
        }
        else
        {
            Stats?.RecordMistake();
            yield return WrongSequence();
        }
    }

    /// <summary>
    /// Visible swirl mixing: colored streaks orbit inside the jar while
    /// the two fill halves gradually blend into the result color.
    /// </summary>
    private IEnumerator SwirlMixAnimation(Vector2 center,
        RectTransform leftFillRT, RectTransform rightFillRT, Image rightFillImg,
        Color colorA, Color colorB, Color resultColor)
    {
        // Spawn swirl streak particles
        int swirlCount = 16;
        var swirlGOs = new List<GameObject>();
        var swirlColors = new List<Color>();
        for (int i = 0; i < swirlCount; i++)
        {
            var go = new GameObject("Swirl");
            go.transform.SetParent(playArea, false);
            var rt = go.AddComponent<RectTransform>();
            // Mix of sizes — some large streaks, some small dots
            float sz = i < 6 ? Random.Range(14f, 24f) : Random.Range(6f, 14f);
            rt.sizeDelta = new Vector2(sz, sz * Random.Range(0.6f, 1.0f));
            var img = go.AddComponent<Image>();
            if (circleSprite != null) img.sprite = circleSprite;
            Color c = i % 2 == 0 ? colorA : colorB;
            // Add slight variation
            c = Color.Lerp(c, Color.white, Random.Range(0f, 0.15f));
            img.color = c;
            img.raycastTarget = false;
            swirlGOs.Add(go);
            swirlColors.Add(c);
        }

        float swirlDur = 1.2f;
        float t = 0;
        float maxRadius = 50f;

        while (t < swirlDur)
        {
            t += Time.deltaTime;
            float p = t / swirlDur;

            // Swirl speed: fast at start, slowing down
            float speedMult = 1f + 2f * (1f - p);
            // Radius: starts wide, contracts
            float radius = maxRadius * (1f - p * 0.85f);

            for (int i = 0; i < swirlGOs.Count; i++)
            {
                if (swirlGOs[i] == null) continue;
                var rt = swirlGOs[i].GetComponent<RectTransform>();
                var img = swirlGOs[i].GetComponent<Image>();

                // Each particle has different phase offset for organic feel
                float phaseOffset = i * (360f / swirlCount) + (i % 3) * 40f;
                float angle = (phaseOffset + p * 540f * speedMult) * Mathf.Deg2Rad;

                // Wobble radius per particle
                float r = radius * (0.7f + 0.3f * Mathf.Sin(i * 1.7f + p * 8f));
                Vector2 pos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                rt.anchoredPosition = pos;

                // Rotate streaks along their motion direction
                float rotAngle = angle * Mathf.Rad2Deg + 90f;
                rt.localEulerAngles = new Vector3(0, 0, rotAngle);

                // Blend toward result color over time
                Color blended = Color.Lerp(swirlColors[i], resultColor, p * p);
                float alpha;
                if (p < 0.7f)
                    alpha = 0.85f;
                else
                    alpha = 0.85f * (1f - (p - 0.7f) / 0.3f);
                img.color = new Color(blended.r, blended.g, blended.b, alpha);
            }

            // Simultaneously blend the fill colors toward result
            // Left fill expands toward center, right fill shrinks
            float blendP = Mathf.SmoothStep(0, 1, p);
            Color leftBlend = Color.Lerp(colorA, resultColor, blendP);
            Color rightBlend = Color.Lerp(colorB, resultColor, blendP);
            mixContainerFill.color = leftBlend;
            rightFillImg.color = rightBlend;

            // Gradually merge the two halves: left expands right, right shrinks left
            float splitX = Mathf.Lerp(0.50f, 0.94f, blendP);
            leftFillRT.anchorMax = new Vector2(splitX, leftFillRT.anchorMax.y);
            leftFillRT.offsetMax = Vector2.zero;
            rightFillRT.anchorMin = new Vector2(splitX, rightFillRT.anchorMin.y);
            rightFillRT.offsetMin = Vector2.zero;

            yield return null;
        }

        // Clean up swirl particles
        foreach (var go in swirlGOs)
            if (go != null) Destroy(go);
    }

    /// <summary>
    /// Spawn a stream of colored droplets from container to mix target.
    /// </summary>
    private IEnumerator PourStream(Transform fromContainer, Vector2 toPos, Color color, float dur)
    {
        float t = 0;
        float spawnInterval = 0.035f;
        float nextSpawn = 0;

        while (t < dur)
        {
            t += Time.deltaTime;
            nextSpawn -= Time.deltaTime;

            if (nextSpawn <= 0)
            {
                nextSpawn = spawnInterval;
                Vector2 fromPos = GetWorldAnchoredPos(fromContainer.GetComponent<RectTransform>());
                fromPos.y -= 20f; // from bottom of container

                float dropSize = Random.Range(8f, 16f);
                Color dropColor = Color.Lerp(color, Color.white, Random.Range(0f, 0.15f));

                var go = new GameObject("Drop");
                go.transform.SetParent(playArea, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchoredPosition = fromPos;
                rt.sizeDelta = new Vector2(dropSize, dropSize);
                var img = go.AddComponent<Image>();
                if (circleSprite != null) img.sprite = circleSprite;
                img.color = dropColor;
                img.raycastTarget = false;

                StartCoroutine(AnimateDrop(rt, img, fromPos, toPos, Random.Range(0.2f, 0.35f)));
            }
            yield return null;
        }
    }

    private IEnumerator AnimateDrop(RectTransform rt, Image img, Vector2 from, Vector2 to, float dur)
    {
        float t = 0;
        Vector2 offset = new Vector2(Random.Range(-15f, 15f), Random.Range(-5f, 5f));
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            rt.anchoredPosition = Vector2.Lerp(from, to + offset, p * p); // accelerate
            float fade = p < 0.8f ? 1f : (1f - (p - 0.8f) / 0.2f);
            img.color = new Color(img.color.r, img.color.g, img.color.b, fade);
            yield return null;
        }
        Destroy(rt.gameObject);
    }

    // ── success ─────────────────────────────────────────────────────
    private IEnumerator SuccessSequence(Color resultColor)
    {
        if (targetPulseRoutine != null) StopCoroutine(targetPulseRoutine);

        // Glow around mixing container
        if (mixContainerGlow != null)
        {
            float t = 0;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(0f, 0.35f, t / 0.3f);
                mixContainerGlow.color = new Color(resultColor.r, resultColor.g, resultColor.b, a);
                yield return null;
            }
        }

        // Bounce mixing container
        yield return ElasticBounce(mixContainerBody.transform.parent, 1.12f, 0.3f);

        // Sparkles
        Vector2 mixPos = GetWorldAnchoredPos(mixContainerBody.GetComponent<RectTransform>());
        SpawnSuccessSparkles(mixPos);

        // Bounce target
        yield return ElasticBounce(targetColorCircle.transform.parent.parent, 1.1f, 0.25f);

        // Complete the round — triggers confetti + sound via BaseMiniGame
        CompleteRound();

        yield return new WaitForSeconds(0.6f);

        // Fade glow
        if (mixContainerGlow != null)
        {
            float t2 = 0;
            Color glowC = mixContainerGlow.color;
            while (t2 < 0.3f)
            {
                t2 += Time.deltaTime;
                mixContainerGlow.color = new Color(glowC.r, glowC.g, glowC.b, glowC.a * (1f - t2 / 0.3f));
                yield return null;
            }
        }
    }

    // ── wrong answer — reset containers but keep same target ────────
    private IEnumerator WrongSequence()
    {
        // Gentle wobble on mixing container
        yield return WobbleTransform(mixContainerBody.transform.parent);

        // Hold result visible
        yield return new WaitForSeconds(0.8f);

        // Fade fill out
        float dur = 0.4f;
        float t = 0;
        Color startColor = mixContainerFill.color;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            mixContainerFill.color = new Color(startColor.r, startColor.g, startColor.b, 1f - p);
            yield return null;
        }

        // Reset containers but keep the same target color
        containerLeftColorId = null;
        containerRightColorId = null;
        ResetContainerFill(containerLeftFill);
        ResetContainerFill(containerRightFill);
        ResetContainerFill(mixContainerFill);
        ResetContainerTransform(containerLeftBody);
        ResetContainerTransform(containerRightBody);
        ResetContainerTransform(mixContainerBody);
        if (mixContainerGlow != null)
            mixContainerGlow.color = new Color(1, 1, 1, 0);

        SetPaletteInteractable(true);
        isAnimating = false;
    }

    // ── target animations ───────────────────────────────────────────
    private IEnumerator TargetEntranceAnim()
    {
        var tr = targetColorCircle.transform;
        tr.localScale = Vector3.zero;
        float dur = 0.4f;
        float t = 0;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float elastic = 1f + Mathf.Pow(2f, -10f * p) *
                Mathf.Sin((p - 0.075f) * (2f * Mathf.PI) / 0.3f) * -1f;
            tr.localScale = Vector3.one * elastic;
            yield return null;
        }
        tr.localScale = Vector3.one;
    }

    private IEnumerator TargetPulseLoop()
    {
        var tr = targetColorCircle.transform;
        float phase = 0;
        while (true)
        {
            phase += Time.deltaTime;
            float breath = 1f + 0.05f * Mathf.Sin(phase * 1.8f);
            tr.localScale = Vector3.one * breath;

            if (targetGlowImage != null)
            {
                float a = 0.18f + 0.08f * Mathf.Sin(phase * 1.8f + Mathf.PI);
                targetGlowImage.color = new Color(1f, 1f, 1f, a);
            }
            if (targetOuterGlowImage != null)
            {
                float s = 1f + 0.04f * Mathf.Sin(phase * 1.2f + 0.5f);
                targetOuterGlowImage.transform.localScale = Vector3.one * s;
            }
            yield return null;
        }
    }

    // ── animation helpers ───────────────────────────────────────────
    private Vector2 GetWorldAnchoredPos(RectTransform rt)
    {
        // Convert world position to playArea anchored position
        Vector2 worldPos = rt.position;
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            playArea, RectTransformUtility.WorldToScreenPoint(null, worldPos),
            null, out localPoint);
        return localPoint;
    }

    private Color GetPrimaryColor(string id)
    {
        foreach (var def in Primaries)
            if (def.id == id) return def.color;
        return Color.white;
    }

    private IEnumerator ElasticBounce(Transform tr, float maxScale, float dur)
    {
        Vector3 orig = tr.localScale;
        float t = 0;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float s;
            if (p < 0.4f)
            {
                float rp = p / 0.4f;
                s = 1f + (maxScale - 1f) * Mathf.Sin(rp * Mathf.PI * 0.5f);
            }
            else
            {
                float sp = (p - 0.4f) / 0.6f;
                s = 1f + (maxScale - 1f) * Mathf.Exp(-sp * 4f) * Mathf.Cos(sp * Mathf.PI * 2f);
            }
            tr.localScale = Vector3.one * s;
            yield return null;
        }
        tr.localScale = orig;
    }

    private IEnumerator WobbleTransform(Transform tr)
    {
        Vector3 origin = tr.localPosition;
        float dur = 0.4f;
        float t = 0;
        while (t < dur)
        {
            t += Time.deltaTime;
            float decay = 1f - t / dur;
            float x = Mathf.Sin(t * 35f) * 8f * decay;
            float y = Mathf.Cos(t * 28f) * 2f * decay;
            tr.localPosition = origin + new Vector3(x, y, 0f);
            yield return null;
        }
        tr.localPosition = origin;
    }

    // ── particles ───────────────────────────────────────────────────
    private void SpawnSplash(Vector2 pos, Color color, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(150f, 380f);
            float size = Random.Range(6f, 18f);
            float life = Random.Range(0.3f, 0.55f);
            Color c = Color.Lerp(color, Color.white, Random.Range(0f, 0.3f));

            var go = new GameObject("Splash");
            go.transform.SetParent(playArea, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            if (circleSprite != null) img.sprite = circleSprite;
            img.color = c;
            img.raycastTarget = false;

            StartCoroutine(AnimateParticle(rt, img, angle, speed, life, 400f));
        }
    }

    private void SpawnRipple(Vector2 pos, Color color)
    {
        StartCoroutine(AnimateRipple(pos, color, 0.45f));
        StartCoroutine(AnimateRipple(pos, color, 0.55f));
    }

    private IEnumerator AnimateRipple(Vector2 pos, Color color, float dur)
    {
        var go = new GameObject("Ripple");
        go.transform.SetParent(playArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(20, 20);
        var img = go.AddComponent<Image>();
        if (circleSprite != null) img.sprite = circleSprite;
        img.color = new Color(color.r, color.g, color.b, 0.35f);
        img.raycastTarget = false;

        float t = 0;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float size = Mathf.Lerp(20f, 220f, p);
            rt.sizeDelta = new Vector2(size, size);
            img.color = new Color(color.r, color.g, color.b, 0.35f * (1f - p));
            yield return null;
        }
        Destroy(go);
    }

    private void SpawnSuccessSparkles(Vector2 pos)
    {
        for (int i = 0; i < 16; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(250f, 520f);
            float size = Random.Range(6f, 16f);
            float life = Random.Range(0.4f, 0.7f);
            Color c = Color.Lerp(currentTarget.color, Color.white, Random.Range(0.3f, 0.7f));
            if (Random.value < 0.25f) c = new Color(1f, 0.95f, 0.4f);

            var go = new GameObject("Sparkle");
            go.transform.SetParent(playArea, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            if (circleSprite != null) img.sprite = circleSprite;
            img.color = c;
            img.raycastTarget = false;

            StartCoroutine(AnimateParticle(rt, img, angle, speed, life, 160f));
        }
    }

    private IEnumerator AnimateParticle(RectTransform rt, Image img,
        float angle, float speed, float life, float gravity)
    {
        Vector2 pos = rt.anchoredPosition;
        Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
        Color sc = img.color;
        float t = 0;
        while (t < life)
        {
            t += Time.deltaTime;
            vel.y -= gravity * Time.deltaTime;
            pos += vel * Time.deltaTime;
            rt.anchoredPosition = pos;
            float fade = 1f - t / life;
            rt.localScale = Vector3.one * (0.3f + 0.7f * fade);
            img.color = new Color(sc.r, sc.g, sc.b, fade);
            yield return null;
        }
        Destroy(rt.gameObject);
    }

    // ── navigation ──────────────────────────────────────────────────
    public void OnHomePressed() => ExitGame();

    private static string GetColorSoundName(string colorId)
    {
        switch (colorId)
        {
            case "red":    return "Red";
            case "blue":   return "Blue";
            case "yellow": return "Yellow";
            case "white":  return "White";
            default:       return colorId;
        }
    }

    private static string GetMixedColorName(string mixKey)
    {
        switch (mixKey)
        {
            case "red+yellow":  case "yellow+red":  return "Orange";
            case "blue+yellow": case "yellow+blue": return "Green";
            case "red+blue":    case "blue+red":    return "Purple";
            case "red+white":   case "white+red":   return "Pink";
            case "blue+white":  case "white+blue":  return "Light Blue";
            default: return null;
        }
    }
}
