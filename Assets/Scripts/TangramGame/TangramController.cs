using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tangram game controller. Displays a target silhouette and scattered pieces.
/// Player drags pieces onto the silhouette. Tap to rotate.
/// Background: warm wooden table (Memory Game style).
/// </summary>
public class TangramController : BaseMiniGame
{
    [Header("References")]
    public RectTransform boardArea;    // center area where silhouette + snapped pieces go
    public RectTransform piecesArea;   // bottom area where scattered pieces start

    private Canvas canvas;
    private List<TangramPiece> activePieces = new List<TangramPiece>();
    private List<GameObject> silhouetteObjects = new List<GameObject>();
    private int placedCount;
    private TangramFigures.Figure currentFigure;

    // Board layout
    private const float GridUnit = 100f; // 1 grid unit = 100px

    // Wooden table colors (from MemoryGame)
    private static readonly Color TableBase = HexColor("#5C3D2E");
    private static readonly Color WoodA = HexColor("#8B6B4A");
    private static readonly Color WoodB = HexColor("#7E6042");
    private static readonly Color PlankSep = HexColor("#5A4030");
    private static readonly Color FrameEdge = HexColor("#6B4D38");
    private static readonly Color InnerRim = HexColor("#A08060");
    private static readonly Color SilhouetteColor = new Color(0.35f, 0.25f, 0.18f, 0.3f);

    protected override string GetFallbackGameId() => "tangram";

    protected override void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        BuildTableBackground();
        WireHomeButton();
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
        placedCount = 0;

        // Pick a figure based on difficulty
        int tier = TangramFigures.DifficultyToTier(Difficulty);
        var candidates = TangramFigures.GetByDifficulty(tier);
        currentFigure = candidates[Random.Range(0, candidates.Count)];

        BuildSilhouette();
        SpawnPieces();

