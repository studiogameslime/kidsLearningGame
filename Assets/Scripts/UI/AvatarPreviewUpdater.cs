using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Updates the small circular live preview to match the current crop position.
/// Runs every frame to sync with drag/zoom changes.
/// </summary>
public class AvatarPreviewUpdater : MonoBehaviour
{
    public RectTransform sourceRT;   // the draggable image
    public Image previewImg;         // the preview Image component
    public RectTransform previewRT;  // the preview RectTransform
    public float cropDiameter;
    public int texWidth;
    public int texHeight;

    private void LateUpdate()
    {
        if (sourceRT == null || previewImg == null || previewRT == null) return;

        // Mirror the source image's UV offset into the preview
        // The preview shows what's inside the crop circle
        Vector2 imgSize = sourceRT.sizeDelta;
        Vector2 imgPos = sourceRT.anchoredPosition;

        // The crop circle is at (0,0) relative to workspace center
        // Image offset from circle center:
        float scaleX = imgSize.x > 0 ? cropDiameter / imgSize.x : 1f;
        float scaleY = imgSize.y > 0 ? cropDiameter / imgSize.y : 1f;
        float scale = Mathf.Max(scaleX, scaleY);

        previewRT.localScale = Vector3.one * scale;
        previewRT.anchoredPosition = imgPos * (scale * 100f / cropDiameter);
    }
}
