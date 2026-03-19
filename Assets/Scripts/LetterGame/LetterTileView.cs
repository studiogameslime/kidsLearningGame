using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Visual component for a single letter tile in the word display.
/// Supports filled, missing (pulsing "?"), and reveal states.
/// </summary>
public class LetterTileView : MonoBehaviour
{
    private Image bgImage;
    private Image borderImage;
    private TextMeshProUGUI letterText;
    private RectTransform rt;

    private static readonly Color FilledBg     = HexColor("#FFFFFF");
    private static readonly Color FilledBorder  = HexColor("#E0E0E0");
    private static readonly Color FilledText    = HexColor("#333333");
    private static readonly Color MissingBg     = HexColor("#FFF9C4");
    private static readonly Color MissingBorder = HexColor("#FFB74D");
    private static readonly Color MissingText   = HexColor("#BDBDBD");

    private Coroutine _pulseCoroutine;

    public void Init(Image bg, Image border, TextMeshProUGUI text)
    {
        bgImage = bg;
        borderImage = border;
        letterText = text;
        rt = GetComponent<RectTransform>();
    }

    public void SetFilled(char letter)
    {
        StopPulse();
        bgImage.color = FilledBg;
        if (borderImage != null) borderImage.color = FilledBorder;
        letterText.color = FilledText;
        letterText.fontSize = 56;
        letterText.fontStyle = FontStyles.Bold;
        letterText.text = letter.ToString();
        transform.localScale = Vector3.one;
    }

    public void SetMissing()
    {
        bgImage.color = MissingBg;
        if (borderImage != null) borderImage.color = MissingBorder;
        letterText.color = MissingText;
        letterText.fontSize = 56;
        letterText.fontStyle = FontStyles.Bold;
        letterText.text = "?";
        _pulseCoroutine = StartCoroutine(PulseLoop());
    }

    public void RevealLetter(char letter, float delay)
    {
        StartCoroutine(DoReveal(letter, delay));
    }

    private IEnumerator DoReveal(char letter, float delay)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);
        StopPulse();

        // Animate scale bounce
        float dur = 0.3f;
        letterText.text = letter.ToString();
        letterText.color = HexColor("#4CAF50"); // green flash

        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float p = t / dur;
            float scale = 1f + 0.3f * Mathf.Sin(p * Mathf.PI);
            transform.localScale = Vector3.one * scale;
            yield return null;
        }
        transform.localScale = Vector3.one;

        // Fade to normal filled state
        bgImage.color = FilledBg;
        if (borderImage != null) borderImage.color = FilledBorder;
        letterText.color = FilledText;
    }

    public void BounceIn(float delay)
    {
        StartCoroutine(DoBounceIn(delay));
    }

    private IEnumerator DoBounceIn(float delay)
    {
        transform.localScale = Vector3.zero;
        if (delay > 0) yield return new WaitForSeconds(delay);
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

    public void WaveBounce(float delay)
    {
        StartCoroutine(DoWaveBounce(delay));
    }

    private IEnumerator DoWaveBounce(float delay)
    {
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

    private IEnumerator PulseLoop()
    {
        while (true)
        {
            float dur = 1.2f;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                float p = t / dur;
                float scale = 1f + 0.06f * Mathf.Sin(p * Mathf.PI * 2f);
                transform.localScale = Vector3.one * scale;
                float a = Mathf.Lerp(0.7f, 1f, 0.5f + 0.5f * Mathf.Sin(p * Mathf.PI * 2f));
                bgImage.color = new Color(MissingBg.r, MissingBg.g, MissingBg.b, a);
                yield return null;
            }
        }
    }

    private void StopPulse()
    {
        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }
        transform.localScale = Vector3.one;
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
