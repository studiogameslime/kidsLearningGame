using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Half Puzzle game: fruit images are cut in half (top/bottom).
/// Top halves in a row at the top, bottom halves shuffled at the bottom.
/// Both halves are draggable — the child can drag from top to bottom or bottom to top.
/// When two matching halves are close enough, they snap together.
///
/// Layout similar to Shadow Match: horizontal rows, top and bottom.
///
/// Difficulty scales the number of pairs:
///   1-3  → 2 pairs
///   4-6  → 3-4 pairs
///   7-9  → 5-6 pairs
///   10   → 7 pairs
///
/// Uses fruit sprites from Resources/Tractor/Fruits.png sprite sheet (Fruits_0..19).
/// </summary>
public class HalfPuzzleController : BaseMiniGame
{
    [Header("Board")]
    public RectTransform boardArea;

    [Header("Settings")]
    public float snapDistance = 100f;

    // Runtime state
    private Sprite[] _fruitSprites;
    private readonly List<DraggableHalf> _topPieces = new List<DraggableHalf>();
    private readonly List<DraggableHalf> _bottomPieces = new List<DraggableHalf>();
    private readonly List<int> _roundFruitIndices = new List<int>();
    private int _matchedCount;
    private int _totalPairs;
    private Canvas _canvas;

    // ── BaseMiniGame overrides ──

    protected override string GetFallbackGameId() => "halfpuzzle";

    protected override void OnGameInit()
    {
        totalRounds = 3;
        contentCategory = "fruits";
        playConfettiOnSessionWin = true;
        _canvas = GetComponentInParent<Canvas>();

        _fruitSprites = Resources.LoadAll<Sprite>("Tractor/Fruits");
        if (_fruitSprites == null || _fruitSprites.Length == 0)
            Debug.LogError("[HalfPuzzle] No fruit sprites found in Resources/Tractor/Fruits!");
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
        _topPieces.Clear();
        _bottomPieces.Clear();
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

        // Layout: top row at ~70% height, bottom row at ~25% height
        float topRowY = boardH * 0.70f;
        float bottomRowY = boardH * 0.25f;

        // Card size — large (x2), based on available width
        float padding = 30f;
        float spacing = 24f;
        float availW = boardW - padding * 2f - (_totalPairs - 1) * spacing;
        float cardW = Mathf.Min(availW / _totalPairs, 240f);
        float cardH = cardW; // square halves

        // Horizontal centering
        float totalW = _totalPairs * cardW + (_totalPairs - 1) * spacing;
        float startX = (boardW - totalW) / 2f + cardW / 2f;

        // Shuffled orders (independent for top and bottom rows)
        var topOrder = new List<int>();
        var bottomOrder = new List<int>();
        for (int i = 0; i < _totalPairs; i++) { topOrder.Add(i); bottomOrder.Add(i); }
        ShuffleList(topOrder);
        ShuffleList(bottomOrder);

        for (int i = 0; i < _totalPairs; i++)
        {
            float xPos = startX + i * (cardW + spacing);

            // ── Top half (draggable) ──
            int topIdx = topOrder[i];
            int topFruitIdx = _roundFruitIndices[topIdx];
            Sprite topFullSprite = _fruitSprites[topFruitIdx];
            if (topFullSprite == null) continue;

            Texture2D topTex = ExtractSpriteTexture(topFullSprite);
            Sprite topSprite = CreateTopHalf(topTex);

            var topGO = new GameObject($"Top_{topFullSprite.name}");
            topGO.transform.SetParent(boardArea, false);
            var topRT = topGO.AddComponent<RectTransform>();
            topRT.anchorMin = Vector2.zero;
            topRT.anchorMax = Vector2.zero;
            topRT.pivot = new Vector2(0.5f, 0f); // pivot at bottom edge
            topRT.sizeDelta = new Vector2(cardW, cardH);
            topRT.anchoredPosition = new Vector2(xPos, topRowY);

            var topImg = topGO.AddComponent<Image>();
            topImg.sprite = topSprite;
            topImg.preserveAspect = true;
            topImg.raycastTarget = true;

            var topDrag = topGO.AddComponent<DraggableHalf>();
            topDrag.Init(topIdx, topFullSprite.name, _canvas, this);
            _topPieces.Add(topDrag);

            // ── Bottom half (draggable) ──
            int bottomIdx = bottomOrder[i];
            int bottomFruitIdx = _roundFruitIndices[bottomIdx];
            Sprite bottomFullSprite = _fruitSprites[bottomFruitIdx];
            if (bottomFullSprite == null) continue;

            Texture2D bottomTex = ExtractSpriteTexture(bottomFullSprite);
            Sprite bottomSprite = CreateBottomHalf(bottomTex);

            var bottomGO = new GameObject($"Bottom_{bottomFullSprite.name}");
            bottomGO.transform.SetParent(boardArea, false);
            var bottomRT = bottomGO.AddComponent<RectTransform>();
            bottomRT.anchorMin = Vector2.zero;
            bottomRT.anchorMax = Vector2.zero;
            bottomRT.pivot = new Vector2(0.5f, 1f); // pivot at top edge
            bottomRT.sizeDelta = new Vector2(cardW, cardH);
            bottomRT.anchoredPosition = new Vector2(xPos, bottomRowY);

            var bottomImg = bottomGO.AddComponent<Image>();
            bottomImg.sprite = bottomSprite;
            bottomImg.preserveAspect = true;
            bottomImg.raycastTarget = true;

            var bottomDrag = bottomGO.AddComponent<DraggableHalf>();
            bottomDrag.Init(bottomIdx, bottomFullSprite.name, _canvas, this);
            _bottomPieces.Add(bottomDrag);
        }

        StartCoroutine(CaptureStartPositions());
    }

