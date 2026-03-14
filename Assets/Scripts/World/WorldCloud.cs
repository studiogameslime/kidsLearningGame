using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A single moving cloud in the World scene. Moves left or right based on speed sign.
/// Positive speed = moving right, negative speed = moving left.
/// Tappable: squash-bounce + cute rain shower from the cloud.
/// </summary>
public class WorldCloud : MonoBehaviour
{
    public float speed;       // positive = right, negative = left
    public float leftBound;   // x position at which we're off-screen left
    public float rightBound;  // x position at which we're off-screen right

    private RectTransform rt;
    private bool isBouncing;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (rt == null) return;
        rt.anchoredPosition += new Vector2(speed * Time.deltaTime, 0f);
    }

    public Vector2 GetPosition()
    {
        return rt != null ? rt.anchoredPosition : Vector2.zero;
    }

    public bool IsOffScreen()
    {
        if (rt == null) return true;
        float x = rt.anchoredPosition.x;
        return x < leftBound || x > rightBound;
    }

    public void OnTap()
    {
        if (isBouncing) return;
        StartCoroutine(TapReaction());
    }

    private IEnumerator TapReaction()
    {
        isBouncing = true;

        // Squash-bounce
        float dur = 0.1f;
        float elapsed = 0f;
        Vector3 squash = new Vector3(1.2f, 0.85f, 1f);
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(transform.localScale, squash, elapsed / dur);
            yield return null;
        }

        elapsed = 0f;
        dur = 0.15f;
        Vector3 baseScale = Vector3.one * transform.localScale.z; // preserve cloud scale
        // Recover original uniform scale from the z component (unaffected by squash)
        float uniformScale = transform.localScale.z;
        Vector3 target = Vector3.one * uniformScale;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
            transform.localScale = Vector3.Lerp(squash, target, t);
            yield return null;
        }
        transform.localScale = target;

        // Spawn rain
        SpawnRain();

        isBouncing = false;
    }

    private void SpawnRain()
    {
        // Create a temporary GameObject with ParticleSystem for rain
        var rainGO = new GameObject("Rain");
        rainGO.transform.SetParent(transform.parent, false);

        // Position rain at the bottom of this cloud in world space
        // Convert cloud's UI position to the parent's local space
        rainGO.transform.position = transform.position;

        var ps = rainGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 1.2f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(120f, 200f);
        main.startSize = new ParticleSystem.MinMaxCurve(3f, 6f);
        main.startColor = new Color(0.55f, 0.75f, 0.95f, 0.7f); // soft blue
        main.maxParticles = 40;
        main.gravityModifier = 0.3f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0f, 15, 25),
            new ParticleSystem.Burst(0.3f, 10, 15)
        });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(60f, 5f, 1f);
        shape.rotation = new Vector3(90f, 0, 0); // emit downward

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.55f, 0.75f, 0.95f), 0f),
                new GradientColorKey(new Color(0.65f, 0.82f, 1f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.7f, 0f),
                new GradientAlphaKey(0.4f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.5f, 0.8f),
            new Keyframe(1f, 0.3f)
        ));

        // Ensure the particle system renders in UI space
        var renderer = rainGO.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = 10;

        ps.Play();

        // Auto-destroy after particles finish
        Destroy(rainGO, main.duration + main.startLifetime.constantMax + 0.5f);
    }
}
