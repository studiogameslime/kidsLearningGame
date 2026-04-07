using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Letter Bubbles mini-game — Hebrew letter bubbles float up from the bottom.
/// Alin says "pop the letter X!" and the child taps bubbles with the correct letter.
/// Inherits BaseMiniGame for round lifecycle, stats, and difficulty.
/// </summary>
public class LetterBubblesController : BaseMiniGame
{
    [Header("Layout")]
    public RectTransform playArea;

    [Header("UI")]
    public TextMeshProUGUI titleText;
    public Image targetLetterBG;
    public TextMeshProUGUI targetLetterText;

    // ── Hebrew alphabet ──
    private static readonly char[] HebrewAlphabet = "אבגדהוזחטיכלמנסעפצקרשת".ToCharArray();

    // ── Confusing letter pairs for hard mode ──
    private static readonly Dictionary<char, char[]> ConfusingLetters = new Dictionary<char, char[]>
    {
        {'ב', new[]{'כ','ב'}}, {'כ', new[]{'ב','כ'}},
        {'ד', new[]{'ר','ד'}}, {'ר', new[]{'ד','ר','ק'}},
        {'ח', new[]{'ה','ח'}}, {'ה', new[]{'ח','ה'}},
        {'ו', new[]{'ז','ו'}}, {'ז', new[]{'ו','ז'}},
        {'ע', new[]{'צ','ע','ס'}}, {'צ', new[]{'ע','צ'}},
        {'ג', new[]{'נ','ג'}}, {'נ', new[]{'ג','נ'}},
        {'ק', new[]{'ר','ק'}}, {'ס', new[]{'ע','ס'}},
    };

    // ── Cheerful bubble color palette ──
    private static readonly Color[] BubbleColors =
    {
        HexColor("#EF5350"), // red
        HexColor("#42A5F5"), // blue
        HexColor("#66BB6A"), // green
        HexColor("#FFEE58"), // yellow
        HexColor("#AB47BC"), // purple
        HexColor("#FF7043"), // orange
        HexColor("#EC407A"), // pink
    };

    // ── Difficulty parameters ──
    private struct DifficultyParams
    {
        public int totalBubbles;
        public int targetCount;
        public float floatSpeed;
        public float bubbleSize;
        public bool useConfusingLetters;
    }

    private static DifficultyParams GetParams(int difficulty)
    {
        if (difficulty <= 3)
            return new DifficultyParams { totalBubbles = 6, targetCount = 2, floatSpeed = 80f, bubbleSize = 140f, useConfusingLetters = false };
        if (difficulty <= 6)
            return new DifficultyParams { totalBubbles = 8, targetCount = 3, floatSpeed = 120f, bubbleSize = 120f, useConfusingLetters = false };
        return new DifficultyParams { totalBubbles = 12, targetCount = 4, floatSpeed = 160f, bubbleSize = 100f, useConfusingLetters = true };
    }

    // ── Runtime state ──
    private DifficultyParams currentParams;
    private char targetLetter;
    private int targetsPoppedThisRound;
    private int targetsNeededThisRound;
    private int lettersCompletedThisRound;
    private const int LettersPerRound = 5;
    private Sprite circleSprite;
    private List<BubbleData> activeBubbles = new List<BubbleData>();
    private HashSet<char> usedLettersThisSession = new HashSet<char>();
    private bool roundActive;

    private class BubbleData
    {
        public GameObject go;
        public RectTransform rt;
        public Image bgImage;
        public TextMeshProUGUI letterTMP;
        public char letter;
        public bool isTarget;
        public float sinePhase;
        public float sineAmp;
        public float sineFreq;
        public Color bubbleColor;
        public bool isPopping;
    }

    // ── BaseMiniGame hooks ──

    protected override string GetFallbackGameId() => "letterbubbles";

    protected override void OnGameInit()
    {
        isEndless = true;
        playConfettiOnRoundWin = false;
        playConfettiOnSessionWin = true;
        playWinSound = true;
        delayBeforeNextRound = 0.3f;

        circleSprite = CreateCircleSprite();
    }

