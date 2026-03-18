using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Flappy Bird mini-game controller. The bird flies forward continuously,
/// player taps to flap upward, obstacles scroll from right to left.
/// Uses UI-based rendering (RectTransform + Image) like all other mini-games.
/// </summary>
public class FlappyBirdController : MonoBehaviour
{
    [Header("Bird")]
    public RectTransform birdRT;
    public Image birdImage;
    public UISpriteAnimator birdAnimator;

    [Header("Play Area")]
    public RectTransform playArea;         // the main game area (below header)
    public float groundFraction = 0.16f;   // ground top as fraction of play area height

    [Header("Obstacles")]
    public Sprite pipeSprite;              // elementWood019
    public RectTransform obstacleContainer;

    [Header("Parallax")]
    public RectTransform[] parallaxLayers; // background layers (slowest to fastest)
    public float[] parallaxSpeeds;         // scroll speed multiplier per layer (0..1 of pipeSpeed)

    [Header("Settings")]
    public float gravity = -1200f;         // pixels/sec²  (gentle for kids)
    public float flapStrength = 420f;      // pixels/sec upward on tap
    public float pipeSpeed = 250f;         // pixels/sec leftward
    public float spawnInterval = 2.2f;     // seconds between pipe pairs
    public float gapSize = 520f;           // vertical gap between pipes (generous for 480px bird)
    public float pipeWidth = 160f;
    public float pipeMinY = 0.2f;          // gap center min (fraction of play area)
    public float pipeMaxY = 0.75f;         // gap center max

    // State
    private float velocity;
    private bool isPlaying;
    private bool isDead;
    private int score;
    private int bestScore;
    private float spawnTimer;
    private float playAreaHeight;
    private float playAreaWidth;
    private float groundY;                 // bird dies if below this

    private readonly List<PipePair> pipes = new List<PipePair>();
    private readonly List<PipePair> pipePool = new List<PipePair>();
    private float[] parallaxOffsets;
    private float smoothTilt;          // current visual tilt angle (smoothed)
    private GameStatsCollector _stats;

    private struct PipePair
    {
        public RectTransform top;
        public RectTransform bottom;
        public RectTransform root;
        public bool scored;
    }

    private void Start()
    {
        // Load best score
        var profile = ProfileManager.ActiveProfile;
        if (profile != null)
        {
            var stat = profile.progress.GetOrCreate("flappybird");
            bestScore = stat.bestScore;
        }

        playAreaHeight = playArea.rect.height;
        playAreaWidth = playArea.rect.width;
        groundY = playAreaHeight * groundFraction;

        // Init parallax offsets
        if (parallaxLayers != null)
            parallaxOffsets = new float[parallaxLayers.Length];

        // Start in floating state, waiting for first tap
        birdAnimator?.PlayFloating();
        ResetBird();
        isPlaying = false;
        isDead = false;
    }

    private void Update()
    {
        if (isDead) return;

        // Wait for first tap to start
        if (!isPlaying)
        {
            // Gentle hover while waiting
            float hover = Mathf.Sin(Time.time * 2.5f) * 15f;
            birdRT.anchoredPosition = new Vector2(birdRT.anchoredPosition.x,
                playAreaHeight * 0.5f + hover);

            if (Input.GetMouseButtonDown(0))
            {
                isPlaying = true;
                velocity = flapStrength;
                _stats = new GameStatsCollector("flappybird");
                if (GameCompletionBridge.Instance != null)
                    GameCompletionBridge.Instance.ActiveCollector = _stats;
            }
            return;
        }

        // Tap input
        if (Input.GetMouseButtonDown(0))
            Flap();

        // Physics
        velocity += gravity * Time.deltaTime;
        var pos = birdRT.anchoredPosition;
        pos.y += velocity * Time.deltaTime;

        // Ceiling clamp (half bird height from top)
        float ceilY = playAreaHeight - birdRT.sizeDelta.y * 0.3f;
        if (pos.y > ceilY)
        {
            pos.y = ceilY;
            velocity = 0;
        }

        birdRT.anchoredPosition = pos;

        // Smooth tilt: nose up when rising, nose down when falling
        // Negative Z because X scale is flipped (-1)
        float targetTilt = Mathf.Clamp(velocity / flapStrength, -1f, 1f) * -30f;
        smoothTilt = Mathf.Lerp(smoothTilt, targetTilt, Time.deltaTime * 8f);
        birdRT.localEulerAngles = new Vector3(0, 0, smoothTilt);

        // Ground collision
        if (pos.y <= groundY)
        {
            pos.y = groundY;
            birdRT.anchoredPosition = pos;
            Die();
            return;
        }

        // Spawn pipes
        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            SpawnPipe();
            spawnTimer = spawnInterval;
        }

