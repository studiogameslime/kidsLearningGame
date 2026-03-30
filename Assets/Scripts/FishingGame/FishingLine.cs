using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visual fishing line rendered between Elroey's rod tip and a target fish.
/// Uses a thin UI Image stretched and rotated between two points.
/// </summary>
public class FishingLine : MonoBehaviour
{
    public RectTransform lineImage;
    public RectTransform rodTip;

    private Coroutine activeRoutine;
    private Vector2 _rodPosOverride;
    private bool _hasRodPosOverride;

    /// <summary>Sets the rod position in the line's coordinate space without moving the actual rodTip anchor.</summary>
    public void SetRodPosition(Vector2 posInLineSpace)
    {
        _rodPosOverride = posInLineSpace;
        _hasRodPosOverride = true;
    }

    private Vector2 GetRodPosition()
    {
        return _hasRodPosOverride ? _rodPosOverride : rodTip.anchoredPosition;
    }

    public void Init(RectTransform canvasParent)
    {
        var go = new GameObject("FishingLineVisual");
        go.transform.SetParent(canvasParent, false);
        lineImage = go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = new Color(0.35f, 0.25f, 0.15f, 0.9f); // dark brown line
        img.raycastTarget = false;
        lineImage.sizeDelta = new Vector2(3, 0);
        lineImage.pivot = new Vector2(0.5f, 0);
        go.SetActive(false);
    }

    /// <summary>
    /// Casts the line from the rod tip to the target position over duration seconds.
    /// </summary>
    public void Cast(Vector2 targetPos, float duration, System.Action onReached)
    {
        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(CastRoutine(targetPos, duration, onReached));
    }

    /// <summary>
    /// Retracts the line back to the rod tip over duration seconds.
    /// </summary>
    public void Retract(float duration, System.Action onDone)
    {
        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(RetractRoutine(duration, onDone));
    }

    /// <summary>
    /// Pulls fish along the line back to the rod, with the line shortening.
    /// </summary>
    public void PullFish(RectTransform fishRT, Vector2 targetInFishSpace, float duration, System.Action onDone)
    {
        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(PullRoutine(fishRT, targetInFishSpace, duration, onDone));
    }

    public void Hide()
    {
        if (activeRoutine != null) { StopCoroutine(activeRoutine); activeRoutine = null; }
        if (lineImage != null) lineImage.gameObject.SetActive(false);
        _hasRodPosOverride = false;
    }

    // ── Routines ──

    private IEnumerator CastRoutine(Vector2 target, float duration, System.Action onReached)
    {
        lineImage.gameObject.SetActive(true);
        Vector2 start = GetRodPosition();

        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0, 1, t / duration);
            Vector2 currentEnd = Vector2.Lerp(start, target, p);
            UpdateLineVisual(start, currentEnd);
            yield return null;
        }

        UpdateLineVisual(start, target);
        onReached?.Invoke();
    }

    private IEnumerator RetractRoutine(float duration, System.Action onDone)
    {
        if (lineImage == null || !lineImage.gameObject.activeSelf)
        {
            onDone?.Invoke();
            yield break;
        }

        Vector2 start = GetRodPosition();
        // Current end position from the line's current state
        Vector2 currentEnd = GetLineEndPosition();

        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0, 1, t / duration);
            Vector2 end = Vector2.Lerp(currentEnd, start, p);
            UpdateLineVisual(start, end);
            yield return null;
        }

        lineImage.gameObject.SetActive(false);
        onDone?.Invoke();
    }

    private IEnumerator PullRoutine(RectTransform fishRT, Vector2 targetInFishSpace, float duration, System.Action onDone)
    {
        Vector2 start = fishRT.anchoredPosition;
        Vector2 target = targetInFishSpace;
        RectTransform lineParent = lineImage.parent as RectTransform;

        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0, 1, t / duration);
            Vector2 fishPos = Vector2.Lerp(start, target, p);
            fishRT.anchoredPosition = fishPos;

            // Shrink fish as it approaches
            float scale = Mathf.Lerp(1f, 0.3f, p);
            fishRT.localScale = new Vector3(scale, scale, 1f);

            // Convert fish world position to line's coordinate space so line shortens correctly
            Vector2 fishInLineSpace = WorldToLocal(fishRT, lineParent);
            UpdateLineVisual(GetRodPosition(), fishInLineSpace);
            yield return null;
        }

        lineImage.gameObject.SetActive(false);
        fishRT.gameObject.SetActive(false);
        onDone?.Invoke();
    }

    /// <summary>Converts a RectTransform's world position to local position in a target parent.</summary>
    private static Vector2 WorldToLocal(RectTransform source, RectTransform targetParent)
    {
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, source.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(targetParent, screenPos, null, out Vector2 localPos);
        return localPos;
    }

    private void UpdateLineVisual(Vector2 from, Vector2 to)
    {
        Vector2 diff = to - from;
        float dist = diff.magnitude;
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg - 90f;

        lineImage.anchoredPosition = from;
        lineImage.sizeDelta = new Vector2(3, dist);
        lineImage.localRotation = Quaternion.Euler(0, 0, angle);
    }

    private Vector2 GetLineEndPosition()
    {
        // Calculate end from current rotation and height
        float height = lineImage.sizeDelta.y;
        float angle = (lineImage.localRotation.eulerAngles.z + 90f) * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        return lineImage.anchoredPosition + dir * height;
    }
}
