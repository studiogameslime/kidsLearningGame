using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Main controller for the Hebrew First Letter game ("האות הראשונה").
/// Shows an image (animal/color), Hebrew word with missing first letter,
/// and 3 answer buttons. Child taps the correct first letter.
/// </summary>
public class LetterGameController : BaseMiniGame
{
    [Header("Layout")]
    public RectTransform imageArea;
    public RectTransform tileArea;
    public RectTransform buttonArea;

    [Header("UI")]
    public Image animalImage;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI scoreText;
    public Button replayButton;

    [Header("Sprites")]
    public Sprite cellSprite;      // RoundedRect
    public Sprite circleSprite;    // Circle

    private int _score;
    private int _attemptsThisRound;
    private int _hintsUsed;
    private float _lastInteractionTime;

    private LetterWordBank.WordEntry _currentWord;
    private char _correctLetter;
    private List<string> _recentWords = new List<string>();
    private const int RecentAvoidCount = 3;

    // Dynamic UI elements created per round
    private List<GameObject> _tileObjects = new List<GameObject>();
    private List<GameObject> _buttonObjects = new List<GameObject>();
    private LetterTileView _missingTile;
    private UISpriteAnimator _spriteAnimator;
    private Coroutine _inactivityCoroutine;
    private Button _correctButton;

    // ── BASE MINI GAME HOOKS ──

    protected override void Start()
    {
        if (replayButton != null)
            replayButton.onClick.AddListener(ReplaySound);
        _score = 0;
        base.Start();
    }

    protected override void OnGameInit()
    {
        totalRounds = 5;
        playWinSound = true;
        delayBeforeNextRound = 1.5f;
        delayAfterFinalRound = 2.0f;
    }

    protected override string GetFallbackGameId() => "letters";

    protected override void OnRoundSetup()
    {
        _attemptsThisRound = 0;
        _hintsUsed = 0;

        // Pick a word
        _currentWord = PickWord();
        _correctLetter = _currentWord.hebrewWord[0];

        // Update score display
        UpdateScoreText();

        // Setup image
        SetupImage();

        // Build letter tiles (RTL)
        BuildTiles();

        // Build answer buttons
        BuildAnswerButtons();

        // Play word sound after delay
        _lastInteractionTime = Time.time;
        StartCoroutine(PlaySoundDelayed(0.5f));

        // Start inactivity monitor
        _inactivityCoroutine = StartCoroutine(InactivityMonitor());

        // Position tutorial hand on one of the answer buttons
        PositionTutorialHand();

        Stats.SetCustom("wordLength", (float)_currentWord.wordLength);

        Debug.Log($"[LetterGame] Round {CurrentRound + 1}: {_currentWord.id} ({_currentWord.hebrewWord}), first letter: {_correctLetter}, difficulty: {Difficulty}");
    }

    protected override void OnRoundCleanup()
    {
        if (_inactivityCoroutine != null)
        {
            StopCoroutine(_inactivityCoroutine);
            _inactivityCoroutine = null;
        }

        foreach (var go in _tileObjects) if (go != null) Destroy(go);
        foreach (var go in _buttonObjects) if (go != null) Destroy(go);
        _tileObjects.Clear();
        _buttonObjects.Clear();
        _missingTile = null;
        _correctButton = null;

        if (_spriteAnimator != null)
        {
            Destroy(_spriteAnimator);
            _spriteAnimator = null;
        }

        if (animalImage != null)
        {
            animalImage.sprite = null;
            animalImage.color = Color.white;
            animalImage.gameObject.SetActive(true);
            // Reset to original anchors matching the setup layout (right half: 0.52–0.99, 0.32–0.98)
            var imgRT = animalImage.GetComponent<RectTransform>();
            if (imgRT != null)
            {
                imgRT.anchorMin = new Vector2(0.52f, 0.32f);
                imgRT.anchorMax = new Vector2(0.99f, 0.98f);
                imgRT.offsetMin = Vector2.zero;
                imgRT.offsetMax = Vector2.zero;
                imgRT.sizeDelta = Vector2.zero;
                imgRT.anchoredPosition = Vector2.zero;
            }
        }
    }

