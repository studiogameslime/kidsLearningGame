using System.Collections;
using UnityEngine;

/// <summary>
/// Reusable confetti burst for win celebrations.
/// Attach to any Canvas. Call Play() to fire confetti.
/// Creates its own ParticleSystem on a worldspace camera overlay.
/// </summary>
public class ConfettiController : MonoBehaviour
{
    private ParticleSystem ps;
    private static ConfettiController _instance;

    /// <summary>
    /// Get or create the singleton confetti controller.
    /// Safe to call from any scene — auto-creates if missing.
    /// </summary>
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
        CreateParticleSystem();
    }

    private void CreateParticleSystem()
    {
        var psGO = new GameObject("ConfettiParticles");
        psGO.transform.SetParent(transform, false);
        // Position in front of UI camera — top center, raining down
        psGO.transform.position = new Vector3(0f, 6f, 0f);

        ps = psGO.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 2f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.gravityModifier = 0.6f;
        main.maxParticles = 300;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.3f, 0.4f),
            new Color(0.3f, 0.8f, 1f)
        );
        main.playOnAwake = false;

        // Emission — big burst
        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, 120, 180)
        });

        // Shape — wide cone from top
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 45f;
        shape.radius = 3f;
        shape.rotation = new Vector3(0f, 0f, 180f); // point downward

        // Color over lifetime — fade out at end
        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 0.7f),
                new GradientColorKey(Color.white, 1f),
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.6f),
                new GradientAlphaKey(0f, 1f),
            }
        );
        colorOverLife.color = new ParticleSystem.MinMaxGradient(gradient);

        // Rotation over lifetime — tumble
        var rotOverLife = ps.rotationOverLifetime;
        rotOverLife.enabled = true;
        rotOverLife.z = new ParticleSystem.MinMaxCurve(-180f * Mathf.Deg2Rad, 180f * Mathf.Deg2Rad);

        // Size over lifetime — slight shrink
        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.5f, 1f),
            new Keyframe(1f, 0.3f)
        );
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Velocity over lifetime — slight spread
        var velOverLife = ps.velocityOverLifetime;
        velOverLife.enabled = true;
        velOverLife.x = new ParticleSystem.MinMaxCurve(-1f, 1f);

        // Use default particle material
        var renderer = psGO.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = 9999; // render on top of everything

        // Try to use a sprite material, fall back to default
        var mat = new Material(Shader.Find("Particles/Standard Unlit"));
        if (mat != null)
        {
            mat.SetFloat("_Mode", 0); // Additive or Alpha Blended
            mat.color = Color.white;
        }
        renderer.material = mat;

        ps.Stop();
    }

    /// <summary>
    /// Fire confetti burst. Can be called multiple times.
    /// </summary>
    public void Play()
    {
        if (ps == null) return;
        ps.Clear();
        ps.Play();
    }

    /// <summary>
    /// Stop confetti immediately.
    /// </summary>
    public void Stop()
    {
        if (ps == null) return;
        ps.Stop();
        ps.Clear();
    }
}
