using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Half Puzzle game: fruit images are cut in half (left/right).
/// Left halves in a row at the top (shuffled), right halves at the bottom (shuffled).
/// Both halves are draggable — the child drags one half to its match.
/// When matching halves are close enough, they snap together side by side.
///
/// Kitchen theme background built procedurally.
///
/// Difficulty scales the number of pairs:
///   1-3  → 2 pairs
///   4-6  → 3-4 pairs
///   7-9  → 5-6 pairs
///   10   → 7 pairs
/// </summary>
public class HalfPuzzleController : BaseMiniGame
{
    [Header("Board")]
    public RectTransform boardArea;

    [Header("Settings")]
    public float snapDistance = 160f;

    // Runtime state
    private Sprite[] _fruitSprites;
    private Sprite _basketSprite;
    private readonly List<DraggableHalf> _topPieces = new List<DraggableHalf>();    // left halves
    private readonly List<DraggableHalf> _bottomPieces = new List<DraggableHalf>(); // right halves
    private readonly List<int> _roundFruitIndices = new List<int>();
    private readonly List<Texture2D> _createdTextures = new List<Texture2D>();
    private readonly List<Sprite> _createdSprites = new List<Sprite>();
    private readonly List<GameObject> _completedFruits = new List<GameObject>();
    private int _matchedCount;
    private int _totalPairs;
    private Canvas _canvas;
    private RectTransform _basketRT;
    private int _basketItemCount;

    // ── BaseMiniGame overrides ──

    protected override string GetFallbackGameId() => "halfpuzzle";

    protected override void OnGameInit()
    {
        totalRounds = 1;
        isEndless = true;
        contentCategory = "fruits";
        playConfettiOnRoundWin = true;
        playConfettiOnSessionWin = true;
        _canvas = GetComponentInParent<Canvas>();

        _fruitSprites = Resources.LoadAll<Sprite>("Tractor/Fruits");
        if (_fruitSprites == null || _fruitSprites.Length == 0)
            Debug.LogError("[HalfPuzzle] No fruit sprites found in Resources/Tractor/Fruits!");

        _basketSprite = Resources.Load<Sprite>("Basket");
    }

    protected override void OnRoundSetup()
    {
        _matchedCount = 0;
        _totalPairs = GetPairCount();

        if (_fruitSprites == null || _fruitSprites.Length == 0) return;

        _roundFruitIndices.Clear();
        var pool = new List<int>();
        for (int i = 0; i < _fruitSprites.Length; i++) pool.Add(i);
        ShuffleList(pool);
        for (int i = 0; i < _totalPairs && i < pool.Count; i++)
            _roundFruitIndices.Add(pool[i]);

        Debug.Log($"[HalfPuzzle] Round {CurrentRound + 1}: {_totalPairs} pairs, difficulty={Difficulty}");

        BuildBoard();
    }

    protected override void OnRoundCleanup()
    {
        foreach (var p in _topPieces) if (p != null) Destroy(p.gameObject);
        foreach (var p in _bottomPieces) if (p != null) Destroy(p.gameObject);
        foreach (var g in _completedFruits) if (g != null) Destroy(g);
        if (_basketRT != null) Destroy(_basketRT.gameObject);
        _basketRT = null;
        _topPieces.Clear();
        _bottomPieces.Clear();
        _completedFruits.Clear();
        _basketItemCount = 0;
        CleanupTextures();
    }

    private void OnDestroy()
    {
        CleanupTextures();
    }

    private void CleanupTextures()
    {
        foreach (var s in _createdSprites) if (s != null) Destroy(s);
        foreach (var t in _createdTextures) if (t != null) Destroy(t);
        _createdSprites.Clear();
        _createdTextures.Clear();
    }

    protected override string GetContentId()
    {
        return _roundFruitIndices.Count > 0 ? $"fruit_{_roundFruitIndices[0]}" : "fruits";
    }

    // ── Difficulty → pair count ──

    private int GetPairCount()
    {
        if (Difficulty <= 3) return 2;
        if (Difficulty <= 5) return 3;
        if (Difficulty <= 6) return 4;
        if (Difficulty <= 8) return 5;
        if (Difficulty <= 9) return 6;
        return 7;
    }

    // ── Board building ──

