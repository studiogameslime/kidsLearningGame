using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// Adds a press-down scale + bounce-back animation to any UI element.
/// Attach to a GameObject with a Button (auto-attached by ButtonEffectsInstaller).
/// </summary>
public class ButtonPressEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private Vector3 _originalScale;
    private Coroutine _anim;

    void Awake()
    {
        _originalScale = transform.localScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(ScaleTo(_originalScale * 0.9f, 0.08f));
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(BounceBack());
    }

    private IEnumerator ScaleTo(Vector3 target, float duration)
    {
        Vector3 start = transform.localScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.Lerp(start, target, t);
            yield return null;
        }
        transform.localScale = target;
        _anim = null;
    }

    private IEnumerator BounceBack()
    {
        Vector3 start = transform.localScale;
        float duration = 0.15f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Overshoot to 1.05 then settle to 1.0
            float bounce = 1f + 0.05f * Mathf.Sin(t * Mathf.PI);
            transform.localScale = _originalScale * bounce;
            yield return null;
        }
        transform.localScale = _originalScale;
        _anim = null;
    }

    void OnDisable()
    {
        // Reset scale if disabled mid-animation
        if (_anim != null)
        {
            StopCoroutine(_anim);
            _anim = null;
        }
        transform.localScale = _originalScale;
    }
}
