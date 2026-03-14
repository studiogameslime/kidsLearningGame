using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shadow Match game controller.
///
/// Layout:
///   Upper half — 4 animal silhouettes in a 2x2 grid
///   Lower half — 4 draggable animals in a 2x2 grid
///
/// The child drags each animal onto its matching shadow.
/// Proximity hints pulse the correct shadow when an animal is dragged near.
/// </summary>
public class ShadowMatchController : MonoBehaviour
{
    [Header("References")]
    public RectTransform shadowsArea;
    public RectTransform animalsRow;
    public Sprite circleSprite;

    [Header("Settings")]
    public int animalCount = 4;
    public float shadowSize = 240f;
    public float animalSize = 260f;

    private Canvas canvas;
    private List<DraggableAnimal> animals = new List<DraggableAnimal>();
    private List<ShadowSlot> shadows = new List<ShadowSlot>();
    private int matchedCount;

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

        // Pick random animals
        var pool = new List<int>();
        for (int i = 0; i < game.subItems.Count; i++) pool.Add(i);
        Shuffle(pool);

        var picked = new List<SubItemData>();
        for (int i = 0; i < animalCount; i++)
            picked.Add(game.subItems[pool[i]]);

        // ── Shadows: 2x2 grid ──
        float sGapX = shadowSize * 1.5f;
        float sGapY = shadowSize * 1.35f;

        for (int i = 0; i < animalCount; i++)
        {
            var item = picked[i];
            Sprite spr = item.thumbnail ?? item.contentAsset;
            if (spr == null) continue;

            int col = i % 2, row = i / 2;

            var go = new GameObject($"Shadow_{i}");
            go.transform.SetParent(shadowsArea, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(shadowSize, shadowSize);
            rt.anchoredPosition = new Vector2(
                (col - 0.5f) * sGapX,
                (0.5f - row) * sGapY);

            // Silhouette: idle sprite tinted dark
            var img = go.AddComponent<Image>();
            img.sprite = spr;
            img.color = new Color(0.231f, 0.231f, 0.231f, 0.85f); // #3B3B3B
            img.preserveAspect = true;
            img.raycastTarget = false;

            var slot = go.AddComponent<ShadowSlot>();
            slot.animalId = item.id;
            shadows.Add(slot);
        }

        // ── Animals: shuffled 2x2 grid ──
        var order = new List<int>();
        for (int i = 0; i < animalCount; i++) order.Add(i);
        Shuffle(order);

        float aGapX = animalSize * 1.4f;
        float aGapY = animalSize * 1.15f;

        for (int i = 0; i < animalCount; i++)
        {
            int idx = order[i];
            var item = picked[idx];
            Sprite spr = item.thumbnail ?? item.contentAsset;
            if (spr == null) continue;

            int col = i % 2, row = i / 2;

            var go = new GameObject($"Animal_{idx}");
            go.transform.SetParent(animalsRow, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(animalSize, animalSize);
            rt.anchoredPosition = new Vector2(
                (col - 0.5f) * aGapX,
                (0.5f - row) * aGapY);

            var img = go.AddComponent<Image>();
            img.sprite = spr;
            img.preserveAspect = true;
            img.raycastTarget = true;

            // Sprite animation
            string key = item.categoryKey;
            if (!string.IsNullOrEmpty(key))
            {
                string cap = char.ToUpper(key[0]) + key.Substring(1);
                var data = AnimalAnimData.Load(cap);
                if (data != null && data.idleFrames != null && data.idleFrames.Length > 0)
                {
                    var anim = go.AddComponent<UISpriteAnimator>();
                    anim.targetImage = img;
                    anim.idleFrames = data.idleFrames;
                    anim.floatingFrames = data.floatingFrames;
                    anim.successFrames = data.successFrames;
                    anim.framesPerSecond = data.idleFps > 0 ? data.idleFps : 30f;
                }
            }

            go.AddComponent<CanvasGroup>();
            var drag = go.AddComponent<DraggableAnimal>();
            drag.Init(item.id, canvas, this);
            animals.Add(drag);
        }

        // Pulse first unmatched shadow
        UpdateGuidedPulse();

        // Announce first animal name
        if (picked.Count > 0)
        {
            string key = picked[0].categoryKey;
            if (!string.IsNullOrEmpty(key))
                SoundLibrary.PlayAnimalName(char.ToUpper(key[0]) + key.Substring(1));
        }
    }

    private void ClearRound()
    {
        foreach (var a in animals) if (a != null) Destroy(a.gameObject);
        animals.Clear();
        foreach (var s in shadows) if (s != null) Destroy(s.gameObject);
        shadows.Clear();
        matchedCount = 0;
    }

    /// <summary>Called by DraggableAnimal during drag to check proximity hints.</summary>
    public void CheckProximity(DraggableAnimal animal)
    {
        var animalPos = animal.GetComponent<RectTransform>().position;
        float hintDist = shadowSize * 1.5f;

        foreach (var slot in shadows)
        {
            if (slot.isMatched) continue;
            float dist = Vector2.Distance(animalPos, slot.GetComponent<RectTransform>().position);

            if (dist < hintDist && slot.animalId == animal.animalId)
                slot.ShowProximityHint();
        }
    }

    public bool TryMatch(DraggableAnimal animal)
    {
        foreach (var slot in shadows)
        {
            if (slot.isMatched) continue;

            float dist = Vector2.Distance(
                animal.GetComponent<RectTransform>().position,
                slot.GetComponent<RectTransform>().position);

            if (dist < shadowSize * 0.85f && slot.animalId == animal.animalId)
            {
                slot.isMatched = true;
                slot.StopPulse();
                animal.GetComponent<RectTransform>().position =
                    slot.GetComponent<RectTransform>().position;
                animal.Lock();

                // Hide shadow silhouette
                var img = slot.GetComponent<Image>();
                if (img != null) img.color = new Color(1, 1, 1, 0);

                animal.PlayMatchCelebration();
                SoundLibrary.PlayRandomFeedback();

                matchedCount++;
                if (matchedCount >= animalCount)
                    StartCoroutine(RoundComplete());
                else
                    UpdateGuidedPulse();

                return true;
            }
        }
        return false;
    }

    private void UpdateGuidedPulse()
    {
        foreach (var s in shadows) s.StopPulse();
        foreach (var s in shadows)
        {
            if (!s.isMatched) { s.StartPulse(); break; }
        }
    }

    private IEnumerator RoundComplete()
    {
        yield return new WaitForSeconds(0.5f);
        ConfettiController.Instance.Play();
        SoundLibrary.PlayRandomFeedback();
        yield return new WaitForSeconds(2f);
        LoadRound();
    }

    public void OnHomePressed() => NavigationManager.GoToMainMenu();
    public void OnRestartPressed() => LoadRound();

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T t = list[i]; list[i] = list[j]; list[j] = t;
        }
    }
}
