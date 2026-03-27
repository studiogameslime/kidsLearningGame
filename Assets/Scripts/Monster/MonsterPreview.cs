using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages a modular monster preview with body-specific anchor positioning.
/// Each part is a separate child Image that repositions when the body changes.
/// Animation-friendly: all parts are independent transforms.
///
/// Hierarchy:
///   MonsterRoot
///     ├── LegLeft
///     ├── LegRight
///     ├── Body
///     ├── ArmLeft
///     ├── ArmRight
///     ├── EyeLeft
///     ├── EyeRight
///     ├── Nose
///     ├── Mouth
///     └── Detail
/// </summary>
public class MonsterPreview : MonoBehaviour
{
    public Image body;
    public Image armLeft;
    public Image armRight;
    public Image legLeft;
    public Image legRight;
    public Image eyeLeft;
    public Image eyeRight;
    public Image nose;
    public Image mouth;
    public Image detail;

    private string currentBodySprite;

    /// <summary>
    /// Creates the full part hierarchy under this transform.
    /// Call once during setup.
    /// </summary>
    public void Build()
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();

        // Create parts in render order (back to front)
        legLeft  = CreatePart("LegLeft");
        legRight = CreatePart("LegRight");
        body     = CreatePart("Body");
        armLeft  = CreatePart("ArmLeft");
        armRight = CreatePart("ArmRight");
        eyeLeft  = CreatePart("EyeLeft");
        eyeRight = CreatePart("EyeRight");
        nose     = CreatePart("Nose");
        mouth    = CreatePart("Mouth");
        detail   = CreatePart("Detail");
    }

    private Image CreatePart(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var partRT = go.AddComponent<RectTransform>();
        partRT.anchorMin = partRT.anchorMax = new Vector2(0.5f, 0.5f);
        partRT.pivot = new Vector2(0.5f, 0.5f);
        var img = go.AddComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.enabled = false;
        return img;
    }

    /// <summary>
    /// Sets the body sprite and repositions all parts using body-specific anchors.
    /// </summary>
    public void SetBody(string spriteName, Color tint)
    {
        currentBodySprite = spriteName;
        var sprite = MonsterCreatorController.LoadMonsterSprite(spriteName);
        if (sprite == null) return;

        var anchors = MonsterBodyConfig.Get(spriteName);

        // Body
        body.sprite = sprite;
        body.color = tint;
        body.enabled = true;
        body.rectTransform.sizeDelta = anchors.bodySize;
        body.rectTransform.anchoredPosition = Vector2.zero;

        // Reposition all attached parts
        RepositionPart(armLeft,  anchors.leftArmAnchor,  anchors.armSize);
        RepositionPart(armRight, anchors.rightArmAnchor, anchors.armSize);
        RepositionPart(legLeft,  anchors.leftLegAnchor,  anchors.legSize);
        RepositionPart(legRight, anchors.rightLegAnchor, anchors.legSize);
        RepositionPart(eyeLeft,  anchors.eyeLeftAnchor,  anchors.eyeSize);
        RepositionPart(eyeRight, anchors.eyeRightAnchor, anchors.eyeSize);
        RepositionPart(nose,     anchors.noseAnchor,     new Vector2(anchors.eyeSize.x * 0.7f, anchors.eyeSize.y * 0.7f));
        RepositionPart(mouth,    anchors.mouthAnchor,    new Vector2(anchors.eyeSize.x * 1.2f, anchors.eyeSize.y * 0.6f));
        RepositionPart(detail,   anchors.detailAnchor,   anchors.eyeSize);
    }

    private void RepositionPart(Image part, Vector2 position, Vector2 size)
    {
        if (part == null) return;
        part.rectTransform.anchoredPosition = position;
        part.rectTransform.sizeDelta = size;
    }

    /// <summary>Sets a part sprite with optional tint and flip.</summary>
    public void SetPart(Image part, string spriteName, Color tint, bool flipX = false)
    {
        if (part == null) return;
        if (string.IsNullOrEmpty(spriteName)) { part.enabled = false; return; }

        var sprite = MonsterCreatorController.LoadMonsterSprite(spriteName);
        if (sprite == null) { part.enabled = false; return; }

        part.sprite = sprite;
        part.color = tint;
        part.enabled = true;

        var s = part.rectTransform.localScale;
        s.x = flipX ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);
        part.rectTransform.localScale = s;
    }

    /// <summary>Applies a full MonsterData to the preview.</summary>
    public void ApplyData(MonsterData data, Color bodyColor, Color armColor, Color legColor)
    {
        if (data == null) return;

        SetBody(data.bodySprite, bodyColor);
        SetPart(eyeLeft,  data.eyeSprite,  Color.white);
        SetPart(eyeRight, data.eyeSprite,  Color.white);
        SetPart(nose,      data.noseSprite,  Color.white);
        SetPart(mouth,     data.mouthSprite, Color.white);
        SetPart(armLeft,   data.armSprite,   armColor, flipX: true);
        SetPart(armRight,  data.armSprite,   armColor, flipX: false);
        SetPart(legLeft,   data.legSprite,   legColor, flipX: true);
        SetPart(legRight,  data.legSprite,   legColor, flipX: false);
        SetPart(detail,    data.detailSprite, Color.white);
    }

    /// <summary>Play a pop animation on a specific part.</summary>
    public void PopPart(Image part)
    {
        if (part != null)
            StartCoroutine(PopAnim(part.rectTransform));
    }

    private IEnumerator PopAnim(RectTransform rt)
    {
        Vector3 orig = rt.localScale;
        float t = 0;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            float s = 1f + 0.3f * Mathf.Sin(t / 0.2f * Mathf.PI);
            rt.localScale = new Vector3(
                orig.x > 0 ? Mathf.Abs(orig.x) * s : -Mathf.Abs(orig.x) * s,
                Mathf.Abs(orig.y) * s, orig.z);
            yield return null;
        }
        rt.localScale = orig;
    }

    /// <summary>Get current body anchors for external use.</summary>
    public MonsterBodyConfig.BodyAnchors GetCurrentAnchors()
    {
        return MonsterBodyConfig.Get(currentBodySprite);
    }
}
