using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Sticker award popup — a colored balloon rises from the bottom with the sticker inside.
/// The child taps the balloon to pop it → sticker pulse → shrink away.
/// The sticker is ONLY added to the collection when the balloon is popped.
/// </summary>
public static class StickerPopup
{
    private static bool _isShowing;

    /// <summary>
    /// Optional callback invoked when the balloon is popped. Visual only — sticker already collected.
    /// </summary>
    public static System.Action<string> OnStickerCollected;

    public static IEnumerator Show(string stickerId)
    {
        if (_isShowing) yield break;

        Sprite sprite = StickerSpriteBank.GetSprite(stickerId);

        // Achievement stickers use game thumbnail instead of sprite bank
        if (sprite == null && stickerId.StartsWith("achievement_"))
        {
            var db = Resources.Load<GameDatabase>("GameDatabase");
            if (db != null)
            {
                string withoutPrefix = stickerId.Substring("achievement_".Length);
                int lastUnderscore = withoutPrefix.LastIndexOf('_');
                if (lastUnderscore > 0)
                {
                    string gameId = withoutPrefix.Substring(0, lastUnderscore);
                    foreach (var game in db.games)
                        if (game != null && game.id == gameId && game.thumbnail != null)
                        { sprite = game.thumbnail; break; }
                }
            }
        }

        if (sprite == null) yield break;

        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) yield break;

        _isShowing = true;

        // Profile color for balloon
        Color balloonColor = new Color(0.56f, 0.79f, 0.98f); // default blue
        var profile = ProfileManager.ActiveProfile;
        if (profile != null) balloonColor = profile.AvatarColor;

        // ── Root overlay ──
        var rootGO = new GameObject("StickerPopup");
        rootGO.transform.SetParent(canvas.transform, false);
        rootGO.transform.SetAsLastSibling();
        var rootRT = rootGO.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero; rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero; rootRT.offsetMax = Vector2.zero;

        // Dim background
        var dimImg = rootGO.AddComponent<Image>();
        dimImg.color = new Color(0, 0, 0, 0);
        dimImg.raycastTarget = false; // only balloon is tappable

        // ── Balloon container (moves up) ──
        var balloonContainer = new GameObject("BalloonContainer");
        balloonContainer.transform.SetParent(rootGO.transform, false);
        var balloonContainerRT = balloonContainer.AddComponent<RectTransform>();
        balloonContainerRT.anchorMin = new Vector2(0.5f, 0.5f);
        balloonContainerRT.anchorMax = new Vector2(0.5f, 0.5f);
        balloonContainerRT.sizeDelta = new Vector2(320, 420);

        // Start below screen
        var canvasRT = canvas.GetComponent<RectTransform>();
        float canvasH = canvasRT != null ? canvasRT.rect.height : 1080f;
        balloonContainerRT.anchoredPosition = new Vector2(0, -canvasH * 0.7f);

        // ── Balloon string (thin line below balloon) ──
        var stringGO = new GameObject("String");
        stringGO.transform.SetParent(balloonContainer.transform, false);
        var stringRT = stringGO.AddComponent<RectTransform>();
        stringRT.anchorMin = new Vector2(0.5f, 0);
        stringRT.anchorMax = new Vector2(0.5f, 0);
        stringRT.pivot = new Vector2(0.5f, 1);
        stringRT.anchoredPosition = new Vector2(0, 20);
        stringRT.sizeDelta = new Vector2(3, 80);
        var stringImg = stringGO.AddComponent<Image>();
        stringImg.color = new Color(balloonColor.r * 0.7f, balloonColor.g * 0.7f, balloonColor.b * 0.7f);
        stringImg.raycastTarget = false;

        // ── Balloon body (circle sprite, semi-transparent) ──
        var circleSprite = Resources.Load<Sprite>("Circle");
        if (circleSprite == null)
        {
            // Fallback: try UI path
            circleSprite = Resources.Load<Sprite>("UI/Circle");
        }
        if (circleSprite == null)
        {
            Debug.LogWarning("[StickerPopup] Circle sprite not found in Resources!");
            Object.Destroy(rootGO);
            _isShowing = false;
            yield break;
        }

