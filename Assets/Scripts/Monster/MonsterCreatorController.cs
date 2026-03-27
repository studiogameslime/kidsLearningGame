using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Step-by-step monster creation screen.
/// All parts pre-populated with defaults on open. Each step swaps that part.
/// Layout: preview left (big), options + color palette right.
/// Body step shows white shapes + color palette to tint.
/// </summary>
public class MonsterCreatorController : MonoBehaviour
{
    [Header("Preview")]
    public Image previewBody;
    public Image previewEyeLeft;
    public Image previewEyeRight;
    public Image previewNose;
    public Image previewMouth;
    public Image previewArmLeft;
    public Image previewArmRight;
    public Image previewLegLeft;
    public Image previewLegRight;
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
    private Color bodyColor = Color.white;
    private List<GameObject> optionItems = new List<GameObject>();
    private List<GameObject> colorItems = new List<GameObject>();
    private Canvas _worldCanvas;

    // Same colors as onboarding profile creation
    private static readonly Color[] PaletteColors =
    {
        HexColor("#EF5350"), HexColor("#F48FB1"), HexColor("#CE93D8"),
        HexColor("#90CAF9"), HexColor("#80DEEA"), HexColor("#A5D6A7"),
        HexColor("#FFF59D"), HexColor("#FFCC80"), HexColor("#BCAAA4"),
    };

    // Default parts (first option per step)
    private static readonly string[] DefaultParts =
    {
        "body_whiteA",    // body
        "eye_blue",       // eyes
        "nose_red",       // nose
        "mouthA",         // mouth
        "arm_whiteA",     // left arm
        "arm_whiteA",     // right arm
        "leg_whiteA",     // left leg
        "leg_whiteA",     // right leg
        "",               // detail (optional)
    };

    private enum Step { Body, Eyes, Nose, Mouth, LeftArm, RightArm, LeftLeg, RightLeg, Detail }

    private static readonly string[] StepTitles =
    {
        "\u05D2\u05D5\u05E3",                        // גוף
        "\u05E2\u05D9\u05E0\u05D9\u05D9\u05DD",      // עיניים
        "\u05D0\u05E3",                              // אף
        "\u05E4\u05D4",                              // פה
        "\u05D9\u05D3 \u05E9\u05DE\u05D0\u05DC",      // יד שמאל
        "\u05D9\u05D3 \u05D9\u05DE\u05D9\u05DF",      // יד ימין
        "\u05E8\u05D2\u05DC \u05E9\u05DE\u05D0\u05DC", // רגל שמאל
        "\u05E8\u05D2\u05DC \u05D9\u05DE\u05D9\u05DF", // רגל ימין
        "\u05E4\u05E8\u05D8\u05D9\u05DD",             // פרטים
    };

    private void Awake()
    {
        // Wire buttons internally — don't rely on external listener wiring
        if (nextButton != null) nextButton.onClick.AddListener(OnNextPressed);
        if (backButton != null) backButton.onClick.AddListener(OnBackPressed);
        if (doneButton != null) doneButton.onClick.AddListener(OnDonePressed);
    }

    public void Open()
    {
        if (creatorPanel != null) creatorPanel.SetActive(true);

        // Find and disable the WORLD canvas (the one named "Canvas" or containing WorldController)
        DisableWorldCanvas();

        currentStep = 0;
        data = new MonsterData();

        // Default body color from profile
        var profile = ProfileManager.ActiveProfile;
        if (profile != null && !string.IsNullOrEmpty(profile.avatarColorHex))
            ColorUtility.TryParseHtmlString(profile.avatarColorHex, out bodyColor);
        else
            bodyColor = PaletteColors[0];

        // Pre-populate all parts with defaults
        ApplyDefaults();

        ShowStep(0);
    }

    public void Close()
    {
        if (creatorPanel != null) creatorPanel.SetActive(false);
        EnableWorldCanvas();
    }

    private void DisableWorldCanvas()
    {
        // Find the world canvas — it has a WorldController on it
        var wc = FindObjectOfType<WorldController>();
        if (wc != null)
        {
            _worldCanvas = wc.GetComponentInParent<Canvas>();
            if (_worldCanvas != null)
            {
                var cg = _worldCanvas.GetComponent<CanvasGroup>();
                if (cg == null) cg = _worldCanvas.gameObject.AddComponent<CanvasGroup>();
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }
        }
    }