    protected override void OnBeforeComplete()
    {
        Stats.SetCustom("attemptsThisRound", (float)_attemptsThisRound);
        Stats.SetCustom("hintsUsed", (float)_hintsUsed);
    }

    protected override IEnumerator OnAfterComplete()
    {
        // Flash button green already happened before CompleteRound()
        // Wave bounce all tiles
        for (int i = 0; i < _tileObjects.Count; i++)
        {
            var tv = _tileObjects[i].GetComponent<LetterTileView>();
            if (tv != null) tv.WaveBounce(i * 0.08f);
        }

        // Play success animation on sprite
        if (_spriteAnimator != null)
            _spriteAnimator.PlaySuccess();

        yield return new WaitForSeconds(0.5f);

        // Update score
        _score++;
        UpdateScoreText();
    }

    // ── WORD PICKING ──

    private LetterWordBank.WordEntry PickWord()
    {
        var pool = LetterWordBank.GetFilteredPool(Difficulty);
        if (pool.Count == 0) pool = new List<LetterWordBank.WordEntry>(LetterWordBank.Pool);

        // Remove recently used
        var candidates = new List<LetterWordBank.WordEntry>();
        foreach (var w in pool)
        {
            if (!_recentWords.Contains(w.id))
                candidates.Add(w);
        }
        if (candidates.Count == 0) candidates = pool;

        var picked = candidates[Random.Range(0, candidates.Count)];

        // Track recent
        _recentWords.Add(picked.id);
        if (_recentWords.Count > RecentAvoidCount)
            _recentWords.RemoveAt(0);

        return picked;
    }

    // ── IMAGE SETUP ──

    private void SetupImage()
    {
        if (animalImage == null) return;

        if (_currentWord.soundType == "animal" && _currentWord.hasAnim)
        {
            var animData = AnimalAnimData.Load(_currentWord.id);
            if (animData != null && animData.idleFrames != null && animData.idleFrames.Length > 0)
            {
                animalImage.sprite = animData.idleFrames[0];
                animalImage.preserveAspect = true;
                animalImage.color = Color.white;

                _spriteAnimator = animalImage.gameObject.GetComponent<UISpriteAnimator>();
                if (_spriteAnimator == null)
                    _spriteAnimator = animalImage.gameObject.AddComponent<UISpriteAnimator>();
                _spriteAnimator.targetImage = animalImage;
                _spriteAnimator.idleFrames = animData.idleFrames;
                _spriteAnimator.floatingFrames = animData.floatingFrames;
                _spriteAnimator.successFrames = animData.successFrames;
                _spriteAnimator.framesPerSecond = animData.idleFps > 0 ? animData.idleFps : 12f;
                _spriteAnimator.PlayIdle();
            }
            else
            {
                // Fallback: try loading static sprite
                var sprite = Resources.Load<Sprite>($"AnimalSprites/{_currentWord.id}");
                if (sprite != null)
                {
                    animalImage.sprite = sprite;
                    animalImage.preserveAspect = true;
                }
            }
        }
        else if (_currentWord.soundType == "color")
        {
            // Color swatch: fixed-size circle centered in the image area
            if (circleSprite != null)
                animalImage.sprite = circleSprite;
            else if (cellSprite != null)
                animalImage.sprite = cellSprite;
            animalImage.color = _currentWord.swatchColor;
            animalImage.preserveAspect = true;
            var imgRT = animalImage.GetComponent<RectTransform>();
            if (imgRT != null)
            {
                // Keep the right-half anchors from reset, center the circle within that area
                // Anchors are already set to imageArea (0.52–0.99, 0.32–0.98) by OnRoundCleanup
                // Use pivot center and fixed size
                imgRT.anchorMin = new Vector2(0.52f, 0.32f);
                imgRT.anchorMax = new Vector2(0.99f, 0.98f);
                imgRT.offsetMin = Vector2.zero;
                imgRT.offsetMax = Vector2.zero;
            }
        }
    }

    // ── TILE BUILDING (RTL) ──

