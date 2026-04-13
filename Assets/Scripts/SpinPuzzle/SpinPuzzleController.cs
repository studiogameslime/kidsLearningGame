using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spin Puzzle — "סובב והתאם".
/// Pieces are placed in their correct grid positions but randomly rotated.
/// Tap a piece to rotate it 90° clockwise. Solve by getting all pieces to 0° rotation.
///
/// Solo: one puzzle centered.
/// 2-Player: split screen, identical puzzles, race to finish first.
/// Difficulty: 2x2 (easy), 3x3 (medium), 4x4 (hard).
/// </summary>
public class SpinPuzzleController : BaseMiniGame
{
    [Header("UI References")]
    public RectTransform playArea;

    [Header("Settings")]
    public Sprite roundedRectSprite;

    private Canvas canvas;
    private int gridSize = 3;
    private int totalPieces => gridSize * gridSize;
    private int lastAnimalIndex = -1;

    private List<GameObject> spawnedObjects = new List<GameObject>();

    // Solo state
    private List<SpinPiece> pieces = new List<SpinPiece>();
    private int solvedCount;

    // 2-Player state
    private List<SpinPiece> p1Pieces = new List<SpinPiece>();
    private List<SpinPiece> p2Pieces = new List<SpinPiece>();
    private int p1Solved;
    private int p2Solved;
    private bool raceFinished;

    // ── BaseMiniGame Hooks ──────────────────────────────────────

    protected override string GetFallbackGameId() => "spinpuzzle";

    protected override void OnGameInit()
    {
        totalRounds = 1;
        isEndless = false;
        playConfettiOnSessionWin = true;

        canvas = GetComponentInParent<Canvas>();

        gridSize = GameDifficultyConfig.SpinPuzzleGridSize(Difficulty);
    }

    protected override void OnRoundSetup()
    {
        StartCoroutine(SetupAfterLayout());
    }

    protected override void OnRoundCleanup()
    {
        ClearAll();
    }

