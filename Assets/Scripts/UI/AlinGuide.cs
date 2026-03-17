using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Alin talking guide controller.
/// Manages showing/hiding Alin and triggering talk animation.
///
/// Usage:
///   AlinGuide.Instance.Show();        // show on screen (idle pose)
///   AlinGuide.Instance.PlayTalking(); // start talking animation
///   AlinGuide.Instance.StopTalking(); // return to idle
///   AlinGuide.Instance.Hide();        // hide from screen
///
/// The prefab should be a child of a Canvas. Add via the setup tool:
/// Tools > Kids Learning Game > Setup Alin Guide
/// </summary>
[RequireComponent(typeof(Image), typeof(Animator))]
public class AlinGuide : MonoBehaviour
{
    public static AlinGuide Instance { get; private set; }

    [Tooltip("If true, Alin starts visible (idle). If false, starts hidden until Show() is called.")]
    public bool startVisible;

    private Animator _animator;
    private CanvasGroup _canvasGroup;
    private static readonly int IsTalking = Animator.StringToHash("IsTalking");

    private void Awake()
    {
        Instance = this;
        _animator = GetComponent<Animator>();

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (!startVisible)
            gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Show Alin on screen in idle pose.
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
        _canvasGroup.alpha = 1f;
        _animator.SetBool(IsTalking, false);
    }

    /// <summary>
    /// Start the talking animation loop.
    /// </summary>
    public void PlayTalking()
    {
        if (!gameObject.activeSelf)
            Show();
        _animator.SetBool(IsTalking, true);
    }

    /// <summary>
    /// Stop talking, return to idle pose.
    /// </summary>
    public void StopTalking()
    {
        _animator.SetBool(IsTalking, false);
    }

    /// <summary>
    /// Hide Alin from screen.
    /// </summary>
    public void Hide()
    {
        _animator.SetBool(IsTalking, false);
        gameObject.SetActive(false);
    }
}
