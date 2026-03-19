using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Visual component for a single cell in the Number Maze grid.
/// Handles default, completed, and error states with animations.
/// </summary>
public class NumberMazeCellView : MonoBehaviour
{
    private Image bgImage;
    private Image borderImage;
    private TextMeshProUGUI numberText;
    private RectTransform rt;
    private int _cellIndex;

    private static readonly Color DefaultBg      = HexColor("#FFFFFF");
    private static readonly Color DefaultBorder   = HexColor("#E0E0E0");
    private static readonly Color DefaultText     = HexColor("#37474F");
    private static readonly Color CompletedBg     = HexColor("#C8E6C9");
    private static readonly Color CompletedBorder = HexColor("#66BB6A");
    private static readonly Color CompletedText   = HexColor("#2E7D32");
    private static readonly Color NextTargetBg    = HexColor("#FFF9C4");
    private static readonly Color NextTargetBorder = HexColor("#FFB74D");
    private static readonly Color ErrorBg         = HexColor("#FFCDD2");
    // Start cell (number 1): bright green
    private static readonly Color StartBg         = HexColor("#A5D6A7");
    private static readonly Color StartBorder     = HexColor("#43A047");
    private static readonly Color StartText       = HexColor("#1B5E20");
    // End cell (target number): bright orange/gold
    private static readonly Color EndBg           = HexColor("#FFE082");
    private static readonly Color EndBorder       = HexColor("#FFA000");
    private static readonly Color EndText         = HexColor("#E65100");

    public int CellIndex => _cellIndex;

    public void Init(Image bg, Image border, TextMeshProUGUI text, int cellIndex)
    {
        bgImage = bg;
        borderImage = border;
        numberText = text;
        rt = GetComponent<RectTransform>();
        _cellIndex = cellIndex;
    }

    public void SetDefault(int number)
    {
        bgImage.color = DefaultBg;
        if (borderImage != null) borderImage.color = DefaultBorder;
        numberText.color = DefaultText;
        numberText.text = number.ToString();
        transform.localScale = Vector3.one;
    }

    /// <summary>Start cell (1): green highlight, slightly larger.</summary>
    public void SetStart(int number)
    {
        bgImage.color = StartBg;
        if (borderImage != null) borderImage.color = StartBorder;
        numberText.color = StartText;
        numberText.text = number.ToString();
        transform.localScale = Vector3.one * 1.05f;
    }

    /// <summary>End cell (target): gold/orange highlight, slightly larger.</summary>
    public void SetEnd(int number)
    {
        bgImage.color = EndBg;
        if (borderImage != null) borderImage.color = EndBorder;
        numberText.color = EndText;
        numberText.text = number.ToString();
        transform.localScale = Vector3.one * 1.05f;
    }

    public void SetCompleted()
    {
        StartCoroutine(DoCompleted());
    }

    private IEnumerator DoCompleted()
    {
        bgImage.color = CompletedBg;
        if (borderImage != null) borderImage.color = CompletedBorder;
        numberText.color = CompletedText;

        // Bounce
        float dur = 0.25f;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float p = t / dur;
            float scale = 1f + 0.15f * Mathf.Sin(p * Mathf.PI);
            transform.localScale = Vector3.one * scale;
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

    public void ShowError()
    {
        StartCoroutine(DoError());
    }

    private IEnumerator DoError()
    {
        Color origBg = bgImage.color;
        bgImage.color = ErrorBg;

        Vector2 orig = rt.anchoredPosition;
        float dur = 0.25f;
        float amp = 10f;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float p = t / dur;
            float offset = Mathf.Sin(p * Mathf.PI * 6f) * amp * (1f - p);
            rt.anchoredPosition = orig + new Vector2(offset, 0);
            yield return null;
        }
        rt.anchoredPosition = orig;
        bgImage.color = origBg;
    }

    public void PulseHint()
    {
        StartCoroutine(DoPulseHint());
    }

    private IEnumerator DoPulseHint()
    {
        for (int i = 0; i < 3; i++)
        {
            bgImage.color = NextTargetBg;
            if (borderImage != null) borderImage.color = NextTargetBorder;
            float dur = 0.3f;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                float scale = 1f + 0.08f * Mathf.Sin((t / dur) * Mathf.PI);
                transform.localScale = Vector3.one * scale;
                yield return null;
            }
            transform.localScale = Vector3.one;
            bgImage.color = DefaultBg;
            if (borderImage != null) borderImage.color = DefaultBorder;
            yield return new WaitForSeconds(0.1f);
        }
    }

    public void BounceIn(float delay)
    {
        StartCoroutine(DoBounceIn(delay));
    }

    private IEnumerator DoBounceIn(float delay)
    {
        transform.localScale = Vector3.zero;
        if (delay > 0) yield return new WaitForSeconds(delay);
        float dur = 0.2f;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float p = t / dur;
            float scale = 1f + 0.12f * Mathf.Sin(p * Mathf.PI);
            transform.localScale = Vector3.one * scale;
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
