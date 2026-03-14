using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws a smooth wavy ribbon below a balloon using a procedural texture.
/// A single continuous curved line — no dots or gaps.
/// Sways gently over time.
/// </summary>
public class BalloonString : MonoBehaviour
{
    public Color ribbonColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);

    private const int TexWidth = 32;
    private const int TexHeight = 80;
    private const float WaveAmplitude = 5f;   // pixels of horizontal wave in texture
    private const float WaveFrequency = 2.2f; // number of half-waves along the ribbon
    private const float LineThickness = 1.8f; // stroke width in texture pixels
    private const float SwaySpeed = 1.2f;
    private const float SwayAmount = 3f;

    private RectTransform rt;
    private Image img;
    private Texture2D ribbonTex;
    private float swayPhase;
    private float baseRotation;

    private void Start()
    {
        rt = GetComponent<RectTransform>();
        swayPhase = Random.Range(0f, Mathf.PI * 2f);
        baseRotation = 0f;

        BuildRibbonTexture();

        img = GetComponent<Image>();
        if (img == null) img = gameObject.AddComponent<Image>();
        img.sprite = Sprite.Create(ribbonTex,
            new Rect(0, 0, TexWidth, TexHeight),
            new Vector2(0.5f, 1f), 100f);
        img.color = ribbonColor;
        img.raycastTarget = false;
        img.preserveAspect = false;
    }

    private void BuildRibbonTexture()
    {
        ribbonTex = new Texture2D(TexWidth, TexHeight, TextureFormat.RGBA32, false);
        ribbonTex.filterMode = FilterMode.Bilinear;
        ribbonTex.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[TexWidth * TexHeight];
        // Clear to transparent
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(0, 0, 0, 0);

        float centerX = TexWidth * 0.5f;

        // Draw smooth anti-aliased sine wave line
        for (int y = 0; y < TexHeight; y++)
        {
            float t = (float)y / TexHeight;
            // Sine wave X offset
            float waveX = Mathf.Sin(t * Mathf.PI * WaveFrequency) * WaveAmplitude;
            float cx = centerX + waveX;

            // Fade alpha towards the bottom
            float fade = Mathf.Lerp(1f, 0.35f, t * t);

            // Draw anti-aliased thick point at this Y
            for (int x = 0; x < TexWidth; x++)
            {
                float dist = Mathf.Abs(x - cx);
                if (dist < LineThickness + 1f)
                {
                    // Smooth falloff for anti-aliasing
                    float alpha = Mathf.Clamp01(1f - (dist - LineThickness + 0.5f));
                    alpha *= fade;
                    byte a = (byte)(alpha * 255);
                    if (a > pixels[y * TexWidth + x].a)
                        pixels[y * TexWidth + x] = new Color32(255, 255, 255, a);
                }
            }
        }

        ribbonTex.SetPixels32(pixels);
        ribbonTex.Apply();
    }

    private void Update()
    {
        if (rt == null) return;
        // Gentle sway rotation
        float sway = Mathf.Sin(Time.time * SwaySpeed + swayPhase) * SwayAmount;
        rt.localRotation = Quaternion.Euler(0, 0, sway);
    }

    private void OnDestroy()
    {
        if (ribbonTex != null)
            Destroy(ribbonTex);
    }
}
