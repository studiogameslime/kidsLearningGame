using System;

/// <summary>
/// Serializable data for a player-created monster. Stored in UserProfile.
/// Each field is a sprite asset name from Art/Monsters Parts/.
/// </summary>
[Serializable]
public class MonsterData
{
    public string bodySprite;      // e.g. "body_whiteA"
    public string eyeSprite;       // e.g. "eye_blue"
    public string noseSprite;      // e.g. "nose_red"
    public string mouthSprite;     // e.g. "mouthA"
    public string armSprite;       // e.g. "arm_whiteC" (both arms same shape)
    public string legSprite;       // e.g. "leg_whiteA" (both legs same shape)
    public string detailSprite;    // e.g. "detail_blue_horn_large" (optional)

    // Per-part tint colors (hex)
    public string bodyColorHex = "#EF5350";
    public string armColorHex = "#EF5350";
    public string legColorHex = "#EF5350";

    public bool IsComplete =>
        !string.IsNullOrEmpty(bodySprite) &&
        !string.IsNullOrEmpty(eyeSprite) &&
        !string.IsNullOrEmpty(mouthSprite) &&
        !string.IsNullOrEmpty(armSprite) &&
        !string.IsNullOrEmpty(legSprite);
}

/// <summary>
/// Monster progression state stored in JourneyProgress.
/// </summary>
[Serializable]
public class MonsterProgress
{
    public bool eggUnlocked;           // true once threshold reached
    public bool hasSeenHatchAnimation; // true after hatch animation played
    public bool monsterCreated;        // true after monster creation completed
    public MonsterData monsterData;    // the created monster (null until created)
}
