using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A draggable colored item in the Color Sort game.
/// White sprite tinted with a specific color.
/// </summary>
public class ColorSortItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int colorIndex;
    public Color itemColor;
    public ColorSortController controller;
    public Vector2 homePosition;
    public Sprite itemSprite; // keep reference for basket display

    private RectTransform rt;
    private bool isPlaced;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    public void SetHome()
    {
        homePosition = rt.anchoredPosition;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isPlaced) return;
        rt.SetAsLastSibling();
        rt.localScale = Vector3.one * 1.12f;
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
        rt.localScale = Vector3.one;
        if (controller != null)
            controller.OnItemDropped(this);
    }

    public void MarkPlaced()
    {
        isPlaced = true;
        gameObject.SetActive(false);
    }

    public void ReturnToHome()
    {
        StartCoroutine(ReturnAnimation());
    }

    private IEnumerator ReturnAnimation()
    {
        Vector2 from = rt.anchoredPosition;

        // Quick shake
        for (int i = 0; i < 3; i++)
        {
            float offset = (i % 2 == 0 ? 8f : -8f) * (1f - i / 3f);
            rt.anchoredPosition = from + new Vector2(offset, 0);
            yield return new WaitForSeconds(0.04f);
        }

        // Smooth return
        from = rt.anchoredPosition;
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / 0.3f);
            rt.anchoredPosition = Vector2.Lerp(from, homePosition, p);
            yield return null;
        }
        rt.anchoredPosition = homePosition;
    }

    public IEnumerator PopIn(float delay)
    {
        if (rt == null) rt = GetComponent<RectTransform>();
        rt.localScale = Vector3.zero;
        yield return new WaitForSeconds(delay);

        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            float p = t / 0.25f;
            float s = p < 0.65f
                ? Mathf.Lerp(0f, 1.12f, p / 0.65f)
                : Mathf.Lerp(1.12f, 1f, (p - 0.65f) / 0.35f);
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }
}
