using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the created monster in the world scene with idle animations.
/// Uses MonsterPreview for body-specific anchor positioning.
/// </summary>
public class MonsterWorldController : MonoBehaviour
{
    public MonsterPreview preview;

    private RectTransform rt;
    private float idleTimer;
    private Vector2 basePos;
    private bool initialized;

    public void Setup(MonsterData data)
    {
        if (data == null || !data.IsComplete) { gameObject.SetActive(false); return; }

        gameObject.SetActive(true);
        rt = GetComponent<RectTransform>();
        basePos = rt.anchoredPosition;

        if (preview == null)
        {
            preview = GetComponent<MonsterPreview>();
            if (preview == null) preview = gameObject.AddComponent<MonsterPreview>();
        }
        if (preview.body == null) preview.Build();

        Color bodyTint = Color.white, armTint = Color.white, legTint = Color.white;
        if (!string.IsNullOrEmpty(data.bodyColorHex))
            ColorUtility.TryParseHtmlString("#" + data.bodyColorHex, out bodyTint);
        if (!string.IsNullOrEmpty(data.armColorHex))
            ColorUtility.TryParseHtmlString("#" + data.armColorHex, out armTint);
        if (!string.IsNullOrEmpty(data.legColorHex))
            ColorUtility.TryParseHtmlString("#" + data.legColorHex, out legTint);

        preview.ApplyData(data, bodyTint, armTint, legTint);
        initialized = true;
    }

    private void Update()
    {
        if (!initialized || rt == null) return;
        idleTimer += Time.deltaTime;
        float bounce = Mathf.Abs(Mathf.Sin(idleTimer * 2f)) * 6f;
        rt.anchoredPosition = basePos + new Vector2(0, bounce);
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(idleTimer * 1.3f) * 2f);
    }

    public void OnMonsterTapped()
    {
        StartCoroutine(TapBounce());
    }

    private IEnumerator TapBounce()
    {
        Vector3 orig = rt.localScale;
        float t = 0;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            float s = 1f + 0.15f * Mathf.Sin(t / 0.3f * Mathf.PI * 2f);
            rt.localScale = orig * s;
            yield return null;
        }
        rt.localScale = orig;
        SoundLibrary.PlayRandomFeedback();
    }
}
