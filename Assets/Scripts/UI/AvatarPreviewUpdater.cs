using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mirrors the crop image position/scale into the small circular preview.
/// The preview shows exactly what the final avatar will look like.
/// </summary>
public class AvatarPreviewUpdater : MonoBehaviour
{
    public RectTransform sourceRT;       // the draggable image RT
    public RectTransform previewImageRT; // the image inside the preview circle
    public float cropDiameter;           // crop circle diameter in workspace
    public float previewDiameter;        // preview circle diameter

    private void LateUpdate()
    {
        if (sourceRT == null || previewImageRT == null || cropDiameter <= 0) return;

        // Scale ratio: preview / crop
        float ratio = previewDiameter / cropDiameter;

        // Mirror the source image size and position into preview space
        Vector2 srcSize = sourceRT.sizeDelta;
        Vector2 srcPos = sourceRT.anchoredPosition;

        previewImageRT.sizeDelta = srcSize * ratio;
        previewImageRT.anchoredPosition = srcPos * ratio;
    }
}
