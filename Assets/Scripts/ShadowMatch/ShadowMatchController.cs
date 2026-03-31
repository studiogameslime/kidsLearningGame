using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shadow Match game controller.
///
/// Layout (portrait):
///   Top row  — 4 animal silhouettes in a single horizontal row (in the sky)
///   Bottom row — 4 draggable animals in a single horizontal row (on the ground)
///
/// The child drags each animal onto its matching shadow.
/// Proximity hints pulse the correct shadow when an animal is dragged near.
/// </summary>
public class ShadowMatchController : BaseMiniGame
{
    [Header("References")]
    public RectTransform shadowsArea;
    public RectTransform animalsRow;
    public Sprite circleSprite;

    [Header("Settings")]
    public int animalCount = 4;
    public float shadowSize = 400f;
    public float animalSize = 400f;

    private static readonly Color SilhouetteColor = new Color(0.15f, 0.15f, 0.18f, 0.90f);

    private Canvas canvas;
    private List<DraggableAnimal> animals = new List<DraggableAnimal>();
    private List<ShadowSlot> shadows = new List<ShadowSlot>();
    private int matchedCount;
    private Material silhouetteMaterial;

    // ── Base Mini Game Hooks ──

    protected override string GetFallbackGameId() => "shadows";

    protected override void Start()
    {
        canvas = GetComponentInParent<Canvas>();

        var silShader = Shader.Find("UI/Silhouette");
        if (silShader != null)
        {
            silhouetteMaterial = new Material(silShader);
            silhouetteMaterial.SetColor("_Color", SilhouetteColor);
        }

        base.Start();
    }

    protected override void OnGameInit()
    {
        totalRounds = 1;
        isEndless = true;
        playWinSound = true;
        playConfettiOnRoundWin = true;
        delayBeforeNextRound = 0.5f;
    }

