using UnityEngine;
using TMPro;

/// <summary>
/// Auto-fixes Hebrew RTL text for TextMeshPro on all platforms.
///
/// Attach this component to any GameObject that has a TMP_Text (TextMeshProUGUI
/// or TextMeshPro). It intercepts text changes and applies HebrewFixer.Fix()
/// automatically, so you never need to call HebrewFixer manually.
///
/// Also enforces correct TMP settings for Hebrew:
/// - isRightToLeftText = false (we handle RTL ourselves via string reversal)
/// - Disables auto-size if it causes spacing issues
///
/// Usage:
///   1. Add this component to any UI GameObject with TextMeshProUGUI
///   2. Set text normally: GetComponent<HebrewTMPText>().Text = "שלום עולם";
///   3. Or set text on the TMP component — OnEnable monitors for changes
///
/// For static text (set once in editor or setup):
///   Just set text on the TMP_Text as usual. HebrewTMPText will fix it
///   in OnEnable and whenever the text changes at runtime.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class HebrewTMPText : MonoBehaviour
{
    private TMP_Text _tmp;
    private string _lastRawText;
    private string _lastFixedText;

    /// <summary>
    /// Set Hebrew text. Automatically applies RTL fix.
    /// </summary>
    public string Text
    {
        get => _lastRawText ?? "";
        set => SetText(value);
    }

    private void Awake()
    {
        _tmp = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        if (_tmp == null) _tmp = GetComponent<TMP_Text>();
        if (_tmp == null) return;

        // Enforce correct RTL settings for mobile builds
        _tmp.isRightToLeftText = false;

        // Fix current text if it hasn't been fixed yet
        if (!string.IsNullOrEmpty(_tmp.text) && _tmp.text != _lastFixedText)
        {
            _lastRawText = _tmp.text;
            _lastFixedText = HebrewFixer.Fix(_tmp.text);
            if (_lastFixedText != _tmp.text)
                _tmp.text = _lastFixedText;
        }
    }

    private void LateUpdate()
    {
        if (_tmp == null) return;

        // Detect external text changes (e.g. set directly on TMP_Text)
        if (_tmp.text != _lastFixedText)
        {
            string current = _tmp.text;
            // Avoid infinite loop: only fix if it's not already our fixed version
            if (current != _lastFixedText)
            {
                _lastRawText = current;
                _lastFixedText = HebrewFixer.Fix(current);
                if (_lastFixedText != current)
                    _tmp.text = _lastFixedText;
            }
        }
    }

    /// <summary>
    /// Set text with automatic Hebrew RTL fix.
    /// </summary>
    public void SetText(string rawText)
    {
        if (_tmp == null) _tmp = GetComponent<TMP_Text>();
        if (_tmp == null) return;

        _lastRawText = rawText ?? "";
        _lastFixedText = HebrewFixer.Fix(_lastRawText);
        _tmp.text = _lastFixedText;
        _tmp.isRightToLeftText = false;
    }

    /// <summary>
    /// Force re-fix the current text. Call after changing font or style.
    /// </summary>
    public void Refresh()
    {
        if (_tmp == null) return;
        _lastFixedText = HebrewFixer.Fix(_lastRawText ?? _tmp.text);
        _tmp.text = _lastFixedText;
    }
}