    protected override void OnRoundSetup()
    {
        currentParams = GetParams(Difficulty);

        // Pick a target letter (avoid recently used if possible)
        targetLetter = PickTargetLetter();
        usedLettersThisSession.Add(targetLetter);
        if (usedLettersThisSession.Count >= HebrewAlphabet.Length)
            usedLettersThisSession.Clear();

        targetsPoppedThisRound = 0;
        targetsNeededThisRound = currentParams.targetCount;
        lettersCompletedThisRound = 0;

        // Update target display
        if (targetLetterText != null)
            HebrewText.SetText(targetLetterText, targetLetter.ToString());

        if (targetLetterBG != null)
        {
            targetLetterBG.gameObject.SetActive(true);
            // Pulse animation on new round
            StartCoroutine(PulseTargetDisplay());
        }

        // Play "פוצצו את האות..." then the letter name
        StartCoroutine(PlayLetterInstruction(targetLetter));

        // Clear old bubbles
        ClearAllBubbles();

        // Spawn initial bubbles
        SpawnAllBubbles();

        roundActive = true;
    }

    protected override void OnRoundCleanup()
    {
        roundActive = false;
        ClearAllBubbles();
    }

    protected override void OnGameplayUpdate()
    {
        if (!roundActive || IsInputLocked) return;

        float areaHeight = playArea.rect.height;
        float areaWidth = playArea.rect.width;
        float speed = currentParams.floatSpeed;
        float dt = Time.deltaTime;

        // Track which bubbles went off screen
        List<BubbleData> toRespawn = new List<BubbleData>();

        for (int i = activeBubbles.Count - 1; i >= 0; i--)
        {
            var b = activeBubbles[i];
            if (b.isPopping || b.go == null) continue;

            // Move upward
            var pos = b.rt.anchoredPosition;
            pos.y += speed * dt;

            // Sine wobble
            float wobble = Mathf.Sin(Time.time * b.sineFreq + b.sinePhase) * b.sineAmp;
            float baseX = pos.x; // Keep track via the stored position approach
            pos.x += wobble * dt; // incremental wobble

            // Clamp X to stay in bounds
            float halfSize = currentParams.bubbleSize * 0.5f;
            pos.x = Mathf.Clamp(pos.x, halfSize, areaWidth - halfSize);

            b.rt.anchoredPosition = pos;

            // Check if off the top
            if (pos.y > areaHeight + currentParams.bubbleSize)
            {
                toRespawn.Add(b);
            }
        }

        // Respawn bubbles that went off screen
        foreach (var b in toRespawn)
        {
            RespawnBubble(b);
        }

        // Ensure we always have the right number of target bubbles on screen
        EnsureTargetBubbles();
    }

    public void OnHomePressed()
    {
        ExitGame();
    }

    // ── Bubble management ──

    private void SpawnAllBubbles()
    {
        float areaWidth = playArea.rect.width;
        float areaHeight = playArea.rect.height;
        int totalBubbles = currentParams.totalBubbles;
        int targetCount = currentParams.targetCount;

        // Determine distractor letters
        char[] distractorPool = GetDistractorPool();

        for (int i = 0; i < totalBubbles; i++)
        {
            bool isTarget = i < targetCount;
            char letter = isTarget ? targetLetter : distractorPool[Random.Range(0, distractorPool.Length)];

            // Start from below the screen, staggered so they rise in gradually
            float x = Random.Range(currentParams.bubbleSize * 0.5f, areaWidth - currentParams.bubbleSize * 0.5f);
            float y = Random.Range(-areaHeight * 0.3f, -currentParams.bubbleSize);

            var bubble = CreateBubble(letter, isTarget, new Vector2(x, y));
            activeBubbles.Add(bubble);
        }
    }

    private char[] GetDistractorPool()
    {
        if (currentParams.useConfusingLetters && ConfusingLetters.ContainsKey(targetLetter))
        {
            // Mix confusing letters with some random ones
            var pool = new List<char>(ConfusingLetters[targetLetter]);
            // Add some random letters too
            for (int i = 0; i < 4; i++)
            {
                char c = HebrewAlphabet[Random.Range(0, HebrewAlphabet.Length)];
                if (c != targetLetter && !pool.Contains(c))
                    pool.Add(c);
            }
            return pool.ToArray();
        }
        else
        {
            // Random letters excluding target
            var pool = new List<char>();
            foreach (char c in HebrewAlphabet)
            {
                if (c != targetLetter)
                    pool.Add(c);
            }
            return pool.ToArray();
        }
    }

