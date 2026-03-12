using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Puzzle game: shows a faded reference image in the center.
/// 4 pieces (2x2) sit at the bottom — player drags them onto
/// the reference. Pieces snap and stick when placed correctly.
/// On completion, loads a new random puzzle after a short delay.
/// </summary>
public class PuzzleGameController : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform puzzleArea;        // main area (below top bar)
    public RawImage referenceImage;         // big faded reference in center
    public RectTransform pieceTray;         // bottom tray where pieces start

    [Header("Settings")]
    public float referenceAlpha = 0.3f;     // opacity of the guide image

    private Canvas canvas;
    private List<PuzzlePiece> pieces = new List<PuzzlePiece>();
    private List<GameObject> slotObjects = new List<GameObject>();
    private int placedCount = 0;
    private const int GridSize = 2; // 2x2 = 4 pieces
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
            GameContext.CurrentSelection = null; // clear so next round is random
            return sprite;
        }

        // Pick a random animal from the game's sub-items list
        var game = GameContext.CurrentGame;
        if (game != null && game.subItems != null && game.subItems.Count > 0)
        {
            int index;
            if (game.subItems.Count == 1)
            {
                index = 0;
            }
            else
            {
                // Avoid repeating the same animal
                do { index = Random.Range(0, game.subItems.Count); }
                while (index == lastAnimalIndex);
            }
            lastAnimalIndex = index;
            return game.subItems[index].contentAsset;
        }

        return null;
    }

    private void LoadRandomPuzzle()
    {
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
        // Destroy old pieces
        foreach (var piece in pieces)
        {
            if (piece != null)
                Destroy(piece.gameObject);
        }
        pieces.Clear();

        // Destroy old slot overlays
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

        // ── Reference image: fill most of the puzzle area ──
        float areaW = puzzleArea.rect.width;
        float areaH = puzzleArea.rect.height;

        // Reserve bottom 30% for piece tray, use top 70% for reference
        float refAreaH = areaH * 0.65f;
        float refAreaW = areaW * 0.9f;

        float aspect = (float)srcW / srcH;
        float refW, refH;
        if (aspect > refAreaW / refAreaH)
        {
            refW = refAreaW;
            refH = refAreaW / aspect;
        }
        else
        {
            refH = refAreaH;
            refW = refAreaH * aspect;
        }

        // Position reference image centered in upper portion
        referenceImage.rectTransform.sizeDelta = new Vector2(refW, refH);
        referenceImage.rectTransform.anchoredPosition = new Vector2(0, areaH * 0.12f);
        referenceImage.texture = sourceSprite.texture;
        referenceImage.color = new Color(1f, 1f, 1f, referenceAlpha);

        // Piece dimensions on screen
        float pieceW = refW / GridSize;
        float pieceH = refH / GridSize;

        // The reference image's center in puzzleArea local space
        Vector2 refCenter = referenceImage.rectTransform.anchoredPosition;
        float refLeft = refCenter.x - refW / 2f;
        float refTop = refCenter.y + refH / 2f;

        // Correct positions for each piece (relative to puzzleArea)
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

        // Slice source texture
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

        // Shuffle order for tray placement
        List<int> indices = new List<int>();
        for (int i = 0; i < 4; i++) indices.Add(i);
        for (int i = 3; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = indices[i];
            indices[i] = indices[j];
            indices[j] = tmp;
        }

        // Place pieces in the tray (bottom area)
        float trayW = pieceTray.rect.width;
        float trayPieceW = Mathf.Min(pieceW * 0.85f, trayW / 4f - 10f);
        float trayPieceH = trayPieceW / ((float)sliceW / sliceH);
        float trayStartX = -((4 * trayPieceW + 3 * 10f) / 2f) + trayPieceW / 2f;

        for (int i = 0; i < 4; i++)
        {
            int idx = indices[i];

            var pieceGO = new GameObject($"Piece_{idx}");
            pieceGO.transform.SetParent(puzzleArea, false);

            var rt = pieceGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(pieceW - 4, pieceH - 4);

            var rawImg = pieceGO.AddComponent<RawImage>();
            rawImg.texture = pieceTextures[idx];
            rawImg.raycastTarget = true;

            pieceGO.AddComponent<CanvasGroup>();
            var piece = pieceGO.AddComponent<PuzzlePiece>();
            piece.Init(idx, correctPositions[idx], canvas, this);

            // Position in tray row at bottom
            float trayX = trayStartX + i * (trayPieceW + 10f);
            Vector2 trayLocalPos = new Vector2(trayX, -areaH / 2f + trayPieceH / 2f + 30f);
            rt.anchoredPosition = trayLocalPos;
            rt.localScale = new Vector3(trayPieceW / (pieceW - 4), trayPieceH / (pieceH - 4), 1f);

            piece.SetStartPosition(trayLocalPos);
            piece.SetTrayScale(rt.localScale);
            piece.SetFullScale(Vector3.one);

            pieces.Add(piece);
        }

        // Draw subtle grid lines on the reference
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
                slotGO.transform.SetParent(puzzleArea, false);
                slotGO.transform.SetAsFirstSibling();

                var slotRT = slotGO.AddComponent<RectTransform>();
                slotRT.sizeDelta = new Vector2(pieceW - 4, pieceH - 4);
                slotRT.anchoredPosition = new Vector2(
                    refLeft + col * pieceW + pieceW / 2f,
                    refTop - row * pieceH - pieceH / 2f
                );

                var slotImg = slotGO.AddComponent<Image>();
                slotImg.color = new Color(0.85f, 0.82f, 0.78f, 0.35f);
                slotImg.raycastTarget = false;

                slotObjects.Add(slotGO);
            }
        }
    }

    public void OnPiecePlaced()
    {
        placedCount++;
        if (placedCount >= 4)
        {
            // Puzzle complete — hide reference, show completed image briefly, then load next
            referenceImage.color = new Color(1f, 1f, 1f, 0f);
            Debug.Log("Puzzle complete!");
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
