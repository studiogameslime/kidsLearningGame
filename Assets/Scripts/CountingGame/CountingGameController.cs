using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Match Quantity to Number game. A group of animals appears in the center.
/// Number buttons (1-5) appear below. Tap the number matching the animal count.
/// Correct = star animation. Wrong = gentle feedback. Auto-loads next round.
/// </summary>
public class CountingGameController : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform animalArea;
    public RectTransform numberRow;

    [Header("Settings")]
    public int maxNumber = 5;
    public float animalSize = 340f;

    [Header("Sprites")]
    public Sprite circleSprite;

    private List<GameObject> spawnedAnimals = new List<GameObject>();
    private List<GameObject> numberButtons = new List<GameObject>();
    private List<GameObject> countLabels = new List<GameObject>();
    private int correctAnswer;
    private bool isRoundActive;

    private static readonly Color CorrectColor = new Color(0.4f, 0.85f, 0.4f);
    private static readonly Color WrongColor = new Color(1f, 0.5f, 0.5f);
    private static readonly Color DefaultBtnColor = new Color(0.506f, 0.78f, 0.518f); // #81C784

    // Number colors: 1=red, 2=green, 3=blue, 4=purple, 5=orange
    private static readonly Color[] NumberDarkColors = {
        new Color(0.9f, 0.22f, 0.21f),  // red
        new Color(0.30f, 0.69f, 0.31f), // green
        new Color(0.25f, 0.47f, 0.85f), // blue
        new Color(0.61f, 0.32f, 0.79f), // purple
        new Color(1f, 0.60f, 0f),       // orange
    };
    private static readonly Color[] NumberLightColors = {
        new Color(1f, 0.72f, 0.71f),    // light red
        new Color(0.72f, 0.93f, 0.73f), // light green
        new Color(0.68f, 0.79f, 1f),    // light blue
        new Color(0.85f, 0.72f, 0.95f), // light purple
        new Color(1f, 0.85f, 0.6f),     // light orange
    };

    private void Start()
    {
        LoadRound();
    }

    private void LoadRound()
    {
        ClearRound();
        isRoundActive = true;

        correctAnswer = Random.Range(1, maxNumber + 1);

        Sprite animalSprite = PickRandomAnimalSprite();
        if (animalSprite == null)
        {
            Debug.LogError("CountingGame: No animal sprites!");
            return;
        }

        float areaW = animalArea.rect.width;
        float areaH = animalArea.rect.height;
        float padding = 20f;
        float halfSize = animalSize / 2f;

        List<Vector2> positions = GeneratePositions(
            correctAnswer, animalSize,
            -areaW / 2f + halfSize + padding, areaW / 2f - halfSize - padding,
            -areaH / 2f + halfSize + padding, areaH / 2f - halfSize - padding);

        for (int i = 0; i < correctAnswer; i++)
        {
            var go = new GameObject($"Animal_{i}");
            go.transform.SetParent(animalArea, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(animalSize, animalSize);
            rt.anchoredPosition = positions[i];
            rt.localRotation = Quaternion.Euler(0, 0, Random.Range(-8f, 8f));

            var img = go.AddComponent<Image>();
            img.sprite = animalSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;

            spawnedAnimals.Add(go);
            StartCoroutine(PopIn(rt, i * 0.1f));
        }

        CreateNumberButtons();
    }

    private void ClearRound()
    {
        foreach (var go in spawnedAnimals)
            if (go != null) Destroy(go);
        spawnedAnimals.Clear();

        foreach (var go in numberButtons)
            if (go != null) Destroy(go);
        numberButtons.Clear();

        foreach (var go in countLabels)
            if (go != null) Destroy(go);
        countLabels.Clear();
    }

    private void CreateNumberButtons()
    {
        // Build 3 options: correct + 2 unique wrong answers
        List<int> options = new List<int> { correctAnswer };
        int attempts = 0;
        while (options.Count < 3 && attempts < 50)
        {
            int wrong = Random.Range(1, maxNumber + 1);
            if (!options.Contains(wrong))
                options.Add(wrong);
            attempts++;
        }

        // Shuffle order
        for (int i = options.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp2 = options[i]; options[i] = options[j]; options[j] = tmp2;
        }

        int count = options.Count;
        float btnSize = 280f;
        float spacing = 40f;
        float totalW = count * btnSize + (count - 1) * spacing;
        float startX = -totalW / 2f + btnSize / 2f;

        for (int i = 0; i < count; i++)
        {
            int number = options[i];
            var go = new GameObject($"Num_{number}");
            go.transform.SetParent(numberRow, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(btnSize, btnSize);
            rt.anchoredPosition = new Vector2(startX + i * (btnSize + spacing), 0);

            var img = go.AddComponent<Image>();
            if (circleSprite != null) img.sprite = circleSprite;
            img.color = DefaultBtnColor;
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => OnNumberTapped(number, go));

            var txtGO = new GameObject("Text");
            txtGO.transform.SetParent(go.transform, false);
            var txtRT = txtGO.AddComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = Vector2.zero;
            txtRT.offsetMax = Vector2.zero;
            var tmp = txtGO.AddComponent<TextMeshProUGUI>();
            tmp.text = number.ToString();
            tmp.fontSize = 96;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            numberButtons.Add(go);
        }
    }

    private void OnNumberTapped(int number, GameObject btnGO)
    {
        if (!isRoundActive) return;

        var img = btnGO.GetComponent<Image>();

        if (number == correctAnswer)
        {
            isRoundActive = false;
            img.color = CorrectColor;
            StartCoroutine(OnCorrect());
        }
        else
        {
            StartCoroutine(WrongFeedback(btnGO, img));
        }
    }

    private IEnumerator OnCorrect()
    {
        ConfettiController.Instance.Play();
        // Sort animals left-to-right by x position
        List<GameObject> sorted = new List<GameObject>(spawnedAnimals);
        sorted.Sort((a, b) =>
            a.GetComponent<RectTransform>().anchoredPosition.x
            .CompareTo(b.GetComponent<RectTransform>().anchoredPosition.x));

        // Instantly show all number labels in light colors above each animal
        for (int i = 0; i < sorted.Count; i++)
        {
            var animalRT = sorted[i].GetComponent<RectTransform>();
            int colorIdx = i % NumberLightColors.Length;

            var labelGO = new GameObject($"CountLabel_{i + 1}");
            labelGO.transform.SetParent(animalArea, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.sizeDelta = new Vector2(80, 80);
            labelRT.anchoredPosition = animalRT.anchoredPosition + new Vector2(0, animalSize / 2f + 45f);

            var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
            labelTMP.text = (i + 1).ToString();
            labelTMP.fontSize = 64;
            labelTMP.fontStyle = FontStyles.Bold;
            labelTMP.color = NumberLightColors[colorIdx];
            labelTMP.alignment = TextAlignmentOptions.Center;
            labelTMP.raycastTarget = false;

            countLabels.Add(labelGO);
        }

        yield return new WaitForSeconds(0.3f);

        // Count through each one: highlight to dark color + bounce animal
        for (int i = 0; i < sorted.Count; i++)
        {
            int colorIdx = i % NumberDarkColors.Length;
            var labelTMP = countLabels[i].GetComponent<TextMeshProUGUI>();
            var labelRT = countLabels[i].GetComponent<RectTransform>();

            // Darken the number
            labelTMP.color = NumberDarkColors[colorIdx];

            // Scale up the label briefly
            StartCoroutine(BounceOnce(labelRT));

            // Bounce the animal
            if (sorted[i] != null)
                StartCoroutine(BounceOnce(sorted[i].GetComponent<RectTransform>()));

            yield return new WaitForSeconds(0.5f);
        }

        yield return new WaitForSeconds(0.6f);
        LoadRound();
    }

    private IEnumerator WrongFeedback(GameObject go, Image img)
    {
        img.color = WrongColor;
        var rt = go.GetComponent<RectTransform>();
        Vector2 orig = rt.anchoredPosition;
        float dur = 0.3f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float offset = Mathf.Sin(t * 35f) * 10f * (1f - t / dur);
            rt.anchoredPosition = orig + new Vector2(offset, 0);
            yield return null;
        }
        rt.anchoredPosition = orig;
        img.color = DefaultBtnColor;
    }

    private IEnumerator PopIn(RectTransform rt, float delay)
    {
        rt.localScale = Vector3.zero;
        yield return new WaitForSeconds(delay);
        float dur = 0.25f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float s = 1f + 0.15f * Mathf.Sin(p * Mathf.PI);
            if (p >= 1f) s = 1f;
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        rt.localScale = Vector3.one;
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

    private List<Vector2> GeneratePositions(int count, float size,
        float minX, float maxX, float minY, float maxY)
    {
        var positions = new List<Vector2>();
        int maxAttempts = 80;
        for (int i = 0; i < count; i++)
        {
            Vector2 pos = Vector2.zero;
            bool valid = false;
            for (int a = 0; a < maxAttempts; a++)
            {
                pos = new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));
                valid = true;
                foreach (var e in positions)
                {
                    if (Vector2.Distance(pos, e) < size * 0.85f)
                    { valid = false; break; }
                }
                if (valid) break;
            }
            positions.Add(pos);
        }
        return positions;
    }

    private Sprite PickRandomAnimalSprite()
    {
        var game = GameContext.CurrentGame;
        if (game != null && game.subItems != null && game.subItems.Count > 0)
        {
            var item = game.subItems[Random.Range(0, game.subItems.Count)];
            return item.thumbnail != null ? item.thumbnail : item.contentAsset;
        }
        return null;
    }

    public void OnHomePressed() => NavigationManager.GoToMainMenu();
    public void OnRestartPressed() => LoadRound();
}