    private BubbleData CreateBubble(char letter, bool isTarget, Vector2 position)
    {
        float size = currentParams.bubbleSize;
        Color bubbleColor = BubbleColors[Random.Range(0, BubbleColors.Length)];

        // Root bubble object
        var go = new GameObject($"Bubble_{letter}");
        go.transform.SetParent(playArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = position;

        // Background circle (semi-transparent)
        var bgImg = go.AddComponent<Image>();
        bgImg.sprite = circleSprite;
        bgImg.color = new Color(bubbleColor.r, bubbleColor.g, bubbleColor.b, 0.7f);

        // Shine highlight (top-left arc)
        var shineGO = new GameObject("Shine");
        shineGO.transform.SetParent(go.transform, false);
        var shineRT = shineGO.AddComponent<RectTransform>();
        shineRT.anchorMin = new Vector2(0.15f, 0.5f);
        shineRT.anchorMax = new Vector2(0.55f, 0.85f);
        shineRT.offsetMin = Vector2.zero;
        shineRT.offsetMax = Vector2.zero;
        var shineImg = shineGO.AddComponent<Image>();
        shineImg.sprite = circleSprite;
        shineImg.color = new Color(1f, 1f, 1f, 0.35f);
        shineImg.raycastTarget = false;

        // Rim (slightly larger circle behind, visible at edges)
        var rimGO = new GameObject("Rim");
        rimGO.transform.SetParent(go.transform, false);
        rimGO.transform.SetAsFirstSibling();
        var rimRT = rimGO.AddComponent<RectTransform>();
        rimRT.anchorMin = new Vector2(-0.04f, -0.04f);
        rimRT.anchorMax = new Vector2(1.04f, 1.04f);
        rimRT.offsetMin = Vector2.zero;
        rimRT.offsetMax = Vector2.zero;
        var rimImg = rimGO.AddComponent<Image>();
        rimImg.sprite = circleSprite;
        rimImg.color = new Color(bubbleColor.r * 0.8f, bubbleColor.g * 0.8f, bubbleColor.b * 0.8f, 0.5f);
        rimImg.raycastTarget = false;

        // Letter text
        var textGO = new GameObject("LetterText");
        textGO.transform.SetParent(go.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;
        var textTMP = textGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(textTMP, letter.ToString());
        textTMP.fontSize = size * 0.45f;
        textTMP.fontStyle = FontStyles.Bold;
        textTMP.color = Color.white;
        textTMP.alignment = TextAlignmentOptions.Center;
        textTMP.raycastTarget = false;
        textTMP.enableWordWrapping = false;
        // Add outline for better readability
        textTMP.outlineWidth = 0.15f;
        textTMP.outlineColor = new Color(0, 0, 0, 0.3f);

        // Button for tap detection
        var btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        var bubbleData = new BubbleData
        {
            go = go,
            rt = rt,
            bgImage = bgImg,
            letterTMP = textTMP,
            letter = letter,
            isTarget = isTarget,
            sinePhase = Random.Range(0f, Mathf.PI * 2f),
            sineAmp = Random.Range(20f, 50f),
            sineFreq = Random.Range(1.5f, 3f),
            bubbleColor = bubbleColor,
            isPopping = false,
        };

        btn.onClick.AddListener(() => OnBubbleTapped(bubbleData));

        // Entry scale animation
        go.transform.localScale = Vector3.zero;
        StartCoroutine(ScaleIn(go.transform));

        return bubbleData;
    }

    private void OnBubbleTapped(BubbleData bubble)
    {
        if (IsInputLocked || !roundActive || bubble.isPopping) return;
        if (bubble.go == null) return;

        DismissTutorial();

        if (bubble.letter == targetLetter)
        {
            // Correct!
            bubble.isPopping = true;
            targetsPoppedThisRound++;

            bool isLast = targetsPoppedThisRound >= targetsNeededThisRound;
            RecordCorrect(targetId: targetLetter.ToString(), isLast: isLast);
            PlayCorrectEffect(bubble.rt);
            ShowFloatingScore(bubble.rt);

            // Pop with particles
            SpawnPopParticles(bubble);
            StartCoroutine(PopBubble(bubble));

            if (isLast)
            {
                lettersCompletedThisRound++;
                if (lettersCompletedThisRound >= LettersPerRound)
                {
                    // All 5 letters done — complete the round
                    roundActive = false;
                    StartCoroutine(CompleteAfterDelay(0.5f));
                }
                else
                {
                    // Switch to next letter seamlessly
                    StartCoroutine(SwitchLetterInline());
                }
            }
        }
        else
        {
            // Wrong — shake + flash, don't pop
            RecordMistake(targetId: targetLetter.ToString());
            PlayWrongEffect(bubble.rt);
            StartCoroutine(FlashBubble(bubble));
        }
    }

    private IEnumerator SwitchLetterInline()
    {
        yield return new WaitForSeconds(0.4f);

        // Pick new letter
        targetLetter = PickTargetLetter();
        usedLettersThisSession.Add(targetLetter);
        if (usedLettersThisSession.Count >= HebrewAlphabet.Length)
            usedLettersThisSession.Clear();

        targetsPoppedThisRound = 0;

        // Update display
        if (targetLetterText != null)
            HebrewText.SetText(targetLetterText, targetLetter.ToString());

        // Play "פוצצו את האות..." then the letter name
        StartCoroutine(PlayLetterInstruction(targetLetter));

        // Pulse target display
        StartCoroutine(PulseTargetDisplay());

        // Replace all existing bubble letters (swap non-target bubbles to new random + add new targets)
        ReassignBubbleLetters();
    }

    private void ReassignBubbleLetters()
    {
        int targetCount = 0;
        foreach (var b in activeBubbles)
        {
            if (b.isPopping || b.go == null) continue;

            if (targetCount < targetsNeededThisRound && Random.value < 0.35f)
            {
                // Make this a target bubble
                b.letter = targetLetter;
                b.isTarget = true;
                targetCount++;
            }
            else
            {
                char[] pool = GetDistractorPool();
                b.letter = pool[Random.Range(0, pool.Length)];
                b.isTarget = false;
            }
            b.letterTMP.text = b.letter.ToString();
        }

        // Ensure minimum targets
        if (targetCount < targetsNeededThisRound)
        {
            foreach (var b in activeBubbles)
            {
                if (b.isPopping || b.go == null || b.isTarget) continue;
                b.letter = targetLetter;
                b.isTarget = true;
                b.letterTMP.text = b.letter.ToString();
                targetCount++;
                if (targetCount >= targetsNeededThisRound) break;
            }
        }
    }

    private IEnumerator CompleteAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        CompleteRound();
    }

    private void RespawnBubble(BubbleData bubble)
    {
        if (bubble.go == null) return;

        float areaWidth = playArea.rect.width;
        float halfSize = currentParams.bubbleSize * 0.5f;

        // Determine if we need another target
        int activeTargets = CountActiveTargets();
        bool makeTarget = bubble.isTarget || (activeTargets < targetsNeededThisRound - targetsPoppedThisRound);

        char newLetter;
        if (makeTarget)
        {
            newLetter = targetLetter;
            bubble.isTarget = true;
        }
        else
        {
            char[] pool = GetDistractorPool();
            newLetter = pool[Random.Range(0, pool.Length)];
            bubble.isTarget = false;
        }

        bubble.letter = newLetter;
        HebrewText.SetText(bubble.letterTMP, newLetter.ToString());

        // New position at bottom
        float x = Random.Range(halfSize, areaWidth - halfSize);
        bubble.rt.anchoredPosition = new Vector2(x, -currentParams.bubbleSize);

        // New random color
        Color newColor = BubbleColors[Random.Range(0, BubbleColors.Length)];
        bubble.bubbleColor = newColor;
        bubble.bgImage.color = new Color(newColor.r, newColor.g, newColor.b, 0.7f);

        // New sine params
        bubble.sinePhase = Random.Range(0f, Mathf.PI * 2f);
        bubble.sineAmp = Random.Range(20f, 50f);
        bubble.sineFreq = Random.Range(1.5f, 3f);

        bubble.go.transform.localScale = Vector3.one;
        bubble.isPopping = false;
    }

    private void EnsureTargetBubbles()
    {
        int needed = targetsNeededThisRound - targetsPoppedThisRound;
        if (needed <= 0) return;

        int activeTargets = CountActiveTargets();
        if (activeTargets >= needed) return;

        // Convert some distractor bubbles to targets
        int deficit = needed - activeTargets;
        foreach (var b in activeBubbles)
        {
            if (deficit <= 0) break;
            if (b.isTarget || b.isPopping || b.go == null) continue;

            b.isTarget = true;
            b.letter = targetLetter;
            HebrewText.SetText(b.letterTMP, targetLetter.ToString());
            deficit--;
        }
    }

    private int CountActiveTargets()
    {
        int count = 0;
        foreach (var b in activeBubbles)
        {
            if (b.isTarget && !b.isPopping && b.go != null)
                count++;
        }
        return count;
    }

    private void ClearAllBubbles()
    {
        foreach (var b in activeBubbles)
        {
            if (b.go != null)
                Destroy(b.go);
        }
        activeBubbles.Clear();
    }

    private char PickTargetLetter()
    {
        // Prefer letters not recently used
        var candidates = new List<char>();
        foreach (char c in HebrewAlphabet)
        {
            if (!usedLettersThisSession.Contains(c))
                candidates.Add(c);
        }

        if (candidates.Count == 0)
        {
            // All used, pick any
            return HebrewAlphabet[Random.Range(0, HebrewAlphabet.Length)];
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    // ── Visual effects ──

    private IEnumerator ScaleIn(Transform t)
    {
        float elapsed = 0f;
        float dur = 0.3f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / dur;
            float s = progress < 0.7f
                ? Mathf.Lerp(0f, 1.15f, progress / 0.7f)
                : Mathf.Lerp(1.15f, 1f, (progress - 0.7f) / 0.3f);
            t.localScale = Vector3.one * s;
            yield return null;
        }
        t.localScale = Vector3.one;
    }

    private IEnumerator PopBubble(BubbleData bubble)
    {
        if (bubble.go == null) yield break;

        // Quick scale up
        float elapsed = 0f;
        float dur = 0.08f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            if (bubble.go != null)
                bubble.go.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 1.3f, elapsed / dur);
            yield return null;
        }

        // Hide
        if (bubble.go != null)
        {
            bubble.go.transform.localScale = Vector3.zero;
            SetBubbleVisibility(bubble, false);
        }

        yield return new WaitForSeconds(0.8f);

        // Remove from active list or respawn as distractor if round still active
        if (roundActive && bubble.go != null)
        {
            SetBubbleVisibility(bubble, true);
            // Respawn as distractor
            bubble.isTarget = false;
            char[] pool = GetDistractorPool();
            bubble.letter = pool[Random.Range(0, pool.Length)];
            HebrewText.SetText(bubble.letterTMP, bubble.letter.ToString());

            float areaWidth = playArea.rect.width;
            float halfSize = currentParams.bubbleSize * 0.5f;
            float x = Random.Range(halfSize, areaWidth - halfSize);
            bubble.rt.anchoredPosition = new Vector2(x, -currentParams.bubbleSize);

            Color newColor = BubbleColors[Random.Range(0, BubbleColors.Length)];
            bubble.bubbleColor = newColor;
            bubble.bgImage.color = new Color(newColor.r, newColor.g, newColor.b, 0.7f);

            bubble.isPopping = false;
            bubble.go.transform.localScale = Vector3.zero;
            StartCoroutine(ScaleIn(bubble.go.transform));
        }
    }

