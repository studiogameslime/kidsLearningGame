using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.Linq;

/// <summary>
/// Editor helper to create TutorialHand GameObjects in scene setup scripts.
/// Loads sprite frames from Assets/Art/Tutorial Hand/{animationName}/ folder.
/// </summary>
public static class TutorialHandHelper
{
    /// <summary>
    /// Available animation names matching the folder structure under Assets/Art/Tutorial Hand/.
    /// </summary>
    public static class Anim
    {
        public const string Tap = "Tap";
        public const string SlideRight = "Tap and Slide to right";
        public const string SlideLeft = "Tap and Slide to left";
        public const string SlideUp = "Tap and Slide to top";
        public const string SlideDown = "Tap and Slide to bot";
        public const string DrawCircle = "Draw Circle";
        public const string DrawSquare = "Draw Square";
        public const string DrawTriangle = "Draw Triangle";
        public const string Slice = "Slice";
        public const string Erase = "Erase";
    }

    /// <summary>
    /// Creates a TutorialHand GameObject with animated sprites.
    /// </summary>
    /// <param name="parent">Parent transform (usually SafeArea or Canvas root).</param>
    /// <param name="animationName">Folder name under Assets/Art/Tutorial Hand/.</param>
    /// <param name="anchoredPos">Position relative to anchor.</param>
    /// <param name="size">Size of the hand image (e.g. 150x150).</param>
    /// <param name="tutorialKey">Unique key for PlayerPrefs tracking (usually game ID).</param>
    /// <returns>The created GameObject, or null if sprites not found.</returns>
    public static GameObject Create(Transform parent, string animationName,
        Vector2 anchoredPos, Vector2 size, string tutorialKey)
    {
        string folderPath = $"Assets/Art/Tutorial Hand/{animationName}";

        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });

        var sprites = guids
            .Select(g => AssetDatabase.GUIDToAssetPath(g))
            .Distinct()
            .OrderBy(p => p)
            .Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p))
            .Where(s => s != null)
            .ToArray();

        if (sprites.Length == 0)
        {
            Debug.LogWarning($"TutorialHandHelper: No sprites found in {folderPath}");
            return null;
        }

        var go = new GameObject("TutorialHand");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.sprite = sprites[0];
        img.preserveAspect = true;
        img.raycastTarget = false;

        var cg = go.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        var hand = go.AddComponent<TutorialHand>();
        hand.frames = sprites;
        hand.tutorialKey = tutorialKey;

        // Ensure it renders on top
        go.transform.SetAsLastSibling();

        return go;
    }
}
