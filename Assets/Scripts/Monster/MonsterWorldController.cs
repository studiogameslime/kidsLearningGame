using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the created monster in the world scene with idle animations.
/// Reconstructs the monster from MonsterData sprites.
/// </summary>
public class MonsterWorldController : MonoBehaviour
{
    [Header("Part Images")]
    public Image bodyImage;
    public Image eyeLeftImage;
    public Image eyeRightImage;
    public Image noseImage;
    public Image mouthImage;
    public Image armLeftImage;
    public Image armRightImage;
    public Image legLeftImage;
    public Image legRightImage;
    public Image detailImage;

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

        Color bodyTint = Color.white, armTint = Color.white, legTint = Color.white;
        if (!string.IsNullOrEmpty(data.bodyColorHex))
            ColorUtility.TryParseHtmlString("#" + data.bodyColorHex, out bodyTint);
        if (!string.IsNullOrEmpty(data.armColorHex))
            ColorUtility.TryParseHtmlString("#" + data.armColorHex, out armTint);
        if (!string.IsNullOrEmpty(data.legColorHex))
            ColorUtility.TryParseHtmlString("#" + data.legColorHex, out legTint);

        SetPart(bodyImage, data.bodySprite, tint: bodyTint);
        SetPart(eyeLeftImage, data.eyeSprite);
        SetPart(eyeRightImage, data.eyeSprite);
        SetPart(noseImage, data.noseSprite);
        SetPart(mouthImage, data.mouthSprite);
        // Sprite curves RIGHT by default → screen-left flipped, screen-right normal
        SetPart(armLeftImage, data.armSprite, flipX: true, tint: armTint);
        SetPart(armRightImage, data.armSprite, flipX: false, tint: armTint);
        SetPart(legLeftImage, data.legSprite, flipX: true, tint: legTint);
        SetPart(legRightImage, data.legSprite, flipX: false, tint: legTint);
        SetPart(detailImage, data.detailSprite);

        initialized = true;
    }

    private void SetPart(Image img, string spriteName, bool flipX = false, Color? tint = null)
    {
        if (img == null) return;
        if (string.IsNullOrEmpty(spriteName)) { img.enabled = false; return; }

        var sprite = MonsterCreatorController.LoadMonsterSprite(spriteName);
        if (sprite != null)
        {
            img.sprite = sprite; img.enabled = true;
            img.color = tint ?? Color.white;
            var s = img.rectTransform.localScale;
            s.x = flipX ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);
            img.rectTransform.localScale = s;
        }
        else { img.enabled = false; }
    }

    // ── Idle Animation ──

    private void Update()
    {
        if (!initialized || rt == null) return;

        idleTimer += Time.deltaTime;

        // Gentle bounce
        float bounce = Mathf.Abs(Mathf.Sin(idleTimer * 2f)) * 6f;
        rt.anchoredPosition = basePos + new Vector2(0, bounce);

        // Subtle body sway
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(idleTimer * 1.3f) * 2f);
    }

    // ── Tap Reaction ──

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
