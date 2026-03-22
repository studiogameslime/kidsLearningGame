using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Animal Counting mini-game (landscape). Children count animals on screen
/// and tap the correct number from 3 choices. Features animated animals,
/// predefined layout patterns, counting celebration, and difficulty scaling.
///
/// Difficulty: 0=Easy (1-3), 1=Medium (1-5), 2=Hard (1-8)
/// </summary>
public class CountingGameController : BaseMiniGame
{
    [Header("UI References")]
    public RectTransform animalArea;
    public RectTransform buttonArea;
    public TextMeshProUGUI questionText;
    public Image questionAnimalIcon;
    public TextMeshProUGUI countNumberText;

    [Header("Settings")]
    public float animalSize = 240f;
    public float buttonSize = 160f;

    [Header("Sprites")]
    public Sprite circleSprite;

    private List<GameObject> spawnedAnimals = new List<GameObject>();
    private List<UISpriteAnimator> animalAnimators = new List<UISpriteAnimator>();
    private List<GameObject> answerButtons = new List<GameObject>();
    private int correctAnswer;
    private string currentAnimalId;
    private Coroutine idleAnimCoroutine;
    private int _minCount;
    private int _maxCount;

    // Button colors
    private static readonly Color ButtonColor = new Color(0.98f, 0.98f, 1f, 1f);
    private static readonly Color ButtonBorderColor = new Color(0.75f, 0.82f, 0.92f, 1f);
    private static readonly Color CorrectColor = new Color(0.55f, 0.88f, 0.55f, 1f);
    private static readonly Color WrongColor = new Color(1f, 0.55f, 0.55f, 1f);

    // Counting label colors per number
    private static readonly Color[] NumberDarkColors = {
        new Color(0.9f, 0.22f, 0.21f),   // 1 red
        new Color(0.30f, 0.69f, 0.31f),   // 2 green
        new Color(0.25f, 0.47f, 0.85f),   // 3 blue
        new Color(0.61f, 0.32f, 0.79f),   // 4 purple
        new Color(1f, 0.60f, 0f),         // 5 orange
        new Color(0.0f, 0.74f, 0.83f),    // 6 teal
        new Color(0.85f, 0.26f, 0.56f),   // 7 pink
        new Color(0.48f, 0.71f, 0f),      // 8 lime
    };
    // Hebrew animal names
    private static readonly Dictionary<string, string> AnimalHebrew = new Dictionary<string, string>
    {
        {"Cat","\u05D7\u05EA\u05D5\u05DC"},{"Dog","\u05DB\u05DC\u05D1"},{"Bear","\u05D3\u05D5\u05D1"},
        {"Duck","\u05D1\u05E8\u05D5\u05D6"},{"Fish","\u05D3\u05D2"},{"Frog","\u05E6\u05E4\u05E8\u05D3\u05E2"},
        {"Bird","\u05E6\u05D9\u05E4\u05D5\u05E8"},{"Cow","\u05E4\u05E8\u05D4"},{"Horse","\u05E1\u05D5\u05E1"},
        {"Lion","\u05D0\u05E8\u05D9\u05D4"},{"Monkey","\u05E7\u05D5\u05E3"},{"Elephant","\u05E4\u05D9\u05DC"},
        {"Giraffe","\u05D2\u05F3\u05D9\u05E8\u05E4\u05D4"},{"Zebra","\u05D6\u05D1\u05E8\u05D4"},
        {"Turtle","\u05E6\u05D1"},{"Snake","\u05E0\u05D7\u05E9"},{"Sheep","\u05DB\u05D1\u05E9\u05D4"},
        {"Chicken","\u05EA\u05E8\u05E0\u05D2\u05D5\u05DC"},{"Donkey","\u05D7\u05DE\u05D5\u05E8"}
    };

    // ── BASE MINI GAME HOOKS ──

    protected override string GetFallbackGameId() => "counting";

