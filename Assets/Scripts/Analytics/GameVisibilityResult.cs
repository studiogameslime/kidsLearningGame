/// <summary>
/// Why a game is visible or hidden for a specific child.
/// UI layer maps reason codes to Hebrew display strings.
/// </summary>
public enum VisibilityReasonCode
{
    Visible_Default,
    Visible_WithinAgeRange,      // game is in the child's age bucket baseline
    Visible_ParentForceEnabled,  // parent override: force show
    Hidden_NotInAgeBucket,       // game is not part of the child's age bucket
    Hidden_ParentForceDisabled,  // parent override: force hide
    Hidden_MissingData           // game or profile data is invalid
}

/// <summary>
/// What system layer determined visibility.
/// </summary>
public enum VisibilitySource
{
    Default,
    AgeFilter,
    ParentOverride,
    MissingData
}

/// <summary>
/// Result of evaluating whether a game should be visible to a child.
/// Contains structured reason data — Hebrew display strings are resolved in UI layer only.
/// </summary>
public class GameVisibilityResult
{
    public bool isVisible;
    public VisibilityReasonCode reasonCode;
    public VisibilitySource source;

    public GameVisibilityResult(bool visible, VisibilityReasonCode reason, VisibilitySource src)
    {
        isVisible = visible;
        reasonCode = reason;
        source = src;
    }
}
