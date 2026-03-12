using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// A single numbered dot in the Connect-the-Dots game.
/// Shows its number, highlights when next, and animates when activated by drag.
/// </summary>
public class DotPoint : MonoBehaviour
{
    [Header("References")]
    public Image dotImage;
    public Image ringImage;
    public TextMeshProUGUI numberText;

    [HideInInspector] public int dotIndex;
    [HideInInspector] public Vector2 normalizedPosition;

    private bool isActivated;

    private static readonly Color InactiveColor = new Color(0.75f, 0.75f, 0.75f, 1f);
    private static readonly Color ActiveColor = new Color(0.3f, 0.85f, 0.4f, 1f);
    private static readonly Color NextColor = new Color(1f, 0.65f, 0.2f, 1f);
    private static readonly Color RingColor = new Color(1f, 1f, 1f, 0.5f);

    public void Init(int index, int displayNumber, Action<DotPoint> tapCallback)
    {
        dotIndex = index;
        isActivated = false;

        if (numberText != null)
            numberText.text = displayNumber.ToString();

        if (dotImage != null)
        {
            dotImage.color = InactiveColor;
            dotImage.raycastTarget = false;
        }

        if (ringImage != null)
        {
            ringImage.color = RingColor;
            ringImage.gameObject.SetActive(false);
        }

        // Disable any raycast on root so drag is not blocked
        var rootImg = GetComponent<Image>();
        if (rootImg != null)
            rootImg.raycastTarget = false;
    }

    public void SetAsNext()
    {
        if (dotImage != null)
            dotImage.color = NextColor;
        if (ringImage != null)
        {
            ringImage.gameObject.SetActive(true);
            StartCoroutine(PulseRing());
        }
    }

    public void Activate()
    {
        isActivated = true;
        if (dotImage != null)
            dotImage.color = ActiveColor;
        if (ringImage != null)
        {
            StopAllCoroutines();
            ringImage.gameObject.SetActive(false);
        }
        StartCoroutine(BounceAnimation());
    }

    public void SetInactive()
    {
        if (dotImage != null)
            dotImage.color = InactiveColor;
        if (ringImage != null)
            ringImage.gameObject.SetActive(false);
    }

    private IEnumerator BounceAnimation()
    {
        Vector3 orig = transform.localScale;
        Vector3 big = orig * 1.4f;
        float dur = 0.12f;

        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(orig, big, t / dur);
            yield return null;
        }

        t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(big, orig, t / dur);
            yield return null;
        }
        transform.localScale = orig;
    }

    private IEnumerator PulseRing()
    {
        while (true)
        {
            float t = 0f;
            float dur = 0.8f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float a = 0.2f + 0.4f * Mathf.Sin(t / dur * Mathf.PI);
                if (ringImage != null)
                    ringImage.color = new Color(1f, 1f, 1f, a);
                yield return null;
            }
        }
    }
}
