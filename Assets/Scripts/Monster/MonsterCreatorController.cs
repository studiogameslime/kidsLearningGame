using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Step-by-step monster creation: Body → Eyes → Nose → Mouth → Arms → Legs → Detail
/// All parts pre-populated. Each step swaps that part + has its own color picker.
/// Arms/legs are merged (one step sets both). Arm sprites: monster's left = screen right.
/// </summary>
public class MonsterCreatorController : MonoBehaviour
{
    [Header("Preview")]
    public Image previewBody;
    public Image previewEyeLeft;
    public Image previewEyeRight;
    public Image previewNose;
    public Image previewMouth;
    public Image previewArmLeft;   // screen-left = monster's RIGHT arm
    public Image previewArmRight;  // screen-right = monster's LEFT arm (flipped)
    public Image previewLegLeft;   // screen-left
    public Image previewLegRight;  // screen-right (flipped)
    public Image previewDetail;

    [Header("UI")]
    public Transform optionsGrid;
    public Transform colorPaletteGrid;
    public TextMeshProUGUI stepTitle;
    public Button nextButton;
    public Button backButton;
    public Button doneButton;
    public GameObject creatorPanel;

    public System.Action<MonsterData> onMonsterCreated;

    private MonsterData data = new MonsterData();
    private int currentStep;
    private Color bodyColor, armColor, legColor;
    private List<GameObject> optionItems = new List<GameObject>();
    private List<GameObject> colorItems = new List<GameObject>();
    private Canvas _worldCanvas;

    private static readonly Color[] PaletteColors =
    {
        HexColor("#EF5350"), HexColor("#F48FB1"), HexColor("#CE93D8"),
        HexColor("#90CAF9"), HexColor("#80DEEA"), HexColor("#A5D6A7"),
        HexColor("#FFF59D"), HexColor("#FFCC80"), HexColor("#BCAAA4"),
    };

    // 7 steps (merged arms + legs)
    private enum Step { Body, Eyes, Nose, Mouth, Arms, Legs, Detail }

    private static readonly string[] StepTitles =
    {
        "\u05D2\u05D5\u05E3",                        // גוף
        "\u05E2\u05D9\u05E0\u05D9\u05D9\u05DD",      // עיניים
        "\u05D0\u05E3",                              // אף
        "\u05E4\u05D4",                              // פה
        "\u05D9\u05D3\u05D9\u05D9\u05DD",             // ידיים
        "\u05E8\u05D2\u05DC\u05D9\u05D9\u05DD",       // רגליים
        "\u05E4\u05E8\u05D8\u05D9\u05DD",             // פרטים
    };

    private void Awake()
    {
        if (nextButton != null) nextButton.onClick.AddListener(OnNextPressed);
        if (backButton != null) backButton.onClick.AddListener(OnBackPressed);
        if (doneButton != null) doneButton.onClick.AddListener(OnDonePressed);
    }

    public void Open()
    {
        if (creatorPanel != null) creatorPanel.SetActive(true);
        DisableWorldCanvas();

        currentStep = 0;
        data = new MonsterData();

        // Default colors from profile
        var profile = ProfileManager.ActiveProfile;
        Color defaultColor = PaletteColors[0];
        if (profile != null && !string.IsNullOrEmpty(profile.avatarColorHex))
            ColorUtility.TryParseHtmlString(profile.avatarColorHex, out defaultColor);

        bodyColor = defaultColor;
        armColor = defaultColor;
        legColor = defaultColor;

        ApplyDefaults();
        ShowStep(0);
    }

    public void Close()
    {
        if (creatorPanel != null) creatorPanel.SetActive(false);
        EnableWorldCanvas();
    }

    private void ApplyDefaults()
    {
        data.bodySprite = "body_whiteA";
        data.eyeSprite = "eye_blue";
        data.noseSprite = "nose_red";
        data.mouthSprite = "mouthA";
        data.armSprite = "arm_whiteA";
        data.legSprite = "leg_whiteA";
        data.detailSprite = "";
        data.bodyColorHex = ColorUtility.ToHtmlStringRGB(bodyColor);
        data.armColorHex = ColorUtility.ToHtmlStringRGB(armColor);
        data.legColorHex = ColorUtility.ToHtmlStringRGB(legColor);

        SetPreview(previewBody, data.bodySprite, bodyColor);
        SetPreview(previewEyeLeft, data.eyeSprite, Color.white);
        SetPreview(previewEyeRight, data.eyeSprite, Color.white);
        SetPreview(previewNose, data.noseSprite, Color.white);
        SetPreview(previewMouth, data.mouthSprite, Color.white);

        // Screen-left arm = monster's right (no flip, arm sprite faces left by default)
        SetPreview(previewArmLeft, data.armSprite, armColor, flipX: false);
        // Screen-right arm = monster's left (flip)
        SetPreview(previewArmRight, data.armSprite, armColor, flipX: true);

        SetPreview(previewLegLeft, data.legSprite, legColor, flipX: false);
        SetPreview(previewLegRight, data.legSprite, legColor, flipX: true);
    }

