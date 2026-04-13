using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A single puzzle piece that can be rotated by tapping.
/// Each tap rotates 90° clockwise with a smooth animation.
/// Solved when rotation returns to 0° (original orientation).
/// </summary>
public class SpinPiece : MonoBehaviour, IPointerClickHandler
{
    public int Index { get; private set; }
    public int CurrentRotationSteps { get; private set; } // 0-3 (0=solved)
    public bool IsSolved => CurrentRotationSteps == 0;

    private System.Action<SpinPiece> onTapped;
    private bool isAnimating;

    public void Init(int index, int initialRotationSteps, System.Action<SpinPiece> tapCallback)
    {
        Index = index;
        CurrentRotationSteps = initialRotationSteps % 4;
        onTapped = tapCallback;

        // Apply initial rotation immediately
        transform.localEulerAngles = new Vector3(0, 0, -CurrentRotationSteps * 90f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isAnimating || IsSolved) return;
        onTapped?.Invoke(this);
    }

    /// <summary>
    /// Rotate 90° clockwise with animation.
    /// </summary>
    public void RotateStep(MonoBehaviour runner)
    {
        if (isAnimating) return;
        CurrentRotationSteps = (CurrentRotationSteps + 1) % 4;
        runner.StartCoroutine(AnimateRotation());
    }

    private System.Collections.IEnumerator AnimateRotation()
    {
        isAnimating = true;

        float startZ = transform.localEulerAngles.z;
        float targetZ = startZ - 90f;
        float duration = 0.2f;
        float t = 0;

        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            // Smooth ease-out
            float ease = 1f - (1f - p) * (1f - p);
            float z = Mathf.LerpAngle(startZ, targetZ, ease);
            transform.localEulerAngles = new Vector3(0, 0, z);
            yield return null;
        }

        transform.localEulerAngles = new Vector3(0, 0, targetZ);

        // Snap to exact angle to avoid float drift
        float snapped = Mathf.Round(targetZ / 90f) * 90f;
        transform.localEulerAngles = new Vector3(0, 0, snapped);

        // Solved visual feedback
        if (IsSolved)
        {
            // Quick scale pop
            Vector3 orig = transform.localScale;
            t = 0;
            while (t < 0.15f)
            {
                t += Time.deltaTime;
                float s = 1f + 0.08f * Mathf.Sin(Mathf.Clamp01(t / 0.15f) * Mathf.PI);
                transform.localScale = orig * s;
                yield return null;
            }
            transform.localScale = orig;

            // Disable further clicks
            var rawImg = GetComponent<RawImage>();
            if (rawImg != null)
                rawImg.color = new Color(1f, 1f, 1f, 1f);
        }

        isAnimating = false;
    }
}
