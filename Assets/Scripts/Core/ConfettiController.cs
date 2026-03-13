using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reusable UI-based confetti burst for win celebrations.
/// Creates its own overlay canvas with falling confetti pieces.
/// Call Play() to fire. Works on top of any ScreenSpaceOverlay UI.
/// </summary>
public class ConfettiController : MonoBehaviour
{
    private static ConfettiController _instance;

    private Canvas confettiCanvas;
    private RectTransform canvasRT;
    private List<RectTransform> pool = new List<RectTransform>();
    private bool isPlaying;

    private const int ParticleCount = 80;
    private const float Duration = 3f;

    private static readonly Color[] ConfettiColors = {
        new Color(1f, 0.3f, 0.4f),     // red
        new Color(1f, 0.65f, 0.2f),     // orange
        new Color(1f, 0.9f, 0.2f),      // yellow
        new Color(0.3f, 0.85f, 0.4f),   // green
        new Color(0.3f, 0.7f, 1f),      // blue
        new Color(0.68f, 0.51f, 0.93f), // purple
        new Color(1f, 0.5f, 0.7f),      // pink
        new Color(0.2f, 0.9f, 0.9f),    // cyan
    };

    public static ConfettiController Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("ConfettiController");
                _instance = go.AddComponent<ConfettiController>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        CreateCanvas();
        CreatePool();
    }

    private void CreateCanvas()
    {
        var canvasGO = new GameObject("ConfettiCanvas");
        canvasGO.transform.SetParent(transform, false);

        confettiCanvas = canvasGO.AddComponent<Canvas>();
        confettiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        confettiCanvas.sortingOrder = 999;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0f;

        canvasRT = canvasGO.GetComponent<RectTransform>();

        // No GraphicRaycaster — confetti should not block input
    }

    private void CreatePool()
    {
        for (int i = 0; i < ParticleCount; i++)
        {
            var go = new GameObject($"Confetti_{i}");
            go.transform.SetParent(confettiCanvas.transform, false);

            var rt = go.AddComponent<RectTransform>();
            // Random rectangular confetti pieces
            float w = Random.Range(16f, 32f);
            float h = Random.Range(8f, 20f);
            rt.sizeDelta = new Vector2(w, h);

            var img = go.AddComponent<Image>();
            img.color = ConfettiColors[i % ConfettiColors.Length];
            img.raycastTarget = false;

            go.SetActive(false);
            pool.Add(rt);
        }
    }

    public void Play()
    {
        if (isPlaying) return;
        GameCompletionBridge.Instance?.OnConfettiPlayed();
        SoundLibrary.PlayRandomFeedback();
        StartCoroutine(PlayConfetti(ParticleCount));
    }

    public void PlayBig()
    {
        if (isPlaying) return;
        // Expand pool if needed for double confetti
        while (pool.Count < ParticleCount * 2)
        {
            var go = new GameObject($"Confetti_{pool.Count}");
            go.transform.SetParent(confettiCanvas.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(Random.Range(16f, 32f), Random.Range(8f, 20f));
            var img = go.AddComponent<Image>();
            img.color = ConfettiColors[pool.Count % ConfettiColors.Length];
            img.raycastTarget = false;
            go.SetActive(false);
            pool.Add(rt);
        }
        StartCoroutine(PlayConfetti(ParticleCount * 2));
    }

    public void Stop()
    {
        StopAllCoroutines();
        isPlaying = false;
        foreach (var rt in pool)
            if (rt != null) rt.gameObject.SetActive(false);
    }

    private IEnumerator PlayConfetti(int count)
    {
        isPlaying = true;

        float canvasW = canvasRT.rect.width > 0 ? canvasRT.rect.width : 1080f;
        float canvasH = canvasRT.rect.height > 0 ? canvasRT.rect.height : 1920f;
        float halfW = canvasW * 0.5f;
        float topY = canvasH * 0.5f + 50f;

        // Initialize each piece with random properties
        float[] startX = new float[count];
        float[] fallSpeed = new float[count];
        float[] swaySpeed = new float[count];
        float[] swayAmount = new float[count];
        float[] rotSpeed = new float[count];
        float[] delay = new float[count];

        for (int i = 0; i < count; i++)
        {
            startX[i] = Random.Range(-halfW, halfW);
            fallSpeed[i] = Random.Range(600f, 1200f);
            swaySpeed[i] = Random.Range(2f, 5f);
            swayAmount[i] = Random.Range(30f, 120f);
            rotSpeed[i] = Random.Range(-360f, 360f);
            delay[i] = Random.Range(0f, 0.4f);

            var rt = pool[i];
            rt.anchoredPosition = new Vector2(startX[i], topY);
            rt.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

            // Randomize color
            var img = rt.GetComponent<Image>();
            img.color = ConfettiColors[Random.Range(0, ConfettiColors.Length)];

            // Randomize size
            float w = Random.Range(16f, 32f);
            float h = Random.Range(8f, 20f);
            rt.sizeDelta = new Vector2(w, h);

            rt.gameObject.SetActive(true);
        }

        float elapsed = 0f;
        float bottomY = -canvasH * 0.5f - 100f;

        while (elapsed < Duration)
        {
            elapsed += Time.deltaTime;

            for (int i = 0; i < count; i++)
            {
                float t = elapsed - delay[i];
                if (t < 0f) continue;

                var rt = pool[i];
                float x = startX[i] + Mathf.Sin(t * swaySpeed[i]) * swayAmount[i];
                float y = topY - t * fallSpeed[i];

                rt.anchoredPosition = new Vector2(x, y);
                rt.localRotation = Quaternion.Euler(0, 0, rotSpeed[i] * t);

                // Fade out in last 0.5s
                if (elapsed > Duration - 0.5f)
                {
                    float alpha = (Duration - elapsed) / 0.5f;
                    var img = rt.GetComponent<Image>();
                    Color c = img.color;
                    img.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(alpha));
                }

                // Hide if fallen off screen
                if (y < bottomY)
                    rt.gameObject.SetActive(false);
            }

            yield return null;
        }

        // Clean up
        foreach (var rt in pool)
            if (rt != null) rt.gameObject.SetActive(false);

        isPlaying = false;
    }
}
