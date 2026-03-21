using UnityEngine;

/// <summary>
/// Central audio clip loader for voice-over and feedback sounds.
/// Clips are loaded from the Assets/Sounds folder via Resources.
/// Call SoundLibrary.Play*() methods from anywhere — they use BackgroundMusicManager.PlayOneShot.
/// </summary>
public static class SoundLibrary
{
    // ── Animal Names ──

    public static AudioClip AnimalName(string animalId)
    {
        return Resources.Load<AudioClip>($"Sounds/Animals Names/{animalId}");
    }

    public static void PlayAnimalName(string animalId)
    {
        var clip = AnimalName(animalId);
        if (clip != null) BackgroundMusicManager.PlayOneShot(clip);
    }

    // ── Color Names ──

    public static AudioClip ColorName(string colorId)
    {
        return Resources.Load<AudioClip>($"Sounds/Colors/{colorId}");
    }

    public static void PlayColorName(string colorId)
    {
        var clip = ColorName(colorId);
        if (clip != null) BackgroundMusicManager.PlayOneShot(clip);
    }

    // ── Number Names ──

    public static AudioClip NumberName(int number)
    {
        return Resources.Load<AudioClip>($"Sounds/Numbers/{number}");
    }

    public static void PlayNumberName(int number)
    {
        var clip = NumberName(number);
        if (clip != null) BackgroundMusicManager.PlayOneShot(clip);
    }

    // ── Feedback (random win clip) ──

    private static readonly string[] FeedbackClips =
    {
        "Sounds/Feedbacks/Great",
        "Sounds/Feedbacks/Your champions",
        "Sounds/Feedbacks/So much fun to play together",
        "Sounds/Feedbacks/You succeed",
    };

    public static void PlayRandomFeedback()
    {
        if (Random.value > 0.3f) return; // 30% chance to play
        string path = FeedbackClips[Random.Range(0, FeedbackClips.Length)];
        var clip = Resources.Load<AudioClip>(path);
        if (clip != null) BackgroundMusicManager.PlayOneShot(clip);
    }

    // ── Specific Feedbacks ──

    public static void PlayGreatPainting()
    {
        var clip = Resources.Load<AudioClip>("Sounds/Feedbacks/What a great painting");
        if (clip != null) BackgroundMusicManager.PlayOneShot(clip);
    }

    // ── World ──

    public static AudioClip WorldIntro()
    {
        return Resources.Load<AudioClip>("Sounds/World/This is your world");
    }

    public static AudioClip WorldAnimalsAndColors()
    {
        return Resources.Load<AudioClip>("Sounds/World/Here all your discovered animals and colors");
    }

    public static AudioClip WorldSavedPaintings()
    {
        return Resources.Load<AudioClip>("Sounds/World/Here all your saved painting");
    }

    public static AudioClip WorldAllGames()
    {
        return Resources.Load<AudioClip>("Sounds/World/Here all the games");
    }

    public static AudioClip WorldJourney()
    {
        return Resources.Load<AudioClip>("Sounds/World/Here is your journey");
    }

    public static AudioClip WorldGallery()
    {
        return Resources.Load<AudioClip>("Sounds/World/Here your paintings");
    }

    public static AudioClip WorldOpenFirstGift()
    {
        return Resources.Load<AudioClip>("Sounds/World/Lets open your first gift");
    }

    public static AudioClip WorldAnotherGift()
    {
        return Resources.Load<AudioClip>("Sounds/World/You have another gift");
    }
}
