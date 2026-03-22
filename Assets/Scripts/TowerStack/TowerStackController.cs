using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Tower stacking game.
/// A block moves left-right; tap to drop it onto the tower.
/// Misaligned overhang is trimmed and falls. Tower narrows over time.
/// Perfect alignments reward the player with a slight width bonus.
///
/// Uses wood/stone sprites from the TowerGame asset pack and
/// layered world background from Art/World.
/// </summary>
public class TowerStackController : BaseMiniGame
{
    [Header("UI References")]
    public RectTransform playArea;
    public Sprite roundedRectSprite;

    [Header("Sprites")]
    [HideInInspector] public List<string> spriteKeys = new List<string>();
    [HideInInspector] public List<Sprite> spriteValues = new List<Sprite>();

    // ── constants ────────────────────────────────────────────────
    private const float BLOCK_H = 52f;
    private const float INITIAL_W = 400f;
    private const float MIN_BLOCK_W = 18f;
    private const float BASE_SPEED = 220f;
    private const float SPEED_STEP = 4f;
    private const float MAX_SPEED = 450f;
    private const float PERFECT_THRESH = 8f;
    private const float PERFECT_GROW = 10f;
    private const float SPAWN_GAP = 120f;
    private const float GROUND_H = 100f;
    private const float CAMERA_SMOOTH = 4.5f;

    // Block materials alternate: wood, stone, wood, stone...
    private static readonly string[] BLOCK_SPRITES = { "wood_block", "stone_block" };

    private static readonly Color SKY_TOP = new Color(0.53f, 0.81f, 0.98f);

    // ── runtime state ────────────────────────────────────────────
    private Canvas canvas;
    private Dictionary<string, Sprite> spriteLookup;
    private float playW, playH;

    private RectTransform worldContainer;
    private RectTransform activeBlockRT;
    private Image activeBlockImg;
    private RectTransform activeShadowRT;

    private float activeCenterX;
    private float activeHalfW;
    private float activeY;
    private float moveSpeed;
    private float moveDir = 1f;
    private string activeSpriteKey;

    private struct StackPiece
    {
        public float left, right, y;
        public RectTransform rt;
    }

    private List<StackPiece> stack = new List<StackPiece>();
    private float towerTopY;
    private int colorIdx;

    private bool isStarted;
    private bool isMoving;
    private bool isDropping;
    private bool isGameOver;

    private int score;
    private int bestScore;
    private int consecutivePerfects;

    private float cameraOffset;
    private float targetCameraOffset;

    private GameObject gameOverPanel;
    private List<GameObject> spawnedObjects = new List<GameObject>();

    // ── BaseMiniGame Hooks ──────────────────────────────────────

    protected override string GetFallbackGameId() => "towerstack";

    protected override void OnGameInit()
    {
        isEndless = true;
        playConfettiOnRoundWin = false;
        playConfettiOnSessionWin = false;
        delayBeforeNextRound = 0f;

        canvas = GetComponentInParent<Canvas>();

        spriteLookup = new Dictionary<string, Sprite>();
        for (int i = 0; i < spriteKeys.Count && i < spriteValues.Count; i++)
            spriteLookup[spriteKeys[i]] = spriteValues[i];
    }

    protected override void OnRoundSetup()
    {
        StartCoroutine(InitAfterLayout());
    }

    // ── lifecycle ────────────────────────────────────────────────

    private IEnumerator InitAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(playArea);
        playW = playArea.rect.width;
        playH = playArea.rect.height;

        if (playW < 10f || playH < 10f)
        {
            yield return null;
            playW = playArea.rect.width;
            playH = playArea.rect.height;
        }

