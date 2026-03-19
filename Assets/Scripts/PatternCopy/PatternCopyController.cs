using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Pattern Copy mini-game controller.
/// Left side: source pattern. Right side: player grid.
/// Tap cells to toggle ON/OFF. Auto-wins when pattern matches exactly.
/// After 5 seconds of inactivity, hints by flashing one correct unfilled cell.
/// </summary>
public class PatternCopyController : MonoBehaviour
{
    [Header("Layout")]
    public RectTransform sourceGridParent;
    public RectTransform playerGridParent;
    public RectTransform playArea;

    [Header("UI")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI scoreText;

    [Header("Sprites")]
    public Sprite cellSprite;

    [Header("Settings")]
    public float cellSpacing = 8f;
    public float gridPadding = 12f;
    public float hintDelay = 5f;
    public Color emptyColor = new Color(0.93f, 0.93f, 0.93f, 1f);
    public Color sourceFilledColor = new Color(0.55f, 0.75f, 0.95f, 1f);
    public Color correctFeedbackColor = new Color(0.4f, 0.85f, 0.45f, 1f);
    public Color hintColor = new Color(1f, 0.85f, 0.3f, 0.8f);

    // State
    private int _gridSize;
    private bool[,] _sourcePattern;
    private bool[,] _playerGrid;
    private Image[,] _sourceCellImages;
    private Image[,] _playerCellImages;
    private int _filledTarget;
    private int _roundsPlayed;
    private int _undoCount;
    private GameStatsCollector _stats;
    private Color _playerColor;
    private bool _roundActive;
    private bool _celebrating;
    private float _lastTapTime;
    private Coroutine _hintCoroutine;
    private bool _hintActive;

    private void Start()
    {
        // Resolve player color from profile
        _playerColor = sourceFilledColor;
        var profile = ProfileManager.ActiveProfile;
        if (profile != null)
        {
            Color parsed;
            if (ColorUtility.TryParseHtmlString(profile.avatarColorHex, out parsed))
                _playerColor = parsed;
        }

        LoadRound();
    }

    private void Update()
    {
        if (!_roundActive || _celebrating) return;

        // Check inactivity for hint
        if (Time.realtimeSinceStartup - _lastTapTime >= hintDelay && !_hintActive)
        {
            _hintCoroutine = StartCoroutine(ShowHint());
        }
    }

    private void LoadRound()
    {
        _roundsPlayed++;
        _undoCount = 0;
        _celebrating = false;
        _roundActive = true;
        _hintActive = false;
        _lastTapTime = Time.realtimeSinceStartup;

        if (_hintCoroutine != null)
        {
            StopCoroutine(_hintCoroutine);
            _hintCoroutine = null;
        }

        string gameId = GameContext.CurrentGame != null ? GameContext.CurrentGame.id : "patterncopy";
        int difficulty = GameDifficultyConfig.GetLevel(gameId);

        _stats = new GameStatsCollector(gameId, null, SessionContent.Matching);
        if (GameCompletionBridge.Instance != null)
            GameCompletionBridge.Instance.ActiveCollector = _stats;

        var pattern = PatternGenerator.Generate(difficulty);
        _gridSize = pattern.gridSize;
        _sourcePattern = pattern.cells;
        _filledTarget = pattern.filledCount;
        _playerGrid = new bool[_gridSize, _gridSize];

        _stats.SetCustom("gridSize", _gridSize);
        _stats.SetCustom("filledCells", _filledTarget);
        _stats.SetCustom("difficulty", difficulty);
        _stats.SetTotalRoundsPlanned(1);

        Debug.Log($"[PatternCopy] Difficulty={difficulty} Grid={_gridSize}x{_gridSize} Filled={_filledTarget}");

        BuildGrid(sourceGridParent, _sourcePattern, true, ref _sourceCellImages);
        BuildGrid(playerGridParent, _playerGrid, false, ref _playerCellImages);
        UpdateScoreText();
        StartCoroutine(AnimateGridsIn());
    }

    private void BuildGrid(RectTransform parent, bool[,] grid, bool isSource, ref Image[,] imageArray)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);

        imageArray = new Image[_gridSize, _gridSize];

        // Use the smaller dimension of the parent to keep cells square
        float parentW = parent.rect.width;
        float parentH = parent.rect.height;
        if (parentW <= 0) parentW = 400f;
        if (parentH <= 0) parentH = 400f;
        float parentSize = Mathf.Min(parentW, parentH);

