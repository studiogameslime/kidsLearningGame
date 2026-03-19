using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Handles drag events on number option tiles in the Number Train game.
/// Delegates to NumberTrainController for validation and placement.
/// </summary>
public class NumberTrainDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [HideInInspector] public NumberTrainController controller;
    [HideInInspector] public int value;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (controller != null)
            controller.OnOptionDragBegin(gameObject, value, eventData);
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
