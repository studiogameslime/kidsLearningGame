using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Hebrew letter tracing mini-game.
/// Children trace letters stroke-by-stroke following guided paths.
/// Extends BaseMiniGame for standard lifecycle and stats.
/// </summary>
public class LetterTracingController : BaseMiniGame
{
    [Header("Tracing")]
    public TracingCanvas tracingCanvas;

    [Header("Display")]
    public TextMeshProUGUI letterDisplay;   // large reference letter
    public TextMeshProUGUI letterNameText;  // Hebrew name of the letter

    [Header("Color Palette")]
    public Transform colorButtonContainer;
    public GameObject colorButtonPrefab;

    [Header("Progress")]
    public Transform progressDotsContainer; // shows stroke progress dots

    [Header("Buttons")]
    public Button nextButton;

    private static readonly Color[] PaletteColors =
    {
        HexColor("#EF4444"), // red
        HexColor("#F97316"), // orange
        HexColor("#FACC15"), // yellow
        HexColor("#22C55E"), // green
        HexColor("#3B82F6"), // blue
        HexColor("#8B5CF6"), // purple
    };

    private int selectedColorIndex = 0;
    private List<Image> colorIndicators = new List<Image>();
    private List<Image> progressDots = new List<Image>();
    private char currentLetter;
    private int lettersCompleted;

    // Letters available at current difficulty
    private char[] availableLetters;

    // ── BaseMiniGame ──

    protected override string GetFallbackGameId() => "lettertracing";

    protected override void OnGameInit()
    {
        isEndless = true;
        totalRounds = 1;
        playConfettiOnRoundWin = true;
        playWinSound = true;
        delayBeforeNextRound = 1.5f;
    }

    protected override void OnRoundSetup()
    {
        lettersCompleted = 0;

        // Select letters based on difficulty
        availableLetters = GetLettersForDifficulty();

        // Build color palette
        BuildColorPalette();

        // Set initial color
        tracingCanvas.SetColor(PaletteColors[selectedColorIndex]);

        // Wire tracing callbacks
        tracingCanvas.onStrokeCompleted = OnStrokeCompleted;
        tracingCanvas.onLetterCompleted = OnLetterCompleted;

        // Init canvas
        tracingCanvas.Init();

        // Adjust tolerance by difficulty
        tracingCanvas.pathTolerance = Difficulty <= 3 ? 0.12f : Difficulty <= 6 ? 0.09f : 0.06f;
        tracingCanvas.startTolerance = Difficulty <= 3 ? 0.14f : 0.10f;

        // Hide next button
        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(false);
            nextButton.onClick.AddListener(OnNextPressed);
        }

        // Load first letter
        LoadRandomLetter();

