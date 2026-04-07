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
    public void ScheduleStickerReady(DateTime fireTimeUtc, string childName = "")
    {
        CancelStickerNotification();

        // Respect global notification toggle from parent settings
        if (!AppSettings.NotificationsEnabled) return;

        var delay = fireTimeUtc - DateTime.UtcNow;
        if (delay.TotalSeconds <= 0) return; // already ready

        // Personalized message with child's name
        string title = string.IsNullOrEmpty(childName)
            ? "\u05D4\u05DE\u05D3\u05D1\u05E7\u05D4 \u05DE\u05D5\u05DB\u05E0\u05D4!"  // המדבקה מוכנה!
            : $"{childName}, \u05D4\u05DE\u05D3\u05D1\u05E7\u05D4 \u05E9\u05DC\u05DA \u05DE\u05D5\u05DB\u05E0\u05D4!"; // מתן, המדבקה שלך מוכנה!
        string body = string.IsNullOrEmpty(childName)
            ? "\u05D1\u05D5\u05D0\u05D5 \u05DC\u05D0\u05E1\u05D5\u05E3 \u05D0\u05EA \u05D4\u05DE\u05D3\u05D1\u05E7\u05D4 \u05DE\u05E2\u05E5 \u05D4\u05DE\u05D3\u05D1\u05E7\u05D5\u05EA!" // בואו לאסוף את המדבקה מעץ המדבקות!
            : $"\u05D1\u05D5\u05D0\u05D5 \u05DC\u05E2\u05D6\u05D5\u05E8 \u05DC-{childName} \u05DC\u05D0\u05E1\u05D5\u05E3 \u05D0\u05EA \u05D4\u05DE\u05D3\u05D1\u05E7\u05D4 \u05D4\u05D7\u05D3\u05E9\u05D4!"; // בואו לעזור ל-מתן לאסוף את המדבקה החדשה!

#if UNITY_ANDROID
        var notification = new AndroidNotification
        {
            Title = title,
            Text = body,
            FireTime = fireTimeUtc.ToLocalTime()
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
            Title = title,
            Body = body,
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
            FireTime = DateTime.Now.AddSeconds(delaySeconds)
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