        var balloonGO = new GameObject("Balloon");
        balloonGO.transform.SetParent(balloonContainer.transform, false);
        var balloonRT = balloonGO.AddComponent<RectTransform>();
        balloonRT.anchorMin = new Vector2(0.5f, 0.5f);
        balloonRT.anchorMax = new Vector2(0.5f, 0.5f);
        balloonRT.anchoredPosition = new Vector2(0, 30);
        balloonRT.sizeDelta = new Vector2(280, 280);
        var balloonImg = balloonGO.AddComponent<Image>();
        if (circleSprite != null) balloonImg.sprite = circleSprite;
        balloonImg.color = new Color(balloonColor.r, balloonColor.g, balloonColor.b, 0.3f);
        balloonImg.raycastTarget = true; // TAPPABLE

        // Balloon highlight (shine effect — small white circle at top-left)
        var shineGO = new GameObject("Shine");
        shineGO.transform.SetParent(balloonGO.transform, false);
        var shineRT = shineGO.AddComponent<RectTransform>();
        shineRT.anchorMin = new Vector2(0.15f, 0.55f);
        shineRT.anchorMax = new Vector2(0.4f, 0.8f);
        shineRT.offsetMin = Vector2.zero;
        shineRT.offsetMax = Vector2.zero;
        var shineImg = shineGO.AddComponent<Image>();
        if (circleSprite != null) shineImg.sprite = circleSprite;
        shineImg.color = new Color(1f, 1f, 1f, 0.25f);
        shineImg.raycastTarget = false;

        // ── Sticker inside balloon (clipped to circle) ──
        var stickerMaskGO = new GameObject("StickerMask");
        stickerMaskGO.transform.SetParent(balloonGO.transform, false);
        var stickerMaskRT = stickerMaskGO.AddComponent<RectTransform>();
        stickerMaskRT.anchorMin = new Vector2(0.1f, 0.1f);
        stickerMaskRT.anchorMax = new Vector2(0.9f, 0.9f);
        stickerMaskRT.offsetMin = Vector2.zero;
        stickerMaskRT.offsetMax = Vector2.zero;
        var maskImg = stickerMaskGO.AddComponent<Image>();
        if (circleSprite != null) maskImg.sprite = circleSprite;
        maskImg.color = new Color(1, 1, 1, 0); // invisible mask shape
        maskImg.raycastTarget = false;
        stickerMaskGO.AddComponent<Mask>().showMaskGraphic = false;

        var stickerGO = new GameObject("Sticker");
        stickerGO.transform.SetParent(stickerMaskGO.transform, false);
        var stickerRT = stickerGO.AddComponent<RectTransform>();
        stickerRT.anchorMin = new Vector2(0.05f, 0.05f);
        stickerRT.anchorMax = new Vector2(0.95f, 0.95f);
        stickerRT.offsetMin = Vector2.zero;
        stickerRT.offsetMax = Vector2.zero;
        var stickerImg = stickerGO.AddComponent<Image>();
        stickerImg.sprite = sprite;
        stickerImg.preserveAspect = true;
        stickerImg.raycastTarget = false;

        // ── Tap handler ──
        bool popped = false;
        var btn = balloonGO.AddComponent<Button>();
        btn.targetGraphic = balloonImg;
        btn.onClick.AddListener(() => popped = true);

        // ═══════════════════════════════
        //  ANIMATION
        // ═══════════════════════════════

