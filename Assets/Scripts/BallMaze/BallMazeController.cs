using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ball Maze game controller.
/// The child drags a ball across a wooden board, navigating around obstacles
/// and timing past rotating blockers to reach the hole.
///
/// Visual: wooden table board with frame, block shadows, ball shadow.
/// Physics: velocity-based drag with inertia, bounce on collision.
/// </summary>
public class BallMazeController : BaseMiniGame
{
    [Header("UI References")]
    public RectTransform playArea;

    [Header("Sprites")]
    [HideInInspector] public List<string> spriteKeys = new List<string>();
    [HideInInspector] public List<Sprite> spriteValues = new List<Sprite>();
    public Sprite roundedRectSprite;

    // ── runtime ──────────────────────────────────────────────────
    private Canvas canvas;
    private Dictionary<string, Sprite> spriteLookup;
    private int currentLevelIndex;
    private BallMazeLevel currentLevel;

    private RectTransform boardRT;
    private RectTransform ballRT;
    private RectTransform ballShadowRT;
    private RectTransform holeRT;
    private float unitSize;
    private float boardPixelW, boardPixelH;
    private float ballRadiusPx;

    private List<GameObject> spawnedObjects = new List<GameObject>();
    private List<BlockCollider> colliders = new List<BlockCollider>();

    // Physics state
    private Vector2 ballVelocity;
    private bool isComplete;

    // Physics constants — tilt (accelerometer) based
    private const float TILT_ACCEL = 3200f;      // acceleration force from full tilt
    private const float TILT_FRICTION = 3.5f;    // rolling friction (deceleration)
    private const float TILT_DEAD_ZONE = 0.06f;  // ignore tilt below this threshold
    private const float TILT_SMOOTHING = 8f;     // input smoothing factor
    private const float BOUNCE_REFLECT = 1.4f;   // velocity reflection multiplier on collision
    private const float BOUNCE_DAMPEN = 0.25f;   // velocity retention after bounce
    private const float MAX_SPEED = 1200f;       // max speed
    private Vector2 smoothedTilt;                 // smoothed accelerometer reading

    // Visual constants
    private static readonly Color TABLE_COLOR = new Color(0.91f, 0.87f, 0.82f);   // warm cream
    private static readonly Color FRAME_COLOR = new Color(0.76f, 0.62f, 0.42f);    // light wood
    private static readonly Color FRAME_DARK  = new Color(0.55f, 0.42f, 0.25f);    // frame edge
    private static readonly Color BOARD_COLOR = new Color(0.87f, 0.78f, 0.65f);    // warm wood board
    private static readonly Color BLOCK_COLOR = new Color(0.55f, 0.42f, 0.25f);    // dark wood block
    private static readonly Color BALL_COLOR  = new Color(0.93f, 0.31f, 0.24f);    // red ball
    private static readonly Color HOLE_COLOR  = new Color(0.15f, 0.15f, 0.15f);    // dark hole
    private static readonly Color START_COLOR = new Color(0.6f, 0.85f, 0.6f, 0.6f);// green start
    private const float FRAME_THICKNESS = 14f;
    private const float SHADOW_OFFSET = 6f;

    private struct BlockCollider
    {
        public Vector2 center;
        public Vector2 halfSize;     // half-size in UNROTATED local space
        public float rotation;       // degrees
        public float rotSpeed;       // degrees/sec (0 = static)
        public RectTransform rt;     // visual (for rotating blocks)
        public RectTransform shadowRT; // shadow (for rotating blocks)
    }

    // ── BaseMiniGame Hooks ──────────────────────────────────────

    protected override string GetFallbackGameId() => "ballmaze";

    protected override void OnGameInit()
    {
        totalRounds = 1;
        isEndless = false;
        playConfettiOnSessionWin = true;
        delayAfterFinalRound = 2.5f;

        canvas = GetComponentInParent<Canvas>();

        spriteLookup = new Dictionary<string, Sprite>();
        for (int i = 0; i < spriteKeys.Count && i < spriteValues.Count; i++)
            spriteLookup[spriteKeys[i]] = spriteValues[i];

        // Start at difficulty tier from central system (0=easy, 1=medium, 2=hard)
        currentLevelIndex = GameDifficultyConfig.BallMazeTier(Difficulty);
    }

    protected override void OnRoundSetup()
    {
        StartCoroutine(StartAfterLayout());
    }

