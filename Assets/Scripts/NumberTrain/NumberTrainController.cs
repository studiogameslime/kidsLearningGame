using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Number Train game ("רכבת המספרים").
/// Train of 5-7 wagons enters from the left. Some wagons are empty.
/// Player drags numbers from the bottom into the correct empty wagons.
/// On success the train exits to the right.
/// </summary>
public class NumberTrainController : MonoBehaviour
{
    [Header("Layout")]
    public RectTransform trainArea;   // horizontal strip for the train
    public RectTransform optionsArea; // bottom strip for draggable numbers

    [Header("UI")]
    public TextMeshProUGUI titleText;

    [Header("Sprites")]
    public Sprite cellSprite;    // RoundedRect
    public Sprite circleSprite;  // Circle

    [Header("Settings")]
    public int totalRounds = 5;

    // Colors
    private static readonly Color WagonBg       = HexColor("#BBDEFB");
    private static readonly Color WagonBorder    = HexColor("#42A5F5");
    private static readonly Color EmptyWagonBg   = HexColor("#E3F2FD");
    private static readonly Color EmptyBorder    = HexColor("#90CAF9");
    private static readonly Color FilledText     = HexColor("#1565C0");
    private static readonly Color EmptyText      = HexColor("#BDBDBD");
    private static readonly Color OptionBg       = HexColor("#FFF9C4");
    private static readonly Color OptionBorder   = HexColor("#FFC107");
    private static readonly Color OptionText     = HexColor("#F57F17");
    private static readonly Color CorrectBg      = HexColor("#C8E6C9");
    private static readonly Color CorrectBorder  = HexColor("#66BB6A");
    private static readonly Color ConnectorColor = HexColor("#78909C");

    // State
    private GameStatsCollector _stats;
    private int _difficulty = 1;
    private int _currentRound;
    private bool _roundActive;
    private int _mistakesThisRound;
    private int _hintsUsed;
    private float _lastInteractionTime;
    private int _placedCount;

    // Level data
    private int _startNumber;
    private int _wagonCount;
    private int[] _sequence;        // full sequence values
    private bool[] _isMissing;      // which wagons are empty
    private int[] _missingValues;   // the values that need to be placed
    private int _totalMissing;

    // UI objects
    private List<GameObject> _wagonObjects = new List<GameObject>();
    private List<GameObject> _optionObjects = new List<GameObject>();
    private List<GameObject> _connectorObjects = new List<GameObject>();
    private Dictionary<int, RectTransform> _emptySlots = new Dictionary<int, RectTransform>(); // wagonIndex → RT
    private Dictionary<int, TextMeshProUGUI> _emptyTexts = new Dictionary<int, TextMeshProUGUI>();
    private RectTransform _trainGroupRT;

    // Drag state
    private GameObject _draggedOption;
    private RectTransform _draggedRT;
    private int _draggedValue;
    private Vector2 _dragOffset;
    private Vector2 _dragOriginalPos;
    private int _dragOriginalSiblingIndex;

    private Coroutine _inactivityCoroutine;

    private void Start()
    {
        _currentRound = 0;
        LoadRound();
    }

    // ── ROUND LIFECYCLE ──

    private void LoadRound()
    {
        ClearRound();
        _roundActive = false; // becomes true after train entrance
        _mistakesThisRound = 0;
        _hintsUsed = 0;
        _placedCount = 0;

        string gameId = GameContext.CurrentGame != null ? GameContext.CurrentGame.id : "numbertrain";
        _stats = new GameStatsCollector(gameId);
        if (GameCompletionBridge.Instance != null)
            GameCompletionBridge.Instance.ActiveCollector = _stats;
        _stats.SetTotalRoundsPlanned(1);

        _difficulty = GameDifficultyConfig.GetLevel(gameId);

        GenerateLevel();
        BuildTrain();
        BuildOptions();

        _stats.SetCustom("difficulty", (float)_difficulty);
        _stats.SetCustom("wagonCount", (float)_wagonCount);
        _stats.SetCustom("missingCount", (float)_totalMissing);
        _stats.SetCustom("startNumber", (float)_startNumber);

        // Animate train entrance
        StartCoroutine(TrainEntrance());
    }

