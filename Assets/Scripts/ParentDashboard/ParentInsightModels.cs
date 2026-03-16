using System;

/// <summary>
/// A single parent-facing insight generated from analytics data.
/// All visible text is Hebrew.
/// </summary>
[Serializable]
public class ParentInsight
{
    public string type;
    public string titleHebrew;
    public string descriptionHebrew;
    public string iconId;
    public int priority;
    public string relatedGameId;
    public string relatedCategory;
}

/// <summary>
/// A parent-facing achievement badge. Subtle and parent-toned.
/// </summary>
[Serializable]
public class ParentBadge
{
    public string id;
    public string titleHebrew;
    public string subtitleHebrew;
    public string iconId;
    public string relatedGameId;
}