    private void EnableWorldCanvas()
    {
        if (_worldCanvas != null)
        {
            var cg = _worldCanvas.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
            _worldCanvas = null;
        }
    }

    // ── Pre-populate all parts ──

    private void ApplyDefaults()
    {
        data.bodySprite = DefaultParts[0];
        data.eyeSprite = DefaultParts[1];
        data.noseSprite = DefaultParts[2];
        data.mouthSprite = DefaultParts[3];
        data.leftArmSprite = DefaultParts[4];
        data.rightArmSprite = DefaultParts[5];
        data.leftLegSprite = DefaultParts[6];
        data.rightLegSprite = DefaultParts[7];
        data.detailSprite = DefaultParts[8];

        SetPreview(previewBody,    data.bodySprite,    bodyColor);
        SetPreview(previewEyeLeft, data.eyeSprite,     Color.white);
        SetPreview(previewEyeRight,data.eyeSprite,     Color.white);
        SetPreview(previewNose,    data.noseSprite,     Color.white);
        SetPreview(previewMouth,   data.mouthSprite,    Color.white);
        SetPreview(previewArmLeft, data.leftArmSprite,  bodyColor);
        SetPreview(previewArmRight,data.rightArmSprite, bodyColor, flipX: true);
        SetPreview(previewLegLeft, data.leftLegSprite,  bodyColor);
        SetPreview(previewLegRight,data.rightLegSprite, bodyColor, flipX: true);

        if (!string.IsNullOrEmpty(data.detailSprite))
            SetPreview(previewDetail, data.detailSprite, Color.white);
    }

    private void SetPreview(Image img, string spriteName, Color tint, bool flipX = false)
    {
        if (img == null || string.IsNullOrEmpty(spriteName)) return;
        var sprite = LoadPartSprite(spriteName);
        if (sprite == null) return;
        img.sprite = sprite;
        img.color = tint;
        img.enabled = true;
        if (flipX)
        {
            var s = img.rectTransform.localScale;
            s.x = -Mathf.Abs(s.x);
            img.rectTransform.localScale = s;
        }
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

        var stepEnum = (Step)step;
        BuildOptions(stepEnum);

        if (stepEnum == Step.Body || stepEnum == Step.LeftArm || stepEnum == Step.RightArm ||
            stepEnum == Step.LeftLeg || stepEnum == Step.RightLeg)
            BuildColorPalette();
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
            case Step.LeftArm:
            case Step.RightArm:
                sprites = new[] { "arm_whiteA", "arm_whiteB", "arm_whiteC", "arm_whiteD", "arm_whiteE" };
                break;
            case Step.LeftLeg:
            case Step.RightLeg:
                sprites = new[] { "leg_whiteA", "leg_whiteB", "leg_whiteC", "leg_whiteD", "leg_whiteE" };
                break;
            case Step.Detail:
                sprites = new[] { "detail_blue_horn_large", "detail_blue_horn_small",
                    "detail_blue_ear", "detail_blue_ear_round",
                    "detail_blue_antenna_large", "detail_blue_antenna_small",
                    "detail_red_horn_large", "detail_green_horn_large",
                    "detail_yellow_ear", "detail_red_ear_round" };
                break;
            default:
                sprites = new string[0];
                break;
        }

