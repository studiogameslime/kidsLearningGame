using UnityEngine;

/// <summary>
/// Global app settings stored in PlayerPrefs.
/// These are device-level (not per-profile) toggles controlled from the parent dashboard.
/// </summary>
public static class AppSettings
{
    private const string KeyMusic = "app_music_enabled";
    private const string KeyVoice = "app_voice_enabled";
    private const string KeyNotifications = "app_notifications_enabled";

    /// <summary>Background music on/off.</summary>
    public static bool MusicEnabled
    {
        get => PlayerPrefs.GetInt(KeyMusic, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(KeyMusic, value ? 1 : 0);
            PlayerPrefs.Save();
            BackgroundMusicManager.SetMuted(!value);
        }
    }

    /// <summary>Voice/SFX (Alin voice, animal names, feedbacks) on/off.</summary>
    public static bool VoiceEnabled
    {
        get => PlayerPrefs.GetInt(KeyVoice, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(KeyVoice, value ? 1 : 0);
            PlayerPrefs.Save();
            BackgroundMusicManager.SetSfxMuted(!value);
        }
    }

    /// <summary>Push notifications on/off. Overrides per-tree bell setting.</summary>
    public static bool NotificationsEnabled
    {
        get => PlayerPrefs.GetInt(KeyNotifications, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(KeyNotifications, value ? 1 : 0);
            PlayerPrefs.Save();
            if (!value)
            {
                NotificationService.Instance?.CancelStickerNotification();
            }
            else
            {
                // Re-schedule sticker notification if a timer is running
                RescheduleIfTimerRunning();
            }
        }
    }

    /// <summary>
    /// When notifications are re-enabled, check if a sticker timer is active
    /// and re-schedule the notification for the remaining time.
    /// </summary>
    private static void RescheduleIfTimerRunning()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        // Read the last collection time (same key pattern as StickerTreeController)
        string key = $"stree_{profile.id}_lastCollect";
        string val = PlayerPrefs.GetString(key, "");
        if (!long.TryParse(val, out long lastCollect) || lastCollect == 0) return;

        // Default grow duration = 6 hours (matches StickerTreeController)
        const float growDuration = 6f * 3600f;
        long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long fireTime = lastCollect + (long)growDuration;

        if (fireTime > now)
        {
            var fireUtc = System.DateTimeOffset.FromUnixTimeSeconds(fireTime).UtcDateTime;
            NotificationService.Instance?.ScheduleStickerReady(fireUtc);
        }
    }
}
