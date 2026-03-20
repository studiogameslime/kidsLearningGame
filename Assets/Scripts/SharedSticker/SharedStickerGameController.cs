using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Spot It / Dobble" style mini-game. Two circular cards are displayed, each with
/// multiple stickers. Exactly one sticker appears on both cards. The player must
/// find and tap the shared sticker. Difficulty increases as rounds progress.
/// </summary>
public class SharedStickerGameController : BaseMiniGame
{
    [Header("Card Containers")]
    public RectTransform leftCardArea;
    public RectTransform rightCardArea;

    [Header("Card Visuals")]
    public Image leftCardBg;
    public Image rightCardBg;

    [Header("Stickers")]
    public Sprite[] stickerSprites; // assigned by editor setup from Sticker.png sprite sheet

    [Header("UI")]
    public Sprite circleSprite;

    // Difficulty: stickers per card at each stage
    private static readonly int[] DifficultyStickers = { 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8 };

    private int internalRound; // tracks difficulty progression across endless rounds
    private int sharedStickerIndex; // index into stickerSprites of the shared one
    private List<GameObject> spawnedStickers = new List<GameObject>();
    private bool acceptingInput = true;

    // Track which sticker GameObjects are the shared ones (one per card)
    private readonly List<Image> sharedStickerImages = new List<Image>();

    // ── BaseMiniGame Overrides ──

    protected override string GetFallbackGameId() => "sharedsticker";

    protected override void OnGameInit()
    {
        isEndless = true;
        playConfettiOnRoundWin = false;   // we manually play confetti every 3 rounds
        playConfettiOnSessionWin = false;  // endless game, no session win
        internalRound = 0;
    }

    protected override void OnRoundSetup()
    {
        GenerateRound();
    }

    protected override void OnRoundCleanup()
    {
        ClearStickers();
    }

    protected override IEnumerator OnAfterComplete()
    {
        // Play confetti every 3 correct answers
        if (CurrentRound > 0 && (CurrentRound) % 3 == 0)
        {
            if (ConfettiController.Instance != null)
                ConfettiController.Instance.Play();
        }
        yield break;
    }

    // ── GENERATION ──────────────────────────────────────────────

    private int StickersPerCard =>
        DifficultyStickers[Mathf.Min(internalRound, DifficultyStickers.Length - 1)];

    private void GenerateRound()
    {
        ClearStickers();
        acceptingInput = true;
        sharedStickerImages.Clear();

        int count = StickersPerCard;
        if (stickerSprites == null || stickerSprites.Length < count * 2 - 1)
        {
            Debug.LogError("SharedSticker: Not enough sticker sprites for current difficulty.");
            return;
        }

        // Shuffle indices
        var pool = new List<int>();
        for (int i = 0; i < stickerSprites.Length; i++) pool.Add(i);
        Shuffle(pool);

        // First index is the shared sticker
        sharedStickerIndex = pool[0];

        // Card A: shared + (count-1) unique
        var cardA = new List<int> { pool[0] };
        for (int i = 1; i < count; i++)
            cardA.Add(pool[i]);

        // Card B: shared + (count-1) unique from remaining pool
        var cardB = new List<int> { pool[0] };
        for (int i = count; i < count * 2 - 1; i++)
            cardB.Add(pool[i]);

        // Shuffle each card's sticker order so shared sticker isn't always first
        Shuffle(cardA);
        Shuffle(cardB);

        // Spawn
        SpawnCardStickers(leftCardArea, leftCardBg, cardA);
        SpawnCardStickers(rightCardArea, rightCardBg, cardB);
    }

    private void SpawnCardStickers(RectTransform cardArea, Image cardBg, List<int> indices)
    {
        float cardRadius = Mathf.Min(cardArea.rect.width, cardArea.rect.height) * 0.5f;
        if (cardRadius <= 0) cardRadius = 200f; // fallback before layout

        var positions = LayoutStickersInCircle(indices.Count, cardRadius);

        for (int i = 0; i < indices.Count; i++)
        {
            int sIdx = indices[i];
            Sprite spr = stickerSprites[sIdx];

            var go = new GameObject($"Sticker_{spr.name}");
            go.transform.SetParent(cardArea, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            // Size based on card radius and sticker count
            float stickerSize = cardRadius * 0.45f / Mathf.Sqrt(indices.Count * 0.5f);
            stickerSize = Mathf.Clamp(stickerSize, 50f, 140f);
            rt.sizeDelta = new Vector2(stickerSize, stickerSize);
            rt.anchoredPosition = positions[i];

            // Small random rotation for visual variety
            float rot = Random.Range(-15f, 15f);
            rt.localEulerAngles = new Vector3(0, 0, rot);

            // Small scale variation
            float scale = Random.Range(0.9f, 1.1f);
            rt.localScale = Vector3.one * scale;

            var img = go.AddComponent<Image>();
            img.sprite = spr;
            img.preserveAspect = true;
            img.raycastTarget = true;

            // Track shared sticker images
            bool isShared = sIdx == sharedStickerIndex;
            if (isShared)
                sharedStickerImages.Add(img);

            // Button for tap
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            int capturedIdx = sIdx;
            btn.onClick.AddListener(() => OnStickerTapped(capturedIdx, go));

            spawnedStickers.Add(go);
        }
    }

    /// <summary>
    /// Distribute stickers inside a circle using a spiral/ring layout that avoids overlap.
    /// </summary>
    private List<Vector2> LayoutStickersInCircle(int count, float cardRadius)
    {
        var positions = new List<Vector2>();
        float safeRadius = cardRadius * 0.65f; // keep stickers well inside the circle

        if (count == 1)
        {
            positions.Add(Vector2.zero);
            return positions;
        }

        if (count <= 4)
        {
            // Place on a single ring
            float ringRadius = safeRadius * 0.5f;
            float angleStep = 360f / count;
            float startAngle = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                float angle = (startAngle + i * angleStep) * Mathf.Deg2Rad;
                positions.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ringRadius);
            }
            return positions;
        }

