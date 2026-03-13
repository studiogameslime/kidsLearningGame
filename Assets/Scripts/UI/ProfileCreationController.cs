using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Multi-step onboarding flow for creating a new profile.
/// Steps: 1) Greeting → 2) Record name → 3) Type name → 4) Choose age → 5) Choose color/photo → 6) Save
/// </summary>
public class ProfileCreationController : MonoBehaviour
{
    [Header("Step Panels")]
    public GameObject stepGreeting;
    public GameObject stepRecordName;
    public GameObject stepTypeName;
    public GameObject stepChooseAge;
    public GameObject stepChooseAnimal;
    public GameObject stepChooseColor;
    public GameObject stepDone;

    [Header("Greeting Step")]
    public Button greetingNextButton;

    [Header("Record Name Step")]
    public Button recordButton;
    public Button stopRecordButton;
    public Button playRecordButton;
    public Button skipRecordButton;
    public Button recordNextButton;
    public Image recordIndicator;

    [Header("Type Name Step")]
    public TMP_InputField nameInput;
    public Button nameNextButton;

    [Header("Age Step")]
    public Transform ageButtonContainer;
    public Button[] ageButtons; // ages 1-8
    public Button ageNextButton;

    [Header("Animal Step")]
    public Button[] animalButtons; // 3 buttons: Cat, Dog, Bear
    public Image[] animalImages;   // corresponding animal images
    public Sprite[] animalSprites; // fallback static sprites (set by editor)
    public Button animalNextButton;

    [Header("Color Step")]
    public Transform colorButtonContainer;
    public Button[] colorButtons;
    public Image colorPreview;
    public Image colorPreviewPhoto; // shows picked photo inside preview circle
    public TextMeshProUGUI colorPreviewInitial;
    public TextMeshProUGUI colorPreviewName;
    public Button colorNextButton;
    public Button pickPhotoButton;

    [Header("Webcam Preview (Desktop)")]
    public GameObject webcamPanel;
    public RawImage webcamPreview;
    public Button webcamCaptureButton;
    public Button webcamCancelButton;

    [Header("Done Step")]
    public TextMeshProUGUI doneNameText;
    public Image doneAvatar;
    public Image doneAvatarPhoto;
    public TextMeshProUGUI doneInitial;
    public Button doneButton;

    [Header("Navigation")]
    public Button backButton;

    [Header("Onboarding Audio")]
    public AudioClip[] stepSounds; // one per step (0-5)

    // State
    private int currentStep;
    private string recordedName;
    private int selectedAge = 3;
    private string selectedAnimalId = "Cat";
    private UISpriteAnimator[] animalAnimators;
    private string selectedColorHex = "#90CAF9";
    private string recordedAudioPath;
    private AudioClip recordedClip;
    private bool isRecording;
    private Texture2D pickedPhoto;
    private string pickedPhotoPath; // temp path of picked image
    private AudioSource audioSource;
    private WebCamTexture webCamTexture;

    private static readonly string[] AvatarColors = {
        "#EF9A9A", "#F48FB1", "#CE93D8", "#B39DDB",
        "#90CAF9", "#80DEEA", "#80CBC4", "#A5D6A7",
        "#FFF59D", "#FFCC80", "#FFAB91", "#BCAAA4"
    };

    private void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        // Wire greeting
        if (greetingNextButton != null)
            greetingNextButton.onClick.AddListener(() => GoToStep(1));

        // Wire record buttons
        if (recordButton != null)
            recordButton.onClick.AddListener(StartRecording);
        if (stopRecordButton != null)
            stopRecordButton.onClick.AddListener(StopRecording);
        if (playRecordButton != null)
            playRecordButton.onClick.AddListener(PlayRecording);
        if (skipRecordButton != null)
            skipRecordButton.onClick.AddListener(() => GoToStep(2));
        if (recordNextButton != null)
            recordNextButton.onClick.AddListener(() => GoToStep(2));

        // Wire name input
        if (nameNextButton != null)
            nameNextButton.onClick.AddListener(OnNameNext);
        if (nameInput != null)
            nameInput.onValueChanged.AddListener(OnNameChanged);

