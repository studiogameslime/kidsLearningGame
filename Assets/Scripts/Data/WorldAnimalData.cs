using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Legacy combined container. No longer referenced by WorldController at runtime.
/// Kept for editor compatibility.
/// </summary>
[CreateAssetMenu(fileName = "WorldAnimalData", menuName = "Kids Learning Game/World Animal Data")]
public class WorldAnimalData : ScriptableObject
{
    public List<AnimalAnimEntry> animals = new List<AnimalAnimEntry>();

    public AnimalAnimEntry GetAnimal(string animalId)
    {
        foreach (var a in animals)
            if (a.animalId == animalId) return a;
        return null;
    }
}

[Serializable]
public class AnimalAnimEntry
{
    public string animalId;
    public float idleFps;
    public Sprite[] idleFrames;
    public float floatingFps;
    public Sprite[] floatingFrames;
    public float successFps;
    public Sprite[] successFrames;
}
