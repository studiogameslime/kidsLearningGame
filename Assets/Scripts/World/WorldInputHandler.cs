using UnityEngine;

/// <summary>
/// Custom input handler for the World scene. Routes touch/mouse to:
/// - WorldAnimal tap/drag
/// - WorldBalloon tap
/// - World pan (empty area drag)
/// No ScrollRect used to avoid drag conflicts.
/// </summary>
public class WorldInputHandler : MonoBehaviour
{
    [Header("References")]
    public RectTransform worldContent;
    public RectTransform viewport;

    [Header("Settings")]
    public float dragThreshold = 20f;

    private bool isDragging;
    private bool isWorldPan;
    private Vector2 touchStartPos;
    private Vector2 contentStartPos;
    private WorldAnimal draggedAnimal;

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            OnTouchStart(Input.mousePosition);
        else if (Input.GetMouseButton(0))
            OnTouchMove(Input.mousePosition);
        else if (Input.GetMouseButtonUp(0))
            OnTouchEnd(Input.mousePosition);
    }

    private void OnTouchStart(Vector2 screenPos)
    {
        touchStartPos = screenPos;
        contentStartPos = worldContent.anchoredPosition;
        isDragging = false;
        isWorldPan = false;
        draggedAnimal = null;

        // Raycast to check what we hit
        var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        var pointerData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
        {
            position = screenPos
        };
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointerData, results);

        foreach (var result in results)
        {
            var animal = result.gameObject.GetComponent<WorldAnimal>();
            if (animal != null)
            {
                draggedAnimal = animal;
                animal.OnTouchStart(screenPos);
                return;
            }

            var balloon = result.gameObject.GetComponent<WorldBalloon>();
            if (balloon != null)
            {
                balloon.Pop();
                return;
            }
        }

        // No animal or balloon hit — will be a world pan
        isWorldPan = true;
    }

    private void OnTouchMove(Vector2 screenPos)
    {
        Vector2 delta = screenPos - touchStartPos;

        if (!isDragging && delta.magnitude > dragThreshold)
            isDragging = true;

        if (!isDragging) return;

        if (draggedAnimal != null)
        {
            draggedAnimal.OnDrag(screenPos);
        }
        else if (isWorldPan)
        {
            PanWorld(delta);
        }
    }

    private void OnTouchEnd(Vector2 screenPos)
    {
        if (draggedAnimal != null)
        {
            if (!isDragging)
                draggedAnimal.OnTap();
            else
                draggedAnimal.OnDragEnd();
            draggedAnimal = null;
        }

        isDragging = false;
        isWorldPan = false;
    }

    private void PanWorld(Vector2 totalDelta)
    {
        float newX = contentStartPos.x + totalDelta.x;

        // Clamp to bounds
        float contentWidth = worldContent.rect.width;
        float viewportWidth = viewport != null ? viewport.rect.width : 1080f;
        float minX = -(contentWidth - viewportWidth);
        float maxX = 0f;

        if (contentWidth <= viewportWidth)
            newX = 0f;
        else
            newX = Mathf.Clamp(newX, minX, maxX);

        worldContent.anchoredPosition = new Vector2(newX, worldContent.anchoredPosition.y);
    }
}
