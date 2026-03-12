using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Animal Shadow Match game. Vertical layout:
/// Shadows on the left, draggable animals on the right.
/// Drag animals to their matching shadow.
/// </summary>
public class ShadowMatchController : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform animalsColumn;     // right column for colored animals
    public RectTransform shadowsColumn;     // left column for silhouettes

    [Header("Settings")]
    public int animalCount = 4;
    public float cardSize = 360f;

    private Canvas canvas;
    private List<DraggableAnimal> animals = new List<DraggableAnimal>();
    private List<ShadowSlot> shadows = new List<ShadowSlot>();
    private int matchedCount = 0;

    private void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        LoadRound();
    }

    private void LoadRound()
    {
        ClearRound();

        var game = GameContext.CurrentGame;
        if (game == null || game.subItems == null || game.subItems.Count < animalCount)
        {
            Debug.LogError("ShadowMatch: Not enough animal sub-items!");
            return;
        }

        List<int> indices = new List<int>();
        for (int i = 0; i < game.subItems.Count; i++) indices.Add(i);
        ShuffleList(indices);

        List<SubItemData> picked = new List<SubItemData>();
        for (int i = 0; i < animalCount; i++)
            picked.Add(game.subItems[indices[i]]);

        // Vertical layout: stack items top to bottom
        float spacing = 20f;
        float totalH = animalCount * cardSize + (animalCount - 1) * spacing;
        float startY = totalH / 2f - cardSize / 2f;

        // Create shadow slots (left column) in order
        for (int i = 0; i < animalCount; i++)
        {
            var item = picked[i];
            Sprite sprite = item.thumbnail != null ? item.thumbnail : item.contentAsset;
            if (sprite == null) continue;

            var shadowGO = new GameObject($"Shadow_{i}");
            shadowGO.transform.SetParent(shadowsColumn, false);
            var shadowRT = shadowGO.AddComponent<RectTransform>();
            shadowRT.sizeDelta = new Vector2(cardSize, cardSize);
            shadowRT.anchoredPosition = new Vector2(0, startY - i * (cardSize + spacing));

            var shadowImg = shadowGO.AddComponent<Image>();
            shadowImg.sprite = sprite;
            shadowImg.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            shadowImg.preserveAspect = true;
            shadowImg.raycastTarget = false;

            var slot = shadowGO.AddComponent<ShadowSlot>();
            slot.animalId = item.id;
            shadows.Add(slot);
        }

        // Create draggable animals (right column) in shuffled order
        List<int> shuffledOrder = new List<int>();
        for (int i = 0; i < animalCount; i++) shuffledOrder.Add(i);
        ShuffleList(shuffledOrder);

        for (int i = 0; i < animalCount; i++)
        {
            int idx = shuffledOrder[i];
            var item = picked[idx];
            Sprite sprite = item.thumbnail != null ? item.thumbnail : item.contentAsset;
            if (sprite == null) continue;

            var animalGO = new GameObject($"Animal_{idx}");
            animalGO.transform.SetParent(animalsColumn, false);
            var animalRT = animalGO.AddComponent<RectTransform>();
            animalRT.sizeDelta = new Vector2(cardSize, cardSize);
            animalRT.anchoredPosition = new Vector2(0, startY - i * (cardSize + spacing));

            var animalImg = animalGO.AddComponent<Image>();
            animalImg.sprite = sprite;
            animalImg.preserveAspect = true;
            animalImg.raycastTarget = true;

            animalGO.AddComponent<CanvasGroup>();
            var draggable = animalGO.AddComponent<DraggableAnimal>();
            draggable.Init(item.id, canvas, this);

            animals.Add(draggable);
        }
    }

    private void ClearRound()
    {
        foreach (var a in animals)
            if (a != null) Destroy(a.gameObject);
        animals.Clear();

        foreach (var s in shadows)
            if (s != null) Destroy(s.gameObject);
        shadows.Clear();

        matchedCount = 0;
    }

    public bool TryMatch(DraggableAnimal animal)
    {
        foreach (var slot in shadows)
        {
            if (slot.isMatched) continue;
            float dist = Vector2.Distance(
                animal.GetComponent<RectTransform>().position,
                slot.GetComponent<RectTransform>().position);

            if (dist < cardSize * 0.7f && slot.animalId == animal.animalId)
            {
                slot.isMatched = true;
                animal.GetComponent<RectTransform>().position = slot.GetComponent<RectTransform>().position;
                animal.Lock();

                slot.GetComponent<Image>().color = Color.white;

                matchedCount++;
                StartCoroutine(MatchEffect(animal.transform));

                if (matchedCount >= animalCount)
                    StartCoroutine(OnAllMatched());

                return true;
            }
        }
        return false;
    }

    private IEnumerator MatchEffect(Transform target)
    {
        Vector3 orig = target.localScale;
        Vector3 big = orig * 1.2f;
        float dur = 0.12f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            target.localScale = Vector3.Lerp(orig, big, t / dur);
            yield return null;
        }
        t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            target.localScale = Vector3.Lerp(big, orig, t / dur);
            yield return null;
        }
        target.localScale = orig;
    }

    private IEnumerator OnAllMatched()
    {
        ConfettiController.Instance.Play();
        yield return new WaitForSeconds(1.2f);
        LoadRound();
    }

    public void OnHomePressed() => NavigationManager.GoToMainMenu();
    public void OnRestartPressed() => LoadRound();

    private static void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }
}
