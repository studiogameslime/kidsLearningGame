using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-body-type anchor positions for monster part attachment.
/// All positions are in local space relative to the monster root center.
/// The body determines where every part goes — parts just swap sprites.
///
/// To add a new body type:
///   1. Add a new entry to BodyConfigs dictionary with the body sprite name as key
///   2. Define anchor positions by looking at the body shape
///   3. All existing arm/leg/face parts will automatically attach correctly
///
/// To add new arms/legs/face parts:
///   Just add the sprite — no config changes needed. Parts use the body's anchors.
/// </summary>
public static class MonsterBodyConfig
{
    public struct BodyAnchors
    {
        public Vector2 bodySize;        // size of the body Image
        public Vector2 leftArmAnchor;   // screen-left arm position
        public Vector2 rightArmAnchor;  // screen-right arm position
        public Vector2 leftLegAnchor;   // screen-left leg position
        public Vector2 rightLegAnchor;  // screen-right leg position
        public Vector2 eyeLeftAnchor;   // left eye position
        public Vector2 eyeRightAnchor;  // right eye position
        public Vector2 noseAnchor;
        public Vector2 mouthAnchor;
        public Vector2 detailAnchor;    // horns/ears/antenna at top
        public Vector2 armSize;         // arm image size
        public Vector2 legSize;         // leg image size
        public Vector2 eyeSize;
    }

    private static Dictionary<string, BodyAnchors> _configs;

    /// <summary>
    /// Gets the anchor configuration for a body sprite name.
    /// Falls back to body A (square) if not found.
    /// </summary>
    public static BodyAnchors Get(string bodySpriteName)
    {
        EnsureInit();
        // Extract shape letter: "body_whiteA" → "A", "body_blueC" → "C"
        string key = ExtractShapeKey(bodySpriteName);
        if (_configs.TryGetValue(key, out var config))
            return config;
        return _configs["A"]; // fallback
    }

    private static string ExtractShapeKey(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName) || spriteName.Length == 0)
            return "A";
        // Last character is the shape letter (A-F)
        return spriteName.Substring(spriteName.Length - 1).ToUpper();
    }

    private static void EnsureInit()
    {
        if (_configs != null) return;
        _configs = new Dictionary<string, BodyAnchors>();

        // ══════════════════════════════════════════
        //  A: Rounded Square — wide, even proportions
        // ══════════════════════════════════════════
        _configs["A"] = new BodyAnchors
        {
            bodySize       = new Vector2(260, 260),
            leftArmAnchor  = new Vector2(-145, -10),
            rightArmAnchor = new Vector2(145, -10),
            leftLegAnchor  = new Vector2(-55, -160),
            rightLegAnchor = new Vector2(55, -160),
            eyeLeftAnchor  = new Vector2(-42, 35),
            eyeRightAnchor = new Vector2(42, 35),
            noseAnchor     = new Vector2(0, -10),
            mouthAnchor    = new Vector2(0, -50),
            detailAnchor   = new Vector2(0, 150),
            armSize        = new Vector2(70, 110),
            legSize        = new Vector2(50, 90),
            eyeSize        = new Vector2(50, 50),
        };

        // ══════════════════════════════════════════
        //  B: Circle — round, slightly smaller
        // ══════════════════════════════════════════
        _configs["B"] = new BodyAnchors
        {
            bodySize       = new Vector2(260, 260),
            leftArmAnchor  = new Vector2(-140, -20),
            rightArmAnchor = new Vector2(140, -20),
            leftLegAnchor  = new Vector2(-50, -155),
            rightLegAnchor = new Vector2(50, -155),
            eyeLeftAnchor  = new Vector2(-40, 30),
            eyeRightAnchor = new Vector2(40, 30),
            noseAnchor     = new Vector2(0, -15),
            mouthAnchor    = new Vector2(0, -50),
            detailAnchor   = new Vector2(0, 145),
            armSize        = new Vector2(65, 105),
            legSize        = new Vector2(48, 85),
            eyeSize        = new Vector2(48, 48),
        };

        // ══════════════════════════════════════════
        //  C: Egg/Oval — narrow top, wider bottom
        // ══════════════════════════════════════════
        _configs["C"] = new BodyAnchors
        {
            bodySize       = new Vector2(230, 290),
            leftArmAnchor  = new Vector2(-130, 0),
            rightArmAnchor = new Vector2(130, 0),
            leftLegAnchor  = new Vector2(-50, -170),
            rightLegAnchor = new Vector2(50, -170),
            eyeLeftAnchor  = new Vector2(-35, 50),
            eyeRightAnchor = new Vector2(35, 50),
            noseAnchor     = new Vector2(0, 5),
            mouthAnchor    = new Vector2(0, -35),
            detailAnchor   = new Vector2(0, 165),
            armSize        = new Vector2(65, 105),
            legSize        = new Vector2(48, 85),
            eyeSize        = new Vector2(45, 45),
        };

        // ══════════════════════════════════════════
        //  D: Triangle-ish — wide top, narrow bottom
        // ══════════════════════════════════════════
        _configs["D"] = new BodyAnchors
        {
            bodySize       = new Vector2(270, 260),
            leftArmAnchor  = new Vector2(-148, 10),
            rightArmAnchor = new Vector2(148, 10),
            leftLegAnchor  = new Vector2(-40, -150),
            rightLegAnchor = new Vector2(40, -150),
            eyeLeftAnchor  = new Vector2(-45, 40),
            eyeRightAnchor = new Vector2(45, 40),
            noseAnchor     = new Vector2(0, -5),
            mouthAnchor    = new Vector2(0, -40),
            detailAnchor   = new Vector2(0, 150),
            armSize        = new Vector2(70, 110),
            legSize        = new Vector2(45, 80),
            eyeSize        = new Vector2(50, 50),
        };

        // ══════════════════════════════════════════
        //  E: Tall Capsule — narrow and tall
        // ══════════════════════════════════════════
        _configs["E"] = new BodyAnchors
        {
            bodySize       = new Vector2(180, 330),
            leftArmAnchor  = new Vector2(-108, 20),
            rightArmAnchor = new Vector2(108, 20),
            leftLegAnchor  = new Vector2(-40, -195),
            rightLegAnchor = new Vector2(40, -195),
            eyeLeftAnchor  = new Vector2(-30, 70),
            eyeRightAnchor = new Vector2(30, 70),
            noseAnchor     = new Vector2(0, 20),
            mouthAnchor    = new Vector2(0, -20),
            detailAnchor   = new Vector2(0, 185),
            armSize        = new Vector2(60, 100),
            legSize        = new Vector2(45, 85),
            eyeSize        = new Vector2(42, 42),
        };

        // ══════════════════════════════════════════
        //  F: Fuzzy Blob — wide with built-in arm nubs
        // ══════════════════════════════════════════
        _configs["F"] = new BodyAnchors
        {
            bodySize       = new Vector2(240, 310),
            leftArmAnchor  = new Vector2(-138, -30),
            rightArmAnchor = new Vector2(138, -30),
            leftLegAnchor  = new Vector2(-50, -175),
            rightLegAnchor = new Vector2(50, -175),
            eyeLeftAnchor  = new Vector2(-38, 45),
            eyeRightAnchor = new Vector2(38, 45),
            noseAnchor     = new Vector2(0, 0),
            mouthAnchor    = new Vector2(0, -40),
            detailAnchor   = new Vector2(0, 175),
            armSize        = new Vector2(65, 105),
            legSize        = new Vector2(48, 85),
            eyeSize        = new Vector2(46, 46),
        };
    }
}