    private IEnumerator CaptureStartPositions()
    {
        yield return null;
        foreach (var p in _topPieces) if (p != null) p.CaptureStartPosition();
        foreach (var p in _bottomPieces) if (p != null) p.CaptureStartPosition();

        // Tutorial hand on first bottom piece
        if (TutorialHand != null && _bottomPieces.Count > 0)
        {
            var rt = _bottomPieces[0].GetComponent<RectTransform>();
            TutorialHand.SetPosition(TutorialHand.GetLocalCenter(rt));
        }
    }

    // ── Match logic ──

    /// <summary>
    /// Called by DraggableHalf on drop. Checks if the dragged piece is near its matching partner.
    /// Works in both directions: top piece near matching bottom, or bottom piece near matching top.
    /// </summary>
    public bool TryMatch(DraggableHalf piece)
    {
        if (IsInputLocked || piece.IsPlaced) return false;

        DismissTutorial();

        int pairId = piece.pairId;
        RectTransform pieceRT = piece.GetComponent<RectTransform>();

        // Find the matching partner in the OTHER row
        bool isTopPiece = _topPieces.Contains(piece);
        var partnerList = isTopPiece ? _bottomPieces : _topPieces;

        DraggableHalf partner = null;
        foreach (var p in partnerList)
        {
            if (p != null && !p.IsPlaced && p.pairId == pairId)
            { partner = p; break; }
        }

        if (partner == null) return false;

        RectTransform partnerRT = partner.GetComponent<RectTransform>();
        float dist = Vector2.Distance(pieceRT.anchoredPosition, partnerRT.anchoredPosition);

        if (dist > snapDistance)
        {
            // Check if near a WRONG partner (mistake)
            foreach (var p in partnerList)
            {
                if (p == null || p.IsPlaced || p.pairId == pairId) continue;
                RectTransform wrongRT = p.GetComponent<RectTransform>();
                float wrongDist = Vector2.Distance(pieceRT.anchoredPosition, wrongRT.anchoredPosition);
                if (wrongDist < snapDistance)
                {
                    Stats?.RecordMistake("wrong_half");
                    PlayWrongEffect(pieceRT);
                    return false;
                }
            }
            return false;
        }

        // Match! Snap both halves together at the midpoint
        Stats?.RecordCorrect("match", piece.animalId);

        Vector2 midpoint = (pieceRT.anchoredPosition + partnerRT.anchoredPosition) / 2f;

        // Determine which is top and which is bottom for correct stacking
        DraggableHalf topPiece = isTopPiece ? piece : partner;
        DraggableHalf bottomPiece = isTopPiece ? partner : piece;
        RectTransform topRT = topPiece.GetComponent<RectTransform>();
        RectTransform bottomRT = bottomPiece.GetComponent<RectTransform>();

        float halfH = topRT.sizeDelta.y * 0.5f;
        topPiece.Lock(new Vector2(midpoint.x, midpoint.y + halfH));
        bottomPiece.Lock(new Vector2(midpoint.x, midpoint.y - halfH));

        PlayCorrectEffect(pieceRT);

        _matchedCount++;
        if (_matchedCount >= _totalPairs)
            StartCoroutine(DelayedComplete());

        return true;
    }

    private IEnumerator DelayedComplete()
    {
        yield return new WaitForSeconds(0.5f);
        CompleteRound();
    }

    // ── Sprite splitting (top/bottom) ──

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

    private static Sprite CreateTopHalf(Texture2D tex)
    {
        if (tex == null) return null;
        int halfH = tex.height / 2;
        return Sprite.Create(tex,
            new Rect(0, halfH, tex.width, tex.height - halfH),
            new Vector2(0.5f, 0f), // pivot at bottom edge
            100f);
    }

    private static Sprite CreateBottomHalf(Texture2D tex)
    {
        if (tex == null) return null;
        int halfH = tex.height / 2;
        return Sprite.Create(tex,
            new Rect(0, 0, tex.width, halfH),
            new Vector2(0.5f, 1f), // pivot at top edge
            100f);
    }

    // ── Utility ──

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