        foreach (var spriteName in sprites)
        {
            var sprite = LoadPartSprite(spriteName);
            if (sprite == null) continue;

            var go = new GameObject(spriteName);
            go.transform.SetParent(optionsGrid, false);
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(100, 100);

            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = true;

            // Tint tintable parts
            bool tintable = step == Step.Body || step == Step.LeftArm || step == Step.RightArm ||
                            step == Step.LeftLeg || step == Step.RightLeg;
            if (tintable) img.color = bodyColor;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            string captured = spriteName;
            Step capturedStep = step;
            RectTransform capturedRT = go.GetComponent<RectTransform>();
            btn.onClick.AddListener(() => OnPartSelected(capturedStep, captured, capturedRT));

            optionItems.Add(go);
        }
    }

    private void BuildColorPalette()
    {
        if (colorPaletteGrid == null) return;

        for (int i = 0; i < PaletteColors.Length; i++)
        {
            var go = new GameObject($"Color_{i}");
            go.transform.SetParent(colorPaletteGrid, false);
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(65, 65);

            var img = go.AddComponent<Image>();
            img.color = PaletteColors[i];
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            int idx = i;
            btn.onClick.AddListener(() => OnColorSelected(idx));

            colorItems.Add(go);
        }
    }

    private void OnColorSelected(int colorIndex)
    {
        bodyColor = PaletteColors[colorIndex];

        // Update preview tints
        if (previewBody != null) previewBody.color = bodyColor;
        if (previewArmLeft != null) previewArmLeft.color = bodyColor;
        if (previewArmRight != null) previewArmRight.color = bodyColor;
        if (previewLegLeft != null) previewLegLeft.color = bodyColor;
        if (previewLegRight != null) previewLegRight.color = bodyColor;

        // Tint option items too
        foreach (var go in optionItems)
        {
            var img = go.GetComponent<Image>();
            if (img != null) img.color = bodyColor;
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
                if (previewBody != null) { previewBody.sprite = sprite; previewBody.enabled = true; previewBody.color = bodyColor; }
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
            case Step.LeftArm:
                data.leftArmSprite = spriteName;
                if (previewArmLeft != null) { previewArmLeft.sprite = sprite; previewArmLeft.enabled = true; previewArmLeft.color = bodyColor; }
                break;
            case Step.RightArm:
                data.rightArmSprite = spriteName;
                if (previewArmRight != null)
                {
                    previewArmRight.sprite = sprite; previewArmRight.enabled = true; previewArmRight.color = bodyColor;
                    var s = previewArmRight.rectTransform.localScale;
                    s.x = -Mathf.Abs(s.x); previewArmRight.rectTransform.localScale = s;
                }
                break;
            case Step.LeftLeg:
                data.leftLegSprite = spriteName;
                if (previewLegLeft != null) { previewLegLeft.sprite = sprite; previewLegLeft.enabled = true; previewLegLeft.color = bodyColor; }
                break;
            case Step.RightLeg:
                data.rightLegSprite = spriteName;
                if (previewLegRight != null)
                {
                    previewLegRight.sprite = sprite; previewLegRight.enabled = true; previewLegRight.color = bodyColor;
                    var s = previewLegRight.rectTransform.localScale;
                    s.x = -Mathf.Abs(s.x); previewLegRight.rectTransform.localScale = s;
                }
                break;
            case Step.Detail:
                data.detailSprite = spriteName;
                if (previewDetail != null) { previewDetail.sprite = sprite; previewDetail.enabled = true; }
                break;
        }
    }

    public void OnNextPressed()
    {
        if (currentStep < (int)Step.Detail)
            ShowStep(currentStep + 1);
    }

    public void OnBackPressed()
    {
        if (currentStep > 0)
            ShowStep(currentStep - 1);
    }

    public void OnDonePressed()
    {
        // Detail is optional — don't require it
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

    private void ClearOptions()
    {
        foreach (var go in optionItems) if (go != null) Destroy(go);
        optionItems.Clear();
    }

    private void ClearColors()
    {
        foreach (var go in colorItems) if (go != null) Destroy(go);
        colorItems.Clear();
    }

    private static Sprite LoadPartSprite(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return null;
        var sprite = Resources.Load<Sprite>($"MonsterParts/{spriteName}");
        if (sprite != null) return sprite;
        var all = Resources.LoadAll<Sprite>("MonsterParts");
        foreach (var s in all)
            if (s.name == spriteName) return s;
        return null;
    }

    public static Sprite LoadMonsterSprite(string spriteName) => LoadPartSprite(spriteName);

    private IEnumerator PopAnimation(RectTransform target)
    {
        if (target == null) yield break;
        Vector3 orig = target.localScale;
        float t = 0;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            float s = 1f + 0.25f * Mathf.Sin(t / 0.2f * Mathf.PI);
            target.localScale = orig * s;
            yield return null;
        }
        target.localScale = orig;
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
