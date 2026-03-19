using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Emits animated smoke puffs from a train chimney.
/// Attach to the chimney GameObject. Puffs rise, expand, drift, and fade.
/// </summary>
public class TrainSmokeEmitter : MonoBehaviour
{
    public Sprite circleSprite;
    public float driftX = -1f;       // -1 = drift left, +1 = drift right
    public float emitInterval = 0.5f;
    public float puffLifetime = 2.0f;
    public float riseSpeed = 40f;
    public float driftSpeed = 20f;
    public float startSize = 14f;
    public float endSize = 40f;

    private RectTransform _rt;

    private void Start()
    {
        _rt = GetComponent<RectTransform>();
        StartCoroutine(EmitLoop());
    }

    private IEnumerator EmitLoop()
    {
        // Small initial delay
        yield return new WaitForSeconds(0.3f);

        while (true)
        {
            SpawnPuff();
            yield return new WaitForSeconds(emitInterval + Random.Range(-0.15f, 0.15f));
        }
    }

    private void SpawnPuff()
    {
        if (_rt == null) return;

        var puffGO = new GameObject("SmokePuff");
        // Parent to chimney's parent (the locomotive) so puff isn't clipped by chimney rect
        var parentTransform = _rt.parent != null ? _rt.parent : _rt;
        puffGO.transform.SetParent(parentTransform, false);

        var puffRT = puffGO.AddComponent<RectTransform>();
        puffRT.anchorMin = new Vector2(0.5f, 1f);
        puffRT.anchorMax = new Vector2(0.5f, 1f);
        puffRT.pivot = new Vector2(0.5f, 0.5f);
        puffRT.sizeDelta = new Vector2(startSize, startSize);

        // Start position: top of chimney with slight random offset
        float chimneyTopY = _rt.anchoredPosition.y + _rt.sizeDelta.y;
        puffRT.anchoredPosition = new Vector2(
            _rt.anchoredPosition.x + Random.Range(-5f, 5f),
            chimneyTopY + 5f);

        var img = puffGO.AddComponent<Image>();
        if (circleSprite != null) img.sprite = circleSprite;
        img.color = new Color(0.92f, 0.92f, 0.92f, 0.65f);
        img.raycastTarget = false;

        StartCoroutine(AnimatePuff(puffRT, img, puffGO));
    }

    private IEnumerator AnimatePuff(RectTransform rt, Image img, GameObject go)
    {
        float elapsed = 0f;
        Vector2 startPos = rt.anchoredPosition;
        float randomDriftOffset = Random.Range(-8f, 8f);

        while (elapsed < puffLifetime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / puffLifetime;

            // Rise upward + drift sideways
            float y = startPos.y + riseSpeed * t;
            float x = startPos.x + (driftX * driftSpeed * t) + randomDriftOffset * t;
            rt.anchoredPosition = new Vector2(x, y);

            // Grow
            float size = Mathf.Lerp(startSize, endSize, t);
            rt.sizeDelta = new Vector2(size, size);

            // Fade out
            float alpha = Mathf.Lerp(0.6f, 0f, t);
            img.color = new Color(0.92f, 0.92f, 0.92f, alpha);

            yield return null;
        }

        Destroy(go);
    }
}
