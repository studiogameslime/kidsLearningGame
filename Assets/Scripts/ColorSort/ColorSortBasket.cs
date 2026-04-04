using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A colored basket in the Color Sort game.
/// Accepts items of the matching color.
/// </summary>
public class ColorSortBasket : MonoBehaviour
{
    public Color basketColor;
    public int colorIndex; // index into the round's color array
    public int filledCount;

    private RectTransform rt;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    /// <summary>Returns a generous drop zone in screen space.</summary>
    public Rect GetDropZoneScreen()
    {
        if (rt == null) rt = GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        float pad = 40f;
        return new Rect(
            corners[0].x - pad, corners[0].y - pad,
            corners[2].x - corners[0].x + pad * 2,
            corners[2].y - corners[0].y + pad * 2);
    }

    /// <summary>Adds an item visually inside the basket with glow and pop animation.</summary>
    public void AddItem(Sprite itemSprite, Color tint)
    {
        var go = new GameObject($"BasketItem_{filledCount}");
        go.transform.SetParent(transform, false);
        go.transform.SetAsFirstSibling(); // behind basket sprite

        var itemRT = go.AddComponent<RectTransform>();
        float size = 80f;
        itemRT.sizeDelta = new Vector2(size, size);
        itemRT.anchorMin = itemRT.anchorMax = new Vector2(0, 1);
        itemRT.pivot = new Vector2(0, 1);

        // Grid: centered inside basket with generous padding for the basket border
        int cols = 4;
        float padX = 50f;  // skip left basket border
        float padY = 40f;  // skip top basket border
        float stepX = (size + 4f);
        float stepY = (size + 4f);
        int col = filledCount % cols;
        int row = filledCount / cols;
        float x = padX + col * stepX;
        float y = -padY - row * stepY;
        itemRT.anchoredPosition = new Vector2(x, y);

        var img = go.AddComponent<Image>();
        img.sprite = itemSprite;
        img.color = tint;
        img.preserveAspect = true;
        img.raycastTarget = false;

        // Pop-in animation
        StartCoroutine(ItemPopIn(itemRT));

        filledCount++;
    }

    public IEnumerator BounceIn(float delay)
    {
        if (rt == null) rt = GetComponent<RectTransform>();
        rt.localScale = Vector3.zero;
        yield return new WaitForSeconds(delay);

        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            float p = t / 0.35f;
            float s = p < 0.65f
                ? Mathf.Lerp(0f, 1.15f, p / 0.65f)
                : Mathf.Lerp(1.15f, 1f, (p - 0.65f) / 0.35f);
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private IEnumerator ItemPopIn(RectTransform itemRT)
    {
        itemRT.localScale = Vector3.zero;
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            float p = t / 0.25f;
            float s = p < 0.6f
                ? Mathf.Lerp(0f, 1.2f, p / 0.6f)
                : Mathf.Lerp(1.2f, 1f, (p - 0.6f) / 0.4f);
            itemRT.localScale = Vector3.one * s;
            yield return null;
        }
        itemRT.localScale = Vector3.one;
    }
}
