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
/// Used by StickerTreeController to remind the child when a sticker is ready,
/// and for general one-shot notifications (e.g. parent dashboard reminder).
/// </summary>
public class NotificationService : MonoBehaviour
{
    public static NotificationService Instance { get; private set; }

    private const string AndroidChannelId = "sticker_tree";
    private const string AndroidGeneralChannelId = "general";
    private const string StickerNotifTag = "sticker_ready";

    // Saved notification ID so we can cancel it later
    private const string PrefKey = "sticker_notif_id";

    // Whether Android 13+ runtime permission has been requested this session
    private bool _androidPermissionRequested;

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
        // Sticker tree channel
        var stickerChannel = new AndroidNotificationChannel
        {
            Id = AndroidChannelId,
            Name = "Sticker Tree",
            Description = "Notification when a sticker is ready to collect",
            Importance = Importance.High
        };
        AndroidNotificationCenter.RegisterNotificationChannel(stickerChannel);

        // General channel for parent reminders, milestones, etc.
        var generalChannel = new AndroidNotificationChannel
        {
            Id = AndroidGeneralChannelId,
            Name = "General",
            Description = "General app notifications and reminders",
            Importance = Importance.Default
        };
        AndroidNotificationCenter.RegisterNotificationChannel(generalChannel);

        // Request POST_NOTIFICATIONS permission on Android 13+ (API 33+)
        RequestAndroidPermission();
#endif
#if UNITY_IOS
        StartCoroutine(RequestIOSPermission());
#endif
    }

#if UNITY_ANDROID
    private void RequestAndroidPermission()
    {
        if (_androidPermissionRequested) return;
        _androidPermissionRequested = true;

        // PermissionStatus was added in com.unity.mobile.notifications 2.1.0+
        // On Android 12 and below this is a no-op (permission is granted at install).
        var status = AndroidNotificationCenter.UserPermissionToPost;
        if (status == PermissionStatus.NotRequested || status == PermissionStatus.Denied)
        {
            StartCoroutine(RequestAndroidPermissionCoroutine());
        }
    }

    private System.Collections.IEnumerator RequestAndroidPermissionCoroutine()
    {
        var request = new PermissionRequest();
        while (request.Status == PermissionStatus.RequestPending)
            yield return null;
    }
#endif

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
            Title = "המדבקה מוכנה!",
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
            Title = "המדבקה מוכנה!",
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
    /// Schedule a one-shot local notification with custom title/body.
    /// Uses a PlayerPrefs guard key to ensure it fires only once.
    /// </summary>
    /// <param name="tag">Unique tag for this notification (used as iOS identifier and PlayerPrefs guard).</param>
    /// <param name="title">Notification title (Hebrew OK).</param>
    /// <param name="body">Notification body (Hebrew OK).</param>
    /// <param name="delaySeconds">Delay in seconds before firing (default: 5 seconds).</param>
    public void ScheduleOneShot(string tag, string title, string body, double delaySeconds = 5.0)
    {
        if (!AppSettings.NotificationsEnabled) return;

        // Guard: only fire once
        string guardKey = $"notif_sent_{tag}";
        if (PlayerPrefs.GetInt(guardKey, 0) == 1) return;

        if (delaySeconds <= 0) delaySeconds = 1;

#if UNITY_ANDROID
        var notification = new AndroidNotification
        {
            Title = title,
            Text = body,
            FireTime = DateTime.Now.AddSeconds(delaySeconds),
            SmallIcon = "icon_small",
            LargeIcon = "icon_large"
        };
        AndroidNotificationCenter.SendNotification(notification, AndroidGeneralChannelId);
#elif UNITY_IOS
        var timeTrigger = new iOSNotificationTimeIntervalTrigger
        {
            TimeInterval = TimeSpan.FromSeconds(delaySeconds),
            Repeats = false
        };
        var notification = new iOSNotification
        {
            Identifier = tag,
            Title = title,
            Body = body,
            ShowInForeground = false,
            Trigger = timeTrigger
        };
        iOSNotificationCenter.ScheduleNotification(notification);
#endif

        PlayerPrefs.SetInt(guardKey, 1);
        PlayerPrefs.Save();
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
