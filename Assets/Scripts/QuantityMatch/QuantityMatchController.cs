using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Number-to-Quantity Match game ("התאם כמות").
/// Shows a target number at top, 4 animal-group tiles at bottom.
/// Child taps the tile whose animal count matches the target number.
/// </summary>
public class QuantityMatchController : BaseMiniGame
{
    [Header("Layout")]
    public RectTransform numberArea;   // top zone for the big number
    public RectTransform tilesArea;    // bottom zone for 4 answer tiles

    [Header("UI")]
    public TextMeshProUGUI targetNumberText;
    public TextMeshProUGUI titleText;

    [Header("Sprites")]
    public Sprite cellSprite; // RoundedRect

    private static readonly string[] AllAnimals =
    {
        "Elephant", "Giraffe", "Horse", "Cow", "Lion",
        "Dog", "Cat", "Sheep", "Monkey", "Donkey", "Bear", "Zebra",
        "Chicken", "Duck", "Bird", "Fish", "Frog", "Snake", "Turtle"
    };

    private int _attemptsThisRound;
    private int _hintsUsed;
    private float _lastInteractionTime;
    private float _roundStartTime;

    private int _targetNumber;
    private int _correctTileIndex;
    private int[] _tileQuantities = new int[4];
    private string[] _tileAnimals = new string[4];

    private List<GameObject> _tileObjects = new List<GameObject>();
    private Coroutine _inactivityCoroutine;
    private string _lastCorrectAnimal;

    // ── BASE MINI GAME HOOKS ─────────────────────────────────────

    protected override void OnGameInit()
    {
        totalRounds = 5;
        contentCategory = "";
        playWinSound = true;
        delayBeforeNextRound = 1.2f;
        delayAfterFinalRound = 2.5f;
    }

    protected override string GetFallbackGameId() => "quantitymatch";

    protected override string GetContentId() => null;

    protected override void OnRoundSetup()
    {
        _attemptsThisRound = 0;
        _hintsUsed = 0;

        // Generate round
        GenerateRound();

        // Display target number
        if (targetNumberText != null)
            targetNumberText.text = _targetNumber.ToString();

        // Build tiles
        BuildTiles();

        _roundStartTime = Time.time;
        _lastInteractionTime = Time.time;
        _inactivityCoroutine = StartCoroutine(InactivityMonitor());

        Stats.SetCustom("targetNumber", (float)_targetNumber);

        // Position tutorial hand on one of the answer tiles (the correct one)
        PositionTutorialHand();

        Debug.Log($"[QuantityMatch] Round {CurrentRound + 1}: target={_targetNumber}, correct at slot {_correctTileIndex}, quantities=[{_tileQuantities[0]},{_tileQuantities[1]},{_tileQuantities[2]},{_tileQuantities[3]}], difficulty={Difficulty}");
    }

    protected override void OnRoundCleanup()
    {
        if (_inactivityCoroutine != null)
        {
            StopCoroutine(_inactivityCoroutine);
            _inactivityCoroutine = null;
        }
        foreach (var go in _tileObjects)
            if (go != null) Destroy(go);
        _tileObjects.Clear();
    }

    protected override void OnBeforeComplete()
    {
        Stats.SetCustom("attemptsThisRound", (float)_attemptsThisRound);
        Stats.SetCustom("hintsUsed", (float)_hintsUsed);
        Stats.SetCustom("responseTime", Time.time - _roundStartTime);
    }

    // ── ROUND GENERATION ──

    private void GenerateRound()
    {
        // Determine target number range based on difficulty
        int minTarget, maxTarget;
        GetTargetRange(Difficulty, out minTarget, out maxTarget);
        _targetNumber = Random.Range(minTarget, maxTarget + 1);

        // Generate 4 unique quantities: 1 correct + 3 distractors
        GenerateQuantities();

        // Assign a different animal to each tile, avoiding recent repetition
        AssignAnimals();
    }

    private void GetTargetRange(int difficulty, out int min, out int max)
    {
        // Difficulty scaling: easy(1-3)=1-3, medium(4-6)=1-5, hard(7-10)=1-8
        int tier = difficulty <= 3 ? 0 : difficulty <= 6 ? 1 : 2;
        switch (tier)
        {
            case 0:  min = 1; max = 3; break;
            case 1:  min = 1; max = 5; break;
            default: min = 1; max = 8; break;
        }
    }

