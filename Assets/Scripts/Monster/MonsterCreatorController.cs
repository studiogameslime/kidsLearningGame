using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Step-by-step monster creation screen.
/// Player taps parts from a selection grid; monster preview updates live.
/// Steps: Body → Eyes → Nose → Mouth → Left Arm → Right Arm → Left Leg → Right Leg → Detail (optional) → Done
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
    public TextMeshProUGUI stepTitle;
    public Button nextButton;
    public Button backButton;
    public Button doneButton;
    public GameObject creatorPanel; // fullscreen panel

    /// <summary>Called when monster creation is finished.</summary>
    public System.Action<MonsterData> onMonsterCreated;

    private MonsterData data = new MonsterData();
    private int currentStep;
    private List<GameObject> optionItems = new List<GameObject>();

    private static readonly string PartsPath = "Assets/Art/Monsters Parts/";

    private enum Step
    {
        Body, Eyes, Nose, Mouth, LeftArm, RightArm, LeftLeg, RightLeg, Detail
    }

    private static readonly string[] StepTitles =
    {
        "\u05D2\u05D5\u05E3",           // גוף
        "\u05E2\u05D9\u05E0\u05D9\u05D9\u05DD", // עיניים
        "\u05D0\u05E3",                 // אף
        "\u05E4\u05D4",                 // פה
        "\u05D9\u05D3 \u05E9\u05DE\u05D0\u05DC", // יד שמאל
        "\u05D9\u05D3 \u05D9\u05DE\u05D9\u05DF", // יד ימין
        "\u05E8\u05D2\u05DC \u05E9\u05DE\u05D0\u05DC", // רגל שמאל
        "\u05E8\u05D2\u05DC \u05D9\u05DE\u05D9\u05DF", // רגל ימין
        "\u05E4\u05E8\u05D8\u05D9\u05DD",  // פרטים
    };

    public void Open()
    {
        if (creatorPanel != null) creatorPanel.SetActive(true);
        currentStep = 0;
        data = new MonsterData();
        ShowStep(0);
    }

    public void Close()
    {
        if (creatorPanel != null) creatorPanel.SetActive(false);
    }

    private void ShowStep(int step)
    {
        currentStep = step;

        // Update title
        if (stepTitle != null && step < StepTitles.Length)
            HebrewText.SetText(stepTitle, StepTitles[step]);

        // Navigation buttons
        if (backButton != null) backButton.gameObject.SetActive(step > 0);
        if (nextButton != null) nextButton.gameObject.SetActive(step < (int)Step.Detail);
        if (doneButton != null) doneButton.gameObject.SetActive(step == (int)Step.Detail);

        // Build options for this step
        ClearOptions();
        BuildOptions((Step)step);
    }

    private void BuildOptions(Step step)
    {
        string[] sprites;

        switch (step)
        {
            case Step.Body:
                sprites = GetSpriteNames("body_");
                break;
            case Step.Eyes:
                sprites = GetSpriteNames("eye_");
                break;
            case Step.Nose:
                sprites = GetSpriteNames("nose_");
                break;
            case Step.Mouth:
                sprites = GetSpriteNames("mouth");
                break;
            case Step.LeftArm:
            case Step.RightArm:
                sprites = GetSpriteNames("arm_");
                break;
            case Step.LeftLeg:
            case Step.RightLeg:
                sprites = GetSpriteNames("leg_");
                break;
            case Step.Detail:
                sprites = GetSpriteNames("detail_");
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

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            string captured = spriteName;
            Step capturedStep = step;
            btn.onClick.AddListener(() => OnPartSelected(capturedStep, captured, rt));

            optionItems.Add(go);
        }
    }

    private void OnPartSelected(Step step, string spriteName, RectTransform btnRT)
    {
        // Pop animation on button
        StartCoroutine(PopAnimation(btnRT));

        // Apply to data + preview
        var sprite = LoadPartSprite(spriteName);

        switch (step)
        {
            case Step.Body:
                data.bodySprite = spriteName;
                if (previewBody != null) { previewBody.sprite = sprite; previewBody.enabled = true; }
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
                if (previewArmLeft != null) { previewArmLeft.sprite = sprite; previewArmLeft.enabled = true; }
                break;
            case Step.RightArm:
                data.rightArmSprite = spriteName;
                if (previewArmRight != null)
                {
                    previewArmRight.sprite = sprite;
                    previewArmRight.enabled = true;
                    // Flip right arm horizontally
                    var s = previewArmRight.rectTransform.localScale;
                    s.x = -Mathf.Abs(s.x);
                    previewArmRight.rectTransform.localScale = s;
                }
                break;
            case Step.LeftLeg:
                data.leftLegSprite = spriteName;
                if (previewLegLeft != null) { previewLegLeft.sprite = sprite; previewLegLeft.enabled = true; }
                break;
            case Step.RightLeg:
                data.rightLegSprite = spriteName;
                if (previewLegRight != null)
                {
                    previewLegRight.sprite = sprite;
                    previewLegRight.enabled = true;
                    var s = previewLegRight.rectTransform.localScale;
                    s.x = -Mathf.Abs(s.x);
                    previewLegRight.rectTransform.localScale = s;
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
            // Shake the done button as feedback — need at least body, eyes, mouth, arms, legs
            if (doneButton != null)
                StartCoroutine(PopAnimation(doneButton.GetComponent<RectTransform>()));
            return;
        }

        // Save monster
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

    // ── Helpers ──

    private void ClearOptions()
    {
        foreach (var go in optionItems) if (go != null) Destroy(go);
        optionItems.Clear();
    }

    private string[] GetSpriteNames(string prefix)
    {
        var results = new List<string>();

        // Load from Resources at runtime
        var allSprites = Resources.LoadAll<Sprite>("MonsterParts");
        if (allSprites != null)
        {
            foreach (var s in allSprites)
            {
                if (s.name.StartsWith(prefix))
                    results.Add(s.name);
            }
        }

        // Fallback: if nothing in Resources, use hardcoded common patterns
        if (results.Count == 0)
        {
            string[] colors = { "blue", "dark", "green", "red", "white", "yellow" };
            string[] shapes;

            if (prefix == "body_")
            {
                shapes = new[] { "A", "B", "C", "D", "E", "F" };
                foreach (var c in colors)
                    foreach (var s in shapes)
                        results.Add($"body_{c}{s}");
            }
            else if (prefix == "arm_")
            {
                shapes = new[] { "A", "B", "C", "D", "E" };
                foreach (var c in colors)
                    foreach (var s in shapes)
                        results.Add($"arm_{c}{s}");
            }
            else if (prefix == "leg_")
            {
                shapes = new[] { "A", "B", "C", "D", "E" };
                foreach (var c in colors)
                    foreach (var s in shapes)
                        results.Add($"leg_{c}{s}");
            }
            else if (prefix == "eye_")
            {
                results.AddRange(new[] {
                    "eye_blue", "eye_red", "eye_yellow",
                    "eye_cute_dark", "eye_cute_light",
                    "eye_human", "eye_human_blue", "eye_human_green", "eye_human_red",
                    "eye_closed_happy", "eye_closed_feminine",
                    "eye_angry_blue", "eye_angry_green", "eye_angry_red"
                });
            }
            else if (prefix == "nose_")
            {
                results.AddRange(new[] { "nose_brown", "nose_green", "nose_red", "nose_yellow" });
            }
            else if (prefix == "mouth")
            {
                for (char c = 'A'; c <= 'J'; c++)
                    results.Add($"mouth{c}");
                results.AddRange(new[] {
                    "mouth_closed_fangs", "mouth_closed_happy",
                    "mouth_closed_sad", "mouth_closed_teeth"
                });
            }
            else if (prefix == "detail_")
            {
                string[] types = { "antenna_large", "antenna_small", "ear", "ear_round", "horn_large", "horn_small" };
                foreach (var c in colors)
                    foreach (var t in types)
                        results.Add($"detail_{c}_{t}");
            }
        }

        results.Sort();
        return results.ToArray();
    }

    private static Sprite LoadPartSprite(string spriteName)
    {
        // Try Resources/MonsterParts first
        var sprite = Resources.Load<Sprite>($"MonsterParts/{spriteName}");
        if (sprite != null) return sprite;

        // Try loading all from MonsterParts and find by name
        var all = Resources.LoadAll<Sprite>("MonsterParts");
        foreach (var s in all)
            if (s.name == spriteName) return s;

        return null;
    }

    /// <summary>Loads a sprite by name — used by other systems to reconstruct monsters.</summary>
    public static Sprite LoadMonsterSprite(string spriteName)
    {
        return LoadPartSprite(spriteName);
    }

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
}
