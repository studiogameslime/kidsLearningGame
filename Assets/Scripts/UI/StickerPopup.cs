using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Balloon sticker popup. Balloon rises from bottom, child taps to pop.
/// Sticker is added to collection ONLY when balloon is popped.
/// If not popped (timeout/scene change), sticker is lost.
/// </summary>
public static class StickerPopup
{
    private static bool _isShowing;

    /// <summary>
    /// Optional callback when balloon is popped — used to add sticker to collection.
    /// </summary>
    public static System.Action<string> OnStickerCollected;

    public static IEnumerator Show(string stickerId)
    {
        if (_isShowing) yield break;

        Sprite sprite = StickerSpriteBank.GetSprite(stickerId);

        // Achievement stickers use game thumbnail
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

        // Find the ROOT canvas (not a nested sub-canvas)
        Canvas canvas = null;
        foreach (var c in Object.FindObjectsOfType<Canvas>())
        {
            if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
            { canvas = c; break; }
        }
        if (canvas == null)
        {
            // Fallback: any root canvas
            foreach (var c in Object.FindObjectsOfType<Canvas>())
            {
                if (c.isRootCanvas) { canvas = c; break; }
            }
        }
        if (canvas == null) yield break;

        var circleSprite = Resources.Load<Sprite>("Circle");
        if (circleSprite == null)
        {
            Debug.LogWarning("[StickerPopup] Circle sprite not found!");
            yield break;
        }

        _isShowing = true;

        Color balloonColor = new Color(0.56f, 0.79f, 0.98f);
        var profile = ProfileManager.ActiveProfile;
        if (profile != null) balloonColor = profile.AvatarColor;

        // ── Root overlay — IS the dim background ──
        var rootGO = new GameObject("StickerPopup");
        rootGO.transform.SetParent(canvas.transform, false);
        rootGO.transform.SetAsLastSibling();
        var rootRT = rootGO.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero; rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero; rootRT.offsetMax = Vector2.zero;

        // Root itself is the dim — catches all background taps
        var dimImg = rootGO.AddComponent<Image>();
        dimImg.color = new Color(0, 0, 0, 0);
        dimImg.raycastTarget = true;

        // Ensure this canvas has a GraphicRaycaster (needed for button clicks)
        if (canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // ── Balloon container ──
        var balloonContainer = new GameObject("BalloonContainer");
        balloonContainer.transform.SetParent(rootGO.transform, false);
        var balloonContainerRT = balloonContainer.AddComponent<RectTransform>();
        balloonContainerRT.anchorMin = new Vector2(0.5f, 0.5f);
        balloonContainerRT.anchorMax = new Vector2(0.5f, 0.5f);
        balloonContainerRT.sizeDelta = new Vector2(320, 420);

        var canvasRT = canvas.GetComponent<RectTransform>();
        float canvasH = canvasRT != null && canvasRT.rect.height > 0 ? canvasRT.rect.height : 1080f;
        float startY = -(canvasH / 2f + 250f); // below bottom edge of screen
        balloonContainerRT.anchoredPosition = new Vector2(0, startY);

        // ── String ──
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

        // ── Balloon body ──
        var balloonGO = new GameObject("Balloon");
        balloonGO.transform.SetParent(balloonContainer.transform, false);
        var balloonRT = balloonGO.AddComponent<RectTransform>();
        balloonRT.anchorMin = new Vector2(0.5f, 0.5f);
        balloonRT.anchorMax = new Vector2(0.5f, 0.5f);
        balloonRT.anchoredPosition = new Vector2(0, 30);
        balloonRT.sizeDelta = new Vector2(280, 280);
        var balloonImg = balloonGO.AddComponent<Image>();
        balloonImg.sprite = circleSprite;
        balloonImg.color = new Color(balloonColor.r, balloonColor.g, balloonColor.b, 0.35f);
        balloonImg.raycastTarget = false; // tap handled by separate TapArea overlay

        // Shine
        var shineGO = new GameObject("Shine");
        shineGO.transform.SetParent(balloonGO.transform, false);
        var shineRT = shineGO.AddComponent<RectTransform>();
        shineRT.anchorMin = new Vector2(0.15f, 0.55f);
        shineRT.anchorMax = new Vector2(0.4f, 0.8f);
        shineRT.offsetMin = Vector2.zero; shineRT.offsetMax = Vector2.zero;
        var shineImg = shineGO.AddComponent<Image>();
        shineImg.sprite = circleSprite;
        shineImg.color = new Color(1f, 1f, 1f, 0.3f);
        shineImg.raycastTarget = false;

        // ── Sticker inside balloon (circle mask) ──
        var stickerMaskGO = new GameObject("StickerMask");
        stickerMaskGO.transform.SetParent(balloonGO.transform, false);
        var stickerMaskRT = stickerMaskGO.AddComponent<RectTransform>();
        stickerMaskRT.anchorMin = new Vector2(0.1f, 0.1f);
        stickerMaskRT.anchorMax = new Vector2(0.9f, 0.9f);
        stickerMaskRT.offsetMin = Vector2.zero; stickerMaskRT.offsetMax = Vector2.zero;
        var maskImg = stickerMaskGO.AddComponent<Image>();
        maskImg.sprite = circleSprite;
        maskImg.color = new Color(1, 1, 1, 0);
        maskImg.raycastTarget = false;
        stickerMaskGO.AddComponent<Mask>().showMaskGraphic = false;

        var stickerGO = new GameObject("Sticker");
        stickerGO.transform.SetParent(stickerMaskGO.transform, false);
        var stickerRT = stickerGO.AddComponent<RectTransform>();
        stickerRT.anchorMin = new Vector2(0.05f, 0.05f);
        stickerRT.anchorMax = new Vector2(0.95f, 0.95f);
        stickerRT.offsetMin = Vector2.zero; stickerRT.offsetMax = Vector2.zero;
        var stickerImg = stickerGO.AddComponent<Image>();
        stickerImg.sprite = sprite;
        stickerImg.preserveAspect = true;
        stickerImg.raycastTarget = false;

        // ── Tap handler — separate invisible overlay on top of balloon ──
        // (Mask component on children blocks raycasts, so button must be a separate GO)
        bool popped = false;
        var tapGO = new GameObject("TapArea");
        tapGO.transform.SetParent(balloonContainer.transform, false);
        tapGO.transform.SetAsLastSibling(); // on top of everything
        var tapRT = tapGO.AddComponent<RectTransform>();
        tapRT.anchorMin = new Vector2(0.5f, 0.5f);
        tapRT.anchorMax = new Vector2(0.5f, 0.5f);
        tapRT.anchoredPosition = new Vector2(0, 30); // same as balloon
        tapRT.sizeDelta = new Vector2(300, 300); // slightly larger hit area
        var tapImg = tapGO.AddComponent<Image>();
        tapImg.color = new Color(0, 0, 0, 0); // fully transparent
        tapImg.raycastTarget = true;
        var btn = tapGO.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = tapImg;
        btn.interactable = false; // disabled until rise completes
        btn.onClick.AddListener(() => popped = true);

        // ══════════════════════════
        //  ANIMATION
        // ══════════════════════════

        // ── Phase 1: Dim + rise (1.5s — slow gentle rise) ──
        Vector2 startPos = balloonContainerRT.anchoredPosition;
        Vector2 endPos = new Vector2(0, 20);
        float t = 0f;
        float riseDur = 1.5f;
        while (t < riseDur)
        {
            if (rootGO == null) { _isShowing = false; yield break; }
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / riseDur);
            float ease = 1f - Mathf.Pow(1f - p, 3f);
            balloonContainerRT.anchoredPosition = Vector2.Lerp(startPos, endPos, ease);
            // Slight wobble during rise
            float wobble = Mathf.Sin(p * Mathf.PI * 4f) * 3f * (1f - p);
            balloonContainerRT.anchoredPosition += new Vector2(wobble, 0);
            dimImg.color = new Color(0, 0, 0, p * 0.6f);
            yield return null;
        }
        if (rootGO == null) { _isShowing = false; yield break; }
        balloonContainerRT.anchoredPosition = endPos;

        // Enable tap
        btn.interactable = true;

        // ── Phase 2: Float + wait for tap (20s timeout) ──
        float floatTime = 0f;
        while (!popped)
        {
            if (rootGO == null) { _isShowing = false; yield break; }
            floatTime += Time.deltaTime;
            if (floatTime > 20f) { popped = true; break; }
            float bobY = Mathf.Sin(floatTime * 1.5f) * 8f;
            float bobX = Mathf.Sin(floatTime * 0.7f) * 4f;
            balloonContainerRT.anchoredPosition = endPos + new Vector2(bobX, bobY);
            float breathe = 1f + Mathf.Sin(floatTime * 2f) * 0.015f;
            balloonRT.localScale = Vector3.one * breathe;
            yield return null;
        }

        // ── Phase 3: Pop! ──
        if (rootGO == null) { _isShowing = false; yield break; }

        SoundLibrary.PlayBubblePop();

        // Collect the sticker NOW
        OnStickerCollected?.Invoke(stickerId);

        // Hide balloon + tap area
        balloonGO.SetActive(false);
        tapGO.SetActive(false);
        stringGO.SetActive(false);

        // Free sticker from mask — reparent to balloonContainer
        stickerMaskGO.transform.SetParent(balloonContainer.transform, true);
        var mask = stickerMaskGO.GetComponent<Mask>();
        if (mask != null) Object.Destroy(mask);
        if (maskImg != null) maskImg.enabled = false;
        stickerMaskGO.transform.SetAsLastSibling();

        // Pop particles — spawn at balloon center position
        SpawnPopParticles(balloonContainer.transform, new Vector2(0, 30), balloonColor, circleSprite);

        // ── Phase 4: Sticker pulse (0.3s) ──
        t = 0f;
        while (t < 0.3f)
        {
            if (rootGO == null) { _isShowing = false; yield break; }
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / 0.3f);
            float scale = 1f + Mathf.Sin(p * Mathf.PI) * 0.3f;
            stickerMaskGO.transform.localScale = Vector3.one * scale;
            yield return null;
        }

