using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A piece of food in the aquarium. Sinks individually with unique drift/wobble.
/// Stays active until eaten by a fish or reaches the floor and fades out.
/// Only eaten food grants progress — expired food does not.
/// </summary>
public class AquariumFood : MonoBehaviour
{
    public AquariumController controller;
    public float floorY;             // Y position considered "floor" (set by controller)
    public float sinkSpeed = 25f;    // base downward speed (randomized per piece)
    public float driftAmp = 12f;     // horizontal sway amplitude
    public float wobbleSpeed = 3f;   // rotation wobble speed
    public float startDelay;         // staggered start for natural scatter

    public bool IsEaten { get; private set; }
    public bool IsExpired { get; private set; } // reached floor, fading out — no longer targetable
    public bool IsValid => !IsEaten && !IsExpired;

    private RectTransform rt;
    private Image img;
    private float elapsed;
    private float driftPhase;
    private float wobblePhase;
    private bool started;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        img = GetComponent<Image>();
        driftPhase = Random.Range(0f, Mathf.PI * 2f);
        wobblePhase = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        if (IsEaten || IsExpired) return;

        // Staggered start delay
        if (!started)
        {
            startDelay -= Time.deltaTime;
            if (startDelay > 0f) return;
            started = true;
        }

        elapsed += Time.deltaTime;

        // Sink downward
        Vector2 pos = rt.anchoredPosition;
        pos.y -= sinkSpeed * Time.deltaTime;

        // Horizontal drift (unique per piece)
        pos.x += Mathf.Sin(driftPhase + elapsed * 1.5f) * driftAmp * Time.deltaTime;

        // Rotation wobble
        float angle = Mathf.Sin(wobblePhase + elapsed * wobbleSpeed) * 15f;
        rt.localRotation = Quaternion.Euler(0, 0, angle);

        rt.anchoredPosition = pos;

        // Check if reached floor
        if (pos.y <= floorY)
        {
            IsExpired = true;
            StartCoroutine(FloorFadeOut());
        }
    }

    /// <summary>Called by AquariumFish when it reaches this food.</summary>
    public void OnEaten(AquariumFish fish)
    {
        if (IsEaten || IsExpired) return;
        IsEaten = true;

        if (controller != null)
            controller.OnFoodEaten();

        StartCoroutine(EatAnimation());
    }

    private IEnumerator EatAnimation()
    {
        float dur = 0.15f;
        float t = 0f;
        Vector3 startScale = rt.localScale;

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            rt.localScale = startScale * (1f - p);
            yield return null;
        }

        Destroy(gameObject);
    }

    private IEnumerator FloorFadeOut()
    {
        float dur = 1.2f;
        float t = 0f;
        Color startColor = img.color;

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            img.color = new Color(startColor.r, startColor.g, startColor.b, startColor.a * (1f - p));
            yield return null;
        }

        Destroy(gameObject);
    }
}
