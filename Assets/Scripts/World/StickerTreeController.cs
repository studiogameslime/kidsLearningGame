using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Sticker Tree in the World scene. The tree grows a sticker automatically
/// every 6 hours. The child can water the tree for fun (visual only).
/// When the sticker is ready, tapping collects it.
/// Notifications are controlled globally from the parent dashboard settings.
/// </summary>
public class StickerTreeController : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite[] treeStages; // 4 stages: seedling → small → medium → full
    public Sprite[] stickerSprites; // available sticker sprites to award

    [Header("Timer")]
    public float growDurationSeconds = 6f * 3600f; // 6 hours

    private Image _treeImage;
    private RectTransform _rt;

    // Sticker bloom UI
    private GameObject _stickerGO;
    private Image _stickerImage;
    private bool _hasStickerReady;
    private int _pendingStickerIndex = -1; // sticker chosen at bloom time, reused at collection

    // Watering animation (visual only)
    private bool _isAnimating;
    private List<GameObject> _waterDrops = new List<GameObject>();

    // Timer UI
    private TextMeshProUGUI _timerText;

    // Current computed stage (driven by elapsed time)
    private int _currentVisualStage = -1;

    // Size per stage
    private static readonly Vector2[] StageSizes = {
        new Vector2(60, 80),
        new Vector2(120, 160),
        new Vector2(200, 260),
        new Vector2(280, 340),
    };

    private bool _isLocked;

    private void Start()
    {
        _treeImage = GetComponent<Image>();
        _rt = GetComponent<RectTransform>();

        if (treeStages == null || treeStages.Length == 0) return;

        // Check if feature is locked
        _isLocked = !FeatureUnlockManager.IsUnlocked(FeatureUnlockManager.Feature.StickerTree);

        CreateTimerUI();

        if (!_isLocked)
            RefreshState();
        else
            ApplyLockedState();
    }

    private void Update()
    {
        if (treeStages == null || treeStages.Length == 0) return;
        if (_isLocked) return; // no growth or timer when locked
        RefreshState();
    }

    private void ApplyLockedState()
    {
        // Show seedling stage, no sticker, no timer
        ApplyStageVisual(0);
        if (_timerText != null) _timerText.text = "";
        if (_stickerGO != null) { Destroy(_stickerGO); _stickerGO = null; }
        _hasStickerReady = false;
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

        // Visual-only watering — fun interaction, no effect on timer
        StartCoroutine(WaterSequence());
    }

    // ── Time-Based Growth ──

    private void RefreshState()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        long lastCollect = GetLastCollectionTime(profile);
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // First ever load: give a sticker immediately
        if (lastCollect == 0)
        {
            if (!_hasStickerReady)
            {
                _hasStickerReady = true;
                BloomSticker(profile);
            }
            UpdateTimerText(0);
            return;
        }

        long elapsed = now - lastCollect;
        float duration = growDurationSeconds;

        if (elapsed >= (long)duration)
        {
            // Sticker ready!
            if (!_hasStickerReady)
            {
                _hasStickerReady = true;
                BloomSticker(profile);
            }
            SetVisualStage(treeStages.Length - 1);
            UpdateTimerText(0);
        }
        else
        {
            // Still growing — compute stage from elapsed time
            float progress = elapsed / duration; // 0..1
            int stage = Mathf.Clamp(Mathf.FloorToInt(progress * treeStages.Length), 0, treeStages.Length - 1);
            SetVisualStage(stage);

            long remaining = (long)duration - elapsed;
            UpdateTimerText(remaining);

            // Destroy sticker if somehow visible during growth
            if (_hasStickerReady)
            {
                _hasStickerReady = false;
                if (_stickerGO != null) { Destroy(_stickerGO); _stickerGO = null; }
            }
        }
    }

    private void SetVisualStage(int stage)
    {
        if (stage == _currentVisualStage) return;

        // Animate transition only if going up (not on first load)
        bool shouldAnimate = _currentVisualStage >= 0 && stage > _currentVisualStage;
        _currentVisualStage = stage;

        if (shouldAnimate)
            StartCoroutine(GrowAnimation(stage));
        else
            ApplyStageVisual(stage);
    }

    private void ApplyStageVisual(int stage)
    {
        if (treeStages != null && stage >= 0 && stage < treeStages.Length)
            _treeImage.sprite = treeStages[stage];
        if (stage >= 0 && stage < StageSizes.Length)
            _rt.sizeDelta = StageSizes[stage];
    }

    // ── Timer UI ──

    private void CreateTimerUI()
    {
        var timerGO = new GameObject("TimerText");
        timerGO.transform.SetParent(transform, false);

        var timerRT = timerGO.AddComponent<RectTransform>();
        timerRT.anchorMin = new Vector2(0.5f, 0f);
        timerRT.anchorMax = new Vector2(0.5f, 0f);
        timerRT.pivot = new Vector2(0.5f, 1f);
        timerRT.anchoredPosition = new Vector2(0, -8);
        timerRT.sizeDelta = new Vector2(200, 40);

        _timerText = timerGO.AddComponent<TextMeshProUGUI>();
        _timerText.fontSize = 22;
        _timerText.alignment = TextAlignmentOptions.Center;
        _timerText.color = new Color(0.35f, 0.25f, 0.15f);
        _timerText.raycastTarget = false;
        _timerText.fontStyle = FontStyles.Bold;
    }

    private void UpdateTimerText(long remainingSeconds)
    {
        if (_timerText == null) return;

        if (_hasStickerReady)
        {
            _timerText.text = "";
            return;
        }

        if (remainingSeconds <= 0)
        {
            _timerText.text = "";
            return;
        }

        int h = (int)(remainingSeconds / 3600);
        int m = (int)((remainingSeconds % 3600) / 60);
        int s = (int)(remainingSeconds % 60);
        _timerText.text = $"{h}:{m:D2}:{s:D2}";
    }

    // ── Sticker Bloom & Collection ──

    private void BloomSticker(UserProfile profile)
    {
        // Check if a sticker was already chosen (persisted from previous session)
        string pendingKey = $"stree_{profile.id}_pendingSticker";
        int savedIndex = PlayerPrefs.GetInt(pendingKey, -1);

        int stickerIndex;
        if (savedIndex >= 0)
        {
            // Reuse the previously chosen sticker
            stickerIndex = savedIndex;
        }
        else
        {
            // Pick a new one
            stickerIndex = GetNextStickerIndex(profile);
            if (stickerIndex < 0)
            {
                _hasStickerReady = false;
                _pendingStickerIndex = -1;
                return;
            }
            // Persist so it doesn't change on scene reload
            PlayerPrefs.SetInt(pendingKey, stickerIndex);
            PlayerPrefs.Save();
        }

        _hasStickerReady = true;
        _pendingStickerIndex = stickerIndex;

        if (_stickerGO != null) Destroy(_stickerGO);
        _stickerGO = new GameObject("StickerReward");
        _stickerGO.transform.SetParent(transform, false);

        var stickerRT = _stickerGO.AddComponent<RectTransform>();
        stickerRT.anchorMin = stickerRT.anchorMax = new Vector2(0.5f, 0.75f);
        stickerRT.pivot = new Vector2(0.5f, 0.5f);
        stickerRT.anchoredPosition = Vector2.zero;
        stickerRT.sizeDelta = new Vector2(80, 80);

        _stickerImage = _stickerGO.AddComponent<Image>();
        if (stickerSprites != null && stickerIndex < stickerSprites.Length && stickerSprites[stickerIndex] != null)
            _stickerImage.sprite = stickerSprites[stickerIndex];
        _stickerImage.preserveAspect = true;
        _stickerImage.raycastTarget = false;

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

        int stickerIndex = _pendingStickerIndex;
        if (stickerIndex < 0) { _isAnimating = false; yield break; }
        string spriteName = (stickerSprites != null && stickerIndex < stickerSprites.Length && stickerSprites[stickerIndex] != null)
            ? stickerSprites[stickerIndex].name.ToLower() : $"{stickerIndex}";
        string stickerId = $"nature_{spriteName}";

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

        // Reset timer — set last collection to now
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        SetLastCollectionTime(profile, now);
        ProfileManager.Instance.Save();

        _hasStickerReady = false;
        _pendingStickerIndex = -1;
        _currentVisualStage = -1; // force visual refresh

        // Clear persisted sticker so a new one is picked next time
        string pendingKey = $"stree_{profile.id}_pendingSticker";
        PlayerPrefs.DeleteKey(pendingKey);
        PlayerPrefs.Save();

        // Schedule notification for next sticker if parent enabled notifications
        if (AppSettings.NotificationsEnabled)
        {
            var fireUtc = DateTimeOffset.FromUnixTimeSeconds(now + (long)growDurationSeconds).UtcDateTime;
            string childName = ProfileManager.ActiveProfile?.displayName ?? "";
            NotificationService.Instance?.ScheduleStickerReady(fireUtc, childName);
        }

        // Play feedback
        SoundLibrary.PlayRandomFeedback();

        // Shrink back to seedling
        yield return StartCoroutine(GrowAnimation(0));
        _currentVisualStage = 0;
        _isAnimating = false;
    }

    // ── Visual-Only Watering ──

    private IEnumerator WaterSequence()
    {
        _isAnimating = true;
        yield return StartCoroutine(ShowWaterDrops());
        yield return StartCoroutine(TreeWiggle());
        _isAnimating = false;
    }

    // ── Animations ──

    private IEnumerator ShowWaterDrops()
    {
        for (int i = 0; i < 5; i++)
        {
            var drop = new GameObject("Drop");
            drop.transform.SetParent(transform, false);
            var dropRT = drop.AddComponent<RectTransform>();
            dropRT.sizeDelta = new Vector2(12, 16);
            dropRT.anchorMin = dropRT.anchorMax = new Vector2(0.5f, 0.8f);
            dropRT.anchoredPosition = new Vector2(UnityEngine.Random.Range(-40f, 40f), UnityEngine.Random.Range(0f, 30f));

            var dropImg = drop.AddComponent<Image>();
            dropImg.color = new Color(0.4f, 0.7f, 1f, 0.8f);
            dropImg.raycastTarget = false;

            _waterDrops.Add(drop);
        }

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

            if (!spriteSwapped && t >= 0.5f)
            {
                spriteSwapped = true;
                if (targetStage >= 0 && targetStage < treeStages.Length)
                    _treeImage.sprite = treeStages[targetStage];
            }

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

    private long GetLastCollectionTime(UserProfile profile)
    {
        // PlayerPrefs doesn't support long, store as string
        string key = $"stree_{profile.id}_lastCollect";
        string val = PlayerPrefs.GetString(key, "");
        if (long.TryParse(val, out long ts)) return ts;
        return 0;
    }

    private void SetLastCollectionTime(UserProfile profile, long timestamp)
    {
        string key = $"stree_{profile.id}_lastCollect";
        PlayerPrefs.SetString(key, timestamp.ToString());
        PlayerPrefs.Save();
    }

    private int GetNextStickerIndex(UserProfile profile)
    {
        var collected = profile.journey.collectedStickerIds ?? new List<string>();
        if (stickerSprites == null || stickerSprites.Length == 0) return -1;

        var available = new List<int>();
        for (int i = 0; i < stickerSprites.Length; i++)
        {
            string spriteName = stickerSprites[i] != null ? stickerSprites[i].name.ToLower() : $"{i}";
            if (!collected.Contains($"nature_{spriteName}"))
                available.Add(i);
        }

        if (available.Count == 0)
            return -1;

        return available[UnityEngine.Random.Range(0, available.Count)];
    }
}