        // For 5+: center sticker + outer ring
        positions.Add(Vector2.zero); // center

        int outerCount = count - 1;
        float outerRadius = safeRadius * 0.65f;
        float outerAngleStep = 360f / outerCount;
        float outerStartAngle = Random.Range(0f, 360f);
        for (int i = 0; i < outerCount; i++)
        {
            float angle = (outerStartAngle + i * outerAngleStep) * Mathf.Deg2Rad;
            positions.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * outerRadius);
        }

        // Shuffle positions so the center one isn't always index 0
        Shuffle(positions);
        return positions;
    }

    private void ClearStickers()
    {
        foreach (var go in spawnedStickers)
            if (go != null) Destroy(go);
        spawnedStickers.Clear();
        sharedStickerImages.Clear();
    }

    // ── INPUT ───────────────────────────────────────────────────

    private void OnStickerTapped(int stickerIndex, GameObject tappedGO)
    {
        if (IsInputLocked || !acceptingInput) return;

        if (stickerIndex == sharedStickerIndex)
        {
            Stats?.RecordCorrect();
            StartCoroutine(CorrectSequence(tappedGO));
        }
        else
        {
            Stats?.RecordMistake();
            StartCoroutine(WrongSequence(tappedGO));
        }
    }

    private IEnumerator CorrectSequence(GameObject tappedGO)
    {
        acceptingInput = false;

        // Highlight both shared stickers with glow + bounce
        foreach (var img in sharedStickerImages)
        {
            if (img == null) continue;
            StartCoroutine(BounceAndGlow(img));
        }

        yield return new WaitForSeconds(0.8f);

        internalRound++;

        // Complete the round — triggers feedback via BaseMiniGame
        // Confetti is handled in OnAfterComplete every 3 rounds
        CompleteRound();
    }

    private IEnumerator WrongSequence(GameObject tappedGO)
    {
        acceptingInput = false;

        var rt = tappedGO.GetComponent<RectTransform>();
        if (rt != null)
            yield return Shake(rt);

        acceptingInput = true;
    }

    // ── ANIMATIONS ──────────────────────────────────────────────

    private IEnumerator BounceAndGlow(Image img)
    {
        if (img == null) yield break;
        var rt = img.rectTransform;

        // Add glow outline
        var glowGO = new GameObject("Glow");
        glowGO.transform.SetParent(rt, false);
        var glowRT = glowGO.AddComponent<RectTransform>();
        glowRT.anchorMin = Vector2.zero;
        glowRT.anchorMax = Vector2.one;
        glowRT.offsetMin = new Vector2(-12, -12);
        glowRT.offsetMax = new Vector2(12, 12);
        glowGO.transform.SetAsFirstSibling();
        var glowImg = glowGO.AddComponent<Image>();
        if (circleSprite != null) glowImg.sprite = circleSprite;
        glowImg.color = new Color(1f, 0.9f, 0.2f, 0.7f);
        glowImg.raycastTarget = false;

        // Bounce: scale up then back
        float t = 0;
        Vector3 orig = rt.localScale;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            float s = 1f + 0.3f * Mathf.Sin(t / 0.2f * Mathf.PI);
            rt.localScale = orig * s;
            yield return null;
        }
        rt.localScale = orig;

        // Hold glow briefly
        yield return new WaitForSeconds(0.4f);

        // Fade glow
        t = 0;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            if (glowImg != null)
                glowImg.color = new Color(1f, 0.9f, 0.2f, 0.7f * (1f - t / 0.3f));
            yield return null;
        }

        if (glowGO != null) Destroy(glowGO);
    }

    private IEnumerator Shake(RectTransform rt)
    {
        Vector2 orig = rt.anchoredPosition;
        float duration = 0.3f;
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            float offset = Mathf.Sin(t * 40f) * 8f * (1f - t / duration);
            rt.anchoredPosition = orig + new Vector2(offset, 0);
            yield return null;
        }
        rt.anchoredPosition = orig;
    }

    // ── NAVIGATION ──────────────────────────────────────────────

    public void OnHomePressed() => ExitGame();

    // ── UTILITY ─────────────────────────────────────────────────

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