        // ── Phase 1: Dim in + balloon rises from bottom (0.8s) ──
        Vector2 startPos = balloonContainerRT.anchoredPosition;
        Vector2 endPos = new Vector2(0, 20); // slightly above center
        float t = 0f;
        float riseDur = 0.8f;
        while (t < riseDur)
        {
            if (rootGO == null) { _isShowing = false; yield break; }
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / riseDur);
            float ease = 1f - Mathf.Pow(1f - p, 3f); // ease out cubic
            balloonContainerRT.anchoredPosition = Vector2.Lerp(startPos, endPos, ease);
            dimImg.color = new Color(0, 0, 0, p * 0.35f);
            yield return null;
        }
        if (rootGO == null) { _isShowing = false; yield break; }
        balloonContainerRT.anchoredPosition = endPos;

        // Disable tap during rise
        btn.interactable = true;

        // ── Phase 2: Gentle float + wait for tap (15s timeout) ──
        float floatTime = 0f;
        const float tapTimeout = 15f;
        while (!popped)
        {
            if (rootGO == null) { _isShowing = false; yield break; }
            floatTime += Time.deltaTime;
            if (floatTime > tapTimeout) { popped = true; break; } // auto-pop after timeout
            // Gentle bob
            float bobY = Mathf.Sin(floatTime * 1.5f) * 8f;
            float bobX = Mathf.Sin(floatTime * 0.7f) * 4f;
            balloonContainerRT.anchoredPosition = endPos + new Vector2(bobX, bobY);
            // Subtle scale breathe
            float breathe = 1f + Mathf.Sin(floatTime * 2f) * 0.015f;
            balloonRT.localScale = Vector3.one * breathe;
            yield return null;
        }

        // ── Phase 3: Pop! ──
        if (rootGO == null) { _isShowing = false; yield break; }

        SoundLibrary.PlayBubblePop();

        // Collect the sticker NOW
        OnStickerCollected?.Invoke(stickerId);

        // Hide balloon, show sticker free
        balloonImg.enabled = false;
        shineGO.SetActive(false);
        stringGO.SetActive(false);
        stickerMaskGO.transform.SetParent(balloonContainer.transform, true); // unparent from balloon

        // Remove mask so sticker shows fully
        if (stickerMaskGO.GetComponent<Mask>() != null)
            Object.Destroy(stickerMaskGO.GetComponent<Mask>());
        if (maskImg != null) maskImg.enabled = false;

        // Spawn pop particles
        SpawnPopParticles(rootGO.transform, balloonContainerRT.anchoredPosition, balloonColor, circleSprite);

        // ── Phase 4: Sticker pulse (0.3s) ──
        t = 0f;
        while (t < 0.3f)
        {
            if (rootGO == null) { _isShowing = false; yield break; }
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / 0.3f);
            float scale = 1f + Mathf.Sin(p * Mathf.PI) * 0.25f;
            stickerMaskGO.transform.localScale = Vector3.one * scale;
            yield return null;
        }

        // ── Phase 5: Hold (1.0s) ──
        yield return new WaitForSeconds(1.0f);

        // ── Phase 6: Shrink + fade (0.4s) ──
        if (rootGO == null) { _isShowing = false; yield break; }
        t = 0f;
        while (t < 0.4f)
        {
            if (rootGO == null) { _isShowing = false; yield break; }
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / 0.4f);
            float ease = p * p;
            stickerMaskGO.transform.localScale = Vector3.one * (1f - ease);
            dimImg.color = new Color(0, 0, 0, 0.35f * (1f - ease));
            yield return null;
        }

        if (rootGO != null) Object.Destroy(rootGO);
        _isShowing = false;
    }

    // ── Pop particles ──

    private static void SpawnPopParticles(Transform parent, Vector2 center, Color balloonColor, Sprite circleSprite)
    {
        Color lighter = Color.Lerp(balloonColor, Color.white, 0.4f);
        Color darker = Color.Lerp(balloonColor, Color.black, 0.15f);

        for (int i = 0; i < 14; i++)
        {
            var go = new GameObject("PopParticle");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            float size = Random.Range(8f, 22f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = center + Random.insideUnitCircle * 30f;

            var img = go.AddComponent<Image>();
            if (circleSprite != null) img.sprite = circleSprite;
            // Mix of balloon color shards
            img.color = i % 3 == 0 ? lighter : i % 3 == 1 ? balloonColor : darker;
            img.raycastTarget = false;

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            var mono = go.AddComponent<SparkleParticle>();
            mono.velocity = dir * Random.Range(300f, 600f);
            mono.lifetime = Random.Range(0.4f, 0.7f);
        }
    }

    // ── Helpers ──

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}

/// <summary>Simple self-destroying particle.</summary>
public class SparkleParticle : MonoBehaviour
{
    public Vector2 velocity;
    public float lifetime;
    private float _elapsed;
    private RectTransform _rt;
    private Image _img;

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _img = GetComponent<Image>();
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        if (_elapsed >= lifetime) { Destroy(gameObject); return; }

        float p = _elapsed / lifetime;
        _rt.anchoredPosition += velocity * Time.deltaTime;
        velocity *= 0.92f;
        velocity.y -= 400f * Time.deltaTime; // gravity
        _rt.localScale = Vector3.one * (1f - p * 0.7f);
        var c = _img.color;
        _img.color = new Color(c.r, c.g, c.b, c.a * (1f - p));
    }
}
