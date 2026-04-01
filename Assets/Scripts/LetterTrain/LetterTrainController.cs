using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Letter Train game ("רכבת האותיות").
/// Train of 5-7 wagons with Hebrew letters. Some wagons are empty.
/// Player drags missing letters from the bottom into the correct empty wagons.
/// On success the train exits to the right.
/// </summary>
public class LetterTrainController : BaseMiniGame
{
    [Header("Layout")]
    public RectTransform trainArea;
    public RectTransform optionsArea;

    [Header("UI")]
    public TextMeshProUGUI titleText;

    [Header("Sprites")]
    public Sprite cellSprite;
    public Sprite circleSprite;
    public Sprite locomotiveSprite;

    // Hebrew alphabet (22 standard letters, no final forms)
    private static readonly char[] HebrewAlphabet =
    {
        '\u05D0', // א
        '\u05D1', // ב
        '\u05D2', // ג
        '\u05D3', // ד
        '\u05D4', // ה
        '\u05D5', // ו
        '\u05D6', // ז
        '\u05D7', // ח
        '\u05D8', // ט
        '\u05D9', // י
        '\u05DB', // כ
        '\u05DC', // ל
        '\u05DE', // מ
        '\u05E0', // נ
        '\u05E1', // ס
        '\u05E2', // ע
        '\u05E4', // פ
        '\u05E6', // צ
        '\u05E7', // ק
        '\u05E8', // ר
        '\u05E9', // ש
        '\u05EA', // ת
    };

    // Colors
    private static readonly Color WagonBg       = HexColor("#D1C4E9");
    private static readonly Color WagonBorder    = HexColor("#7E57C2");
    private static readonly Color EmptyWagonBg   = HexColor("#EDE7F6");
    private static readonly Color EmptyBorder    = HexColor("#B39DDB");
    private static readonly Color FilledText     = HexColor("#4527A0");
    private static readonly Color EmptyText      = HexColor("#BDBDBD");
    private static readonly Color OptionBg       = HexColor("#FFF9C4");
    private static readonly Color OptionBorder   = HexColor("#FFC107");
    private static readonly Color OptionText     = HexColor("#F57F17");
    private static Color CorrectBg => GameUIConstants.CorrectColor;
    private static readonly Color CorrectBorder  = HexColor("#66BB6A");
    private static readonly Color ConnectorColor = HexColor("#9575CD");

    // State
    private int _mistakesThisRound;
    private int _hintsUsed;
    private float _lastInteractionTime;
    private int _placedCount;
    private float _wagonW;
    private bool _trainReady; // true after entrance animation completes

    // Level data
    private int _startIndex; // index into HebrewAlphabet
    private int _wagonCount;
    private char[] _sequence;
    private bool[] _isMissing;
    private char[] _missingLetters;
    private int _totalMissing;

    // UI objects
    private List<GameObject> _wagonObjects = new List<GameObject>();
    private List<GameObject> _optionObjects = new List<GameObject>();
    private List<GameObject> _connectorObjects = new List<GameObject>();
    private Dictionary<int, RectTransform> _emptySlots = new Dictionary<int, RectTransform>();
    private Dictionary<int, TextMeshProUGUI> _emptyTexts = new Dictionary<int, TextMeshProUGUI>();
    private RectTransform _trainGroupRT;

    // Drag state
    private GameObject _draggedOption;
    private RectTransform _draggedRT;
    private char _draggedLetter;
    private Vector2 _dragOriginalPos;

    private Coroutine _inactivityCoroutine;

    // ── BASE MINI GAME HOOKS ──

    protected override void OnGameInit()
    {
        totalRounds = 1;
        isEndless = true;
        playConfettiOnRoundWin = false; // we trigger confetti manually before train exit
        playWinSound = true;
        delayBeforeNextRound = 0.5f;
    }

    protected override string GetFallbackGameId() => "lettertrain";

    protected override void OnRoundSetup()
    {
        _mistakesThisRound = 0;
        _hintsUsed = 0;
        _placedCount = 0;
        _trainReady = false;

        GenerateLevel();
        BuildTrain();
        BuildOptions();

        Stats.SetCustom("wagonCount", (float)_wagonCount);
        Stats.SetCustom("missingCount", (float)_totalMissing);

        StartCoroutine(TrainEntrance());
    }