        // ── Phase 5: Hold (1.0s) ──
        t = 0f;
        while (t < 1.0f)
        {
            if (rootGO == null) { _isShowing = false; yield break; }
            t += Time.deltaTime;
            yield return null;
        }

        // ── Phase 6: Shrink + fade (0.4s) ──
        t = 0f;
        while (t < 0.4f)
        {
            if (rootGO == null) { _isShowing = false; yield break; }
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / 0.4f);
            stickerMaskGO.transform.localScale = Vector3.one * (1f - p * p);
            dimImg.color = new Color(0, 0, 0, 0.6f * (1f - p));
            yield return null;
        }

        if (rootGO != null) Object.Destroy(rootGO);
        _isShowing = false;
    }

    private static void SpawnPopParticles(Transform parent, Vector2 center, Color balloonColor, Sprite circleSprite)
    {
        Color lighter = Color.Lerp(balloonColor, Color.white, 0.5f);
        Color darker = Color.Lerp(balloonColor, Color.black, 0.1f);

        // Large shards (balloon pieces)
        for (int i = 0; i < 18; i++)
        {
            var go = new GameObject("PopShard");
            go.transform.SetParent(parent, false);
            go.transform.SetAsLastSibling();
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            float size = Random.Range(12f, 35f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = center + Random.insideUnitCircle * 40f;

            var img = go.AddComponent<Image>();
            if (circleSprite != null) img.sprite = circleSprite;
            img.color = i % 3 == 0 ? lighter : i % 3 == 1 ? balloonColor : darker;
            img.raycastTarget = false;

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            var mono = go.AddComponent<SparkleParticle>();
            mono.velocity = dir * Random.Range(400f, 900f);
            mono.lifetime = Random.Range(0.5f, 0.9f);
        }

        // Small sparkles
        for (int i = 0; i < 10; i++)
        {
            var go = new GameObject("PopSparkle");
            go.transform.SetParent(parent, false);
            go.transform.SetAsLastSibling();
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(8, 8);
            rt.anchoredPosition = center;

            var img = go.AddComponent<Image>();
            if (circleSprite != null) img.sprite = circleSprite;
            img.color = Color.white;
            img.raycastTarget = false;

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            var mono = go.AddComponent<SparkleParticle>();
            mono.velocity = dir * Random.Range(200f, 500f);
            mono.lifetime = Random.Range(0.3f, 0.5f);
        }
    }
}

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
        velocity.y -= 400f * Time.deltaTime;
        _rt.localScale = Vector3.one * (1f - p * 0.7f);
        var c = _img.color;
        _img.color = new Color(c.r, c.g, c.b, c.a * (1f - p));
    }
}
