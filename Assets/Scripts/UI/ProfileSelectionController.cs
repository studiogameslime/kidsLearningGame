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

        // Add info button FIRST (so overlay renders on top of it)
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            CreateInfoButton(canvas.transform);

        // Show first-launch onboarding overlay (once ever) — on top of everything
        if (canvas != null)
            FirstLaunchOverlay.TryShow(canvas.transform);

        // Play "Who playing?" sound
        if (whoPlayingSound != null && audioSource != null)
        {
            audioSource.clip = whoPlayingSound;
            audioSource.Play();
        }
    }

    private void CreateInfoButton(Transform canvasRoot)
    {
        var go = new GameObject("InfoButton");
        go.transform.SetParent(canvasRoot, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.sizeDelta = new Vector2(70, 70);
        rt.anchoredPosition = new Vector2(-16, -16);

        var img = go.AddComponent<Image>();
        var infoSprite = Resources.Load<Sprite>("UI/info");
        if (infoSprite != null)
            img.sprite = infoSprite;
        else
        {
            // Fallback: circle with "?" text
            img.color = new Color(0.35f, 0.55f, 0.85f, 0.9f);
            var textGO = new GameObject("QuestionMark");
            textGO.transform.SetParent(go.transform, false);
            var trt = textGO.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = "?";
            tmp.fontSize = 40;
            tmp.fontStyle = TMPro.FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
        }

        img.raycastTarget = true;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => FirstLaunchOverlay.Show(canvasRoot));
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

        // Move add card to the end, hide if max profiles reached
        if (addProfileCard != null)
        {
            addProfileCard.SetActive(ProfileManager.Instance.CanCreateProfile);
            addProfileCard.transform.SetAsLastSibling();
        }
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
