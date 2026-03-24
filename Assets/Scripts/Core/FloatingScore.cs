using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Static utility that spawns a floating "+1" (or custom text) popup near a UI element.
/// The text floats upward, scales up then down, and fades out before self-destructing.
/// Uses a shared persistent canvas for performance.
/// </summary>
public static class FloatingScore
{
    private const float DefaultDuration = 0.8f;
    private const float FloatDistance = 80f;
    private const float FontSize = 36f;
    private const int SortingOrder = 990;

    private static readonly Color DefaultColor = new Color(1f, 0.843f, 0f); // #FFD700 gold

    private static Canvas _sharedCanvas;
    private static RectTransform _canvasRT;
    private static FloatingScoreRunner _runner;

    /// <summary>
    /// Show a floating score popup near the given UI element.
    /// </summary>
    public static void Show(RectTransform target, string text = "+1", Color? color = null)
    {
        if (target == null) return;

        EnsureCanvas();
        if (_sharedCanvas == null) return;

        Color textColor = color ?? DefaultColor;

        // Create text object
        var textGo = new GameObject("FloatingText");
        textGo.transform.SetParent(_canvasRT, false);

        var rt = textGo.AddComponent<RectTransform>();
        var cg = textGo.AddComponent<CanvasGroup>();
        cg.interactable = false;
        cg.blocksRaycasts = false;

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = FontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = textColor;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;

        // Outline for readability
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = new Color32(0, 0, 0, 180);

        rt.sizeDelta = new Vector2(200f, 60f);

        // Position: convert target's world position to our shared overlay canvas
        Vector3 worldPos = target.position;
        Camera cam = null;
        Canvas targetCanvas = target.GetComponentInParent<Canvas>();
        if (targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = targetCanvas.worldCamera;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRT, screenPoint, null, out Vector2 localPos);

        localPos.y += 30f;
        rt.anchoredPosition = localPos;

        _runner.StartCoroutine(AnimateFloatingScore(rt, cg, localPos, textGo));
    }

    private static void EnsureCanvas()
    {
        if (_sharedCanvas != null) return;

        var canvasGo = new GameObject("_FloatingScoreCanvas");
        Object.DontDestroyOnLoad(canvasGo);
        canvasGo.hideFlags = HideFlags.HideAndDontSave;

        _sharedCanvas = canvasGo.AddComponent<Canvas>();
        _sharedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _sharedCanvas.sortingOrder = SortingOrder;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        _canvasRT = canvasGo.GetComponent<RectTransform>();
        _runner = canvasGo.AddComponent<FloatingScoreRunner>();
    }

    private static IEnumerator AnimateFloatingScore(
        RectTransform rt, CanvasGroup cg, Vector2 startPos, GameObject textGo)
    {
        float elapsed = 0f;

        while (elapsed < DefaultDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / DefaultDuration);

            // Float upward (ease-out)
            float eased = 1f - (1f - t) * (1f - t);
            rt.anchoredPosition = startPos + Vector2.up * FloatDistance * eased;

            // Scale: 1.0 → 1.3 (at t=0.4) → 1.0 (at t=1.0)
            float scale;
            if (t < 0.4f)
                scale = Mathf.Lerp(1f, 1.3f, t / 0.4f);
            else
                scale = Mathf.Lerp(1.3f, 1f, (t - 0.4f) / 0.6f);
            rt.localScale = Vector3.one * scale;

            // Fade out
            if (t < 0.3f)
                cg.alpha = 1f;
            else
                cg.alpha = Mathf.Lerp(1f, 0f, (t - 0.3f) / 0.7f);

            yield return null;
        }

        Object.Destroy(textGo);
    }

    /// <summary>Tiny MonoBehaviour to host the floating score coroutine.</summary>
    private class FloatingScoreRunner : MonoBehaviour { }
}
