using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Legacy drag handler kept for scene compatibility.
/// Input is now handled directly in MazeController.Update().
/// </summary>
public class MazeDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public MazeController controller;

    public void OnBeginDrag(PointerEventData eventData) { }
    public void OnDrag(PointerEventData eventData) { }
    public void OnEndDrag(PointerEventData eventData) { }
}
