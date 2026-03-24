using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Android speech recognizer using the native SpeechRecognizer API.
/// Uses AndroidJavaProxy to implement RecognitionListener in C#.
/// All Android callbacks are dispatched to Unity's main thread via a queue.
/// </summary>
public class AndroidSpeechRecognizer : MonoBehaviour, ISpeechRecognizer
{
    public bool IsListening { get; private set; }
    public bool IsInitialized { get; private set; }

    public event System.Action OnReady;
    public event System.Action<string[]> OnResults;
    public event System.Action<string> OnPartialResult;
    public event System.Action<string> OnError;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject speechRecognizer;
    private AndroidJavaObject activity;
    private string languageCode;

    // Thread-safe action queue for dispatching Android callbacks to Unity main thread
    private readonly Queue<System.Action> mainThreadQueue = new Queue<System.Action>();

    public void Initialize(string langCode)
    {
        languageCode = langCode;

        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

        // Microphone permission disabled for store compliance

        activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
        {
            using (var srClass = new AndroidJavaClass("android.speech.SpeechRecognizer"))
            {
                bool available = srClass.CallStatic<bool>("isRecognitionAvailable", activity);
                if (!available)
                {
                    Enqueue(() => OnError?.Invoke("Speech recognition not available on this device"));
                    return;
                }

                speechRecognizer = srClass.CallStatic<AndroidJavaObject>("createSpeechRecognizer", activity);
                speechRecognizer.Call("setRecognitionListener", new SpeechListener(this));
                IsInitialized = true;
                Debug.Log("[AndroidSpeechRecognizer] Initialized successfully");
            }
        }));
    }

    public void StartListening()
    {
        if (!IsInitialized || IsListening) return;
        IsListening = true;

        activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
        {
            if (speechRecognizer == null) return;

            using (var intent = new AndroidJavaObject("android.content.Intent",
                "android.speech.action.RECOGNIZE_SPEECH"))
            {
                intent.Call<AndroidJavaObject>("putExtra",
                    "android.speech.extra.LANGUAGE_MODEL", "free_form");
                intent.Call<AndroidJavaObject>("putExtra",
                    "android.speech.extra.LANGUAGE", languageCode);
                intent.Call<AndroidJavaObject>("putExtra",
                    "android.speech.extra.PARTIAL_RESULTS", true);
                intent.Call<AndroidJavaObject>("putExtra",
                    "android.speech.extra.MAX_RESULTS", 5);

                speechRecognizer.Call("startListening", intent);
            }
        }));
    }

    public void StopListening()
    {
        IsListening = false;
        if (!IsInitialized) return;

        activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
        {
            if (speechRecognizer != null)
                speechRecognizer.Call("stopListening");
        }));
    }

    public void Destroy()
    {
        IsListening = false;
        IsInitialized = false;

        if (activity != null)
        {
            activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                if (speechRecognizer != null)
                {
                    speechRecognizer.Call("cancel");
                    speechRecognizer.Call("destroy");
                    speechRecognizer = null;
                }
            }));
        }
    }

    private void OnDestroy()
    {
        Destroy();
    }

    private void Update()
    {
        // Process queued main-thread actions
        lock (mainThreadQueue)
        {
            while (mainThreadQueue.Count > 0)
                mainThreadQueue.Dequeue()?.Invoke();
        }
    }

    private void Enqueue(System.Action action)
    {
        lock (mainThreadQueue)
            mainThreadQueue.Enqueue(action);
    }

    /// <summary>
    /// AndroidJavaProxy implementing android.speech.RecognitionListener.
    /// Method names must exactly match the Java interface.
    /// </summary>
    private class SpeechListener : AndroidJavaProxy
    {
        private readonly AndroidSpeechRecognizer owner;

        public SpeechListener(AndroidSpeechRecognizer owner)
            : base("android.speech.RecognitionListener")
        {
            this.owner = owner;
        }

        // Called when the recognizer is ready
        void onReadyForSpeech(AndroidJavaObject bundle)
        {
            owner.Enqueue(() => owner.OnReady?.Invoke());
        }

        // Called with final recognition results
        void onResults(AndroidJavaObject bundle)
        {
            var matches = bundle.Call<AndroidJavaObject>("getStringArrayList",
                "results_recognition");

            string[] results = null;
            if (matches != null)
            {
                int count = matches.Call<int>("size");
                results = new string[count];
                for (int i = 0; i < count; i++)
                    results[i] = matches.Call<string>("get", i);
            }

            owner.Enqueue(() =>
            {
                owner.IsListening = false;
                owner.OnResults?.Invoke(results ?? new string[0]);
            });
        }

        // Called with partial results while user is still speaking
        void onPartialResults(AndroidJavaObject bundle)
        {
            var matches = bundle.Call<AndroidJavaObject>("getStringArrayList",
                "results_recognition");

            if (matches != null && matches.Call<int>("size") > 0)
            {
                string partial = matches.Call<string>("get", 0);
                owner.Enqueue(() => owner.OnPartialResult?.Invoke(partial));
            }
        }

        // Called on error
        void onError(int error)
        {
            string desc;
            switch (error)
            {
                case 1: desc = "network_timeout"; break;
                case 2: desc = "network"; break;
                case 3: desc = "audio"; break;
                case 4: desc = "server"; break;
                case 5: desc = "client"; break;
                case 6: desc = "speech_timeout"; break;
                case 7: desc = "no_match"; break;
                case 8: desc = "busy"; break;
                case 9: desc = "insufficient_permissions"; break;
                default: desc = $"unknown_{error}"; break;
            }

            owner.Enqueue(() =>
            {
                owner.IsListening = false;
                owner.OnError?.Invoke(desc);
            });
        }

        // Unused callbacks — must be present to satisfy the interface
        void onBeginningOfSpeech() { }
        void onEndOfSpeech() { }
        void onBufferReceived(AndroidJavaObject buffer) { }
        void onRmsChanged(float rmsdB) { }
        void onEvent(int eventType, AndroidJavaObject bundle) { }
    }

#else
    // Stub for non-Android platforms (should not be used — use MockSpeechRecognizer instead)
    public void Initialize(string languageCode) => Debug.LogWarning("AndroidSpeechRecognizer only works on Android");
    public void StartListening() { }
    public void StopListening() { }
    public void Destroy() { }
#endif
}
