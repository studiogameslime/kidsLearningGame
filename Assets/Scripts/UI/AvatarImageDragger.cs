using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Simple drag handler for positioning the avatar image within the crop circle.
/// </summary>
public class AvatarImageDragger : MonoBehaviour, IDragHandler
{
    public Canvas canvas;

    public void OnDrag(PointerEventData eventData)
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null || canvas == null) return;
        rt.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }
}
