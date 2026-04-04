using UnityEngine;

/// <summary>
/// Keeps interactive content within the device safe area for landscape games.
///
/// The RectTransform itself stays full-screen (edge-to-edge) so that any
/// background Image on this GameObject fills the entire screen seamlessly.
///
/// Child elements are pushed inward by adjusting their anchored positions
/// via left/right offsets on the rect. This avoids notch/camera punch-hole
/// areas while keeping backgrounds looking clean.
///
/// Landscape approach: only left + right insets are applied (where the
/// notch/camera physically is in landscape). Top/bottom stay full — headers
/// extend to the top edge, game content to the bottom edge.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaHandler : MonoBehaviour
{
    private RectTransform rectTransform;
    private Rect lastSafeArea;

    /// <summary>Safe area insets in screen pixels.</summary>
    public static float LeftInsetPx { get; private set; }
    public static float RightInsetPx { get; private set; }

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

        // Convert safe area to anchor values (0–1)
        Vector2 anchorMin = lastSafeArea.position;
        Vector2 anchorMax = lastSafeArea.position + lastSafeArea.size;

        anchorMin.x /= Screen.width;
        anchorMax.x /= Screen.width;

        // Cache pixel values
        LeftInsetPx = lastSafeArea.x;
        RightInsetPx = Screen.width - (lastSafeArea.x + lastSafeArea.width);

        // Only horizontal insets — landscape camera/notch is on left or right side.
        // Top/bottom stay at 0/1 so headers and backgrounds extend to screen edges.
        rectTransform.anchorMin = new Vector2(anchorMin.x, 0f);
        rectTransform.anchorMax = new Vector2(anchorMax.x, 1f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}
