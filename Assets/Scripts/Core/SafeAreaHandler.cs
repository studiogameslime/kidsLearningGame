using UnityEngine;

/// <summary>
/// Adjusts the attached RectTransform to stay within the device safe area.
/// Attach to a full-stretch child of the Canvas. All UI should be placed inside it.
///
/// Landscape-only mode: only insets left/right (where notches/cutouts are in landscape).
/// Top/bottom remain at full screen so headers and backgrounds stretch without gaps.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaHandler : MonoBehaviour
{
    /// <summary>When true, only applies horizontal (left/right) safe area insets.
    /// Keeps top/bottom at full screen so headers/backgrounds extend edge-to-edge.</summary>
    [Tooltip("Only inset left/right for landscape notches. Keep top/bottom full.")]
    public bool landscapeOnly = true;

    private RectTransform rectTransform;
    private Rect lastSafeArea;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    private void Update()
    {
        if (Screen.safeArea != lastSafeArea)
            ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        lastSafeArea = Screen.safeArea;

        // Convert safe area from screen pixels to anchor values (0–1).
        Vector2 anchorMin = lastSafeArea.position;
        Vector2 anchorMax = lastSafeArea.position + lastSafeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        if (landscapeOnly)
        {
            // Only apply horizontal insets (left/right notch areas).
            // Top/bottom stay at 0/1 so backgrounds and headers fill the screen.
            anchorMin.y = 0f;
            anchorMax.y = 1f;
        }

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}
