using UnityEngine;

/// <summary>
/// Firebase Analytics wrapper — TEMPORARILY DISABLED for debugging.
/// All methods are no-ops.
/// </summary>
public class FirebaseAnalyticsManager : MonoBehaviour
{
    public static FirebaseAnalyticsManager Instance { get; private set; }

    public static void UpdateUserProperties() { }
    public static void LogGameStarted(string gameId, int difficulty) { }
    public static void LogGameCompleted(string gameId, int difficulty, float score, int mistakes, float duration) { }
    public static void LogGameExited(string gameId) { }
    public static void LogScreenView(string screenName) { }
    public static void LogProfileCreated(int age, string favoriteAnimal) { }
    public static void LogAquariumFeed() { }
    public static void LogAquariumGiftOpened(string itemId) { }
    public static void LogColorMixed(string colorA, string colorB, string result) { }
    public static void LogDiscovery(string type, string id) { }
    public static void LogParentDashboardOpened() { }
}
