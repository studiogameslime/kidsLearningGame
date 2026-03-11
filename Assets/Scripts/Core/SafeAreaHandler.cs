using UnityEngine;

/// <summary>
/// Adjusts the attached RectTransform to stay within the device safe area.
/// Attach to a full-stretch child of the Canvas. All UI should be placed inside it.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaHandler : MonoBehaviour
{
    private RectTransform rectTransform;
    private Rect lastSafeArea;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    private void Update()
    {
        // Recheck each frame in case orientation changes (shouldn't in portrait-only, but safe).
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

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}