        // Wire age buttons
        if (ageButtons != null)
        {
            for (int i = 0; i < ageButtons.Length; i++)
            {
                int age = i + 1;
                ageButtons[i].onClick.AddListener(() => OnAgeSelected(age));
            }
        }
        if (ageNextButton != null)
            ageNextButton.onClick.AddListener(() => GoToStep(4));

        // Wire animal buttons
        string[] animalIds = { "Cat", "Dog", "Bear" };
        if (animalButtons != null)
        {
            for (int i = 0; i < animalButtons.Length && i < animalIds.Length; i++)
            {
                int idx = i;
                string id = animalIds[i];
                animalButtons[i].onClick.AddListener(() => OnAnimalSelected(idx, id));
            }
        }
        if (animalNextButton != null)
            animalNextButton.onClick.AddListener(() => GoToStep(5));

        InitAnimalAnimations();

        // Wire color buttons
        for (int i = 0; i < AvatarColors.Length && colorButtons != null && i < colorButtons.Length; i++)
        {
            int idx = i;
            string hex = AvatarColors[i];
            colorButtons[i].onClick.AddListener(() => OnColorSelected(idx, hex));
        }
        if (colorNextButton != null)
            colorNextButton.onClick.AddListener(() => GoToStep(6));

        // Wire pick photo button
        if (pickPhotoButton != null)
            pickPhotoButton.onClick.AddListener(OnPickPhotoPressed);

        // Wire webcam buttons (desktop)
        if (webcamCaptureButton != null)
            webcamCaptureButton.onClick.AddListener(OnWebcamCapture);
        if (webcamCancelButton != null)
            webcamCancelButton.onClick.AddListener(OnWebcamCancel);
        if (webcamPanel != null)
            webcamPanel.SetActive(false);

        // Wire done
        if (doneButton != null)
            doneButton.onClick.AddListener(OnDonePressed);

        // Wire back
        if (backButton != null)
            backButton.onClick.AddListener(OnBackPressed);

