using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Main controller for the Odd One Out game ("מצא את השונה").
/// Shows 4 animals in a row — 3 identical, 1 different. Child taps the odd one.
/// </summary>
public class OddOneOutController : MonoBehaviour
{
    [Header("Layout")]
    public RectTransform rowArea; // horizontal strip for 4 animal slots

    [Header("UI")]
    public TextMeshProUGUI titleText;

    [Header("Sprites")]
    public Sprite cellSprite; // RoundedRect for card bg

    [Header("Settings")]
    public int totalRounds = 5;

    // ── All 19 animals ──
    private static readonly string[] AllAnimals =
    {
        "Elephant", "Giraffe", "Horse", "Cow", "Lion",
        "Dog", "Cat", "Sheep", "Monkey", "Donkey", "Bear", "Zebra",
        "Chicken", "Duck", "Bird", "Fish", "Frog", "Snake", "Turtle"
    };

    // Visually distinct tiers — easy pairs are very different animals
    private static readonly string[] EasyPool =
        { "Elephant", "Giraffe", "Horse", "Cow", "Lion", "Dog", "Cat", "Bear" };
    private static readonly string[] MediumPool =
        { "Elephant", "Giraffe", "Horse", "Cow", "Lion", "Dog", "Cat",
          "Sheep", "Monkey", "Donkey", "Bear", "Zebra" };

    private GameStatsCollector _stats;
    private int _difficulty = 1;
    private int _currentRound;
    private int _score;
    private bool _roundActive;
    private int _attemptsThisRound;
    private int _hintsUsed;
    private float _lastInteractionTime;

    private int _oddIndex; // which of the 4 slots is the odd one
    private string _majorAnimal;
    private string _oddAnimal;
    private string _lastMajor;
    private string _lastOdd;

    private List<GameObject> _slotObjects = new List<GameObject>();
    private UISpriteAnimator[] _animators = new UISpriteAnimator[4];
    private Coroutine _inactivityCoroutine;

    private void Start()
    {
        _currentRound = 0;
        _score = 0;
        LoadRound();
    }

    // ── ROUND LIFECYCLE ──

    private void LoadRound()
    {
        ClearRound();
        _roundActive = true;
        _attemptsThisRound = 0;
        _hintsUsed = 0;

        string gameId = GameContext.CurrentGame != null ? GameContext.CurrentGame.id : "oddoneout";
        _stats = new GameStatsCollector(gameId);
        if (GameCompletionBridge.Instance != null)
            GameCompletionBridge.Instance.ActiveCollector = _stats;
        _stats.SetTotalRoundsPlanned(1);

        _difficulty = GameDifficultyConfig.GetLevel(gameId);

        // Pick animals for this round
        PickAnimals();

        // Assign slots: 3 × major, 1 × odd
        _oddIndex = Random.Range(0, 4);

        // Build the 4 animal cards
        BuildRow();

        _lastInteractionTime = Time.time;
        _inactivityCoroutine = StartCoroutine(InactivityMonitor());

        _stats.SetCustom("difficulty", (float)_difficulty);

        Debug.Log($"[OddOneOut] Round {_currentRound + 1}: major={_majorAnimal}, odd={_oddAnimal} at slot {_oddIndex}, difficulty={_difficulty}");
    }

    private void ClearRound()
    {
        if (_inactivityCoroutine != null)
        {
            StopCoroutine(_inactivityCoroutine);
            _inactivityCoroutine = null;
        }

        foreach (var go in _slotObjects)
            if (go != null) Destroy(go);
        _slotObjects.Clear();

        for (int i = 0; i < 4; i++)
            _animators[i] = null;
    }

    // ── ANIMAL SELECTION ──

    private void PickAnimals()
    {
        // Choose pool based on difficulty
        // Low difficulty: pick from very different animals (easy to distinguish)
        // High difficulty: pick from similar-looking animals
        string[] pool;
        if (_difficulty <= 3)
            pool = EasyPool;
        else if (_difficulty <= 7)
            pool = MediumPool;
        else
            pool = AllAnimals;

        // Pick two different animals, avoiding immediate repetition
        int safety = 0;
        do
        {
            _majorAnimal = pool[Random.Range(0, pool.Length)];
            safety++;
        } while (_majorAnimal == _lastMajor && safety < 20);

        safety = 0;
        do
        {
            _oddAnimal = pool[Random.Range(0, pool.Length)];
            safety++;
        } while ((_oddAnimal == _majorAnimal || _oddAnimal == _lastOdd) && safety < 20);

        _lastMajor = _majorAnimal;
        _lastOdd = _oddAnimal;
    }

    // ── ROW BUILDING ──

    private void BuildRow()
    {
        if (rowArea == null) return;

        float areaW = rowArea.rect.width;
        float areaH = rowArea.rect.height;

        // 4 cards with spacing
        float spacing = 30f;
        float maxCardW = (areaW - 3 * spacing) / 4f;
        float cardSize = Mathf.Min(maxCardW, areaH * 0.90f, 320f);

        float totalW = 4 * cardSize + 3 * spacing;
        float startX = -totalW / 2f + cardSize / 2f;

        for (int i = 0; i < 4; i++)
        {
            string animalId = (i == _oddIndex) ? _oddAnimal : _majorAnimal;
            float x = startX + i * (cardSize + spacing);

            var slotGO = CreateAnimalSlot(rowArea, x, 0f, cardSize, i, animalId);
            _slotObjects.Add(slotGO);

            // Pop-in animation
            StartCoroutine(PopIn(slotGO.GetComponent<RectTransform>(), 0.15f + i * 0.1f));
        }
    }