        BuildScene();
        StartGame();
    }

    protected override void OnGameplayUpdate()
    {
        if (!isStarted || isGameOver) return;

        if (isMoving && !isDropping)
        {
            MoveActiveBlock();
            HandleInput();
        }

        UpdateCamera();
    }

    // ── scene building ───────────────────────────────────────────

    private void BuildScene()
    {
        // ═══ STATIC BACKGROUND (does NOT scroll) ═══
        // Matches the layered landscape style from TowerBuilder

        // 1. GROUND FILL — solid green base (full screen, behind everything)
        var groundFill = CreateFullStretch("GroundFill", new Color(0.30f, 0.55f, 0.22f));
        groundFill.GetComponent<Image>().raycastTarget = false;

        // 2. SKY — covers upper portion only, green shows through at bottom
        CreateBgLayer(playArea, "Sky", null,
            new Vector2(0, 0.30f), new Vector2(1, 1f),
            SKY_TOP);

        // 2. CLOUD LAYER BACK (wispy clouds, upper half)
        CreateBgLayer(playArea, "CloudLayerB1", "bg_cloudB1",
            new Vector2(0, 0.55f), new Vector2(1, 1f),
            new Color(1, 1, 1, 0.7f));

        // 3. CLOUD LAYER FRONT (denser clouds, overlaps mountains)
        CreateBgLayer(playArea, "CloudLayerB2", "bg_cloudB2",
            new Vector2(0, 0.50f), new Vector2(1, 0.92f),
            new Color(1, 1, 1, 0.8f));

        // 4. MOUNTAINS (mid-background)
        CreateBgLayer(playArea, "Mountains", "bg_mountain",
            new Vector2(0, 0.35f), new Vector2(1, 0.70f),
            Color.white);

        // 5. HILLS (foreground of landscape)
        CreateBgLayer(playArea, "Hills", "bg_hills",
            new Vector2(0, 0.22f), new Vector2(1, 0.55f),
            Color.white);

        // 6. GROUND BACK (full grass, overlaps hills)
        CreateBgLayer(playArea, "GroundBack", "bg_ground1",
            new Vector2(0, 0f), new Vector2(1, 0.35f),
            new Color(0.45f, 0.72f, 0.35f));

        // 7. GROUND FRONT (darker grass strip at bottom)
        CreateBgLayer(playArea, "GroundFront", "bg_ground2",
            new Vector2(0, 0f), new Vector2(1, 0.20f),
            new Color(0.38f, 0.62f, 0.30f));

        // ═══ WORLD CONTAINER (scrolls with camera) ═══
        // All gameplay objects (blocks, ground platform) live here
        var wc = new GameObject("WorldContainer");
        wc.transform.SetParent(playArea, false);
        worldContainer = wc.AddComponent<RectTransform>();
        worldContainer.anchorMin = new Vector2(0.5f, 0f);
        worldContainer.anchorMax = new Vector2(0.5f, 0f);
        worldContainer.pivot = new Vector2(0.5f, 0f);
        worldContainer.sizeDelta = new Vector2(playW, 4000f);
        worldContainer.anchoredPosition = Vector2.zero;
        spawnedObjects.Add(wc);

    }

    private void CreateBgLayer(RectTransform parent, string name, string spriteKey,
        Vector2 anchorMin, Vector2 anchorMax, Color tint)
    {
        Sprite spr = spriteKey != null ? GetSprite(spriteKey) : null;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        if (spr != null)
        {
            img.sprite = spr;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = tint;
        }
        else
        {
            // Fallback: use tint directly as solid color (visible even without sprite)
            img.color = new Color(tint.r, tint.g, tint.b, tint.a * 0.5f);
        }
        spawnedObjects.Add(go);
    }

    // ── game flow ────────────────────────────────────────────────

    private void StartGame()
    {
        score = 0;
        consecutivePerfects = 0;
        colorIdx = 0;
        isGameOver = false;

        isDropping = false;
        isMoving = false;
        cameraOffset = 0f;
        targetCameraOffset = 0f;
        towerTopY = GROUND_H;

        // Place base block
        float baseLeft = -INITIAL_W / 2f;
        float baseRight = INITIAL_W / 2f;
        PlaceBlockVisual(baseLeft, baseRight, towerTopY, NextBlockSpriteKey());
        towerTopY += BLOCK_H;

        // Spawn first moving block
        SpawnActiveBlock(INITIAL_W);
        isStarted = true;

        PositionTutorialHand();
    }

    private void PositionTutorialHand()
    {
        if (TutorialHand == null || playArea == null) return;

        // Position hand at center-top of play area where blocks move
        Vector2 localPos = TutorialHand.GetLocalCenter(playArea);
        // Offset upward to where the active block is
        localPos.y += playArea.rect.height * 0.3f;
        TutorialHand.SetPosition(localPos);
    }

    // ── active block ─────────────────────────────────────────────

    private void SpawnActiveBlock(float width)
    {
        activeHalfW = width / 2f;
        activeSpriteKey = NextBlockSpriteKey();

        activeY = towerTopY + SPAWN_GAP;

        moveDir = (Random.value > 0.5f) ? 1f : -1f;
        float startX = moveDir > 0 ? (-playW / 2f + activeHalfW + 20f) : (playW / 2f - activeHalfW - 20f);
        activeCenterX = startX;

        moveSpeed = Mathf.Min(BASE_SPEED + score * SPEED_STEP, MAX_SPEED);

        // Shadow
        var shadowGO = CreateBlockVisual(worldContainer, "ActiveShadow",
            activeCenterX + 4f, activeY - 4f, width,
            new Color(0, 0, 0, 0.15f), false, null);
        activeShadowRT = shadowGO.GetComponent<RectTransform>();

        // Block
        var blockGO = CreateBlockVisual(worldContainer, "ActiveBlock",
            activeCenterX, activeY, width, Color.white, true, activeSpriteKey);
        activeBlockRT = blockGO.GetComponent<RectTransform>();
        activeBlockImg = blockGO.GetComponent<Image>();

        isMoving = true;

        targetCameraOffset = Mathf.Max(0f, (towerTopY + SPAWN_GAP + BLOCK_H * 2f) - playH * 0.55f);
    }

    private void MoveActiveBlock()
    {
        if (activeBlockRT == null) return;

        float maxX = playW / 2f - activeHalfW - 10f;
        float minX = -playW / 2f + activeHalfW + 10f;

        activeCenterX += moveSpeed * moveDir * Time.deltaTime;

        if (activeCenterX > maxX) { activeCenterX = maxX; moveDir = -1f; }
        else if (activeCenterX < minX) { activeCenterX = minX; moveDir = 1f; }

        activeBlockRT.anchoredPosition = new Vector2(activeCenterX, activeY);
        if (activeShadowRT != null)
            activeShadowRT.anchoredPosition = new Vector2(activeCenterX + 4f, activeY - 4f);
    }

    // ── input ────────────────────────────────────────────────────

    private void HandleInput()
    {
        bool tapped = false;
        Vector2 tapPos = Vector2.zero;

        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began) { tapped = true; tapPos = t.position; }
        }
        else if (Input.GetMouseButtonDown(0))
        {
            tapped = true;
            tapPos = Input.mousePosition;
        }

        if (tapped && IsTapInPlayArea(tapPos))
        {
            DismissTutorial();
            StartCoroutine(DropBlock());
        }
    }

    private bool IsTapInPlayArea(Vector2 screenPos)
    {
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(playArea, screenPos, cam);
    }

    // ── drop & overlap ───────────────────────────────────────────

    private IEnumerator DropBlock()
    {
        isDropping = true;
        isMoving = false;

        StackPiece topPiece = stack[stack.Count - 1];
        float prevLeft = topPiece.left;
        float prevRight = topPiece.right;

        float activeLeft = activeCenterX - activeHalfW;
        float activeRight = activeCenterX + activeHalfW;

        // Animate drop
        float startY = activeY;
        float targetY = towerTopY + BLOCK_H / 2f;
        float dropDist = startY - targetY;
        float dropTime = Mathf.Max(0.1f, dropDist / 1800f);
        float t = 0f;

        while (t < dropTime)
        {
            t += Time.deltaTime;
            float p = Mathf.Min(t / dropTime, 1f);
            float ease = p * p;
            float y = Mathf.Lerp(startY, targetY, ease);
            if (activeBlockRT != null)
                activeBlockRT.anchoredPosition = new Vector2(activeCenterX, y);
            if (activeShadowRT != null)
                activeShadowRT.anchoredPosition = new Vector2(activeCenterX + 4f, y - 4f);
            yield return null;
        }

        // Calculate overlap
        float overlapLeft = Mathf.Max(activeLeft, prevLeft);
        float overlapRight = Mathf.Min(activeRight, prevRight);
        float overlapW = overlapRight - overlapLeft;

        if (overlapW <= 0f)
        {
            // Complete miss
            if (activeBlockRT != null)
                StartCoroutine(AnimateFalling(activeBlockRT, activeCenterX, targetY,
                    activeCenterX > (prevLeft + prevRight) / 2f ? 1f : -1f));
            if (activeShadowRT != null) Destroy(activeShadowRT.gameObject);
            activeBlockRT = null;
            activeShadowRT = null;
            yield return new WaitForSeconds(0.6f);
            GameOver();
            yield break;
        }

        float blockW = activeRight - activeLeft;
        bool isPerfect = Mathf.Abs(overlapW - blockW) < PERFECT_THRESH;

        string droppedSpriteKey = activeSpriteKey;

        Destroy(activeBlockRT.gameObject);
        if (activeShadowRT != null) Destroy(activeShadowRT.gameObject);
        activeBlockRT = null;
        activeShadowRT = null;

        if (isPerfect)
        {
            overlapLeft = activeLeft;
            overlapRight = activeRight;
            overlapW = blockW;
            consecutivePerfects++;

            if (consecutivePerfects >= 3)
            {
                float grow = PERFECT_GROW;
                overlapLeft -= grow / 2f;
                overlapRight += grow / 2f;
                overlapW = overlapRight - overlapLeft;
                if (overlapW > INITIAL_W)
                {
                    float excess = overlapW - INITIAL_W;
                    overlapLeft += excess / 2f;
                    overlapRight -= excess / 2f;
                    overlapW = INITIAL_W;
                }
            }
        }
        else
        {
            consecutivePerfects = 0;

            if (activeLeft < prevLeft)
            {
                float ohCenter = (activeLeft + prevLeft) / 2f;
                float ohW = prevLeft - activeLeft;
                CreateFallingPiece(ohCenter, towerTopY + BLOCK_H / 2f, ohW, droppedSpriteKey, -1f);
            }
            if (activeRight > prevRight)
            {
                float ohCenter = (prevRight + activeRight) / 2f;
                float ohW = activeRight - prevRight;
                CreateFallingPiece(ohCenter, towerTopY + BLOCK_H / 2f, ohW, droppedSpriteKey, 1f);
            }
        }

        PlaceBlockVisual(overlapLeft, overlapRight, towerTopY, droppedSpriteKey);
        towerTopY += BLOCK_H;
        score++;
        Stats?.RecordCorrect();
        SoundLibrary.PlayRandomFeedback();
        if (score > bestScore) bestScore = score;

        StackPiece placed = stack[stack.Count - 1];
        if (isPerfect)
        {
            SpawnParticles(new Vector2((overlapLeft + overlapRight) / 2f, towerTopY), 16,
                new Color(1f, 0.85f, 0.3f));
            yield return StartCoroutine(BlockBounce(placed.rt));
        }
        else
        {
            SpawnParticles(new Vector2((overlapLeft + overlapRight) / 2f, towerTopY), 8,
                new Color(0.8f, 0.65f, 0.4f));
            yield return StartCoroutine(BlockSettle(placed.rt));
        }

        if (overlapW < MIN_BLOCK_W)
        {
            yield return new WaitForSeconds(0.3f);
            GameOver();
            yield break;
        }

        SpawnActiveBlock(overlapW);
        isDropping = false;
    }

    // ── block visuals ────────────────────────────────────────────

    private void PlaceBlockVisual(float left, float right, float y, string blockSpriteKey)
    {
        float centerX = (left + right) / 2f;
        float width = right - left;
        float centerY = y + BLOCK_H / 2f;

        // Shadow
        CreateBlockVisual(worldContainer, "BlockShadow",
            centerX + 4f, centerY - 4f, width + 2f,
            new Color(0, 0, 0, 0.12f), false, null);

        // Block
        var go = CreateBlockVisual(worldContainer, "PlacedBlock",
            centerX, centerY, width, Color.white, true, blockSpriteKey);

        stack.Add(new StackPiece
        {
            left = left,
            right = right,
            y = y,
            rt = go.GetComponent<RectTransform>()
        });
    }

    private GameObject CreateBlockVisual(RectTransform parent, string name,
        float x, float y, float width, Color tint, bool isBlock, string blockSpriteKey)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, BLOCK_H);
        var img = go.AddComponent<Image>();
        img.raycastTarget = false;

        if (isBlock)
        {
            // Use wood or stone sprite based on blockSpriteKey
            Sprite blockSpr = blockSpriteKey != null ? GetSprite(blockSpriteKey) : null;
            if (blockSpr != null)
            {
                img.sprite = blockSpr;
                img.type = Image.Type.Tiled;
                img.color = tint;
            }
            else
            {
                // Fallback: colored rounded rect (brown for wood, grey for stone)
                bool isStone = blockSpriteKey != null && blockSpriteKey.Contains("stone");
                img.color = isStone
                    ? new Color(0.62f, 0.62f, 0.65f)
                    : new Color(0.72f, 0.53f, 0.32f);
                if (roundedRectSprite != null)
                {
                    img.sprite = roundedRectSprite;
                    img.type = Image.Type.Sliced;
                }
            }

            // Top highlight
            if (width > 10f)
            {
                var shine = new GameObject("Shine");
                shine.transform.SetParent(go.transform, false);
                var shineRT = shine.AddComponent<RectTransform>();
                shineRT.anchorMin = new Vector2(0, 0.7f);
                shineRT.anchorMax = new Vector2(1, 1);
                shineRT.offsetMin = new Vector2(2, 0);
                shineRT.offsetMax = new Vector2(-2, -1);
                var shineImg = shine.AddComponent<Image>();
                shineImg.color = new Color(1, 1, 1, 0.15f);
                shineImg.raycastTarget = false;
            }
        }
        else
        {
            // Shadow — simple dark shape
            img.color = tint;
            if (roundedRectSprite != null)
            {
                img.sprite = roundedRectSprite;
                img.type = Image.Type.Sliced;
            }
        }

        spawnedObjects.Add(go);
        return go;
    }

    // ── falling pieces ───────────────────────────────────────────

    private void CreateFallingPiece(float centerX, float centerY, float width, string spriteKey, float dir)
    {
        var go = CreateBlockVisual(worldContainer, "FallingPiece",
            centerX, centerY, width, Color.white, true, spriteKey);
        StartCoroutine(AnimateFalling(go.GetComponent<RectTransform>(), centerX, centerY, dir));
    }

    private IEnumerator AnimateFalling(RectTransform rt, float startX, float startY, float dir)
    {
        float t = 0f;
        float velY = 0f;
        float rotSpeed = dir * Random.Range(80f, 160f);
        float driftX = dir * Random.Range(40f, 90f);
        float gravity = 2200f;

        while (t < 2.5f && rt != null)
        {
            t += Time.deltaTime;
            velY -= gravity * Time.deltaTime;
            startY += velY * Time.deltaTime;
            startX += driftX * Time.deltaTime;

            rt.anchoredPosition = new Vector2(startX, startY);
            rt.localEulerAngles = new Vector3(0, 0, rotSpeed * t);

            var img = rt.GetComponent<Image>();
            if (img != null && t > 0.6f)
                img.color = new Color(img.color.r, img.color.g, img.color.b,
                    Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.6f));

            if (startY < -cameraOffset - 300f) break;
            yield return null;
        }

        if (rt != null) Destroy(rt.gameObject);
    }

    // ── camera follow ────────────────────────────────────────────

    private void UpdateCamera()
    {
        cameraOffset = Mathf.Lerp(cameraOffset, targetCameraOffset, Time.deltaTime * CAMERA_SMOOTH);
        if (worldContainer != null)
            worldContainer.anchoredPosition = new Vector2(0, -cameraOffset);
    }

    // ── feedback effects ─────────────────────────────────────────

    private IEnumerator BlockBounce(RectTransform rt)
    {
        if (rt == null) yield break;
        Vector2 orig = rt.anchoredPosition;
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            float bounce = Mathf.Sin(t / 0.3f * Mathf.PI * 2f) * 7f * (1f - t / 0.3f);
            rt.anchoredPosition = orig + new Vector2(0, bounce);
            yield return null;
        }
        rt.anchoredPosition = orig;
    }

    private IEnumerator BlockSettle(RectTransform rt)
    {
        if (rt == null) yield break;
        Vector2 orig = rt.anchoredPosition;
        float t = 0f;
        while (t < 0.15f)
        {
            t += Time.deltaTime;
            float settle = Mathf.Sin(t / 0.15f * Mathf.PI) * 3f;
            rt.anchoredPosition = orig + new Vector2(0, settle);
            yield return null;
        }
        rt.anchoredPosition = orig;
    }

    private void SpawnParticles(Vector2 worldPos, int count, Color baseColor)
    {
        Sprite debrisSpr = GetSprite("debris_wood");
        for (int i = 0; i < count; i++)
        {
            float angle = (i * 360f / count + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
            float speed = Random.Range(150f, 350f);
            float size = Random.Range(10f, 22f);
            float lifetime = Random.Range(0.4f, 0.8f);
            Color c = Color.Lerp(baseColor, Color.white, Random.Range(0.1f, 0.4f));

            var go = new GameObject("Particle");
            go.transform.SetParent(worldContainer, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = worldPos;
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            img.preserveAspect = true;
            if (debrisSpr != null)
                img.sprite = debrisSpr;
            else if (roundedRectSprite != null)
            {
                img.sprite = roundedRectSprite;
                img.type = Image.Type.Sliced;
            }

            StartCoroutine(AnimateParticle(rt, img, angle, speed, lifetime));
        }
    }

    private IEnumerator AnimateParticle(RectTransform rt, Image img,
        float angle, float speed, float lifetime)
    {
        Vector2 pos = rt.anchoredPosition;
        Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
        Color startColor = img.color;
        float t = 0f;
        while (t < lifetime)
        {
            t += Time.deltaTime;
            vel.y -= 400f * Time.deltaTime;
            pos += vel * Time.deltaTime;
            rt.anchoredPosition = pos;
            float fade = 1f - t / lifetime;
            rt.localScale = Vector3.one * (0.5f + 0.5f * fade);
            img.color = new Color(startColor.r, startColor.g, startColor.b, fade);
            rt.localEulerAngles = new Vector3(0, 0, t * 200f);
            yield return null;
        }
        Destroy(rt.gameObject);
    }

    // ── game over ────────────────────────────────────────────────

    private void GameOver()
    {
        isGameOver = true;
        isMoving = false;
        Stats?.SetCustom("finalScore", score);
        Stats?.RecordMistake();
        CompleteRound();
        StartCoroutine(GameOverSequence());
    }

    private IEnumerator GameOverSequence()
    {
        float t = 0f;
        while (t < 0.4f)
        {
            t += Time.deltaTime;
            float shake = Mathf.Sin(t * 40f) * 5f * (1f - t / 0.4f);
            if (worldContainer != null)
                worldContainer.anchoredPosition = new Vector2(shake, -cameraOffset);
            yield return null;
        }
        if (worldContainer != null)
            worldContainer.anchoredPosition = new Vector2(0, -cameraOffset);

        yield return new WaitForSeconds(0.3f);
        ShowGameOverPanel();
    }

    private void ShowGameOverPanel()
    {
        gameOverPanel = new GameObject("GameOverPanel");
        gameOverPanel.transform.SetParent(playArea, false);
        var panelRT = gameOverPanel.AddComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;
        var panelImg = gameOverPanel.AddComponent<Image>();
        panelImg.color = new Color(0, 0, 0, 0.35f);
        spawnedObjects.Add(gameOverPanel);

        var card = new GameObject("Card");
        card.transform.SetParent(gameOverPanel.transform, false);
        var cardRT = card.AddComponent<RectTransform>();
        cardRT.sizeDelta = new Vector2(480f, 320f);
        cardRT.anchoredPosition = new Vector2(0, 20f);
        var cardImg = card.AddComponent<Image>();
        cardImg.color = Color.white;
        if (roundedRectSprite != null)
        {
            cardImg.sprite = roundedRectSprite;
            cardImg.type = Image.Type.Sliced;
        }

        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(card.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        titleRT.anchoredPosition = new Vector2(0, 90f);
        titleRT.sizeDelta = new Vector2(400f, 60f);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05E0\u05D2\u05DE\u05E8 \u05D4\u05DE\u05E9\u05D7\u05E7");
        titleTMP.fontSize = 38;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = new Color(0.3f, 0.3f, 0.3f);
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.raycastTarget = false;

        var scoreGO = new GameObject("Score");
        scoreGO.transform.SetParent(card.transform, false);
        var scoreRT = scoreGO.AddComponent<RectTransform>();
        scoreRT.anchoredPosition = new Vector2(0, 30f);
        scoreRT.sizeDelta = new Vector2(400f, 55f);
        var scoreTMP = scoreGO.AddComponent<TextMeshProUGUI>();
        scoreTMP.text = score.ToString();
        scoreTMP.fontSize = 52;
        scoreTMP.fontStyle = FontStyles.Bold;
        scoreTMP.color = new Color(0.72f, 0.53f, 0.32f);
        scoreTMP.alignment = TextAlignmentOptions.Center;
        scoreTMP.raycastTarget = false;

        CreatePanelButton(card.transform,
            "\u05E0\u05E1\u05D4 \u05E9\u05D5\u05D1",
            new Vector2(-110f, -75f), new Vector2(200f, 60f),
            new Color(0.40f, 0.80f, 0.40f), OnRetry);

        CreatePanelButton(card.transform,
            "\u05D1\u05D9\u05EA",
            new Vector2(110f, -75f), new Vector2(160f, 60f),
            new Color(0.26f, 0.65f, 0.96f), OnHomePressed);
    }

    private void CreatePanelButton(Transform parent, string hebrewText,
        Vector2 pos, Vector2 size, Color color, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject("Btn");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = color;
        if (roundedRectSprite != null)
        {
            img.sprite = roundedRectSprite;
            img.type = Image.Type.Sliced;
        }
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(go.transform, false);
        var txtRT = txtGO.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(tmp, hebrewText);
        tmp.fontSize = 28;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
    }

    // ── navigation ───────────────────────────────────────────────

    private void OnRetry()
    {
        ClearAll();
        BuildScene();
        StartGame();
    }

    public void OnHomePressed() => ExitGame();

    // ── helpers ───────────────────────────────────────────────────

    private string NextBlockSpriteKey()
    {
        string key = BLOCK_SPRITES[colorIdx % BLOCK_SPRITES.Length];
        colorIdx++;
        return key;
    }

    private Sprite GetSprite(string key)
    {
        Sprite s;
        if (spriteLookup != null && spriteLookup.TryGetValue(key, out s))
            return s;
        return null;
    }

    private void ClearAll()
    {
        StopAllCoroutines();
        foreach (var go in spawnedObjects)
            if (go != null) Destroy(go);
        spawnedObjects.Clear();
        stack.Clear();
        activeBlockRT = null;
        activeBlockImg = null;
        activeShadowRT = null;
        worldContainer = null;
        gameOverPanel = null;
    }

    private GameObject CreateFullStretch(string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(playArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = color;
        spawnedObjects.Add(go);
        return go;
    }

    private GameObject CreateImage(RectTransform parent, string name,
        Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = color;
        spawnedObjects.Add(go);
        return go;
    }
}