    private void BuildTiles()
    {
        if (tileArea == null || cellSprite == null) return;

        string word = _currentWord.hebrewWord;
        int len = word.Length;
        // Word tiles under image — big and readable
        float spacing = 16f;
        float maxByHeight = tileArea.rect.height * 0.90f;
        float maxByWidth = (tileArea.rect.width - (len - 1) * spacing) / len;
        float tileW = Mathf.Min(160f, Mathf.Min(maxByHeight, maxByWidth));
        float tileH = tileW;
        float totalW = len * tileW + (len - 1) * spacing;
        float startX = totalW / 2f - tileW / 2f; // rightmost tile x (RTL: first letter = rightmost)

        for (int i = 0; i < len; i++)
        {
            float x = startX - i * (tileW + spacing);
            var tileGO = CreateTile(tileArea, x, tileW, tileH);
            _tileObjects.Add(tileGO);

            var tileView = tileGO.GetComponent<LetterTileView>();
            if (i == 0)
            {
                // First letter (rightmost) = missing
                tileView.SetMissing();
                _missingTile = tileView;
            }
            else
            {
                tileView.SetFilled(word[i]);
            }
            tileView.BounceIn(i * 0.08f);
        }
    }

    private GameObject CreateTile(RectTransform parent, float x, float w, float h)
    {
        var go = new GameObject("Tile");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, 0);

        // Border
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
        bgImg.raycastTarget = false;

        // Letter text
        var textGO = new GameObject("Letter");
        textGO.transform.SetParent(go.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 56;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.isRightToLeftText = true;

        var tileView = go.AddComponent<LetterTileView>();
        tileView.Init(bgImg, borderImg, tmp);

        return go;
    }

    // ── ANSWER BUTTONS ──

    private void BuildAnswerButtons()
    {
        if (buttonArea == null || cellSprite == null) return;

        char[] options = LetterDistractorGenerator.Generate(_correctLetter, Difficulty);

        // Answer buttons — big and easy to tap
        float maxBtnH = buttonArea.rect.height * 0.90f;
        float maxBtnW = (buttonArea.rect.width - 2 * 30f) / 3f;
        float btnSize = Mathf.Min(maxBtnH, Mathf.Min(maxBtnW, 220f));
        float spacing = 30f;
        float totalW = 3 * btnSize + 2 * spacing;
        float startX = -totalW / 2f + btnSize / 2f;

        for (int i = 0; i < 3; i++)
        {
            char letter = options[i];
            float x = startX + i * (btnSize + spacing);

            var btnGO = CreateAnswerButton(buttonArea, letter, x, btnSize);
            _buttonObjects.Add(btnGO);

            if (letter == _correctLetter)
                _correctButton = btnGO.GetComponent<Button>();

            // Pop-in animation
            StartCoroutine(PopIn(btnGO.GetComponent<RectTransform>(), 0.3f + i * 0.1f));
        }
    }

    private GameObject CreateAnswerButton(RectTransform parent, char letter, float x, float size)
    {
        var go = new GameObject($"Btn_{letter}");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = new Vector2(x, 0);

        var img = go.AddComponent<Image>();
        img.sprite = cellSprite;
        img.type = Image.Type.Sliced;
        img.color = GameUIConstants.ButtonBackground;
        img.raycastTarget = true;

        // Border
        var borderGO = new GameObject("Border");
        borderGO.transform.SetParent(go.transform, false);
        var brt = borderGO.AddComponent<RectTransform>();
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(-4, -4);
        brt.offsetMax = new Vector2(4, 4);
        borderGO.transform.SetAsFirstSibling();
        var bimg = borderGO.AddComponent<Image>();
        bimg.sprite = cellSprite;
        bimg.type = Image.Type.Sliced;
        bimg.color = GameUIConstants.ButtonBorder;
        bimg.raycastTarget = false;

        // Letter text
        var textGO = new GameObject("Letter");
        textGO.transform.SetParent(go.transform, false);
        var trt = textGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = letter.ToString();
        tmp.fontSize = GameUIConstants.ButtonFontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = GameUIConstants.ButtonTextColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.isRightToLeftText = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        // Color block for button states
        var colors = btn.colors;
        colors.normalColor = GameUIConstants.ButtonBackground;
        colors.highlightedColor = GameUIConstants.ButtonHighlighted;
        colors.pressedColor = GameUIConstants.ButtonPressed;
        btn.colors = colors;

        char capturedLetter = letter;
        btn.onClick.AddListener(() => OnAnswerTapped(capturedLetter, go));

        return go;
    }