    private void ClearRound()
    {
        if (_inactivityCoroutine != null)
        {
            StopCoroutine(_inactivityCoroutine);
            _inactivityCoroutine = null;
        }
        StopAllCoroutines();

        foreach (var go in _wagonObjects) if (go != null) Destroy(go);
        foreach (var go in _optionObjects) if (go != null) Destroy(go);
        foreach (var go in _connectorObjects) if (go != null) Destroy(go);
        if (_trainGroupRT != null && _trainGroupRT.gameObject != null)
            Destroy(_trainGroupRT.gameObject);
        _wagonObjects.Clear();
        _optionObjects.Clear();
        _connectorObjects.Clear();
        _emptySlots.Clear();
        _emptyTexts.Clear();
        _draggedOption = null;
    }

    // ── LEVEL GENERATION ──

    private void GenerateLevel()
    {
        // Wagon count based on difficulty
        if (_difficulty <= 3)      _wagonCount = 5;
        else if (_difficulty <= 6) _wagonCount = 6;
        else                       _wagonCount = 7;

        // Missing count based on difficulty
        if (_difficulty <= 2)      _totalMissing = 1;
        else if (_difficulty <= 5) _totalMissing = 2;
        else                       _totalMissing = 3;

        // Start number
        int maxStart = Mathf.Max(1, 15 - _wagonCount);
        _startNumber = Random.Range(1, maxStart + 1);

        // Build sequence
        _sequence = new int[_wagonCount];
        _isMissing = new bool[_wagonCount];
        for (int i = 0; i < _wagonCount; i++)
            _sequence[i] = _startNumber + i;

        // Pick which wagons to leave empty (not first or last for easier games)
        var candidates = new List<int>();
        for (int i = 1; i < _wagonCount - 1; i++)
            candidates.Add(i);
        // Shuffle
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = candidates[i]; candidates[i] = candidates[j]; candidates[j] = tmp;
        }

        _missingValues = new int[_totalMissing];
        for (int i = 0; i < _totalMissing; i++)
        {
            int idx = candidates[i];
            _isMissing[idx] = true;
            _missingValues[i] = _sequence[idx];
        }

