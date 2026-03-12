using UnityEngine;

/// <summary>
/// Persistent background music player. Auto-creates itself on game start
/// and survives scene transitions via DontDestroyOnLoad.
/// </summary>
public class BackgroundMusicManager : MonoBehaviour
{
    private static BackgroundMusicManager _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
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
        source.volume = 0.3f;
        source.playOnAwake = false;
        source.Play();
    }
}
