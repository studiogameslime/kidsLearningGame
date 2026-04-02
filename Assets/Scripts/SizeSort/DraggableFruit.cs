using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A draggable fruit in the Size Sort game.
/// Can be small (0), medium (1), or large (2).
/// </summary>
public class DraggableFruit : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int sizeCategory; // 0=small, 1=medium, 2=large
    public SizeSortController controller;
    public Vector2 homePosition;

    private RectTransform rt;
    private bool isDragging;
    private bool isPlaced;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    private void Start()
    {
        homePosition = rt.anchoredPosition;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isPlaced) return;
        isDragging = true;
        rt.SetAsLastSibling();
        rt.localScale = Vector3.one * 1.1f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isPlaced) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rt.parent as RectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPos);
        rt.anchoredPosition = localPos;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isPlaced) return;
        isDragging = false;
        rt.localScale = Vector3.one;

        if (controller != null)
            controller.OnFruitDropped(this);
    }

    /// <summary>Mark this fruit as successfully placed.</summary>
    public void MarkPlaced()
    {
        isPlaced = true;
        gameObject.SetActive(false);
    }

    /// <summary>Animate back to spawn position after wrong drop.</summary>
    public void ReturnToHome()
    {
        StartCoroutine(ReturnAnimation());
    }

    private IEnumerator ReturnAnimation()
    {
        Vector2 from = rt.anchoredPosition;
        float t = 0f;
        float dur = 0.3f;

        // Quick shake first
        for (int i = 0; i < 3; i++)
        {
            float offset = (i % 2 == 0 ? 8f : -8f) * (1f - i / 3f);
            rt.anchoredPosition = from + new Vector2(offset, 0);
            yield return new WaitForSeconds(0.04f);
        }

        // Smooth return
        from = rt.anchoredPosition;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / dur);
            rt.anchoredPosition = Vector2.Lerp(from, homePosition, p);
            yield return null;
        }

        rt.anchoredPosition = homePosition;
    }

    public IEnumerator PopIn(float delay)
    {
        rt = GetComponent<RectTransform>();
        rt.localScale = Vector3.zero;
        yield return new WaitForSeconds(delay);

        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            float p = t / 0.3f;
            float s = p < 0.65f
                ? Mathf.Lerp(0f, 1.15f, p / 0.65f)
                : Mathf.Lerp(1.15f, 1f, (p - 0.65f) / 0.35f);
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }
}