    private void BuildBoard()
    {
        if (boardArea == null) return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(boardArea);

        float boardW = boardArea.rect.width;
        float boardH = boardArea.rect.height;

        // ── Basket on the left side ──
        _basketItemCount = 0;
        float basketW = 280f;
        float basketH = 340f;
        BuildBasket(boardArea, basketW, basketH, boardH);

        // Pieces area starts after the basket
        float piecesStartX = basketW + 30f;
        float piecesW = boardW - piecesStartX;

        // Layout: top row (left halves) at ~72%, bottom row (right halves) at ~28%
        float topRowY = boardH * 0.72f;
        float bottomRowY = boardH * 0.28f;

        // Card size — each half is half-width, full-height of the fruit
        float padding = 40f;
        float spacing = 28f;
        float availW = piecesW - padding * 2f - (_totalPairs - 1) * spacing;
        float cardW = Mathf.Min(availW / _totalPairs, 200f);
        float cardH = cardW * 1.6f; // taller than wide since it's half a fruit (left/right cut)

        // Horizontal centering within the pieces area
        float totalW = _totalPairs * cardW + (_totalPairs - 1) * spacing;
        float startX = piecesStartX + (piecesW - totalW) / 2f + cardW / 2f;

        // Shuffled orders
        var topOrder = new List<int>();
        var bottomOrder = new List<int>();
        for (int i = 0; i < _totalPairs; i++) { topOrder.Add(i); bottomOrder.Add(i); }
        ShuffleList(topOrder);
        ShuffleList(bottomOrder);

        var roundedRect = Resources.Load<Sprite>("UI/RoundedRect");

        for (int i = 0; i < _totalPairs; i++)
        {
            float xPos = startX + i * (cardW + spacing);

            // ── Top row: LEFT halves (draggable) ──
            int topIdx = topOrder[i];
            int topFruitIdx = _roundFruitIndices[topIdx];
            Sprite topFullSprite = _fruitSprites[topFruitIdx];
            if (topFullSprite == null) continue;

            Texture2D topTex = ExtractSpriteTexture(topFullSprite);
            Sprite leftSprite = CreateLeftHalf(topTex);

            var topGO = CreateHalfCard(boardArea, $"Left_{topFullSprite.name}",
                leftSprite, roundedRect, cardW, cardH, new Vector2(xPos, topRowY),
                new Vector2(1f, 0.5f)); // pivot at right edge

            var topDrag = topGO.AddComponent<DraggableHalf>();
            topDrag.Init(topIdx, topFullSprite.name, _canvas, this);
            topDrag.minClampX = piecesStartX;
            _topPieces.Add(topDrag);

            // ── Bottom row: RIGHT halves (draggable) ──
            int bottomIdx = bottomOrder[i];
            int bottomFruitIdx = _roundFruitIndices[bottomIdx];
            Sprite bottomFullSprite = _fruitSprites[bottomFruitIdx];
            if (bottomFullSprite == null) continue;

            Texture2D bottomTex = ExtractSpriteTexture(bottomFullSprite);
            Sprite rightSprite = CreateRightHalf(bottomTex);

            var bottomGO = CreateHalfCard(boardArea, $"Right_{bottomFullSprite.name}",
                rightSprite, roundedRect, cardW, cardH, new Vector2(xPos, bottomRowY),
                new Vector2(0f, 0.5f)); // pivot at left edge

            var bottomDrag = bottomGO.AddComponent<DraggableHalf>();
            bottomDrag.Init(bottomIdx, bottomFullSprite.name, _canvas, this);
            bottomDrag.minClampX = piecesStartX;
            _bottomPieces.Add(bottomDrag);
        }

        StartCoroutine(CaptureStartPositions());
    }

    /// <summary>Creates a styled half-card with cabinet-like background.</summary>
    private GameObject CreateHalfCard(Transform parent, string name, Sprite halfSprite,
        Sprite roundedRect, float w, float h, Vector2 position, Vector2 pivot)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = pivot;
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = position;

        // Cabinet-style background
        var bgImg = go.AddComponent<Image>();
        if (roundedRect != null) { bgImg.sprite = roundedRect; bgImg.type = Image.Type.Sliced; }
        bgImg.color = new Color(0.92f, 0.95f, 0.98f, 0.9f); // soft light blue-white
        bgImg.raycastTarget = true;

