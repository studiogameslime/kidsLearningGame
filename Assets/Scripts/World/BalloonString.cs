using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates a wavy curly ribbon below a balloon using small rotated segments.
/// Sways gently over time for a natural hanging ribbon feel.
/// </summary>
public class BalloonString : MonoBehaviour
{
    public Color ribbonColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);

    private const int SegmentCount = 10;
    private const float SegmentWidth = 2.5f;
    private const float WaveAmplitude = 6f;
    private const float WaveFrequency = 2.5f;
    private const float SwaySpeed = 1.2f;
    private const float SwayAmount = 2f;

    private RectTransform[] segments;
    private RectTransform rt;
    private float swayPhase;

    private void Start()
    {
        rt = GetComponent<RectTransform>();
        swayPhase = Random.Range(0f, Mathf.PI * 2f);
        BuildRibbon();
    }

    private void BuildRibbon()
    {
        float totalHeight = rt.sizeDelta.y;
        float segmentHeight = totalHeight / SegmentCount;

        segments = new RectTransform[SegmentCount];

        for (int i = 0; i < SegmentCount; i++)
        {
            var segGO = new GameObject($"Seg{i}");
            segGO.transform.SetParent(transform, false);

            var segRT = segGO.AddComponent<RectTransform>();
            segRT.pivot = new Vector2(0.5f, 1f);

            // Position along the sine wave curve
            float t = (float)i / SegmentCount;
            float tNext = (float)(i + 1) / SegmentCount;

            float y0 = -t * totalHeight;
            float y1 = -tNext * totalHeight;
            float x0 = Mathf.Sin(t * Mathf.PI * WaveFrequency) * WaveAmplitude;
            float x1 = Mathf.Sin(tNext * Mathf.PI * WaveFrequency) * WaveAmplitude;

            // Position at start of segment
            segRT.anchorMin = new Vector2(0.5f, 1f);
            segRT.anchorMax = new Vector2(0.5f, 1f);
            segRT.anchoredPosition = new Vector2(x0, y0);

            // Calculate angle to next point
            float dx = x1 - x0;
            float dy = y1 - y0;
            float angle = Mathf.Atan2(dx, -dy) * Mathf.Rad2Deg;
            segRT.localRotation = Quaternion.Euler(0, 0, -angle);

            // Length is the distance between the two points
            float segLen = Mathf.Sqrt(dx * dx + dy * dy);
            segRT.sizeDelta = new Vector2(SegmentWidth, segLen + 1f); // +1 overlap

            var img = segGO.AddComponent<Image>();
            // Fade ribbon towards the end
            float fade = Mathf.Lerp(1f, 0.4f, t);
            img.color = new Color(ribbonColor.r, ribbonColor.g, ribbonColor.b, ribbonColor.a * fade);
            img.raycastTarget = false;

            segments[i] = segRT;
        }
    }

    private void Update()
    {
        if (segments == null) return;

        float totalHeight = rt.sizeDelta.y;
        float sway = Mathf.Sin(Time.time * SwaySpeed + swayPhase) * SwayAmount;

        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] == null) continue;

            float t = (float)i / SegmentCount;
            float tNext = (float)(i + 1) / SegmentCount;

            // Apply sway that increases towards the bottom
            float swayT = t * t * sway;
            float swayTNext = tNext * tNext * sway;

            float y0 = -t * totalHeight;
            float y1 = -tNext * totalHeight;
            float x0 = Mathf.Sin(t * Mathf.PI * WaveFrequency) * WaveAmplitude + swayT;
            float x1 = Mathf.Sin(tNext * Mathf.PI * WaveFrequency) * WaveAmplitude + swayTNext;

            segments[i].anchoredPosition = new Vector2(x0, y0);

            float dx = x1 - x0;
            float dy = y1 - y0;
            float angle = Mathf.Atan2(dx, -dy) * Mathf.Rad2Deg;
            segments[i].localRotation = Quaternion.Euler(0, 0, -angle);
        }
    }
}
