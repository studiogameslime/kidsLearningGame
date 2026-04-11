using UnityEngine;

/// <summary>
/// DontDestroyOnLoad singleton that holds sticker sprite references.
/// Populated by CollectibleAlbumController.Awake() in WorldScene.
/// Persists across scene changes so StickerSpriteBank works everywhere.
/// </summary>
public class StickerSpriteBankLoader : MonoBehaviour
{
    public static StickerSpriteBankLoader Instance { get; private set; }

    public Sprite[] animalsStickers;
    public Sprite[] lettersStickers;
    public Sprite[] numbersStickers;
    public Sprite[] balloonsStickers;
    public Sprite[] aquariumStickers;
    public Sprite[] carsStickers;
    public Sprite[] foodStickers;
    public Sprite[] artStickers;
    public Sprite[] natureStickers;

    /// <summary>
    /// Called from CollectibleAlbumController.Awake() to populate and persist.
    /// </summary>
    public static void Initialize(
        Sprite[] animals, Sprite[] letters, Sprite[] numbers,
        Sprite[] balloons, Sprite[] aquarium, Sprite[] cars,
        Sprite[] food, Sprite[] art, Sprite[] nature)
    {
        if (Instance != null) return; // already initialized

        var go = new GameObject("StickerSpriteBankLoader");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<StickerSpriteBankLoader>();

        Instance.animalsStickers = animals;
        Instance.lettersStickers = letters;
        Instance.numbersStickers = numbers;
        Instance.balloonsStickers = balloons;
        Instance.aquariumStickers = aquarium;
        Instance.carsStickers = cars;
        Instance.foodStickers = food;
        Instance.artStickers = art;
        Instance.natureStickers = nature;

        // Register all in bank
        StickerSpriteBank.Register("animal_", animals);
        StickerSpriteBank.Register("letter_", letters);
        StickerSpriteBank.Register("number_", numbers);
        StickerSpriteBank.Register("balloon_", balloons);
        StickerSpriteBank.Register("ocean_", aquarium);
        StickerSpriteBank.Register("vehicle_", cars);
        StickerSpriteBank.Register("food_", food);
        StickerSpriteBank.Register("art_", art);
        StickerSpriteBank.Register("nature_", nature);

        Debug.Log("[StickerSpriteBank] Initialized and persisted via DontDestroyOnLoad");
    }
}
