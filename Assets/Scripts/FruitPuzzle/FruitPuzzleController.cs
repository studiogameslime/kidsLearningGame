using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Vehicle Puzzle — vehicles shown as silhouettes on the grass,
/// each cut into 3 horizontal strips scattered in the sky.
/// Easy: 1 vehicle (3 pieces), Medium: 2 vehicles (6 pieces), Hard: 3 vehicles (9 pieces).
/// All pieces mixed together — child figures out which piece belongs where.
/// </summary>
public class FruitPuzzleController : BaseMiniGame
{
    [Header("Layout")]
    public RectTransform silhouetteArea;
    public RectTransform piecesArea;

    [Header("UI")]
    public TextMeshProUGUI titleText;

    [Header("Settings")]
    public float vehicleDisplaySize = 450f;
    public float snapThreshold = 0.45f;

    private Sprite[] vehicleSprites;
    private List<int> usedIndices = new List<int>();

    private List<GameObject> silhouetteGOs = new List<GameObject>();
    private List<FruitPuzzlePiece> activePieces = new List<FruitPuzzlePiece>();
    private int placedCount;
    private int totalPieces;
    private int vehicleCount;
    private const int StripCount = 3; // always 3 horizontal strips

    protected override void OnGameInit()
    {
        isEndless = true;
        totalRounds = 1;
        vehicleSprites = Resources.LoadAll<Sprite>("VehiclePuzzle/cars");
    }

    protected override void OnRoundSetup()
    {
        ClearRound();
        placedCount = 0;

        int difficulty = GameDifficultyConfig.GetLevel("fruitpuzzle");

        // Easy: 1 vehicle, Medium: 2, Hard: 3
        if (difficulty <= 3)      vehicleCount = 1;
        else if (difficulty <= 6) vehicleCount = 2;
        else                      vehicleCount = 3;

        if (vehicleSprites == null || vehicleSprites.Length == 0) return;

        // Pick unique vehicles
        var pickedSprites = new List<Sprite>();
        for (int v = 0; v < vehicleCount; v++)
        {
            int idx; int attempts = 0;
            do { idx = Random.Range(0, vehicleSprites.Length); attempts++; }
            while (usedIndices.Contains(idx) && attempts < 50);
            usedIndices.Add(idx);
            pickedSprites.Add(vehicleSprites[idx]);
        }

        totalPieces = StripCount * vehicleCount;

        CreateSilhouettes(pickedSprites);

        // Build all piece data
        var allPieces = new List<PieceData>();
        for (int v = 0; v < pickedSprites.Count; v++)
        {
            Vector2 silCenter = GetSilhouetteCenter(v);
            for (int strip = 0; strip < StripCount; strip++)
            {
                allPieces.Add(new PieceData
                {
                    sprite = pickedSprites[v],
                    vehicleIndex = v,
                    stripIndex = strip,
                    silhouetteCenter = silCenter
                });
            }
        }

        // Shuffle all pieces
        for (int i = allPieces.Count - 1; i > 0; i--)
        { int j = Random.Range(0, i + 1); var tmp = allPieces[i]; allPieces[i] = allPieces[j]; allPieces[j] = tmp; }

        CreatePieces(allPieces);
    }

    protected override void OnRoundCleanup() => ClearRound();
    protected override string GetFallbackGameId() => "fruitpuzzle";
    public void OnHomePressed() => ExitGame();

    private void ClearRound()
    {
        foreach (var go in silhouetteGOs) if (go != null) Destroy(go);
        silhouetteGOs.Clear();
        foreach (var p in activePieces) if (p != null) Destroy(p.gameObject);
        activePieces.Clear();
    }

    // ── Silhouettes ──

    private Vector2 GetSilhouetteCenter(int idx)
    {
        if (silhouetteArea == null) return Vector2.zero;
        Rect bounds = silhouetteArea.rect;
        float y = 0f; // all at the same height

        if (vehicleCount == 1)
            return new Vector2(0, y);

        // Evenly spread across width
        float totalW = bounds.width * 0.8f;
        float step = totalW / (vehicleCount - 1);
        float startX = -totalW * 0.5f;
        return new Vector2(startX + idx * step, y);
    }

