using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Animated tutorial hand overlay. Plays a sprite-frame animation in a loop
/// until dismissed (usually on first player interaction). Tracks per-profile
/// whether the tutorial was already shown so it only appears once.
///
/// Supports two modes:
/// 1. Static — plays animation in place (Tap, DrawCircle, etc.)
/// 2. Moving — moves from point A to B while playing animation (drag demos)
///
/// Call SetMovePath(from, to) before Show() to enable moving mode.
/// Call SetPosition(pos) to reposition the hand on a real game element.
/// </summary>
public class TutorialHand : MonoBehaviour
{
    [Header("Animation")]
    public Sprite[] frames;
    public float fps = 20f;
    public float loopDelay = 0.6f;

    [Header("Settings")]
    public string tutorialKey;

    private Image _image;
    private CanvasGroup _group;
    private RectTransform _rt;
    private Coroutine _animCoroutine;
    private bool _dismissed;

    // Move path (optional)
    private bool _hasMovePath;
    private Vector2 _moveFrom;
    private Vector2 _moveTo;
    private float _moveDuration = 1f;

    // Tilt animation (optional — rotates in place like a steering wheel)
    private bool _isTiltMode;
    private float _tiltAngle = 15f;
    private float _tiltDuration = 1.3f;

    void Awake()
    {
        _image = GetComponent<Image>();
        _rt = GetComponent<RectTransform>();
        _group = GetComponent<CanvasGroup>();
        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _group.interactable = false;

        // Render above everything else
        var overrideCanvas = GetComponent<Canvas>();
        if (overrideCanvas == null) overrideCanvas = gameObject.AddComponent<Canvas>();
        overrideCanvas.overrideSorting = true;
        overrideCanvas.sortingOrder = 999;

        // Ensure no GraphicRaycaster — prevents blocking input to game elements below
        var raycaster = GetComponent<UnityEngine.UI.GraphicRaycaster>();
        if (raycaster != null) Destroy(raycaster);

        // Image must not intercept raycasts
        _image.raycastTarget = false;
    }

    void Start()
    {
        if (WasShown())
        {
            _dismissed = true;
            gameObject.SetActive(false);
            return;
        }
        // If Show() was already called (by game controller before our Start),
        // don't hide. Otherwise hide until SetPosition/SetMovePath triggers Show.
        if (_animCoroutine == null)
            _group.alpha = 0f;
    }

    /// <summary>Convert a RectTransform's visual center to this hand's parent local space.</summary>
    public Vector2 GetLocalCenter(RectTransform target)
    {
        // Get the visual center of the target (accounts for non-center pivots)
        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector3 worldCenter = (corners[0] + corners[2]) * 0.5f;

        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, worldCenter);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            transform.parent as RectTransform, screenPos, null, out Vector2 localPos);
        return localPos;
    }

    /// <summary>Reposition the hand on a specific UI element. Restarts animation.</summary>
    public void SetPosition(Vector2 anchoredPos)
    {
        _hasMovePath = false;
        _rt.anchoredPosition = anchoredPos;
        if (!_dismissed) Show();
    }

    /// <summary>Set a move path so the hand animates from A to B (for drag demos). Restarts animation.</summary>
    public void SetMovePath(Vector2 from, Vector2 to, float duration = 1f)
    {
        _hasMovePath = true;
        _moveFrom = from;
        _moveTo = to;
        _moveDuration = duration;
        _rt.anchoredPosition = from;
        if (!_dismissed) Show();
    }

    /// <summary>Enable tilt animation — rotates the icon side-to-side in a loop.</summary>
    public void SetTiltMode(float angle = 15f, float duration = 1.3f)
    {
        _isTiltMode = true;
        _hasMovePath = false;
        _tiltAngle = angle;
        _tiltDuration = duration;
        if (!_dismissed) Show();
    }

    public void Show()
    {
        if (_dismissed || frames == null || frames.Length == 0) return;
        gameObject.SetActive(true);
        _group.alpha = 1f;
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        if (_isTiltMode)
            _animCoroutine = StartCoroutine(AnimateTiltLoop());
        else if (_hasMovePath)
            _animCoroutine = StartCoroutine(AnimateMoveLoop());
        else
            _animCoroutine = StartCoroutine(AnimateLoop());
    }

    public void Dismiss()
    {
        if (_dismissed) return;
        _dismissed = true;
        MarkShown();
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        if (gameObject.activeInHierarchy)
            StartCoroutine(FadeOut());
        else
            gameObject.SetActive(false);
    }

    /// <summary>Static animation — plays frames in place.</summary>
    private IEnumerator AnimateLoop()
    {
        float interval = 1f / fps;
        while (!_dismissed)
        {
            for (int i = 0; i < frames.Length && !_dismissed; i++)
            {
                _image.sprite = frames[i];
                yield return new WaitForSeconds(interval);
            }
            yield return new WaitForSeconds(loopDelay);
        }
    }

    /// <summary>Moving animation — shows static tap sprite sliding from A to B.</summary>
    private IEnumerator AnimateMoveLoop()
    {
        // Use first frame only (static tap finger, no slide animation)
        if (frames.Length > 0)
            _image.sprite = frames[0];

        while (!_dismissed)
        {
            // Appear at start
            _rt.anchoredPosition = _moveFrom;
            _group.alpha = 1f;

            // Move from A to B
            float elapsed = 0f;
            while (elapsed < _moveDuration && !_dismissed)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / _moveDuration);
                _rt.anchoredPosition = Vector2.Lerp(_moveFrom, _moveTo, t);
                yield return null;
            }

            // Disappear instantly at destination, reappear at start
            _group.alpha = 0f;
            _rt.anchoredPosition = _moveFrom;
            yield return new WaitForSeconds(loopDelay);
            _group.alpha = 1f;
        }
    }

    /// <summary>Tilt animation — rotates icon side-to-side like tilting a device.</summary>
    private IEnumerator AnimateTiltLoop()
    {
        if (frames.Length > 0)
            _image.sprite = frames[0];

        // Smooth tilt: center → left → center → right → center
        while (!_dismissed)
        {
            float elapsed = 0f;
            while (elapsed < _tiltDuration && !_dismissed)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _tiltDuration;
                // Sine wave: 0→-1→0→1→0 over one cycle
                float angle = Mathf.Sin(t * Mathf.PI * 2f) * _tiltAngle;
                _rt.localEulerAngles = new Vector3(0, 0, angle);
                yield return null;
            }
        }
        _rt.localEulerAngles = Vector3.zero;
    }

    private IEnumerator FadeOut()
    {
        float t = 1f;
        while (t > 0)
        {
            t -= Time.deltaTime * 4f;
            _group.alpha = Mathf.Max(0, t);
            yield return null;
        }
        gameObject.SetActive(false);
    }

    private bool WasShown()
    {
        if (string.IsNullOrEmpty(tutorialKey)) return false;
        var profile = ProfileManager.ActiveProfile;
        string key = profile != null ? $"tut_{profile.id}_{tutorialKey}" : $"tut_default_{tutorialKey}";
        return PlayerPrefs.GetInt(key, 0) == 1;
    }

    private void MarkShown()
    {
        if (string.IsNullOrEmpty(tutorialKey)) return;
        var profile = ProfileManager.ActiveProfile;
        string key = profile != null ? $"tut_{profile.id}_{tutorialKey}" : $"tut_default_{tutorialKey}";
        PlayerPrefs.SetInt(key, 1);
    }
}
