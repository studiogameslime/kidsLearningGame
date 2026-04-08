using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Fishing mini-game controller.
/// Elroey sits in a boat above water. A speech bubble shows the target fish.
/// Fish swim below. Player taps the matching fish to catch it.
/// 5 correct catches to complete the game.
/// </summary>
public class FishingGameController : BaseMiniGame
{
    [Header("Scene References")]
    public RectTransform elroeyRT;         // Elroey in boat
    public RectTransform rodTipRT;         // fishing rod tip (line origin)
    public Image speechBubbleFish;         // target fish displayed in bubble
    public RectTransform swimArea;         // parent for swimming fish
    public TextMeshProUGUI progressText;   // "2/5" progress display
    public FishingLine fishingLine;

    [Header("Fish Sprites")]
    public Sprite[] fishSprites;           // 8 fish sprites (loaded by setup)
    public string[] fishIds;               // matching IDs

    [Header("Difficulty")]
    public int fishOnScreen = 6;
    public float fishSpeedMin = 50f;
    public float fishSpeedMax = 100f;

    private const int TotalCatches = 5;

    // State
    private enum FishingState { WaitingForInput, Casting, RoundTransition }
    private FishingState state = FishingState.WaitingForInput;

    private int successfulCatches;
    private int currentTargetIndex;
    private List<SwimmingFish> activeFish = new List<SwimmingFish>();
    private List<int> usedTargets = new List<int>();
    private float roundStartTime;

    // ── BaseMiniGame ──

    protected override string GetFallbackGameId() => "fishing";

    protected override void OnGameInit()
    {
        totalRounds = TotalCatches;
        isEndless = false;
        playConfettiOnRoundWin = false;
        playConfettiOnSessionWin = true;
        playWinSound = true;
        delayBeforeNextRound = 0.8f;
    }

    protected override void OnRoundSetup()
    {
        // Difficulty scaling: easy(1-3), medium(4-6), hard(7-10)
        int tier = Difficulty <= 3 ? 0 : Difficulty <= 6 ? 1 : 2;
        switch (tier)
        {
            case 0: fishOnScreen = 4; fishSpeedMin = 40f; fishSpeedMax = 80f;  break;
            case 1: fishOnScreen = 6; fishSpeedMin = 50f; fishSpeedMax = 100f; break;
            case 2: fishOnScreen = 8; fishSpeedMin = 60f; fishSpeedMax = 130f; break;
        }

        successfulCatches = 0;
        usedTargets.Clear();
        state = FishingState.WaitingForInput;

        // Setup fishing line
        if (fishingLine != null)
            fishingLine.Init(swimArea.parent as RectTransform ?? swimArea);

        SpawnFish();
        PickNewTarget();
        UpdateProgress();

        roundStartTime = Time.time;
    }

    protected override void OnBeforeComplete()
    {
        Stats?.SetCustom("totalCatches", successfulCatches);
    }

    protected override IEnumerator OnAfterComplete()
    {
        // Hide all fish
        foreach (var f in activeFish)
            if (f != null) f.gameObject.SetActive(false);
        yield return new WaitForSeconds(0.5f);
    }

    protected override void OnRoundCleanup()
    {
        foreach (var f in activeFish)
            if (f != null) Destroy(f.gameObject);
        activeFish.Clear();
        fishingLine?.Hide();
    }

    // ── Fish Spawning ──

    private void SpawnFish()
    {
        if (swimArea == null || fishSprites == null || fishSprites.Length == 0) return;

        Rect area = swimArea.rect;
        float margin = 80f;
        float minX = area.xMin + margin;
        float maxX = area.xMax - margin;
        float minY = area.yMin + 40f;
        float maxY = area.yMax - 40f;

        // Distribute fish in lanes
        int count = Mathf.Min(fishOnScreen, fishSprites.Length);
        float laneHeight = (maxY - minY) / Mathf.Max(count, 1);

        for (int i = 0; i < count; i++)
        {
            int spriteIdx = i % fishSprites.Length;
            var go = new GameObject($"Fish_{fishIds[spriteIdx]}_{i}");
            go.transform.SetParent(swimArea, false);

            var fish = go.AddComponent<SwimmingFish>();
            float startX = Random.Range(minX + 50, maxX - 50);
            float y = minY + laneHeight * i + laneHeight * 0.5f;
            float speed = Random.Range(fishSpeedMin, fishSpeedMax);
            bool goRight = Random.value > 0.5f;

            fish.Init(fishSprites[spriteIdx], fishIds[spriteIdx],
                      minX, maxX, startX, y, speed, goRight);

            // Wire tap handler
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = fish.fishImage;
            btn.transition = Selectable.Transition.None;
            int capturedIdx = i;
            btn.onClick.AddListener(() => OnFishTapped(capturedIdx));

            activeFish.Add(fish);
        }
    }

    private void RespawnFishPool()
    {
        // Clear and respawn with new random arrangement
        foreach (var f in activeFish)
            if (f != null) Destroy(f.gameObject);
        activeFish.Clear();
        SpawnFish();

        // Ensure target fish is present
        EnsureTargetFishExists();
    }

    private void EnsureTargetFishExists()
    {
        if (currentTargetIndex < 0 || currentTargetIndex >= fishIds.Length) return;

        string targetId = fishIds[currentTargetIndex];
        bool found = false;
        foreach (var f in activeFish)
        {
            if (f != null && f.fishId == targetId)
            {
                found = true;
                break;
            }
        }

        if (!found && activeFish.Count > 0)
        {
            // Replace a random non-target fish with the target
            int replaceIdx = Random.Range(0, activeFish.Count);
            var fish = activeFish[replaceIdx];
            fish.fishId = targetId;
            fish.fishImage.sprite = fishSprites[currentTargetIndex];
        }
    }

