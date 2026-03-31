using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Connect and Match game ("חבר וצייר").
/// Left side: reference pattern. Right side: interactive dot grid.
/// Child connects dots to recreate the shape. Uses profile color for dots/lines.
/// </summary>
public class ConnectMatchController : BaseMiniGame
{
    [Header("Layout")]
    public RectTransform referenceArea; // left: shows target pattern
    public RectTransform playArea;      // right: interactive dot grid
    public RectTransform refLineContainer;
    public RectTransform playLineContainer;

    [Header("UI")]
    public TextMeshProUGUI titleText;

    [Header("Sprites")]
    public Sprite dotSprite;   // Circle.png
    public Sprite cellSprite;  // RoundedRect.png

    // Colors — loaded from profile
    private Color _playerColor = HexColor("#90CAF9"); // default blue
    private Color _playerColorDim;
    private Color _playerColorGlow;

    // State
    private int _mistakesThisRound;
    private int _hintsUsed;
    private float _lastInteractionTime;
    private float _roundStartTime;

    // Level
    private ConnectMatchLevelData.LevelConfig _level;
    private int _expectedStep; // next path index the player should connect

    // Grid dots
    private Dictionary<Vector2Int, GameObject> _refDots = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, GameObject> _playDots = new Dictionary<Vector2Int, GameObject>();
    private List<GameObject> _allObjects = new List<GameObject>();
    private List<GameObject> _playLines = new List<GameObject>();
    private List<Vector2Int> _playerPath = new List<Vector2Int>();

    // Direction: player can start from either end
    private bool _directionChosen;
    private Vector2Int[] _effectivePath; // targetPath or reversed copy

    // Live drag line
    private GameObject _liveLineGO;
    private RectTransform _liveLineRT;
    private Image _liveLineImg;
    private bool _isDragging;

    private Coroutine _inactivityCoroutine;
    private float _dotSize;

    // ── Line rendering constants ──
    private const float LineWidth = 8f;
    private const float GlowWidth = 24f;
    private const float DotSizeBase = 50f;
    private const float HitRadiusMultiplier = 1.4f;

    // ── BASE MINI GAME HOOKS ──

    protected override void Start()
    {
        // Load player color from profile
        LoadPlayerColor();
        base.Start();
    }

    protected override void OnGameInit()
    {
        totalRounds = 1;
        isEndless = true;
        playWinSound = true;
        playConfettiOnRoundWin = true;
        delayBeforeNextRound = 1.5f;
    }

    protected override string GetFallbackGameId() => "connectmatch";

    protected override void OnRoundSetup()
    {
        _mistakesThisRound = 0;
        _hintsUsed = 0;
        _expectedStep = 0;
        _playerPath.Clear();
        _directionChosen = false;
        _effectivePath = null;

        _level = ConnectMatchLevelData.Generate(Difficulty);

        // Build reference (left)
        BuildGrid(referenceArea, refLineContainer, _level, true);

        // Build interactive grid (right)
        BuildGrid(playArea, null, _level, false);

        // Create live drag line
        CreateLiveLine();

        _roundStartTime = Time.time;
        _lastInteractionTime = Time.time;
        _inactivityCoroutine = StartCoroutine(InactivityMonitor());

        Stats.SetCustom("gridSize", (float)(_level.gridCols * _level.gridRows));
        Stats.SetCustom("pathLength", (float)_level.targetPath.Length);

        // Position tutorial hand: drag from first to second dot on the path
        PositionTutorialHand();

        Debug.Log($"[ConnectMatch] Round {CurrentRound + 1}: {_level.gridCols}x{_level.gridRows}, path={_level.targetPath.Length}, difficulty={Difficulty}");
    }

    private void PositionTutorialHand()
    {
        if (TutorialHand == null) return;
        if (_level.targetPath == null || _level.targetPath.Length < 2) return;

        var startCoord = _level.targetPath[0];
        var nextCoord = _level.targetPath[1];

        if (!_playDots.ContainsKey(startCoord) || !_playDots.ContainsKey(nextCoord)) return;

        var fromRT = _playDots[startCoord].GetComponent<RectTransform>();
        var toRT = _playDots[nextCoord].GetComponent<RectTransform>();

        Vector2 fromLocal = TutorialHand.GetLocalCenter(fromRT);
        Vector2 toLocal = TutorialHand.GetLocalCenter(toRT);

        TutorialHand.SetMovePath(fromLocal, toLocal, 1.0f);
    }

