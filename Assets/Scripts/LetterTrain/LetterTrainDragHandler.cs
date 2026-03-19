using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Handles drag events on letter option tiles in the Letter Train game.
/// </summary>
public class LetterTrainDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [HideInInspector] public LetterTrainController controller;
    [HideInInspector] public char letter;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (controller != null)
            controller.OnOptionDragBegin(gameObject, letter, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (controller != null)
            controller.OnOptionDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (controller != null)
            controller.OnOptionDragEnd(eventData);
    }
}
