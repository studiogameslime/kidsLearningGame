using UnityEngine;

/// <summary>
/// A shadow target slot. Holds the animalId it expects to match.
/// </summary>
public class ShadowSlot : MonoBehaviour
{
    [HideInInspector] public string animalId;
    [HideInInspector] public bool isMatched;
}