        // Move and check pipes
        UpdatePipes();

        // Parallax removed — background stays static
    }

    private void Flap()
    {
        // Set upward velocity (don't add — set, so repeated taps feel consistent)
        velocity = flapStrength;
    }

    // ── PIPES ───────────────────────────────────────────────────

    private void SpawnPipe()
    {
        // Clamp gap so pipes stay within play area (below header, above ground)
        float minGapCenter = groundY + gapSize * 0.5f + 40f;
        float maxGapCenter = playAreaHeight - gapSize * 0.5f - 40f;
        float gapCenterY = Random.Range(
            Mathf.Max(minGapCenter, playAreaHeight * pipeMinY),
            Mathf.Min(maxGapCenter, playAreaHeight * pipeMaxY));

        PipePair pair;
        if (pipePool.Count > 0)
        {
            pair = pipePool[pipePool.Count - 1];
            pipePool.RemoveAt(pipePool.Count - 1);
            pair.root.gameObject.SetActive(true);
        }
        else
        {
            pair = CreatePipePair();
        }

        pair.scored = false;

        // Position at right edge
        float spawnX = playAreaWidth * 0.5f + pipeWidth;
        pair.root.anchoredPosition = new Vector2(spawnX, 0);

        // Top pipe: hangs down from ceiling to gap top
        float gapTop = gapCenterY + gapSize * 0.5f;
        float topPipeHeight = Mathf.Max(0, playAreaHeight - gapTop);
        pair.top.anchoredPosition = new Vector2(0, gapTop);
        pair.top.sizeDelta = new Vector2(pipeWidth, topPipeHeight);
        pair.top.pivot = new Vector2(0.5f, 0f);

        // Bottom pipe: from ground to gap bottom
        float bottomPipeHeight = Mathf.Max(0, gapCenterY - gapSize * 0.5f);
        pair.bottom.anchoredPosition = new Vector2(0, 0);
        pair.bottom.sizeDelta = new Vector2(pipeWidth, bottomPipeHeight);
        pair.bottom.pivot = new Vector2(0.5f, 0f);

        pipes.Add(pair);
    }

    private PipePair CreatePipePair()
    {
        var rootGO = new GameObject("PipePair");
        rootGO.transform.SetParent(obstacleContainer, false);
        var rootRT = rootGO.AddComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0.5f, 0f);
        rootRT.anchorMax = new Vector2(0.5f, 0f);
        rootRT.pivot = new Vector2(0.5f, 0f);

        // Top pipe (flipped vertically)
        var topGO = new GameObject("TopPipe");
        topGO.transform.SetParent(rootRT, false);
        var topRT = topGO.AddComponent<RectTransform>();
        topRT.anchorMin = new Vector2(0.5f, 0f);
        topRT.anchorMax = new Vector2(0.5f, 0f);
        var topImg = topGO.AddComponent<Image>();
        if (pipeSprite != null) topImg.sprite = pipeSprite;
        topImg.type = Image.Type.Simple;
        topImg.color = new Color(0.85f, 0.95f, 0.85f);
        topImg.raycastTarget = false;

        // Bottom pipe
        var botGO = new GameObject("BottomPipe");
        botGO.transform.SetParent(rootRT, false);
        var botRT = botGO.AddComponent<RectTransform>();
        botRT.anchorMin = new Vector2(0.5f, 0f);
        botRT.anchorMax = new Vector2(0.5f, 0f);
        var botImg = botGO.AddComponent<Image>();
        if (pipeSprite != null) botImg.sprite = pipeSprite;
        botImg.type = Image.Type.Simple;
        botImg.color = new Color(0.85f, 0.95f, 0.85f);
        botImg.raycastTarget = false;

        return new PipePair { root = rootRT, top = topRT, bottom = botRT, scored = false };
    }

    private void UpdatePipes()
    {
        float birdX = birdRT.anchoredPosition.x;
        float birdY = birdRT.anchoredPosition.y;
        // Fixed collision box — the bird sprite (480px) is much larger than the visible bird
        // Use a fixed pixel size for consistent collision regardless of sprite dimensions
        float birdW = 40f;
        float birdH = 40f;

        for (int i = pipes.Count - 1; i >= 0; i--)
        {
            var pair = pipes[i];

            // Move left
            var pos = pair.root.anchoredPosition;
            pos.x -= pipeSpeed * Time.deltaTime;
            pair.root.anchoredPosition = pos;

            // Off screen — recycle
            if (pos.x < -(playAreaWidth * 0.5f + pipeWidth))
            {
                pair.root.gameObject.SetActive(false);
                pipePool.Add(pair);
                pipes.RemoveAt(i);
                continue;
            }

            // Scoring: pipe center passed bird
            if (!pair.scored && pos.x < birdX - pipeWidth * 0.5f)
            {
                pair.scored = true;
                pipes[i] = pair;
                score++;
                _stats?.RecordCorrect();
            }

            // Collision check (AABB) — tight to visible pipe
            float pipeX = pos.x;
            float halfPipeW = pipeWidth * 0.3f; // generously inset from pipe edges for kids

            // Check horizontal overlap first
            if (Mathf.Abs(birdX - pipeX) < birdW + halfPipeW)
            {
                // Top pipe collision (pipe grows up from anchoredPosition.y)
                float topBottom = pair.top.anchoredPosition.y;
                if (birdY + birdH > topBottom)
                {
                    Die();
                    return;
                }

                // Bottom pipe collision (top edge of bottom pipe)
                float botTop = pair.bottom.sizeDelta.y;
                if (birdY - birdH < botTop)
                {
                    Die();
                    return;
                }
            }
        }
    }

    // ── PARALLAX ────────────────────────────────────────────────

    private void UpdateParallax()
    {
        if (parallaxLayers == null || parallaxSpeeds == null || parallaxOffsets == null) return;

        int count = Mathf.Min(parallaxLayers.Length, parallaxSpeeds.Length);
        for (int i = 0; i < count; i++)
        {
            if (parallaxLayers[i] == null) continue;

            parallaxOffsets[i] -= pipeSpeed * parallaxSpeeds[i] * Time.deltaTime;

            var rt = parallaxLayers[i];
            // Shift both offsetMin.x and offsetMax.x equally to slide without stretching
            rt.offsetMin = new Vector2(parallaxOffsets[i], rt.offsetMin.y);
            rt.offsetMax = new Vector2(parallaxOffsets[i], rt.offsetMax.y);
        }
    }

    private void ResetParallax()
    {
        if (parallaxLayers == null || parallaxOffsets == null) return;

        for (int i = 0; i < parallaxLayers.Length; i++)
        {
            if (parallaxLayers[i] == null) continue;
            parallaxOffsets[i] = 0f;
            var rt = parallaxLayers[i];
            rt.offsetMin = new Vector2(0, rt.offsetMin.y);
            rt.offsetMax = new Vector2(0, rt.offsetMax.y);
        }
    }

    // ── GAME OVER ───────────────────────────────────────────────

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        isPlaying = false;
        _stats?.SetCustom("finalScore", score);
        _stats?.RecordMistake(); // death = mistake

        // Save score
        if (score > bestScore)
            bestScore = score;

        var profile = ProfileManager.ActiveProfile;
        if (profile != null)
        {
            var stat = profile.progress.GetOrCreate("flappybird");
            stat.timesPlayed++;
            if (score > stat.bestScore) stat.bestScore = score;
            stat.lastPlayedAt = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            ProfileManager.Instance?.Save();
        }

        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        // Brief pause
        yield return new WaitForSeconds(0.3f);

        // Fall to ground
        float fallVel = 0;
        while (birdRT.anchoredPosition.y > groundY)
        {
            fallVel += gravity * 1.5f * Time.deltaTime;
            var pos = birdRT.anchoredPosition;
            pos.y += fallVel * Time.deltaTime;
            if (pos.y < groundY) pos.y = groundY;
            birdRT.anchoredPosition = pos;

            // Tilt nose down (positive Z because X is flipped)
            birdRT.localEulerAngles = new Vector3(0, 0, 60f);
            yield return null;
        }

        yield return new WaitForSeconds(0.8f);

        // Restart
        Restart();
    }

    private void Restart()
    {
        isDead = false;
        isPlaying = false;
        score = 0;
        velocity = 0;
        spawnTimer = spawnInterval;

        // Clear pipes
        foreach (var pair in pipes)
        {
            if (pair.root != null)
            {
                pair.root.gameObject.SetActive(false);
                pipePool.Add(pair);
            }
        }
        pipes.Clear();

        ResetBird();
        birdAnimator?.PlayFloating();
    }

    private void ResetBird()
    {
        birdRT.anchoredPosition = new Vector2(-playAreaWidth * 0.25f, playAreaHeight * 0.5f);
        birdRT.localEulerAngles = Vector3.zero;
        birdRT.localScale = new Vector3(-1, 1, 1); // flip X so bird faces right
        smoothTilt = 0f;
    }

    // ── NAVIGATION ──────────────────────────────────────────────

    public void OnHomePressed() => NavigationManager.GoToMainMenu();
}
