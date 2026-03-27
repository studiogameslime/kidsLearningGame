using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Step-by-step monster creation screen.
/// Layout: preview on left (big), options + color palette on right.
/// Body step shows only white shapes + color palette to tint.
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

    // Same colors as onboarding profile creation
    private static readonly Color[] PaletteColors =
    {
        HexColor("#EF5350"), // red
        HexColor("#F48FB1"), // pink
        HexColor("#CE93D8"), // purple
        HexColor("#90CAF9"), // blue
        HexColor("#80DEEA"), // light blue
        HexColor("#A5D6A7"), // green
        HexColor("#FFF59D"), // yellow
        HexColor("#FFCC80"), // orange
        HexColor("#BCAAA4"), // brown
    };

    private enum Step { Body, Eyes, Nose, Mouth, LeftArm, RightArm, LeftLeg, RightLeg, Detail }

    private static readonly string[] StepTitles =
    {
        "\u05D2\u05D5\u05E3",                     // גוף
        "\u05E2\u05D9\u05E0\u05D9\u05D9\u05DD",   // עיניים
        "\u05D0\u05E3",                           // אף
        "\u05E4\u05D4",                           // פה
        "\u05D9\u05D3 \u05E9\u05DE\u05D0\u05DC",   // יד שמאל
        "\u05D9\u05D3 \u05D9\u05DE\u05D9\u05DF",   // יד ימין
        "\u05E8\u05D2\u05DC \u05E9\u05DE\u05D0\u05DC", // רגל שמאל
        "\u05E8\u05D2\u05DC \u05D9\u05DE\u05D9\u05DF", // רגל ימין
        "\u05E4\u05E8\u05D8\u05D9\u05DD",          // פרטים
    };

    public void Open()
    {
        if (creatorPanel != null) creatorPanel.SetActive(true);
        currentStep = 0;
        data = new MonsterData();

        // Default body color = child's favorite color from profile
        var profile = ProfileManager.ActiveProfile;
        if (profile != null && !string.IsNullOrEmpty(profile.avatarColorHex))
        {
            ColorUtility.TryParseHtmlString(profile.avatarColorHex, out bodyColor);
        }
        else
        {
            bodyColor = PaletteColors[0];
        }

        ShowStep(0);
    }

    public void Close()
    {
        if (creatorPanel != null) creatorPanel.SetActive(false);
    }

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

        // Show color palette for body step
        if (stepEnum == Step.Body)
            BuildColorPalette();
    }

    private void BuildOptions(Step step)
    {
        string[] sprites;

        switch (step)
        {
            case Step.Body:
                // Only white body shapes — user picks color separately
                sprites = new[] { "body_whiteA", "body_whiteB", "body_whiteC",
                                   "body_whiteD", "body_whiteE", "body_whiteF" };
                break;
            case Step.Eyes:
                sprites = new[] { "eye_blue", "eye_red", "eye_yellow",
                    "eye_cute_dark", "eye_cute_light", "eye_human", "eye_human_blue",
                    "eye_human_green", "eye_human_red", "eye_closed_happy", "eye_closed_feminine" };
                break;
            case Step.Nose:
                sprites = new[] { "nose_brown", "nose_green", "nose_red", "nose_yellow" };
                break;
            case Step.Mouth:
                sprites = new[] { "mouthA", "mouthB", "mouthC", "mouthD", "mouthE",
                    "mouthF", "mouthG", "mouthH", "mouthI", "mouthJ",
                    "mouth_closed_happy", "mouth_closed_teeth", "mouth_closed_fangs" };
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
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(90, 90);

            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = true;

            // Tint white parts with body color
            if (step == Step.Body || step == Step.LeftArm || step == Step.RightArm ||
                step == Step.LeftLeg || step == Step.RightLeg)
                img.color = bodyColor;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            string captured = spriteName;
            Step capturedStep = step;
            RectTransform capturedRT = rt;
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
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(70, 70);

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

        // Tint the body preview
        if (previewBody != null)
            previewBody.color = bodyColor;

        // Tint all white shape options in the grid to show the color
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
        if (!data.IsComplete)
        {
            if (doneButton != null)
                StartCoroutine(PopAnimation(doneButton.GetComponent<RectTransform>()));
            return;
        }

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