    private void GenerateQuantities()
    {
        // Build distractor pool: close to target, all unique, no duplicates of target
        int maxSpread = GetDistractorSpread(Difficulty);

        var quantities = new HashSet<int>();
        quantities.Add(_targetNumber);

        // Prefer ±1, then ±2, then expand
        var candidates = new List<int>();
        for (int d = 1; d <= Mathf.Max(maxSpread, 3); d++)
        {
            if (_targetNumber - d >= 1) candidates.Add(_targetNumber - d);
            if (_targetNumber + d <= 10) candidates.Add(_targetNumber + d);
        }

        // Shuffle candidates
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = tmp;
        }

        // Pick closest first (candidates are already ordered by distance before shuffle,
        // but we want some randomness). Sort by distance, then pick 3.
        candidates.Sort((a, b) => Mathf.Abs(a - _targetNumber).CompareTo(Mathf.Abs(b - _targetNumber)));

        foreach (int c in candidates)
        {
            if (quantities.Count >= 4) break;
            if (!quantities.Contains(c) && c >= 1)
                quantities.Add(c);
        }

        // Fallback if not enough (shouldn't happen for targets 1-8)
        int fallback = 1;
        while (quantities.Count < 4)
        {
            if (!quantities.Contains(fallback)) quantities.Add(fallback);
            fallback++;
        }

        // Convert to array and shuffle positions
        var qList = new List<int>(quantities);
        for (int i = qList.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = qList[i];
            qList[i] = qList[j];
            qList[j] = tmp;
        }

        for (int i = 0; i < 4; i++)
            _tileQuantities[i] = qList[i];