    protected override void OnRoundCleanup()
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

    protected override void OnBeforeComplete()
    {
        Stats.SetCustom("mistakes", (float)_mistakesThisRound);
        Stats.SetCustom("hintsUsed", (float)_hintsUsed);
    }

    protected override IEnumerator OnAfterComplete()
    {
        // Bounce all wagons in wave
        for (int i = 0; i < _wagonObjects.Count; i++)
            StartCoroutine(BounceWagon(i));

        float bounceWait = 0.3f;
        yield return new WaitForSeconds(bounceWait);

        // Train exits
        yield return StartCoroutine(TrainExit());
    }

    // ── LEVEL GENERATION ──

    private void GenerateLevel()
    {
        // Difficulty scaling: easy(1-3), medium(4-6), hard(7-10)
        int tier = Difficulty <= 3 ? 0 : Difficulty <= 6 ? 1 : 2;
        switch (tier)
        {
            case 0: _wagonCount = 6;  _totalMissing = 3; break;
            case 1: _wagonCount = 8;  _totalMissing = 5; break;
            case 2: _wagonCount = 10; _totalMissing = 7; break;
        }

        // Start position in alphabet based on difficulty
        int maxStart;
        if (Difficulty <= 3)
            maxStart = 5; // early letters (א-ו range)
        else if (Difficulty <= 6)
            maxStart = 12; // mid letters
        else
            maxStart = HebrewAlphabet.Length - _wagonCount; // any position

        maxStart = Mathf.Clamp(maxStart, 0, HebrewAlphabet.Length - _wagonCount);
        _startIndex = Random.Range(0, maxStart + 1);

        // Build sequence
        _sequence = new char[_wagonCount];
        _isMissing = new bool[_wagonCount];
        for (int i = 0; i < _wagonCount; i++)
            _sequence[i] = HebrewAlphabet[_startIndex + i];

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

        _missingLetters = new char[_totalMissing];
        for (int i = 0; i < _totalMissing; i++)
        {
            int idx = candidates[i];
            _isMissing[idx] = true;
            _missingLetters[i] = _sequence[idx];
        }

        Debug.Log($"[LetterTrain] Round {CurrentRound + 1}: letters {HebrewAlphabet[_startIndex]}-{HebrewAlphabet[_startIndex + _wagonCount - 1]}, missing {_totalMissing}, difficulty {Difficulty}");
    }

    // ── TRAIN BUILDING ──

    private void BuildTrain()
    {
        if (trainArea == null) return;

        var trainGroupGO = new GameObject("TrainGroup");
        trainGroupGO.transform.SetParent(trainArea, false);
        _trainGroupRT = trainGroupGO.AddComponent<RectTransform>();
        _trainGroupRT.anchorMin = Vector2.zero;
        _trainGroupRT.anchorMax = Vector2.one;
        _trainGroupRT.offsetMin = Vector2.zero;
        _trainGroupRT.offsetMax = Vector2.zero;

        float areaW = trainArea.rect.width;
        float wagonW = Mathf.Min(200f, (areaW - (_wagonCount - 1) * 20f - 100f) / (_wagonCount + 1));
        _wagonW = wagonW;
        float wagonH = wagonW * 0.85f;
        float connW = 20f;
        float locoGap = 40f;
        float locoW = wagonW * 1.6f;
        float totalW = _wagonCount * wagonW + (_wagonCount - 1) * connW + locoGap + locoW;
        // RTL: first letter (index 0) is rightmost, locomotive on left
        float startX = totalW / 2f - wagonW / 2f;

        for (int i = 0; i < _wagonCount; i++)
        {
            float x = startX - i * (wagonW + connW);

            if (i > 0)
            {
                var connGO = new GameObject("Connector");
                connGO.transform.SetParent(_trainGroupRT, false);
                var connRT = connGO.AddComponent<RectTransform>();
                connRT.anchorMin = new Vector2(0.5f, 0f);
                connRT.anchorMax = new Vector2(0.5f, 0f);
                connRT.pivot = new Vector2(0.5f, 0.5f);
                connRT.sizeDelta = new Vector2(connW + 6f, 8f);
                connRT.anchoredPosition = new Vector2(x + (wagonW + connW) / 2f, wagonH * 0.45f);
                var connImg = connGO.AddComponent<Image>();
                connImg.color = ConnectorColor;
                connImg.raycastTarget = false;
                _connectorObjects.Add(connGO);
            }

            bool empty = _isMissing[i];
            var wagonGO = CreateWagon(_trainGroupRT, x, 0, wagonW, wagonH, i, empty);
            _wagonObjects.Add(wagonGO);
        }

        // Locomotive on the left (RTL: leads from left)
        float locoX = startX - _wagonCount * (wagonW + connW) + connW - locoGap;
        CreateLocomotive(_trainGroupRT, locoX, 0, wagonW, wagonH);
    }

