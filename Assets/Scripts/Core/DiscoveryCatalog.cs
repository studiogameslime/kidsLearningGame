using System.Collections.Generic;

/// <summary>
/// Defines the fixed order in which animals, colors, and games are discovered.
/// Pattern: 2 animals, 1 color, repeat. 1 game every 5th discovery.
/// Fallback chain when a type runs out: primary → animal → color → game.
/// </summary>
public static class DiscoveryCatalog
{
    // Starter content (seeded on first journey)
    public static readonly string[] StarterColors = { "Red", "Blue", "Yellow" };

    // Discovery order (includes Cat/Dog/Bear since only the favorite is seeded)
    private static readonly string[] AnimalOrder =
    {
        "Dog", "Cat", "Bear",
        "Duck", "Fish", "Frog", "Bird", "Cow", "Horse", "Lion", "Monkey",
        "Elephant", "Giraffe", "Zebra", "Turtle", "Snake", "Sheep", "Chicken", "Donkey"
    };

    private static readonly string[] ColorOrder =
    {
        "Green", "Orange", "Purple", "Pink", "Cyan", "Brown", "Black", "White", "Grey"
    };

    public static bool HasMore(JourneyProgress jp)
    {
        return NextAnimal(jp) != null || NextColor(jp) != null;
    }

    /// <summary>
    /// Try to create a contextual discovery based on what the child just played.
    /// Returns null if no contextual discovery is possible (item already unlocked, etc.)
    /// </summary>
    public static DiscoveryEntry GetContextual(JourneyProgress jp, string animalKey, string colorKey)
    {
        // Try animal from the game just played
        if (!string.IsNullOrEmpty(animalKey))
        {
            // Capitalize first letter for matching
            string id = char.ToUpper(animalKey[0]) + animalKey.Substring(1).ToLower();
            if (!jp.unlockedAnimalIds.Contains(id) && !IsAlreadyPendingOrQueued(jp, "animal", id)
                && System.Array.Exists(AnimalOrder, a => a == id))
                return new DiscoveryEntry { type = "animal", id = id };
        }

        // Try color from the game just played
        if (!string.IsNullOrEmpty(colorKey))
        {
            string id = char.ToUpper(colorKey[0]) + colorKey.Substring(1).ToLower();
            if (!jp.unlockedColorIds.Contains(id) && !IsAlreadyPendingOrQueued(jp, "color", id)
                && System.Array.Exists(ColorOrder, c => c == id))
                return new DiscoveryEntry { type = "color", id = id };
        }

        return null;
    }

    public static DiscoveryEntry GetNext(JourneyProgress jp)
    {
        int discoveryIndex = jp.discoveryQueue.Count;

        // Pattern: 2 animals, 1 color (positions 0,1 = animal, 2 = color, repeat)
        int cyclePos = discoveryIndex % 3;
        DiscoveryEntry primary = null;

        if (cyclePos < 2)
            primary = NextAnimal(jp);
        else
            primary = NextColor(jp);

        if (primary != null) return primary;

        // Fallback chain
        var animal = NextAnimal(jp);
        if (animal != null) return animal;

        var color = NextColor(jp);
        if (color != null) return color;

        return null;
    }

    private static bool IsAlreadyPendingOrQueued(JourneyProgress jp, string type, string id)
    {
        foreach (var e in jp.pendingWorldRewards)
            if (e.type == type && e.id == id) return true;
        foreach (var e in jp.discoveryQueue)
            if (e.type == type && e.id == id) return true;
        return false;
    }

    private static DiscoveryEntry NextAnimal(JourneyProgress jp)
    {
        foreach (var id in AnimalOrder)
        {
            if (!jp.unlockedAnimalIds.Contains(id) && !IsAlreadyPendingOrQueued(jp, "animal", id))
                return new DiscoveryEntry { type = "animal", id = id };
        }
        return null;
    }

    private static DiscoveryEntry NextColor(JourneyProgress jp)
    {
        foreach (var id in ColorOrder)
        {
            if (!jp.unlockedColorIds.Contains(id) && !IsAlreadyPendingOrQueued(jp, "color", id))
                return new DiscoveryEntry { type = "color", id = id };
        }
        return null;
    }

}