        // Position tutorial hand at first stroke start
        PositionTutorialHand();
    }

    protected override void OnBeforeComplete()
    {
        Stats?.SetCustom("lettersCompleted", lettersCompleted);
    }

    protected override IEnumerator OnAfterComplete()
    {
        // Pop animation on the letter display
        if (letterDisplay != null)
        {
            var rt = letterDisplay.GetComponent<RectTransform>();
            Vector3 original = rt.localScale;
            float t = 0;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                float s = 1f + 0.2f * Mathf.Sin(t / 0.3f * Mathf.PI);
                rt.localScale = original * s;
                yield return null;
            }
            rt.localScale = original;
        }
        yield return new WaitForSeconds(0.5f);
    }

    protected override void OnRoundCleanup()
    {
        tracingCanvas.Clear();
        ClearProgressDots();

        // Clear color buttons
        if (colorButtonContainer != null)
        {
            for (int i = colorButtonContainer.childCount - 1; i >= 0; i--)
                Destroy(colorButtonContainer.GetChild(i).gameObject);
        }
        colorIndicators.Clear();
    }

    // ── Letter Management ──

    private void LoadRandomLetter()
    {
        // Pick a random letter from available set
        currentLetter = availableLetters[Random.Range(0, availableLetters.Length)];

        var letterData = HebrewLetterStrokeData.Get(currentLetter);

        // Update display
        if (letterDisplay != null)
            HebrewText.SetText(letterDisplay, currentLetter.ToString());

        if (letterNameText != null)
            HebrewText.SetText(letterNameText, letterData.name);

        // Load into tracing canvas
        tracingCanvas.SetLetter(letterData);

        // Build progress dots
        BuildProgressDots(letterData.strokes.Count);

        // Hide next button
        if (nextButton != null)
            nextButton.gameObject.SetActive(false);
    }

    private char[] GetLettersForDifficulty()
    {
        if (Difficulty <= 3)
            return HebrewLetterStrokeData.SimpleLetters;
        if (Difficulty <= 6)
        {
            var combined = new List<char>(HebrewLetterStrokeData.SimpleLetters);
            combined.AddRange(HebrewLetterStrokeData.MediumLetters);
            return combined.ToArray();
        }
        return HebrewLetterStrokeData.AllLetters;
    }

    // ── Callbacks ──

    private void OnStrokeCompleted(int strokeIndex)
    {
        // Update progress dot
        if (strokeIndex < progressDots.Count)
            progressDots[strokeIndex].color = PaletteColors[selectedColorIndex];

        Stats?.RecordCorrect("stroke", $"{currentLetter}_stroke{strokeIndex}");
        SoundLibrary.PlayRandomFeedback();
    }

    private void OnLetterCompleted()
    {
        lettersCompleted++;

        // Show next button
        if (nextButton != null)
            nextButton.gameObject.SetActive(true);

        // Success animation — scale pop on the letter
        if (letterDisplay != null)
            StartCoroutine(LetterPopAnimation());
    }

    private void OnNextPressed()
    {
        if (nextButton != null)
            nextButton.gameObject.SetActive(false);

        // Load next letter (endless mode)
        tracingCanvas.Clear();
        tracingCanvas.Init();
        tracingCanvas.SetColor(PaletteColors[selectedColorIndex]);
        tracingCanvas.onStrokeCompleted = OnStrokeCompleted;
        tracingCanvas.onLetterCompleted = OnLetterCompleted;

        LoadRandomLetter();
    }

    private IEnumerator LetterPopAnimation()
    {
        if (letterDisplay == null) yield break;
        var rt = letterDisplay.GetComponent<RectTransform>();
        Vector3 orig = rt.localScale;

        // Scale up
        float t = 0;
        while (t < 0.15f)
        {
            t += Time.deltaTime;
            rt.localScale = orig * (1f + 0.3f * (t / 0.15f));
            yield return null;
        }
        // Scale back
        t = 0;
        while (t < 0.15f)
        {
            t += Time.deltaTime;
            rt.localScale = orig * (1.3f - 0.3f * (t / 0.15f));
            yield return null;
        }
        rt.localScale = orig;
    }

    // ── Color Palette ──

    private void BuildColorPalette()
    {
        if (colorButtonPrefab == null || colorButtonContainer == null) return;

        for (int i = 0; i < PaletteColors.Length; i++)
        {
            var go = Instantiate(colorButtonPrefab, colorButtonContainer);
            var btn = go.GetComponent<Button>();
            var img = go.GetComponent<Image>();
            int index = i;

            img.color = PaletteColors[i];

            var ring = go.transform.Find("Ring");
            Image ringImg = ring != null ? ring.GetComponent<Image>() : null;
            if (ringImg != null) colorIndicators.Add(ringImg);

            btn.onClick.AddListener(() => SelectColor(index));
        }
        UpdateColorSelection();
    }

    private void SelectColor(int index)
    {
        selectedColorIndex = index;
        tracingCanvas.SetColor(PaletteColors[index]);
        UpdateColorSelection();
    }

    private void UpdateColorSelection()
    {
        for (int i = 0; i < colorIndicators.Count; i++)
            colorIndicators[i].gameObject.SetActive(i == selectedColorIndex);
    }

    // ── Progress Dots ──

    private void BuildProgressDots(int count)
    {
        ClearProgressDots();
        if (progressDotsContainer == null) return;

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject($"Dot_{i}");
            go.transform.SetParent(progressDotsContainer, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(24, 24);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.75f, 0.75f, 0.75f, 0.5f);
            img.raycastTarget = false;

            var circleSprite = Resources.Load<Sprite>("UI/Circle");
            if (circleSprite != null) img.sprite = circleSprite;

            progressDots.Add(img);
        }
    }

    private void ClearProgressDots()
    {
        foreach (var img in progressDots)
            if (img != null) Destroy(img.gameObject);
        progressDots.Clear();
    }

    // ── Tutorial ──

    private void PositionTutorialHand()
    {
        if (TutorialHand == null || tracingCanvas == null) return;
        Vector2 localPos = TutorialHand.GetLocalCenter(tracingCanvas.GetComponent<RectTransform>());
        TutorialHand.SetPosition(localPos);
    }

    // ── Navigation ──

    public void OnHomePressed() => ExitGame();

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
