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
    private bool isDragging;
    private bool isComplete;
    private Vector2 fingerTarget; // current finger position in board-local coords

    // Physics constants
    private const float FOLLOW_SPEED = 18f;     // how quickly ball follows finger
    private const float FRICTION = 8f;           // deceleration when released
    private const float BOUNCE_REFLECT = 1.4f;   // velocity reflection multiplier on collision
    private const float BOUNCE_DAMPEN = 0.25f;    // velocity retention after bounce
    private const float MAX_SPEED = 1200f;

    // Visual constants
    private static readonly Color TABLE_COLOR = new Color(0.91f, 0.87f, 0.82f);   // warm cream
    private static readonly Color FRAME_COLOR = new Color(0.76f, 0.62f, 0.42f);    // light wood
    private static readonly Color FRAME_DARK  = new Color(0.55f, 0.42f, 0.25f);    // frame edge
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

        currentLevelIndex = 0;
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
        int levelDifficulty = Mathf.Clamp(currentLevelIndex / 2, 0, 2); // 0-1=easy, 2-3=medium, 4+=hard
        currentLevel = BallMazeLevels.GenerateLevel(levelDifficulty);
        isComplete = false;
        isDragging = false;
        ballVelocity = Vector2.zero;

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
            new Vector2(boardPixelW, boardPixelH), Color.white);
        boardRT = boardGO.GetComponent<RectTransform>();
        var boardImg = boardGO.GetComponent<Image>();
        boardImg.sprite = GetSprite("background_brown");
        boardImg.type = Image.Type.Tiled;
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
            new Vector2(unitSize * 0.9f, unitSize * 0.9f), new Color(1, 1, 1, 0.6f));
        startGO.GetComponent<Image>().sprite = GetSprite("hole_start");
        startGO.GetComponent<Image>().preserveAspect = true;
        startGO.GetComponent<Image>().raycastTarget = false;

        // ═══ HOLE ═══
        Vector2 holePos = GridToLocal(currentLevel.holeX, currentLevel.holeY);
        float holeVisSize = currentLevel.holeRadius * 2.2f * unitSize;
        var holeGO = CreateImage(boardRT, "Hole", holePos,
            new Vector2(holeVisSize, holeVisSize), Color.white);
        holeRT = holeGO.GetComponent<RectTransform>();
        holeGO.GetComponent<Image>().sprite = GetSprite("hole_large_end");
        holeGO.GetComponent<Image>().preserveAspect = true;
        holeGO.GetComponent<Image>().raycastTarget = false;

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
            new Vector2(ballSize, ballSize), Color.white);
        ballRT = ballGO.GetComponent<RectTransform>();
        ballGO.GetComponent<Image>().sprite = GetSprite(currentLevel.ballSprite);
        ballGO.GetComponent<Image>().preserveAspect = true;
        ballGO.GetComponent<Image>().raycastTarget = false;
        ballRT.SetAsLastSibling();

        fingerTarget = startPos;
        StartCoroutine(BallIdlePulse());

        // Position tutorial hand: show dragging ball toward the hole
        PositionTutorialHand(startPos, holePos);

        _levelLoading = false;
    }

    private void PositionTutorialHand(Vector2 ballStartLocal, Vector2 holeLocal)
    {
        if (TutorialHand == null || boardRT == null) return;

        var handParent = TutorialHand.transform.parent as RectTransform;

        // Convert ball start position from boardRT local space to hand parent space
        Vector3 worldFrom = boardRT.TransformPoint(ballStartLocal);
        Vector2 screenFrom = RectTransformUtility.WorldToScreenPoint(null, worldFrom);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            handParent, screenFrom, null, out Vector2 fromPos);

        // Convert hole position from boardRT local space to hand parent space
        Vector3 worldTo = boardRT.TransformPoint(holeLocal);
        Vector2 screenTo = RectTransformUtility.WorldToScreenPoint(null, worldTo);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            handParent, screenTo, null, out Vector2 toPos);

        // Show path from ball to partway toward hole (not all the way — just hint the direction)
        Vector2 direction = (toPos - fromPos).normalized;
        float hintDist = Mathf.Min(Vector2.Distance(fromPos, toPos) * 0.5f, 200f);
        Vector2 hintTo = fromPos + direction * hintDist;

        TutorialHand.SetMovePath(fromPos, hintTo, 1.2f);
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
        var go = CreateImage(boardRT, "Block_" + def.type, pos, size, Color.white);
        var img = go.GetComponent<Image>();
        img.sprite = GetSprite(def.SpriteName);
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
        var go = CreateImage(boardRT, "Rotating_" + def.type, pos, size, Color.white);
        var img = go.GetComponent<Image>();
        img.sprite = GetSprite(def.SpriteName);
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

    // ── input ────────────────────────────────────────────────────
    private void HandleInput()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                if (IsTouchOnBall(touch.position))
                {
                    isDragging = true;
                    DismissTutorial();
                }
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                isDragging = false;
            }

            if (isDragging)
                UpdateFingerTarget(touch.position);
        }
        else
        {
            if (Input.GetMouseButtonDown(0) && IsTouchOnBall(Input.mousePosition))
            {
                isDragging = true;
                DismissTutorial();
            }
            else if (Input.GetMouseButtonUp(0))
                isDragging = false;

            if (isDragging)
                UpdateFingerTarget(Input.mousePosition);
        }
    }

    private bool IsTouchOnBall(Vector2 screenPos)
    {
        if (ballRT == null || boardRT == null) return false;
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRT, screenPos, cam, out localPoint))
            return false;
        return Vector2.Distance(localPoint, ballRT.anchoredPosition) < ballRadiusPx * 3f;
    }

    private void UpdateFingerTarget(Vector2 screenPos)
    {
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 local;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRT, screenPos, cam, out local))
            fingerTarget = local;
    }

    // ── ball physics ─────────────────────────────────────────────
    private void UpdateBallPhysics()
    {
        if (ballRT == null) return;
        float dt = Time.deltaTime;
        if (dt < 0.0001f) return;

        Vector2 pos = ballRT.anchoredPosition;

        if (isDragging)
        {
            // Smooth follow toward finger with spring-like behavior
            Vector2 diff = fingerTarget - pos;
            ballVelocity = Vector2.Lerp(ballVelocity, diff * FOLLOW_SPEED, dt * 12f);
        }
        else
        {
            // Friction when not dragging
            ballVelocity *= Mathf.Max(0f, 1f - FRICTION * dt);
            if (ballVelocity.sqrMagnitude < 1f)
                ballVelocity = Vector2.zero;
        }

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
            isDragging = false;
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
                if (ballRT != null && !isDragging)
                {
                    float pulse = 1f + 0.03f * Mathf.Sin(t / 1.5f * Mathf.PI * 2f);
                    ballRT.localScale = Vector3.one * pulse;
                }
                yield return null;
            }
            if (ballRT != null && !isDragging) ballRT.localScale = Vector3.one;
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
