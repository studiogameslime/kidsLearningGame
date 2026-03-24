using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// Adds a press-down scale + bounce-back animation to any UI element.
/// Attach to a GameObject with a Button (auto-attached by ButtonEffectsInstaller).
/// </summary>
public class ButtonPressEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private Coroutine _anim;

    private Vector3 OriginalScale
    {
        get
        {
            // Always use current scale (or Vector3.one if mid-animation/zero)
            // Never cache — other animations may modify scale
            Vector3 s = transform.localScale;
            return s.x < 0.5f ? Vector3.one : s;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(ScaleTo(OriginalScale * 0.9f, 0.08f));
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
        Vector3 target = OriginalScale;
        Vector3 start = transform.localScale;
        float duration = 0.15f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float bounce = 1f + 0.05f * Mathf.Sin(t * Mathf.PI);
            transform.localScale = target * bounce;
            yield return null;
        }
        transform.localScale = target;
        _anim = null;
    }

    void OnDisable()
    {
        if (_anim != null)
        {
            StopCoroutine(_anim);
            _anim = null;
        }
        // Don't force scale — other animations may be managing it
    }
}