    // ── Target Selection ──

    private void PickNewTarget()
    {
        // Pick a fish type not used recently
        List<int> available = new List<int>();
        for (int i = 0; i < fishSprites.Length; i++)
        {
            if (!usedTargets.Contains(i))
                available.Add(i);
        }

        if (available.Count == 0)
        {
            // All used — reset but avoid last target
            available.Clear();
            for (int i = 0; i < fishSprites.Length; i++)
            {
                if (usedTargets.Count == 0 || i != usedTargets[usedTargets.Count - 1])
                    available.Add(i);
            }
            usedTargets.Clear();
        }

        currentTargetIndex = available[Random.Range(0, available.Count)];
        usedTargets.Add(currentTargetIndex);

        // Update speech bubble
        if (speechBubbleFish != null)
        {
            speechBubbleFish.sprite = fishSprites[currentTargetIndex];

            // Pop animation
            StartCoroutine(BubblePopAnimation());
        }

        // Ensure at least one target fish is swimming
        EnsureTargetFishExists();

        roundStartTime = Time.time;
    }

    private IEnumerator BubblePopAnimation()
    {
        if (speechBubbleFish == null) yield break;
        var rt = speechBubbleFish.GetComponent<RectTransform>();
        Vector3 orig = rt.localScale;

        rt.localScale = orig * 0.5f;
        float t = 0;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            float p = t / 0.25f;
            float s = 0.5f + 0.5f * Mathf.Sin(p * Mathf.PI * 0.5f);
            // Overshoot
            if (p > 0.6f) s = 1f + 0.1f * (1f - p) / 0.4f;
            rt.localScale = orig * s;
            yield return null;
        }
        rt.localScale = orig;
    }

    // ── Player Interaction ──

    private void OnFishTapped(int fishIndex)
    {
        if (state != FishingState.WaitingForInput) return;
        if (IsInputLocked) return;
        if (fishIndex < 0 || fishIndex >= activeFish.Count) return;

        var fish = activeFish[fishIndex];
        if (fish == null || fish.locked) return;

        state = FishingState.Casting;
        fish.Lock();

        // Cast line toward the fish
        Vector2 fishPos = fish.rt.anchoredPosition;

        // Convert positions to line's coordinate space
        var lineParent = fishingLine.lineImage.parent as RectTransform;
        Vector2 fishWorldPos = GetPositionInParent(fish.rt, lineParent);
        Vector2 rodPos = GetPositionInParent(rodTipRT, lineParent);
        fishingLine.SetRodPosition(rodPos);

        bool isCorrect = fish.fishId == fishIds[currentTargetIndex];

        fishingLine.Cast(fishWorldPos, 0.4f, () =>
        {
            // Hook reaction
            fish.PlayHookReaction();

            if (isCorrect)
            {
                OnCorrectCatch(fish);
            }
            else
            {
                OnIncorrectCatch(fish);
            }
        });
    }

    private void OnCorrectCatch(SwimmingFish fish)
    {
        Stats?.RecordCorrect("catch", fish.fishId);
        Stats?.SetCustom($"round{successfulCatches}_time", Time.time - roundStartTime);

        // Pull fish to rod tip (convert rod position to fish's parent space)
        Vector2 rodInFishSpace = GetPositionInParent(rodTipRT, swimArea);
        fishingLine.PullFish(fish.rt, rodInFishSpace, 0.8f, () =>
        {
            successfulCatches++;
            UpdateProgress();

            if (successfulCatches >= TotalCatches)
            {
                state = FishingState.RoundTransition;
                CompleteRound();
            }
            else
            {
                // Next target
                state = FishingState.RoundTransition;
                StartCoroutine(NextRoundTransition());
            }
        });
    }

    private void OnIncorrectCatch(SwimmingFish fish)
    {
        Stats?.RecordMistake("wrong_fish", $"target={fishIds[currentTargetIndex]},tapped={fish.fishId}");

        // Retract line, release fish
        fishingLine.Retract(0.4f, () =>
        {
            fish.Unlock();
            state = FishingState.WaitingForInput;
        });
    }

    private IEnumerator NextRoundTransition()
    {
        yield return new WaitForSeconds(0.6f);

        // Respawn fish pool for variety
        RespawnFishPool();
        PickNewTarget();

        state = FishingState.WaitingForInput;
    }

    // ── UI ──

    private void UpdateProgress()
    {
        if (progressText != null)
            HebrewText.SetText(progressText, $"{successfulCatches}/{TotalCatches}");
    }

    // ── Helpers ──

    /// <summary>
    /// Converts a RectTransform's anchored position to the coordinate space of another parent.
    /// </summary>
    private Vector2 GetPositionInParent(RectTransform source, RectTransform targetParent)
    {
        Vector3 worldPos = source.position;
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetParent, RectTransformUtility.WorldToScreenPoint(null, worldPos), null, out localPos);
        return localPos;
    }

    // ── Navigation ──
    public void OnHomePressed() => ExitGame();

    // ── Boat Bobbing ──
    private float boatBaseY;
    private bool boatBaseSet;

    private void LateUpdate()
    {
        if (elroeyRT == null) return;
        if (!boatBaseSet)
        {
            boatBaseY = elroeyRT.anchoredPosition.y;
            boatBaseSet = true;
        }

        float bob = Mathf.Sin(Time.time * 1.2f) * 5f;
        var pos = elroeyRT.anchoredPosition;
        pos.y = boatBaseY + bob;
        elroeyRT.anchoredPosition = pos;

        // Rod tip follows boat
        if (rodTipRT != null)
        {
            var rodPos = rodTipRT.anchoredPosition;
            rodPos.y += bob * 0.01f; // subtle sync
        }
    }
}
