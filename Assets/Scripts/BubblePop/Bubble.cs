using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A single bubble that floats upward with a bubbly wobble and gentle pulse.
/// Has a color that may match the target.
/// </summary>
public class Bubble : MonoBehaviour
{
    [HideInInspector] public Color bubbleColor;

    private float speed;
    private RectTransform rectTransform;
    private float wobbleOffset;
    private float pulseOffset;
    private Vector3 baseScale;

    public void Init(Color color, float floatSpeed, BubblePopController ctrl)
    {
        bubbleColor = color;
        speed = floatSpeed;
        rectTransform = GetComponent<RectTransform>();
        wobbleOffset = Random.Range(0f, Mathf.PI * 2f);
        pulseOffset = Random.Range(0f, Mathf.PI * 2f);
        baseScale = rectTransform.localScale;
    }

    private void Update()
    {
        if (rectTransform == null) return;

        // Float upward with gentle side wobble
        float wobble = Mathf.Sin(Time.time * 2f + wobbleOffset) * 30f * Time.deltaTime;
        rectTransform.anchoredPosition += new Vector2(wobble, speed * Time.deltaTime);

        // Gentle breathing/pulse effect
        float pulse = 1f + 0.05f * Mathf.Sin(Time.time * 3f + pulseOffset);
        rectTransform.localScale = baseScale * pulse;
    }
}
