using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Auto-attaches PopInEffect to children of layout groups (Grid, Vertical, Horizontal)
/// on every scene load, creating a staggered bouncy scale-in animation.
/// Uses [RuntimeInitializeOnLoadMethod] — no manual setup needed.
/// </summary>
public class PopInAnimator : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        var go = new GameObject("PopInAnimator");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<PopInAnimator>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartCoroutine(DelayedInstall());
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(DelayedInstall());
    }

    IEnumerator DelayedInstall()
    {
        // Wait one frame so dynamically-created UI has time to initialize
        yield return null;
        InstallPopInEffects();
    }

    static readonly string[] SkipNames = { "SafeArea", "TopBar", "Header" };

    static bool IsSkippedAncestor(Transform t)
    {
        var current = t;
        while (current != null)
        {
            for (int i = 0; i < SkipNames.Length; i++)
            {
                if (current.name == SkipNames[i])
                    return true;
            }
            current = current.parent;
        }
        return false;
    }

    static bool IsInsideScrollRect(Transform t)
    {
        var current = t;
        while (current != null)
        {
            if (current.GetComponent<ScrollRect>() != null)
                return true;
            current = current.parent;
        }
        return false;
    }

    static void InstallPopInEffects()
    {
        // Only apply to MainMenu and SelectionMenu — game scenes manage their own pop-in
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName != "MainMenu" && sceneName != "SelectionMenu"
            && sceneName != "ProfileSelection" && sceneName != "HomeScene")
            return;

        // Process GridLayoutGroups
        var grids = FindObjectsByType<GridLayoutGroup>(FindObjectsSortMode.None);
        foreach (var grid in grids)
            ProcessLayoutGroup(grid.transform);

        // Process VerticalLayoutGroups
        var verticals = FindObjectsByType<VerticalLayoutGroup>(FindObjectsSortMode.None);
        foreach (var vl in verticals)
            ProcessLayoutGroup(vl.transform);

        // Process HorizontalLayoutGroups
        var horizontals = FindObjectsByType<HorizontalLayoutGroup>(FindObjectsSortMode.None);
        foreach (var hl in horizontals)
            ProcessLayoutGroup(hl.transform);
    }

    static void ProcessLayoutGroup(Transform layoutTransform)
    {
        // Skip layout groups inside SafeArea/TopBar/Header
        if (IsSkippedAncestor(layoutTransform))
            return;

        // Skip layout groups inside ScrollRect content
        if (IsInsideScrollRect(layoutTransform))
            return;

        // Count active children
        int activeCount = 0;
        for (int i = 0; i < layoutTransform.childCount; i++)
        {
            if (layoutTransform.GetChild(i).gameObject.activeInHierarchy)
                activeCount++;
        }

        // Skip if fewer than 2 active children
        if (activeCount < 2)
            return;

        int delayIndex = 0;
        for (int i = 0; i < layoutTransform.childCount; i++)
        {
            var child = layoutTransform.GetChild(i);

            // Skip inactive children
            if (!child.gameObject.activeInHierarchy)
                continue;

            // Skip if already has PopInEffect or another script is managing scale
            if (child.GetComponent<PopInEffect>() != null)
                continue;
            if (child.localScale != Vector3.one)
                continue;

            var effect = child.gameObject.AddComponent<PopInEffect>();
            effect.delay = delayIndex * 0.05f;
            delayIndex++;
        }
    }
}

/// <summary>
/// Scales a UI element from 0 to 1 with an elastic overshoot (0 -> 1.1 -> 1.0).
/// Uses unscaled time so it works even when timeScale=0.
/// </summary>
public class PopInEffect : MonoBehaviour
{
    [HideInInspector] public float delay;

    const float Duration = 0.3f;
    const float Overshoot = 1.1f;

    void Start()
    {
        transform.localScale = Vector3.zero;
        StartCoroutine(Animate());
    }

    IEnumerator Animate()
    {
        // Wait for the stagger delay (unscaled)
        if (delay > 0f)
        {
            float waited = 0f;
            while (waited < delay)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        float elapsed = 0f;
        while (elapsed < Duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Duration);
            float scale = EaseOutBack(t);
            transform.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }

        // Ensure we land exactly at 1
        transform.localScale = Vector3.one;

        // Self-remove to keep hierarchy clean
        Destroy(this);
    }

    /// <summary>
    /// Ease-out-back curve: overshoots to ~1.1 then settles to 1.0.
    /// </summary>
    static float EaseOutBack(float t)
    {
        // Custom overshoot factor tuned so peak is ~1.1
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        float shifted = t - 1f;
        float value = 1f + c3 * shifted * shifted * shifted + c1 * shifted * shifted;

        // Clamp the peak overshoot so it doesn't go above Overshoot
        return Mathf.Min(value, Overshoot);
    }
}
