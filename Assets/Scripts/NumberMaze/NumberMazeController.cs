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
public class NumberMazeController : MonoBehaviour
{
    [Header("Layout")]
    public RectTransform gridArea;

    [Header("UI")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI progressText;

    [Header("Sprites")]
    public Sprite cellSprite; // RoundedRect

    private GameStatsCollector _stats;
    private int _difficulty = 1;
    private bool _roundActive;
    private int _mistakesThisRound;
    private int _hintsUsed;
    private float _lastInteractionTime;

    private NumberMazeBoardGenerator.BoardData _board;
    private int _expectedNext; // 1-based: next number the player should tap
    private List<GameObject> _cellObjects = new List<GameObject>();
    private NumberMazeCellView[] _cellViews;
    private Coroutine _inactivityCoroutine;

    private void Start()
    {
        LoadRound();
    }

    // ── ROUND LIFECYCLE ──

    private void LoadRound()
    {
        ClearRound();
        _roundActive = true;
        _mistakesThisRound = 0;
        _hintsUsed = 0;
        _expectedNext = 1;

        string gameId = GameContext.CurrentGame != null ? GameContext.CurrentGame.id : "numbermaze";
        _stats = new GameStatsCollector(gameId);
        if (GameCompletionBridge.Instance != null)
            GameCompletionBridge.Instance.ActiveCollector = _stats;
        _stats.SetTotalRoundsPlanned(1);

        _difficulty = GameDifficultyConfig.GetLevel(gameId);

        // Generate board
        int cols, rows, pathLen;
        NumberMazeBoardGenerator.GetGridConfig(_difficulty, out cols, out rows, out pathLen);
        _board = NumberMazeBoardGenerator.Generate(cols, rows, pathLen);

        _stats.SetCustom("gridCols", (float)cols);
        _stats.SetCustom("gridRows", (float)rows);
        _stats.SetCustom("pathLength", (float)_board.pathLength);

        // Build grid UI
        BuildGrid();
        UpdateProgress();

        _lastInteractionTime = Time.time;
        _inactivityCoroutine = StartCoroutine(InactivityMonitor());

        Debug.Log($"[NumberMaze] Grid {cols}x{rows}, path {_board.pathLength}, difficulty {_difficulty}");
    }

    private void ClearRound()
    {
        if (_inactivityCoroutine != null)
        {
            StopCoroutine(_inactivityCoroutine);
            _inactivityCoroutine = null;
        }

        foreach (var go in _cellObjects)
            if (go != null) Destroy(go);
        _cellObjects.Clear();
        _cellViews = null;
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

    // ── INPUT HANDLING ──

    private void OnCellTapped(int cellIndex)
    {
        if (!_roundActive) return;
        _lastInteractionTime = Time.time;

        var cell = _board.cells[cellIndex];
        var view = _cellViews[cellIndex];

        // Check: is this the expected next number on the path?
        if (cell.isOnPath && cell.pathOrder == _expectedNext - 1)
        {
            // Correct!
            _stats.RecordCorrect("number_tap", _expectedNext.ToString());
            view.SetCompleted();

            // Disable this cell's button
            var btn = view.GetComponent<Button>();
            if (btn != null) btn.interactable = false;

            _expectedNext++;
            UpdateProgress();

            // Check if completed
            if (_expectedNext > _board.pathLength)
            {
                _roundActive = false;
                _stats.SetCustom("mistakes", (float)_mistakesThisRound);
                _stats.SetCustom("hintsUsed", (float)_hintsUsed);
                StartCoroutine(OnRoundComplete());
            }
        }
        else
        {
            // Wrong tap
            _mistakesThisRound++;
            _stats.RecordMistake("wrong_cell", cell.displayNumber.ToString());
            view.ShowError();

            // After 3 mistakes on this round, hint
            if (_mistakesThisRound >= 3 && _mistakesThisRound % 2 == 1)
            {
                _hintsUsed++;
                _stats.RecordHint();
                HintNextCell();
            }
        }
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

    // ── COMPLETION ──

    private IEnumerator OnRoundComplete()
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

        SoundLibrary.PlayRandomFeedback();
        yield return new WaitForSeconds(0.8f);

        _stats.RecordRoundComplete();

        // Confetti
        if (ConfettiController.Instance != null)
            ConfettiController.Instance.Play();

        if (!GameCompletionBridge.WillJourneyNavigate)
        {
            yield return new WaitForSeconds(2.5f);
            LoadRound();
        }
    }

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
        while (_roundActive)
        {
            yield return new WaitForSeconds(1f);
            if (_roundActive && Time.time - _lastInteractionTime >= 6f)
            {
                HintNextCell();
                _lastInteractionTime = Time.time;
            }
        }
    }

    // ── NAVIGATION ──

    public void OnHomePressed()
    {
        if (_roundActive && _stats != null) _stats.Abandon();
        NavigationManager.GoToMainMenu();
    }
}