    protected override void OnRoundCleanup()
    {
        if (_inactivityCoroutine != null)
        {
            StopCoroutine(_inactivityCoroutine);
            _inactivityCoroutine = null;
        }
        foreach (var go in _allObjects)
            if (go != null) Destroy(go);
        _allObjects.Clear();
        foreach (var go in _playLines)
            if (go != null) Destroy(go);
        _playLines.Clear();
        _refDots.Clear();
        _playDots.Clear();

        if (_liveLineGO != null) Destroy(_liveLineGO);
        _liveLineGO = null;
        _isDragging = false;
    }

    protected override void OnBeforeComplete()
    {
        Stats.SetCustom("mistakes", (float)_mistakesThisRound);
        Stats.SetCustom("hintsUsed", (float)_hintsUsed);
        Stats.SetCustom("responseTime", Time.time - _roundStartTime);
    }

    protected override IEnumerator OnAfterComplete()
    {
        // Wave pulse all connected dots
        for (int i = 0; i < _playerPath.Count; i++)
        {
            if (_playDots.ContainsKey(_playerPath[i]))
            {
                var rt = _playDots[_playerPath[i]].GetComponent<RectTransform>();
                StartCoroutine(CelebrateBounce(rt, i * 0.08f));
            }
        }
        yield return new WaitForSeconds(1.0f);
    }

