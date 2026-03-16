using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// View component for a profile card in the profile selection screen.
/// Shows a circular colored avatar with the child's initial and name below.
/// </summary>
public class ProfileCardView : MonoBehaviour
{
    [Header("References")]
    public Image avatarCircle;
    public Image avatarImage; // for custom photo, hidden by default
    public TextMeshProUGUI initialText;
    public TextMeshProUGUI nameText;
    public Button button;

    public void Setup(UserProfile profile, UnityAction onTap)
    {
        // Avatar color
        if (avatarCircle != null)
            avatarCircle.color = profile.AvatarColor;

        // Initial letter
        if (initialText != null)
            initialText.text = profile.Initial;

        // Custom avatar image — fills the entire circle, hides color background
        if (avatarImage != null)
        {
            if (!string.IsNullOrEmpty(profile.avatarImagePath))
            {
                string fullPath = System.IO.Path.Combine(Application.persistentDataPath, profile.avatarImagePath);
                if (System.IO.File.Exists(fullPath))
                {
                    var bytes = System.IO.File.ReadAllBytes(fullPath);
                    var tex = new Texture2D(2, 2);
                    if (tex.LoadImage(bytes))
                    {
                        avatarImage.sprite = Sprite.Create(tex,
                            new Rect(0, 0, tex.width, tex.height),
                            new Vector2(0.5f, 0.5f));
                        avatarImage.gameObject.SetActive(true);
                        // Hide color and initial — photo covers the whole circle
                        if (avatarCircle != null)
                            avatarCircle.color = Color.white;
                        if (initialText != null)
                            initialText.gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                avatarImage.gameObject.SetActive(false);
            }
        }

        // Name (enable RTL for Hebrew)
        if (nameText != null)
        {
            nameText.text = ProfileCreationController.IsHebrew(profile.displayName)
                ? HebrewFixer.Fix(profile.displayName)
                : profile.displayName;
            nameText.isRightToLeftText = true;
        }

        // Tap handler
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onTap);
        }
    }
}
