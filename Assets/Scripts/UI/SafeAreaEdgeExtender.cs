using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to a UI element (e.g. TopBar) that lives inside a SafeArea-shrunk parent.
/// Creates an edge-to-edge background child that extends beyond the safe area bounds,
/// while keeping this object's own RectTransform (and thus all buttons/text children)
/// within the safe area.
///
/// The original Image on this GameObject is made transparent; the visual background
/// is carried by the extending child so it fills the full screen width.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaEdgeExtender : MonoBehaviour
{
    private RectTransform _bgRt;
    private Image _bgImg;
    private Image _originalImg;
    private Rect _lastSafeArea;

    private void Awake()
    {
        CreateExtendedBackground();
    }

    private void CreateExtendedBackground()
    {
        _originalImg = GetComponent<Image>();
        Color bgColor = _originalImg != null ? _originalImg.color : Color.clear;
        Sprite bgSprite = _originalImg != null ? _originalImg.sprite : null;
        var bgType = _originalImg != null ? _originalImg.type : Image.Type.Simple;

        // Hide the original image — the extending child carries the visual
        if (_originalImg != null)
            _originalImg.color = Color.clear;

        // Create background child that extends beyond parent bounds
        var bgGO = new GameObject("ExtendedBg");
        bgGO.transform.SetParent(transform, false);
        bgGO.transform.SetAsFirstSibling(); // render behind everything

        _bgRt = bgGO.AddComponent<RectTransform>();
        _bgRt.anchorMin = Vector2.zero;
        _bgRt.anchorMax = Vector2.one;

        _bgImg = bgGO.AddComponent<Image>();
        _bgImg.color = bgColor;
        _bgImg.sprite = bgSprite;
        _bgImg.type = bgType;
        _bgImg.raycastTarget = false;

        UpdateExtents();
    }

    private void Update()
    {
        if (Screen.safeArea != _lastSafeArea)
            UpdateExtents();

        // Sync color if ThemeHeader or other scripts change the original Image color
        if (_originalImg != null && _bgImg != null && _originalImg.color.a > 0.01f)
        {
            _bgImg.color = _originalImg.color;
            _originalImg.color = Color.clear;
        }
    }

    private void UpdateExtents()
    {
        if (_bgRt == null) return;
        _lastSafeArea = Screen.safeArea;

        float leftInset = SafeAreaHandler.LeftInsetPx;
        float rightInset = SafeAreaHandler.RightInsetPx;

        Canvas canvas = GetComponentInParent<Canvas>();
        float scale = (canvas != null && canvas.scaleFactor > 0.01f) ? canvas.scaleFactor : 1f;

        float leftExtend = leftInset / scale;
        float rightExtend = rightInset / scale;

        // Negative offsetMin.x extends left; positive offsetMax.x extends right
        _bgRt.offsetMin = new Vector2(-leftExtend, 0f);
        _bgRt.offsetMax = new Vector2(rightExtend, 0f);
    }

    /// <summary>Set the background color (call from ThemeHeader etc).</summary>
    public void SetColor(Color color)
    {
        if (_bgImg != null) _bgImg.color = color;
    }
}