    private IEnumerator FlashBubble(BubbleData bubble)
    {
        if (bubble.go == null) yield break;
        var img = bubble.bgImage;
        Color original = img.color;
        Color flashColor = new Color(1f, 0.3f, 0.3f, 0.9f);

        // Flash red briefly
        img.color = flashColor;
        yield return new WaitForSeconds(0.15f);
        if (img != null) img.color = original;
    }

    private void SetBubbleVisibility(BubbleData bubble, bool visible)
    {
        if (bubble.go == null) return;
        var images = bubble.go.GetComponentsInChildren<Image>(true);
        foreach (var img in images)
            img.enabled = visible;
        var texts = bubble.go.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var txt in texts)
            txt.enabled = visible;
    }

    private IEnumerator PlayLetterInstruction(char letter)
    {
        var instructionClip = SoundLibrary.PopTheLetterInstruction();
        if (instructionClip != null)
        {
            BackgroundMusicManager.PlayOneShot(instructionClip);
            yield return new WaitForSeconds(instructionClip.length + 0.2f);
        }
        SoundLibrary.PlayLetterName(letter.ToString());
    }

    private IEnumerator PulseTargetDisplay()
    {
        if (targetLetterBG == null) yield break;
        var rt = targetLetterBG.GetComponent<RectTransform>();
        float elapsed = 0f;
        float dur = 0.4f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float s = t < 0.5f
                ? Mathf.Lerp(1f, 1.25f, t / 0.5f)
                : Mathf.Lerp(1.25f, 1f, (t - 0.5f) / 0.5f);
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    // ── Pop Particles (same pattern as WorldBalloon) ──

    private void SpawnPopParticles(BubbleData bubble)
    {
        if (bubble.go == null) return;
        RectTransform parentRT = playArea;
        Vector2 localPos = bubble.rt.anchoredPosition;

        Color solidColor = new Color(bubble.bubbleColor.r, bubble.bubbleColor.g, bubble.bubbleColor.b, 1f);
        Color lighterColor = Color.Lerp(solidColor, Color.white, 0.4f);
        Color darkerColor = Color.Lerp(solidColor, Color.black, 0.2f);

        int shardCount = Random.Range(10, 15);
        int sparkleCount = Random.Range(6, 10);

        // Shards
        for (int i = 0; i < shardCount; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(400f, 800f);
            float size = Random.Range(14f, 30f);
            float lifetime = Random.Range(0.5f, 0.85f);
            Color shardColor = Color.Lerp(solidColor, lighterColor, Random.Range(0f, 0.5f));
            if (Random.value < 0.3f) shardColor = darkerColor;

            var shard = CreateShard(parentRT, localPos, size, shardColor);
            StartCoroutine(AnimateShard(shard, localPos, angle, speed, lifetime, 1200f));
        }

        // Sparkles (stars)
        for (int i = 0; i < sparkleCount; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(250f, 550f);
            float size = Random.Range(6f, 12f);
            float lifetime = Random.Range(0.25f, 0.5f);
            Color sparkColor = Color.Lerp(Color.white, lighterColor, Random.Range(0f, 0.3f));

            var sparkle = CreateShard(parentRT, localPos, size, sparkColor);
            StartCoroutine(AnimateShard(sparkle, localPos, angle, speed, lifetime, 400f));
        }
    }

    private RectTransform CreateShard(RectTransform parent, Vector2 pos, float size, Color color)
    {
        var go = new GameObject("Shard");
        go.transform.SetParent(parent, false);
        var shardRT = go.AddComponent<RectTransform>();
        shardRT.anchorMin = Vector2.zero;
        shardRT.anchorMax = Vector2.zero;
        shardRT.pivot = new Vector2(0.5f, 0.5f);
        shardRT.anchoredPosition = pos;
        shardRT.sizeDelta = new Vector2(size, size);
        shardRT.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

        var shardImg = go.AddComponent<Image>();
        shardImg.color = color;
        if (circleSprite != null) shardImg.sprite = circleSprite;
        shardImg.raycastTarget = false;

        return shardRT;
    }

    private IEnumerator AnimateShard(RectTransform shardRT, Vector2 startPos, float angle,
        float speed, float lifetime, float gravity)
    {
        Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
        float spinSpeed = Random.Range(-720f, 720f);
        float t = 0f;
        Image shardImg = shardRT.GetComponent<Image>();
        Color startColor = shardImg.color;
        Vector3 startScale = shardRT.localScale;

        while (t < lifetime)
        {
            t += Time.deltaTime;
            float progress = t / lifetime;

            velocity.y -= gravity * Time.deltaTime;
            startPos += velocity * Time.deltaTime;
            shardRT.anchoredPosition = startPos;

            shardRT.Rotate(0, 0, spinSpeed * Time.deltaTime);

            float fade = 1f - progress * progress;
            shardRT.localScale = startScale * Mathf.Lerp(1f, 0.2f, progress);
            shardImg.color = new Color(startColor.r, startColor.g, startColor.b, startColor.a * fade);

            yield return null;
        }

        Destroy(shardRT.gameObject);
    }

    // ── Procedural circle sprite ──

    private static Sprite CreateCircleSprite()
    {
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        float center = (size - 1) / 2f;
        float radius = center;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) - radius;
                float alpha = Mathf.Clamp01(1f - dist);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // ── Utility ──

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
