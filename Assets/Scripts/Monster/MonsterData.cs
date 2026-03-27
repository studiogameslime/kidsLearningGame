using System;

/// <summary>
/// Serializable data for a player-created monster. Stored in UserProfile.
/// Each field is a sprite asset name from Art/Monsters Parts/.
/// </summary>
[Serializable]
public class MonsterData
{
    public string bodySprite;      // e.g. "body_blueA"
    public string eyeSprite;       // e.g. "eye_blue"
    public string noseSprite;      // e.g. "nose_red"
    public string mouthSprite;     // e.g. "mouthA"
    public string leftArmSprite;   // e.g. "arm_blueC"
    public string rightArmSprite;  // e.g. "arm_greenA"
    public string leftLegSprite;   // e.g. "leg_blueA"
    public string rightLegSprite;  // e.g. "leg_redB"
    public string detailSprite;    // e.g. "detail_blue_horn_large" (optional)

    public bool IsComplete =>
        !string.IsNullOrEmpty(bodySprite) &&
        !string.IsNullOrEmpty(eyeSprite) &&
        !string.IsNullOrEmpty(mouthSprite) &&
        !string.IsNullOrEmpty(leftArmSprite) &&
        !string.IsNullOrEmpty(rightArmSprite) &&
        !string.IsNullOrEmpty(leftLegSprite) &&
        !string.IsNullOrEmpty(rightLegSprite);
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