        float totalSpacing = cellSpacing * (_gridSize - 1) + gridPadding * 2f;
        float cellSize = (parentSize - totalSpacing) / _gridSize;
        cellSize = Mathf.Max(cellSize, 20f);

        float gridSpan = cellSize * _gridSize + cellSpacing * (_gridSize - 1);
        float startX = -gridSpan / 2f + cellSize / 2f;
        float startY = gridSpan / 2f - cellSize / 2f;

        for (int r = 0; r < _gridSize; r++)
        {
            for (int c = 0; c < _gridSize; c++)
            {
                var cellGO = new GameObject($"Cell_{r}_{c}");
                cellGO.transform.SetParent(parent, false);

                var rt = cellGO.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(cellSize, cellSize);
                float x = startX + c * (cellSize + cellSpacing);
                float y = startY - r * (cellSize + cellSpacing);
                rt.anchoredPosition = new Vector2(x, y);

                var img = cellGO.AddComponent<Image>();
                img.sprite = cellSprite;
                img.type = Image.Type.Sliced;
                img.raycastTarget = !isSource;
                imageArray[r, c] = img;

                if (isSource)
                {
                    img.color = grid[r, c] ? _playerColor : emptyColor;
                }
                else
                {
                    img.color = emptyColor;
                    var btn = cellGO.AddComponent<Button>();
                    btn.transition = Selectable.Transition.None;
                    int row = r, col = c;
                    btn.onClick.AddListener(() => OnPlayerCellTapped(row, col));
                }
            }
        }
    }

    // ── PLAYER INPUT ──────────────────────────────────────────────

    private void OnPlayerCellTapped(int row, int col)
    {
        if (!_roundActive || _celebrating) return;

        // Cancel any active hint
        if (_hintCoroutine != null)
        {
            StopCoroutine(_hintCoroutine);
            _hintCoroutine = null;
            _hintActive = false;
            // Restore any hint-highlighted source cells
            RestoreSourceColors();
        }

        _lastTapTime = Time.realtimeSinceStartup;

        bool wasOn = _playerGrid[row, col];
        _playerGrid[row, col] = !wasOn;

        _playerCellImages[row, col].color = _playerGrid[row, col] ? _playerColor : emptyColor;
        StartCoroutine(CellTapAnimation(_playerCellImages[row, col].rectTransform));

        // Record analytics
        bool shouldBeOn = _sourcePattern[row, col];

        if (!wasOn && _playerGrid[row, col])
        {
            if (shouldBeOn)
                _stats.RecordCorrect("fill", $"{row},{col}");
            else
                _stats.RecordMistake("fill_wrong", $"{row},{col}");
        }
        else if (wasOn && !_playerGrid[row, col])
        {
            if (shouldBeOn)
            {
                _undoCount++;
                _stats.RecordMistake("undo_correct", $"{row},{col}");
            }
            else
            {
                _stats.RecordCorrect("undo_wrong", $"{row},{col}");
            }
        }

        UpdateScoreText();

        // Check for auto-win
        if (CheckPatternMatch())
            StartCoroutine(OnPatternComplete());
    }

    private void UpdateScoreText()
    {
        if (scoreText == null) return;

        int matching = 0;
        for (int r = 0; r < _gridSize; r++)
            for (int c = 0; c < _gridSize; c++)
                if (_playerGrid[r, c] && _sourcePattern[r, c])
                    matching++;

        HebrewText.SetText(scoreText, $"{matching}/{_filledTarget}");
    }

    // ── AUTO-WIN CHECK ────────────────────────────────────────────

    /// <summary>Returns true if player grid exactly matches source pattern.</summary>
    private bool CheckPatternMatch()
    {
        for (int r = 0; r < _gridSize; r++)
            for (int c = 0; c < _gridSize; c++)
                if (_playerGrid[r, c] != _sourcePattern[r, c])
                    return false;
        return true;
    }

    private IEnumerator OnPatternComplete()
    {
        _roundActive = false;
        _celebrating = true;

        // Record metrics
        int correctFilled = 0, wrongFilled = 0;
        for (int r = 0; r < _gridSize; r++)
            for (int c = 0; c < _gridSize; c++)
            {
                if (_playerGrid[r, c] && _sourcePattern[r, c]) correctFilled++;
                if (_playerGrid[r, c] && !_sourcePattern[r, c]) wrongFilled++;
            }

        _stats.SetCustom("correctFilled", correctFilled);
        _stats.SetCustom("wrongFilled", wrongFilled);
        _stats.SetCustom("missedCells", 0);
        _stats.SetCustom("undoCorrectCount", _undoCount);
        _stats.RecordRoundComplete();

        // Flash all player cells green briefly
        yield return StartCoroutine(WinFlash());

        // Celebration
        ConfettiController.Instance?.Play();

        yield return new WaitForSeconds(2f);

        // Next round or let journey navigate
        if (!GameCompletionBridge.WillJourneyNavigate)
        {
            yield return new WaitForSeconds(0.5f);
            LoadRound();
        }
    }

    private IEnumerator WinFlash()
    {
        // Quick green flash on all filled cells
        for (int r = 0; r < _gridSize; r++)
            for (int c = 0; c < _gridSize; c++)
                if (_playerGrid[r, c])
                    _playerCellImages[r, c].color = correctFeedbackColor;

        yield return new WaitForSeconds(0.5f);

        // Restore player color
        for (int r = 0; r < _gridSize; r++)
            for (int c = 0; c < _gridSize; c++)
                if (_playerGrid[r, c])
                    _playerCellImages[r, c].color = _playerColor;
    }

    // ── INACTIVITY HINT ───────────────────────────────────────────

    private IEnumerator ShowHint()
    {
        _hintActive = true;
        _stats.RecordHint();

        // Find a cell that should be ON but isn't (or is ON but shouldn't be)
        // Prioritize: unfilled correct cells
        var candidates = new List<(int r, int c)>();
        for (int r = 0; r < _gridSize; r++)
            for (int c = 0; c < _gridSize; c++)
                if (_sourcePattern[r, c] && !_playerGrid[r, c])
                    candidates.Add((r, c));

        if (candidates.Count == 0)
        {
            // All correct cells are filled — hint on a wrongly filled cell
            for (int r = 0; r < _gridSize; r++)
                for (int c = 0; c < _gridSize; c++)
                    if (!_sourcePattern[r, c] && _playerGrid[r, c])
                        candidates.Add((r, c));
        }

        if (candidates.Count == 0)
        {
            _hintActive = false;
            yield break;
        }

        var target = candidates[Random.Range(0, candidates.Count)];
        var sourceImg = _sourceCellImages[target.r, target.c];
        Color origColor = sourceImg.color;

        // Pulse the source cell 3 times
        for (int pulse = 0; pulse < 3; pulse++)
        {
            if (!_roundActive) break;

            float duration = 0.4f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (!_roundActive) break;
                elapsed += Time.deltaTime;
                float t = Mathf.Sin(elapsed / duration * Mathf.PI);
                sourceImg.color = Color.Lerp(origColor, hintColor, t);
                yield return null;
            }
            sourceImg.color = origColor;

            if (pulse < 2)
                yield return new WaitForSeconds(0.15f);
        }

        sourceImg.color = origColor;
        _hintActive = false;
        _lastTapTime = Time.realtimeSinceStartup; // reset timer after hint
    }

    private void RestoreSourceColors()
    {
        if (_sourceCellImages == null) return;
        for (int r = 0; r < _gridSize; r++)
            for (int c = 0; c < _gridSize; c++)
                if (_sourceCellImages[r, c] != null)
                    _sourceCellImages[r, c].color = _sourcePattern[r, c] ? _playerColor : emptyColor;
    }

    // ── ANIMATIONS ────────────────────────────────────────────────

    private IEnumerator AnimateGridsIn()
    {
        for (int r = 0; r < _gridSize; r++)
        {
            for (int c = 0; c < _gridSize; c++)
            {
                if (_sourceCellImages[r, c] != null)
                    StartCoroutine(PopInCell(_sourceCellImages[r, c].rectTransform, (r * _gridSize + c) * 0.02f));
                if (_playerCellImages[r, c] != null)
                    StartCoroutine(PopInCell(_playerCellImages[r, c].rectTransform, (r * _gridSize + c) * 0.02f + 0.15f));
            }
        }
        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator PopInCell(RectTransform rt, float delay)
    {
        rt.localScale = Vector3.zero;
        yield return new WaitForSeconds(delay);

        float duration = 0.25f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.1f;
            if (t >= 1f) scale = 1f;
            rt.localScale = Vector3.one * (t * scale);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private IEnumerator CellTapAnimation(RectTransform rt)
    {
        float duration = 0.15f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.15f;
            rt.localScale = Vector3.one * scale;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    // ── NAVIGATION ────────────────────────────────────────────────

    public void OnHomePressed()
    {
        if (_roundActive && _stats != null)
            _stats.Abandon();
        NavigationManager.GoToMainMenu();
    }
}