    private void SetPreview(Image img, string spriteName, Color tint, bool flipX = false)
    {
        if (img == null || string.IsNullOrEmpty(spriteName)) return;
        var sprite = LoadPartSprite(spriteName);
        if (sprite == null) return;
        img.sprite = sprite; img.color = tint; img.enabled = true;
        var s = img.rectTransform.localScale;
        s.x = flipX ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);
        img.rectTransform.localScale = s;
    }

    // ── Steps ──

    private void ShowStep(int step)
    {
        currentStep = step;
        if (stepTitle != null && step < StepTitles.Length)
            HebrewText.SetText(stepTitle, StepTitles[step]);

        if (backButton != null) backButton.gameObject.SetActive(step > 0);
        if (nextButton != null) nextButton.gameObject.SetActive(step < (int)Step.Detail);
        if (doneButton != null) doneButton.gameObject.SetActive(step == (int)Step.Detail);

        ClearOptions();
        ClearColors();

        var s = (Step)step;
        BuildOptions(s);

        // Color palette for tintable parts
        if (s == Step.Body || s == Step.Arms || s == Step.Legs)
            BuildColorPalette(s);
    }

    private void BuildOptions(Step step)
    {
        string[] sprites;
        switch (step)
        {
            case Step.Body:
                sprites = new[] { "body_whiteA", "body_whiteB", "body_whiteC",
                                   "body_whiteD", "body_whiteE", "body_whiteF" };
                break;
            case Step.Eyes:
                sprites = new[] { "eye_blue", "eye_red", "eye_yellow",
                    "eye_cute_dark", "eye_cute_light", "eye_human",
                    "eye_closed_happy", "eye_closed_feminine" };
                break;
            case Step.Nose:
                sprites = new[] { "nose_brown", "nose_green", "nose_red", "nose_yellow" };
                break;
            case Step.Mouth:
                sprites = new[] { "mouthA", "mouthB", "mouthC", "mouthD", "mouthE",
                    "mouthF", "mouthG", "mouthH", "mouthI", "mouthJ" };
                break;
            case Step.Arms:
                sprites = new[] { "arm_whiteA", "arm_whiteB", "arm_whiteC", "arm_whiteD", "arm_whiteE" };
                break;
            case Step.Legs:
                sprites = new[] { "leg_whiteA", "leg_whiteB", "leg_whiteC", "leg_whiteD", "leg_whiteE" };
                break;
            case Step.Detail:
                sprites = new[] { "detail_blue_horn_large", "detail_blue_horn_small",
                    "detail_blue_ear", "detail_blue_ear_round",
                    "detail_blue_antenna_large", "detail_blue_antenna_small",
                    "detail_red_horn_large", "detail_green_horn_large",
                    "detail_yellow_ear", "detail_red_ear_round" };
                break;
            default: sprites = new string[0]; break;
        }

        Color tint = GetTintForStep(step);

        foreach (var spriteName in sprites)
        {
            var sprite = LoadPartSprite(spriteName);
            if (sprite == null) continue;

            var go = new GameObject(spriteName);
            go.transform.SetParent(optionsGrid, false);
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(100, 100);

            var img = go.AddComponent<Image>();
            img.sprite = sprite; img.preserveAspect = true; img.raycastTarget = true;
            if (IsTintableStep(step)) img.color = tint;

            var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
            string captured = spriteName;
            Step capturedStep = step;
            RectTransform capturedRT = go.GetComponent<RectTransform>();
            btn.onClick.AddListener(() => OnPartSelected(capturedStep, captured, capturedRT));
            optionItems.Add(go);
        }
    }

    private void BuildColorPalette(Step step)
    {
        if (colorPaletteGrid == null) return;
        for (int i = 0; i < PaletteColors.Length; i++)
        {
            var go = new GameObject($"Color_{i}");
            go.transform.SetParent(colorPaletteGrid, false);
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(65, 65);
            var img = go.AddComponent<Image>();
            img.color = PaletteColors[i]; img.raycastTarget = true;
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
            int idx = i;
            Step capturedStep = step;
            btn.onClick.AddListener(() => OnColorSelected(capturedStep, idx));
            colorItems.Add(go);
        }
    }

    private void OnColorSelected(Step step, int colorIndex)
    {
        Color c = PaletteColors[colorIndex];

        switch (step)
        {
            case Step.Body:
                bodyColor = c;
                data.bodyColorHex = ColorUtility.ToHtmlStringRGB(c);
                if (previewBody != null) previewBody.color = c;
                break;
            case Step.Arms:
                armColor = c;
                data.armColorHex = ColorUtility.ToHtmlStringRGB(c);
                if (previewArmLeft != null) previewArmLeft.color = c;
                if (previewArmRight != null) previewArmRight.color = c;
                break;
            case Step.Legs:
                legColor = c;
                data.legColorHex = ColorUtility.ToHtmlStringRGB(c);
                if (previewLegLeft != null) previewLegLeft.color = c;
                if (previewLegRight != null) previewLegRight.color = c;
                break;
        }

        // Tint option items
        foreach (var go in optionItems)
        {
            var img = go.GetComponent<Image>();
            if (img != null) img.color = c;
        }
    }

    private void OnPartSelected(Step step, string spriteName, RectTransform btnRT)
    {
        StartCoroutine(PopAnimation(btnRT));
        var sprite = LoadPartSprite(spriteName);

        switch (step)
        {
            case Step.Body:
                data.bodySprite = spriteName;
                if (previewBody != null) { previewBody.sprite = sprite; previewBody.color = bodyColor; }
                break;
            case Step.Eyes:
                data.eyeSprite = spriteName;
                if (previewEyeLeft != null) { previewEyeLeft.sprite = sprite; previewEyeLeft.enabled = true; }
                if (previewEyeRight != null) { previewEyeRight.sprite = sprite; previewEyeRight.enabled = true; }
                break;
            case Step.Nose:
                data.noseSprite = spriteName;
                if (previewNose != null) { previewNose.sprite = sprite; previewNose.enabled = true; }
                break;
            case Step.Mouth:
                data.mouthSprite = spriteName;
                if (previewMouth != null) { previewMouth.sprite = sprite; previewMouth.enabled = true; }
                break;
            case Step.Arms:
                data.armSprite = spriteName;
                // Screen-left = monster's right (no flip), screen-right = monster's left (flip)
                SetPreview(previewArmLeft, spriteName, armColor, flipX: false);
                SetPreview(previewArmRight, spriteName, armColor, flipX: true);
                break;
            case Step.Legs:
                data.legSprite = spriteName;
                SetPreview(previewLegLeft, spriteName, legColor, flipX: false);
                SetPreview(previewLegRight, spriteName, legColor, flipX: true);
                break;
            case Step.Detail:
                data.detailSprite = spriteName;
                if (previewDetail != null) { previewDetail.sprite = sprite; previewDetail.enabled = true; }
                break;
        }
    }

    private bool IsTintableStep(Step s) => s == Step.Body || s == Step.Arms || s == Step.Legs;

    private Color GetTintForStep(Step s)
    {
        switch (s) { case Step.Body: return bodyColor; case Step.Arms: return armColor;
                     case Step.Legs: return legColor; default: return Color.white; }
    }

    public void OnNextPressed()
    {
        if (currentStep < (int)Step.Detail) ShowStep(currentStep + 1);
    }

    public void OnBackPressed()
    {
        if (currentStep > 0) ShowStep(currentStep - 1);
    }

    public void OnDonePressed()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile != null)
        {
            profile.journey.monster.monsterCreated = true;
            profile.journey.monster.monsterData = data;
            ProfileManager.Instance.Save();
        }
        Close();
        onMonsterCreated?.Invoke(data);
    }

    // ── World Canvas Blocking ──

    private CanvasGroup _worldCG;

    private void DisableWorldCanvas()
    {
        var wc = FindObjectOfType<WorldController>();
        if (wc != null)
        {
            _worldCanvas = wc.GetComponentInParent<Canvas>();
            if (_worldCanvas != null)
            {
                _worldCG = _worldCanvas.GetComponent<CanvasGroup>();
                if (_worldCG == null)
                    _worldCG = _worldCanvas.gameObject.AddComponent<CanvasGroup>();
                _worldCG.interactable = false;
                _worldCG.blocksRaycasts = false;
            }
        }
    }

    private void EnableWorldCanvas()
    {
        if (_worldCG != null)
        {
            _worldCG.interactable = true;
            _worldCG.blocksRaycasts = true;
            _worldCG = null;
        }
        _worldCanvas = null;
    }

    // ── Helpers ──

    private void ClearOptions() { foreach (var go in optionItems) if (go != null) Destroy(go); optionItems.Clear(); }
    private void ClearColors() { foreach (var go in colorItems) if (go != null) Destroy(go); colorItems.Clear(); }

    private static Sprite LoadPartSprite(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return null;
        var sprite = Resources.Load<Sprite>($"MonsterParts/{spriteName}");
        if (sprite != null) return sprite;
        var all = Resources.LoadAll<Sprite>("MonsterParts");
        foreach (var s in all) if (s.name == spriteName) return s;
        return null;
    }

    public static Sprite LoadMonsterSprite(string spriteName) => LoadPartSprite(spriteName);

    private IEnumerator PopAnimation(RectTransform target)
    {
        if (target == null) yield break;
        Vector3 orig = target.localScale;
        float t = 0;
        while (t < 0.2f) { t += Time.deltaTime; target.localScale = orig * (1f + 0.25f * Mathf.Sin(t / 0.2f * Mathf.PI)); yield return null; }
        target.localScale = orig;
    }

    private static Color HexColor(string hex) { ColorUtility.TryParseHtmlString(hex, out Color c); return c; }
}