    protected override void OnGameInit()
    {
        totalRounds = 1;
        isEndless = true;
        playWinSound = true;
        playConfettiOnRoundWin = true;
        delayBeforeNextRound = 0.2f;

        GameDifficultyConfig.CountingRange(Difficulty, out _minCount, out _maxCount);
    }

    protected override void OnRoundSetup()
    {
        correctAnswer = Random.Range(_minCount, _maxCount + 1);
        Debug.Log($"[Difficulty] Game=counting Level={Difficulty} Animals={correctAnswer} Range={_minCount}-{_maxCount}");

        // Pick random animal
        var animalInfo = PickRandomAnimal();
        if (animalInfo.sprite == null)
        {
            Debug.LogError("CountingGame: No animal sprites available!");
            return;
        }
        currentAnimalId = animalInfo.id;

        // Update question text
        UpdateQuestion(animalInfo.id);

        // Hide header animal icon (not needed with static title)
        if (questionAnimalIcon != null)
            questionAnimalIcon.gameObject.SetActive(false);

        // Load animation data
        AnimalAnimData animData = null;
        if (!string.IsNullOrEmpty(animalInfo.id))
            animData = AnimalAnimData.Load(animalInfo.id);

        // Place animals using predefined layout
        Vector2[] positions = GetLayoutPositions(correctAnswer);
        for (int i = 0; i < correctAnswer; i++)
        {
            var go = new GameObject($"Animal_{i}");
            go.transform.SetParent(animalArea, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(animalSize, animalSize);
            rt.anchoredPosition = positions[i];

            var img = go.AddComponent<Image>();
            img.sprite = animalInfo.sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;

            // Add animator if we have animation data
            UISpriteAnimator animator = null;
            if (animData != null && animData.idleFrames != null && animData.idleFrames.Length > 0)
            {
                animator = go.AddComponent<UISpriteAnimator>();
                animator.targetImage = img;
                animator.idleFrames = animData.idleFrames;
                animator.floatingFrames = animData.floatingFrames;
                animator.successFrames = animData.successFrames;
                animator.framesPerSecond = animData.idleFps > 0 ? animData.idleFps : 12f;
                // Don't start idle yet — wait for pop-in
            }

            spawnedAnimals.Add(go);
            animalAnimators.Add(animator);
            StartCoroutine(PopIn(rt, i * 0.12f, animator));
        }

        // Create answer buttons
        CreateAnswerButtons();

        // Position tutorial hand on the first animal
        StartCoroutine(PositionTutorialHandOnFirstAnimal());

        // Start occasional idle animations
        idleAnimCoroutine = StartCoroutine(RandomIdleAnimations());
    }

    protected override void OnRoundCleanup()
    {
        if (idleAnimCoroutine != null)
        {
            StopCoroutine(idleAnimCoroutine);
            idleAnimCoroutine = null;
        }

        foreach (var go in spawnedAnimals)
            if (go != null) Destroy(go);
        spawnedAnimals.Clear();
        animalAnimators.Clear();

        foreach (var go in answerButtons)
            if (go != null) Destroy(go);
        answerButtons.Clear();

        // Hide counting number
        if (countNumberText != null)
            countNumberText.gameObject.SetActive(false);
    }

    protected override void OnBeforeComplete()
    {
        Stats?.SetCustom("correctAnswer", correctAnswer);
        Stats?.SetCustom("animalCount", correctAnswer);
    }

    private IEnumerator CountThenComplete()
    {
        // Stop idle animations
        if (idleAnimCoroutine != null)
        {
            StopCoroutine(idleAnimCoroutine);
            idleAnimCoroutine = null;
        }

        // Sort animals left-to-right for counting order
        List<int> sortedIndices = new List<int>();
        for (int i = 0; i < spawnedAnimals.Count; i++)
            sortedIndices.Add(i);
        sortedIndices.Sort((a, b) =>
        {
            var posA = spawnedAnimals[a].GetComponent<RectTransform>().anchoredPosition;
            var posB = spawnedAnimals[b].GetComponent<RectTransform>().anchoredPosition;
            float rowA = Mathf.Round(posA.y / 50f);
            float rowB = Mathf.Round(posB.y / 50f);
            if (rowA != rowB) return rowB.CompareTo(rowA);
            return posA.x.CompareTo(posB.x);
        });

        // Show large counting number at top center
        if (countNumberText != null)
        {
            countNumberText.text = "";
            countNumberText.gameObject.SetActive(true);
            countNumberText.transform.localScale = Vector3.one;
        }

        yield return new WaitForSeconds(0.4f);

        // Count through: number sound + animate each animal
        for (int i = 0; i < sortedIndices.Count; i++)
        {
            int idx = sortedIndices[i];
            int currentCount = i + 1;
            int colorIdx = i % NumberDarkColors.Length;

            // Play number sound (1, 2, 3...)
            SoundLibrary.PlayNumberName(currentCount);

            // Update the single large counting number
            if (countNumberText != null)
            {
                countNumberText.text = currentCount.ToString();
                countNumberText.color = NumberDarkColors[colorIdx];
                StartCoroutine(BounceOnce(countNumberText.rectTransform));
            }

            // Highlight the current animal
            var animalImg = spawnedAnimals[idx].GetComponent<Image>();
            if (animalImg != null)
                animalImg.color = new Color(1f, 1f, 0.7f, 1f);

            // Play success animation on this animal
            if (idx < animalAnimators.Count && animalAnimators[idx] != null)
                animalAnimators[idx].PlaySuccess();

            // Bounce the animal
            var animalRT = spawnedAnimals[idx].GetComponent<RectTransform>();
            StartCoroutine(CountBounce(animalRT));

            yield return new WaitForSeconds(1.1f);

            // Return animal to normal color
            if (animalImg != null)
                animalImg.color = Color.white;
        }

        yield return new WaitForSeconds(0.3f);

        // Now trigger confetti + feedback sound
        CompleteRound();
    }

    protected override IEnumerator OnAfterComplete()
    {
        yield return new WaitForSeconds(0.8f);

        // Exit animation
        yield return StartCoroutine(ExitAnimals());

        yield return new WaitForSeconds(0.2f);
    }

    private IEnumerator PositionTutorialHandOnFirstAnimal()
    {
        // Wait for pop-in animations to start, then position on first animal
        yield return null;

        if (TutorialHand != null && spawnedAnimals.Count > 0)
        {
            var firstAnimalRT = spawnedAnimals[0].GetComponent<RectTransform>();
            Vector2 localPos = TutorialHand.GetLocalCenter(firstAnimalRT);
            TutorialHand.SetPosition(localPos);
        }
    }

    // ── QUESTION TEXT ──

    private void UpdateQuestion(string animalId)
    {
        if (questionText == null) return;

        // Always show static title: "כמה חיות יש?"
        HebrewText.SetText(questionText, "\u05DB\u05DE\u05D4 \u05D7\u05D9\u05D5\u05EA \u05D9\u05E9?");
    }

    // ── DIFFICULTY ──

    private int GetMaxCount()
    {
        return _maxCount;
    }

    // ── LAYOUT PATTERNS ──

    /// <summary>
    /// Returns predefined positions (in local space of animalArea) for the given count.
    /// All positions are designed to be well-spaced and visually balanced.
    /// </summary>
    private Vector2[] GetLayoutPositions(int count)
    {
        float aW = animalArea.rect.width;
        float aH = animalArea.rect.height;
        float s = animalSize * 1.15f; // spacing unit (animal size + gap)

        switch (count)
        {
            case 1:
                return new[] { Vector2.zero };

            case 2:
                return new[] {
                    new Vector2(-s * 0.6f, 0),
                    new Vector2(s * 0.6f, 0)
                };

            case 3:
                // Gentle arc
                return new[] {
                    new Vector2(-s * 0.85f, -s * 0.1f),
                    new Vector2(0, s * 0.2f),
                    new Vector2(s * 0.85f, -s * 0.1f)
                };

            case 4:
                // 2x2 grid
                float g4 = s * 0.55f;
                return new[] {
                    new Vector2(-g4, g4),
                    new Vector2(g4, g4),
                    new Vector2(-g4, -g4),
                    new Vector2(g4, -g4)
                };

            case 5:
                // 3 top + 2 bottom
                float g5 = s * 0.7f;
                return new[] {
                    new Vector2(-g5, g5 * 0.5f),
                    new Vector2(0, g5 * 0.5f),
                    new Vector2(g5, g5 * 0.5f),
                    new Vector2(-g5 * 0.5f, -g5 * 0.6f),
                    new Vector2(g5 * 0.5f, -g5 * 0.6f)
                };

            case 6:
                // 3x2 grid
                float g6 = s * 0.65f;
                return new[] {
                    new Vector2(-g6, g6 * 0.55f),
                    new Vector2(0, g6 * 0.55f),
                    new Vector2(g6, g6 * 0.55f),
                    new Vector2(-g6, -g6 * 0.55f),
                    new Vector2(0, -g6 * 0.55f),
                    new Vector2(g6, -g6 * 0.55f)
                };

            case 7:
                // 4 top + 3 bottom
                float g7 = s * 0.6f;
                return new[] {
                    new Vector2(-g7 * 1.5f, g7 * 0.55f),
                    new Vector2(-g7 * 0.5f, g7 * 0.55f),
                    new Vector2(g7 * 0.5f, g7 * 0.55f),
                    new Vector2(g7 * 1.5f, g7 * 0.55f),
                    new Vector2(-g7, -g7 * 0.55f),
                    new Vector2(0, -g7 * 0.55f),
                    new Vector2(g7, -g7 * 0.55f)
                };

            case 8:
                // 4x2 grid
                float g8 = s * 0.55f;
                return new[] {
                    new Vector2(-g8 * 1.5f, g8 * 0.55f),
                    new Vector2(-g8 * 0.5f, g8 * 0.55f),
                    new Vector2(g8 * 0.5f, g8 * 0.55f),
                    new Vector2(g8 * 1.5f, g8 * 0.55f),
                    new Vector2(-g8 * 1.5f, -g8 * 0.55f),
                    new Vector2(-g8 * 0.5f, -g8 * 0.55f),
                    new Vector2(g8 * 0.5f, -g8 * 0.55f),
                    new Vector2(g8 * 1.5f, -g8 * 0.55f)
                };

            default:
                // Fallback: grid layout
                return GenerateGridPositions(count, s);
        }
    }

    private Vector2[] GenerateGridPositions(int count, float spacing)
    {
        int cols = Mathf.CeilToInt(Mathf.Sqrt(count * 1.5f));
        int rows = Mathf.CeilToInt((float)count / cols);
        var positions = new Vector2[count];
        int idx = 0;
        for (int r = 0; r < rows && idx < count; r++)
        {
            int colsInRow = Mathf.Min(cols, count - idx);
            float rowW = (colsInRow - 1) * spacing;
            float startX = -rowW / 2f;
            float y = ((rows - 1) / 2f - r) * spacing * 0.65f;
            for (int c = 0; c < colsInRow && idx < count; c++)
            {
                positions[idx] = new Vector2(startX + c * spacing, y);
                idx++;
            }
        }
        return positions;
    }

    // ── ANSWER BUTTONS ──

    private void CreateAnswerButtons()
    {
        List<int> options = GenerateAnswerOptions();

        float spacing = Difficulty <= 4 ? 60f : 40f;
        float totalW = options.Count * buttonSize + (options.Count - 1) * spacing;
        float startX = -totalW / 2f + buttonSize / 2f;

        for (int i = 0; i < options.Count; i++)
        {
            int number = options[i];
            var go = new GameObject($"Btn_{number}");
            go.transform.SetParent(buttonArea, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(buttonSize, buttonSize);
            rt.anchoredPosition = new Vector2(startX + i * (buttonSize + spacing), 0);

            var img = go.AddComponent<Image>();
            if (circleSprite != null) img.sprite = circleSprite;
            img.color = ButtonColor;
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            int captured = number;
            btn.onClick.AddListener(() => OnNumberTapped(captured, go));

            // Outline ring
            var outlineGO = new GameObject("Outline");
            outlineGO.transform.SetParent(go.transform, false);
            var outlineRT = outlineGO.AddComponent<RectTransform>();
            outlineRT.anchorMin = Vector2.zero;
            outlineRT.anchorMax = Vector2.one;
            outlineRT.offsetMin = new Vector2(-4, -4);
            outlineRT.offsetMax = new Vector2(4, 4);
            var outlineImg = outlineGO.AddComponent<Image>();
            if (circleSprite != null) outlineImg.sprite = circleSprite;
            outlineImg.color = ButtonBorderColor;
            outlineImg.raycastTarget = false;
            outlineGO.transform.SetAsFirstSibling();

            // Number text
            var txtGO = new GameObject("Text");
            txtGO.transform.SetParent(go.transform, false);
            var txtRT = txtGO.AddComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = Vector2.zero;
            txtRT.offsetMax = Vector2.zero;
            var tmp = txtGO.AddComponent<TextMeshProUGUI>();
            tmp.text = number.ToString();
            tmp.fontSize = 72;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(0.2f, 0.3f, 0.5f, 1f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            answerButtons.Add(go);

            // Pop in animation
            StartCoroutine(PopIn(rt, 0.3f + i * 0.08f, null));
        }
    }

    private List<int> GenerateAnswerOptions()
    {
        List<int> options = new List<int> { correctAnswer };
        int maxVal = GetMaxCount();

        // Generate 2 wrong answers close to the correct value
        int attempts = 0;
        while (options.Count < 3 && attempts < 50)
        {
            // Pick within ±2 of correct, clamped to valid range
            int wrong = correctAnswer + Random.Range(-2, 3);
            if (wrong < 1) wrong = 1;
            if (wrong > maxVal) wrong = maxVal;
            if (!options.Contains(wrong))
                options.Add(wrong);
            attempts++;
        }

        // If we still don't have 3 (e.g. correctAnswer=1, maxVal=1), fill with nearby
        attempts = 0;
        while (options.Count < 3 && attempts < 20)
        {
            int v = Random.Range(1, Mathf.Max(maxVal + 1, correctAnswer + 3));
            if (!options.Contains(v))
                options.Add(v);
            attempts++;
        }

        // Shuffle
        for (int i = options.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = options[i]; options[i] = options[j]; options[j] = tmp;
        }

        return options;
    }

    // ── INPUT ──

    private void OnNumberTapped(int number, GameObject btnGO)
    {
        if (IsInputLocked) return;

        DismissTutorial();

        if (number == correctAnswer)
        {
            RecordCorrect();
            btnGO.GetComponent<Image>().color = CorrectColor;
            // Don't CompleteRound yet — start counting animation first
            StartCoroutine(CountThenComplete());
        }
        else
        {
            RecordMistake();
            StartCoroutine(WrongFeedback(btnGO));
        }
    }

    // ── EXIT ANIMATION ──

    private IEnumerator ExitAnimals()
    {
        // All animals and buttons shrink out simultaneously
        float dur = 0.3f;
        float t = 0f;
        var allObjects = new List<GameObject>(spawnedAnimals);
        allObjects.AddRange(answerButtons);

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float scale = 1f - p;
            foreach (var go in allObjects)
            {
                if (go != null)
                    go.transform.localScale = Vector3.one * scale;
            }
            yield return null;
        }

        // Also hide counting number
        if (countNumberText != null)
            countNumberText.gameObject.SetActive(false);
    }

    // ── WRONG ANSWER ──

    private IEnumerator WrongFeedback(GameObject go)
    {
        var img = go.GetComponent<Image>();
        img.color = WrongColor;
        var rt = go.GetComponent<RectTransform>();
        Vector2 orig = rt.anchoredPosition;
        float dur = 0.35f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float offset = Mathf.Sin(t * 35f) * 12f * (1f - t / dur);
            rt.anchoredPosition = orig + new Vector2(offset, 0);
            yield return null;
        }
        rt.anchoredPosition = orig;
        img.color = ButtonColor;
    }

    // ── ANIMATIONS ──

    private IEnumerator PopIn(RectTransform rt, float delay, UISpriteAnimator animator)
    {
        rt.localScale = Vector3.zero;
        yield return new WaitForSeconds(delay);

        float dur = 0.3f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            // Overshoot elastic-like bounce
            float s = 1f + 0.2f * Mathf.Sin(p * Mathf.PI);
            if (p >= 1f) s = 1f;
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        rt.localScale = Vector3.one;

        // Start idle animation after pop-in
        if (animator != null)
            animator.PlayIdle();
    }

    private IEnumerator BounceOnce(RectTransform rt)
    {
        Vector3 orig = rt.localScale;
        Vector3 big = orig * 1.25f;
        float dur = 0.12f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            rt.localScale = Vector3.Lerp(orig, big, t / dur);
            yield return null;
        }
        t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            rt.localScale = Vector3.Lerp(big, orig, t / dur);
            yield return null;
        }
        rt.localScale = orig;
    }

    /// <summary>
    /// Stronger bounce for the counting celebration — animal jumps up and lands back.
    /// </summary>
    private IEnumerator CountBounce(RectTransform rt)
    {
        Vector2 origPos = rt.anchoredPosition;
        Vector3 origScale = rt.localScale;
        float jumpHeight = 30f;

        // Jump up
        float t = 0f;
        float upDur = 0.12f;
        while (t < upDur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / upDur);
            rt.anchoredPosition = origPos + new Vector2(0, jumpHeight * Mathf.Sin(p * Mathf.PI * 0.5f));
            rt.localScale = origScale * (1f + 0.15f * p);
            yield return null;
        }

        // Fall back down
        t = 0f;
        float downDur = 0.15f;
        while (t < downDur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / downDur);
            rt.anchoredPosition = origPos + new Vector2(0, jumpHeight * (1f - p));
            rt.localScale = origScale * (1.15f - 0.15f * p);
            yield return null;
        }

        rt.anchoredPosition = origPos;
        rt.localScale = origScale;
    }

    /// <summary>
    /// Occasionally trigger idle animations on random animals to keep the scene alive.
    /// Subtle, infrequent, not all at once.
    /// </summary>
    private IEnumerator RandomIdleAnimations()
    {
        yield return new WaitForSeconds(2f);

        while (true)
        {
            float wait = Random.Range(2.5f, 5f);
            yield return new WaitForSeconds(wait);

            if (IsInputLocked || animalAnimators.Count == 0) continue;

            // Pick one random animal to give a small bounce
            int idx = Random.Range(0, spawnedAnimals.Count);
            if (spawnedAnimals[idx] != null)
            {
                var rt = spawnedAnimals[idx].GetComponent<RectTransform>();
                StartCoroutine(GentleBounce(rt));
            }
        }
    }

    private IEnumerator GentleBounce(RectTransform rt)
    {
        Vector3 orig = rt.localScale;
        float dur = 0.4f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float s = 1f + 0.08f * Mathf.Sin(p * Mathf.PI);
            rt.localScale = orig * s;
            yield return null;
        }
        rt.localScale = orig;
    }

    // ── ANIMAL SELECTION ──

    private struct AnimalInfo
    {
        public string id;
        public Sprite sprite;
    }

    private AnimalInfo PickRandomAnimal()
    {
        var game = GameContext.CurrentGame;
        if (game != null && game.subItems != null && game.subItems.Count > 0)
        {
            var item = game.subItems[Random.Range(0, game.subItems.Count)];
            return new AnimalInfo
            {
                id = item.title,
                sprite = item.contentAsset != null ? item.contentAsset : item.thumbnail
            };
        }
        return new AnimalInfo { id = "", sprite = null };
    }

    // ── NAVIGATION ──

    public void OnHomePressed() => ExitGame();
}
