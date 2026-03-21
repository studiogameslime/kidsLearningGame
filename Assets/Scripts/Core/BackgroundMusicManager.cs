using UnityEngine;

/// <summary>
/// Persistent background music player. Auto-creates itself on game start
/// and survives scene transitions via DontDestroyOnLoad.
/// Also provides a static PlayOneShot for voice/SFX that survives scene loads.
/// </summary>
public class BackgroundMusicManager : MonoBehaviour
{
    private static BackgroundMusicManager _instance;
    private AudioSource sfxSource;
    private AudioClip _lastClip;
    private float _lastClipTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        // Target 60fps on all platforms
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;

        if (_instance != null) return;

        var go = new GameObject("BackgroundMusicManager");
        _instance = go.AddComponent<BackgroundMusicManager>();
        DontDestroyOnLoad(go);
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

        var clip = Resources.Load<AudioClip>("BackgroundMusic");
        if (clip == null)
        {
            Debug.LogWarning("BackgroundMusicManager: Could not find Resources/BackgroundMusic audio clip.");
            return;
        }

        var source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.volume = 0.1f;
        source.playOnAwake = false;
        source.Play();
    }

    private AudioSource GetSfxSource()
    {
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }
        return sfxSource;
    }

    /// <summary>
    /// Play a one-shot audio clip that survives scene transitions.
    /// </summary>
    public static void PlayOneShot(AudioClip clip, float volume = 1f)
    {
        if (clip == null || _instance == null) return;

        // Prevent playing the same clip while it's still playing
        if (_instance._lastClip == clip && Time.time - _instance._lastClipTime < clip.length * 0.5f)
            return;

        _instance._lastClip = clip;
        _instance._lastClipTime = Time.time;
        _instance.GetSfxSource().PlayOneShot(clip, volume);
    }

    /// <summary>
    /// Mute/unmute the background music (e.g. during microphone recording).
    /// </summary>
    public static void SetMuted(bool muted)
    {
        if (_instance == null) return;
        var bgSource = _instance.GetComponent<AudioSource>();
        if (bgSource != null)
            bgSource.mute = muted;
    }
}