        // Start at greeting
        GoToStep(0);
        UpdateRecordUI();
    }

    private void GoToStep(int step)
    {
        currentStep = step;
        if (stepGreeting != null) stepGreeting.SetActive(step == 0);
        if (stepRecordName != null) stepRecordName.SetActive(step == 1);
        if (stepTypeName != null) stepTypeName.SetActive(step == 2);
        if (stepChooseAge != null) stepChooseAge.SetActive(step == 3);
        if (stepChooseAnimal != null)
        {
            stepChooseAnimal.SetActive(step == 4);
            if (step == 4) PlayOnboardingClip("Sounds/Onboarding/WhatIsYourFavoriteAnimal");
        }
        if (stepChooseColor != null)
        {
            stepChooseColor.SetActive(step == 5);
            if (step == 5) UpdateColorPreview();
        }
        if (stepDone != null)
        {
            stepDone.SetActive(step == 6);
            if (step == 6) UpdateDoneStep();
        }

        // Play step sound
        if (stepSounds != null && step >= 0 && step < stepSounds.Length && stepSounds[step] != null)
        {
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = stepSounds[step];
                audioSource.Play();
            }
        }

        // Update back button visibility
        if (backButton != null)
            backButton.gameObject.SetActive(step > 0 && step < 6);
    }

    private void OnBackPressed()
    {
        if (currentStep > 0)
            GoToStep(currentStep - 1);
        else
            NavigationManager.GoToProfileSelection();
    }

    // ── Record Name ──

    private void StartRecording()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("No microphone found!");
            return;
        }

        isRecording = true;
        BackgroundMusicManager.SetMuted(true);
        recordedClip = Microphone.Start(null, false, 5, 44100); // max 5 seconds
        UpdateRecordUI();
        StartCoroutine(AutoStopRecording(5f));
    }

    private IEnumerator AutoStopRecording(float maxDuration)
    {
        yield return new WaitForSeconds(maxDuration);
        if (isRecording)
            StopRecording();
    }

    private void StopRecording()
    {
        if (!isRecording) return;
        isRecording = false;
        BackgroundMusicManager.SetMuted(false);

        int pos = Microphone.GetPosition(null);
        Microphone.End(null);

        if (pos > 0 && recordedClip != null)
        {
            // Trim the clip to actual recorded length
            float[] data = new float[pos * recordedClip.channels];
            recordedClip.GetData(data, 0);
            var trimmed = AudioClip.Create("RecordedName", pos, recordedClip.channels, 44100, false);
            trimmed.SetData(data, 0);
            recordedClip = trimmed;
        }

        UpdateRecordUI();
    }

    private bool isPlayingPreview;

    private void PlayRecording()
    {
        if (recordedClip == null || isPlayingPreview) return;
        StartCoroutine(PlayRecordingCoroutine());
    }

    private IEnumerator PlayRecordingCoroutine()
    {
        isPlayingPreview = true;
        if (playRecordButton != null) playRecordButton.interactable = false;
        BackgroundMusicManager.PlayOneShot(recordedClip);
        yield return new WaitForSeconds(recordedClip.length + 0.1f);
        isPlayingPreview = false;
        if (playRecordButton != null) playRecordButton.interactable = true;
    }

    private void UpdateRecordUI()
    {
        if (recordButton != null) recordButton.gameObject.SetActive(!isRecording);
        if (stopRecordButton != null) stopRecordButton.gameObject.SetActive(isRecording);
        if (playRecordButton != null) playRecordButton.gameObject.SetActive(!isRecording && recordedClip != null);
        if (recordNextButton != null) recordNextButton.gameObject.SetActive(!isRecording && recordedClip != null);
        if (recordIndicator != null) recordIndicator.gameObject.SetActive(isRecording);
    }

    // ── Type Name ──

    private void OnNameChanged(string value)
    {
        if (nameNextButton != null)
            nameNextButton.interactable = !string.IsNullOrWhiteSpace(value);
    }

    private void OnNameNext()
    {
        recordedName = nameInput != null ? nameInput.text.Trim() : "";
        if (string.IsNullOrWhiteSpace(recordedName)) return;
        GoToStep(3);
    }

    public static bool IsHebrew(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (char c in text)
        {
            if (c >= '\u0590' && c <= '\u05FF') return true;
        }
        return false;
    }

    // ── Age ──

    private void OnAgeSelected(int age)
    {
        selectedAge = age;

        // Highlight selected
        if (ageButtons != null)
        {
            for (int i = 0; i < ageButtons.Length; i++)
            {
                var img = ageButtons[i].GetComponent<Image>();
                if (img != null)
                {
                    ColorUtility.TryParseHtmlString(i + 1 == age ? "#90CAF9" : "#E0E0E0", out Color c);
                    img.color = c;
                }
            }
        }

        if (ageNextButton != null) ageNextButton.interactable = true;
    }

    // ── Animal ──

    private void InitAnimalAnimations()
    {
        // UISpriteAnimators are pre-built in the scene by ProfileSceneSetup.
        // Just find the existing components on the animal images.
        animalAnimators = new UISpriteAnimator[3];

        if (animalImages == null) return;

        for (int i = 0; i < animalImages.Length && i < 3; i++)
        {
            if (animalImages[i] == null) continue;
            animalAnimators[i] = animalImages[i].GetComponent<UISpriteAnimator>();
        }
    }

    private void OnAnimalSelected(int index, string animalId)
    {
        selectedAnimalId = animalId;

        // Play animal name sound
        SoundLibrary.PlayAnimalName(animalId);

        // Highlight selected
        if (animalButtons != null)
        {
            for (int i = 0; i < animalButtons.Length; i++)
            {
                var outline = animalButtons[i].GetComponent<Outline>();
                if (outline != null)
                    outline.enabled = (i == index);
            }
        }

        // Play success animation on selected animal
        if (animalAnimators != null && index < animalAnimators.Length && animalAnimators[index] != null)
            animalAnimators[index].PlaySuccess();

        if (animalNextButton != null) animalNextButton.interactable = true;
    }

    // ── Color / Photo ──

    private void UpdateColorPreview()
    {
        // Update name in preview card
        if (colorPreviewName != null)
        {
            string displayName = !string.IsNullOrWhiteSpace(recordedName) ? recordedName : "";
            colorPreviewName.text = IsHebrew(displayName) ? HebrewFixer.Fix(displayName) : displayName;
            colorPreviewName.isRightToLeftText = false;
        }

        // Update initial
        if (colorPreviewInitial != null && !string.IsNullOrWhiteSpace(recordedName))
            colorPreviewInitial.text = recordedName.Substring(0, 1).ToUpper();
    }

    private void OnColorSelected(int index, string hex)
    {
        selectedColorHex = hex;
        ColorUtility.TryParseHtmlString(hex, out Color c);

        // Clear picked photo when choosing a color
        pickedPhoto = null;
        pickedPhotoPath = null;

        if (colorPreview != null)
            colorPreview.color = c;

        if (colorPreviewPhoto != null)
            colorPreviewPhoto.gameObject.SetActive(false);

        if (colorPreviewInitial != null)
        {
            colorPreviewInitial.gameObject.SetActive(true);
            if (!string.IsNullOrWhiteSpace(recordedName))
                colorPreviewInitial.text = recordedName.Substring(0, 1).ToUpper();
        }

        // Highlight selected color button
        if (colorButtons != null)
        {
            for (int i = 0; i < colorButtons.Length; i++)
            {
                var outline = colorButtons[i].GetComponent<Outline>();
                if (outline != null)
                    outline.enabled = (i == index);
            }
        }

        if (colorNextButton != null) colorNextButton.interactable = true;
    }

    private void OnPickPhotoPressed()
    {
#if UNITY_ANDROID || UNITY_IOS
        NativeCamera.TakePicture((path) =>
        {
            if (string.IsNullOrEmpty(path)) return;
            var tex = NativeCamera.LoadImageAtPath(path, 512, false);
            if (tex == null) return;
            ApplyPickedPhoto(tex, path);
        }, 512, preferredCamera: NativeCamera.PreferredCamera.Front);
#else
        OpenWebcam();
#endif
    }

    private void ApplyPickedPhoto(Texture2D tex, string path = null)
    {
        pickedPhoto = tex;
        pickedPhotoPath = path;

        var sprite = Sprite.Create(tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f));

        if (colorPreviewPhoto != null)
        {
            colorPreviewPhoto.sprite = sprite;
            colorPreviewPhoto.gameObject.SetActive(true);
        }
        if (colorPreviewInitial != null)
            colorPreviewInitial.gameObject.SetActive(false);

        if (colorButtons != null)
        {
            for (int i = 0; i < colorButtons.Length; i++)
            {
                var outline = colorButtons[i].GetComponent<Outline>();
                if (outline != null) outline.enabled = false;
            }
        }

        if (colorNextButton != null) colorNextButton.interactable = true;
    }

    // ── Webcam (Desktop) ──

    private void OpenWebcam()
    {
        if (webcamPanel == null || webcamPreview == null) return;

        // Prefer front-facing camera
        string deviceName = null;
        foreach (var device in WebCamTexture.devices)
        {
            if (device.isFrontFacing) { deviceName = device.name; break; }
        }

        webCamTexture = deviceName != null
            ? new WebCamTexture(deviceName, 512, 512, 30)
            : new WebCamTexture(512, 512, 30);

        webcamPreview.texture = webCamTexture;
        webCamTexture.Play();
        webcamPanel.SetActive(true);
    }

    private void OnWebcamCapture()
    {
        if (webCamTexture == null || !webCamTexture.isPlaying) return;

        // Snapshot current frame
        var tex = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGB24, false);
        tex.SetPixels(webCamTexture.GetPixels());
        tex.Apply();

        StopWebcam();
        ApplyPickedPhoto(tex);
    }

    private void OnWebcamCancel()
    {
        StopWebcam();
    }

    private void StopWebcam()
    {
        if (webCamTexture != null)
        {
            webCamTexture.Stop();
            Destroy(webCamTexture);
            webCamTexture = null;
        }
        if (webcamPanel != null)
            webcamPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        StopWebcam();
    }

    // ── Done ──

    private void UpdateDoneStep()
    {
        if (doneNameText != null)
        {
            doneNameText.text = IsHebrew(recordedName) ? HebrewFixer.Fix(recordedName) : recordedName;
            doneNameText.isRightToLeftText = false;
        }

        if (pickedPhoto != null)
        {
            // Show photo in done avatar
            var sprite = Sprite.Create(pickedPhoto,
                new Rect(0, 0, pickedPhoto.width, pickedPhoto.height),
                new Vector2(0.5f, 0.5f));

            if (doneAvatarPhoto != null)
            {
                doneAvatarPhoto.sprite = sprite;
                doneAvatarPhoto.gameObject.SetActive(true);
            }
            if (doneInitial != null)
                doneInitial.gameObject.SetActive(false);
        }
        else
        {
            ColorUtility.TryParseHtmlString(selectedColorHex, out Color c);
            if (doneAvatar != null)
                doneAvatar.color = c;

            if (doneAvatarPhoto != null)
                doneAvatarPhoto.gameObject.SetActive(false);
            if (doneInitial != null)
            {
                doneInitial.gameObject.SetActive(true);
                if (!string.IsNullOrWhiteSpace(recordedName))
                    doneInitial.text = recordedName.Substring(0, 1).ToUpper();
            }
        }
    }

    private void OnDonePressed()
    {
        // Create the profile
        var profile = ProfileManager.Instance.CreateProfile(recordedName, selectedAge, selectedColorHex);

        // Save picked photo if any
        if (pickedPhoto != null)
        {
            string folder = ProfileManager.Instance.GetProfileFolder(profile.id);
            string imagePath = Path.Combine(folder, "avatar.png");
            File.WriteAllBytes(imagePath, pickedPhoto.EncodeToPNG());
            profile.avatarImagePath = Path.Combine("profiles", profile.id, "avatar.png");
        }

        // Save recorded audio if any
        if (recordedClip != null)
        {
            string folder = ProfileManager.Instance.GetProfileFolder(profile.id);
            string audioFile = Path.Combine(folder, "name.wav");
            SaveWav(audioFile, recordedClip);
            profile.nameAudioPath = Path.Combine("profiles", profile.id, "name.wav");
        }

        // Save favorite animal
        profile.favoriteAnimalId = selectedAnimalId;

        ProfileManager.Instance.UpdateProfile(profile);

        // Set as active and go to home
        ProfileManager.Instance.SetActiveProfile(profile);
        NavigationManager.GoToHome();
    }

    // ── Onboarding Audio ──

    private void PlayOnboardingClip(string resourcePath)
    {
        var clip = Resources.Load<AudioClip>(resourcePath);
        if (clip != null)
            BackgroundMusicManager.PlayOneShot(clip);
    }

    // ── WAV Save Helper ──

    private static void SaveWav(string path, AudioClip clip)
    {
        float[] data = new float[clip.samples * clip.channels];
        clip.GetData(data, 0);

        using (var stream = new FileStream(path, FileMode.Create))
        using (var writer = new BinaryWriter(stream))
        {
            int samples = clip.samples;
            int channels = clip.channels;
            int sampleRate = clip.frequency;

            writer.Write(new char[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + samples * channels * 2);
            writer.Write(new char[] { 'W', 'A', 'V', 'E' });
            writer.Write(new char[] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * 2);
            writer.Write((short)(channels * 2));
            writer.Write((short)16); // bits per sample

            writer.Write(new char[] { 'd', 'a', 't', 'a' });
            writer.Write(samples * channels * 2);

            for (int i = 0; i < data.Length; i++)
            {
                short val = (short)(Mathf.Clamp(data[i], -1f, 1f) * short.MaxValue);
                writer.Write(val);
            }
        }
    }
}