    protected override void OnGameplayUpdate()
    {
        if (playArea == null) return;

        bool pressed = Input.GetMouseButton(0);
        bool justPressed = Input.GetMouseButtonDown(0);
        bool justReleased = Input.GetMouseButtonUp(0);

        if (justPressed)
        {
            DismissTutorial();
            _isDragging = true;
        }

        if (pressed && _isDragging)
        {
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                playArea, Input.mousePosition, null, out localPoint))
            {
                // Update live line from last connected dot
                if (_playerPath.Count > 0 && _playDots.ContainsKey(_playerPath[_playerPath.Count - 1]))
                {
                    var fromPos = _playDots[_playerPath[_playerPath.Count - 1]]
                        .GetComponent<RectTransform>().anchoredPosition;
                    _liveLineGO.SetActive(true);
                    _liveLineImg.color = new Color(_playerColor.r, _playerColor.g, _playerColor.b, 0.5f);
                    PositionLine(_liveLineRT, fromPos, localPoint, LineWidth * 0.8f);
                }

                // Select dot only when finger drags over it
                var hitDot = FindNearestDot(localPoint);
                if (hitDot.HasValue)
                    TrySelectDot(hitDot.Value);
            }
        }

        if (justReleased)
        {
            _isDragging = false;
            if (_liveLineGO != null) _liveLineGO.SetActive(false);
        }
    }

    private void LoadPlayerColor()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile != null && !string.IsNullOrEmpty(profile.avatarColorHex))
        {
            if (ColorUtility.TryParseHtmlString(profile.avatarColorHex, out Color parsed))
                _playerColor = parsed;
        }
        _playerColorDim = new Color(_playerColor.r, _playerColor.g, _playerColor.b, 0.4f);
        _playerColorGlow = new Color(_playerColor.r, _playerColor.g, _playerColor.b, 0.2f);
    }

    // ── GRID BUILDING ──

    private void BuildGrid(RectTransform area, RectTransform lineContainer,
        ConnectMatchLevelData.LevelConfig level, bool isReference)
    {
        int cols = level.gridCols;
        int rows = level.gridRows;

        float areaW = area.rect.width;
        float areaH = area.rect.height;
        float spacing = Mathf.Min(areaW / (cols + 1), areaH / (rows + 1));
        _dotSize = Mathf.Min(DotSizeBase, spacing * 0.5f);

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                float px = -areaW / 2f + (x + 1) * areaW / (cols + 1);
                float py = areaH / 2f - (y + 1) * areaH / (rows + 1);
                var coord = new Vector2Int(x, y);

                bool onPath = System.Array.IndexOf(level.targetPath, coord) >= 0;
                var dotGO = CreateDot(area, px, py, coord, isReference, onPath);
                _allObjects.Add(dotGO);

                if (isReference)
                    _refDots[coord] = dotGO;
                else
                    _playDots[coord] = dotGO;
            }
        }

        // Draw reference lines
        if (isReference && lineContainer != null)
        {
            for (int i = 0; i < level.targetPath.Length - 1; i++)
            {
                var from = level.targetPath[i];
                var to = level.targetPath[i + 1];
                if (_refDots.ContainsKey(from) && _refDots.ContainsKey(to))
                {
                    var fromPos = _refDots[from].GetComponent<RectTransform>().anchoredPosition;
                    var toPos = _refDots[to].GetComponent<RectTransform>().anchoredPosition;
                    var lineGO = DrawLine(lineContainer, fromPos, toPos, _playerColor, LineWidth);
                    _allObjects.Add(lineGO);
                }
            }
        }
    }

    private GameObject CreateDot(RectTransform parent, float x, float y,
        Vector2Int coord, bool isReference, bool onPath)
    {
        var go = new GameObject($"Dot_{coord.x}_{coord.y}");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(_dotSize, _dotSize);
        rt.anchoredPosition = new Vector2(x, y);

        var img = go.AddComponent<Image>();
        if (dotSprite != null) img.sprite = dotSprite;
        img.raycastTarget = !isReference;

        if (isReference)
        {
            // Reference dots: colored if on path, dim gray otherwise
            img.color = onPath ? _playerColor : HexColor("#D0D0D0");
            // Make path dots slightly larger
            if (onPath) rt.sizeDelta = new Vector2(_dotSize * 1.15f, _dotSize * 1.15f);
        }
        else
        {
            // Play dots: all start dim, become bold when connected
            img.color = _playerColorDim;
        }

        return go;
    }

    // ── LINE DRAWING ──

    private GameObject DrawLine(RectTransform parent, Vector2 from, Vector2 to, Color color, float width)
    {
        var lineGO = new GameObject("Line");
        lineGO.transform.SetParent(parent, false);
        var rt = lineGO.AddComponent<RectTransform>();
        var img = lineGO.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        PositionLine(rt, from, to, width);
        return lineGO;
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

    private void CreateLiveLine()
    {
        _liveLineGO = new GameObject("LiveLine");
        _liveLineGO.transform.SetParent(playArea, false);
        _liveLineRT = _liveLineGO.AddComponent<RectTransform>();
        _liveLineImg = _liveLineGO.AddComponent<Image>();
        _liveLineImg.color = _playerColorDim;
        _liveLineImg.raycastTarget = false;
        _liveLineGO.SetActive(false);
    }

    // ── INPUT ──

    private Vector2Int? FindNearestDot(Vector2 localPoint)
    {
        float hitRadius = _dotSize * HitRadiusMultiplier;
        float bestDist = float.MaxValue;
        Vector2Int? best = null;

        foreach (var kvp in _playDots)
        {
            var dotPos = kvp.Value.GetComponent<RectTransform>().anchoredPosition;
            float dist = Vector2.Distance(localPoint, dotPos);
            if (dist < hitRadius && dist < bestDist)
            {
                bestDist = dist;
                best = kvp.Key;
            }
        }
        return best;
    }

    // ── DOT SELECTION / VALIDATION ──

    private void TrySelectDot(Vector2Int coord)
    {
        _lastInteractionTime = Time.time;

        // Already connected this dot?
        if (_playerPath.Contains(coord)) return;

        // First dot: must be either end of the path (player chooses direction)
        if (_playerPath.Count == 0)
        {
            var pathStart = _level.targetPath[0];
            var pathEnd = _level.targetPath[_level.targetPath.Length - 1];

            if (coord == pathStart)
            {
                // Forward direction
                _directionChosen = true;
                _effectivePath = _level.targetPath;
                AcceptDot(coord);
                _isDragging = true;
            }
            else if (coord == pathEnd)
            {
                // Reverse direction
                _directionChosen = true;
                _effectivePath = new Vector2Int[_level.targetPath.Length];
                for (int i = 0; i < _level.targetPath.Length; i++)
                    _effectivePath[i] = _level.targetPath[_level.targetPath.Length - 1 - i];
                AcceptDot(coord);
                _isDragging = true;
            }
            else
            {
                _mistakesThisRound++;
                RecordMistake("wrong_start", $"{coord.x},{coord.y}");
                if (_playDots.ContainsKey(coord))
                    PlayWrongEffect(_playDots[coord].GetComponent<RectTransform>());
                ShakeDot(coord);
            }
            return;
        }

        // Subsequent dots: must be adjacent to last dot
        var lastDot = _playerPath[_playerPath.Count - 1];
        if (!IsAdjacent(lastDot, coord, _level.allowDiagonals))
        {
            // Not adjacent — ignore silently (forgiving for kids)
            return;
        }

        // Check if this is the expected next dot on the effective path
        int nextIdx = _playerPath.Count;
        if (nextIdx < _effectivePath.Length && coord == _effectivePath[nextIdx])
        {
            AcceptDot(coord);

            // Check completion
            if (_playerPath.Count >= _effectivePath.Length)
            {
                _isDragging = false;
                if (_liveLineGO != null) _liveLineGO.SetActive(false);
                RecordCorrect("path_complete", isLast: true);

                CompleteRound();
            }
        }
        else
        {
            // Wrong dot — soft feedback only, no immediate hint
            _mistakesThisRound++;
            RecordMistake("wrong_dot", $"{coord.x},{coord.y}");
            if (_playDots.ContainsKey(coord))
                PlayWrongEffect(_playDots[coord].GetComponent<RectTransform>());
            ShakeDot(coord);
        }
    }

    private void AcceptDot(Vector2Int coord)
    {
        _playerPath.Add(coord);
        if (_playDots.ContainsKey(coord))
            PlayCorrectEffect(_playDots[coord].GetComponent<RectTransform>());
        RecordCorrect("dot_connect", $"{coord.x},{coord.y}");

        // Brighten this dot
        if (_playDots.ContainsKey(coord))
        {
            var img = _playDots[coord].GetComponent<Image>();
            if (img != null) img.color = _playerColor;
            var rt = _playDots[coord].GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(_dotSize * 1.2f, _dotSize * 1.2f);
        }

        // Draw line from previous dot
        if (_playerPath.Count >= 2)
        {
            var prev = _playerPath[_playerPath.Count - 2];
            if (_playDots.ContainsKey(prev) && _playDots.ContainsKey(coord))
            {
                var fromPos = _playDots[prev].GetComponent<RectTransform>().anchoredPosition;
                var toPos = _playDots[coord].GetComponent<RectTransform>().anchoredPosition;

                // Glow
                var glowLine = DrawLine(playArea, fromPos, toPos, _playerColorGlow, GlowWidth);
                glowLine.transform.SetAsFirstSibling();
                _playLines.Add(glowLine);

                // Main line
                var mainLine = DrawLine(playArea, fromPos, toPos, _playerColor, LineWidth);
                _playLines.Add(mainLine);
            }
        }

        // Next dot hint only after inactivity, not immediately
    }

    private bool IsAdjacent(Vector2Int a, Vector2Int b, bool diagonals)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        if (diagonals)
            return dx <= 1 && dy <= 1 && (dx + dy > 0);
        return (dx + dy) == 1;
    }

    // ── VISUAL FEEDBACK ──

    private void HighlightDot(Vector2Int coord, bool isStart)
    {
        if (!_playDots.ContainsKey(coord)) return;
        var dot = _playDots[coord];
        StartCoroutine(PulseDot(dot, isStart));
    }

    private IEnumerator PulseDot(GameObject dot, bool strong)
    {
        var rt = dot.GetComponent<RectTransform>();
        var img = dot.GetComponent<Image>();
        float baseSize = _dotSize;
        Color baseColor = img.color;
        Color pulseColor = strong ? _playerColor : new Color(_playerColor.r, _playerColor.g, _playerColor.b, 0.7f);

        for (int i = 0; i < (strong ? 4 : 2); i++)
        {
            img.color = pulseColor;
            float dur = 0.3f;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                float p = t / dur;
                float scale = 1f + 0.2f * Mathf.Sin(p * Mathf.PI);
                rt.sizeDelta = new Vector2(baseSize * scale, baseSize * scale);
                yield return null;
            }
            rt.sizeDelta = new Vector2(baseSize, baseSize);
            img.color = baseColor;
            yield return new WaitForSeconds(0.1f);
        }
    }

    private void ShakeDot(Vector2Int coord)
    {
        if (!_playDots.ContainsKey(coord)) return;
        StartCoroutine(DoShakeDot(_playDots[coord]));
    }

    private IEnumerator DoShakeDot(GameObject dot)
    {
        var rt = dot.GetComponent<RectTransform>();
        var img = dot.GetComponent<Image>();
        Color orig = img.color;
        img.color = HexColor("#FFCDD2");

        Vector2 origPos = rt.anchoredPosition;
        float dur = 0.25f;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float p = t / dur;
            float offset = Mathf.Sin(p * Mathf.PI * 6f) * 8f * (1f - p);
            rt.anchoredPosition = origPos + new Vector2(offset, 0);
            yield return null;
        }
        rt.anchoredPosition = origPos;
        img.color = orig;
    }

    private void HintNextDot()
    {
        if (_effectivePath == null)
        {
            // Not started yet — show ghost drag from first to second dot on the target path
            var startCoord = _level.targetPath[0];
            var nextCoord = _level.targetPath.Length > 1 ? _level.targetPath[1] : startCoord;
            if (_playDots.ContainsKey(startCoord) && _playDots.ContainsKey(nextCoord))
            {
                var fromPos = _playDots[startCoord].GetComponent<RectTransform>().anchoredPosition;
                var toPos = _playDots[nextCoord].GetComponent<RectTransform>().anchoredPosition;
                StartCoroutine(AnimateGhostDrag(startCoord, fromPos, toPos));
            }
            return;
        }
        int nextIdx = _playerPath.Count;
        if (nextIdx < _effectivePath.Length)
        {
            // Show ghost drag from last connected dot to the next expected dot
            var prevCoord = _playerPath[_playerPath.Count - 1];
            var nextCoord = _effectivePath[nextIdx];
            if (_playDots.ContainsKey(prevCoord) && _playDots.ContainsKey(nextCoord))
            {
                var fromPos = _playDots[prevCoord].GetComponent<RectTransform>().anchoredPosition;
                var toPos = _playDots[nextCoord].GetComponent<RectTransform>().anchoredPosition;
                StartCoroutine(AnimateGhostDrag(nextCoord, fromPos, toPos));
            }
        }
    }

    /// <summary>
    /// Animates a ghost "finger drag" line from one dot to another.
    /// </summary>
    private IEnumerator AnimateGhostDrag(Vector2Int targetDot, Vector2 from, Vector2 to)
    {
        // Create a temporary ghost line
        var ghostGO = new GameObject("GhostDrag");
        ghostGO.transform.SetParent(playArea, false);
        var ghostRT = ghostGO.AddComponent<RectTransform>();
        var ghostImg = ghostGO.AddComponent<Image>();
        Color ghostColor = new Color(_playerColor.r, _playerColor.g, _playerColor.b, 0.4f);
        ghostImg.color = ghostColor;
        ghostImg.raycastTarget = false;

        // Also pulse the target dot
        if (_playDots.ContainsKey(targetDot))
        {
            var dotImg = _playDots[targetDot].GetComponent<Image>();
            if (dotImg != null)
                dotImg.color = new Color(_playerColor.r, _playerColor.g, _playerColor.b, 0.7f);
        }

        // Animate: line grows from 'from' toward 'to' over 0.8s
        float dur = 0.8f;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float p = t / dur;
            float ease = p * (2f - p); // ease-out quadratic
            Vector2 current = Vector2.Lerp(from, to, ease);
            PositionLine(ghostRT, from, current, LineWidth * 0.7f);
            ghostImg.color = new Color(ghostColor.r, ghostColor.g, ghostColor.b, 0.4f * (1f - p * 0.3f));
            yield return null;
        }

        // Hold briefly
        PositionLine(ghostRT, from, to, LineWidth * 0.7f);
        yield return new WaitForSeconds(0.3f);

        // Fade out
        float fadeDur = 0.4f;
        for (float t = 0; t < fadeDur; t += Time.deltaTime)
        {
            float p = t / fadeDur;
            ghostImg.color = new Color(ghostColor.r, ghostColor.g, ghostColor.b, 0.4f * (1f - p));
            yield return null;
        }

        Destroy(ghostGO);

        // Reset target dot color
        if (_playDots.ContainsKey(targetDot))
        {
            var dotImg = _playDots[targetDot].GetComponent<Image>();
            if (dotImg != null)
                dotImg.color = _playerColorDim;
        }
    }

    // ── CELEBRATION ──

    private IEnumerator CelebrateBounce(RectTransform rt, float delay)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);
        float dur = 0.2f;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float p = t / dur;
            float s = 1f + 0.2f * Mathf.Sin(p * Mathf.PI);
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    // ── INACTIVITY ──

    private IEnumerator InactivityMonitor()
    {
        while (!IsInputLocked)
        {
            yield return new WaitForSeconds(1f);
            if (!IsInputLocked && Time.time - _lastInteractionTime >= 5f)
            {
                _hintsUsed++;
                RecordHint();
                HintNextDot();
                _lastInteractionTime = Time.time;
            }
        }
    }

    // ── NAVIGATION ──

    public void OnHomePressed()
    {
        ExitGame();
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
