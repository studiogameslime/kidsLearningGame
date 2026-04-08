using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Tappable bubble that pops with a particle burst when touched.
/// Attached to large rising bubbles spawned by AquariumAmbience.
/// </summary>
public class PoppableBubble : MonoBehaviour, IPointerClickHandler
{
    public AquariumAmbience ambience;

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

        // XP for popping bubble
        var profile = ProfileManager.ActiveProfile;
        if (profile != null)
        {
            profile.aquarium.xp += 5;
            int newLevel = profile.aquarium.xp / 100;
            if (newLevel > profile.aquarium.level)
            {
                profile.aquarium.level = newLevel;
                if (ConfettiController.Instance != null) ConfettiController.Instance.Play();
                if (profile.journey != null) { profile.journey.totalStars++; profile.journey.totalGamesCompleted++; }
            }
            ProfileManager.Instance?.Save();
        }
        Destroy(gameObject);
    }
}
