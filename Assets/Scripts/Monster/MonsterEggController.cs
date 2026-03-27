using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Monster egg world object. Shows locked egg with idle animation,
/// handles unlock detection and hatching animation sequence.
/// </summary>
public class MonsterEggController : MonoBehaviour
{
    [Header("References")]
    public Image eggImage;
    public GameObject lockOverlay;
    public TextMeshProUGUI lockText;
    public Image progressBarFill;

    public const int UnlockThreshold = 5;

    private RectTransform rt;
    private float idleTimer;
    private bool isHatching;

    /// <summary>Callback when hatching completes — host should open MonsterCreator.</summary>
    public System.Action onHatchComplete;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    private void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;
        var mp = profile.journey.monster;

        if (mp.monsterCreated)
        {
            // Monster already created — hide egg entirely
            gameObject.SetActive(false);
            return;
        }

        int stars = profile.journey.totalStars;
        bool unlocked = stars >= UnlockThreshold;

        if (unlocked && !mp.hasSeenHatchAnimation)
        {
            // Ready to hatch — play animation
            mp.eggUnlocked = true;
            ProfileManager.Instance.Save();
            ShowLockUI(false);
            StartCoroutine(HatchSequence());
        }
        else if (unlocked && mp.hasSeenHatchAnimation && !mp.monsterCreated)
        {
            // Egg already hatched but monster not created — show egg, ready to tap → open creator
            ShowLockUI(false);
        }
        else
        {
            // Still locked
            ShowLockUI(true);
            UpdateLockProgress(stars);
        }
    }

    private void ShowLockUI(bool locked)
    {
        if (lockOverlay != null) lockOverlay.SetActive(locked);
    }

    private void UpdateLockProgress(int currentStars)
    {
        int remaining = UnlockThreshold - currentStars;
        if (remaining < 0) remaining = 0;

        if (lockText != null)
            HebrewText.SetText(lockText, $"\u05E2\u05D5\u05D3 {remaining} \u05DE\u05E9\u05D7\u05E7\u05D9\u05DD"); // עוד X משחקים

        if (progressBarFill != null)
        {
            var barRT = progressBarFill.GetComponent<RectTransform>();
            float progress = Mathf.Clamp01((float)currentStars / UnlockThreshold);
            barRT.anchorMax = new Vector2(progress, barRT.anchorMax.y);
        }
    }

    // ── Idle Animation ──

    private void Update()
    {
        if (isHatching) return;

        idleTimer += Time.deltaTime;

        // Gentle side-to-side sway + bounce
        float swayX = Mathf.Sin(idleTimer * 1.5f) * 5f;
        float bounceY = Mathf.Abs(Mathf.Sin(idleTimer * 2f)) * 3f;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x + swayX * Time.deltaTime * 0.5f, rt.anchoredPosition.y);

        // Subtle rotation sway
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(idleTimer * 1.2f) * 2f);
    }

    // ── Tap Handling ──

    public void OnEggTapped()
    {
        if (isHatching) return;

        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;
        var mp = profile.journey.monster;

        if (mp.hasSeenHatchAnimation && !mp.monsterCreated)
        {
            // Egg ready — open creator directly
            onHatchComplete?.Invoke();
            return;
        }

        if (!mp.eggUnlocked)
        {
            // Still locked — shake animation
            StartCoroutine(ShakeAnimation());
        }
    }

    private IEnumerator ShakeAnimation()
    {
        float duration = 0.4f;
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            float shake = Mathf.Sin(t * 40f) * 8f * (1f - t / duration);
            rt.localRotation = Quaternion.Euler(0, 0, shake);
            yield return null;
        }
        rt.localRotation = Quaternion.identity;
    }

    // ── Hatching Sequence ──

    private IEnumerator HatchSequence()
    {
        isHatching = true;

        // Phase 1: Shake intensely
        float shakeDuration = 1.5f;
        float t = 0;
        while (t < shakeDuration)
        {
            t += Time.deltaTime;
            float intensity = Mathf.Lerp(3f, 15f, t / shakeDuration);
            float shake = Mathf.Sin(t * 35f) * intensity;
            rt.localRotation = Quaternion.Euler(0, 0, shake);
            yield return null;
        }

        // Phase 2: Flash/pop
        if (eggImage != null)
        {
            for (int i = 0; i < 4; i++)
            {
                eggImage.color = Color.white * 2f; // bright flash
                yield return new WaitForSeconds(0.08f);
                eggImage.color = Color.white;
                yield return new WaitForSeconds(0.08f);
            }
        }

        // Phase 3: Scale pop and disappear
        Vector3 originalScale = rt.localScale;
        t = 0;
        float popDuration = 0.3f;
        while (t < popDuration)
        {
            t += Time.deltaTime;
            float p = t / popDuration;
            float s = 1f + 0.4f * Mathf.Sin(p * Mathf.PI);
            rt.localScale = originalScale * s;
            if (eggImage != null)
                eggImage.color = new Color(1, 1, 1, 1f - p);
            yield return null;
        }

        // Mark as hatched
        var profile = ProfileManager.ActiveProfile;
        if (profile != null)
        {
            profile.journey.monster.hasSeenHatchAnimation = true;
            ProfileManager.Instance.Save();
        }

        rt.localScale = originalScale;
        isHatching = false;

        // Open monster creator
        onHatchComplete?.Invoke();
    }
}
