using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Automatically tracks time spent in every scene and logs it to Firebase.
/// Auto-creates via RuntimeInitializeOnLoadMethod — no setup needed.
/// Fires scene_duration event on every scene change and app pause.
/// </summary>
public class SceneDurationTracker : MonoBehaviour
{
    private static SceneDurationTracker _instance;
    private string _currentScene;
    private float _sceneStartTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (_instance != null) return;
        var go = new GameObject("[SceneDurationTracker]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<SceneDurationTracker>();
    }

    private void Awake()
    {
        _currentScene = SceneManager.GetActiveScene().name;
        _sceneStartTime = Time.realtimeSinceStartup;
        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;
    }

    private void OnSceneChanged(UnityEngine.SceneManagement.Scene from, UnityEngine.SceneManagement.Scene to)
    {
        LogCurrentScene();
        _currentScene = to.name;
        _sceneStartTime = Time.realtimeSinceStartup;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) LogCurrentScene();
        else _sceneStartTime = Time.realtimeSinceStartup; // reset on resume
    }

    private void OnApplicationQuit()
    {
        LogCurrentScene();
    }

    private void LogCurrentScene()
    {
        if (string.IsNullOrEmpty(_currentScene)) return;
        float duration = Time.realtimeSinceStartup - _sceneStartTime;
        if (duration < 2f) return; // ignore very short visits

        FirebaseAnalyticsManager.LogSceneDuration(_currentScene, duration);
    }
}