        Debug.Log($"[NumberTrain] Round {_currentRound + 1}: sequence {_startNumber}-{_startNumber + _wagonCount - 1}, missing {_totalMissing}, difficulty {_difficulty}");
    }

    // ── TRAIN BUILDING ──

    private void BuildTrain()
    {
        if (trainArea == null) return;

        // Create a train group container (so we can slide the whole train)
        var trainGroupGO = new GameObject("TrainGroup");
        trainGroupGO.transform.SetParent(trainArea, false);
        _trainGroupRT = trainGroupGO.AddComponent<RectTransform>();
        _trainGroupRT.anchorMin = Vector2.zero;
        _trainGroupRT.anchorMax = Vector2.one;
        _trainGroupRT.offsetMin = Vector2.zero;
        _trainGroupRT.offsetMax = Vector2.zero;

        float areaW = trainArea.rect.width;
        float areaH = trainArea.rect.height;

        float wagonW = Mathf.Min(220f, (areaW - (_wagonCount - 1) * 20f - 20f) / _wagonCount);
        float wagonH = wagonW * 0.85f;
        float connW = 20f;
        float totalW = _wagonCount * wagonW + (_wagonCount - 1) * connW;
        float startX = -totalW / 2f + wagonW / 2f;

        for (int i = 0; i < _wagonCount; i++)
        {
            float x = startX + i * (wagonW + connW);

            // Connector line (between wagons, except before first)
            if (i > 0)
            {
                var connGO = new GameObject("Connector");
                connGO.transform.SetParent(_trainGroupRT, false);
                var connRT = connGO.AddComponent<RectTransform>();
                connRT.sizeDelta = new Vector2(connW + 6f, 8f);
                connRT.anchoredPosition = new Vector2(x - (wagonW + connW) / 2f, 0f);
                var connImg = connGO.AddComponent<Image>();
                connImg.color = ConnectorColor;
                connImg.raycastTarget = false;
                _connectorObjects.Add(connGO);
            }

            // Wagon
            bool empty = _isMissing[i];
            var wagonGO = CreateWagon(_trainGroupRT, x, 0, wagonW, wagonH, i, empty);
            _wagonObjects.Add(wagonGO);
        }
    }

    private GameObject CreateWagon(RectTransform parent, float x, float y,
        float w, float h, int wagonIndex, bool isEmpty)
    {
        var go = new GameObject($"Wagon_{wagonIndex}");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);

        // Border
        var borderGO = new GameObject("Border");
        borderGO.transform.SetParent(go.transform, false);
        var brt = borderGO.AddComponent<RectTransform>();
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(-4, -4);
        brt.offsetMax = new Vector2(4, 4);
        var bimg = borderGO.AddComponent<Image>();
        if (cellSprite != null) { bimg.sprite = cellSprite; bimg.type = Image.Type.Sliced; }
        bimg.color = isEmpty ? EmptyBorder : WagonBorder;
        bimg.raycastTarget = false;

        // Background
        var bgImg = go.AddComponent<Image>();
        if (cellSprite != null) { bgImg.sprite = cellSprite; bgImg.type = Image.Type.Sliced; }
        bgImg.color = isEmpty ? EmptyWagonBg : WagonBg;
        bgImg.raycastTarget = false;

        // Wheels (two small circles at bottom)
        float wheelSize = Mathf.Min(20f, w * 0.14f);
        CreateWheel(go.transform, -w * 0.25f, -h / 2f - wheelSize * 0.3f, wheelSize);
        CreateWheel(go.transform, w * 0.25f, -h / 2f - wheelSize * 0.3f, wheelSize);

        // Number text
        var textGO = new GameObject("Number");
        textGO.transform.SetParent(go.transform, false);
        var trt = textGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = Mathf.Min(56f, h * 0.5f);
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        if (isEmpty)
        {
            tmp.text = "?";
            tmp.color = EmptyText;
            _emptySlots[wagonIndex] = rt;
            _emptyTexts[wagonIndex] = tmp;
        }
        else
        {
            tmp.text = _sequence[wagonIndex].ToString();
            tmp.color = FilledText;
        }

        return go;
    }

    private void CreateWheel(Transform parent, float x, float y, float size)
    {
        var go = new GameObject("Wheel");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = new Vector2(x, y);
        var img = go.AddComponent<Image>();
        if (circleSprite != null) img.sprite = circleSprite;
        img.color = HexColor("#546E7A");
        img.raycastTarget = false;
    }

    // ── OPTIONS BUILDING ──

    private void BuildOptions()
    {
        if (optionsArea == null) return;

        // Shuffle missing values for display
        var shuffled = new List<int>(_missingValues);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = shuffled[i]; shuffled[i] = shuffled[j]; shuffled[j] = tmp;
        }

        float areaW = optionsArea.rect.width;
        float areaH = optionsArea.rect.height;
        float optSize = Mathf.Min(160f, areaH * 0.90f);
        float spacing = 30f;
        float totalW = shuffled.Count * optSize + (shuffled.Count - 1) * spacing;
        float startX = -totalW / 2f + optSize / 2f;

        for (int i = 0; i < shuffled.Count; i++)
        {
            float x = startX + i * (optSize + spacing);
            var optGO = CreateOption(optionsArea, x, 0, optSize, shuffled[i]);
            _optionObjects.Add(optGO);
        }
    }

    private GameObject CreateOption(RectTransform parent, float x, float y,
        float size, int value)
    {
        var go = new GameObject($"Option_{value}");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = new Vector2(x, y);

        // Border
        var borderGO = new GameObject("Border");
        borderGO.transform.SetParent(go.transform, false);
        var brt = borderGO.AddComponent<RectTransform>();
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(-4, -4);
        brt.offsetMax = new Vector2(4, 4);
        var bimg = borderGO.AddComponent<Image>();
        if (cellSprite != null) { bimg.sprite = cellSprite; bimg.type = Image.Type.Sliced; }
        bimg.color = OptionBorder;
        bimg.raycastTarget = false;

        // Background
        var bgImg = go.AddComponent<Image>();
        if (cellSprite != null) { bgImg.sprite = cellSprite; bgImg.type = Image.Type.Sliced; }
        bgImg.color = OptionBg;
        bgImg.raycastTarget = true;

        // Number text
        var textGO = new GameObject("Number");
        textGO.transform.SetParent(go.transform, false);
        var trt = textGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = value.ToString();
        tmp.fontSize = Mathf.Min(52f, size * 0.5f);
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = OptionText;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        // Drag handler
        var handler = go.AddComponent<NumberTrainDragHandler>();
        handler.controller = this;
        handler.value = value;

        return go;
    }

    // ── DRAG AND DROP ──

    public void OnOptionDragBegin(GameObject option, int value, PointerEventData eventData)
    {
        if (!_roundActive) return;
        _lastInteractionTime = Time.time;

        _draggedOption = option;
        _draggedRT = option.GetComponent<RectTransform>();
        _draggedValue = value;
        _dragOriginalPos = _draggedRT.anchoredPosition;
        _dragOriginalSiblingIndex = option.transform.GetSiblingIndex();

        // Bring to front
        option.transform.SetAsLastSibling();
        _draggedRT.localScale = Vector3.one * 1.1f;
    }

    public void OnOptionDrag(PointerEventData eventData)
    {
        if (_draggedOption == null || !_roundActive) return;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _draggedRT.parent as RectTransform, eventData.position, eventData.pressEventCamera, out localPoint);
        _draggedRT.anchoredPosition = localPoint;

        // Highlight nearby empty wagon
        HighlightNearestEmptyWagon(eventData.position);
    }

    public void OnOptionDragEnd(PointerEventData eventData)
    {
        if (_draggedOption == null || !_roundActive) return;

        _draggedRT.localScale = Vector3.one;

        // Check if dropped on an empty wagon
        int targetWagon = FindEmptyWagonAtPosition(eventData.position);

        if (targetWagon >= 0 && _sequence[targetWagon] == _draggedValue)
        {
            // Correct placement!
            PlaceNumberInWagon(targetWagon, _draggedValue, _draggedOption);
        }
        else if (targetWagon >= 0)
        {
            // Wrong wagon — return to original
            _mistakesThisRound++;
            _stats.RecordMistake("wrong_wagon", $"{_draggedValue}→slot{targetWagon}");
            StartCoroutine(ReturnToOriginal(_draggedRT, _dragOriginalPos));
            StartCoroutine(ShakeWagon(targetWagon));
        }
        else
        {
            // Dropped nowhere — return silently
            StartCoroutine(ReturnToOriginal(_draggedRT, _dragOriginalPos));
        }

        // Reset highlights
        ResetWagonHighlights();
        _draggedOption = null;
    }

    private void PlaceNumberInWagon(int wagonIndex, int value, GameObject option)
    {
        _stats.RecordCorrect("number_placed", value.ToString());
        _placedCount++;

        // Update wagon text
        if (_emptyTexts.ContainsKey(wagonIndex))
        {
            _emptyTexts[wagonIndex].text = value.ToString();
            _emptyTexts[wagonIndex].color = FilledText;
        }

        // Update wagon colors to "correct"
        if (_wagonObjects[wagonIndex] != null)
        {
            var bg = _wagonObjects[wagonIndex].GetComponent<Image>();
            if (bg != null) bg.color = CorrectBg;
            var border = _wagonObjects[wagonIndex].transform.Find("Border");
            if (border != null)
            {
                var bimg = border.GetComponent<Image>();
                if (bimg != null) bimg.color = CorrectBorder;
            }
        }

        // Remove from empty slots
        _emptySlots.Remove(wagonIndex);
        _emptyTexts.Remove(wagonIndex);

        // Destroy the option
        Destroy(option);
        _optionObjects.Remove(option);

        // Bounce wagon
        StartCoroutine(BounceWagon(wagonIndex));

        // Check if all placed
        if (_placedCount >= _totalMissing)
        {
            _roundActive = false;
            _stats.SetCustom("mistakes", (float)_mistakesThisRound);
            _stats.SetCustom("hintsUsed", (float)_hintsUsed);
            StartCoroutine(OnRoundComplete());
        }
    }

    private int FindEmptyWagonAtPosition(Vector2 screenPos)
    {
        foreach (var kvp in _emptySlots)
        {
            var wagonRT = kvp.Value;
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                wagonRT, screenPos, null, out localPoint))
            {
                if (wagonRT.rect.Contains(localPoint))
                    return kvp.Key;
            }
        }
        return -1;
    }

    private void HighlightNearestEmptyWagon(Vector2 screenPos)
    {
        ResetWagonHighlights();
        int target = FindEmptyWagonAtPosition(screenPos);
        if (target >= 0 && _wagonObjects[target] != null)
        {
            var bg = _wagonObjects[target].GetComponent<Image>();
            if (bg != null) bg.color = HexColor("#FFF9C4"); // yellow highlight
        }
    }

    private void ResetWagonHighlights()
    {
        foreach (var kvp in _emptySlots)
        {
            int idx = kvp.Key;
            if (idx < _wagonObjects.Count && _wagonObjects[idx] != null)
            {
                var bg = _wagonObjects[idx].GetComponent<Image>();
                if (bg != null) bg.color = EmptyWagonBg;
            }
        }
    }

    // ── ANIMATIONS ──

    private IEnumerator TrainEntrance()
    {
        if (_trainGroupRT == null) yield break;

        // Start off-screen left
        float screenW = trainArea.rect.width;
        _trainGroupRT.anchoredPosition = new Vector2(-screenW, 0);

        // Slide in (slow for kids to enjoy)
        float dur = 2.0f;
        Vector2 target = Vector2.zero;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float p = t / dur;
            float ease = 1f - Mathf.Pow(1f - p, 3f); // ease-out cubic
            _trainGroupRT.anchoredPosition = Vector2.Lerp(new Vector2(-screenW, 0), target, ease);
            yield return null;
        }
        _trainGroupRT.anchoredPosition = target;

        _roundActive = true;
        _lastInteractionTime = Time.time;
        _inactivityCoroutine = StartCoroutine(InactivityMonitor());
    }

    private IEnumerator TrainExit()
    {
        if (_trainGroupRT == null) yield break;

        float screenW = trainArea.rect.width;
        Vector2 start = _trainGroupRT.anchoredPosition;
        Vector2 target = new Vector2(screenW * 1.5f, 0);

        float dur = 2.4f;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float p = t / dur;
            float ease = p * p; // ease-in
            _trainGroupRT.anchoredPosition = Vector2.Lerp(start, target, ease);
            yield return null;
        }
    }

    private IEnumerator BounceWagon(int wagonIndex)
    {
        if (wagonIndex >= _wagonObjects.Count) yield break;
        var rt = _wagonObjects[wagonIndex].GetComponent<RectTransform>();
        float dur = 0.25f;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float p = t / dur;
            float scale = 1f + 0.15f * Mathf.Sin(p * Mathf.PI);
            rt.localScale = Vector3.one * scale;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private IEnumerator ShakeWagon(int wagonIndex)
    {
        if (wagonIndex >= _wagonObjects.Count) yield break;
        var rt = _wagonObjects[wagonIndex].GetComponent<RectTransform>();
        Vector2 orig = rt.anchoredPosition;
        float dur = 0.3f;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float p = t / dur;
            float offset = Mathf.Sin(p * Mathf.PI * 6f) * 10f * (1f - p);
            rt.anchoredPosition = orig + new Vector2(offset, 0);
            yield return null;
        }
        rt.anchoredPosition = orig;
    }

    private IEnumerator ReturnToOriginal(RectTransform rt, Vector2 target)
    {
        Vector2 start = rt.anchoredPosition;
        float dur = 0.2f;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            rt.anchoredPosition = Vector2.Lerp(start, target, t / dur);
            yield return null;
        }
        rt.anchoredPosition = target;
    }

    // ── COMPLETION ──

    private IEnumerator OnRoundComplete()
    {
        // Bounce all wagons in wave
        for (int i = 0; i < _wagonObjects.Count; i++)
            StartCoroutine(BounceWagon(i));

        SoundLibrary.PlayRandomFeedback();
        yield return new WaitForSeconds(0.8f);

        // Train exits to the right
        yield return StartCoroutine(TrainExit());

        _currentRound++;
        _stats.RecordRoundComplete();

        if (_currentRound >= totalRounds)
        {
            if (ConfettiController.Instance != null)
                ConfettiController.Instance.Play();

            if (!GameCompletionBridge.WillJourneyNavigate)
            {
                yield return new WaitForSeconds(2.5f);
                _currentRound = 0;
                LoadRound();
            }
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
            LoadRound();
        }
    }

    // ── INACTIVITY / HINTS ──

    private IEnumerator InactivityMonitor()
    {
        while (_roundActive)
        {
            yield return new WaitForSeconds(1f);
            if (_roundActive && Time.time - _lastInteractionTime >= 6f)
            {
                _hintsUsed++;
                _stats.RecordHint();
                HintNextEmpty();
                _lastInteractionTime = Time.time;
            }
        }
    }

    private void HintNextEmpty()
    {
        // Pulse the first remaining empty wagon
        foreach (var kvp in _emptySlots)
        {
            int idx = kvp.Key;
            if (idx < _wagonObjects.Count)
            {
                StartCoroutine(PulseHint(_wagonObjects[idx]));
                break;
            }
        }
    }

    private IEnumerator PulseHint(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        var img = go.GetComponent<Image>();
        for (int i = 0; i < 3; i++)
        {
            if (img != null) img.color = HexColor("#FFF9C4");
            float dur = 0.3f;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                float scale = 1f + 0.08f * Mathf.Sin((t / dur) * Mathf.PI);
                rt.localScale = Vector3.one * scale;
                yield return null;
            }
            rt.localScale = Vector3.one;
            if (img != null) img.color = EmptyWagonBg;
            yield return new WaitForSeconds(0.1f);
        }
    }

    // ── NAVIGATION ──

    public void OnHomePressed()
    {
        if (_roundActive && _stats != null) _stats.Abandon();
        NavigationManager.GoToMainMenu();
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
