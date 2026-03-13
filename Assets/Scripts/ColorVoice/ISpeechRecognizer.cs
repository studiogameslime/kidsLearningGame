/// <summary>
/// Abstraction for speech recognition. Platform-specific implementations
/// (Android, iOS, Editor mock) implement this interface.
/// </summary>
public interface ISpeechRecognizer
{
    /// <summary>Initialize with a BCP-47 language code (e.g. "he-IL").</summary>
    void Initialize(string languageCode);

    /// <summary>Start listening for speech.</summary>
    void StartListening();

    /// <summary>Stop listening.</summary>
    void StopListening();

    /// <summary>Release all native resources.</summary>
    void Destroy();

    /// <summary>True if currently listening.</summary>
    bool IsListening { get; }

    /// <summary>True after Initialize() has completed.</summary>
    bool IsInitialized { get; }

    /// <summary>Fired when the recognizer is ready to accept speech.</summary>
    event System.Action OnReady;

    /// <summary>Fired with an array of possible transcriptions (best first).</summary>
    event System.Action<string[]> OnResults;

    /// <summary>Fired with a partial transcription while user is still speaking.</summary>
    event System.Action<string> OnPartialResult;

    /// <summary>Fired on recognition error with a description string.</summary>
    event System.Action<string> OnError;
}