    // ── INPUT HANDLING ──

    private void OnAnswerTapped(char letter, GameObject btnGO)
    {
        if (IsInputLocked) return;
        DismissTutorial();
        _lastInteractionTime = Time.time;
        _attemptsThisRound++;

        if (letter == _correctLetter)
        {
            SoundLibrary.PlayLetterName(letter.ToString());
            RecordCorrect("first_letter", _currentWord.id);
            PlayCorrectEffect(btnGO.GetComponent<RectTransform>());
            StartCoroutine(OnCorrectSequence(btnGO));
        }
        else
        {
            RecordMistake("wrong_letter", letter.ToString());
            PlayWrongEffect(btnGO.GetComponent<RectTransform>());
            StartCoroutine(ShakeButton(btnGO));

            // After 2 wrong attempts, hint
            if (_attemptsThisRound >= 3 && _correctButton != null)
            {
                _hintsUsed++;
                RecordHint();
                StartCoroutine(PulseHint(_correctButton.gameObject));
            }
        }
    }

    private IEnumerator OnCorrectSequence(GameObject btnGO)
    {
        // Flash button green
        var img = btnGO.GetComponent<Image>();
        if (img != null) img.color = GameUIConstants.CorrectColor;

        // Reveal the missing letter with animation
        if (_missingTile != null)
            _missingTile.RevealLetter(_correctLetter, 0.1f);

        yield return new WaitForSeconds(0.3f);

        // CompleteRound handles: sound, stats, confetti, round advance
        // OnAfterComplete will do the wave bounce + sprite success anim
        CompleteRound();
    }

    // ── ANIMATIONS ──

    private IEnumerator ShakeButton(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        var img = go.GetComponent<Image>();
        if (img != null) img.color = GameUIConstants.WrongColor;

        Vector2 orig = rt.anchoredPosition;
        float dur = 0.3f;
        float amp = 15f;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float p = t / dur;
            float offset = Mathf.Sin(p * Mathf.PI * 6f) * amp * (1f - p);
            rt.anchoredPosition = orig + new Vector2(offset, 0);
            yield return null;
        }
        rt.anchoredPosition = orig;

        // Reset color
        if (img != null) img.color = HexColor("#E3F2FD");
    }

    private IEnumerator PulseHint(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        var img = go.GetComponent<Image>();

        for (int p = 0; p < 3; p++)
        {
            if (img != null) img.color = HexColor("#FFF9C4"); // yellow pulse
            float dur = 0.3f;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                float scale = 1f + 0.1f * Mathf.Sin((t / dur) * Mathf.PI);
                rt.localScale = Vector3.one * scale;
                yield return null;
            }
            rt.localScale = Vector3.one;
            if (img != null) img.color = HexColor("#E3F2FD");
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

    // ── SOUND ──

    private IEnumerator PlaySoundDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        PlayWordSound();
    }

    private void PlayWordSound()
    {
        if (_currentWord.soundType == "animal")
            SoundLibrary.PlayAnimalName(_currentWord.soundId);
        else if (_currentWord.soundType == "color")
            SoundLibrary.PlayColorName(_currentWord.soundId);
    }

    private void ReplaySound()
    {
        _lastInteractionTime = Time.time;
        PlayWordSound();
    }

    private IEnumerator InactivityMonitor()
    {
        while (!IsInputLocked)
        {
            yield return new WaitForSeconds(1f);
            if (!IsInputLocked && Time.time - _lastInteractionTime >= 5f)
            {
                PlayWordSound();
                _lastInteractionTime = Time.time;
            }
        }
    }

    // ── UI ──

    private void UpdateScoreText()
    {
        if (scoreText != null)
            scoreText.text = $"{_score}/{totalRounds}";
    }

    // ── TUTORIAL HAND ──

    private void PositionTutorialHand()
    {
        if (TutorialHand == null || _correctButton == null) return;

        // Point at the correct answer button
        var btnRT = _correctButton.GetComponent<RectTransform>();
        Vector2 localPos = TutorialHand.GetLocalCenter(btnRT);
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
