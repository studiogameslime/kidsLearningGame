using UnityEngine;
using TMPro;

/// <summary>
/// Drop-in Hebrew RTL text component for TextMeshPro.
/// Attach alongside TextMeshProUGUI on scene-baked labels.
///
/// Set text via the Text property (code) or the Inspector field.
/// The component automatically sets isRightToLeftText based on content.
///
/// For code-driven text assignments, prefer HebrewText.SetText(tmp, "...")
/// which works without requiring this component.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
[ExecuteInEditMode]
public class HebrewTMPText : MonoBehaviour
{
    [SerializeField, TextArea(1, 5)]
    [Tooltip("Raw Hebrew text in logical order. RTL is set automatically.")]
    private string _hebrewText;

    private TextMeshProUGUI _tmp;
    private string _lastApplied;

    /// <summary>
    /// Get or set the raw text. Setting triggers RTL configuration.
    /// </summary>
    public string Text
    {
        get => _hebrewText;
        set
        {
            if (_hebrewText == value) return;
            _hebrewText = value;
            Apply();
        }
    }

    private void Awake()
    {
        _tmp = GetComponent<TextMeshProUGUI>();
        Apply();
    }

    private void OnValidate()
    {
        if (_tmp == null) _tmp = GetComponent<TextMeshProUGUI>();
        Apply();
    }

    private void Apply()
    {
        if (_tmp == null) return;
        if (_hebrewText == _lastApplied) return;
        _lastApplied = _hebrewText;
        HebrewText.SetText(_tmp, _hebrewText);
    }
}
