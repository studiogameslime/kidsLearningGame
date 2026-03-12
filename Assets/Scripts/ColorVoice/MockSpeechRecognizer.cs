using UnityEngine;

/// <summary>
/// Mock speech recognizer for Unity Editor testing.
/// Shows on-screen buttons for each color that simulate speech recognition.
/// </summary>
public class MockSpeechRecognizer : MonoBehaviour, ISpeechRecognizer
{
    public bool IsListening { get; private set; }

    public event System.Action OnReady;
    public event System.Action<string[]> OnResults;
    public event System.Action<string> OnPartialResult;
    public event System.Action<string> OnError;

    private bool isInitialized;

    public void Initialize(string languageCode)
    {
        isInitialized = true;
        Debug.Log($"[MockSpeechRecognizer] Initialized with language: {languageCode}");
    }

    public void StartListening()
    {
        if (!isInitialized) return;
        IsListening = true;
        Debug.Log("[MockSpeechRecognizer] Listening started — use on-screen buttons or number keys 1-7");
        OnReady?.Invoke();
    }

    public void StopListening()
    {
        IsListening = false;
    }

    public void Destroy()
    {
        isInitialized = false;
        IsListening = false;
    }

    /// <summary>
    /// Simulate a recognition result (called by debug UI buttons).
    /// </summary>
    public void SimulateResult(string hebrewWord)
    {
        if (!IsListening) return;
        IsListening = false;
        Debug.Log($"[MockSpeechRecognizer] Simulated: {hebrewWord}");
        OnResults?.Invoke(new[] { hebrewWord });
    }

    /// <summary>
    /// Simulate a wrong/garbage result.
    /// </summary>
    public void SimulateWrong()
    {
        if (!IsListening) return;
        IsListening = false;
        Debug.Log("[MockSpeechRecognizer] Simulated wrong answer");
        OnResults?.Invoke(new[] { "שגוי" }); // garbage word
    }

    private void Update()
    {
        if (!IsListening) return;

        // Keyboard shortcuts: 1-7 for colors, 0 for wrong
        for (int i = 0; i < ColorVoiceData.Colors.Length && i < 7; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                SimulateResult(ColorVoiceData.Colors[i].hebrewName);
                return;
            }
        }
        if (Input.GetKeyDown(KeyCode.Alpha0))
            SimulateWrong();
    }

    private void OnGUI()
    {
        if (!IsListening) return;

        float btnW = 120f;
        float btnH = 50f;
        float startX = 10f;
        float startY = Screen.height - btnH - 10f;

        GUI.skin.button.fontSize = 20;

        for (int i = 0; i < ColorVoiceData.Colors.Length; i++)
        {
            var color = ColorVoiceData.Colors[i];
            var rect = new Rect(startX + i * (btnW + 5f), startY, btnW, btnH);

            // Color the button background
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = color.color;

            if (GUI.Button(rect, $"{i + 1}: {color.hebrewName}"))
                SimulateResult(color.hebrewName);

            GUI.backgroundColor = oldBg;
        }

        // Wrong answer button
        var wrongRect = new Rect(startX + ColorVoiceData.Colors.Length * (btnW + 5f), startY, btnW, btnH);
        GUI.backgroundColor = Color.gray;
        if (GUI.Button(wrongRect, "0: Wrong"))
            SimulateWrong();
        GUI.backgroundColor = Color.white;
    }
}