    private void CreateSilhouettes(List<Sprite> sprites)
    {
        if (silhouetteArea == null) return;
        for (int v = 0; v < sprites.Count; v++)
        {
            var go = new GameObject($"Silhouette_{v}");
            go.transform.SetParent(silhouetteArea, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(vehicleDisplaySize, vehicleDisplaySize);
            rt.anchoredPosition = GetSilhouetteCenter(v);
            var img = go.AddComponent<Image>();
            img.sprite = sprites[v];
            img.preserveAspect = true;
            img.color = new Color(0.1f, 0.1f, 0.1f, 0.25f);
            img.raycastTarget = false;
            silhouetteGOs.Add(go);
        }
    }

    // ── Pieces ──

    private struct PieceData
    {
        public Sprite sprite;
        public int vehicleIndex;
        public int stripIndex; // 0=top, 1=middle, 2=bottom
        public Vector2 silhouetteCenter;
    }

    private void CreatePieces(List<PieceData> allPieces)
    {
        if (piecesArea == null) return;

        float stripW = vehicleDisplaySize / StripCount;
        float stripH = vehicleDisplaySize;

        Rect bounds = piecesArea.rect;

        // Arrange pieces in a row, evenly spaced
        int count = allPieces.Count;
        float totalRowW = count * stripW + (count - 1) * 20f;
        float rowStartX = -totalRowW * 0.5f + stripW * 0.5f;
        float rowStep = stripW + 20f;

        for (int idx = 0; idx < allPieces.Count; idx++)
        {
            var pd = allPieces[idx];

            var maskGO = new GameObject($"Strip_{pd.vehicleIndex}_{pd.stripIndex}");
            maskGO.transform.SetParent(piecesArea, false);

            var maskRT = maskGO.AddComponent<RectTransform>();
            maskRT.anchorMin = maskRT.anchorMax = new Vector2(0.5f, 0.5f);
            maskRT.pivot = new Vector2(0.5f, 0.5f);
            maskRT.sizeDelta = new Vector2(stripW, stripH);

            Vector2 scatterPos = new Vector2(rowStartX + idx * rowStep, 0);
            maskRT.anchoredPosition = scatterPos;

            maskGO.AddComponent<RectMask2D>();

            var hitImg = maskGO.AddComponent<Image>();
            hitImg.color = Color.clear;
            hitImg.raycastTarget = true;

            // Child image — full vehicle, offset horizontally to show correct strip
            var imageGO = new GameObject("VehicleImage");
            imageGO.transform.SetParent(maskGO.transform, false);
            var imageRT = imageGO.AddComponent<RectTransform>();
            imageRT.anchorMin = imageRT.anchorMax = new Vector2(0.5f, 0.5f);
            imageRT.pivot = new Vector2(0.5f, 0.5f);
            imageRT.sizeDelta = new Vector2(vehicleDisplaySize, vehicleDisplaySize);

            // Offset: strip 0 = left, strip 1 = middle, strip 2 = right
            float offsetX = (1 - pd.stripIndex) * stripW; // strip0=+stripW, strip1=0, strip2=-stripW
            imageRT.anchoredPosition = new Vector2(offsetX, 0);

            var img = imageGO.AddComponent<Image>();
            img.sprite = pd.sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;

            // Piece component
            var piece = maskGO.AddComponent<FruitPuzzlePiece>();
            piece.gridCol = pd.vehicleIndex; // reuse col for vehicle index
            piece.gridRow = pd.stripIndex;    // reuse row for strip index
            piece.controller = this;

            // Target position: on the silhouette, correct horizontal strip
            float targetX = pd.silhouetteCenter.x + (pd.stripIndex - 1) * stripW;
            float targetY = pd.silhouetteCenter.y;

            // Convert from silhouetteArea to piecesArea coordinates
            Vector2 silWorldPos = silhouetteArea.TransformPoint(new Vector3(targetX, targetY, 0));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                piecesArea, RectTransformUtility.WorldToScreenPoint(null, silWorldPos),
                null, out Vector2 localTarget);
            piece.targetPosition = localTarget;

            StartCoroutine(DelayedSetHome(piece, scatterPos, idx * 0.06f));
            StartCoroutine(piece.PopIn(idx * 0.06f));
            activePieces.Add(piece);
        }
    }

    private IEnumerator DelayedSetHome(FruitPuzzlePiece piece, Vector2 pos, float delay)
    { yield return new WaitForSeconds(delay + 0.3f); piece.SetHome(); }

    private List<Vector2> GenerateScatterPositions(Rect bounds, int count, float pieceSize)
    {
        var positions = new List<Vector2>();
        float margin = pieceSize * 0.4f;
        for (int i = 0; i < count; i++)
        {
            Vector2 pos = Vector2.zero;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                pos = new Vector2(
                    Random.Range(bounds.xMin + margin, bounds.xMax - margin),
                    Random.Range(bounds.yMin + margin, bounds.yMax - margin));
                bool tooClose = false;
                foreach (var other in positions)
                    if (Vector2.Distance(pos, other) < pieceSize * 0.9f) { tooClose = true; break; }
                if (!tooClose) break;
            }
            positions.Add(pos);
        }
        return positions;
    }

    // ── Drop Logic ──

    public void OnPieceDropped(FruitPuzzlePiece piece)
    {
        float stripW = vehicleDisplaySize / StripCount;
        float dist = Vector2.Distance(
            piece.GetComponent<RectTransform>().anchoredPosition,
            piece.targetPosition);

        if (dist < stripW * snapThreshold)
        {
            placedCount++;
            piece.SnapToTarget();
            RecordCorrect("strip", $"{piece.gridCol}_{piece.gridRow}", placedCount >= totalPieces);
            if (placedCount >= totalPieces) StartCoroutine(RoundComplete());
        }
        else
        {
            RecordMistake("strip", $"{piece.gridCol}_{piece.gridRow}");
            piece.ReturnToHome();
        }
    }

    private IEnumerator RoundComplete()
    {
        yield return new WaitForSeconds(0.5f);
        foreach (var go in silhouetteGOs)
        { var img = go.GetComponent<Image>(); if (img != null) img.color = Color.clear; }
        if (ConfettiController.Instance != null) ConfettiController.Instance.PlayBig();
        yield return new WaitForSeconds(1f);
        CompleteRound();
    }
}
