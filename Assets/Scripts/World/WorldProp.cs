using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// An interactive tree or bush in the World scene.
/// Tapping causes a playful sway/bounce animation.
/// Trees also spawn falling leaves on tap.
/// </summary>
public class WorldProp : MonoBehaviour
{
    public enum PropType { Tree, Bush }
    public PropType propType = PropType.Tree;

    private RectTransform rt;
    private bool isAnimating;

    private static readonly Color[] LeafColors = {
        new Color(0.40f, 0.75f, 0.25f), // green
        new Color(0.55f, 0.82f, 0.20f), // light green
        new Color(0.85f, 0.78f, 0.15f), // yellow-green
        new Color(0.92f, 0.65f, 0.12f), // golden
        new Color(0.30f, 0.68f, 0.35f), // dark green
    };

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    public void OnTap()
    {
        if (isAnimating) return;

        if (propType == PropType.Tree)
        {
            StartCoroutine(TreeSway());
            SpawnLeaves();
        }
        else
            StartCoroutine(BushBounce());
    }

    private IEnumerator TreeSway()
    {
        isAnimating = true;

        // Sway left-right using rotation
        float dur = 0.5f;
        float elapsed = 0f;
        float maxAngle = 8f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float angle = Mathf.Sin(t * Mathf.PI * 3f) * maxAngle * (1f - t);
            rt.localRotation = Quaternion.Euler(0, 0, angle);
            yield return null;
        }

        rt.localRotation = Quaternion.identity;
        isAnimating = false;
    }

    private void SpawnLeaves()
    {
        int leafCount = Random.Range(5, 8);
        for (int i = 0; i < leafCount; i++)
        {
            StartCoroutine(AnimateLeaf(i));
        }
    }

    private IEnumerator AnimateLeaf(int index)
    {
        // Create leaf as a small colored rectangle
        var leafGO = new GameObject($"Leaf{index}");
        leafGO.transform.SetParent(rt.parent, false);
        var leafRT = leafGO.AddComponent<RectTransform>();
        leafRT.anchorMin = Vector2.zero;
        leafRT.anchorMax = Vector2.zero;
        leafRT.pivot = new Vector2(0.5f, 0.5f);

        float leafSize = Random.Range(6f, 12f);
        leafRT.sizeDelta = new Vector2(leafSize, leafSize * 0.7f);

        // Start near the top of the tree — compute actual X from anchor
        float treeHeight = rt.sizeDelta.y;
        RectTransform parentRT = rt.parent as RectTransform;
        float parentWidth = parentRT != null ? parentRT.rect.width : 1920f;
        float treeActualX = rt.anchorMin.x * parentWidth + rt.anchoredPosition.x;
        float startX = treeActualX + Random.Range(-rt.sizeDelta.x * 0.35f, rt.sizeDelta.x * 0.35f);
        float startY = rt.anchoredPosition.y + treeHeight * Random.Range(0.4f, 0.9f);
        leafRT.anchoredPosition = new Vector2(startX, startY);

        var img = leafGO.AddComponent<Image>();
        Color leafColor = LeafColors[Random.Range(0, LeafColors.Length)];
        img.color = leafColor;
        img.raycastTarget = false;

        // Animate falling with gentle sway
        float dur = Random.Range(1.2f, 2.0f);
        float elapsed = 0f;
        float fallDistance = Random.Range(120f, 220f);
        float swayAmount = Random.Range(20f, 45f);
        float swaySpeed = Random.Range(3f, 5f);
        float rotSpeed = Random.Range(120f, 300f);
        float phaseOffset = Random.Range(0f, Mathf.PI * 2f);

        // Stagger start slightly
        yield return new WaitForSeconds(index * 0.06f);

        Vector2 startPos = leafRT.anchoredPosition;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;

            // Fall with ease-in
            float fallY = fallDistance * t * t;
            // Gentle sway
            float swayX = Mathf.Sin(t * swaySpeed * Mathf.PI + phaseOffset) * swayAmount * (1f - t * 0.5f);
            leafRT.anchoredPosition = new Vector2(startPos.x + swayX, startPos.y - fallY);

            // Gentle rotation
            leafRT.localRotation = Quaternion.Euler(0, 0, elapsed * rotSpeed);

            // Fade out in last 30%
            if (t > 0.7f)
            {
                float fadeT = (t - 0.7f) / 0.3f;
                img.color = new Color(leafColor.r, leafColor.g, leafColor.b, 1f - fadeT);
            }

            yield return null;
        }

        Destroy(leafGO);
    }

    private IEnumerator BushBounce()
    {
        isAnimating = true;

        // Squash-stretch bounce
        float dur = 0.35f;
        float elapsed = 0f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float scaleX = 1f + Mathf.Sin(t * Mathf.PI * 2f) * 0.15f * (1f - t);
            float scaleY = 1f - Mathf.Sin(t * Mathf.PI * 2f) * 0.12f * (1f - t);
            transform.localScale = new Vector3(scaleX, scaleY, 1f);
            yield return null;
        }

        transform.localScale = Vector3.one;
        isAnimating = false;
    }
}
