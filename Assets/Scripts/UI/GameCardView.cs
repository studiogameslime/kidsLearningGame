using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.Events;

/// <summary>
/// View component for a single card in the menu grid.
/// Playful, child-friendly feel: slight rotation, soft shadow, tap bounce, idle breathing.
/// New-in-app games get an animated sticker badge.
/// </summary>
public class GameCardView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("References (set in prefab)")]
    public Image backgroundImage;
    public Image thumbnailImage;
    public TextMeshProUGUI titleText;
    public Button button;

    [Header("Placeholder styling")]
    [Tooltip("Icon shown when no thumbnail is assigned.")]
    public Image placeholderIcon;

    // Runtime-created labels
    private TextMeshProUGUI _gameNameLabel;
    private TextMeshProUGUI _difficultyLabel;

    // Playful state
    private RectTransform _cardRT;
    private RectTransform _badgeRT;
    private bool _isPressed;
    private Coroutine _tapCoroutine;
    private float _idleRotation;

    /// <summary>
    /// Configures the card for display. Called by the menu controller.
    /// </summary>
    public void Setup(string title, Sprite thumbnail, Color cardColor, UnityAction onClick)
    {
        if (titleText != null)
            HebrewText.SetText(titleText, title);

        if (thumbnail != null)
        {
            if (thumbnailImage != null)
            {
                thumbnailImage.sprite = thumbnail;
                thumbnailImage.gameObject.SetActive(true);
            }
            if (placeholderIcon != null)
                placeholderIcon.gameObject.SetActive(false);
        }
        else
        {
            if (thumbnailImage != null)
                thumbnailImage.gameObject.SetActive(false);
            if (placeholderIcon != null)
                placeholderIcon.gameObject.SetActive(true);
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
        }
    }

    /// <summary>
    /// Extended setup for main menu — playful card with rotation, shadow, tap feedback.
    /// </summary>
    public void SetupExtended(string gameId, string hebrewName, Color profileColor, int difficulty, bool isNew = false)
    {
        _cardRT = GetComponent<RectTransform>();

        // ── 1. Playful rotation (slight random tilt) ──
        _idleRotation = Random.Range(-2.5f, 2.5f);
        _cardRT.localRotation = Quaternion.Euler(0, 0, _idleRotation);

        // ── 2. Soft drop shadow ──
        if (backgroundImage != null && backgroundImage.GetComponent<Shadow>() == null)
        {
            var shadow = backgroundImage.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.15f, 0.1f, 0.05f, 0.3f);
            shadow.effectDistance = new Vector2(3, -4);
        }

        // ── 3. New game sticker ──
        if (isNew)
            SetupNewGameHighlight();

        // ── 4. Profile color tint (soft pastel on bubble) ──
        if (backgroundImage != null)
        {
            Color tint = Color.Lerp(Color.white, profileColor, 0.15f);
            tint.a = 0.93f;
            backgroundImage.color = tint;
        }

        // ── 5. 2-player badge ──
        if (TwoPlayerManager.SupportsMultiplayer(gameId) && transform.Find("2PBadge") == null)
        {
            var badgeBgGO = new GameObject("2PBadgeBg");
            badgeBgGO.transform.SetParent(transform, false);
            badgeBgGO.transform.SetAsLastSibling();
            var bgRT = badgeBgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(1, 0);
            bgRT.anchorMax = new Vector2(1, 0);
            bgRT.pivot = new Vector2(1, 0);
            bgRT.anchoredPosition = Vector2.zero;
            bgRT.sizeDelta = new Vector2(120, 120);

            var bgImg = badgeBgGO.AddComponent<Image>();
            var roundedRect = Resources.Load<Sprite>("UI/RoundedRect");
            if (roundedRect != null) { bgImg.sprite = roundedRect; bgImg.type = Image.Type.Sliced; }
            Color borderTint = backgroundImage != null ? backgroundImage.color : profileColor;
            bgImg.color = borderTint;
            bgImg.raycastTarget = false;

            var badgeGO = new GameObject("2PBadge");
            badgeGO.transform.SetParent(badgeBgGO.transform, false);
            var badgeRT = badgeGO.AddComponent<RectTransform>();
            badgeRT.anchorMin = new Vector2(0.5f, 0.5f);
            badgeRT.anchorMax = new Vector2(0.5f, 0.5f);
            badgeRT.pivot = new Vector2(0.5f, 0.5f);
            badgeRT.anchoredPosition = Vector2.zero;
            badgeRT.sizeDelta = new Vector2(130, 130);

            var badgeImg = badgeGO.AddComponent<Image>();
            var twoPlayerSprite = Resources.Load<Sprite>("2 Player");
            if (twoPlayerSprite != null)
            {
                badgeImg.sprite = twoPlayerSprite;
                badgeImg.preserveAspect = true;
            }
            badgeImg.raycastTarget = false;
        }

        // ── 6. Game name ABOVE the thumbnail ──
        if (_gameNameLabel == null)
        {
            var nameGO = new GameObject("GameNameTop");
            nameGO.transform.SetParent(transform, false);
            nameGO.transform.SetAsLastSibling();
            var nameRT = nameGO.AddComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 1);
            nameRT.anchorMax = new Vector2(1, 1);
            nameRT.pivot = new Vector2(0.5f, 1);
            nameRT.anchoredPosition = new Vector2(0, -4);
            nameRT.sizeDelta = new Vector2(-20, 36);

            _gameNameLabel = nameGO.AddComponent<TextMeshProUGUI>();
            _gameNameLabel.fontSize = 24;
            _gameNameLabel.fontStyle = FontStyles.Bold;
            _gameNameLabel.color = new Color(0.25f, 0.25f, 0.3f);
            _gameNameLabel.alignment = TextAlignmentOptions.Center;
            _gameNameLabel.raycastTarget = false;
            _gameNameLabel.enableWordWrapping = false;
            _gameNameLabel.overflowMode = TextOverflowModes.Ellipsis;
        }

        if (_gameNameLabel != null)
            HebrewText.SetText(_gameNameLabel, hebrewName);

        // ── 7. Difficulty label BELOW the thumbnail ──
        if (_difficultyLabel == null)
        {
            var diffGO = new GameObject("DifficultyBottom");
            diffGO.transform.SetParent(transform, false);
            diffGO.transform.SetAsLastSibling();
            var diffRT = diffGO.AddComponent<RectTransform>();
            diffRT.anchorMin = new Vector2(0, 0);
            diffRT.anchorMax = new Vector2(1, 0);
            diffRT.pivot = new Vector2(0.5f, 0);
            diffRT.anchoredPosition = new Vector2(0, 4);
            diffRT.sizeDelta = new Vector2(0, 28);

            _difficultyLabel = diffGO.AddComponent<TextMeshProUGUI>();
            _difficultyLabel.fontSize = 20;
            _difficultyLabel.color = new Color(0.4f, 0.4f, 0.4f);
            _difficultyLabel.alignment = TextAlignmentOptions.Center;
            _difficultyLabel.raycastTarget = false;
        }

        if (_difficultyLabel != null)
        {
            string diffName;
            if (difficulty <= 3) diffName = "\u05E7\u05DC";
            else if (difficulty <= 7) diffName = "\u05D1\u05D9\u05E0\u05D5\u05E0\u05D9";
            else diffName = "\u05E7\u05E9\u05D4";

            HebrewText.SetText(_difficultyLabel, "\u05E8\u05DE\u05D4: " + diffName);
        }

        // ── 8. Start idle breathing animation ──
        StartCoroutine(IdleBreathing());
    }

    // ══════════════════════════════════════════════
    //  TAP FEEDBACK — scale bounce
    // ══════════════════════════════════════════════

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_cardRT == null) return;
        _isPressed = true;
        if (_tapCoroutine != null) StopCoroutine(_tapCoroutine);
        _tapCoroutine = StartCoroutine(AnimateScale(1.05f, 0.08f));
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_cardRT == null) return;
        _isPressed = false;
        if (_tapCoroutine != null) StopCoroutine(_tapCoroutine);
        _tapCoroutine = StartCoroutine(AnimateBounceBack());
    }

    private IEnumerator AnimateScale(float target, float duration)
    {
        float start = _cardRT.localScale.x;
        float elapsed = 0f;
        while (elapsed < duration && _cardRT != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / duration);
            float s = Mathf.Lerp(start, target, t);
            _cardRT.localScale = Vector3.one * s;
            yield return null;
        }
        if (_cardRT != null) _cardRT.localScale = Vector3.one * target;
    }

    private IEnumerator AnimateBounceBack()
    {
        // Overshoot to 0.97 then settle at 1.0
        float start = _cardRT != null ? _cardRT.localScale.x : 1f;

        // Phase 1: shrink to 0.97
        float elapsed = 0f, dur = 0.08f;
        while (elapsed < dur && _cardRT != null)
        {
            elapsed += Time.deltaTime;
            float s = Mathf.Lerp(start, 0.97f, elapsed / dur);
            _cardRT.localScale = Vector3.one * s;
            yield return null;
        }

        // Phase 2: bounce back to 1.0
        elapsed = 0f; dur = 0.12f;
        while (elapsed < dur && _cardRT != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / dur);
            float s = Mathf.Lerp(0.97f, 1f, t);
            _cardRT.localScale = Vector3.one * s;
            yield return null;
        }
        if (_cardRT != null) _cardRT.localScale = Vector3.one;
    }

    // ══════════════════════════════════════════════
    //  IDLE BREATHING — very subtle scale pulse
    // ══════════════════════════════════════════════

    private IEnumerator IdleBreathing()
    {
        // Stagger start so cards don't breathe in sync
        yield return new WaitForSeconds(Random.Range(0f, 2f));

        float speed = Random.Range(0.6f, 0.9f); // each card has slightly different rhythm

        while (_cardRT != null)
        {
            if (!_isPressed)
            {
                float pulse = 1f + Mathf.Sin(Time.time * speed) * 0.012f; // 1.0 → 1.012 → 1.0
                _cardRT.localScale = Vector3.one * pulse;
            }
            yield return null;
        }
    }

    // ══════════════════════════════════════════════
    //  NEW GAME — Bold blob sticker
    // ══════════════════════════════════════════════

    private void SetupNewGameHighlight()
    {
        if (transform.Find("NewSticker") != null) return;

        var circleSprite = Resources.Load<Sprite>("Circle");
        var roundedRect = Resources.Load<Sprite>("UI/RoundedRect");

        // ── Blob sticker (top-left corner, overlaps frame, tilted) ──
        var stickerGO = new GameObject("NewSticker");
        stickerGO.transform.SetParent(transform, false);
        stickerGO.transform.SetAsLastSibling();
        _badgeRT = stickerGO.AddComponent<RectTransform>();
        _badgeRT.anchorMin = new Vector2(0, 1);
        _badgeRT.anchorMax = new Vector2(0, 1);
        _badgeRT.pivot = new Vector2(0.5f, 0.5f);
        _badgeRT.anchoredPosition = new Vector2(30, -10);
        _badgeRT.sizeDelta = new Vector2(100, 42);
        _badgeRT.localRotation = Quaternion.Euler(0, 0, 8f);

        // Main background — hot orange
        var bgImg = stickerGO.AddComponent<Image>();
        if (roundedRect != null) { bgImg.sprite = roundedRect; bgImg.type = Image.Type.Sliced; }
        bgImg.color = new Color(1f, 0.45f, 0.15f, 1f);
        bgImg.raycastTarget = false;

        // White outline
        var outline = stickerGO.AddComponent<Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(3, -3);

        // Drop shadow
        var shadow = stickerGO.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.3f);
        shadow.effectDistance = new Vector2(2, -3);

        // "!חדש" text
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(stickerGO.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = new Vector2(4, 2);
        labelRT.offsetMax = new Vector2(-4, -2);
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(labelTMP, "\u05D7\u05D3\u05E9!"); // !חדש
        labelTMP.fontSize = 28;
        labelTMP.fontStyle = FontStyles.Bold;
        labelTMP.color = Color.white;
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.raycastTarget = false;

        // 2 sparkle dots
        if (circleSprite != null)
        {
            float[][] positions = { new float[] { -50f, 18f }, new float[] { 48f, -16f } };
            for (int i = 0; i < positions.Length; i++)
            {
                var sparkle = new GameObject($"Sparkle_{i}");
                sparkle.transform.SetParent(stickerGO.transform, false);
                var sRT = sparkle.AddComponent<RectTransform>();
                sRT.anchorMin = new Vector2(0.5f, 0.5f);
                sRT.anchorMax = new Vector2(0.5f, 0.5f);
                sRT.sizeDelta = new Vector2(12, 12);
                sRT.anchoredPosition = new Vector2(positions[i][0], positions[i][1]);
                var sImg = sparkle.AddComponent<Image>();
                sImg.sprite = circleSprite;
                sImg.color = new Color(1f, 1f, 0.7f, 0f);
                sImg.raycastTarget = false;
                StartCoroutine(AnimateSparkle(sRT, sImg, i * 1.2f));
            }
        }

        StartCoroutine(AnimateStickerPulse());
    }

    // ── Sticker pulse: 1 → 1.08 → 1 with wiggle ──
    private IEnumerator AnimateStickerPulse()
    {
        float baseRot = 8f;
        while (_badgeRT != null)
        {
            float dur = 0.5f, elapsed = 0f;
            while (elapsed < dur && _badgeRT != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                _badgeRT.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(t * Mathf.PI));
                _badgeRT.localRotation = Quaternion.Euler(0, 0, baseRot + Mathf.Sin(t * Mathf.PI * 2f) * 2f);
                yield return null;
            }
            if (_badgeRT != null)
            {
                _badgeRT.localScale = Vector3.one;
                _badgeRT.localRotation = Quaternion.Euler(0, 0, baseRot);
            }
            yield return new WaitForSeconds(2f);
        }
    }

    // ── Sparkle twinkle ──
    private IEnumerator AnimateSparkle(RectTransform rt, Image img, float delay)
    {
        yield return new WaitForSeconds(delay);
        while (rt != null && img != null)
        {
            float dur = 0.3f, elapsed = 0f;
            while (elapsed < dur && img != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                img.color = new Color(1f, 1f, 0.7f, t);
                rt.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, t);
                yield return null;
            }
            yield return new WaitForSeconds(0.2f);
            elapsed = 0f;
            while (elapsed < dur && img != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                img.color = new Color(1f, 1f, 0.7f, 1f - t);
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, t);
                yield return null;
            }
            yield return new WaitForSeconds(Random.Range(2f, 4f));
        }
    }
}
