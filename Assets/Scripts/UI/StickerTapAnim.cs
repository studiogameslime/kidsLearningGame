using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Per-category tap animation for stickers in the album.
/// Attached to collected sticker images — plays a fun animation on tap.
/// </summary>
public class StickerTapAnim : MonoBehaviour
{
    public enum AnimType
    {
        Walk,      // animals — hop left-right
        Swim,      // ocean — wave side to side
        Drive,     // vehicles — zoom across and back
        Bounce,    // letters — trampoline bounce
        Roll,      // numbers — spin and roll
        Wobble,    // food — jelly wobble
        Grow,      // nature — sprout and grow
        Float,     // balloons — float up and down
        Spin,      // art — creative spin with color pulse
    }

    public AnimType animType;
    private bool _isAnimating;
    private RectTransform _rt;
    private Vector2 _originalPos;
    private Vector3 _originalScale;
    private Quaternion _originalRot;

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
    }

    public void Play()
    {
        if (_isAnimating) return;
        _originalPos = _rt.anchoredPosition;
        _originalScale = _rt.localScale;
        _originalRot = _rt.localRotation;
        StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        _isAnimating = true;

        switch (animType)
        {
            case AnimType.Walk:    yield return Walk(); break;
            case AnimType.Swim:    yield return Swim(); break;
            case AnimType.Drive:   yield return Drive(); break;
            case AnimType.Bounce:  yield return Bounce(); break;
            case AnimType.Roll:    yield return Roll(); break;
            case AnimType.Wobble:  yield return Wobble(); break;
            case AnimType.Grow:    yield return Grow(); break;
            case AnimType.Float:   yield return Float(); break;
            case AnimType.Spin:    yield return Spin(); break;
        }

        // Reset
        _rt.anchoredPosition = _originalPos;
        _rt.localScale = _originalScale;
        _rt.localRotation = _originalRot;
        _isAnimating = false;
    }

    // ── Animals: hop left-right like walking ──
    private IEnumerator Walk()
    {
        float dur = 0.8f, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float hopY = Mathf.Abs(Mathf.Sin(p * Mathf.PI * 4f)) * 15f;
            float moveX = Mathf.Sin(p * Mathf.PI * 2f) * 20f;
            _rt.anchoredPosition = _originalPos + new Vector2(moveX, hopY);
            // Flip direction at halfway
            float scaleX = p > 0.25f && p < 0.75f ? -_originalScale.x : _originalScale.x;
            _rt.localScale = new Vector3(scaleX, _originalScale.y + hopY * 0.003f, 1f);
            yield return null;
        }
    }

    // ── Ocean: wave side to side like swimming ──
    private IEnumerator Swim()
    {
        float dur = 1.0f, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float waveX = Mathf.Sin(p * Mathf.PI * 3f) * 25f;
            float waveY = Mathf.Sin(p * Mathf.PI * 5f) * 6f;
            float tilt = Mathf.Sin(p * Mathf.PI * 3f) * 10f;
            _rt.anchoredPosition = _originalPos + new Vector2(waveX, waveY);
            _rt.localRotation = Quaternion.Euler(0, 0, tilt);
            yield return null;
        }
    }

    // ── Vehicles: zoom right, brake, reverse back ──
    private IEnumerator Drive()
    {
        float dur = 0.7f, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float moveX;
            if (p < 0.4f) moveX = (p / 0.4f) * 40f; // accelerate right
            else if (p < 0.6f) moveX = 40f; // brake
            else moveX = 40f * (1f - (p - 0.6f) / 0.4f); // reverse back
            float shake = Mathf.Sin(p * Mathf.PI * 15f) * 2f * (1f - p);
            _rt.anchoredPosition = _originalPos + new Vector2(moveX, shake);
            yield return null;
        }
    }

    // ── Letters: trampoline bounce ──
    private IEnumerator Bounce()
    {
        float dur = 0.8f, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float bounceH = Mathf.Abs(Mathf.Sin(p * Mathf.PI * 3f)) * 40f * (1f - p);
            float squash = 1f - Mathf.Abs(Mathf.Sin(p * Mathf.PI * 3f)) * 0.15f * (1f - p);
            _rt.anchoredPosition = _originalPos + new Vector2(0, bounceH);
            _rt.localScale = new Vector3(_originalScale.x / squash, _originalScale.y * squash, 1f);
            yield return null;
        }
    }

    // ── Numbers: spin and roll ──
    private IEnumerator Roll()
    {
        float dur = 0.7f, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float angle = p * 720f * (1f - p); // spin fast then slow
            float moveX = Mathf.Sin(p * Mathf.PI) * 30f;
            _rt.anchoredPosition = _originalPos + new Vector2(moveX, 0);
            _rt.localRotation = Quaternion.Euler(0, 0, -angle);
            yield return null;
        }
    }

    // ── Food: jelly wobble ──
    private IEnumerator Wobble()
    {
        float dur = 0.6f, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float wobble = Mathf.Sin(p * Mathf.PI * 6f) * 12f * (1f - p);
            float scaleX = 1f + Mathf.Sin(p * Mathf.PI * 6f) * 0.1f * (1f - p);
            float scaleY = 1f - Mathf.Sin(p * Mathf.PI * 6f) * 0.1f * (1f - p);
            _rt.localRotation = Quaternion.Euler(0, 0, wobble);
            _rt.localScale = new Vector3(_originalScale.x * scaleX, _originalScale.y * scaleY, 1f);
            yield return null;
        }
    }

    // ── Nature: sprout up and settle ──
    private IEnumerator Grow()
    {
        float dur = 0.7f, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float scale;
            if (p < 0.4f) scale = 1f + (p / 0.4f) * 0.4f; // grow big
            else scale = 1.4f - (p - 0.4f) / 0.6f * 0.4f; // settle back
            float sway = Mathf.Sin(p * Mathf.PI * 4f) * 5f * (1f - p);
            _rt.localScale = _originalScale * scale;
            _rt.localRotation = Quaternion.Euler(0, 0, sway);
            yield return null;
        }
    }

    // ── Balloons: float up, wobble, come back ──
    private IEnumerator Float()
    {
        float dur = 1.0f, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float upY = Mathf.Sin(p * Mathf.PI) * 35f;
            float driftX = Mathf.Sin(p * Mathf.PI * 3f) * 10f;
            float tilt = Mathf.Sin(p * Mathf.PI * 2f) * 8f;
            _rt.anchoredPosition = _originalPos + new Vector2(driftX, upY);
            _rt.localRotation = Quaternion.Euler(0, 0, tilt);
            float breathe = 1f + Mathf.Sin(p * Mathf.PI * 2f) * 0.08f;
            _rt.localScale = _originalScale * breathe;
            yield return null;
        }
    }

    // ── Art: creative spin with scale pulse ──
    private IEnumerator Spin()
    {
        float dur = 0.8f, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float angle = p * 360f;
            float scale = 1f + Mathf.Sin(p * Mathf.PI) * 0.3f;
            _rt.localRotation = Quaternion.Euler(0, 0, angle);
            _rt.localScale = _originalScale * scale;
            yield return null;
        }
    }
}
