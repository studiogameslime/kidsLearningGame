using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Find The Animal game. 5 animals appear on screen.
/// 3 or 4 of them are the target animal shown at the top.
/// Player must tap ALL target instances to complete the round.
/// </summary>
public class FindTheAnimalController : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform playArea;
    public Image targetImage;
    public TextMeshProUGUI remainingText;

    [Header("Settings")]
    public float animalSize = 180f;

    private const int AnimalCount = 20;

    private Canvas canvas;
    private List<GameObject> spawnedAnimals = new List<GameObject>();
    private SubItemData targetAnimal;
    private bool isRoundActive;
    private int targetCount;
    private int targetsFound;

    private void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        LoadRound();
    }

    private void LoadRound()
    {
        ClearRound();
        isRoundActive = true;
        targetsFound = 0;

        var game = GameContext.CurrentGame;
        if (game == null || game.subItems == null || game.subItems.Count < 2)
        {
            Debug.LogError("FindTheAnimal: Not enough animal sub-items!");
            return;
        }

        // Randomly choose 3-5 targets to find
        targetCount = Random.Range(3, 6);
        int distractorCount = AnimalCount - targetCount;

        // Pick the target animal
        List<SubItemData> pool = new List<SubItemData>(game.subItems);
        ShuffleList(pool);
        targetAnimal = pool[0];

        // Pick multiple distractor animal types, then fill slots
        List<SubItemData> distractorTypes = new List<SubItemData>();
        for (int i = 1; i < pool.Count; i++)
        {
            if (pool[i].id != targetAnimal.id)
                distractorTypes.Add(pool[i]);
        }

        List<SubItemData> distractors = new List<SubItemData>();
        for (int i = 0; i < distractorCount; i++)
            distractors.Add(distractorTypes[i % distractorTypes.Count]);

        // Show target image at top
        if (targetImage != null)
        {
            Sprite thumb = targetAnimal.thumbnail != null ? targetAnimal.thumbnail : targetAnimal.contentAsset;
            if (thumb != null)
            {
                targetImage.sprite = thumb;
                targetImage.preserveAspect = true;
            }
        }

        UpdateRemainingText();

        // Build the list of animals to place: targetCount copies of target + distractors
        List<SubItemData> toPlace = new List<SubItemData>();
        for (int i = 0; i < targetCount; i++)
            toPlace.Add(targetAnimal);
        for (int i = 0; i < distractors.Count; i++)
            toPlace.Add(distractors[i]);

        ShuffleList(toPlace);

        // Generate positions
        float areaW = playArea.rect.width;
        float areaH = playArea.rect.height;
        float halfSize = animalSize / 2f;
        float padding = 20f;

        List<Vector2> positions = GenerateNonOverlappingPositions(
            AnimalCount, animalSize,
            -areaW / 2f + halfSize + padding, areaW / 2f - halfSize - padding,
            -areaH / 2f + halfSize + padding, areaH / 2f - halfSize - padding);

        for (int i = 0; i < toPlace.Count; i++)
        {
            var item = toPlace[i];
            Sprite sprite = item.thumbnail != null ? item.thumbnail : item.contentAsset;
            if (sprite == null) continue;

            var go = new GameObject($"Animal_{i}");
            go.transform.SetParent(playArea, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(animalSize, animalSize);
            rt.anchoredPosition = positions[i];

            // Small random rotation for playfulness
            rt.localRotation = Quaternion.Euler(0, 0, Random.Range(-10f, 10f));

            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = true;

            string capturedId = item.id;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => OnAnimalTapped(capturedId, go));

            spawnedAnimals.Add(go);
        }
    }

    private void ClearRound()
    {
        foreach (var go in spawnedAnimals)
            if (go != null) Destroy(go);
        spawnedAnimals.Clear();
    }

    private void UpdateRemainingText()
    {
        if (remainingText != null)
            remainingText.text = (targetCount - targetsFound).ToString();
    }

    private void OnAnimalTapped(string animalId, GameObject go)
    {
        if (!isRoundActive) return;

        if (animalId == targetAnimal.id)
        {
            // Correct — bounce and fade this instance
            targetsFound++;
            UpdateRemainingText();
            StartCoroutine(CorrectTapAnimation(go));

            if (targetsFound >= targetCount)
            {
                // All targets found — advance to next round after a short delay
                isRoundActive = false;
                StartCoroutine(AdvanceRound());
            }
        }
        else
        {
            // Wrong — shake
            StartCoroutine(ShakeAnimation(go));
        }
    }

    private IEnumerator CorrectTapAnimation(GameObject go)
    {
        // Disable button so it can't be tapped again
        var btn = go.GetComponent<Button>();
        if (btn != null) btn.interactable = false;

        var rt = go.GetComponent<RectTransform>();
        Vector3 orig = rt.localScale;
        Vector3 big = orig * 1.3f;
        float dur = 0.15f;

        // Bounce up
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            rt.localScale = Vector3.Lerp(orig, big, t / dur);
            yield return null;
        }
        // Bounce back
        t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            rt.localScale = Vector3.Lerp(big, orig, t / dur);
            yield return null;
        }
        rt.localScale = orig;

        // Fade to indicate it's been found
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0.4f;
        cg.blocksRaycasts = false;
    }

    private IEnumerator AdvanceRound()
    {
        ConfettiController.Instance.Play();
        // Brief pause before loading next round
        yield return new WaitForSeconds(0.8f);

        // Fade out remaining distractor animals
        foreach (var animal in spawnedAnimals)
        {
            if (animal == null) continue;
            var cg = animal.GetComponent<CanvasGroup>();
            if (cg == null) cg = animal.AddComponent<CanvasGroup>();
            cg.alpha = 0.3f;
        }

        yield return new WaitForSeconds(0.5f);
        LoadRound();
    }

    private IEnumerator ShakeAnimation(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        Vector2 orig = rt.anchoredPosition;
        float dur = 0.3f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float offset = Mathf.Sin(t * 40f) * 12f * (1f - t / dur);
            rt.anchoredPosition = orig + new Vector2(offset, 0);
            yield return null;
        }
        rt.anchoredPosition = orig;
    }

    private List<Vector2> GenerateNonOverlappingPositions(int count, float size,
        float minX, float maxX, float minY, float maxY)
    {
        var positions = new List<Vector2>();
        int maxAttempts = 100;

        for (int i = 0; i < count; i++)
        {
            Vector2 pos = Vector2.zero;
            bool valid = false;
            for (int a = 0; a < maxAttempts; a++)
            {
                pos = new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));
                valid = true;
                foreach (var existing in positions)
                {
                    if (Vector2.Distance(pos, existing) < size * 0.9f)
                    {
                        valid = false;
                        break;
                    }
                }
                if (valid) break;
            }
            positions.Add(pos);
        }
        return positions;
    }

    public void OnHomePressed() => NavigationManager.GoToMainMenu();
    public void OnRestartPressed()
    {
        LoadRound();
    }

    private static void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }
}
