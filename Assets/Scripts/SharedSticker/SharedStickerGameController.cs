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

    private int internalRound; // tracks round count
    private int sharedStickerIndex; // index into stickerSprites of the shared one
    private List<GameObject> spawnedStickers = new List<GameObject>();
    private bool acceptingInput = true;

    // Track which sticker GameObjects are the shared ones (one per card)
    private readonly List<Image> sharedStickerImages = new List<Image>();

    // ── BaseMiniGame Overrides ──

    protected override string GetFallbackGameId() => "sharedsticker";

    // 2-Player score UI
    private TMPro.TextMeshProUGUI _score1TMP;
    private TMPro.TextMeshProUGUI _score2TMP;

    protected override void OnGameInit()
    {
        isEndless = true;
        playConfettiOnRoundWin = true;
        playConfettiOnSessionWin = false;
        internalRound = 0;

        if (TwoPlayerManager.IsActive)
            Setup2PlayerUI();
    }

    protected override void OnRoundSetup()
    {
        GenerateRound();
    }

    private void Setup2PlayerUI()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        // Left score (Player 1 — BLUE)
        var s1GO = new GameObject("Score1");
        s1GO.transform.SetParent(canvas.transform, false);
        var s1RT = s1GO.AddComponent<RectTransform>();
        s1RT.anchorMin = new Vector2(0, 0.9f); s1RT.anchorMax = new Vector2(0.15f, 1);
        s1RT.offsetMin = new Vector2(10, 0); s1RT.offsetMax = Vector2.zero;
        _score1TMP = s1GO.AddComponent<TMPro.TextMeshProUGUI>();
        _score1TMP.text = "0";
        _score1TMP.fontSize = 48; _score1TMP.fontStyle = TMPro.FontStyles.Bold;
        _score1TMP.color = TwoPlayerManager.Player1Color;
        _score1TMP.alignment = TMPro.TextAlignmentOptions.Center;

        // Player 1 name
        var n1GO = new GameObject("Name1");
        n1GO.transform.SetParent(canvas.transform, false);
        var n1RT = n1GO.AddComponent<RectTransform>();
        n1RT.anchorMin = new Vector2(0, 0.83f); n1RT.anchorMax = new Vector2(0.15f, 0.9f);
        n1RT.offsetMin = new Vector2(10, 0); n1RT.offsetMax = Vector2.zero;
        var n1TMP = n1GO.AddComponent<TMPro.TextMeshProUGUI>();
        HebrewText.SetText(n1TMP, TwoPlayerManager.GetName(1));
        n1TMP.fontSize = 20; n1TMP.color = TwoPlayerManager.Player1Color;
        n1TMP.alignment = TMPro.TextAlignmentOptions.Center;

        // Right score (Player 2 — RED)
        var s2GO = new GameObject("Score2");
        s2GO.transform.SetParent(canvas.transform, false);
        var s2RT = s2GO.AddComponent<RectTransform>();
        s2RT.anchorMin = new Vector2(0.85f, 0.9f); s2RT.anchorMax = new Vector2(1, 1);
        s2RT.offsetMin = Vector2.zero; s2RT.offsetMax = new Vector2(-10, 0);
        _score2TMP = s2GO.AddComponent<TMPro.TextMeshProUGUI>();
        _score2TMP.text = "0";
        _score2TMP.fontSize = 48; _score2TMP.fontStyle = TMPro.FontStyles.Bold;
        _score2TMP.color = TwoPlayerManager.Player2Color;
        _score2TMP.alignment = TMPro.TextAlignmentOptions.Center;

        // Player 2 name
        var n2GO = new GameObject("Name2");
        n2GO.transform.SetParent(canvas.transform, false);
        var n2RT = n2GO.AddComponent<RectTransform>();
        n2RT.anchorMin = new Vector2(0.85f, 0.83f); n2RT.anchorMax = new Vector2(1, 0.9f);
        n2RT.offsetMin = Vector2.zero; n2RT.offsetMax = new Vector2(-10, 0);
        var n2TMP = n2GO.AddComponent<TMPro.TextMeshProUGUI>();
        HebrewText.SetText(n2TMP, TwoPlayerManager.GetName(2));
        n2TMP.fontSize = 20; n2TMP.color = TwoPlayerManager.Player2Color;
        n2TMP.alignment = TMPro.TextAlignmentOptions.Center;

        // Split screen background: blue left, red right
        // Place ABOVE the wood background but BELOW the cards
        var splitCanvas = GetComponentInParent<Canvas>();
        if (splitCanvas != null)
        {
            // Find the play area parent (where the card circles live)
            Transform playParent = leftCardArea != null ? leftCardArea.parent : splitCanvas.transform;

            // Left half — blue
            var leftBg = new GameObject("LeftBg");
            leftBg.transform.SetParent(playParent, false);
            leftBg.transform.SetAsFirstSibling(); // behind cards but in play area
            var lbRT = leftBg.AddComponent<RectTransform>();
            lbRT.anchorMin = Vector2.zero; lbRT.anchorMax = new Vector2(0.5f, 1);
            lbRT.offsetMin = Vector2.zero; lbRT.offsetMax = Vector2.zero;
            leftBg.AddComponent<Image>().color = TwoPlayerManager.Player1Color;
            leftBg.GetComponent<Image>().raycastTarget = false;

            // Right half — red
            var rightBg = new GameObject("RightBg");
            rightBg.transform.SetParent(playParent, false);
            rightBg.transform.SetSiblingIndex(1);
            var rbRT = rightBg.AddComponent<RectTransform>();
            rbRT.anchorMin = new Vector2(0.5f, 0); rbRT.anchorMax = Vector2.one;
            rbRT.offsetMin = Vector2.zero; rbRT.offsetMax = Vector2.zero;
            rightBg.AddComponent<Image>().color = TwoPlayerManager.Player2Color;
            rightBg.GetComponent<Image>().raycastTarget = false;

            // Center divider
            var divider = new GameObject("Divider");
            divider.transform.SetParent(playParent, false);
            divider.transform.SetSiblingIndex(2);
            var dRT = divider.AddComponent<RectTransform>();
            dRT.anchorMin = new Vector2(0.5f, 0); dRT.anchorMax = new Vector2(0.5f, 1);
            dRT.sizeDelta = new Vector2(6, 0);
            divider.AddComponent<Image>().color = new Color(1, 1, 1, 0.5f);
            divider.GetComponent<Image>().raycastTarget = false;
        }
    }

    private void UpdateScoreDisplay()
    {
        if (_score1TMP != null) _score1TMP.text = TwoPlayerManager.Score1.ToString();
        if (_score2TMP != null) _score2TMP.text = TwoPlayerManager.Score2.ToString();
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

    // Difficulty 1-10 → 3-7 stickers per card (controlled via parent dashboard)
    private int StickersPerCard
    {
        get
        {
            if (Difficulty <= 2) return 3;
            if (Difficulty <= 4) return 4;
            if (Difficulty <= 6) return 5;
            if (Difficulty <= 8) return 6;
            return 7;
        }
    }

    private void PositionTutorialHand()
    {
        if (TutorialHand == null) return;

        // Point at the shared sticker — show the child exactly what to tap
        if (sharedStickerImages.Count > 0 && sharedStickerImages[0] != null)
        {
            var stickerRT = sharedStickerImages[0].GetComponent<RectTransform>();
            Vector2 localPos = TutorialHand.GetLocalCenter(stickerRT);
            TutorialHand.SetPosition(localPos);
        }
    }

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

        // Position tutorial hand on one of the stickers
        PositionTutorialHand();
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
        DismissTutorial();

        if (stickerIndex == sharedStickerIndex)
        {
            // ── 2-Player: determine which player tapped ──
            int scoringPlayer = 0;
            if (TwoPlayerManager.IsActive)
            {
                var rt = tappedGO.GetComponent<RectTransform>();
                if (rt != null)
                {
                    // For UI Canvas, position.x is already in screen pixels
                    float screenX = rt.position.x / Screen.width;
                    scoringPlayer = TwoPlayerManager.GetPlayerForScreenPosition(screenX);
                }
                if (scoringPlayer == 1) TwoPlayerManager.Score1++;
                else TwoPlayerManager.Score2++;
            }

            Stats?.RecordCorrect();
            PlayCorrectEffect(tappedGO.GetComponent<RectTransform>());
            StartCoroutine(CorrectSequence(tappedGO, scoringPlayer));
        }
        else
        {
            Stats?.RecordMistake();
            PlayWrongEffect(tappedGO.GetComponent<RectTransform>());
            StartCoroutine(WrongSequence(tappedGO));
        }
    }

    private IEnumerator CorrectSequence(GameObject tappedGO, int scoringPlayer = 0)
    {
        acceptingInput = false;

        // Highlight both shared stickers with glow + bounce
        foreach (var img in sharedStickerImages)
        {
            if (img == null) continue;
            StartCoroutine(BounceAndGlow(img));
        }

        // 2-Player: flash winning side
        if (TwoPlayerManager.IsActive && scoringPlayer > 0)
        {
            var flashSide = scoringPlayer == 1 ? leftCardBg : rightCardBg;
            if (flashSide != null)
            {
                Color origColor = flashSide.color;
                Color flashColor = TwoPlayerManager.GetColor(scoringPlayer);
                flashSide.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0.4f);
                yield return new WaitForSeconds(0.3f);
                flashSide.color = origColor;
            }
            UpdateScoreDisplay();
        }

        yield return new WaitForSeconds(0.5f);

        internalRound++;
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