    private GameObject CreateWagon(RectTransform parent, float x, float y,
        float w, float h, int wagonIndex, bool isEmpty)
    {
        var go = new GameObject($"Wagon_{wagonIndex}");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f); // anchor to bottom-center
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);     // pivot at bottom edge
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

        // Wheels
        float wheelSize = Mathf.Min(50f, w * 0.30f);
        CreateWheel(go.transform, -w * 0.28f, -25f, wheelSize);
        CreateWheel(go.transform, w * 0.28f, -25f, wheelSize);

        // Letter text
        var textGO = new GameObject("Letter");
        textGO.transform.SetParent(go.transform, false);
        var trt = textGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = Mathf.Min(80f, h * 0.7f);
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.isRightToLeftText = true;

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

    private void CreateLocomotive(RectTransform parent, float x, float y, float w, float h)
    {
        float locoH = h * 1.6f;
        float locoW = locoH;

        var go = new GameObject("Locomotive");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(locoW, locoH);
        rt.anchoredPosition = new Vector2(x, y - h * 0.1f);

        var bgImg = go.AddComponent<Image>();
        if (locomotiveSprite != null)
        {
            bgImg.sprite = locomotiveSprite;
            bgImg.preserveAspect = true;
            bgImg.color = Color.white;
        }
        else
        {
            if (cellSprite != null) { bgImg.sprite = cellSprite; bgImg.type = Image.Type.Sliced; }
            bgImg.color = HexColor("#7B1FA2");
        }
        bgImg.raycastTarget = false;

        // Connector to last wagon
        var connGO = new GameObject("LocoConnector");
        connGO.transform.SetParent(parent, false);
        var connRT = connGO.AddComponent<RectTransform>();
        connRT.anchorMin = new Vector2(0.5f, 0f);
        connRT.anchorMax = new Vector2(0.5f, 0f);
        connRT.pivot = new Vector2(0.5f, 0.5f);
        connRT.sizeDelta = new Vector2(24f, 8f);
        connRT.anchoredPosition = new Vector2(x + (locoW + 20f) / 2f, h * 0.45f);
        var connImg = connGO.AddComponent<Image>();
        connImg.color = ConnectorColor;
        connImg.raycastTarget = false;
        _connectorObjects.Add(connGO);

        _wagonObjects.Add(go);
    }

    private void CreateWheel(Transform parent, float x, float y, float size)
    {
        var go = new GameObject("Wheel");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f); // anchor to bottom of wagon
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = new Vector2(x, y);
        var img = go.AddComponent<Image>();
        if (circleSprite != null) img.sprite = circleSprite;
        img.color = HexColor("#5E35B1");
        img.raycastTarget = false;
    }

    // ── OPTIONS ──

    private void BuildOptions()
    {
        if (optionsArea == null) return;

        var shuffled = new List<char>(_missingLetters);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = shuffled[i]; shuffled[i] = shuffled[j]; shuffled[j] = tmp;
        }

        float areaW = optionsArea.rect.width;
        float areaH = optionsArea.rect.height;
        float optSize = Mathf.Min(_wagonW, areaH * 0.95f);
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
        float size, char letter)
    {
        var go = new GameObject($"Option_{letter}");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = new Vector2(x, y);

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

        var bgImg = go.AddComponent<Image>();
        if (cellSprite != null) { bgImg.sprite = cellSprite; bgImg.type = Image.Type.Sliced; }
        bgImg.color = OptionBg;
        bgImg.raycastTarget = true;

        var textGO = new GameObject("Letter");
        textGO.transform.SetParent(go.transform, false);
        var trt = textGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = letter.ToString();
        tmp.fontSize = Mathf.Min(76f, size * 0.65f);
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = OptionText;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.isRightToLeftText = true;

        var handler = go.AddComponent<LetterTrainDragHandler>();
        handler.controller = this;
        handler.letter = letter;

        return go;
    }

    // ── DRAG AND DROP ──

    public void OnOptionDragBegin(GameObject option, char letter, PointerEventData eventData)
    {
        if (!_trainReady || IsInputLocked) return;
        DismissTutorial();
        _lastInteractionTime = Time.time;
        _draggedOption = option;
        _draggedRT = option.GetComponent<RectTransform>();
        _draggedLetter = letter;
        _dragOriginalPos = _draggedRT.anchoredPosition;
        _dragOriginalSiblingIndex = option.transform.GetSiblingIndex();
        option.transform.SetAsLastSibling();
        _draggedRT.localScale = Vector3.one * 1.1f;
        var dragImg = option.GetComponent<UnityEngine.UI.Image>();
        if (dragImg != null) dragImg.raycastTarget = false;
    }

    private int _dragOriginalSiblingIndex;

    public void OnOptionDrag(PointerEventData eventData)
    {
        if (_draggedOption == null || !_trainReady || IsInputLocked) return;
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _draggedRT.parent as RectTransform, eventData.position, eventData.pressEventCamera, out localPoint);
        _draggedRT.anchoredPosition = localPoint;
        HighlightNearestEmptyWagon(eventData.position);
    }

    public void OnOptionDragEnd(PointerEventData eventData)
    {
        if (_draggedOption == null || !_trainReady || IsInputLocked) return;
        _draggedRT.localScale = Vector3.one;

        int targetWagon = FindEmptyWagonAtPosition(eventData.position);

        if (targetWagon >= 0 && _sequence[targetWagon] == _draggedLetter)
        {
            PlaceLetterInWagon(targetWagon, _draggedLetter, _draggedOption);
        }
        else if (targetWagon >= 0)
        {
            _mistakesThisRound++;
            RecordMistake("wrong_wagon", $"{_draggedLetter}→slot{targetWagon}");
            if (_wagonObjects[targetWagon] != null)
                PlayWrongEffect(_wagonObjects[targetWagon].GetComponent<RectTransform>());
            StartCoroutine(ReturnToOriginal(_draggedRT, _dragOriginalPos));
            StartCoroutine(ShakeWagon(targetWagon));
        }
        else
        {
            StartCoroutine(ReturnToOriginal(_draggedRT, _dragOriginalPos));
        }

        // Restore raycast and sibling order
        if (_draggedOption != null)
        {
            var dragImg = _draggedOption.GetComponent<UnityEngine.UI.Image>();
            if (dragImg != null) dragImg.raycastTarget = true;
            _draggedOption.transform.SetSiblingIndex(_dragOriginalSiblingIndex);
        }

        ResetWagonHighlights();
        _draggedOption = null;
    }

    private void PlaceLetterInWagon(int wagonIndex, char letter, GameObject option)
    {
        _placedCount++;
        RecordCorrect("letter_placed", letter.ToString(), isLast: _placedCount >= _totalMissing);
        if (_wagonObjects[wagonIndex] != null)
            PlayCorrectEffect(_wagonObjects[wagonIndex].GetComponent<RectTransform>());

        if (_emptyTexts.ContainsKey(wagonIndex))
        {
            _emptyTexts[wagonIndex].text = letter.ToString();
            _emptyTexts[wagonIndex].color = FilledText;
        }

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

        _emptySlots.Remove(wagonIndex);
        _emptyTexts.Remove(wagonIndex);
        Destroy(option);
        _optionObjects.Remove(option);

        StartCoroutine(BounceWagon(wagonIndex));

        if (_placedCount >= _totalMissing)
        {
            // Confetti immediately when last letter placed (includes feedback sound)
            ConfettiController.Instance?.Play();

            CompleteRound();
        }
    }

    private int FindEmptyWagonAtPosition(Vector2 screenPos)
    {
        foreach (var kvp in _emptySlots)
        {
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                kvp.Value, screenPos, null, out localPoint))
            {
                if (kvp.Value.rect.Contains(localPoint))
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
            if (bg != null) bg.color = HexColor("#FFF9C4");
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
        float screenW = trainArea.rect.width;
        // RTL: enter from the right
        _trainGroupRT.anchoredPosition = new Vector2(screenW, 0);

        float dur = 2.0f;
        Vector2 target = Vector2.zero;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float p = t / dur;
            float ease = 1f - Mathf.Pow(1f - p, 3f);
            _trainGroupRT.anchoredPosition = Vector2.Lerp(new Vector2(screenW, 0), target, ease);
            yield return null;
        }
        _trainGroupRT.anchoredPosition = target;

        _trainReady = true;
        _lastInteractionTime = Time.time;
        _inactivityCoroutine = StartCoroutine(InactivityMonitor());

        // Position tutorial hand: drag from first option letter to its target wagon
        PositionTutorialHand();
    }

    private IEnumerator TrainExit()
    {
        if (_trainGroupRT == null) yield break;
        float screenW = trainArea.rect.width;
        Vector2 start = _trainGroupRT.anchoredPosition;
        // RTL: exit to the left
        Vector2 target = new Vector2(-screenW * 1.5f, 0);

        float dur = 1.2f;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float p = t / dur;
            float ease = p * p;
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
            rt.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(p * Mathf.PI));
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

    // ── INACTIVITY ──

    private IEnumerator InactivityMonitor()
    {
        while (!IsInputLocked)
        {
            yield return new WaitForSeconds(1f);
            if (!IsInputLocked && _trainReady && Time.time - _lastInteractionTime >= 5f)
            {
                _hintsUsed++;
                RecordHint();
                HintNextEmpty();
                _lastInteractionTime = Time.time;
            }
        }
    }

    private void HintNextEmpty()
    {
        foreach (var kvp in _emptySlots)
        {
            if (kvp.Key < _wagonObjects.Count)
            {
                StartCoroutine(PulseHint(_wagonObjects[kvp.Key]));
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
                rt.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin((t / dur) * Mathf.PI));
                yield return null;
            }
            rt.localScale = Vector3.one;
            if (img != null) img.color = EmptyWagonBg;
            yield return new WaitForSeconds(0.1f);
        }
    }

    // ── TUTORIAL HAND ──

    private void PositionTutorialHand()
    {
        if (TutorialHand == null || _optionObjects.Count == 0 || _emptySlots.Count == 0) return;

        // Find first empty wagon and its expected letter
        int firstEmptyWagonIdx = -1;
        RectTransform targetWagonRT = null;
        foreach (var kvp in _emptySlots)
        {
            firstEmptyWagonIdx = kvp.Key;
            targetWagonRT = kvp.Value;
            break;
        }
        if (targetWagonRT == null || firstEmptyWagonIdx < 0 || firstEmptyWagonIdx >= _sequence.Length) return;

        char expectedLetter = _sequence[firstEmptyWagonIdx];

        // Find the option that matches the first empty wagon's letter
        GameObject correctOption = null;
        foreach (var optGO in _optionObjects)
        {
            var tmp = optGO.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null && tmp.text.Length > 0 && tmp.text[0] == expectedLetter)
            {
                correctOption = optGO;
                break;
            }
        }
        if (correctOption == null) correctOption = _optionObjects[0];

        var optionRT = correctOption.GetComponent<RectTransform>();
        Vector2 fromLocal = TutorialHand.GetLocalCenter(optionRT);
        Vector2 toLocal = TutorialHand.GetLocalCenter(targetWagonRT);

        TutorialHand.SetMovePath(fromLocal, toLocal, 1.2f);
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