    private IEnumerator SetupAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(playArea);
        LoadPuzzle();
    }

    // ── Loading ─────────────────────────────────────────────────

    private void LoadPuzzle()
    {
        ClearAll();
        raceFinished = false;

        Sprite puzzleSprite = PickPuzzleSprite();
        if (puzzleSprite == null)
        {
            Debug.LogError("[SpinPuzzle] No puzzle sprite available!");
            return;
        }

        Texture2D source = GetReadableTexture(puzzleSprite);

        if (TwoPlayerManager.IsActive)
            Build2PlayerPuzzles(source, puzzleSprite);
        else
            BuildSoloPuzzle(source, puzzleSprite);

        if (source != puzzleSprite.texture)
            Object.Destroy(source);
    }

    private Sprite PickPuzzleSprite()
    {
        // Use specific selection if provided
        if (GameContext.CurrentSelection != null)
        {
            var sprite = GameContext.CurrentSelection.contentAsset;
            GameContext.CurrentSelection = null;
            return sprite;
        }

        // Pick random from puzzle game's sub-items (reuse puzzle content)
        var puzzleGame = FindPuzzleGameData();
        if (puzzleGame != null && puzzleGame.subItems != null)
        {
            var valid = new List<int>();
            for (int i = 0; i < puzzleGame.subItems.Count; i++)
                if (puzzleGame.subItems[i].contentAsset != null)
                    valid.Add(i);

            if (valid.Count > 0)
            {
                int pick;
                if (valid.Count == 1) pick = 0;
                else
                {
                    do { pick = Random.Range(0, valid.Count); }
                    while (valid[pick] == lastAnimalIndex);
                }
                lastAnimalIndex = valid[pick];
                return puzzleGame.subItems[lastAnimalIndex].contentAsset;
            }
        }

        return null;
    }

    private GameItemData FindPuzzleGameData()
    {
        // Try current game first
        if (GameContext.CurrentGame != null && GameContext.CurrentGame.subItems != null
            && GameContext.CurrentGame.subItems.Count > 0)
            return GameContext.CurrentGame;

        // Fall back to puzzle game from database
        var db = Resources.Load<GameDatabase>("GameDatabase");
        if (db != null)
        {
            foreach (var g in db.games)
                if (g.id == "puzzle") return g;
        }
        return null;
    }

    // ── Solo Build ──────────────────────────────────────────────

    private void BuildSoloPuzzle(Texture2D source, Sprite sourceSprite)
    {
        float areaW = playArea.rect.width;
        float areaH = playArea.rect.height;

        // Puzzle takes center, 70% of area
        float maxW = areaW * 0.70f;
        float maxH = areaH * 0.75f;

        pieces.Clear();
        solvedCount = 0;

        var puzzleContainer = CreatePuzzleContainer("Puzzle", 0.5f, 0.5f);
        BuildPuzzleGrid(source, sourceSprite, puzzleContainer, maxW, maxH, pieces, OnSoloPieceTapped);

        // Reference image small in corner
        CreateReferenceImage(sourceSprite, areaW * 0.15f, areaH * 0.15f,
            new Vector2(areaW - 10f, areaH - 10f), new Vector2(1f, 1f));
    }

    // ── 2-Player Build ──────────────────────────────────────────

    private void Build2PlayerPuzzles(Texture2D source, Sprite sourceSprite)
    {
        float areaW = playArea.rect.width;
        float areaH = playArea.rect.height;

        float halfW = areaW * 0.42f;
        float maxH = areaH * 0.62f;

        p1Pieces.Clear();
        p2Pieces.Clear();
        p1Solved = 0;
        p2Solved = 0;

        // Player backgrounds
        CreatePlayerZone(0f, areaW * 0.49f, 0f, areaH, TwoPlayerManager.Player1Color);
        CreatePlayerZone(areaW * 0.51f, areaW, 0f, areaH, TwoPlayerManager.Player2Color);

        // Center divider
        var divider = new GameObject("Divider");
        divider.transform.SetParent(playArea, false);
        var dRT = divider.AddComponent<RectTransform>();
        dRT.anchorMin = new Vector2(0.5f, 0f);
        dRT.anchorMax = new Vector2(0.5f, 1f);
        dRT.sizeDelta = new Vector2(4, 0);
        dRT.anchoredPosition = Vector2.zero;
        divider.AddComponent<Image>().color = new Color(1, 1, 1, 0.4f);
        divider.GetComponent<Image>().raycastTarget = false;
        spawnedObjects.Add(divider);

        // Player 1 puzzle (left) — centered slightly lower to avoid header
        var p1Container = CreatePuzzleContainer("P1Puzzle", 0.245f, 0.46f);
        BuildPuzzleGrid(source, sourceSprite, p1Container, halfW, maxH, p1Pieces, OnP1PieceTapped);

        // Player 2 puzzle (right) — same rotations
        var p2Container = CreatePuzzleContainer("P2Puzzle", 0.755f, 0.46f);
        BuildPuzzleGrid(source, sourceSprite, p2Container, halfW, maxH, p2Pieces, OnP2PieceTapped,
            GetRotations(p1Pieces));

        // Player names — positioned below header, not overlapping it
        CreatePlayerLabel(TwoPlayerManager.GetName(1), areaW * 0.245f,
            areaH * 0.90f, TwoPlayerManager.Player1Color);
        CreatePlayerLabel(TwoPlayerManager.GetName(2), areaW * 0.755f,
            areaH * 0.90f, TwoPlayerManager.Player2Color);

        // Small reference images
        float refSize = Mathf.Min(areaW * 0.10f, areaH * 0.12f);
        CreateReferenceImage(sourceSprite, refSize, refSize,
            new Vector2(areaW * 0.245f, areaH * 0.05f), new Vector2(0.5f, 0f));
        CreateReferenceImage(sourceSprite, refSize, refSize,
            new Vector2(areaW * 0.755f, areaH * 0.05f), new Vector2(0.5f, 0f));
    }

    private int[] GetRotations(List<SpinPiece> fromPieces)
    {
        var rotations = new int[fromPieces.Count];
        for (int i = 0; i < fromPieces.Count; i++)
            rotations[i] = fromPieces[i].CurrentRotationSteps;
        return rotations;
    }

    // ── Puzzle Grid Builder ─────────────────────────────────────

    private RectTransform CreatePuzzleContainer(string name, float anchorX, float anchorY)
    {
        var go = new GameObject(name);
        go.transform.SetParent(playArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(anchorX, anchorY);
        rt.anchorMax = new Vector2(anchorX, anchorY);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        spawnedObjects.Add(go);
        return rt;
    }

    private void BuildPuzzleGrid(Texture2D source, Sprite sourceSprite,
        RectTransform container, float maxW, float maxH,
        List<SpinPiece> pieceList, System.Action<SpinPiece> onTap,
        int[] forcedRotations = null)
    {
        int srcW = source.width;
        int srcH = source.height;

        float aspect = (float)srcW / srcH;
        float puzzleW, puzzleH;
        if (aspect > maxW / maxH)
        {
            puzzleW = maxW;
            puzzleH = maxW / aspect;
        }
        else
        {
            puzzleH = maxH;
            puzzleW = maxH * aspect;
        }

        float pieceW = puzzleW / gridSize;
        float pieceH = puzzleH / gridSize;
        float gap = 3f;

        int sliceW = srcW / gridSize;
        int sliceH = srcH / gridSize;

        float startX = -puzzleW / 2f;
        float startY = puzzleH / 2f;

        for (int row = 0; row < gridSize; row++)
        {
            for (int col = 0; col < gridSize; col++)
            {
                int idx = row * gridSize + col;

                // Slice texture
                int pixX = col * sliceW;
                int pixY = srcH - (row + 1) * sliceH;
                var pieceTex = new Texture2D(sliceW, sliceH, TextureFormat.RGBA32, false);
                pieceTex.filterMode = FilterMode.Bilinear;
                pieceTex.SetPixels(source.GetPixels(pixX, pixY, sliceW, sliceH));
                pieceTex.Apply();

                // Create piece GO
                var go = new GameObject($"Piece_{row}_{col}");
                go.transform.SetParent(container, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(
                    startX + col * pieceW + pieceW / 2f,
                    startY - row * pieceH - pieceH / 2f);
                rt.sizeDelta = new Vector2(pieceW - gap, pieceH - gap);

                var rawImg = go.AddComponent<RawImage>();
                rawImg.texture = pieceTex;
                rawImg.raycastTarget = true;

                // Piece component
                var piece = go.AddComponent<SpinPiece>();
                int rotSteps;
                if (forcedRotations != null)
                    rotSteps = forcedRotations[idx];
                else
                {
                    // Random rotation: 1, 2, or 3 steps (never 0 = already solved)
                    rotSteps = Random.Range(1, 4);
                }
                piece.Init(idx, rotSteps, onTap);
                pieceList.Add(piece);

                // Grid border
                var border = new GameObject("Border");
                border.transform.SetParent(go.transform, false);
                var brt = border.AddComponent<RectTransform>();
                brt.anchorMin = Vector2.zero;
                brt.anchorMax = Vector2.one;
                brt.offsetMin = new Vector2(-1, -1);
                brt.offsetMax = new Vector2(1, 1);
                var bImg = border.AddComponent<Image>();
                bImg.color = new Color(0.3f, 0.25f, 0.2f, 0.4f);
                bImg.raycastTarget = false;
                border.transform.SetAsFirstSibling();

                spawnedObjects.Add(go);
            }
        }
    }

    // ── Tap Handlers ────────────────────────────────────────────

    private void OnSoloPieceTapped(SpinPiece piece)
    {
        if (IsInputLocked) return;
        DismissTutorial();

        piece.RotateStep(this);

        if (piece.IsSolved)
        {
            solvedCount++;
            Stats?.RecordCorrect();
            PlayCorrectEffect(piece.GetComponent<RectTransform>());

            if (solvedCount >= totalPieces)
                StartCoroutine(SoloCompletionSequence());
        }
    }

    private void OnP1PieceTapped(SpinPiece piece)
    {
        if (IsInputLocked || raceFinished) return;

        piece.RotateStep(this);

        if (piece.IsSolved)
        {
            p1Solved++;
            PlayCorrectEffect(piece.GetComponent<RectTransform>());

            if (p1Solved >= totalPieces)
            {
                raceFinished = true;
                TwoPlayerManager.Score1++;
                StartCoroutine(RaceCompletionSequence(1));
            }
        }
    }

    private void OnP2PieceTapped(SpinPiece piece)
    {
        if (IsInputLocked || raceFinished) return;

        piece.RotateStep(this);

        if (piece.IsSolved)
        {
            p2Solved++;
            PlayCorrectEffect(piece.GetComponent<RectTransform>());

            if (p2Solved >= totalPieces)
            {
                raceFinished = true;
                TwoPlayerManager.Score2++;
                StartCoroutine(RaceCompletionSequence(2));
            }
        }
    }

    // ── Completion ──────────────────────────────────────────────

    private IEnumerator SoloCompletionSequence()
    {
        yield return new WaitForSeconds(0.3f);

        // Flash all pieces
        foreach (var piece in pieces)
            StartCoroutine(FlashPiece(piece));
        yield return new WaitForSeconds(0.6f);

        CompleteRound();
    }

    private IEnumerator RaceCompletionSequence(int winner)
    {
        yield return new WaitForSeconds(0.3f);

        // Flash winner's pieces
        var winnerPieces = winner == 1 ? p1Pieces : p2Pieces;
        foreach (var piece in winnerPieces)
            StartCoroutine(FlashPiece(piece));

        // Show winner message
        yield return StartCoroutine(ShowWinnerMessage(winner));

        yield return new WaitForSeconds(0.5f);

        CompleteRound();
    }

    private IEnumerator FlashPiece(SpinPiece piece)
    {
        if (piece == null) yield break;
        var img = piece.GetComponent<RawImage>();
        if (img == null) yield break;

        Color orig = img.color;
        float t = 0;
        while (t < 0.15f)
        {
            t += Time.deltaTime;
            img.color = Color.Lerp(orig, Color.white, t / 0.15f * 0.5f);
            yield return null;
        }
        t = 0;
        Color peak = img.color;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            img.color = Color.Lerp(peak, orig, t / 0.3f);
            yield return null;
        }
        img.color = orig;
    }

    private IEnumerator ShowWinnerMessage(int winner)
    {
        string winnerName = TwoPlayerManager.GetName(winner);
        Color winnerColor = TwoPlayerManager.GetColor(winner);

        var msgGO = new GameObject("WinMsg");
        msgGO.transform.SetParent(playArea, false);
        var msgRT = msgGO.AddComponent<RectTransform>();
        msgRT.anchorMin = new Vector2(0.5f, 0.5f);
        msgRT.anchorMax = new Vector2(0.5f, 0.5f);
        msgRT.pivot = new Vector2(0.5f, 0.5f);
        msgRT.anchoredPosition = Vector2.zero;
        msgRT.sizeDelta = new Vector2(600, 80);

        var tmp = msgGO.AddComponent<TMPro.TextMeshProUGUI>();
        HebrewText.SetText(tmp, winnerName + " \u05E0\u05D9\u05E6\u05D7!");
        tmp.fontSize = 52;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.color = winnerColor;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.outlineWidth = 0.3f;
        tmp.outlineColor = new Color(0.1f, 0.1f, 0.1f);

        spawnedObjects.Add(msgGO);

        // Scale in
        msgRT.localScale = Vector3.zero;
        float t = 0;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / 0.35f);
            float scale = p < 0.6f
                ? Mathf.Lerp(0f, 1.12f, p / 0.6f)
                : Mathf.Lerp(1.12f, 1f, (p - 0.6f) / 0.4f);
            msgRT.localScale = Vector3.one * scale;
            yield return null;
        }
        msgRT.localScale = Vector3.one;

        yield return new WaitForSeconds(1.5f);

        // Fade out
        t = 0;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            tmp.alpha = 1f - Mathf.Clamp01(t / 0.3f);
            yield return null;
        }

        Destroy(msgGO);
    }

    // ── UI Helpers ──────────────────────────────────────────────

    private void CreateReferenceImage(Sprite sprite, float w, float h,
        Vector2 pos, Vector2 pivot)
    {
        var go = new GameObject("RefImage");
        go.transform.SetParent(playArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;

        float aspect = (float)sprite.texture.width / sprite.texture.height;
        float fitW, fitH;
        if (aspect > w / h) { fitW = w; fitH = w / aspect; }
        else { fitH = h; fitW = h * aspect; }
        rt.sizeDelta = new Vector2(fitW, fitH);

        var img = go.AddComponent<RawImage>();
        img.texture = sprite.texture;
        img.color = new Color(1, 1, 1, 0.6f);
        img.raycastTarget = false;

        // Border
        var border = new GameObject("RefBorder");
        border.transform.SetParent(go.transform, false);
        var brt = border.AddComponent<RectTransform>();
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(-2, -2);
        brt.offsetMax = new Vector2(2, 2);
        var bImg = border.AddComponent<Image>();
        bImg.color = new Color(0.3f, 0.25f, 0.2f, 0.5f);
        bImg.raycastTarget = false;
        border.transform.SetAsFirstSibling();

        spawnedObjects.Add(go);
    }

    private void CreatePlayerZone(float left, float right, float bottom, float top, Color color)
    {
        // Border-only frame (no fill to avoid tinting)
        float borderW = 8f;
        float pad = 6f;
        Color borderColor = new Color(color.r, color.g, color.b, 0.8f);

        CreateRect("ZoneTop", left + pad, top - borderW - pad, right - left - pad * 2, borderW, borderColor);
        CreateRect("ZoneBottom", left + pad, bottom + pad, right - left - pad * 2, borderW, borderColor);
        CreateRect("ZoneLeft", left + pad, bottom + pad, borderW, top - bottom - pad * 2, borderColor);
        CreateRect("ZoneRight", right - borderW - pad, bottom + pad, borderW, top - bottom - pad * 2, borderColor);
    }

    private void CreateRect(string name, float x, float y, float w, float h, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(playArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
        go.AddComponent<Image>().color = color;
        go.GetComponent<Image>().raycastTarget = false;
        spawnedObjects.Add(go);
    }

    private void CreatePlayerLabel(string text, float centerX, float y, Color color)
    {
        var go = new GameObject("PlayerLabel");
        go.transform.SetParent(playArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(centerX, y);
        rt.sizeDelta = new Vector2(300, 40);

        var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
        HebrewText.SetText(tmp, text);
        tmp.fontSize = 28;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.color = color;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        spawnedObjects.Add(go);
    }

    private void ClearAll()
    {
        foreach (var go in spawnedObjects)
            if (go != null) Destroy(go);
        spawnedObjects.Clear();
        pieces.Clear();
        p1Pieces.Clear();
        p2Pieces.Clear();
    }

    // ── Navigation ──────────────────────────────────────────────

    public void OnHomePressed() => ExitGame();

    // ── Texture Utility ─────────────────────────────────────────

    private static Texture2D GetReadableTexture(Sprite sprite)
    {
        var tex = sprite.texture;
        try { tex.GetPixels(); return tex; }
        catch { /* Not readable — copy via RenderTexture */ }

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
