using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Tappable bubble that pops with a particle burst when touched.
/// Attached to large rising bubbles spawned by AquariumAmbience.
/// </summary>
public class PoppableBubble : MonoBehaviour, IPointerClickHandler
{
    public AquariumAmbience ambience;
    public AquariumController controller;

    public void OnPointerClick(PointerEventData eventData)
    {
        var rt = GetComponent<RectTransform>();
        float size = rt.sizeDelta.x;
        Vector2 pos = rt.anchoredPosition;

        // Spawn pop particles via ambience
        if (ambience != null)
            ambience.PopBubbleAt(pos, size);

        SoundLibrary.PlayBubblePop();
        FirebaseAnalyticsManager.LogAquariumBubblePopped();
        if (controller != null) controller.AddProgress(1, 0.05f); // bubble = 1 point, 5% sticker chance
        Destroy(gameObject);
    }
}
