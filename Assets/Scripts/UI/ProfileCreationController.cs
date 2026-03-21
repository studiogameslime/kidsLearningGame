using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Multi-step onboarding flow for creating a new profile.
/// Steps: 1) Greeting → 2) Type name → 3) Choose age → 4) Choose animal → 5) Choose color/photo → 6) Save
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
    public TextMeshProUGUI colorPreviewInitial;
    public TextMeshProUGUI colorPreviewName;
    public Button colorNextButton;

    [Header("Done Step")]
    public TextMeshProUGUI doneNameText;
    public Image doneAvatar;
    public TextMeshProUGUI doneInitial;
    public Button doneButton;

    [Header("Navigation")]
    public Button backButton;

    [Header("Alin Guide")]
    public AlinGuide alinGuide;

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
    private AudioSource audioSource;

    private static readonly string[] AvatarColors = {
        "#EF9A9A", "#F48FB1", "#CE93D8", "#B39DDB",
        "#90CAF9", "#80DEEA", "#80CBC4", "#A5D6A7",
        "#FFF59D", "#FFCC80", "#FFAB91", "#BCAAA4"
    };

    // Color sound name per AvatarColors index. null = hide (duplicate sound).
    private static readonly string[] ColorSoundNames = {
        "Red", "Pink", "Purple", null,          // B39DDB duplicate purple
        "Blue", "Light Blue", null, "Green",    // 80CBC4 duplicate green
        "Yellow", "Orange", null, "Brown"       // FFAB91 duplicate orange
    };

    // Maps new step index → stepSounds array index (recording step removed).
    // Steps: 0=Greeting, 1=TypeName, 2=Age, 3=Animal, 4=Color, 5=Done
    private static readonly int[] StepSoundMap = { 0, 1, 3, 4, 5, -1 };

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
        {
            nameInput.onValueChanged.AddListener(OnNameChanged);

            // Both text and placeholder need RTL=true for correct Hebrew display.
            // TMP reverses the string for rendering — typing still works because
            // each appended character appears at the correct RTL position.
            if (nameInput.textComponent != null)
                nameInput.textComponent.isRightToLeftText = true;
            var placeholder = nameInput.placeholder as TMP_Text;
            if (placeholder != null)
                placeholder.isRightToLeftText = true;
        }

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
            ageNextButton.onClick.AddListener(() => GoToStep(3));

        // Change "8" to "8+" on the last age button
        if (ageButtons != null && ageButtons.Length >= 8)
        {
            var lastAgeText = ageButtons[7].GetComponentInChildren<TextMeshProUGUI>();
            if (lastAgeText != null)
                lastAgeText.text = "+8";
        }

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
            animalNextButton.onClick.AddListener(() => GoToStep(4));

        InitAnimalAnimations();

        // Wire color buttons
        for (int i = 0; i < AvatarColors.Length && colorButtons != null && i < colorButtons.Length; i++)
        {
            int idx = i;
            string hex = AvatarColors[i];
            colorButtons[i].onClick.AddListener(() => OnColorSelected(idx, hex));
        }
        if (colorNextButton != null)
            colorNextButton.onClick.AddListener(() => GoToStep(5));

        // Hide color buttons without unique Alin sounds
        if (colorButtons != null)
        {
            for (int i = 0; i < colorButtons.Length && i < ColorSoundNames.Length; i++)
            {
                if (ColorSoundNames[i] == null)
                    colorButtons[i].gameObject.SetActive(false);
            }
        }

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

        // Steps: 0=Greeting, 1=TypeName, 2=Age, 3=Animal, 4=Color, 5=Done
        if (stepGreeting != null) stepGreeting.SetActive(step == 0);
        if (stepRecordName != null) stepRecordName.SetActive(false); // recording step removed
        if (stepTypeName != null) stepTypeName.SetActive(step == 1);
        if (stepChooseAge != null) stepChooseAge.SetActive(step == 2);
        if (stepChooseAnimal != null)
        {
            stepChooseAnimal.SetActive(step == 3);
            if (step == 3) PlayOnboardingClip("Sounds/Onboarding/WhatIsYourFavoriteAnimal");
        }
        if (stepChooseColor != null)
        {
            stepChooseColor.SetActive(step == 4);
            if (step == 4) UpdateColorPreview();
        }
        if (stepDone != null)
        {
            stepDone.SetActive(step == 5);
            if (step == 5) UpdateDoneStep();
        }

        // Play step sound using mapping (recording step removed, indices shifted)
        int soundIdx = (step >= 0 && step < StepSoundMap.Length) ? StepSoundMap[step] : -1;
        if (soundIdx >= 0 && stepSounds != null && soundIdx < stepSounds.Length && stepSounds[soundIdx] != null)
        {
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = stepSounds[soundIdx];
                audioSource.Play();
            }

            // Alin talks while audio plays, stays visible (idle) after
            if (alinGuide != null)
            {
                if (_alinTalkCoroutine != null) StopCoroutine(_alinTalkCoroutine);
                alinGuide.Show();
                alinGuide.PlayTalking();
                _alinTalkCoroutine = StartCoroutine(StopAlinTalkingAfter(stepSounds[soundIdx].length));
            }
        }
        else if (alinGuide != null)
        {
            // No audio for this step — just show Alin idle
            alinGuide.Show();
            alinGuide.StopTalking();
        }

        if (backButton != null)
            backButton.gameObject.SetActive(step < 5);
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
        GoToStep(2);
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
        SoundLibrary.PlayNumberName(age);

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
            HebrewText.SetText(colorPreviewName, displayName);
        }

        // Update initial
        if (colorPreviewInitial != null && !string.IsNullOrWhiteSpace(recordedName))
            colorPreviewInitial.text = recordedName.Substring(0, 1).ToUpper();
    }

    private void OnColorSelected(int index, string hex)
    {
        selectedColorHex = hex;
        ColorUtility.TryParseHtmlString(hex, out Color c);

        // Update the circle background color
        if (colorPreview != null)
            colorPreview.color = c;

        if (colorPreviewInitial != null)
        {
            colorPreviewInitial.gameObject.SetActive(true);
            if (!string.IsNullOrWhiteSpace(recordedName))
                colorPreviewInitial.text = recordedName.Substring(0, 1).ToUpper();
        }

        // Play Alin color sound
        if (index >= 0 && index < ColorSoundNames.Length && ColorSoundNames[index] != null)
        {
            var clip = Resources.Load<AudioClip>($"Sounds/Colors/{ColorSoundNames[index]}");
            if (clip != null) BackgroundMusicManager.PlayOneShot(clip);
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

    // ── Done ──

    private void UpdateDoneStep()
    {
        if (doneNameText != null)
            HebrewText.SetText(doneNameText, recordedName);

        ColorUtility.TryParseHtmlString(selectedColorHex, out Color c);
        if (doneAvatar != null)
            doneAvatar.color = c;

        if (doneInitial != null)
        {
            doneInitial.gameObject.SetActive(true);
            if (!string.IsNullOrWhiteSpace(recordedName))
                doneInitial.text = recordedName.Substring(0, 1).ToUpper();
        }
    }

    private void OnDonePressed()
    {
        // Create the profile
        var profile = ProfileManager.Instance.CreateProfile(recordedName, selectedAge, selectedColorHex);

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

        // Set as active and go to the World
        ProfileManager.Instance.SetActiveProfile(profile);
        NavigationManager.GoToWorld();
    }

    // ── Alin Guide ──

    private Coroutine _alinTalkCoroutine;

    private IEnumerator StopAlinTalkingAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (alinGuide != null)
            alinGuide.StopTalking(); // stays visible in idle pose
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
