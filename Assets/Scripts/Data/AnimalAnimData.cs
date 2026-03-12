using UnityEngine;

/// <summary>
/// Stores pre-extracted animation sprite frames for a single animal,
/// so the World scene can animate UI Images without SpriteRenderer.
/// Built by WorldSceneSetup at editor time. One asset per animal in
/// Resources/AnimalAnim/ for on-demand loading.
/// </summary>
[CreateAssetMenu(fileName = "AnimalAnimData", menuName = "Kids Learning Game/Animal Anim Data")]
public class AnimalAnimData : ScriptableObject
{
    public string animalId;
    public float idleFps;
    public Sprite[] idleFrames;
    public float floatingFps;
    public Sprite[] floatingFrames;
    public float successFps;
    public Sprite[] successFrames;

    /// <summary>Load a single animal's anim data from Resources/AnimalAnim/{animalId}.</summary>
    public static AnimalAnimData Load(string animalId)
    {
        return Resources.Load<AnimalAnimData>($"AnimalAnim/{animalId}");
    }
}