        // Tutorial: point to first piece
        if (activePieces.Count > 0 && TutorialHand != null)
        {
            var hp = TutorialHand.transform.parent as RectTransform;
            var pieceRT = activePieces[0].GetComponent<RectTransform>();
            Vector3 worldPos = pieceRT.position;
            Vector2 localPos = (Vector2)hp.InverseTransformPoint(worldPos);
            TutorialHand.SetPosition(localPos);
        }
    }

    protected override void OnRoundCleanup()
    {
        foreach (var piece in activePieces)
            if (piece != null && piece.gameObject != null)
                Destroy(piece.gameObject);
        activePieces.Clear();

        foreach (var go in silhouetteObjects)
            if (go != null) Destroy(go);
        silhouetteObjects.Clear();
    }

    protected override IEnumerator OnAfterComplete()
    {
        // Brief celebration pause
        yield return new WaitForSeconds(0.5f);
    }

    // ═══════════════════════════════════════════════════════════
    //  SILHOUETTE
    // ═══════════════════════════════════════════════════════════

    private void BuildSilhouette()
    {
        foreach (var placement in currentFigure.pieces)
        {
            var sprite = TangramShapeGen.Get(placement.pieceIndex);
            if (sprite == null) continue;

            var go = new GameObject($"Silhouette_{placement.pieceIndex}");
            go.transform.SetParent(boardArea, false);

            var rt = go.AddComponent<RectTransform>();
            float size = GetPieceSize(placement.pieceIndex);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = placement.position * GridUnit;
            rt.localEulerAngles = new Vector3(0, 0, -placement.rotation);

            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = SilhouetteColor;
            img.raycastTarget = false;
            img.preserveAspect = true;

            silhouetteObjects.Add(go);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  PIECES
    // ═══════════════════════════════════════════════════════════

    private void SpawnPieces()
    {
        // Collect unique pieces needed for this figure
        var placements = currentFigure.pieces;

        float areaW = piecesArea.rect.width > 0 ? piecesArea.rect.width : 1600f;
        float areaH = piecesArea.rect.height > 0 ? piecesArea.rect.height : 250f;

        for (int i = 0; i < placements.Length; i++)
        {
            var placement = placements[i];
            var sprite = TangramShapeGen.Get(placement.pieceIndex);
            if (sprite == null) continue;

            var go = new GameObject($"Piece_{i}_{placement.pieceIndex}");
            // Parent to canvas root so it can be dragged freely
            go.transform.SetParent(canvas.transform, false);

            var rt = go.AddComponent<RectTransform>();
            float size = GetPieceSize(placement.pieceIndex);
            rt.sizeDelta = new Vector2(size, size);

            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = TangramShapeGen.PieceColors[placement.pieceIndex];
            img.raycastTarget = true;
            img.preserveAspect = true;

            var piece = go.AddComponent<TangramPiece>();

            // Correct position = silhouette position in canvas space
            Vector2 correctCanvasPos = BoardToCanvas(placement.position * GridUnit);
            piece.Init(placement.pieceIndex, correctCanvasPos, placement.rotation, canvas, this);

            // Scatter in pieces area
            Vector2 scatterPos = GetScatterPosition(i, placements.Length, areaW, areaH);
            float scatterRot = Random.Range(0, 8) * 45f; // random rotation in 45° steps
            piece.SetScatteredPosition(scatterPos, scatterRot);

            activePieces.Add(piece);
        }
    }

    private Vector2 GetScatterPosition(int index, int total, float areaW, float areaH)
    {
        // Convert pieces area to canvas space, then distribute evenly
        Vector2 areaCenter = PiecesAreaToCanvas(Vector2.zero);
        float spacing = areaW / (total + 1);
        float x = areaCenter.x - areaW * 0.5f + spacing * (index + 1);
        float y = areaCenter.y + Random.Range(-areaH * 0.15f, areaH * 0.15f);
        return new Vector2(x, y);
    }

    private Vector2 BoardToCanvas(Vector2 boardLocal)
    {
        // Convert boardArea local position to canvas local position
        Vector3 worldPos = boardArea.TransformPoint(boardLocal);
        return (Vector2)canvas.transform.InverseTransformPoint(worldPos);
    }

    private Vector2 PiecesAreaToCanvas(Vector2 localPos)
    {
        Vector3 worldPos = piecesArea.TransformPoint(localPos);
        return (Vector2)canvas.transform.InverseTransformPoint(worldPos);
    }

    private float GetPieceSize(int pieceIndex)
    {
        switch (pieceIndex)
        {
            case 0: case 1: return GridUnit * 1.8f;       // large triangles
            case 2: return GridUnit * 1.3f;                 // medium triangle
            case 3: case 4: return GridUnit * 0.9f;         // small triangles
            case 5: return GridUnit * 0.9f;                 // square
            case 6: return GridUnit * 1.3f;                 // parallelogram
            default: return GridUnit;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  CALLBACKS
    // ═══════════════════════════════════════════════════════════

    public void OnPiecePickedUp()
    {
        DismissTutorial();
    }

    public void OnPiecePlaced(RectTransform pieceRT)
    {
        RecordCorrect("snap", null, placedCount + 1 >= currentFigure.pieces.Length);
        PlayCorrectEffect(pieceRT);
        placedCount++;

        if (placedCount >= currentFigure.pieces.Length)
            CompleteRound();
    }

    // ═══════════════════════════════════════════════════════════
    //  TABLE BACKGROUND
    // ═══════════════════════════════════════════════════════════

    private void BuildTableBackground()
    {
        var bgTransform = canvas.transform.Find("Background");
        if (bgTransform == null) return;

        var bgImg = bgTransform.GetComponent<Image>();
        if (bgImg != null)
            bgImg.color = TableBase;

        var bgRT = bgTransform.GetComponent<RectTransform>();

        // Board panel with wooden frame
        var boardPanel = CreateChild(bgRT, "BoardPanel");
        var bpRT = boardPanel.AddComponent<RectTransform>();
        bpRT.anchorMin = new Vector2(0.03f, 0.12f);
        bpRT.anchorMax = new Vector2(0.97f, 0.88f);
        bpRT.sizeDelta = Vector2.zero;
        bpRT.anchoredPosition = Vector2.zero;

        // Frame outer edge
        var frameOuter = AddImageChild(boardPanel.transform, "FrameOuter", FrameEdge);
        StretchFill(frameOuter);

        // Frame main
        var frame = AddImageChild(boardPanel.transform, "Frame", InnerRim);
        var frameRT = frame.GetComponent<RectTransform>();
        frameRT.anchorMin = Vector2.zero;
        frameRT.anchorMax = Vector2.one;
        frameRT.offsetMin = new Vector2(6, 6);
        frameRT.offsetMax = new Vector2(-6, -6);

        // Wood surface with planks
        var surface = AddImageChild(frame.transform, "WoodSurface", WoodA);
        var surfRT = surface.GetComponent<RectTransform>();
        surfRT.anchorMin = Vector2.zero;
        surfRT.anchorMax = Vector2.one;
        surfRT.offsetMin = new Vector2(4, 4);
        surfRT.offsetMax = new Vector2(-4, -4);

        // Add horizontal plank lines
        int plankCount = 5;
        for (int i = 1; i < plankCount; i++)
        {
            float yNorm = (float)i / plankCount;
            var plank = AddImageChild(surface.transform, $"PlankLine_{i}", PlankSep);
            var plankRT = plank.GetComponent<RectTransform>();
            plankRT.anchorMin = new Vector2(0, yNorm);
            plankRT.anchorMax = new Vector2(1, yNorm);
            plankRT.pivot = new Vector2(0.5f, 0.5f);
            plankRT.sizeDelta = new Vector2(0, 2);
            plankRT.anchoredPosition = Vector2.zero;
        }

        // Alternate plank colors
        for (int i = 0; i < plankCount; i++)
        {
            float yMin = (float)i / plankCount;
            float yMax = (float)(i + 1) / plankCount;
            if (i % 2 == 1)
            {
                var altPlank = AddImageChild(surface.transform, $"PlankBg_{i}", WoodB);
                var apRT = altPlank.GetComponent<RectTransform>();
                apRT.anchorMin = new Vector2(0, yMin);
                apRT.anchorMax = new Vector2(1, yMax);
                apRT.sizeDelta = Vector2.zero;
                apRT.anchoredPosition = Vector2.zero;
                altPlank.transform.SetAsFirstSibling();
            }
        }

        // Vignette (darkened edges)
        var vignette = AddImageChild(bgRT, "Vignette", new Color(0.15f, 0.08f, 0.03f, 0.2f));
        StretchFill(vignette);
        vignette.GetComponent<Image>().raycastTarget = false;
    }

    // ── UI Helpers ─────────────────────────────────────────────

    private static GameObject CreateChild(RectTransform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject AddImageChild(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;

        var roundedRect = Resources.Load<Sprite>("UI/RoundedRect");
        if (roundedRect != null)
        {
            img.sprite = roundedRect;
            img.type = Image.Type.Sliced;
        }

        return go;
    }

    private static void StretchFill(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    private static Color HexColor(string hex)
    {
        Color c;
        ColorUtility.TryParseHtmlString(hex, out c);
        return c;
    }

    public void OnHomePressed() => ExitGame();

    private void WireHomeButton()
    {
        var btn = GetComponentInChildren<Canvas>()?.transform.Find("SafeArea/TopBar/HomeButton");
        if (btn != null)
        {
            var button = btn.GetComponent<Button>();
            if (button != null)
                button.onClick.AddListener(OnHomePressed);
        }
    }
}
