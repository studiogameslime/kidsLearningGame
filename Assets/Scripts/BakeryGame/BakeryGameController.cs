using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bakery Game — drag cookies onto matching tray slots.
/// Children drag cookie pieces into shape-matching slots on a baking tray.
/// </summary>
public class BakeryGameController : BaseMiniGame
{
    [Header("References")]
    public RectTransform trayArea;
    public RectTransform cookiesArea;
    public Image trayImage;
    public Sprite[] cookieSprites;
    public Sprite roundedRect;

    private Canvas canvas;
    private List<BakeryDraggable> cookies = new List<BakeryDraggable>();
    private List<BakerySlot> slots = new List<BakerySlot>();
    private int matchedCount;
    private int cookieCount;

    private static readonly Color SlotTint = new Color(1f, 1f, 1f, 0.3f);
    private const float MATCH_THRESHOLD = 0.75f; // fraction of slot size for match distance

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
        ScatterCookies(indices);

        // Tutorial: hint from first cookie to its matching slot
        if (cookies.Count > 0 && slots.Count > 0)
        {
            BakeryDraggable firstCookie = cookies[0];
            BakerySlot matchingSlot = null;
            foreach (var s in slots)
                if (s.cookieId == firstCookie.cookieId) { matchingSlot = s; break; }

            if (matchingSlot != null && TutorialHand != null)
            {
                var handParent = TutorialHand.transform.parent as RectTransform;
                Vector2 from = WorldToLocal(firstCookie.RT, handParent);
                Vector2 to = WorldToLocal(matchingSlot.RT, handParent);
                TutorialHand.SetMovePath(from, to, 1.5f);
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

    // ── Layout ──

    private void LayoutSlots(int[] indices)
    {
        float trayW = trayArea.rect.width;
        float trayH = trayArea.rect.height;
        if (trayW <= 0) trayW = 800f;
        if (trayH <= 0) trayH = 500f;

        // Grid: 2 rows, N/2 columns
        int cols = cookieCount / 2;
        int rows = 2;
        float slotSize = Mathf.Min(trayW / (cols + 0.5f), trayH / (rows + 0.5f)) * 0.85f;
        float spacingX = trayW / (cols + 1);
        float spacingY = trayH / (rows + 1);

        for (int i = 0; i < cookieCount; i++)
        {
            int col = i % cols;
            int row = i / cols;

            var go = new GameObject($"Slot_{i}");
            go.transform.SetParent(trayArea, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(slotSize, slotSize);

            float x = -trayW * 0.5f + spacingX * (col + 1);
            float y = trayH * 0.5f - spacingY * (row + 1);
            rt.anchoredPosition = new Vector2(x, y);

            var img = go.AddComponent<Image>();
            if (indices[i] < cookieSprites.Length)
                img.sprite = cookieSprites[indices[i]];
            img.preserveAspect = true;
            img.color = SlotTint;
            img.raycastTarget = false;

            var slot = go.AddComponent<BakerySlot>();
            slot.cookieId = indices[i];
            slot.StartIdlePulse();
            slots.Add(slot);
        }
    }

    private void ScatterCookies(int[] indices)
    {
        // Shuffle order so cookies don't match slot positions
        var order = new List<int>();
        for (int i = 0; i < cookieCount; i++) order.Add(i);
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = order[i]; order[i] = order[j]; order[j] = tmp;
        }

        float areaW = cookiesArea.rect.width;
        if (areaW <= 0) areaW = 1600f;

        float cookieSize = Mathf.Min(areaW / (cookieCount + 0.5f), 200f);
        float spacing = areaW / (cookieCount + 1);

        for (int i = 0; i < cookieCount; i++)
        {
            int idx = order[i];
            int cookieId = indices[idx];

            var go = new GameObject($"Cookie_{cookieId}");
            // Parent to canvas root so cookie can drag freely across the whole screen
            go.transform.SetParent(canvas.transform, false);
            go.transform.SetAsLastSibling();

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(cookieSize, cookieSize);

            // Position in cookiesArea local space, converted to canvas space
            float localX = -areaW * 0.5f + spacing * (i + 1) + Random.Range(-15f, 15f);
            float localY = Random.Range(-10f, 10f);
            Vector3 worldPos = cookiesArea.TransformPoint(new Vector3(localX, localY, 0));
            Vector2 canvasLocal;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                RectTransformUtility.WorldToScreenPoint(null, worldPos),
                null, out canvasLocal);
            rt.anchoredPosition = canvasLocal;

            var img = go.AddComponent<Image>();
            if (cookieId < cookieSprites.Length)
                img.sprite = cookieSprites[cookieId];
            img.preserveAspect = true;
            img.raycastTarget = true;

            var cg = go.AddComponent<CanvasGroup>();

            var draggable = go.AddComponent<BakeryDraggable>();
            draggable.Init(cookieId, canvas, this);
            draggable.StartWiggle();
            cookies.Add(draggable);
        }
    }

    // ── Matching ──

    public void OnCookiePickedUp()
    {
        DismissTutorial();
    }

    public void CheckProximity(BakeryDraggable cookie)
    {
        foreach (var slot in slots)
        {
            if (slot.isMatched) continue;
            if (slot.cookieId != cookie.cookieId) continue;

            float dist = Vector2.Distance(
                RectTransformUtility.WorldToScreenPoint(null, cookie.RT.position),
                RectTransformUtility.WorldToScreenPoint(null, slot.RT.position));
            float threshold = slot.RT.sizeDelta.x * canvas.scaleFactor * 1.5f;

            if (dist < threshold)
                slot.ShowProximityHint();
        }
    }

    public bool TryMatch(BakeryDraggable cookie)
    {
        BakerySlot bestSlot = null;
        float bestDist = float.MaxValue;

        foreach (var slot in slots)
        {
            if (slot.isMatched) continue;

            float dist = Vector2.Distance(
                RectTransformUtility.WorldToScreenPoint(null, cookie.RT.position),
                RectTransformUtility.WorldToScreenPoint(null, slot.RT.position));
            float threshold = slot.RT.sizeDelta.x * canvas.scaleFactor * MATCH_THRESHOLD;

            if (dist < threshold && dist < bestDist)
            {
                bestDist = dist;
                bestSlot = slot;
            }
        }

        if (bestSlot == null) return false;

        if (bestSlot.cookieId != cookie.cookieId)
        {
            RecordMistake();
            return false;
        }

        // Correct match!
        bestSlot.isMatched = true;
        bestSlot.StopIdlePulse();

        // Snap cookie to slot world position
        Vector2 slotScreen = RectTransformUtility.WorldToScreenPoint(null, bestSlot.RT.position);
        Vector2 canvasLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform, slotScreen, null, out canvasLocal);
        cookie.RT.anchoredPosition = canvasLocal;

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
        if (trayArea != null)
        {
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                float s = 1f + 0.08f * Mathf.Sin(t / 0.3f * Mathf.PI);
                trayArea.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            trayArea.localScale = Vector3.one;
        }

        yield return new WaitForSeconds(0.2f);
        CompleteRound();
    }

    // ── Utility ──

    public void OnHomePressed() => NavigationManager.GoToWorldScene();

    private static Vector2 WorldToLocal(RectTransform source, RectTransform parent)
    {
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, source.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out Vector2 local);
        return local;
    }
}