        // Find which slot has the correct answer
        _correctTileIndex = System.Array.IndexOf(_tileQuantities, _targetNumber);
    }

    private int GetDistractorSpread(int difficulty)
    {
        // Tighter spread at higher difficulty
        if (difficulty <= 2) return 3;
        if (difficulty <= 5) return 2;
        return 2; // always tight at high difficulty
    }

    private void AssignAnimals()
    {
        var used = new HashSet<string>();
        for (int i = 0; i < 4; i++)
        {
            int safety = 0;
            string picked;
            do
            {
                picked = AllAnimals[Random.Range(0, AllAnimals.Length)];
                safety++;
            } while ((used.Contains(picked) || (i == _correctTileIndex && picked == _lastCorrectAnimal))
                     && safety < 30);

            _tileAnimals[i] = picked;
            used.Add(picked);
        }
        _lastCorrectAnimal = _tileAnimals[_correctTileIndex];
    }

    // ── TILE BUILDING ──

    private void BuildTiles()
    {
        if (tilesArea == null) return;

        float areaW = tilesArea.rect.width;
        float areaH = tilesArea.rect.height;

        float spacing = 30f;
        float maxTileW = (areaW - 3 * spacing) / 4f;
        float tileSize = Mathf.Min(maxTileW, areaH * 0.92f, 300f);

        float totalW = 4 * tileSize + 3 * spacing;
        float startX = -totalW / 2f + tileSize / 2f;

        for (int i = 0; i < 4; i++)
        {
            float x = startX + i * (tileSize + spacing);
            var tileGO = CreateTile(tilesArea, x, 0, tileSize, i,
                _tileQuantities[i], _tileAnimals[i]);
            _tileObjects.Add(tileGO);

            StartCoroutine(PopIn(tileGO.GetComponent<RectTransform>(), 0.15f + i * 0.1f));
        }
    }

    private GameObject CreateTile(RectTransform parent, float x, float y, float size,
        int tileIndex, int quantity, string animalId)
    {
        var go = new GameObject($"Tile_{tileIndex}");
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
        var borderImg = borderGO.AddComponent<Image>();
        if (cellSprite != null) { borderImg.sprite = cellSprite; borderImg.type = Image.Type.Sliced; }
        borderImg.color = HexColor("#E0E0E0");
        borderImg.raycastTarget = false;

        // Background
        var bgImg = go.AddComponent<Image>();
        if (cellSprite != null) { bgImg.sprite = cellSprite; bgImg.type = Image.Type.Sliced; }
        bgImg.color = Color.white;
        bgImg.raycastTarget = true;

        // Place animal sprites inside tile
        PlaceAnimals(go.transform, size, quantity, animalId);

        // Button
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bgImg;
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = HexColor("#F5F5F5");
        colors.pressedColor = HexColor("#EEEEEE");
        btn.colors = colors;

        int captured = tileIndex;
        btn.onClick.AddListener(() => OnTileTapped(captured));

        return go;
    }

    private void PlaceAnimals(Transform parent, float tileSize, int count, string animalId)
    {
        // Load animal sprite (first idle frame)
        Sprite animalSprite = null;
        var animData = AnimalAnimData.Load(animalId);
        if (animData != null && animData.idleFrames != null && animData.idleFrames.Length > 0)
            animalSprite = animData.idleFrames[0];
        if (animalSprite == null)
            animalSprite = Resources.Load<Sprite>($"AnimalSprites/{animalId}");

        // Layout: arrange N animals in a grid-like pattern inside the tile
        var positions = GetAnimalPositions(count, tileSize);

        float animalSize = GetAnimalSize(count, tileSize);

        for (int i = 0; i < count; i++)
        {
            var aGO = new GameObject($"Animal_{i}");
            aGO.transform.SetParent(parent, false);
            var art = aGO.AddComponent<RectTransform>();
            art.sizeDelta = new Vector2(animalSize, animalSize);
            art.anchoredPosition = positions[i];

            var img = aGO.AddComponent<Image>();
            img.sprite = animalSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = Color.white;
        }
    }

    private float GetAnimalSize(int count, float tileSize)
    {
        float padding = tileSize * 0.06f;
        float usable = tileSize - 2 * padding;

        if (count <= 1) return usable * 0.85f;
        if (count <= 2) return usable * 0.60f;
        if (count <= 3) return usable * 0.52f;
        if (count <= 4) return usable * 0.50f;
        if (count <= 6) return usable * 0.42f;
        return usable * 0.36f;
    }

    private Vector2[] GetAnimalPositions(int count, float tileSize)
    {
        float padding = tileSize * 0.06f;
        float usable = tileSize - 2 * padding;
        float animalSize = GetAnimalSize(count, tileSize);

        var positions = new Vector2[count];

        switch (count)
        {
            case 1:
                positions[0] = Vector2.zero;
                break;
            case 2:
                positions[0] = new Vector2(-usable * 0.22f, 0);
                positions[1] = new Vector2(usable * 0.22f, 0);
                break;
            case 3:
                // Triangle: 1 top, 2 bottom
                positions[0] = new Vector2(0, usable * 0.18f);
                positions[1] = new Vector2(-usable * 0.22f, -usable * 0.16f);
                positions[2] = new Vector2(usable * 0.22f, -usable * 0.16f);
                break;
            case 4:
                // 2x2 grid
                float off4 = usable * 0.20f;
                positions[0] = new Vector2(-off4, off4);
                positions[1] = new Vector2(off4, off4);
                positions[2] = new Vector2(-off4, -off4);
                positions[3] = new Vector2(off4, -off4);
                break;
            case 5:
                // 2 top, 3 bottom
                float ox5 = usable * 0.20f;
                float oy5 = usable * 0.16f;
                positions[0] = new Vector2(-ox5, oy5);
                positions[1] = new Vector2(ox5, oy5);
                positions[2] = new Vector2(-ox5 * 1.3f, -oy5);
                positions[3] = new Vector2(0, -oy5);
                positions[4] = new Vector2(ox5 * 1.3f, -oy5);
                break;
            case 6:
                // 3x2 grid
                float ox6 = usable * 0.20f;
                float oy6 = usable * 0.16f;
                positions[0] = new Vector2(-ox6, oy6);
                positions[1] = new Vector2(0, oy6);
                positions[2] = new Vector2(ox6, oy6);
                positions[3] = new Vector2(-ox6, -oy6);
                positions[4] = new Vector2(0, -oy6);
                positions[5] = new Vector2(ox6, -oy6);
                break;
            case 7:
                // 3 top, 4 bottom
                float ox7 = usable * 0.15f;
                float oy7 = usable * 0.12f;
                positions[0] = new Vector2(-ox7, oy7);
                positions[1] = new Vector2(0, oy7);
                positions[2] = new Vector2(ox7, oy7);
                positions[3] = new Vector2(-ox7 * 1.3f, -oy7);
                positions[4] = new Vector2(-ox7 * 0.4f, -oy7);
                positions[5] = new Vector2(ox7 * 0.4f, -oy7);
                positions[6] = new Vector2(ox7 * 1.3f, -oy7);
                break;
            default: // 8+
                // 4x2 grid
                float ox8 = usable * 0.15f;
                float oy8 = usable * 0.12f;
                for (int i = 0; i < count && i < 8; i++)
                {
                    int col = i % 4;
                    int row = i / 4;
                    positions[i] = new Vector2(
                        -ox8 * 1.5f + col * ox8,
                        oy8 - row * oy8 * 2f);
                }
                break;
        }

        return positions;
    }

    // ── INPUT HANDLING ──

    private void OnTileTapped(int tileIndex)
    {
        if (IsInputLocked) return;
        DismissTutorial();
        _lastInteractionTime = Time.time;
        _attemptsThisRound++;

        if (tileIndex == _correctTileIndex)
        {
            RecordCorrect("quantity_match", _targetNumber.ToString(), isLast: true);
            PlayCorrectEffect(_tileObjects[tileIndex].GetComponent<RectTransform>());
            StartCoroutine(OnCorrectSequence(tileIndex));
        }
        else
        {
            RecordMistake("wrong_quantity", _tileQuantities[tileIndex].ToString());
            PlayWrongEffect(_tileObjects[tileIndex].GetComponent<RectTransform>());
            StartCoroutine(ShakeTile(_tileObjects[tileIndex]));

            // After 2 wrong taps, hint
            if (_attemptsThisRound >= 3 && _attemptsThisRound % 2 == 1)
            {
                _hintsUsed++;
                RecordHint();
                StartCoroutine(PulseHint(_tileObjects[_correctTileIndex]));
            }
        }
    }

    // ── CORRECT SEQUENCE ──

    private IEnumerator OnCorrectSequence(int correctTile)
    {
        // Highlight correct tile green
        var bg = _tileObjects[correctTile].GetComponent<Image>();
        if (bg != null) bg.color = HexColor("#C8E6C9");
        var border = _tileObjects[correctTile].transform.Find("Border");
        if (border != null)
        {
            var bImg = border.GetComponent<Image>();
            if (bImg != null) bImg.color = HexColor("#66BB6A");
        }

        // Dim others
        for (int i = 0; i < 4; i++)
        {
            if (i == correctTile) continue;
            var img = _tileObjects[i].GetComponent<Image>();
            if (img != null) { var c = img.color; c.a = 0.35f; img.color = c; }
        }

        // Bounce correct tile + play number sound
        StartCoroutine(CelebrateBounce(_tileObjects[correctTile].GetComponent<RectTransform>(), 0f));
        SoundLibrary.PlayNumberName(_targetNumber);

        yield return new WaitForSeconds(1.5f);

        // All game-specific animations done — let base handle stats, confetti, round advance
        CompleteRound();
    }

    // ── ANIMATIONS ──

    private IEnumerator ShakeTile(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        var img = go.GetComponent<Image>();
        Color orig = img != null ? img.color : Color.white;
        if (img != null) img.color = HexColor("#FFCDD2");

        Vector2 origPos = rt.anchoredPosition;
        float dur = 0.3f;
        float amp = 12f;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float p = t / dur;
            float offset = Mathf.Sin(p * Mathf.PI * 6f) * amp * (1f - p);
            rt.anchoredPosition = origPos + new Vector2(offset, 0);
            yield return null;
        }
        rt.anchoredPosition = origPos;
        if (img != null) img.color = orig;
    }

    private IEnumerator PulseHint(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        var img = go.GetComponent<Image>();

        for (int p = 0; p < 3; p++)
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
            if (img != null) img.color = Color.white;
            yield return new WaitForSeconds(0.1f);
        }
    }

    private IEnumerator CelebrateBounce(RectTransform rt, float delay)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);
        float dur = 0.25f;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float p = t / dur;
            float scale = 1f + 0.12f * Mathf.Sin(p * Mathf.PI);
            rt.localScale = Vector3.one * scale;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private IEnumerator PopIn(RectTransform rt, float delay)
    {
        rt.localScale = Vector3.zero;
        yield return new WaitForSeconds(delay);
        float dur = 0.3f;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float p = t / dur;
            rt.localScale = Vector3.one * (1f + 0.2f * Mathf.Sin(p * Mathf.PI));
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
            if (!IsInputLocked && Time.time - _lastInteractionTime >= 6f)
            {
                _hintsUsed++;
                RecordHint();
                StartCoroutine(PulseHint(_tileObjects[_correctTileIndex]));
                _lastInteractionTime = Time.time;
            }
        }
    }

    // ── TUTORIAL HAND ──

    private void PositionTutorialHand()
    {
        if (TutorialHand == null || _tileObjects.Count == 0) return;

        // Point at the correct tile — show the child exactly what to tap
        var tileRT = _tileObjects[_correctTileIndex].GetComponent<RectTransform>();
        Vector2 localPos = TutorialHand.GetLocalCenter(tileRT);
        TutorialHand.SetPosition(localPos);
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
