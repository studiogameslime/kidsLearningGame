using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bakery Game — drag cookies from RIGHT panel onto matching tray slots on LEFT.
/// Slots show indented silhouettes. Cookies are arranged in a clean grid.
/// </summary>
public class BakeryGameController : BaseMiniGame
{
    [Header("References")]
    public RectTransform trayArea;       // slot container inside the tray
    public RectTransform cookiesArea;    // cookie container on the right
    public Image trayImage;              // tray surface (for completion bounce)
    public Sprite[] cookieSprites;       // Cookies_0..7
    public Sprite roundedRect;
    public Sprite circleSprite;

    [Header("Slot Styling")]
    public Color slotColor     = new Color(0.62f, 0.48f, 0.33f, 1f);
    public Color slotEdgeLight = new Color(1f, 1f, 1f, 0.18f);
    public Color slotEdgeDark  = new Color(0f, 0f, 0f, 0.25f);

    private Canvas canvas;
    private List<BakeryDraggable> cookies = new List<BakeryDraggable>();
    private List<BakerySlot> slots = new List<BakerySlot>();
    private int matchedCount;
    private int cookieCount;

    private const float MATCH_THRESHOLD = 0.8f;

    protected override string GetFallbackGameId() => "bakery";

    protected override void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        base.Start();
    }

    protected override void OnGameInit()
    {
        totalRounds = 1;
        isEndless = true;
        playWinSound = true;
        playConfettiOnRoundWin = true;
        delayBeforeNextRound = 0.8f;
    }

    protected override void OnRoundSetup()
    {
        matchedCount = 0;
        cookieCount = BakeryLevels.CookieCount(Difficulty);
        int[] indices = BakeryLevels.PickCookieIndices(cookieCount);

        LayoutSlots(indices);
        LayoutCookies(indices);

        // Tutorial: hint from first cookie to its matching slot
        if (cookies.Count > 0 && slots.Count > 0 && TutorialHand != null)
        {
            BakerySlot target = null;
            foreach (var s in slots)
                if (s.cookieId == cookies[0].cookieId) { target = s; break; }
            if (target != null)
            {
                var hp = TutorialHand.transform.parent as RectTransform;
                TutorialHand.SetMovePath(WorldToLocal(cookies[0].RT, hp), WorldToLocal(target.RT, hp), 1.5f);
            }
        }
    }

    protected override void OnRoundCleanup()
    {
        foreach (var c in cookies) if (c != null) Destroy(c.gameObject);
        foreach (var s in slots) if (s != null) Destroy(s.gameObject);
        cookies.Clear();
        slots.Clear();
    }

    // ═══════════════════════════════════════════════════════════
    //  SLOT LAYOUT (LEFT — indented silhouettes, no cookie art)
    // ═══════════════════════════════════════════════════════════

    private void LayoutSlots(int[] indices)
    {
        float trayW = trayArea.rect.width;
        float trayH = trayArea.rect.height;
        if (trayW <= 0) trayW = 700f;
        if (trayH <= 0) trayH = 600f;

        int cols = cookieCount / 2;
        int rows = 2;
        float padding = 20f;
        float cellW = (trayW - padding * 2) / cols;
        float cellH = (trayH - padding * 2) / rows;
        float slotSize = Mathf.Min(cellW, cellH) * 0.78f;

        for (int i = 0; i < cookieCount; i++)
        {
            int col = i % cols;
            int row = i / cols;

            float cx = -trayW * 0.5f + padding + cellW * (col + 0.5f);
            float cy = trayH * 0.5f - padding - cellH * (row + 0.5f);

            // ── Slot container ──
            var slotGO = new GameObject($"Slot_{i}");
            slotGO.transform.SetParent(trayArea, false);
            var slotRT = slotGO.AddComponent<RectTransform>();
            slotRT.sizeDelta = new Vector2(slotSize, slotSize);
            slotRT.anchoredPosition = new Vector2(cx, cy);

            // Use the ACTUAL cookie sprite as the slot silhouette shape
            Sprite shapeSprite = (indices[i] < cookieSprites.Length) ? cookieSprites[indices[i]] : null;

            // Inner shadow layer (bottom-right offset) — renders FIRST (behind)
            var shadowGO = new GameObject("InnerShadow");
            shadowGO.transform.SetParent(slotGO.transform, false);
            var shRT = shadowGO.AddComponent<RectTransform>();
            shRT.anchorMin = Vector2.zero; shRT.anchorMax = Vector2.one;
            shRT.offsetMin = new Vector2(4, 0); shRT.offsetMax = new Vector2(0, -4);
            var shImg = shadowGO.AddComponent<Image>();
            if (shapeSprite != null) shImg.sprite = shapeSprite;
            shImg.preserveAspect = true;
            shImg.color = slotEdgeDark;
            shImg.raycastTarget = false;

            // Main indented fill — cookie silhouette, darker than tray
            var fillImg = slotGO.AddComponent<Image>();
            if (shapeSprite != null) fillImg.sprite = shapeSprite;
            fillImg.preserveAspect = true;
            fillImg.color = slotColor;
            fillImg.raycastTarget = false;

            // Edge highlight layer (top-left offset) — renders LAST (on top)
            var hlGO = new GameObject("EdgeHighlight");
            hlGO.transform.SetParent(slotGO.transform, false);
            var hlRT = hlGO.AddComponent<RectTransform>();
            hlRT.anchorMin = Vector2.zero; hlRT.anchorMax = Vector2.one;
            hlRT.offsetMin = new Vector2(0, 4); hlRT.offsetMax = new Vector2(-4, 0);
            var hlImg = hlGO.AddComponent<Image>();
            if (shapeSprite != null) hlImg.sprite = shapeSprite;
            hlImg.preserveAspect = true;
            hlImg.color = slotEdgeLight;
            hlImg.raycastTarget = false;

            var slot = slotGO.AddComponent<BakerySlot>();
            slot.cookieId = indices[i];
            slots.Add(slot);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  COOKIE LAYOUT (RIGHT — clean grid)
    // ═══════════════════════════════════════════════════════════

    private void LayoutCookies(int[] indices)
    {
        // Shuffle order so cookies don't line up with slot positions
        var order = new List<int>();
        for (int i = 0; i < cookieCount; i++) order.Add(i);
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = order[i]; order[i] = order[j]; order[j] = tmp;
        }

        float areaW = cookiesArea.rect.width;
        float areaH = cookiesArea.rect.height;
        if (areaW <= 0) areaW = 600f;
        if (areaH <= 0) areaH = 600f;

        // Mirror the slot grid: 2 rows, N/2 cols
        int cols = cookieCount / 2;
        int rows = 2;
        float padding = 16f;
        float cellW = (areaW - padding * 2) / cols;
        float cellH = (areaH - padding * 2) / rows;
        float cookieSize = Mathf.Min(cellW, cellH) * 0.82f;

        for (int i = 0; i < cookieCount; i++)
        {
            int idx = order[i];
            int cookieId = indices[idx];

            int col = i % cols;
            int row = i / cols;

            float cx = -areaW * 0.5f + padding + cellW * (col + 0.5f);
            float cy = areaH * 0.5f - padding - cellH * (row + 0.5f);

            var go = new GameObject($"Cookie_{cookieId}");
            // Parent to canvas root for free dragging across the screen
            go.transform.SetParent(canvas.transform, false);
            go.transform.SetAsLastSibling();

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(cookieSize, cookieSize);

            // Convert grid position from cookiesArea space to canvas space
            Vector3 worldPos = cookiesArea.TransformPoint(new Vector3(cx, cy, 0));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                RectTransformUtility.WorldToScreenPoint(null, worldPos),
                null, out Vector2 canvasPos);
            rt.anchoredPosition = canvasPos;

            var img = go.AddComponent<Image>();
            if (cookieId < cookieSprites.Length)
                img.sprite = cookieSprites[cookieId];
            img.preserveAspect = true;
            img.raycastTarget = true;

            go.AddComponent<CanvasGroup>();

            var draggable = go.AddComponent<BakeryDraggable>();
            draggable.Init(cookieId, canvas, this);
            draggable.StartWiggle();
            cookies.Add(draggable);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  MATCHING LOGIC
    // ═══════════════════════════════════════════════════════════

    public void OnCookiePickedUp() => DismissTutorial();

    public void CheckProximity(BakeryDraggable cookie)
    {
        foreach (var slot in slots)
        {
            if (slot.isMatched || slot.cookieId != cookie.cookieId) continue;
            float dist = ScreenDistance(cookie.RT, slot.RT);
            float threshold = slot.RT.sizeDelta.x * canvas.scaleFactor * 1.5f;
            if (dist < threshold) slot.ShowProximityHint();
        }
    }

    public bool TryMatch(BakeryDraggable cookie)
    {
        BakerySlot best = null;
        float bestDist = float.MaxValue;

        foreach (var slot in slots)
        {
            if (slot.isMatched) continue;
            float dist = ScreenDistance(cookie.RT, slot.RT);
            float threshold = slot.RT.sizeDelta.x * canvas.scaleFactor * MATCH_THRESHOLD;
            if (dist < threshold && dist < bestDist) { bestDist = dist; best = slot; }
        }

        if (best == null) return false;

        if (best.cookieId != cookie.cookieId)
        {
            RecordMistake();
            return false;
        }

        // Correct match
        best.isMatched = true;

        // Snap cookie to slot position
        Vector2 slotScreen = RectTransformUtility.WorldToScreenPoint(null, best.RT.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform, slotScreen, null, out Vector2 snapPos);
        cookie.RT.anchoredPosition = snapPos;
        cookie.RT.sizeDelta = best.RT.sizeDelta; // match slot size

        cookie.Lock();
        StartCoroutine(cookie.PlayMatchCelebration());
        PlayCorrectEffect(cookie.RT);
        RecordCorrect();

        matchedCount++;
        if (matchedCount >= cookieCount)
            StartCoroutine(CompletionSequence());

        return true;
    }

    private IEnumerator CompletionSequence()
    {
        yield return new WaitForSeconds(0.3f);

        // Tray bounce
        var bounceTarget = trayArea.parent as RectTransform; // TraySurface
        if (bounceTarget != null)
        {
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                float s = 1f + 0.06f * Mathf.Sin(t / 0.35f * Mathf.PI);
                bounceTarget.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            bounceTarget.localScale = Vector3.one;
        }

        yield return new WaitForSeconds(0.2f);
        CompleteRound();
    }

    public void OnHomePressed() => NavigationManager.GoToWorld();

    // ── Utility ──

    private static float ScreenDistance(RectTransform a, RectTransform b)
    {
        Vector2 sa = RectTransformUtility.WorldToScreenPoint(null, a.position);
        Vector2 sb = RectTransformUtility.WorldToScreenPoint(null, b.position);
        return Vector2.Distance(sa, sb);
    }

    private static Vector2 WorldToLocal(RectTransform source, RectTransform parent)
    {
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, source.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out Vector2 local);
        return local;
    }
}
