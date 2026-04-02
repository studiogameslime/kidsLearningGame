using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A cart in the Size Sort game. Holds up to 5 fruits of a specific size category.
/// Provides slot positions for placing fruits visibly inside the cart.
/// </summary>
public class SizeSortCart : MonoBehaviour
{
    public int sizeCategory; // 0=small, 1=medium, 2=large
    public int filledCount;

    private RectTransform rt;

    // Sprite content bounds within 612px wide image (from .meta rect data)
    // Normalized to 0-1 range: left edge, right edge
    private static readonly float[][] CartContentBounds = new float[][]
    {
        // Small cart: shifted right + wider range
        new float[] { 0.35f, 0.82f },
        // Medium cart: shifted right slightly
        new float[] { 0.27f, 0.82f },
        // Large cart: rect x:85 w:428 in 612px → left=0.139, right=0.838
        new float[] { 0.139f, 0.838f },
    };

    /// <summary>
    /// Returns slot positions in a horizontal row spanning the actual visible cart area.
    /// </summary>
    private Vector2 GetSlotPosition(int slotIndex)
    {
        if (rt == null) rt = GetComponent<RectTransform>();
        float cartW = rt.sizeDelta.x;
        int cat = Mathf.Clamp(sizeCategory, 0, 2);

        float leftNorm = CartContentBounds[cat][0];
        float rightNorm = CartContentBounds[cat][1];

        // Add some inner padding (10% on each side of the content area)
        float contentW = rightNorm - leftNorm;
        float padded = contentW * 0.10f;
        leftNorm += padded;
        rightNorm -= padded;

        // Convert to local coordinates (-cartW/2 to +cartW/2)
        float leftX = (leftNorm - 0.5f) * cartW;
        float rightX = (rightNorm - 0.5f) * cartW;
        float step = (rightX - leftX) / 4f; // 4 gaps for 5 items
        float x = leftX + step * slotIndex;
        float y = 5f;

        return new Vector2(x, y);
    }

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    /// <summary>Returns the local position for the next fruit slot.</summary>
    public Vector2 GetNextSlotLocalPos()
    {
        return GetSlotPosition(Mathf.Clamp(filledCount, 0, 4));
    }

    /// <summary>Adds a fruit visually into the cart at the next slot.</summary>
    public void AddFruit(Sprite fruitSprite, float fruitScale)
    {
        Vector2 slotPos = GetNextSlotLocalPos();

        var go = new GameObject($"CartFruit_{filledCount}");
        go.transform.SetParent(transform, false);
        go.transform.SetAsFirstSibling(); // behind cart sprite so fruit looks inside

        var fruitRT = go.AddComponent<RectTransform>();
        float size = 95f * fruitScale;
        fruitRT.sizeDelta = new Vector2(size, size);
        fruitRT.anchorMin = fruitRT.anchorMax = new Vector2(0.5f, 0.5f);
        fruitRT.pivot = new Vector2(0.5f, 0.5f);
        fruitRT.anchoredPosition = slotPos;

        var img = go.AddComponent<Image>();
        img.sprite = fruitSprite;
        img.preserveAspect = true;
        img.raycastTarget = false;

        filledCount++;
    }

    /// <summary>Returns a generous drop zone rect in world space for hit testing.</summary>
    public Rect GetDropZoneScreen()
    {
        if (rt == null) rt = GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        // Expand by 30px on each side for child-friendly targeting
        float pad = 30f;
        float xMin = corners[0].x - pad;
        float yMin = corners[0].y - pad;
        float xMax = corners[2].x + pad;
        float yMax = corners[2].y + pad;
        return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
    }
}
