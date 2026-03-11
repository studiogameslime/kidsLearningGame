using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Color Mixing mission-mode game.
/// A target color is shown at top. Two empty mix slots sit in the center.
/// Four draggable colors (Red, Blue, Yellow, White) at the bottom.
/// Drag one color to each slot. Once both are filled, they merge with
/// a smooth mixing animation. If the result matches the target → success.
///
/// Combos: Red+Yellow=Orange, Blue+Yellow=Green, Red+Blue=Purple,
///         Red+White=Pink, Blue+White=LightBlue
/// </summary>
public class ColorMixingController : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform playArea;
    public Image targetColorCircle;
    public Image slotLeftImage;       // left empty circle
    public Image slotRightImage;      // right empty circle
    public Image resultCircle;        // result circle between/below slots
    public RectTransform colorPalette;

    [Header("Sprites")]
    public Sprite circleSprite;

    // ── color definitions ────────────────────────────────────────────
    private static readonly Color ColorRed      = new Color(0.90f, 0.15f, 0.15f);
    private static readonly Color ColorBlue     = new Color(0.20f, 0.40f, 0.90f);
    private static readonly Color ColorYellow   = new Color(1.00f, 0.90f, 0.10f);
    private static readonly Color ColorWhite    = new Color(0.97f, 0.97f, 0.97f);
    private static readonly Color ColorOrange   = new Color(1.00f, 0.60f, 0.00f);
    private static readonly Color ColorGreen    = new Color(0.20f, 0.80f, 0.20f);
    private static readonly Color ColorPurple   = new Color(0.60f, 0.20f, 0.80f);
    private static readonly Color ColorPink     = new Color(1.00f, 0.60f, 0.70f);
    private static readonly Color ColorLightBlue = new Color(0.53f, 0.81f, 0.98f);

    private static readonly Color SlotEmpty = new Color(0.88f, 0.88f, 0.88f, 0.5f);

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

    // ── mixing rules ─────────────────────────────────────────────────
    private static readonly Dictionary<string, Color> MixMap = new Dictionary<string, Color>
    {
        { "red+yellow",  ColorOrange },    { "yellow+red",  ColorOrange },
        { "blue+yellow", ColorGreen  },    { "yellow+blue", ColorGreen  },
        { "red+blue",    ColorPurple },    { "blue+red",    ColorPurple },
        { "red+white",   ColorPink },      { "white+red",   ColorPink },
        { "blue+white",  ColorLightBlue }, { "white+blue",  ColorLightBlue },
    };

    // ── runtime state ────────────────────────────────────────────────
    private Canvas canvas;
    private TargetDef currentTarget;
    private string slotLeftColorId;
    private string slotRightColorId;
    private bool isAnimating;
    private List<DraggableColor> paletteButtons = new List<DraggableColor>();
    private string lastTargetName = "";

    // ── lifecycle ────────────────────────────────────────────────────
    private void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        CreatePaletteButtons();
        StartNewRound();
    }

    // ── palette creation ─────────────────────────────────────────────
    private void CreatePaletteButtons()
    {
        float btnSize = 160f;
        float spacing = 30f;
        int count = Primaries.Length;
        float totalW = count * btnSize + (count - 1) * spacing;
        float startX = -totalW / 2f + btnSize / 2f;

        for (int i = 0; i < count; i++)
        {
            var def = Primaries[i];

            var go = new GameObject(def.id + "Color");
            go.transform.SetParent(colorPalette, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(btnSize, btnSize);
            rt.anchoredPosition = new Vector2(startX + i * (btnSize + spacing), 0);

            var img = go.AddComponent<Image>();
            if (circleSprite != null) img.sprite = circleSprite;
            img.color = def.color;
            img.raycastTarget = true;

            // White needs a subtle border so it's visible
            if (def.id == "white")
            {
                var outline = go.AddComponent<Outline>();
                outline.effectColor = new Color(0.8f, 0.8f, 0.8f);
                outline.effectDistance = new Vector2(3, -3);
            }

            // Shine highlight
            var shineGO = new GameObject("Shine");
            shineGO.transform.SetParent(go.transform, false);
            var shineRT = shineGO.AddComponent<RectTransform>();
            shineRT.anchorMin = new Vector2(0.15f, 0.55f);
            shineRT.anchorMax = new Vector2(0.45f, 0.85f);
            shineRT.offsetMin = Vector2.zero;
            shineRT.offsetMax = Vector2.zero;
            var shineImg = shineGO.AddComponent<Image>();
            if (circleSprite != null) shineImg.sprite = circleSprite;
            shineImg.color = new Color(1f, 1f, 1f, 0.35f);
            shineImg.raycastTarget = false;

            go.AddComponent<CanvasGroup>();
            var dc = go.AddComponent<DraggableColor>();
            dc.Init(def.id, def.color, canvas, OnColorDropped);
            dc.SaveHomePosition();

            paletteButtons.Add(dc);
        }
    }

    // ── round management ─────────────────────────────────────────────
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

        // Reset slots
        slotLeftColorId = null;
        slotRightColorId = null;
        if (slotLeftImage != null)
        {
            slotLeftImage.color = SlotEmpty;
            if (slotLeftImage.transform.childCount > 0)
                slotLeftImage.transform.GetChild(0).gameObject.SetActive(true);
        }
        if (slotRightImage != null)
        {
            slotRightImage.color = SlotEmpty;
            if (slotRightImage.transform.childCount > 0)
                slotRightImage.transform.GetChild(0).gameObject.SetActive(true);
        }
        if (resultCircle != null)
        {
            resultCircle.color = new Color(1, 1, 1, 0);
            resultCircle.transform.localScale = Vector3.zero;
        }

        // Show target
        if (targetColorCircle != null)
        {
            targetColorCircle.color = currentTarget.color;
            targetColorCircle.transform.localScale = Vector3.one;
        }

        isAnimating = false;
    }

    // ── drop handler ─────────────────────────────────────────────────
    private void OnColorDropped(DraggableColor dc)
    {
        if (isAnimating)
        {
            dc.ReturnHome(this);
            return;
        }

        // Check which slot it's nearest to
        var dcPos = dc.GetComponent<RectTransform>().position;
        float distLeft = Vector2.Distance(dcPos, slotLeftImage.GetComponent<RectTransform>().position);
        float distRight = Vector2.Distance(dcPos, slotRightImage.GetComponent<RectTransform>().position);

        float threshold = slotLeftImage.GetComponent<RectTransform>().rect.width * 1.2f;

        bool droppedOnLeft = distLeft < threshold && slotLeftColorId == null;
        bool droppedOnRight = distRight < threshold && slotRightColorId == null;

        // Pick the closer empty slot
        if (droppedOnLeft && droppedOnRight)
        {
            if (distLeft <= distRight) droppedOnRight = false;
            else droppedOnLeft = false;
        }

        if (droppedOnLeft)
        {
            StartCoroutine(FillSlot(dc, slotLeftImage, true));
        }
        else if (droppedOnRight)
        {
            StartCoroutine(FillSlot(dc, slotRightImage, false));
        }
        else
        {
            dc.ReturnHome(this);
        }
    }

    private IEnumerator FillSlot(DraggableColor dc, Image slotImg, bool isLeft)
    {
        isAnimating = true;

        // Animate color shrinking into slot
        var rt = dc.GetComponent<RectTransform>();
        Vector2 slotPos = slotImg.GetComponent<RectTransform>().position;
        Vector2 startPos = rt.position;
        Vector3 startScale = rt.localScale;
        float dur = 0.2f;
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            rt.position = Vector2.Lerp(startPos, slotPos, p);
            rt.localScale = startScale * (1f - p * 0.6f);
            yield return null;
        }

        // Return draggable to home
        rt.localScale = startScale;
        dc.ReturnHome(this);

        // Fill the slot
        Color fillColor = dc.color;
        if (isLeft)
            slotLeftColorId = dc.colorId;
        else
            slotRightColorId = dc.colorId;

        // Set exact color with full opacity
        slotImg.color = new Color(fillColor.r, fillColor.g, fillColor.b, 1f);

        // Hide the border ring behind the slot
        if (slotImg.transform.childCount > 0)
            slotImg.transform.GetChild(0).gameObject.SetActive(false);

        // Splash
        SpawnSplash(slotImg.GetComponent<RectTransform>().anchoredPosition, fillColor);

        // Bounce slot
        yield return BounceTransform(slotImg.transform, 1.15f);

        // If both slots filled, trigger mix
        if (slotLeftColorId != null && slotRightColorId != null)
        {
            yield return new WaitForSeconds(0.3f);
            yield return MixAnimation();
        }
        else
        {
            isAnimating = false;
        }
    }

    // ── mixing animation ─────────────────────────────────────────────
    private IEnumerator MixAnimation()
    {
        // Look up result color
        string key = slotLeftColorId + "+" + slotRightColorId;
        Color resultColor;
        if (!MixMap.TryGetValue(key, out resultColor))
            resultColor = Color.Lerp(GetPrimaryColor(slotLeftColorId), GetPrimaryColor(slotRightColorId), 0.5f);

        // Move both slot circles toward the result circle position
        var leftRT = slotLeftImage.GetComponent<RectTransform>();
        var rightRT = slotRightImage.GetComponent<RectTransform>();
        var resultRT = resultCircle.GetComponent<RectTransform>();

        Vector2 leftStart = leftRT.anchoredPosition;
        Vector2 rightStart = rightRT.anchoredPosition;
        Vector2 center = resultRT.anchoredPosition;

        // Show result circle (invisible, will grow)
        resultCircle.color = new Color(resultColor.r, resultColor.g, resultColor.b, 0f);
        resultCircle.transform.localScale = Vector3.zero;

        // Animate slots moving toward center
        float moveDur = 0.5f;
        float t = 0f;
        while (t < moveDur)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0, 1, t / moveDur);
            leftRT.anchoredPosition = Vector2.Lerp(leftStart, center, p);
            rightRT.anchoredPosition = Vector2.Lerp(rightStart, center, p);

            // Shrink slots as they approach
            float shrink = 1f - p * 0.4f;
            leftRT.localScale = Vector3.one * shrink;
            rightRT.localScale = Vector3.one * shrink;

            yield return null;
        }

        // Hide slots, show result
        slotLeftImage.color = new Color(0, 0, 0, 0);
        slotRightImage.color = new Color(0, 0, 0, 0);

        // Result circle grows with a bounce
        resultCircle.color = resultColor;
        float growDur = 0.35f;
        t = 0f;
        while (t < growDur)
        {
            t += Time.deltaTime;
            float p = t / growDur;
            float s = Mathf.Sin(p * Mathf.PI * 0.5f); // ease out
            float bounce = s + 0.15f * Mathf.Sin(p * Mathf.PI * 2f) * (1f - p);
            resultCircle.transform.localScale = Vector3.one * bounce;
            yield return null;
        }
        resultCircle.transform.localScale = Vector3.one;

        // Sparkles from center
        SpawnSplash(center, resultColor);

        yield return new WaitForSeconds(0.3f);

        // Check result
        bool correct = (slotLeftColorId == currentTarget.primaryA && slotRightColorId == currentTarget.primaryB)
                     || (slotLeftColorId == currentTarget.primaryB && slotRightColorId == currentTarget.primaryA);

        if (correct)
            yield return SuccessSequence();
        else
            yield return WrongSequence();
    }

    // ── success ──────────────────────────────────────────────────────
    private IEnumerator SuccessSequence()
    {
        // Pulse result
        yield return BounceTransform(resultCircle.transform, 1.3f);

        // Sparkle burst
        SpawnSuccessSparkles(resultCircle.GetComponent<RectTransform>().anchoredPosition);

        // Bounce target
        yield return BounceTransform(targetColorCircle.transform, 1.3f);

        yield return new WaitForSeconds(0.6f);

        // Reset slot positions before next round
        ResetSlotPositions();
        StartNewRound();
    }

    // ── wrong answer ─────────────────────────────────────────────────
    private IEnumerator WrongSequence()
    {
        yield return ShakeTransform(resultCircle.transform);

        yield return new WaitForSeconds(0.3f);

        // Shrink result away
        float dur = 0.25f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            resultCircle.transform.localScale = Vector3.one * (1f - t / dur);
            yield return null;
        }
        resultCircle.transform.localScale = Vector3.zero;
        resultCircle.color = new Color(1, 1, 1, 0);

        // Reset slots
        ResetSlotPositions();
        slotLeftColorId = null;
        slotRightColorId = null;
        slotLeftImage.color = SlotEmpty;
        slotRightImage.color = SlotEmpty;
        if (slotLeftImage.transform.childCount > 0)
            slotLeftImage.transform.GetChild(0).gameObject.SetActive(true);
        if (slotRightImage.transform.childCount > 0)
            slotRightImage.transform.GetChild(0).gameObject.SetActive(true);

        isAnimating = false;
    }

    private void ResetSlotPositions()
    {
        // Restore slot positions and scale (they moved during mix)
        var leftRT = slotLeftImage.GetComponent<RectTransform>();
        var rightRT = slotRightImage.GetComponent<RectTransform>();

        // Positions are set by setup — store and restore via anchors
        // Since anchors are preserved, just reset offset and scale
        leftRT.localScale = Vector3.one;
        rightRT.localScale = Vector3.one;

        // Re-anchor to original positions
        float slotSpacing = 160f;
        leftRT.anchoredPosition = new Vector2(-slotSpacing, 0);
        rightRT.anchoredPosition = new Vector2(slotSpacing, 0);
    }

    // ── helpers ──────────────────────────────────────────────────────
    private Color GetPrimaryColor(string id)
    {
        foreach (var def in Primaries)
            if (def.id == id) return def.color;
        return Color.white;
    }

    private IEnumerator TransitionColor(Image img, Color from, Color to, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            img.color = Color.Lerp(from, to, t / dur);
            yield return null;
        }
        img.color = to;
    }

    private IEnumerator BounceTransform(Transform tr, float maxScale)
    {
        float dur = 0.15f;
        float t = 0f;
        Vector3 orig = tr.localScale;
        Vector3 big = orig.normalized * maxScale;
        if (orig.magnitude < 0.01f) big = Vector3.one * maxScale;

        while (t < dur)
        {
            t += Time.deltaTime;
            tr.localScale = Vector3.Lerp(orig, big, t / dur);
            yield return null;
        }
        t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            tr.localScale = Vector3.Lerp(big, orig, t / dur);
            yield return null;
        }
        tr.localScale = orig;
    }

    private IEnumerator ShakeTransform(Transform tr)
    {
        Vector3 origin = tr.localPosition;
        float dur = 0.35f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float x = Mathf.Sin(elapsed * 50f) * 14f * (1f - elapsed / dur);
            tr.localPosition = origin + new Vector3(x, 0f, 0f);
            yield return null;
        }
        tr.localPosition = origin;
    }

    // ── UI particles ─────────────────────────────────────────────────
    private void SpawnSplash(Vector2 pos, Color color)
    {
        int count = Random.Range(6, 10);
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(200f, 450f);
            float size = Random.Range(10f, 22f);
            float lifetime = Random.Range(0.3f, 0.55f);
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

            StartCoroutine(AnimateParticle(rt, img, angle, speed, lifetime, 600f));
        }
    }

    private void SpawnSuccessSparkles(Vector2 pos)
    {
        for (int i = 0; i < 14; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(300f, 600f);
            float size = Random.Range(8f, 18f);
            float lifetime = Random.Range(0.4f, 0.7f);
            Color c = Color.Lerp(currentTarget.color, Color.white, Random.Range(0.3f, 0.7f));
            if (Random.value < 0.3f) c = Color.yellow;

            var go = new GameObject("Sparkle");
            go.transform.SetParent(playArea, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            if (circleSprite != null) img.sprite = circleSprite;
            img.color = c;
            img.raycastTarget = false;

            StartCoroutine(AnimateParticle(rt, img, angle, speed, lifetime, 200f));
        }
    }

    private IEnumerator AnimateParticle(RectTransform rt, Image img,
        float angle, float speed, float lifetime, float gravity)
    {
        Vector2 pos = rt.anchoredPosition;
        Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
        Color startColor = img.color;
        float t = 0f;

        while (t < lifetime)
        {
            t += Time.deltaTime;
            velocity.y -= gravity * Time.deltaTime;
            pos += velocity * Time.deltaTime;
            rt.anchoredPosition = pos;
            float fade = 1f - (t / lifetime);
            rt.localScale = Vector3.one * fade;
            img.color = new Color(startColor.r, startColor.g, startColor.b, fade);
            yield return null;
        }
        Destroy(rt.gameObject);
    }

    // ── navigation ───────────────────────────────────────────────────
    public void OnHomePressed() => NavigationManager.GoToMainMenu();

    public void OnRestartPressed()
    {
        StopAllCoroutines();
        ResetSlotPositions();
        StartNewRound();
    }
}
