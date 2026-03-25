using System;
using UnityEngine;
#if UNITY_ANDROID
using Unity.Notifications.Android;
#endif
#if UNITY_IOS
using Unity.Notifications.iOS;
#endif

/// <summary>
/// Lightweight wrapper for local push notifications (Android + iOS).
/// Used by StickerTreeController to remind the child when a sticker is ready.
/// </summary>
public class NotificationService : MonoBehaviour
{
    public static NotificationService Instance { get; private set; }

    private const string AndroidChannelId = "sticker_tree";
    private const string StickerNotifTag = "sticker_ready";

    // Saved notification ID so we can cancel it later
    private const string PrefKey = "sticker_notif_id";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        var go = new GameObject("NotificationService");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<NotificationService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Initialize();
    }

    private void Initialize()
    {
#if UNITY_ANDROID
        var channel = new AndroidNotificationChannel
        {
            Id = AndroidChannelId,
            Name = "Sticker Tree",
            Description = "Notification when a sticker is ready to collect",
            Importance = Importance.High
        };
        AndroidNotificationCenter.RegisterNotificationChannel(channel);
#endif
#if UNITY_IOS
        StartCoroutine(RequestIOSPermission());
#endif
    }

#if UNITY_IOS
    private System.Collections.IEnumerator RequestIOSPermission()
    {
        var req = new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound, true);
        while (!req.IsFinished) yield return null;
    }
#endif

    /// <summary>
    /// Schedule a local notification for when the sticker is ready.
    /// </summary>
    public void ScheduleStickerReady(DateTime fireTimeUtc)
    {
        CancelStickerNotification();

        // Respect global notification toggle from parent settings
        if (!AppSettings.NotificationsEnabled) return;

        var delay = fireTimeUtc - DateTime.UtcNow;
        if (delay.TotalSeconds <= 0) return; // already ready

#if UNITY_ANDROID
        var notification = new AndroidNotification
        {
            Title = "\u0021\u200F" + "המדבקה מוכנה",
            Text = "בואו לאסוף את המדבקה מעץ המדבקות!",
            FireTime = fireTimeUtc.ToLocalTime(),
            SmallIcon = "icon_small",
            LargeIcon = "icon_large"
        };
        int id = AndroidNotificationCenter.SendNotification(notification, AndroidChannelId);
        PlayerPrefs.SetInt(PrefKey, id);
        PlayerPrefs.Save();
#elif UNITY_IOS
        var timeTrigger = new iOSNotificationTimeIntervalTrigger
        {
            TimeInterval = delay,
            Repeats = false
        };
        var notification = new iOSNotification
        {
            Identifier = StickerNotifTag,
            Title = "!המדבקה מוכנה",
            Body = "בואו לאסוף את המדבקה מעץ המדבקות!",
            ShowInForeground = false,
            Trigger = timeTrigger
        };
        iOSNotificationCenter.ScheduleNotification(notification);
#endif
    }

    /// <summary>
    /// Cancel any pending sticker notification.
    /// </summary>
    public void CancelStickerNotification()
    {
#if UNITY_ANDROID
        if (PlayerPrefs.HasKey(PrefKey))
        {
            AndroidNotificationCenter.CancelNotification(PlayerPrefs.GetInt(PrefKey));
            PlayerPrefs.DeleteKey(PrefKey);
        }
#elif UNITY_IOS
        iOSNotificationCenter.RemoveScheduledNotification(StickerNotifTag);
        iOSNotificationCenter.RemoveDeliveredNotification(StickerNotifTag);
#endif
    }

    /// <summary>
    /// Returns true if the platform supports local notifications.
    /// </summary>
    public static bool IsSupported
    {
        get
        {
#if UNITY_ANDROID || UNITY_IOS
            return true;
#else
            return false;
#endif
        }
    }
}
