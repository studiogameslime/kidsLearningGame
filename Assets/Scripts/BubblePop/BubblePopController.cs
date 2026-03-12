using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bubble Pop game. Colored circle bubbles with shine effect float upward.
/// A target color is shown — tap bubbles matching that color.
/// Every 10 seconds the target color changes.
/// Pop 5 correct bubbles to auto-restart a new round.
/// Popping spawns a particle burst.
/// </summary>
public class BubblePopController : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform playArea;
    public Image targetColorImage;
    public RectTransform bubbleContainer;

    [Header("Settings")]
    public float bubbleSize = 160f;
    public float spawnInterval = 1.2f;
    public float floatSpeed = 120f;
    public int correctNeeded = 5;

    [Header("Sprites")]
    public Sprite circleSprite;

    private Canvas canvas;
    private List<Bubble> activeBubbles = new List<Bubble>();
    private Color targetColor;
    private float targetChangeTimer = 10f;
    private float targetChangeElapsed;
    private int correctPopped;
    private bool isGameActive;
    private Coroutine spawnRoutine;

    private static readonly Color[] BubbleColors = {
        new Color(0.53f, 0.81f, 0.92f, 0.7f), // light blue
        new Color(0.56f, 0.93f, 0.56f, 0.7f), // light green
        new Color(1f, 0.71f, 0.76f, 0.7f),     // light pink
        new Color(0.93f, 0.86f, 0.51f, 0.7f),  // light yellow
        new Color(0.8f, 0.6f, 0.93f, 0.7f),    // light purple
    };

    private void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        StartNewRound();
    }

    private void StartNewRound()
    {
        ClearBubbles();
        correctPopped = 0;
        isGameActive = true;
        PickNewTargetColor();
        spawnRoutine = StartCoroutine(SpawnBubbles());
    }

    private void PickNewTargetColor()
    {
        Color previousColor = targetColor;
        int attempts = 0;
        do
        {
            targetColor = BubbleColors[Random.Range(0, BubbleColors.Length)];
            attempts++;
        } while (ColorsApproxEqual(targetColor, previousColor) && attempts < 20);

        targetChangeElapsed = 0f;

        if (targetColorImage != null)
        {
            targetColorImage.color = targetColor;
        }
    }

    private void ClearBubbles()
    {
        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        foreach (var b in activeBubbles)
            if (b != null) Destroy(b.gameObject);
        activeBubbles.Clear();
    }

    private IEnumerator SpawnBubbles()
    {
        while (isGameActive)
        {
            SpawnOneBubble();
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnOneBubble()
    {
        float areaW = playArea.rect.width;

        bool useTargetColor = Random.value < 0.4f;
        Color chosenColor = useTargetColor ? targetColor : BubbleColors[Random.Range(0, BubbleColors.Length)];

        // Vary bubble sizes slightly for a natural look
        float sizeVariation = bubbleSize * Random.Range(0.85f, 1.15f);

        var bubbleGO = new GameObject("Bubble");
        bubbleGO.transform.SetParent(bubbleContainer, false);

        var rt = bubbleGO.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(sizeVariation, sizeVariation);
        float x = Random.Range(-areaW / 2f + sizeVariation, areaW / 2f - sizeVariation);
        float startY = -playArea.rect.height / 2f - sizeVariation;
        rt.anchoredPosition = new Vector2(x, startY);

        // Circle image for bubble shape
        var bgImg = bubbleGO.AddComponent<Image>();
        bgImg.color = chosenColor;
        if (circleSprite != null)
            bgImg.sprite = circleSprite;
        bgImg.raycastTarget = true;

        // Shine/highlight effect (small white circle, top-left area)
        var shineGO = new GameObject("Shine");
        shineGO.transform.SetParent(bubbleGO.transform, false);
        var shineRT = shineGO.AddComponent<RectTransform>();
        shineRT.anchorMin = new Vector2(0.15f, 0.55f);
        shineRT.anchorMax = new Vector2(0.45f, 0.85f);
        shineRT.offsetMin = Vector2.zero;
        shineRT.offsetMax = Vector2.zero;
        var shineImg = shineGO.AddComponent<Image>();
        if (circleSprite != null)
            shineImg.sprite = circleSprite;
        shineImg.color = new Color(1f, 1f, 1f, 0.4f);
        shineImg.raycastTarget = false;

        // Secondary smaller shine dot
        var shineDotGO = new GameObject("ShineDot");
        shineDotGO.transform.SetParent(bubbleGO.transform, false);
        var shineDotRT = shineDotGO.AddComponent<RectTransform>();
        shineDotRT.anchorMin = new Vector2(0.25f, 0.45f);
        shineDotRT.anchorMax = new Vector2(0.35f, 0.55f);
        shineDotRT.offsetMin = Vector2.zero;
        shineDotRT.offsetMax = Vector2.zero;
        var shineDotImg = shineDotGO.AddComponent<Image>();
        if (circleSprite != null)
            shineDotImg.sprite = circleSprite;
        shineDotImg.color = new Color(1f, 1f, 1f, 0.6f);
        shineDotImg.raycastTarget = false;

        // Rim/outline for bubble edge look (slightly larger, semi-transparent)
        var rimGO = new GameObject("Rim");
        rimGO.transform.SetParent(bubbleGO.transform, false);
        rimGO.transform.SetAsFirstSibling(); // behind everything
        var rimRT = rimGO.AddComponent<RectTransform>();
        rimRT.anchorMin = new Vector2(-0.03f, -0.03f);
        rimRT.anchorMax = new Vector2(1.03f, 1.03f);
        rimRT.offsetMin = Vector2.zero;
        rimRT.offsetMax = Vector2.zero;
        var rimImg = rimGO.AddComponent<Image>();
        if (circleSprite != null)
            rimImg.sprite = circleSprite;
        rimImg.color = new Color(chosenColor.r * 0.7f, chosenColor.g * 0.7f, chosenColor.b * 0.7f, 0.3f);
        rimImg.raycastTarget = false;

        // Bubble component
        var bubble = bubbleGO.AddComponent<Bubble>();
        bubble.Init(chosenColor, floatSpeed, this);

        // Button for tap
        var btn = bubbleGO.AddComponent<Button>();
        btn.targetGraphic = bgImg;
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => OnBubbleTapped(bubble));

        activeBubbles.Add(bubble);
    }

    private void OnBubbleTapped(Bubble bubble)
    {
        if (!isGameActive || bubble == null) return;

        if (ColorsApproxEqual(bubble.bubbleColor, targetColor))
        {
            correctPopped++;
            SpawnPopParticles(bubble.transform.position, bubble.bubbleColor);
            activeBubbles.Remove(bubble);
            Destroy(bubble.gameObject);

            if (correctPopped >= correctNeeded)
            {
                isGameActive = false;
                StartCoroutine(AutoRestart());
            }
        }
        else
        {
            StartCoroutine(ShakeAnimation(bubble));
        }
    }

    private void SpawnPopParticles(Vector3 worldPos, Color color)
    {
        // Convert world position to canvas-local position
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            bubbleContainer, RectTransformUtility.WorldToScreenPoint(null, worldPos),
            null, out localPos);

        Color solidColor = new Color(color.r, color.g, color.b, 1f);
        Color lighterColor = Color.Lerp(solidColor, Color.white, 0.4f);
        Color darkerColor = Color.Lerp(solidColor, Color.black, 0.2f);

        int shardCount = Random.Range(10, 15);
        int sparkleCount = Random.Range(6, 10);

        // Shards: chunky pieces that fly out and fall with gravity
        for (int i = 0; i < shardCount; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(400f, 800f);
            float size = Random.Range(14f, 30f);
            float lifetime = Random.Range(0.5f, 0.85f);
            Color shardColor = Color.Lerp(solidColor, lighterColor, Random.Range(0f, 0.5f));
            if (Random.value < 0.3f) shardColor = darkerColor;

            var shard = CreateUIShard(localPos, size, shardColor);
            StartCoroutine(AnimateShard(shard, localPos, angle, speed, lifetime, true));
        }

        // Sparkles: small white dots that scatter fast and fade
        for (int i = 0; i < sparkleCount; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(250f, 550f);
            float size = Random.Range(6f, 12f);
            float lifetime = Random.Range(0.25f, 0.5f);
            Color sparkColor = Color.Lerp(Color.white, lighterColor, Random.Range(0f, 0.3f));

            var sparkle = CreateUIShard(localPos, size, sparkColor);
            StartCoroutine(AnimateShard(sparkle, localPos, angle, speed, lifetime, false));
        }
    }

    private RectTransform CreateUIShard(Vector2 pos, float size, Color color)
    {
        var go = new GameObject("Shard");
        go.transform.SetParent(bubbleContainer, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);
        rt.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

        var img = go.AddComponent<Image>();
        img.color = color;
        if (circleSprite != null) img.sprite = circleSprite;
        img.raycastTarget = false;

        return rt;
    }

    private IEnumerator AnimateShard(RectTransform rt, Vector2 startPos, float angle,
        float speed, float lifetime, bool useGravity)
    {
        Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
        float spinSpeed = Random.Range(-720f, 720f);
        float gravity = useGravity ? 1200f : 400f;
        float t = 0f;
        Image img = rt.GetComponent<Image>();
        Color startColor = img.color;
        Vector3 startScale = rt.localScale;

        while (t < lifetime)
        {
            t += Time.deltaTime;
            float progress = t / lifetime;

            // Move with velocity + gravity
            velocity.y -= gravity * Time.deltaTime;
            startPos += velocity * Time.deltaTime;
            rt.anchoredPosition = startPos;

            // Spin
            rt.Rotate(0, 0, spinSpeed * Time.deltaTime);

            // Shrink and fade
            float fade = 1f - progress * progress; // ease out
            rt.localScale = startScale * Mathf.Lerp(1f, 0.2f, progress);
            img.color = new Color(startColor.r, startColor.g, startColor.b, startColor.a * fade);

            yield return null;
        }

        Destroy(rt.gameObject);
    }

    private static bool ColorsApproxEqual(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.05f &&
               Mathf.Abs(a.g - b.g) < 0.05f &&
               Mathf.Abs(a.b - b.b) < 0.05f;
    }

    private IEnumerator ShakeAnimation(Bubble bubble)
    {
        if (bubble == null) yield break;
        var rt = bubble.GetComponent<RectTransform>();
        Vector2 orig = rt.anchoredPosition;
        float dur = 0.3f;
        float t = 0f;
        float amp = 15f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float offset = Mathf.Sin(t * 40f) * amp * (1f - t / dur);
            rt.anchoredPosition = orig + new Vector2(offset, 0);
            yield return null;
        }
        rt.anchoredPosition = orig;
    }

    private IEnumerator AutoRestart()
    {
        ConfettiController.Instance.Play();
        yield return new WaitForSeconds(0.5f);
        ClearBubbles();
        StartNewRound();
    }

    private void Update()
    {
        if (!isGameActive) return;

        targetChangeElapsed += Time.deltaTime;
        if (targetChangeElapsed >= targetChangeTimer)
        {
            PickNewTargetColor();
        }

        float topY = playArea.rect.height / 2f + bubbleSize * 2f;
        for (int i = activeBubbles.Count - 1; i >= 0; i--)
        {
            if (activeBubbles[i] == null)
            {
                activeBubbles.RemoveAt(i);
                continue;
            }
            var rt = activeBubbles[i].GetComponent<RectTransform>();
            if (rt.anchoredPosition.y > topY)
            {
                Destroy(activeBubbles[i].gameObject);
                activeBubbles.RemoveAt(i);
            }
        }
    }

    public void OnHomePressed() => NavigationManager.GoToMainMenu();
    public void OnRestartPressed() => StartNewRound();
}
