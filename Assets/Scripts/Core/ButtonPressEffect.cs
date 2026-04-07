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
    private Vector3 _savedScale;
    private bool _scaleSaved;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_anim != null) StopCoroutine(_anim);
        // Save the scale BEFORE pressing down
        if (!_scaleSaved)
        {
            _savedScale = transform.localScale;
            if (_savedScale.x < 0.5f) _savedScale = Vector3.one;
            _scaleSaved = true;
        }
        _anim = StartCoroutine(ScaleTo(_savedScale * 0.9f, 0.08f));
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
        Vector3 target = _scaleSaved ? _savedScale : Vector3.one;
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
        _scaleSaved = false;
    }

    void OnDisable()
    {
        if (_anim != null)
        {
            StopCoroutine(_anim);
            _anim = null;
        }
        // Restore original scale if we saved it
        if (_scaleSaved)
        {
            transform.localScale = _savedScale;
            _scaleSaved = false;
        }
    }
}