        // Shadow
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.2f, 0.15f, 0.1f, 0.25f);
        shadow.effectDistance = new Vector2(2, -3);

        // Fruit image inside
        var imgGO = new GameObject("FruitImage");
        imgGO.transform.SetParent(go.transform, false);
        var imgRT = imgGO.AddComponent<RectTransform>();
        imgRT.anchorMin = new Vector2(0.05f, 0.05f);
        imgRT.anchorMax = new Vector2(0.95f, 0.95f);
        imgRT.offsetMin = Vector2.zero;
        imgRT.offsetMax = Vector2.zero;
        var fruitImg = imgGO.AddComponent<Image>();
        fruitImg.sprite = halfSprite;
        fruitImg.preserveAspect = true;
        fruitImg.raycastTarget = false;

        return go;
    }

    private IEnumerator CaptureStartPositions()
    {
        yield return null;
        foreach (var p in _topPieces) if (p != null) p.CaptureStartPosition();
        foreach (var p in _bottomPieces) if (p != null) p.CaptureStartPosition();

        if (TutorialHand != null && _bottomPieces.Count > 0)
        {
            var rt = _bottomPieces[0].GetComponent<RectTransform>();
            TutorialHand.SetPosition(TutorialHand.GetLocalCenter(rt));
        }
    }

    // ── Match logic ──

    public bool TryMatch(DraggableHalf piece)
    {
        if (IsInputLocked || piece.IsPlaced) return false;

        DismissTutorial();

        int pairId = piece.pairId;
        RectTransform pieceRT = piece.GetComponent<RectTransform>();

        // Find the closest partner piece that overlaps with the dragged piece
        bool isLeftHalf = _topPieces.Contains(piece);
        var partnerList = isLeftHalf ? _bottomPieces : _topPieces;

        Rect pieceWorld = GetWorldRect(pieceRT);

        DraggableHalf closest = null;
        float closestDist = float.MaxValue;
        foreach (var p in partnerList)
        {
            if (p == null || p.IsPlaced) continue;
            RectTransform pRT = p.GetComponent<RectTransform>();
            Rect pWorld = GetWorldRect(pRT);
            if (!pieceWorld.Overlaps(pWorld)) continue;
            float d = Vector2.Distance(pieceRT.anchoredPosition, pRT.anchoredPosition);
            if (d < closestDist) { closestDist = d; closest = p; }
        }

        if (closest == null) return false;

        // Dropped on a wrong partner — shake then bounce back
        if (closest.pairId != pairId)
        {
            Stats?.RecordMistake("wrong_half");
            StartCoroutine(ShakeThenBounceBack(piece, pieceRT));
            return true; // return true to prevent OnEndDrag's BounceBack from conflicting
        }

        // Find the correct partner reference for midpoint calculation
        DraggableHalf partner = closest;
        RectTransform partnerRT = partner.GetComponent<RectTransform>();

        // Match! Merge halves into full fruit and fly to basket
        Stats?.RecordCorrect("match", piece.animalId);

        Vector2 midpoint = (pieceRT.anchoredPosition + partnerRT.anchoredPosition) / 2f;

        // Mark as placed, then hide both half-cards
        piece.LockWithoutAnimation();
        partner.LockWithoutAnimation();
        piece.gameObject.SetActive(false);
        partner.gameObject.SetActive(false);

        // Spawn the full fruit at the midpoint, then fly to basket
        int fruitIdx = _roundFruitIndices[pairId];
        Sprite fullSprite = _fruitSprites[fruitIdx];
        if (fullSprite != null)
        {
            float cardW = pieceRT.sizeDelta.x;
            float cardH = pieceRT.sizeDelta.y;

            var fullGO = new GameObject($"Full_{fullSprite.name}");
            fullGO.transform.SetParent(boardArea, false);
            var fullRT = fullGO.AddComponent<RectTransform>();
            fullRT.anchorMin = Vector2.zero;
            fullRT.anchorMax = Vector2.zero;
            fullRT.pivot = new Vector2(0.5f, 0.5f);
            fullRT.sizeDelta = new Vector2(cardW * 2f, cardH);
            fullRT.anchoredPosition = midpoint;
            fullRT.localScale = Vector3.one * 0.5f;

            var fullImg = fullGO.AddComponent<Image>();
            fullImg.sprite = fullSprite;
            fullImg.preserveAspect = true;
            fullImg.raycastTarget = false;

            _completedFruits.Add(fullGO);
            StartCoroutine(PopThenFlyToBasket(fullRT, fullSprite));
        }

        PlayCorrectEffect(pieceRT);

        _matchedCount++;
        if (_matchedCount >= _totalPairs)
            StartCoroutine(DelayedComplete());

        return true;
    }

    private IEnumerator ShakeThenBounceBack(DraggableHalf piece, RectTransform pieceRT)
    {
        // Shake first (0.4s), then bounce back to start
        PlayWrongEffect(pieceRT);
        yield return new WaitForSeconds(0.45f);

        if (piece == null || piece.IsPlaced) yield break;

        // Now bounce back
        piece.BounceToStart();
    }

    // ── Basket ──

    private void BuildBasket(RectTransform parent, float w, float h, float boardH)
    {
        var basketGO = new GameObject("Basket");
        basketGO.transform.SetParent(parent, false);
        var rt = basketGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(w / 2f + 15f, boardH * 0.3f);

        var img = basketGO.AddComponent<Image>();
        if (_basketSprite != null)
            img.sprite = _basketSprite;
        img.preserveAspect = true;
        img.color = Color.white;
        img.raycastTarget = false;

        _basketRT = rt;

        // Bounce-in animation
        StartCoroutine(BasketBounceIn(rt));
    }

    private IEnumerator BasketBounceIn(RectTransform rt)
    {
        rt.localScale = Vector3.zero;
        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            float p = t / 0.35f;
            float s = p < 0.65f
                ? Mathf.Lerp(0f, 1.15f, p / 0.65f)
                : Mathf.Lerp(1.15f, 1f, (p - 0.65f) / 0.35f);
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private IEnumerator PopThenFlyToBasket(RectTransform rt, Sprite fruitSprite)
    {
        if (rt == null) yield break;

        // Phase 1: Pop in (0.5 → 1.2 → 1.0)
        float dur = 0.2f, elapsed = 0f;
        while (elapsed < dur && rt != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / dur);
            rt.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.2f, t);
            yield return null;
        }
        elapsed = 0f; dur = 0.15f;
        while (elapsed < dur && rt != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / dur);
            rt.localScale = Vector3.one * Mathf.Lerp(1.2f, 1f, t);
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;

        // Brief pause so the child sees the full fruit
        yield return new WaitForSeconds(0.35f);
        if (rt == null) yield break;

        // Phase 2: Lift up slightly before flying
        Vector2 startPos = rt.anchoredPosition;
        Vector2 liftPos = startPos + new Vector2(0, 40f);
        dur = 0.12f; elapsed = 0f;
        while (elapsed < dur && rt != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / dur);
            rt.anchoredPosition = Vector2.Lerp(startPos, liftPos, t);
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 1.1f, t);
            yield return null;
        }
        if (rt == null) yield break;

        // Phase 3: Fly to basket — arc + playful spin + shrink + fade
        Vector2 flyStart = rt.anchoredPosition;
        Vector2 targetPos = _basketRT != null ? _basketRT.anchoredPosition : flyStart;
        float targetScale = 0.35f;
        dur = 0.55f; elapsed = 0f;

        // Higher arc for more dramatic flight
        float arcHeight = 150f;
        // Full 360 spin
        float totalRotation = 360f;

        var canvasGroup = rt.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = rt.gameObject.AddComponent<CanvasGroup>();

        while (elapsed < dur && rt != null)
        {
            elapsed += Time.deltaTime;
            // Ease-in-out with slight acceleration at end
            float t = elapsed / dur;
            float eased = t < 0.5f
                ? 2f * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

            // Position with parabolic arc
            Vector2 pos = Vector2.Lerp(flyStart, targetPos, eased);
            pos.y += arcHeight * 4f * eased * (1f - eased);
            rt.anchoredPosition = pos;

            // Shrink with slight bounce feel
            float scale = Mathf.Lerp(1.1f, targetScale, eased);
            rt.localScale = Vector3.one * scale;

            // Playful spin
            rt.localRotation = Quaternion.Euler(0, 0, -totalRotation * eased);

            // Fade out in the last 20%
            if (t > 0.8f)
                canvasGroup.alpha = Mathf.Lerp(1f, 0.3f, (t - 0.8f) / 0.2f);

            yield return null;
        }

        if (rt != null)
            rt.gameObject.SetActive(false);

        // Phase 4: Add mini fruit inside basket
        if (_basketRT != null && fruitSprite != null)
            AddFruitToBasket(fruitSprite);
    }

    private void AddFruitToBasket(Sprite fruitSprite)
    {
        var itemGO = new GameObject($"BasketItem_{_basketItemCount}");
        itemGO.transform.SetParent(_basketRT, false);
        var itemRT = itemGO.AddComponent<RectTransform>();

        // Grid layout inside basket: 3 columns, centered in lower portion
        float itemSize = 55f;
        float gap = 6f;
        int cols = 3;
        int col = _basketItemCount % cols;
        int row = _basketItemCount / cols;
        float gridW = cols * itemSize + (cols - 1) * gap;
        float x = -gridW / 2f + col * (itemSize + gap) + itemSize / 2f;
        // Start items from upper area of basket
        float topOffset = _basketRT.sizeDelta.y * 0.2f;
        float y = topOffset - row * (itemSize + gap) - itemSize / 2f;

        itemRT.anchoredPosition = new Vector2(x, y);
        itemRT.sizeDelta = new Vector2(itemSize, itemSize);

        var itemImg = itemGO.AddComponent<Image>();
        itemImg.sprite = fruitSprite;
        itemImg.preserveAspect = true;
        itemImg.raycastTarget = false;

        _basketItemCount++;
        _completedFruits.Add(itemGO);

        // Pop-in animation
        StartCoroutine(BasketItemPopIn(itemRT));
    }

    private IEnumerator BasketItemPopIn(RectTransform itemRT)
    {
        itemRT.localScale = Vector3.zero;
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            float p = t / 0.25f;
            float s = p < 0.6f
                ? Mathf.Lerp(0f, 1.2f, p / 0.6f)
                : Mathf.Lerp(1.2f, 1f, (p - 0.6f) / 0.4f);
            itemRT.localScale = Vector3.one * s;
            yield return null;
        }
        itemRT.localScale = Vector3.one;

        // Bounce the basket
        if (_basketRT != null)
            StartCoroutine(BasketBounce());
    }

    private IEnumerator BasketBounce()
    {
        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            float p = t / 0.2f;
            float s = 1f + Mathf.Sin(p * Mathf.PI) * 0.08f;
            _basketRT.localScale = Vector3.one * s;
            yield return null;
        }
        _basketRT.localScale = Vector3.one;
    }

    private IEnumerator DelayedComplete()
    {
        // Wait for pop + fly-to-basket animation to finish
        yield return new WaitForSeconds(1.3f);
        CompleteRound();
    }

    // ── Sprite splitting (left/right) ──

    private static Texture2D ExtractSpriteTexture(Sprite sprite)
    {
        if (sprite == null) return null;

        var source = sprite.texture;
        Rect spriteRect = sprite.textureRect;
        int w = Mathf.RoundToInt(spriteRect.width);
        int h = Mathf.RoundToInt(spriteRect.height);

        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        var extracted = new Texture2D(w, h, TextureFormat.RGBA32, false);
        extracted.ReadPixels(new Rect(spriteRect.x, spriteRect.y, w, h), 0, 0);
        extracted.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return extracted;
    }

    private Sprite CreateLeftHalf(Texture2D tex)
    {
        if (tex == null) return null;
        _createdTextures.Add(tex);
        int halfW = tex.width / 2;
        var sprite = Sprite.Create(tex,
            new Rect(0, 0, halfW, tex.height),
            new Vector2(1f, 0.5f),
            100f);
        _createdSprites.Add(sprite);
        return sprite;
    }

    private Sprite CreateRightHalf(Texture2D tex)
    {
        if (tex == null) return null;
        if (!_createdTextures.Contains(tex))
            _createdTextures.Add(tex);
        int halfW = tex.width / 2;
        var sprite = Sprite.Create(tex,
            new Rect(halfW, 0, tex.width - halfW, tex.height),
            new Vector2(0f, 0.5f),
            100f);
        _createdSprites.Add(sprite);
        return sprite;
    }

    // ── Utility ──

    private static Rect GetWorldRect(RectTransform rt)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        float xMin = corners[0].x;
        float yMin = corners[0].y;
        float w = corners[2].x - corners[0].x;
        float h = corners[2].y - corners[0].y;
        return new Rect(xMin, yMin, w, h);
    }

    private static void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = list[i]; list[i] = list[j]; list[j] = temp;
        }
    }

    public void OnExitPressed() => ExitGame();
}
