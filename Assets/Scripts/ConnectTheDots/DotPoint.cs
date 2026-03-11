using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// A single numbered dot in the Connect-the-Dots game.
/// Shows its number, handles tap, and animates when activated.
/// </summary>
public class DotPoint : MonoBehaviour
{
    [Header("References")]
    public Image dotImage;
    public Image ringImage;
    public TextMeshProUGUI numberText;

    [HideInInspector] public int dotIndex; // 0-based order
    [HideInInspector] public Vector2 normalizedPosition; // 0-1 position within play area

    private Action<DotPoint> onTapped;
    private bool isActivated;
    private Button button;

    private static readonly Color InactiveColor = new Color(0.75f, 0.75f, 0.75f, 1f);
    private static readonly Color ActiveColor = new Color(0.3f, 0.85f, 0.4f, 1f);   // green
    private static readonly Color NextColor = new Color(1f, 0.65f, 0.2f, 1f);        // orange highlight
    private static readonly Color RingColor = new Color(1f, 1f, 1f, 0.5f);

    public void Init(int index, int displayNumber, Action<DotPoint> tapCallback)
    {
        dotIndex = index;
        onTapped = tapCallback;
        isActivated = false;

        if (numberText != null)
            numberText.text = displayNumber.ToString();

        if (dotImage != null)
            dotImage.color = InactiveColor;

        if (ringImage != null)
        {
            ringImage.color = RingColor;
            ringImage.gameObject.SetActive(false);
        }

        // Ensure the root has a raycast-target Image so the Button can receive clicks
        var rootImg = GetComponent<Image>();
        if (rootImg == null)
        {
            rootImg = gameObject.AddComponent<Image>();
            rootImg.color = new Color(0, 0, 0, 0); // invisible
            rootImg.raycastTarget = true;
        }

        button = GetComponent<Button>();
        if (button == null)
            button = gameObject.AddComponent<Button>();

        button.transition = Selectable.Transition.None;
        button.targetGraphic = rootImg;
        button.onClick.AddListener(HandleTap);
    }

    public void SetAsNext()
    {
        if (isActivated) return;
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

    private void HandleTap()
    {
        if (isActivated) return;
        onTapped?.Invoke(this);
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