    protected override void OnRoundSetup()
    {
        // Difficulty scaling: easy(1-3)=3, medium(4-6)=4, hard(7-10)=5
        int tier = Difficulty <= 3 ? 0 : Difficulty <= 6 ? 1 : 2;
        switch (tier)
        {
            case 0: animalCount = 3; break;
            case 1: animalCount = 4; break;
            case 2: animalCount = 5; break;
        }

        matchedCount = 0;

        var game = GameContext.CurrentGame;
        if (game == null || game.subItems == null || game.subItems.Count == 0)
        {
            Debug.LogError("ShadowMatch: No animal sub-items!");
            return;
        }

        // Filter to only animals with valid sprites
        var validItems = new List<SubItemData>();
        foreach (var item in game.subItems)
        {
            Sprite spr = item.thumbnail ?? item.contentAsset;
            if (spr != null)
                validItems.Add(item);
        }

        if (validItems.Count < animalCount)
        {
            Debug.LogError($"ShadowMatch: Only {validItems.Count} animals have sprites, need {animalCount}!");
            // Use what we have
            animalCount = validItems.Count;
            if (animalCount == 0) return;
        }

        // Pick random animals from valid pool
        var pool = new List<int>();
        for (int i = 0; i < validItems.Count; i++) pool.Add(i);
        Shuffle(pool);

        var picked = new List<SubItemData>();
        for (int i = 0; i < animalCount; i++)
            picked.Add(validItems[pool[i]]);

        // ── Shadows: single horizontal row ──
        float shadowAreaW = shadowsArea.rect.width;
        if (shadowAreaW <= 0) shadowAreaW = 900f;

        float sPadding = shadowSize * 0.3f;
        float sUsable = shadowAreaW - sPadding * 2f;
        float sSpacing = sUsable / animalCount;

        for (int i = 0; i < animalCount; i++)
        {
            var item = picked[i];
            Sprite spr = item.thumbnail ?? item.contentAsset;
            if (spr == null) continue;

            var go = new GameObject($"Shadow_{i}");
            go.transform.SetParent(shadowsArea, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(shadowSize, shadowSize);
            // Evenly spaced along horizontal center line
            float x = -sUsable * 0.5f + sSpacing * 0.5f + sSpacing * i;
            rt.anchoredPosition = new Vector2(x, 0f);

            // True silhouette: shader replaces all RGB with solid color, keeps alpha shape
            var img = go.AddComponent<Image>();
            img.sprite = spr;
            if (silhouetteMaterial != null)
            {
                img.material = silhouetteMaterial;
                img.color = Color.white;
            }
            else
            {
                // Fallback if shader not found — tint dark
                img.color = SilhouetteColor;
            }
            img.preserveAspect = true;
            img.raycastTarget = false;

            var slot = go.AddComponent<ShadowSlot>();
            slot.animalId = item.id;
            shadows.Add(slot);
        }

        // ── Animals: shuffled single horizontal row ──
        var order = new List<int>();
        for (int i = 0; i < animalCount; i++) order.Add(i);
        Shuffle(order);

        float animalAreaW = animalsRow.rect.width;
        if (animalAreaW <= 0) animalAreaW = 900f;

        float aPadding = animalSize * 0.2f;
        float aUsable = animalAreaW - aPadding * 2f;
        float aSpacing = aUsable / animalCount;

        for (int i = 0; i < animalCount; i++)
        {
            int idx = order[i];
            var item = picked[idx];
            Sprite spr = item.thumbnail ?? item.contentAsset;
            if (spr == null) continue;

            var go = new GameObject($"Animal_{idx}");
            go.transform.SetParent(animalsRow, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(animalSize, animalSize);
            // Evenly spaced along horizontal center line
            float x = -aUsable * 0.5f + aSpacing * 0.5f + aSpacing * i;
            rt.anchoredPosition = new Vector2(x, 0f);

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
            // Store capitalized categoryKey for sound playback
            if (!string.IsNullOrEmpty(item.categoryKey))
                drag.soundName = char.ToUpper(item.categoryKey[0]) + item.categoryKey.Substring(1);
            animals.Add(drag);
        }

        // Pulse first unmatched animal to encourage interaction
        UpdateGuidedPulse();

        // Position tutorial hand on first animal → its matching shadow
        PositionTutorialHand();

        // Animal names are announced when matched, not on round start
    }

    private void PositionTutorialHand()
    {
        if (TutorialHand == null) return;
        if (animals.Count == 0 || shadows.Count == 0) return;

        // Find first unmatched animal and its matching shadow
        DraggableAnimal firstAnimal = null;
        ShadowSlot matchingShadow = null;
        foreach (var a in animals)
        {
            if (a.isPlaced) continue;
            foreach (var s in shadows)
            {
                if (!s.isMatched && s.animalId == a.animalId)
                {
                    firstAnimal = a;
                    matchingShadow = s;
                    break;
                }
            }
            if (firstAnimal != null) break;
        }

        if (firstAnimal == null || matchingShadow == null) return;

        var animalRT = firstAnimal.GetComponent<RectTransform>();
        var shadowRT = matchingShadow.GetComponent<RectTransform>();

        Vector2 fromLocal = TutorialHand.GetLocalCenter(animalRT);
        Vector2 toLocal = TutorialHand.GetLocalCenter(shadowRT);

        TutorialHand.SetMovePath(fromLocal, toLocal, 1.2f);
    }

    protected override void OnRoundCleanup()
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
        DismissTutorial();

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
                animal.GetComponent<RectTransform>().position =
                    slot.GetComponent<RectTransform>().position;
                animal.Lock();

                // Hide shadow silhouette
                var img = slot.GetComponent<Image>();
                if (img != null) img.color = new Color(1, 1, 1, 0);

                animal.PlayMatchCelebration();
                PlayCorrectEffect(animal.GetComponent<RectTransform>());
                // Play the matched animal's name sound
                if (!string.IsNullOrEmpty(animal.soundName))
                    SoundLibrary.PlayAnimalName(animal.soundName);

                matchedCount++;
                RecordCorrect(isLast: matchedCount >= animalCount);
                if (matchedCount >= animalCount)
                    CompleteRound();
                else
                    UpdateGuidedPulse();

                return true;
            }
        }
        return false;
    }

    private void UpdateGuidedPulse()
    {
        // Stop all animal pulses, then pulse the first unmatched animal
        foreach (var a in animals) a.StopGuidancePulse();
        foreach (var a in animals)
        {
            if (!a.isPlaced) { a.StartGuidancePulse(); break; }
        }
    }

    public void OnHomePressed() => ExitGame();
    public void OnRestartPressed()
    {
        OnRoundCleanup();
        OnRoundSetup();
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T t = list[i]; list[i] = list[j]; list[j] = t;
        }
    }
}
