using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Main controller for the Number Maze game ("מבוך המספרים").
/// Shows a grid of numbered tiles. Player taps numbers 1→2→3→...→N in order.
/// The correct sequence follows an orthogonally adjacent path on the grid.
/// </summary>
public class NumberMazeController : BaseMiniGame
{
    [Header("Layout")]
    public RectTransform gridArea;

    [Header("UI")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI progressText;

    [Header("Sprites")]
    public Sprite cellSprite; // RoundedRect

    private int _mistakesThisRound;
    private int _hintsUsed;
    private float _lastInteractionTime;

    private NumberMazeBoardGenerator.BoardData _board;
    private int _expectedNext; // 1-based: next number the player should tap
    private List<GameObject> _cellObjects = new List<GameObject>();
    private List<GameObject> _pathLines = new List<GameObject>();
    private NumberMazeCellView[] _cellViews;
    private Coroutine _inactivityCoroutine;

    private static readonly Color PathLineColor = new Color(0.26f, 0.63f, 0.28f, 0.8f); // green
    private static readonly Color PathGlowColor = new Color(0.26f, 0.63f, 0.28f, 0.15f);
    private const float PathLineWidth = 8f;
    private const float PathGlowWidth = 22f;

    // ── BASE MINI GAME HOOKS ─────────────────────────────────────

    protected override void OnGameInit()
    {
        totalRounds = 1;
        isEndless = true; // endless free-play rounds
        playConfettiOnRoundWin = true;
        contentCategory = "";
        playWinSound = true;
        delayBeforeNextRound = 2.5f;
    }

    protected override string GetFallbackGameId() => "numbermaze";

    protected override string GetContentId() => null;

    protected override void OnRoundSetup()
    {
        _mistakesThisRound = 0;
        _hintsUsed = 0;
        _expectedNext = 1;

        // Generate board
        int cols, rows, pathLen;
        NumberMazeBoardGenerator.GetGridConfig(Difficulty, out cols, out rows, out pathLen);
        _board = NumberMazeBoardGenerator.Generate(cols, rows, pathLen);

        Stats.SetCustom("gridCols", (float)cols);
        Stats.SetCustom("gridRows", (float)rows);
        Stats.SetCustom("pathLength", (float)_board.pathLength);

        // Build grid UI
        BuildGrid();

        // Auto-complete the first cell (1) so the child starts tapping from 2
        int startCI = _board.pathCellIndices[0];
        _cellViews[startCI].SetCompleted();
        var startBtn = _cellViews[startCI].GetComponent<Button>();
        if (startBtn != null) startBtn.interactable = false;
        _expectedNext = 2;

        UpdateProgress();

        _lastInteractionTime = Time.time;
        _inactivityCoroutine = StartCoroutine(InactivityMonitor());

        // Position tutorial hand on the next number cell the player should tap
        PositionTutorialHand();

        Debug.Log($"[NumberMaze] Grid {cols}x{rows}, path {_board.pathLength}, difficulty {Difficulty}");
    }

    protected override void OnRoundCleanup()
    {
        if (_inactivityCoroutine != null)
        {
            StopCoroutine(_inactivityCoroutine);
            _inactivityCoroutine = null;
        }

        foreach (var go in _cellObjects)
            if (go != null) Destroy(go);
        _cellObjects.Clear();
        foreach (var go in _pathLines)
            if (go != null) Destroy(go);
        _pathLines.Clear();
        _cellViews = null;
    }

    protected override void OnBeforeComplete()
    {
        Stats.SetCustom("mistakes", (float)_mistakesThisRound);
        Stats.SetCustom("hintsUsed", (float)_hintsUsed);
    }

    protected override IEnumerator OnAfterComplete()
    {
        // Wave-complete all path cells
        for (int i = 0; i < _board.pathLength; i++)
        {
            int ci = _board.pathCellIndices[i];
            if (_cellViews[ci] != null)
            {
                var rt = _cellViews[ci].GetComponent<RectTransform>();
                StartCoroutine(CelebrateBounce(rt, i * 0.06f));
            }
        }

        yield return new WaitForSeconds(0.8f);
    }

    // ── GRID BUILDING ──

    private void BuildGrid()
    {
        if (gridArea == null || cellSprite == null) return;

        int cols = _board.cols;
        int rows = _board.rows;

        float areaW = gridArea.rect.width;
        float areaH = gridArea.rect.height;

        // Calculate cell size to fit grid with spacing
        float spacing = 10f;
        float maxCellW = (areaW - (cols - 1) * spacing) / cols;
        float maxCellH = (areaH - (rows - 1) * spacing) / rows;
        float cellSize = Mathf.Min(maxCellW, maxCellH, 160f);

        float gridW = cols * cellSize + (cols - 1) * spacing;
        float gridH = rows * cellSize + (rows - 1) * spacing;

        // Center offset
        float offsetX = -gridW / 2f + cellSize / 2f;
        float offsetY = gridH / 2f - cellSize / 2f;

        _cellViews = new NumberMazeCellView[cols * rows];

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                int ci = y * cols + x;
                float px = offsetX + x * (cellSize + spacing);
                float py = offsetY - y * (cellSize + spacing);

                var cellGO = CreateCell(gridArea, px, py, cellSize, ci);
                _cellObjects.Add(cellGO);

                var view = cellGO.GetComponent<NumberMazeCellView>();
                var cellData = _board.cells[ci];

                // Highlight start (1) and end (target) cells
                if (cellData.isOnPath && cellData.pathOrder == 0)
                    view.SetStart(cellData.displayNumber);
                else if (cellData.isOnPath && cellData.pathOrder == _board.pathLength - 1)
                    view.SetEnd(cellData.displayNumber);
                else
                    view.SetDefault(cellData.displayNumber);

                _cellViews[ci] = view;

                // Staggered bounce-in
                float delay = (y * cols + x) * 0.03f;
                view.BounceIn(delay);
            }
        }
    }

    private GameObject CreateCell(RectTransform parent, float x, float y, float size, int cellIndex)
    {
        var go = new GameObject($"Cell_{cellIndex}");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = new Vector2(x, y);

        // Border (behind)
        var borderGO = new GameObject("Border");
        borderGO.transform.SetParent(go.transform, false);
        var borderRT = borderGO.AddComponent<RectTransform>();
        borderRT.anchorMin = Vector2.zero;
        borderRT.anchorMax = Vector2.one;
        borderRT.offsetMin = new Vector2(-3, -3);
        borderRT.offsetMax = new Vector2(3, 3);
        var borderImg = borderGO.AddComponent<Image>();
        borderImg.sprite = cellSprite;
        borderImg.type = Image.Type.Sliced;
        borderImg.color = new Color(0.88f, 0.88f, 0.88f);
        borderImg.raycastTarget = false;

        // Background
        var bgImg = go.AddComponent<Image>();
        bgImg.sprite = cellSprite;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = Color.white;
        bgImg.raycastTarget = true;

        // Number text
        var textGO = new GameObject("Number");
        textGO.transform.SetParent(go.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = Mathf.Min(52f, size * 0.45f);
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        // Button
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bgImg;
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.95f, 0.95f, 0.95f);
        colors.pressedColor = new Color(0.9f, 0.9f, 0.9f);
        btn.colors = colors;

        int capturedIndex = cellIndex;
        btn.onClick.AddListener(() => OnCellTapped(capturedIndex));

        // Cell view component
        var view = go.AddComponent<NumberMazeCellView>();
        view.Init(bgImg, borderImg, tmp, cellIndex);

        return go;
    }

    // ── PATH LINE DRAWING ──

    private void DrawPathLine(Vector2 from, Vector2 to)
    {
        // Glow (wide, behind)
        var glowGO = new GameObject("PathGlow");
        glowGO.transform.SetParent(gridArea, false);
        glowGO.transform.SetAsFirstSibling();
        var glowRT = glowGO.AddComponent<RectTransform>();
        var glowImg = glowGO.AddComponent<Image>();
        glowImg.color = PathGlowColor;
        glowImg.raycastTarget = false;
        PositionLine(glowRT, from, to, PathGlowWidth);
        _pathLines.Add(glowGO);

        // Main line
        var lineGO = new GameObject("PathLine");
        lineGO.transform.SetParent(gridArea, false);
        lineGO.transform.SetSiblingIndex(1); // above glow, below cells
        var lineRT = lineGO.AddComponent<RectTransform>();
        var lineImg = lineGO.AddComponent<Image>();
        lineImg.color = PathLineColor;
        lineImg.raycastTarget = false;
        PositionLine(lineRT, from, to, PathLineWidth);
        _pathLines.Add(lineGO);
    }

    private void PositionLine(RectTransform rt, Vector2 from, Vector2 to, float width)
    {
        Vector2 dir = to - from;
        float distance = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rt.sizeDelta = new Vector2(distance, width);
        rt.anchoredPosition = from + dir * 0.5f;
        rt.localRotation = Quaternion.Euler(0, 0, angle);
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    // ── INPUT HANDLING ──

    private void OnCellTapped(int cellIndex)
    {
        if (IsInputLocked) return;
        DismissTutorial();
        _lastInteractionTime = Time.time;

        var cell = _board.cells[cellIndex];
        var view = _cellViews[cellIndex];

        // Check: is this the expected next number on the path?
        if (cell.isOnPath && cell.pathOrder == _expectedNext - 1)
        {
            // Correct!
            RecordCorrect("number_tap", _expectedNext.ToString());
            PlayCorrectEffect(view.GetComponent<RectTransform>());
            view.SetCompleted();

            // Draw path line from previous cell
            if (_expectedNext >= 2)
            {
                int prevCI = _board.pathCellIndices[_expectedNext - 2];
                int currCI = cellIndex;
                var fromPos = _cellViews[prevCI].GetComponent<RectTransform>().anchoredPosition;
                var toPos = _cellViews[currCI].GetComponent<RectTransform>().anchoredPosition;
                DrawPathLine(fromPos, toPos);
            }

            // Disable this cell's button
            var btn = view.GetComponent<Button>();
            if (btn != null) btn.interactable = false;

            _expectedNext++;
            UpdateProgress();

            // Check if completed
            if (_expectedNext > _board.pathLength)
            {
                // Play feedback and celebrate BEFORE CompleteRound
                StartCoroutine(OnMazeComplete());
            }
        }
        else
        {
            // Wrong tap
            _mistakesThisRound++;
            RecordMistake("wrong_cell", cell.displayNumber.ToString());
            PlayWrongEffect(view.GetComponent<RectTransform>());
            view.ShowError();

            // After 3 mistakes on this round, hint
            if (_mistakesThisRound >= 3 && _mistakesThisRound % 2 == 1)
            {
                _hintsUsed++;
                RecordHint();
                HintNextCell();
            }
        }
    }

    private IEnumerator OnMazeComplete()
    {
        yield return new WaitForSeconds(0.8f);

        // Base handles sound + confetti + round advance
        CompleteRound();
    }

    private void HintNextCell()
    {
        if (_expectedNext <= _board.pathLength)
        {
            int targetCellIndex = _board.pathCellIndices[_expectedNext - 1];
            if (_cellViews[targetCellIndex] != null)
                _cellViews[targetCellIndex].PulseHint();
        }
    }

    // ── ANIMATIONS ──

    private IEnumerator CelebrateBounce(RectTransform rt, float delay)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);
        float dur = 0.2f;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float p = t / dur;
            float scale = 1f + 0.15f * Mathf.Sin(p * Mathf.PI);
            rt.localScale = Vector3.one * scale;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    // ── UI ──

    private void UpdateProgress()
    {
        if (progressText != null)
        {
            int done = Mathf.Max(0, _expectedNext - 1);
            progressText.text = $"{done}/{_board.pathLength}";
        }
    }

    // ── INACTIVITY ──

    private IEnumerator InactivityMonitor()
    {
        while (!IsInputLocked)
        {
            yield return new WaitForSeconds(1f);
            if (!IsInputLocked && Time.time - _lastInteractionTime >= 6f)
            {
                HintNextCell();
                _lastInteractionTime = Time.time;
            }
        }
    }

    // ── TUTORIAL HAND ──

    private void PositionTutorialHand()
    {
        if (TutorialHand == null || _cellViews == null) return;

        // Point at the next cell the player needs to tap (number 2, index 1 in path)
        if (_board.pathLength >= 2)
        {
            int nextCellIndex = _board.pathCellIndices[1]; // second cell = number 2
            var cellRT = _cellViews[nextCellIndex].GetComponent<RectTransform>();
            Vector2 localPos = TutorialHand.GetLocalCenter(cellRT);
            TutorialHand.SetPosition(localPos);
        }
    }

    // ── NAVIGATION ──

    public void OnHomePressed()
    {
        ExitGame();
    }
}
