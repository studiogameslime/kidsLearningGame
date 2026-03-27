using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Step-by-step monster creation: Body → Eyes → Nose → Mouth → Arms → Legs → Detail
/// Uses MonsterPreview with body-specific anchor positioning.
/// </summary>
public class MonsterCreatorController : MonoBehaviour
{
    [Header("Preview")]
    public MonsterPreview preview;

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
    private CanvasGroup _worldCG;

    private static readonly Color[] PaletteColors =
    {
        HexColor("#EF5350"), HexColor("#F48FB1"), HexColor("#CE93D8"),
        HexColor("#90CAF9"), HexColor("#80DEEA"), HexColor("#A5D6A7"),
        HexColor("#FFF59D"), HexColor("#FFCC80"), HexColor("#BCAAA4"),
    };

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

        var profile = ProfileManager.ActiveProfile;
        Color defaultColor = PaletteColors[0];
        if (profile != null && !string.IsNullOrEmpty(profile.avatarColorHex))
            ColorUtility.TryParseHtmlString(profile.avatarColorHex, out defaultColor);

        bodyColor = defaultColor;
        armColor = defaultColor;
        legColor = defaultColor;

        // Build preview hierarchy if needed
        if (preview != null && preview.body == null)
            preview.Build();

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

        if (preview != null)
            preview.ApplyData(data, bodyColor, armColor, legColor);
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
        BuildOptions((Step)step);

        if ((Step)step == Step.Body || (Step)step == Step.Arms || (Step)step == Step.Legs)
            BuildColorPalette((Step)step);
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
        bool tintable = IsTintableStep(step);

        foreach (var spriteName in sprites)
        {
            var sprite = LoadPartSprite(spriteName);
            if (sprite == null) continue;

            var go = new GameObject(spriteName);
            go.transform.SetParent(optionsGrid, false);
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(100, 100);

            var img = go.AddComponent<Image>();
            img.sprite = sprite; img.preserveAspect = true; img.raycastTarget = true;
            if (tintable) img.color = tint;

            var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
            string captured = spriteName;
            Step capturedStep = step;
            btn.onClick.AddListener(() => OnPartSelected(capturedStep, captured));
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
            int idx = i; Step capturedStep = step;
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
                if (preview != null) preview.body.color = c;
                break;
            case Step.Arms:
                armColor = c;
                data.armColorHex = ColorUtility.ToHtmlStringRGB(c);
                if (preview != null) { preview.armLeft.color = c; preview.armRight.color = c; }
                break;
            case Step.Legs:
                legColor = c;
                data.legColorHex = ColorUtility.ToHtmlStringRGB(c);
                if (preview != null) { preview.legLeft.color = c; preview.legRight.color = c; }
                break;
        }

        // Tint grid options too
        foreach (var go in optionItems)
        {
            var img = go.GetComponent<Image>();
            if (img != null) img.color = c;
        }
    }

    private void OnPartSelected(Step step, string spriteName)
    {
        if (preview == null) return;

        switch (step)
        {
            case Step.Body:
                data.bodySprite = spriteName;
                // Reposition ALL parts using new body's anchors
                preview.SetBody(spriteName, bodyColor);
                // Re-apply all other parts at new positions
                preview.SetPart(preview.eyeLeft,  data.eyeSprite,  Color.white);
                preview.SetPart(preview.eyeRight, data.eyeSprite,  Color.white);
                preview.SetPart(preview.nose,     data.noseSprite,  Color.white);
                preview.SetPart(preview.mouth,    data.mouthSprite, Color.white);
                preview.SetPart(preview.armLeft,  data.armSprite,  armColor, flipX: true);
                preview.SetPart(preview.armRight, data.armSprite,  armColor, flipX: false);
                preview.SetPart(preview.legLeft,  data.legSprite,  legColor, flipX: true);
                preview.SetPart(preview.legRight, data.legSprite,  legColor, flipX: false);
                if (!string.IsNullOrEmpty(data.detailSprite))
                    preview.SetPart(preview.detail, data.detailSprite, Color.white);
                preview.PopPart(preview.body);
                break;
            case Step.Eyes:
                data.eyeSprite = spriteName;
                preview.SetPart(preview.eyeLeft, spriteName, Color.white);
                preview.SetPart(preview.eyeRight, spriteName, Color.white);
                preview.PopPart(preview.eyeLeft);
                break;
            case Step.Nose:
                data.noseSprite = spriteName;
                preview.SetPart(preview.nose, spriteName, Color.white);
                preview.PopPart(preview.nose);
                break;
            case Step.Mouth:
                data.mouthSprite = spriteName;
                preview.SetPart(preview.mouth, spriteName, Color.white);
                preview.PopPart(preview.mouth);
                break;
            case Step.Arms:
                data.armSprite = spriteName;
                preview.SetPart(preview.armLeft, spriteName, armColor, flipX: true);
                preview.SetPart(preview.armRight, spriteName, armColor, flipX: false);
                preview.PopPart(preview.armLeft);
                break;
            case Step.Legs:
                data.legSprite = spriteName;
                preview.SetPart(preview.legLeft, spriteName, legColor, flipX: true);
                preview.SetPart(preview.legRight, spriteName, legColor, flipX: false);
                preview.PopPart(preview.legLeft);
                break;
            case Step.Detail:
                data.detailSprite = spriteName;
                preview.SetPart(preview.detail, spriteName, Color.white);
                preview.PopPart(preview.detail);
                break;
        }
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

    private void DisableWorldCanvas()
    {
        var wc = FindObjectOfType<WorldController>();
        if (wc != null)
        {
            _worldCanvas = wc.GetComponentInParent<Canvas>();
            if (_worldCanvas != null)
            {
                _worldCG = _worldCanvas.GetComponent<CanvasGroup>();
                if (_worldCG == null) _worldCG = _worldCanvas.gameObject.AddComponent<CanvasGroup>();
                _worldCG.interactable = false; _worldCG.blocksRaycasts = false;
            }
        }
    }

    private void EnableWorldCanvas()
    {
        if (_worldCG != null) { _worldCG.interactable = true; _worldCG.blocksRaycasts = true; _worldCG = null; }
        _worldCanvas = null;
    }

    // ── Helpers ──

    private bool IsTintableStep(Step s) => s == Step.Body || s == Step.Arms || s == Step.Legs;
    private Color GetTintForStep(Step s) { switch (s) { case Step.Body: return bodyColor; case Step.Arms: return armColor; case Step.Legs: return legColor; default: return Color.white; } }
    private void ClearOptions() { foreach (var go in optionItems) if (go != null) Destroy(go); optionItems.Clear(); }
    private void ClearColors() { foreach (var go in colorItems) if (go != null) Destroy(go); colorItems.Clear(); }

    public static Sprite LoadPartSprite(string spriteName) => LoadMonsterSprite(spriteName);

    public static Sprite LoadMonsterSprite(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return null;
        var sprite = Resources.Load<Sprite>($"MonsterParts/{spriteName}");
        if (sprite != null) return sprite;
        var all = Resources.LoadAll<Sprite>("MonsterParts");
        foreach (var s in all) if (s.name == spriteName) return s;
        return null;
    }

    private static Color HexColor(string hex) { ColorUtility.TryParseHtmlString(hex, out Color c); return c; }
}
