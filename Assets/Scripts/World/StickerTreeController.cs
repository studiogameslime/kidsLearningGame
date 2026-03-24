using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sticker Tree in the World scene. The child waters the tree to grow it.
/// Every 5 waterings it advances to the next growth stage (4 stages total).
/// After watering the final stage 5 times, a sticker blooms on the tree.
/// Tapping the sticker collects it and resets the tree to stage 0.
/// </summary>
public class StickerTreeController : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite[] treeStages; // 4 stages: seedling → small → medium → full
    public Sprite[] stickerSprites; // available sticker sprites to award

    [Header("Settings")]
    public int wateringsPerStage = 5;

    private Image _treeImage;
    private RectTransform _rt;

    // Sticker bloom UI
    private GameObject _stickerGO;
    private Image _stickerImage;
    private bool _hasStickerReady;

    // Watering animation
    private bool _isAnimating;

    // Particle-like water drops
    private List<GameObject> _waterDrops = new List<GameObject>();

    // Size per stage (seedling is tiny, full tree is large)
    private static readonly Vector2[] StageSizes = {
        new Vector2(60, 80),    // seedling - very small
        new Vector2(120, 160),  // small sprout
        new Vector2(200, 260),  // medium tree
        new Vector2(280, 340),  // full tree
    };

    private void Start()
    {
        _treeImage = GetComponent<Image>();
        _rt = GetComponent<RectTransform>();

        if (treeStages == null || treeStages.Length == 0) return;

        LoadState();
        UpdateVisual();
    }

    /// <summary>Called by WorldInputHandler when player taps the tree.</summary>
    public void OnTap()
    {
        if (_isAnimating) return;

        if (_hasStickerReady)
        {
            CollectSticker();
            return;
        }

        StartCoroutine(WaterSequence());
    }

    private IEnumerator WaterSequence()
    {
        _isAnimating = true;

        // Water drop animation
        yield return StartCoroutine(ShowWaterDrops());

        // Tree wiggle
        yield return StartCoroutine(TreeWiggle());

        // Increment watering count
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) { _isAnimating = false; yield break; }

        int currentStage = GetCurrentStage(profile);
        int waterings = GetWaterings(profile);
        waterings++;

        if (waterings >= wateringsPerStage)
        {
            waterings = 0;
            if (currentStage < treeStages.Length - 1)
            {
                // Grow to next stage
                currentStage++;
                SetCurrentStage(profile, currentStage);
                yield return StartCoroutine(GrowAnimation(currentStage));
            }
            else
            {
                // Final stage fully watered → bloom sticker
                BloomSticker(profile);
            }
        }

        SetWaterings(profile, waterings);
        ProfileManager.Instance.Save();
        UpdateVisual();

        _isAnimating = false;
    }

    private void BloomSticker(UserProfile profile)
    {
        // Determine which sticker to give (random uncollected)
        int stickerIndex = GetNextStickerIndex(profile);
        if (stickerIndex < 0) return; // all stickers collected — nothing to bloom

        _hasStickerReady = true;

        // Create sticker floating above tree
        if (_stickerGO != null) Destroy(_stickerGO);
        _stickerGO = new GameObject("StickerReward");
        _stickerGO.transform.SetParent(transform, false);

        var stickerRT = _stickerGO.AddComponent<RectTransform>();
        stickerRT.anchorMin = stickerRT.anchorMax = new Vector2(0.5f, 0.75f);
        stickerRT.pivot = new Vector2(0.5f, 0.5f);
        stickerRT.anchoredPosition = new Vector2(0, 0);
        stickerRT.sizeDelta = new Vector2(80, 80);

        _stickerImage = _stickerGO.AddComponent<Image>();
        if (stickerSprites != null && stickerIndex < stickerSprites.Length && stickerSprites[stickerIndex] != null)
            _stickerImage.sprite = stickerSprites[stickerIndex];
        _stickerImage.preserveAspect = true;
        _stickerImage.raycastTarget = false;

        // Add glow/pulse
        StartCoroutine(PulseSticker(stickerRT));
    }

    private void CollectSticker()
    {
        _isAnimating = true;
        StartCoroutine(CollectSequence());
    }

    private IEnumerator CollectSequence()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) { _isAnimating = false; yield break; }

        // Determine sticker ID
        int stickerIndex = GetNextStickerIndex(profile);
        string stickerId = $"sticker_{stickerIndex}";

        // Fly sticker up and fade
        if (_stickerGO != null)
        {
            var stickerRT = _stickerGO.GetComponent<RectTransform>();
            var stickerCG = _stickerGO.AddComponent<CanvasGroup>();
            Vector2 startPos = stickerRT.anchoredPosition;
            float elapsed = 0f;
            float dur = 0.6f;

            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                stickerRT.anchoredPosition = startPos + new Vector2(0, 200 * t);
                stickerRT.localScale = Vector3.one * (1f + 0.5f * t);
                stickerCG.alpha = 1f - t * t;
                yield return null;
            }

            Destroy(_stickerGO);
            _stickerGO = null;
        }

        // Save sticker to profile
        if (profile.journey.collectedStickerIds == null)
            profile.journey.collectedStickerIds = new List<string>();
        if (!profile.journey.collectedStickerIds.Contains(stickerId))
            profile.journey.collectedStickerIds.Add(stickerId);

        // Reset tree to seedling
        SetCurrentStage(profile, 0);
        SetWaterings(profile, 0);
        ProfileManager.Instance.Save();

        _hasStickerReady = false;

        // Play feedback
        SoundLibrary.PlayRandomFeedback();

        // Shrink animation back to seedling
        yield return StartCoroutine(GrowAnimation(0));
        UpdateVisual();

        _isAnimating = false;
    }

    // ── Animations ──

    private IEnumerator ShowWaterDrops()
    {
        // Create simple water drops that fall
        for (int i = 0; i < 5; i++)
        {
            var drop = new GameObject("Drop");
            drop.transform.SetParent(transform, false);
            var dropRT = drop.AddComponent<RectTransform>();
            dropRT.sizeDelta = new Vector2(12, 16);
            dropRT.anchorMin = dropRT.anchorMax = new Vector2(0.5f, 0.8f);
            dropRT.anchoredPosition = new Vector2(Random.Range(-40f, 40f), Random.Range(0f, 30f));

            var dropImg = drop.AddComponent<Image>();
            dropImg.color = new Color(0.4f, 0.7f, 1f, 0.8f);
            dropImg.raycastTarget = false;

            _waterDrops.Add(drop);
        }

        // Animate drops falling
        float elapsed = 0f;
        float dur = 0.5f;
        var startPositions = new List<Vector2>();
        foreach (var d in _waterDrops)
            startPositions.Add(d.GetComponent<RectTransform>().anchoredPosition);

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            for (int i = 0; i < _waterDrops.Count; i++)
            {
                if (_waterDrops[i] == null) continue;
                var drt = _waterDrops[i].GetComponent<RectTransform>();
                drt.anchoredPosition = startPositions[i] + new Vector2(0, -120 * t);
                var dimg = _waterDrops[i].GetComponent<Image>();
                dimg.color = new Color(0.4f, 0.7f, 1f, 0.8f * (1f - t));
            }
            yield return null;
        }

        foreach (var d in _waterDrops)
            if (d != null) Destroy(d);
        _waterDrops.Clear();
    }

    private IEnumerator TreeWiggle()
    {
        float elapsed = 0f;
        float dur = 0.4f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float angle = Mathf.Sin(t * Mathf.PI * 4f) * 5f * (1f - t);
            _rt.localRotation = Quaternion.Euler(0, 0, angle);
            yield return null;
        }
        _rt.localRotation = Quaternion.identity;
    }

    private IEnumerator GrowAnimation(int targetStage)
    {
        Vector2 startSize = _rt.sizeDelta;
        Vector2 endSize = (targetStage >= 0 && targetStage < StageSizes.Length)
            ? StageSizes[targetStage] : startSize;

        bool spriteSwapped = false;
        float elapsed = 0f;
        float dur = 0.5f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / dur);
            _rt.sizeDelta = Vector2.Lerp(startSize, endSize, t);

            // Swap sprite at midpoint for smooth transition
            if (!spriteSwapped && t >= 0.5f)
            {
                spriteSwapped = true;
                if (targetStage >= 0 && targetStage < treeStages.Length)
                    _treeImage.sprite = treeStages[targetStage];
            }

            // Bounce overshoot
            float scale = 1f + 0.1f * Mathf.Sin(t * Mathf.PI);
            _rt.localScale = Vector3.one * scale;
            yield return null;
        }

        _rt.sizeDelta = endSize;
        _rt.localScale = Vector3.one;
    }

    private IEnumerator PulseSticker(RectTransform stickerRT)
    {
        float time = 0f;
        while (_hasStickerReady && stickerRT != null)
        {
            time += Time.deltaTime;
            float scale = 1f + 0.15f * Mathf.Sin(time * 3f);
            stickerRT.localScale = Vector3.one * scale;
            yield return null;
        }
    }

    // ── State Persistence ──

    private void UpdateVisual()
    {
        var profile = ProfileManager.ActiveProfile;
        int stage = profile != null ? GetCurrentStage(profile) : 0;

        if (treeStages != null && stage >= 0 && stage < treeStages.Length)
            _treeImage.sprite = treeStages[stage];

        // Set size based on stage
        if (stage >= 0 && stage < StageSizes.Length)
            _rt.sizeDelta = StageSizes[stage];
    }

    private void LoadState()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        int stage = GetCurrentStage(profile);
        int waterings = GetWaterings(profile);

        // Check if sticker is ready (final stage + fully watered)
        if (stage >= treeStages.Length - 1 && waterings >= wateringsPerStage)
        {
            _hasStickerReady = true;
            BloomSticker(profile);
        }
    }

    private int GetCurrentStage(UserProfile profile)
    {
        return PlayerPrefs.GetInt($"stree_{profile.id}_stage", 0);
    }

    private void SetCurrentStage(UserProfile profile, int stage)
    {
        PlayerPrefs.SetInt($"stree_{profile.id}_stage", stage);
    }

    private int GetWaterings(UserProfile profile)
    {
        return PlayerPrefs.GetInt($"stree_{profile.id}_water", 0);
    }

    private void SetWaterings(UserProfile profile, int water)
    {
        PlayerPrefs.SetInt($"stree_{profile.id}_water", water);
    }

    private int GetNextStickerIndex(UserProfile profile)
    {
        var collected = profile.journey.collectedStickerIds ?? new List<string>();
        int total = (stickerSprites != null && stickerSprites.Length > 0) ? stickerSprites.Length : 12;

        // Build list of uncollected sticker indices
        var available = new List<int>();
        for (int i = 0; i < total; i++)
        {
            if (!collected.Contains($"sticker_{i}"))
                available.Add(i);
        }

        if (available.Count == 0)
            return -1; // all collected — no more stickers

        return available[Random.Range(0, available.Count)];
    }
}
