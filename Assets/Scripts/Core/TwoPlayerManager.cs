using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages 2-player game state: player profiles, scores, turns.
/// Player 1 = LEFT = BLUE, Player 2 = RIGHT = RED (always).
/// </summary>
public static class TwoPlayerManager
{
    public static readonly Color Player1Color = new Color(0.23f, 0.51f, 0.96f); // blue
    public static readonly Color Player2Color = new Color(0.94f, 0.27f, 0.27f); // red

    /// <summary>True when a 2-player session is active.</summary>
    public static bool IsActive { get; private set; }

    /// <summary>Player 1 (host, LEFT, BLUE) — always the current active profile.</summary>
    public static UserProfile Player1 { get; private set; }

    /// <summary>Player 2 (partner, RIGHT, RED).</summary>
    public static UserProfile Player2 { get; private set; }

    public static int Score1 { get; set; }
    public static int Score2 { get; set; }

    /// <summary>Whose turn it is (1 or 2). Used for turn-based games.</summary>
    public static int CurrentTurn { get; set; }

    /// <summary>Start a 2-player session.</summary>
    public static void Start(UserProfile host, UserProfile partner)
    {
        IsActive = true;
        Player1 = host;
        Player2 = partner;
        Score1 = 0;
        Score2 = 0;
        CurrentTurn = 1;
        Debug.Log($"[2Player] Started: {Player1.displayName} (BLUE) vs {Player2.displayName} (RED)");
    }

    /// <summary>End the 2-player session.</summary>
    public static void End()
    {
        if (!IsActive) return;
        Debug.Log($"[2Player] Ended: {Score1}-{Score2}");
        IsActive = false;
        Player1 = null;
        Player2 = null;
    }

    /// <summary>Switch turn (for turn-based games).</summary>
    public static void SwitchTurn()
    {
        CurrentTurn = CurrentTurn == 1 ? 2 : 1;
    }

    /// <summary>Get the player for a screen-side touch (left half = P1, right half = P2).</summary>
    public static int GetPlayerForScreenPosition(float normalizedX)
    {
        return normalizedX < 0.5f ? 1 : 2;
    }

    /// <summary>Get color for player number (1 or 2).</summary>
    public static Color GetColor(int player)
    {
        return player == 1 ? Player1Color : Player2Color;
    }

    /// <summary>Get name for player number.</summary>
    public static string GetName(int player)
    {
        if (player == 1) return Player1?.displayName ?? "1";
        return Player2?.displayName ?? "2";
    }

    // ── Supported games ──

    private static readonly HashSet<string> TwoPlayerGames = new HashSet<string>
    {
        "memory",
        "sharedsticker"
    };

    /// <summary>Check if a game supports 2-player mode.</summary>
    public static bool SupportsMultiplayer(string gameId)
    {
        return TwoPlayerGames.Contains(gameId);
    }
}
