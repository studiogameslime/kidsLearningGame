using UnityEngine;

/// <summary>
/// Adjusts the attached RectTransform to stay within the device safe area.
/// Attach to a full-stretch child of the Canvas. All UI should be placed inside it.
///
/// Applies all safe area insets (left, right, top, bottom) so UI elements
/// avoid notches, camera punch-holes, and rounded corners.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaHandler : MonoBehaviour
{
    private RectTransform rectTransform;
    private Rect lastSafeArea;

    /// <summary>Top safe area inset in pixels, available for other scripts.</summary>
    public static float TopInsetPixels { get; private set; }

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

        // Cache top inset for other scripts (e.g. headers that need padding)
        TopInsetPixels = Screen.height - (lastSafeArea.y + lastSafeArea.height);

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}
