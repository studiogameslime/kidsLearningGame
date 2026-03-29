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
        source.mute = !AppSettings.MusicEnabled;
        source.Play();
    }

    private AudioSource feedbackSource; // Alin voice feedback (lower priority)
    private float contentEndTime;       // when current content clip finishes

    private AudioSource GetSfxSource()
    {
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }
        return sfxSource;
    }

    private AudioSource GetFeedbackSource()
    {
        if (feedbackSource == null)
        {
            feedbackSource = gameObject.AddComponent<AudioSource>();
            feedbackSource.playOnAwake = false;
        }
        return feedbackSource;
    }

    /// <summary>
    /// Play a one-shot audio clip that survives scene transitions.
    /// Respects AppSettings.VoiceEnabled — if voice is muted, does nothing.
    /// </summary>
    /// <summary>
    /// Play a one-shot content clip (animal name, color name, number).
    /// High priority — blocks feedback from playing until this finishes.
    /// </summary>
    public static void PlayOneShot(AudioClip clip, float volume = 1f)
    {
        if (clip == null || _instance == null) return;
        if (!AppSettings.VoiceEnabled) return;

        // Prevent playing the same clip while it's still playing
        if (_instance._lastClip == clip && Time.time - _instance._lastClipTime < clip.length * 0.5f)
            return;

        _instance._lastClip = clip;
        _instance._lastClipTime = Time.time;
        _instance.contentEndTime = Time.time + clip.length;
        _instance.GetSfxSource().PlayOneShot(clip, volume);
    }

    /// <summary>
    /// Play a feedback clip (Alin voice, praise).
    /// Lower priority — skipped if a content clip is currently playing.
    /// </summary>
    public static void PlayFeedback(AudioClip clip, float volume = 1f)
    {
        if (clip == null || _instance == null) return;
        if (!AppSettings.VoiceEnabled) return;

        // Don't play feedback if content sound is still playing
        if (Time.time < _instance.contentEndTime)
            return;

        _instance.GetFeedbackSource().PlayOneShot(clip, volume);
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

    /// <summary>
    /// Mute/unmute voice/SFX audio source.
    /// </summary>
    public static void SetSfxMuted(bool muted)
    {
        if (_instance == null) return;
        var src = _instance.sfxSource;
        if (src != null)
            src.mute = muted;
    }
}
