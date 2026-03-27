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

        SetPart(bodyImage, data.bodySprite);
        SetPart(eyeLeftImage, data.eyeSprite);
        SetPart(eyeRightImage, data.eyeSprite);
        SetPart(noseImage, data.noseSprite);
        SetPart(mouthImage, data.mouthSprite);
        SetPart(armLeftImage, data.leftArmSprite);
        SetPart(armRightImage, data.rightArmSprite, flipX: true);
        SetPart(legLeftImage, data.leftLegSprite);
        SetPart(legRightImage, data.rightLegSprite, flipX: true);
        SetPart(detailImage, data.detailSprite);

        initialized = true;
    }

    private void SetPart(Image img, string spriteName, bool flipX = false)
    {
        if (img == null) return;
        if (string.IsNullOrEmpty(spriteName))
        {
            img.enabled = false;
            return;
        }

        var sprite = MonsterCreatorController.LoadMonsterSprite(spriteName);
        if (sprite != null)
        {
            img.sprite = sprite;
            img.enabled = true;
            if (flipX)
            {
                var s = img.rectTransform.localScale;
                s.x = -Mathf.Abs(s.x);
                img.rectTransform.localScale = s;
            }
        }
        else
        {
            img.enabled = false;
        }
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