    private IEnumerator StartAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(playArea);
        LoadLevel();
    }

    protected override void OnGameplayUpdate()
    {
        if (isComplete) return;

        UpdateRotatingBlocks();
        HandleInput();
        UpdateBallPhysics();
    }

    // ── level loading ────────────────────────────────────────────
    private bool _levelLoading;

    private void LoadLevel()
    {
        if (_levelLoading) return;
        _levelLoading = true;

        ClearAll();
        // Generate a fresh procedural level based on difficulty progression
        int levelDifficulty = Mathf.Clamp(currentLevelIndex, 0, 2);
        currentLevel = BallMazeLevels.GenerateLevel(levelDifficulty);
        isComplete = false;
        ballVelocity = Vector2.zero;
        smoothedTilt = Vector2.zero;

        float areaW = playArea.rect.width;
        float areaH = playArea.rect.height;

        float margin = 40f;
        float availW = areaW - margin * 2f;
        float availH = areaH - margin * 2f;
        unitSize = Mathf.Min(availW / currentLevel.boardW, availH / currentLevel.boardH);

        boardPixelW = currentLevel.boardW * unitSize;
        boardPixelH = currentLevel.boardH * unitSize;
        ballRadiusPx = currentLevel.ballRadius * unitSize;

        // ═══ TABLE BACKGROUND ═══
        var tableBG = CreateImage(playArea, "TableBG", Vector2.zero, Vector2.zero, TABLE_COLOR);
        var tableRT = tableBG.GetComponent<RectTransform>();
        tableRT.anchorMin = Vector2.zero;
        tableRT.anchorMax = Vector2.one;
        tableRT.offsetMin = Vector2.zero;
        tableRT.offsetMax = Vector2.zero;
        tableBG.GetComponent<Image>().raycastTarget = false;

        // ═══ BOARD SHADOW (soft, layered) ═══
        CreateBoardShadow(8f, 0.10f, 20f);
        CreateBoardShadow(5f, 0.18f, 10f);

        // ═══ WOOD FRAME ═══
        float frameW = boardPixelW + FRAME_THICKNESS * 2f;
        float frameH = boardPixelH + FRAME_THICKNESS * 2f;

        // Outer frame edge (darker)
        var frameOuter = CreateImage(playArea, "FrameOuter", Vector2.zero,
            new Vector2(frameW + 4f, frameH + 4f), FRAME_DARK);
        if (roundedRectSprite != null)
        {
            frameOuter.GetComponent<Image>().sprite = roundedRectSprite;
            frameOuter.GetComponent<Image>().type = Image.Type.Sliced;
        }
        frameOuter.GetComponent<Image>().raycastTarget = false;

        // Main frame
        var frameGO = CreateImage(playArea, "Frame", Vector2.zero,
            new Vector2(frameW, frameH), FRAME_COLOR);
        if (roundedRectSprite != null)
        {
            frameGO.GetComponent<Image>().sprite = roundedRectSprite;
            frameGO.GetComponent<Image>().type = Image.Type.Sliced;
        }
        frameGO.GetComponent<Image>().raycastTarget = false;

        // Inner frame edge (subtle dark lip)
        var frameInner = CreateImage(playArea, "FrameInner", Vector2.zero,
            new Vector2(boardPixelW + 4f, boardPixelH + 4f),
            new Color(FRAME_DARK.r, FRAME_DARK.g, FRAME_DARK.b, 0.3f));
        if (roundedRectSprite != null)
        {
            frameInner.GetComponent<Image>().sprite = roundedRectSprite;
            frameInner.GetComponent<Image>().type = Image.Type.Sliced;
        }
        frameInner.GetComponent<Image>().raycastTarget = false;

        // ═══ BOARD SURFACE ═══
        var boardGO = CreateImage(playArea, "Board", Vector2.zero,
            new Vector2(boardPixelW, boardPixelH), BOARD_COLOR);
        boardRT = boardGO.GetComponent<RectTransform>();
        var boardImg = boardGO.GetComponent<Image>();
        var bgSprite = GetSprite("background_brown");
        if (bgSprite != null) { boardImg.sprite = bgSprite; boardImg.type = Image.Type.Tiled; boardImg.color = Color.white; }
        boardImg.raycastTarget = true;

        // Vignette overlay (subtle darkening at edges)
        CreateVignette();

        // ═══ BOARD EDGE COLLIDERS ═══
        float hw = boardPixelW / 2f;
        float hh = boardPixelH / 2f;
        float wallThick = unitSize * 0.5f;
        colliders.Add(new BlockCollider { center = new Vector2(0, -hh - wallThick / 2f), halfSize = new Vector2(hw + wallThick, wallThick / 2f) });
        colliders.Add(new BlockCollider { center = new Vector2(0,  hh + wallThick / 2f), halfSize = new Vector2(hw + wallThick, wallThick / 2f) });
        colliders.Add(new BlockCollider { center = new Vector2(-hw - wallThick / 2f, 0), halfSize = new Vector2(wallThick / 2f, hh + wallThick) });
        colliders.Add(new BlockCollider { center = new Vector2( hw + wallThick / 2f, 0), halfSize = new Vector2(wallThick / 2f, hh + wallThick) });

        // ═══ BLOCKS ═══
        foreach (var b in currentLevel.blocks)
            CreateBlock(b);

        // ═══ ROTATING BLOCKS ═══
        if (currentLevel.rotating != null)
            foreach (var r in currentLevel.rotating)
                CreateRotatingBlock(r);

        // ═══ START MARKER ═══
        Vector2 startPos = GridToLocal(currentLevel.ballX, currentLevel.ballY);
        var startGO = CreateImage(boardRT, "StartMarker", startPos,
            new Vector2(unitSize * 0.9f, unitSize * 0.9f), START_COLOR);
        var startImg = startGO.GetComponent<Image>();
        var startSpr = GetSprite("hole_start");
        if (startSpr != null) { startImg.sprite = startSpr; startImg.color = new Color(1, 1, 1, 0.6f); }
        else if (roundedRectSprite != null) { startImg.sprite = roundedRectSprite; startImg.type = Image.Type.Sliced; }
        startImg.preserveAspect = true;
        startImg.raycastTarget = false;

        // ═══ HOLE ═══
        Vector2 holePos = GridToLocal(currentLevel.holeX, currentLevel.holeY);
        float holeVisSize = currentLevel.holeRadius * 2.2f * unitSize;
        var holeGO = CreateImage(boardRT, "Hole", holePos,
            new Vector2(holeVisSize, holeVisSize), HOLE_COLOR);
        holeRT = holeGO.GetComponent<RectTransform>();
        var holeImg = holeGO.GetComponent<Image>();
        var holeSpr = GetSprite("hole_large_end");
        if (holeSpr != null) { holeImg.sprite = holeSpr; holeImg.color = Color.white; }
        else if (roundedRectSprite != null) { holeImg.sprite = roundedRectSprite; holeImg.type = Image.Type.Sliced; }
        holeImg.raycastTarget = false;

        // ═══ BALL SHADOW ═══
        float ballSize = currentLevel.ballRadius * 2f * unitSize;
        var ballShadowGO = CreateImage(boardRT, "BallShadow", startPos + new Vector2(3f, -3f),
            new Vector2(ballSize * 1.05f, ballSize * 0.95f), new Color(0, 0, 0, 0.18f));
        ballShadowRT = ballShadowGO.GetComponent<RectTransform>();
        if (roundedRectSprite != null)
        {
            ballShadowGO.GetComponent<Image>().sprite = roundedRectSprite;
            ballShadowGO.GetComponent<Image>().type = Image.Type.Sliced;
        }
        ballShadowGO.GetComponent<Image>().raycastTarget = false;

        // ═══ BALL ═══
        var ballGO = CreateImage(boardRT, "Ball", startPos,
            new Vector2(ballSize, ballSize), BALL_COLOR);
        ballRT = ballGO.GetComponent<RectTransform>();
        var ballImg = ballGO.GetComponent<Image>();
        var ballSpr = GetSprite(currentLevel.ballSprite);
        if (ballSpr != null) { ballImg.sprite = ballSpr; ballImg.color = Color.white; }
        else if (roundedRectSprite != null) { ballImg.sprite = roundedRectSprite; ballImg.type = Image.Type.Sliced; }
        ballImg.preserveAspect = true;
        ballImg.raycastTarget = false;
        ballRT.SetAsLastSibling();

        StartCoroutine(BallIdlePulse());

        // Show tilt tutorial (centered, rotates in place)
        if (TutorialHand != null)
            TutorialHand.SetTiltMode(15f, 1.3f);

        _levelLoading = false;
    }

    private void CreateBoardShadow(float offset, float alpha, float expand)
    {
        var shadow = CreateImage(playArea, "BoardShadow",
            new Vector2(offset, -offset),
            new Vector2(boardPixelW + FRAME_THICKNESS * 2f + expand,
                        boardPixelH + FRAME_THICKNESS * 2f + expand),
            new Color(0, 0, 0, alpha));
        if (roundedRectSprite != null)
        {
            shadow.GetComponent<Image>().sprite = roundedRectSprite;
            shadow.GetComponent<Image>().type = Image.Type.Sliced;
        }
        shadow.GetComponent<Image>().raycastTarget = false;
    }

    private void CreateVignette()
    {
        // Four gradient overlays at edges for depth
        string[] sides = { "Top", "Bottom", "Left", "Right" };
        Vector2[] anchorsMin = { new Vector2(0, 0.85f), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0.85f, 0) };
        Vector2[] anchorsMax = { new Vector2(1, 1), new Vector2(1, 0.15f), new Vector2(0.15f, 1), new Vector2(1, 1) };
        Color darkEdge = new Color(0, 0, 0, 0.06f);

        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject("Vignette_" + sides[i]);
            go.transform.SetParent(boardRT, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorsMin[i];
            rt.anchorMax = anchorsMax[i];
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = darkEdge;
            img.raycastTarget = false;
            spawnedObjects.Add(go);
        }
    }

    // ── block creation ───────────────────────────────────────────

    private void CreateBlock(MazeBlockDef def)
    {
        Vector2 pos = GridToLocal(def.x, def.y);
        Vector2 size = def.Size * unitSize;

        // Shadow behind block
        CreateBlockShadow(pos, size, def.rotation);

        // Block visual — use natural sprite size, let RectTransform rotation handle orientation
        var go = CreateImage(boardRT, "Block_" + def.type, pos, size, BLOCK_COLOR);
        var img = go.GetComponent<Image>();
        var blockSpr = GetSprite(def.SpriteName);
        if (blockSpr != null) { img.sprite = blockSpr; img.color = Color.white; }
        else if (roundedRectSprite != null) { img.sprite = roundedRectSprite; img.type = Image.Type.Sliced; }
        img.preserveAspect = false;
        img.raycastTarget = false;
        go.GetComponent<RectTransform>().localEulerAngles = new Vector3(0, 0, def.rotation);

        // Collision — unrotated halfSize, rotation handled in collision math
        colliders.Add(new BlockCollider
        {
            center = pos,
            halfSize = size / 2f,
            rotation = def.rotation,
        });
    }

    private void CreateBlockShadow(Vector2 pos, Vector2 size, float rotation)
    {
        var shadow = CreateImage(boardRT, "BlockShadow",
            pos + new Vector2(2.5f, -2.5f),
            size + new Vector2(2f, 2f),
            new Color(0, 0, 0, 0.15f));
        shadow.GetComponent<Image>().raycastTarget = false;
        if (roundedRectSprite != null)
        {
            shadow.GetComponent<Image>().sprite = roundedRectSprite;
            shadow.GetComponent<Image>().type = Image.Type.Sliced;
        }
        shadow.GetComponent<RectTransform>().localEulerAngles = new Vector3(0, 0, rotation);
    }

    private void CreateRotatingBlock(MazeRotatingDef def)
    {
        Vector2 pos = GridToLocal(def.x, def.y);
        Vector2 size = def.Size * unitSize;

        // Shadow
        var shadowGO = CreateImage(boardRT, "RotShadow", pos + new Vector2(2.5f, -2.5f),
            size + new Vector2(2f, 2f), new Color(0, 0, 0, 0.15f));
        shadowGO.GetComponent<Image>().raycastTarget = false;
        if (roundedRectSprite != null)
        {
            shadowGO.GetComponent<Image>().sprite = roundedRectSprite;
            shadowGO.GetComponent<Image>().type = Image.Type.Sliced;
        }
        var shadowRotRT = shadowGO.GetComponent<RectTransform>();

        // Block
        var rotColor = new Color(0.65f, 0.45f, 0.22f); // slightly lighter than static blocks
        var go = CreateImage(boardRT, "Rotating_" + def.type, pos, size, rotColor);
        var img = go.GetComponent<Image>();
        var rotSpr = GetSprite(def.SpriteName);
        if (rotSpr != null) { img.sprite = rotSpr; img.color = Color.white; }
        else if (roundedRectSprite != null) { img.sprite = roundedRectSprite; img.type = Image.Type.Sliced; }
        img.preserveAspect = false;
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();

        colliders.Add(new BlockCollider
        {
            center = pos,
            halfSize = size / 2f,
            rotation = 0f,
            rotSpeed = def.speed,
            rt = rt,
            shadowRT = shadowRotRT,
        });
    }

    // ── rotating blocks ──────────────────────────────────────────
    private void UpdateRotatingBlocks()
    {
        for (int i = 0; i < colliders.Count; i++)
        {
            var c = colliders[i];
            if (c.rotSpeed == 0f) continue;

            c.rotation += c.rotSpeed * Time.deltaTime;
            if (c.rt != null)
                c.rt.localEulerAngles = new Vector3(0, 0, c.rotation);
            if (c.shadowRT != null)
                c.shadowRT.localEulerAngles = new Vector3(0, 0, c.rotation);

            colliders[i] = c;
        }
    }

    // ── input (accelerometer / tilt) ────────────────────────────
    private void HandleInput()
    {
        // Dismiss tutorial on first tilt
        Vector3 accel = Input.acceleration;
        if (Mathf.Abs(accel.x) > 0.15f || Mathf.Abs(accel.y) > 0.15f)
            DismissTutorial();

        // Unity's Input.acceleration is already in screen-space coordinates,
        // so tilt right → positive X, tilt forward → positive Y.
        Vector2 rawTilt = new Vector2(accel.x, accel.y);

        // Dead zone — ignore tiny tilts when device is nearly flat
        if (Mathf.Abs(rawTilt.x) < TILT_DEAD_ZONE) rawTilt.x = 0f;
        if (Mathf.Abs(rawTilt.y) < TILT_DEAD_ZONE) rawTilt.y = 0f;

        // Smooth to prevent jitter
        smoothedTilt = Vector2.Lerp(smoothedTilt, rawTilt, Time.deltaTime * TILT_SMOOTHING);
    }

    // ── ball physics (tilt / accelerometer) ─────────────────────
    private void UpdateBallPhysics()
    {
        if (ballRT == null) return;
        float dt = Time.deltaTime;
        if (dt < 0.0001f) return;

        Vector2 pos = ballRT.anchoredPosition;

        // Apply tilt as acceleration (like gravity on a tilted surface)
        ballVelocity += smoothedTilt * TILT_ACCEL * dt;

        // Rolling friction — always decelerates, stronger when tilt is small
        float frictionMul = Mathf.Max(0f, 1f - TILT_FRICTION * dt);
        ballVelocity *= frictionMul;
        if (ballVelocity.sqrMagnitude < 0.5f)
            ballVelocity = Vector2.zero;

        // Clamp max speed
        if (ballVelocity.magnitude > MAX_SPEED)
            ballVelocity = ballVelocity.normalized * MAX_SPEED;

        // Apply velocity in substeps to prevent tunneling
        Vector2 totalDelta = ballVelocity * dt;
        float totalDist = totalDelta.magnitude;
        if (totalDist < 0.1f)
        {
            // Still resolve collisions even if not moving (rotating blocks may push)
            pos = ResolveAllCollisions(pos);
            ballRT.anchoredPosition = pos;
            UpdateBallShadow(pos);
            return;
        }

        float stepSize = Mathf.Max(ballRadiusPx * 0.3f, 2f);
        int steps = Mathf.CeilToInt(totalDist / stepSize);
        steps = Mathf.Clamp(steps, 1, 40);
        Vector2 stepDelta = totalDelta / steps;

        for (int s = 0; s < steps; s++)
        {
            Vector2 prePos = pos;
            pos += stepDelta;

            // Resolve collisions
            Vector2 resolved = ResolveAllCollisions(pos);

            // Bounce: if collision pushed the ball, reflect velocity
            Vector2 push = resolved - pos;
            if (push.sqrMagnitude > 0.5f)
            {
                Vector2 pushDir = push.normalized;
                float velAlongPush = Vector2.Dot(ballVelocity, pushDir);
                if (velAlongPush < 0f)
                {
                    // Reflect and dampen
                    ballVelocity -= pushDir * velAlongPush * BOUNCE_REFLECT;
                    ballVelocity *= BOUNCE_DAMPEN;
                }
                // Update step direction for remaining substeps
                stepDelta = ballVelocity * dt / steps;
            }

            pos = resolved;
        }

        ballRT.anchoredPosition = pos;
        UpdateBallShadow(pos);

        // Check completion
        Vector2 holePos = holeRT.anchoredPosition;
        float holeDist = Vector2.Distance(pos, holePos);
        if (holeDist < currentLevel.holeRadius * unitSize * 0.9f)
        {
            isComplete = true;
            ballVelocity = Vector2.zero;
            StartCoroutine(CompletionSequence());
        }
    }

    private void UpdateBallShadow(Vector2 ballPos)
    {
        if (ballShadowRT != null)
            ballShadowRT.anchoredPosition = ballPos + new Vector2(3f, -3f);
    }

    // ── collision ────────────────────────────────────────────────
    private Vector2 ResolveAllCollisions(Vector2 ballPos)
    {
        for (int iter = 0; iter < 4; iter++)
        {
            for (int i = 0; i < colliders.Count; i++)
                ballPos = ResolveCircleRect(ballPos, ballRadiusPx, colliders[i]);
        }
        return ballPos;
    }

    private Vector2 ResolveCircleRect(Vector2 ballPos, float radius, BlockCollider block)
    {
        // Transform into block's local coordinate space
        Vector2 local = ballPos - block.center;
        if (block.rotation != 0f)
        {
            float rad = -block.rotation * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            local = new Vector2(local.x * cos - local.y * sin,
                                local.x * sin + local.y * cos);
        }

        // Closest point on AABB to circle center
        float closestX = Mathf.Clamp(local.x, -block.halfSize.x, block.halfSize.x);
        float closestY = Mathf.Clamp(local.y, -block.halfSize.y, block.halfSize.y);

        float dx = local.x - closestX;
        float dy = local.y - closestY;
        float distSq = dx * dx + dy * dy;

        if (distSq >= radius * radius) return ballPos;

        if (distSq > 0.01f)
        {
            float dist = Mathf.Sqrt(distSq);
            float overlap = radius - dist;
            local += new Vector2(dx / dist, dy / dist) * (overlap + 0.5f);
        }
        else
        {
            // Center inside — push to nearest edge
            float toRight  = block.halfSize.x - local.x;
            float toLeft   = block.halfSize.x + local.x;
            float toTop    = block.halfSize.y - local.y;
            float toBottom = block.halfSize.y + local.y;
            float min = Mathf.Min(Mathf.Min(toRight, toLeft), Mathf.Min(toTop, toBottom));

            if (min == toRight)       local.x =  block.halfSize.x + radius + 0.5f;
            else if (min == toLeft)   local.x = -block.halfSize.x - radius - 0.5f;
            else if (min == toTop)    local.y =  block.halfSize.y + radius + 0.5f;
            else                      local.y = -block.halfSize.y - radius - 0.5f;
        }

        // Transform back to world
        if (block.rotation != 0f)
        {
            float rad = block.rotation * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            local = new Vector2(local.x * cos - local.y * sin,
                                local.x * sin + local.y * cos);
        }

        return local + block.center;
    }

    // ── completion ───────────────────────────────────────────────
    private IEnumerator CompletionSequence()
    {
        Stats?.RecordCorrect();
        Stats?.SetCustom("levelCompleted", currentLevelIndex);
        if (holeRT != null) PlayCorrectEffect(holeRT);
        SoundLibrary.PlayRandomFeedback();

        // Phase 1: Pull ball into hole
        Vector2 ballStart = ballRT.anchoredPosition;
        Vector2 holePos = holeRT.anchoredPosition;
        float t = 0f;
        float suckDur = 0.4f;

        while (t < suckDur)
        {
            t += Time.deltaTime;
            float p = t / suckDur;
            float ease = p * p;
            ballRT.anchoredPosition = Vector2.Lerp(ballStart, holePos, ease);
            ballRT.localScale = Vector3.one * Mathf.Lerp(1f, 0.05f, ease);
            ballRT.localEulerAngles = new Vector3(0, 0, p * 200f);
            UpdateBallShadow(Vector2.Lerp(ballStart, holePos, ease));
            if (ballShadowRT != null)
                ballShadowRT.localScale = Vector3.one * Mathf.Lerp(1f, 0f, ease);
            yield return null;
        }
        ballRT.gameObject.SetActive(false);
        if (ballShadowRT != null) ballShadowRT.gameObject.SetActive(false);

        // Phase 2: Hole gulp pulse
        Vector3 holeOrig = holeRT.localScale;
        t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            float p = t / 0.25f;
            holeRT.localScale = holeOrig * (1f + 0.2f * Mathf.Sin(p * Mathf.PI));
            yield return null;
        }
        holeRT.localScale = holeOrig;

        // Phase 3: Star burst
        SpawnStarBurst(holePos);
        yield return new WaitForSeconds(0.15f);

        // Phase 4: Board bounce
        yield return StartCoroutine(BoardBounce());

        yield return new WaitForSeconds(0.5f);

        // Let BaseMiniGame handle confetti, stats, and journey navigation
        CompleteRound();

        // If journey won't navigate and we're back to playing, load next level in OnRoundSetup
    }

    protected override void OnRoundCleanup()
    {
        // Advance to next level for free-play restart
        currentLevelIndex++;
    }

    private IEnumerator BoardBounce()
    {
        if (boardRT == null) yield break;
        // Find all board-related objects to bounce together
        Transform boardParent = boardRT.parent;
        float t = 0f;
        float dur = 0.35f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float bounce = 1f + 0.015f * Mathf.Sin(p * Mathf.PI * 2f) * (1f - p);
            if (boardRT != null) boardRT.localScale = Vector3.one * bounce;
            yield return null;
        }
        if (boardRT != null) boardRT.localScale = Vector3.one;
    }

    private void SpawnStarBurst(Vector2 center)
    {
        Sprite starSpr = GetSprite("star");
        for (int i = 0; i < 14; i++)
        {
            float angle = (i * 360f / 14f + Random.Range(-10f, 10f)) * Mathf.Deg2Rad;
            float speed = Random.Range(200f, 400f);
            float size = Random.Range(16f, 30f);
            float lifetime = Random.Range(0.5f, 0.9f);
            Color c = Color.HSVToRGB(Random.Range(0.08f, 0.15f), Random.Range(0.5f, 0.8f), 1f);

            var go = new GameObject("Star");
            go.transform.SetParent(boardRT, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = center;
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            img.sprite = starSpr;
            img.color = c;
            img.raycastTarget = false;
            img.preserveAspect = true;

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
            vel.y -= 300f * Time.deltaTime;
            pos += vel * Time.deltaTime;
            rt.anchoredPosition = pos;
            float fade = 1f - t / lifetime;
            rt.localScale = Vector3.one * (0.5f + 0.5f * fade);
            img.color = new Color(startColor.r, startColor.g, startColor.b, fade);
            rt.localEulerAngles = new Vector3(0, 0, t * 180f);
            yield return null;
        }
        Destroy(rt.gameObject);
    }

    private IEnumerator BallIdlePulse()
    {
        while (!isComplete && ballRT != null)
        {
            float t = 0f;
            while (t < 1.5f && !isComplete)
            {
                t += Time.deltaTime;
                if (ballRT != null)
                {
                    float pulse = 1f + 0.03f * Mathf.Sin(t / 1.5f * Mathf.PI * 2f);
                    ballRT.localScale = Vector3.one * pulse;
                }
                yield return null;
            }
            if (ballRT != null) ballRT.localScale = Vector3.one;
        }
    }

    // ── coordinate helpers ───────────────────────────────────────
    private Vector2 GridToLocal(float gx, float gy)
    {
        return new Vector2(
            (gx - currentLevel.boardW / 2f) * unitSize,
            (gy - currentLevel.boardH / 2f) * unitSize);
    }

    // ── generic helpers ──────────────────────────────────────────
    private void ClearAll()
    {
        StopAllCoroutines();
        foreach (var go in spawnedObjects)
            if (go != null) Destroy(go);
        spawnedObjects.Clear();
        colliders.Clear();
        ballRT = null;
        ballShadowRT = null;
        holeRT = null;
        boardRT = null;
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

    private Sprite GetSprite(string key)
    {
        Sprite s;
        if (spriteLookup != null && spriteLookup.TryGetValue(key, out s))
            return s;
        return null;
    }

    // ── navigation ───────────────────────────────────────────────
    public void OnHomePressed() => ExitGame();
}