    private GameObject CreateAnimalSlot(RectTransform parent, float x, float y, float size, int slotIndex, string animalId)
    {
        var go = new GameObject($"Slot_{slotIndex}");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = new Vector2(x, y);

        // Card border
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

        // Card background
        var bgImg = go.AddComponent<Image>();
        if (cellSprite != null) { bgImg.sprite = cellSprite; bgImg.type = Image.Type.Sliced; }
        bgImg.color = Color.white;
        bgImg.raycastTarget = true;

        // Animal image
        var animalGO = new GameObject("Animal");
        animalGO.transform.SetParent(go.transform, false);
        var art = animalGO.AddComponent<RectTransform>();
        art.anchorMin = new Vector2(0.08f, 0.08f);
        art.anchorMax = new Vector2(0.92f, 0.92f);
        art.offsetMin = Vector2.zero;
        art.offsetMax = Vector2.zero;
        var animalImg = animalGO.AddComponent<Image>();
        animalImg.preserveAspect = true;
        animalImg.raycastTarget = false;
        animalImg.color = Color.white;

        // Load animal animation
        var animData = AnimalAnimData.Load(animalId);
        if (animData != null && animData.idleFrames != null && animData.idleFrames.Length > 0)
        {
            animalImg.sprite = animData.idleFrames[0];
            var anim = animalGO.AddComponent<UISpriteAnimator>();
            anim.targetImage = animalImg;
            anim.idleFrames = animData.idleFrames;
            anim.floatingFrames = animData.floatingFrames;
            anim.successFrames = animData.successFrames;
            anim.framesPerSecond = animData.idleFps > 0 ? animData.idleFps : 12f;
            anim.PlayIdle();
            _animators[slotIndex] = anim;
        }
        else
        {
            // Fallback static sprite
            var sprite = Resources.Load<Sprite>($"AnimalSprites/{animalId}");
            if (sprite != null) animalImg.sprite = sprite;
        }

        // Button
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bgImg;
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = HexColor("#F5F5F5");
        colors.pressedColor = HexColor("#EEEEEE");
        btn.colors = colors;

        int captured = slotIndex;
        btn.onClick.AddListener(() => OnSlotTapped(captured));

        return go;
    }

    // ── INPUT HANDLING ──

    private void OnSlotTapped(int slotIndex)
    {
        if (!_roundActive) return;
        _lastInteractionTime = Time.time;
        _attemptsThisRound++;

        if (slotIndex == _oddIndex)
        {
            // Correct!
            _roundActive = false;
            _stats.RecordCorrect("odd_found", _oddAnimal);
            _stats.SetCustom("attemptsThisRound", (float)_attemptsThisRound);
            _stats.SetCustom("hintsUsed", (float)_hintsUsed);
            _score++;
            StartCoroutine(OnCorrectSequence(slotIndex));
        }
        else
        {
            // Wrong — soft feedback, keep round active
            _stats.RecordMistake("wrong_slot", _majorAnimal);
            StartCoroutine(ShakeSlot(_slotObjects[slotIndex]));

            // After 2 wrong taps, hint
            if (_attemptsThisRound >= 3 && _attemptsThisRound % 2 == 1)
            {
                _hintsUsed++;
                _stats.RecordHint();
                StartCoroutine(PulseHint(_slotObjects[_oddIndex]));
            }
        }
    }

    // ── CORRECT SEQUENCE ──

    private IEnumerator OnCorrectSequence(int correctSlot)
    {
        // Highlight correct card green
        var bg = _slotObjects[correctSlot].GetComponent<Image>();
        if (bg != null) bg.color = HexColor("#C8E6C9");

        // Play success animation on the odd animal
        if (_animators[correctSlot] != null)
            _animators[correctSlot].PlaySuccess();

        // Dim the other 3 cards
        for (int i = 0; i < 4; i++)
        {
            if (i == correctSlot) continue;
            var img = _slotObjects[i].GetComponent<Image>();
            if (img != null)
            {
                var c = img.color;
                c.a = 0.4f;
                img.color = c;
            }
        }

        SoundLibrary.PlayRandomFeedback();

        yield return new WaitForSeconds(0.4f);

        // Play animal name of the odd one
        SoundLibrary.PlayAnimalName(_oddAnimal);

        yield return new WaitForSeconds(1.0f);

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
                _score = 0;
                LoadRound();
            }
        }
        else
        {
            yield return new WaitForSeconds(1.2f);
            LoadRound();
        }
    }

    // ── ANIMATIONS ──

    private IEnumerator ShakeSlot(GameObject go)
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
        while (_roundActive)
        {
            yield return new WaitForSeconds(1f);
            if (_roundActive && Time.time - _lastInteractionTime >= 6f)
            {
                _hintsUsed++;
                _stats.RecordHint();
                StartCoroutine(PulseHint(_slotObjects[_oddIndex]));
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

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
