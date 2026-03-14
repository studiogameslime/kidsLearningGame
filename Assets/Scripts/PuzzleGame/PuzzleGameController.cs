using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Puzzle game: 3x3 grid (9 pieces).
///
/// Layout (portrait):
///   Right side — puzzle board with faded reference image and grid slots
///   Left side  — 9 scattered puzzle pieces for the child to drag
///
/// Children drag pieces from the left onto matching slots on the right.
/// Pieces snap when placed correctly. Confetti on completion.
/// </summary>
public class PuzzleGameController : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform boardArea;          // right side — puzzle board
    public RectTransform piecesArea;         // left side — scattered pieces
    public RawImage referenceImage;          // faded preview in board area

    [Header("Settings")]
    public float referenceAlpha = 0.25f;

    private Canvas canvas;
    private List<PuzzlePiece> pieces = new List<PuzzlePiece>();
    private List<GameObject> slotObjects = new List<GameObject>();
    private int placedCount = 0;
    private const int GridSize = 3; // 3x3 = 9 pieces
    private int totalPieces => GridSize * GridSize;
    private int lastAnimalIndex = -1;

    private void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        LoadRandomPuzzle();
    }

    private Sprite PickRandomPuzzleSprite()
    {
        // If launched with a specific selection, use it (first time only)
        if (GameContext.CurrentSelection != null)
        {
            var sprite = GameContext.CurrentSelection.contentAsset;
            GameContext.CurrentSelection = null;
            return sprite;
        }

        // Pick a random animal from the game's sub-items list (skip gallery item)
        var game = GameContext.CurrentGame;
        if (game != null && game.subItems != null && game.subItems.Count > 0)
        {
            var validItems = new List<int>();
            for (int i = 0; i < game.subItems.Count; i++)
            {
                if (game.subItems[i].contentAsset != null)
                    validItems.Add(i);
            }

            if (validItems.Count == 0) return null;

            int pick;
            if (validItems.Count == 1)
            {
                pick = 0;
            }
            else
            {
                do { pick = Random.Range(0, validItems.Count); }
                while (validItems[pick] == lastAnimalIndex);
            }
            lastAnimalIndex = validItems[pick];
            return game.subItems[lastAnimalIndex].contentAsset;
        }

        return null;
    }

    private void LoadRandomPuzzle()
    {
        // Check for custom gallery texture first
        if (GameContext.CustomTexture != null)
        {
            var tex = GameContext.CustomTexture;
            GameContext.CustomTexture = null;

            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
            BuildPuzzle(sprite);
            return;
        }

        Sprite puzzleSprite = PickRandomPuzzleSprite();

        if (puzzleSprite == null)
        {
            Debug.LogError("No puzzle sprite available! Check contentAsset on sub-items.");
            return;
        }

        BuildPuzzle(puzzleSprite);
    }

    private void ClearPuzzle()
    {
        foreach (var piece in pieces)
        {
            if (piece != null)
                Destroy(piece.gameObject);
        }
        pieces.Clear();

        foreach (var slot in slotObjects)
        {
            if (slot != null)
                Destroy(slot);
        }
        slotObjects.Clear();

        placedCount = 0;
    }

    private void BuildPuzzle(Sprite sourceSprite)
    {
        Texture2D source = GetReadableTexture(sourceSprite);
        int srcW = source.width;
        int srcH = source.height;

        // ── Board area dimensions ──
        float boardW = boardArea.rect.width;
        float boardH = boardArea.rect.height;
        if (boardW <= 0) boardW = 480f;
        if (boardH <= 0) boardH = 1600f;

        // Fit reference image into board area (90% width, 60% height max)
        float maxRefW = boardW * 0.92f;
        float maxRefH = boardH * 0.65f;

        float aspect = (float)srcW / srcH;
        float refW, refH;
        if (aspect > maxRefW / maxRefH)
        {
            refW = maxRefW;
            refH = maxRefW / aspect;
        }
        else
        {
            refH = maxRefH;
            refW = maxRefH * aspect;
        }

        // Position reference image centered in board area
        referenceImage.rectTransform.sizeDelta = new Vector2(refW, refH);
        referenceImage.rectTransform.anchoredPosition = Vector2.zero;
        referenceImage.texture = sourceSprite.texture;
        referenceImage.color = new Color(1f, 1f, 1f, referenceAlpha);

        // Piece dimensions on screen (matching reference grid)
        float pieceW = refW / GridSize;
        float pieceH = refH / GridSize;

        // The reference image center in board area local space
        Vector2 refCenter = Vector2.zero;
        float refLeft = refCenter.x - refW / 2f;
        float refTop = refCenter.y + refH / 2f;

        // Correct positions for each piece (relative to boardArea)
        List<Vector2> correctPositions = new List<Vector2>();
        for (int row = 0; row < GridSize; row++)
        {
            for (int col = 0; col < GridSize; col++)
            {
                float cx = refLeft + col * pieceW + pieceW / 2f;
                float cy = refTop - row * pieceH - pieceH / 2f;
                correctPositions.Add(new Vector2(cx, cy));
            }
        }

        // Slice source texture into 3x3 pieces
        int sliceW = srcW / GridSize;
        int sliceH = srcH / GridSize;
        List<Texture2D> pieceTextures = new List<Texture2D>();

        for (int row = 0; row < GridSize; row++)
        {
            for (int col = 0; col < GridSize; col++)
            {
                int pixX = col * sliceW;
                int pixY = srcH - (row + 1) * sliceH; // flip Y

                var pieceTex = new Texture2D(sliceW, sliceH, TextureFormat.RGBA32, false);
                pieceTex.filterMode = FilterMode.Bilinear;
                Color[] pixels = source.GetPixels(pixX, pixY, sliceW, sliceH);
                pieceTex.SetPixels(pixels);
                pieceTex.Apply();
                pieceTextures.Add(pieceTex);
            }
        }

        // Shuffle order for left-side placement
        List<int> indices = new List<int>();
        for (int i = 0; i < totalPieces; i++) indices.Add(i);
        for (int i = totalPieces - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = indices[i];
            indices[i] = indices[j];
            indices[j] = tmp;
        }

        // ── Scatter pieces on left side ──
        float piecesW = piecesArea.rect.width;
        float piecesH = piecesArea.rect.height;
        if (piecesW <= 0) piecesW = 480f;
        if (piecesH <= 0) piecesH = 1600f;

        // Scale pieces to fit comfortably on left side
        // Target: pieces fill ~70% of left area width in a 3-column layout
        float scatterPieceW = (piecesW * 0.70f) / 3f;
        float pieceScale = scatterPieceW / pieceW;
        float scatterPieceH = pieceH * pieceScale;

        // Arrange in a 3x3 scattered grid with jitter
        // Extra top margin to prevent overlap with header
        float marginX = piecesW * 0.10f;
        float marginY = piecesH * 0.08f;
        float usableW = piecesW - marginX * 2f;
        float usableH = piecesH - marginY * 2f;
        float cellW = usableW / 3f;
        float cellH = usableH / 3f;

        for (int i = 0; i < totalPieces; i++)
        {
            int idx = indices[i];

            var pieceGO = new GameObject($"Piece_{idx}");
            pieceGO.transform.SetParent(piecesArea, false);

            var rt = pieceGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(pieceW - 3, pieceH - 3);

            var rawImg = pieceGO.AddComponent<RawImage>();
            rawImg.texture = pieceTextures[idx];
            rawImg.raycastTarget = true;

            pieceGO.AddComponent<CanvasGroup>();
            var piece = pieceGO.AddComponent<PuzzlePiece>();

            // Convert correct position from boardArea space to piecesArea space
            Vector2 correctInBoard = correctPositions[idx];
            Vector3 worldPos = boardArea.TransformPoint(correctInBoard);
            Vector2 correctInPieces;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                piecesArea, RectTransformUtility.WorldToScreenPoint(null, worldPos),
                null, out correctInPieces);

            // Actually, pieces snap in their parent's coordinate space.
            // Since pieces are parented to piecesArea but snap targets are in boardArea,
            // we need to store the world-space target and convert on snap.
            // Simpler: parent pieces to a shared container. Use canvas root.

            // Re-parent piece to canvas so it can move freely across both areas
            pieceGO.transform.SetParent(canvas.transform, false);
            piece.Init(idx, correctPositions[idx], canvas, this, boardArea);

            // Scatter position: grid cell + jitter (in piecesArea local space, then convert)
            int scatterCol = i % 3;
            int scatterRow = i / 3;
            float baseX = -piecesW / 2f + marginX + cellW * scatterCol + cellW / 2f;
            float baseY = piecesH / 2f - marginY - cellH * scatterRow - cellH / 2f;
            float jitterX = Random.Range(-cellW * 0.15f, cellW * 0.15f);
            float jitterY = Random.Range(-cellH * 0.12f, cellH * 0.12f);

            // Convert piecesArea local position to canvas local position
            Vector2 localInPieces = new Vector2(baseX + jitterX, baseY + jitterY);
            Vector3 worldScatter = piecesArea.TransformPoint(localInPieces);
            Vector2 canvasLocal;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(),
                RectTransformUtility.WorldToScreenPoint(null, worldScatter),
                null, out canvasLocal);

            rt.anchoredPosition = canvasLocal;
            Vector3 trayScaleVec = Vector3.one * pieceScale;
            rt.localScale = trayScaleVec;

            // Convert board correct position to canvas space
            Vector3 worldCorrect = boardArea.TransformPoint(correctPositions[idx]);
            Vector2 canvasCorrect;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(),
                RectTransformUtility.WorldToScreenPoint(null, worldCorrect),
                null, out canvasCorrect);

            piece.SetCorrectCanvasPos(canvasCorrect);
            piece.SetStartPosition(canvasLocal);
            piece.SetTrayScale(trayScaleVec);
            piece.SetFullScale(Vector3.one);

            pieces.Add(piece);
        }

        // Draw subtle grid overlay on the board
        CreateGridOverlay(refW, refH, pieceW, pieceH, refCenter);

        if (source != sourceSprite.texture)
            Object.Destroy(source);
    }

    private void CreateGridOverlay(float refW, float refH, float pieceW, float pieceH, Vector2 refCenter)
    {
        float refLeft = refCenter.x - refW / 2f;
        float refTop = refCenter.y + refH / 2f;

        for (int row = 0; row < GridSize; row++)
        {
            for (int col = 0; col < GridSize; col++)
            {
                var slotGO = new GameObject($"Slot_{row}_{col}");
                slotGO.transform.SetParent(boardArea, false);
                slotGO.transform.SetAsFirstSibling();

                var slotRT = slotGO.AddComponent<RectTransform>();
                slotRT.sizeDelta = new Vector2(pieceW - 3, pieceH - 3);
                slotRT.anchoredPosition = new Vector2(
                    refLeft + col * pieceW + pieceW / 2f,
                    refTop - row * pieceH - pieceH / 2f
                );

                var slotImg = slotGO.AddComponent<Image>();
                slotImg.color = new Color(0.78f, 0.70f, 0.58f, 0.30f); // warm sandy slot color
                slotImg.raycastTarget = false;

                slotObjects.Add(slotGO);
            }
        }
    }

    public void OnPiecePlaced()
    {
        placedCount++;
        if (placedCount >= totalPieces)
        {
            referenceImage.color = new Color(1f, 1f, 1f, 0f);
            ConfettiController.Instance.Play();
            StartCoroutine(LoadNextPuzzleAfterDelay(1.5f));
        }
    }

    private IEnumerator LoadNextPuzzleAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ClearPuzzle();
        LoadRandomPuzzle();
    }

    public void OnHomePressed()
    {
        NavigationManager.GoToMainMenu();
    }

    private static Texture2D GetReadableTexture(Sprite sprite)
    {
        var tex = sprite.texture;
        try
        {
            tex.GetPixels();
            return tex;
        }
        catch
        {
            // Not readable — copy via RenderTexture
        }

        RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(tex, rt);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
        readable.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        return readable;
    }
}
