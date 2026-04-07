using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Netflix-style profile selection screen. Shows existing profiles as circular avatars
/// with an "Add Profile" card. Tapping a profile sets it as active and goes to MainMenu.
/// </summary>
public class ProfileSelectionController : MonoBehaviour
{
    [Header("UI References")]
    public Transform profileContainer;
    public GameObject profileCardPrefab;
    public GameObject addProfileCard;
    public TextMeshProUGUI titleText;

    [Header("Settings")]
    public float cardAnimDelay = 0.1f;

    [Header("Audio")]
    public AudioClip whoPlayingSound;

    private AudioSource audioSource;
    private bool isSelecting;

    private void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        PopulateProfiles();

        // Show first-launch onboarding overlay (once ever)
        // The overlay sits on top of everything and dismisses itself.
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            FirstLaunchOverlay.TryShow(canvas.transform);

        // Play "Who playing?" sound
        if (whoPlayingSound != null && audioSource != null)
        {
            audioSource.clip = whoPlayingSound;
            audioSource.Play();
        }
    }

    public void PopulateProfiles()
    {
        // Clear existing cards (except addProfileCard which is part of the scene)
        for (int i = profileContainer.childCount - 1; i >= 0; i--)
        {
            var child = profileContainer.GetChild(i).gameObject;
            if (child != addProfileCard)
                Destroy(child);
        }

        var profiles = ProfileManager.Instance.Profiles;

        foreach (var profile in profiles)
        {
            var cardGO = Instantiate(profileCardPrefab, profileContainer);
            var card = cardGO.GetComponent<ProfileCardView>();
            if (card != null)
            {
                card.Setup(profile, () => OnProfileSelected(profile));
            }
        }

        // Move add card to the end
        if (addProfileCard != null)
            addProfileCard.transform.SetAsLastSibling();
    }

    private void OnProfileSelected(UserProfile profile)
    {
        if (isSelecting) return;
        isSelecting = true;

        // Stop any playing sound (e.g. "Who playing?")
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();

        ProfileManager.Instance.SetActiveProfile(profile);

        // Play name audio if it exists
        if (!string.IsNullOrEmpty(profile.nameAudioPath))
        {
            string fullPath = System.IO.Path.Combine(Application.persistentDataPath, profile.nameAudioPath);
            if (System.IO.File.Exists(fullPath))
            {
                StartCoroutine(PlayNameAndNavigate(fullPath));
                return;
            }
        }

        NavigationManager.GoToWorld();
    }

    private IEnumerator PlayNameAndNavigate(string audioPath)
    {
        // Load and play the recorded name
        string url = "file:///" + audioPath.Replace("\\", "/");
        using (var www = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
        {
            yield return www.SendWebRequest();
            if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                var clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(www);
                BackgroundMusicManager.PlayOneShot(clip);
                yield return new WaitForSeconds(Mathf.Min(clip.length + 0.3f, 2f));
            }
        }

        NavigationManager.GoToWorld();
    }

    public void OnAddProfilePressed()
    {
        NavigationManager.GoToProfileCreation();
    }

    /// <summary>
    /// Delete a profile and refresh the list.
    /// Called from profile card's delete button.
    /// </summary>
    public void DeleteProfile(string profileId)
    {
        if (string.IsNullOrEmpty(profileId)) return;

        // Don't allow deleting the last profile
        if (ProfileManager.Instance.Profiles.Count <= 1)
        {
            Debug.LogWarning("Cannot delete the last profile.");
            return;
        }

        ProfileManager.Instance.DeleteProfile(profileId);
        PopulateProfiles();
    }

    /// <summary>
    /// Rename a profile. Shows an input dialog inline.
    /// </summary>
    public void RenameProfile(string profileId, string newName)
    {
        if (string.IsNullOrEmpty(profileId) || string.IsNullOrWhiteSpace(newName)) return;

        var profiles = ProfileManager.Instance.Profiles;
        foreach (var p in profiles)
        {
            if (p.id == profileId)
            {
                p.displayName = newName.Trim();
                ProfileManager.Instance.Save();
                PopulateProfiles();
                return;
            }
        }
    }
}
