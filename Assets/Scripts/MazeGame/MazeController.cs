using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Maze mini-game with predefined layouts.
/// Child drags an animal through the maze to reach a star goal.
/// Features: thick rounded walls, glowing trail, success sparkles, 8 levels.
/// </summary>
public class MazeController : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform mazeContainer;
    public RectTransform playerRT;
    public Image playerImage;
    public RectTransform goalRT;
    public Image goalImage;
    public RectTransform trailContainer;

    [Header("Sprites")]
    public Sprite circleSprite;

    [Header("Settings")]
    public Color wallColor = new Color(0.45f, 0.35f, 0.60f);
    public Color pathColor = new Color(0.96f, 0.94f, 0.98f);
    public Color trailColor = new Color(0.55f, 0.78f, 1f, 0.7f);

    // ── predefined maze data ─────────────────────────────────────────
    // Each maze: width, height, start, goal, and wall string.
    // Wall string: 'H' rows (horizontal walls) then 'V' rows (vertical walls).
    // '1' = wall present, '0' = wall removed.

    private struct MazeDef
    {
        public int w, h;
        public Vector2Int start, goal;
        public string[] hWalls; // h+1 rows of w chars
        public string[] vWalls; // h rows of w+1 chars
    }

    private static readonly MazeDef[] Mazes = CreateMazes();

    private static MazeDef[] CreateMazes()
    {
        return new MazeDef[]
        {
            // Maze 1: 5x5, simple S-curve
            new MazeDef {
                w = 5, h = 5,
                start = new Vector2Int(0, 4), goal = new Vector2Int(4, 0),
                hWalls = new[] {
                    "11111", // y=0 bottom
                    "10010", // y=1
                    "01100", // y=2
                    "10011", // y=3
                    "01100", // y=4
                    "11111", // y=5 top
                },
                vWalls = new[] {
                    "100001", // y=0
                    "101010", // y=1
                    "100101", // y=2
                    "101010", // y=3
                    "100001", // y=4
                },
            },
            // Maze 2: 5x5, zigzag
            new MazeDef {
                w = 5, h = 5,
                start = new Vector2Int(0, 4), goal = new Vector2Int(4, 0),
                hWalls = new[] {
                    "11111",
                    "01001",
                    "10110",
                    "01001",
                    "10110",
                    "11111",
                },
                vWalls = new[] {
                    "110001",
                    "100100",
                    "101010",
                    "100100",
                    "110001",
                },
            },
            // Maze 3: 6x6, more turns
            new MazeDef {
                w = 6, h = 6,
                start = new Vector2Int(0, 5), goal = new Vector2Int(5, 0),
                hWalls = new[] {
                    "111111",
                    "100100",
                    "011010",
                    "100101",
                    "010010",
                    "101001",
                    "111111",
                },
                vWalls = new[] {
                    "1010001",
                    "1001010",
                    "1100101",
                    "1010010",
                    "1001101",
                    "1100001",
                },
            },
            // Maze 4: 6x7, longer with dead ends
            new MazeDef {
                w = 6, h = 7,
                start = new Vector2Int(0, 6), goal = new Vector2Int(5, 0),
                hWalls = new[] {
                    "111111",
                    "100100",
                    "010011",
                    "101000",
                    "010110",
                    "100001",
                    "011010",
                    "111111",
                },
                vWalls = new[] {
                    "1010101",
                    "1001010",
                    "1100001",
                    "1010110",
                    "1001001",
                    "1100010",
                    "1010101",
                },
            },
            // Maze 5: 7x7, medium
            new MazeDef {
                w = 7, h = 7,
                start = new Vector2Int(0, 6), goal = new Vector2Int(6, 0),
                hWalls = new[] {
                    "1111111",
                    "1001001",
                    "0110010",
                    "1001100",
                    "0100011",
                    "0011000",
                    "1000110",
                    "1111111",
                },
                vWalls = new[] {
                    "10100101",
                    "10010010",
                    "11001001",
                    "10100110",
                    "10011001",
                    "11100010",
                    "10010101",
                },
            },
            // Maze 6: 7x8, larger
            new MazeDef {
                w = 7, h = 8,
                start = new Vector2Int(0, 7), goal = new Vector2Int(6, 0),
                hWalls = new[] {
                    "1111111",
                    "0100100",
                    "1010011",
                    "0101000",
                    "1000110",
                    "0110001",
                    "1001010",
                    "0010100",
                    "1111111",
                },
                vWalls = new[] {
                    "10010101",
                    "11001010",
                    "10100101",
                    "10010010",
                    "11101001",
                    "10010100",
                    "10101010",
                    "11010001",
                },
            },
            // Maze 7: 7x9, challenging
            new MazeDef {
                w = 7, h = 9,
                start = new Vector2Int(0, 8), goal = new Vector2Int(6, 0),
                hWalls = new[] {
                    "1111111",
                    "1001010",
                    "0110001",
                    "1001100",
                    "0100010",
                    "1010101",
                    "0001010",
                    "0110001",
                    "1001100",
                    "1111111",
                },
                vWalls = new[] {
                    "10100001",
                    "10010110",
                    "11001001",
                    "10100010",
                    "10011101",
                    "11000010",
                    "10110001",
                    "10001010",
                    "10100101",
                },
            },
            // Maze 8: 8x9, biggest
            new MazeDef {
                w = 8, h = 9,
                start = new Vector2Int(0, 8), goal = new Vector2Int(7, 0),
                hWalls = new[] {
                    "11111111",
                    "10010010",
                    "01100101",
                    "10011010",
                    "01000101",
                    "10110010",
                    "01001001",
                    "10010100",
                    "00101010",
                    "11111111",
                },
                vWalls = new[] {
                    "101001001",
                    "100110010",
                    "110001101",
                    "101010010",
                    "100101001",
                    "110010110",
                    "101001001",
                    "100110010",
                    "110001101",
                },
            },
        };
    }

    // ── runtime state ────────────────────────────────────────────────
    private Canvas canvas;
    private bool[,] hWalls;
    private bool[,] vWalls;
    private float cellW, cellH;
    private float mazeOffsetX, mazeOffsetY;
    private Vector2Int playerCell;
    private Vector2Int goalCell;
    private bool isComplete;
    private int currentLevel;
    private int mazeWidth, mazeHeight;
    private List<GameObject> mazeObjects = new List<GameObject>();
    private List<GameObject> trailObjects = new List<GameObject>();
    private HashSet<Vector2Int> trailCells = new HashSet<Vector2Int>();
    private int lastAnimalIndex = -1;
    private float goalBobTime;
    private Coroutine idleBounceRoutine;
    private bool isDragging;

    // ── lifecycle ────────────────────────────────────────────────────
    private void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        currentLevel = 0;
        // Wait one frame for layout to calculate RectTransform sizes
        StartCoroutine(DelayedStart());
    }

    private IEnumerator DelayedStart()
    {
        yield return null; // wait one frame for layout
        LoadLevel();
    }

    private void Update()
    {
        // Goal bobbing animation
        if (goalRT != null && !isComplete)
        {
            goalBobTime += Time.deltaTime;
            float bob = Mathf.Sin(goalBobTime * 3f) * 5f;
            var pos = CellToLocal(goalCell);
            goalRT.anchoredPosition = pos + new Vector2(0, bob);
        }

        // Direct touch/mouse input
        if (isComplete) return;
        HandleInput();
    }

    private void HandleInput()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                if (IsTouchOnPlayer(touch.position))
                    isDragging = true;
            }
            else if ((touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary) && isDragging)
            {
                MovePlayerTowardFinger(touch.position);
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                isDragging = false;
            }
        }
        else
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (IsTouchOnPlayer(Input.mousePosition))
                    isDragging = true;
            }
            else if (Input.GetMouseButton(0) && isDragging)
            {
                MovePlayerTowardFinger(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
            }
        }
    }

    private bool IsTouchOnPlayer(Vector2 screenPos)
    {
        if (playerRT == null || canvas == null) return false;
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            playerRT, screenPos, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out localPoint);
        // Generous hit area (2x player size) so it's easy for kids to grab
        float halfW = playerRT.rect.width;
        float halfH = playerRT.rect.height;
        return Mathf.Abs(localPoint.x) <= halfW && Mathf.Abs(localPoint.y) <= halfH;
    }

    private void MovePlayerTowardFinger(Vector2 screenPos)
    {
        if (isComplete || mazeContainer == null) return;

        // Convert screen position to mazeContainer local coordinates
        Vector2 localPoint;
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(mazeContainer, screenPos, cam, out localPoint))
            return;

        // Determine which cell the finger is over
        Vector2Int fingerCell = LocalToCell(localPoint);

        // Move player step by step toward finger cell (max ~20 steps per frame to avoid lag)
        int steps = 0;
        while (playerCell != fingerCell && steps < 20)
        {
            // Try to move in the direction that reduces distance to finger most
            Vector2 fingerLocal = CellToLocal(fingerCell);
            Vector2 playerLocal = CellToLocal(playerCell);
            Vector2 diff = fingerLocal - playerLocal;

            // Try primary direction first, then secondary
            Vector2Int primaryDir, secondaryDir;
            if (Mathf.Abs(diff.x) >= Mathf.Abs(diff.y))
            {
                primaryDir = diff.x > 0 ? Vector2Int.right : Vector2Int.left;
                secondaryDir = diff.y > 0 ? Vector2Int.up : Vector2Int.down;
            }
            else
            {
                primaryDir = diff.y > 0 ? Vector2Int.up : Vector2Int.down;
                secondaryDir = diff.x > 0 ? Vector2Int.right : Vector2Int.left;
            }

            Vector2Int next = playerCell + primaryDir;
            if (next.x >= 0 && next.x < mazeWidth && next.y >= 0 && next.y < mazeHeight
                && CanMove(playerCell, next))
            {
                AddTrailSegment(playerCell, next);
                playerCell = next;
            }
            else
            {
                // Try secondary direction
                next = playerCell + secondaryDir;
                if (next.x >= 0 && next.x < mazeWidth && next.y >= 0 && next.y < mazeHeight
                    && CanMove(playerCell, next))
                {
                    AddTrailSegment(playerCell, next);
                    playerCell = next;
                }
                else
                {
                    break; // Blocked in both directions
                }
            }
            steps++;
        }

        playerRT.anchoredPosition = CellToLocal(playerCell);

        // Check goal
        if (playerCell == goalCell)
        {
            isComplete = true;
            if (idleBounceRoutine != null) StopCoroutine(idleBounceRoutine);
            StartCoroutine(OnReachedGoal());
        }
    }

    // ── level loading ────────────────────────────────────────────────
    private void LoadLevel()
    {
        ClearMaze();
        isComplete = false;
        goalBobTime = 0f;

        var def = Mazes[currentLevel % Mazes.Length];
        mazeWidth = def.w;
        mazeHeight = def.h;

        // Parse walls from definition
        ParseMaze(def);

        // Validate and fix walls to ensure solvability
        EnsureSolvable(def.start, def.goal);

        playerCell = def.start;
        goalCell = def.goal;

        DrawMaze();
        PlacePlayerAndGoal();

        // Random animal
        Sprite animalSprite = PickRandomAnimalSprite();
        if (playerImage != null && animalSprite != null)
        {
            playerImage.sprite = animalSprite;
            playerImage.preserveAspect = true;
            playerImage.color = Color.white;
            playerImage.gameObject.SetActive(true);
        }

        // Start idle bounce
        if (idleBounceRoutine != null) StopCoroutine(idleBounceRoutine);
        idleBounceRoutine = StartCoroutine(IdleBounce());
    }

    private void ParseMaze(MazeDef def)
    {
        hWalls = new bool[def.w, def.h + 1];
        vWalls = new bool[def.w + 1, def.h];

        for (int y = 0; y <= def.h; y++)
        {
            if (y < def.hWalls.Length)
            {
                for (int x = 0; x < def.w && x < def.hWalls[y].Length; x++)
                    hWalls[x, y] = def.hWalls[y][x] == '1';
            }
            else
            {
                for (int x = 0; x < def.w; x++) hWalls[x, y] = true;
            }
        }

        for (int y = 0; y < def.h; y++)
        {
            if (y < def.vWalls.Length)
            {
                for (int x = 0; x <= def.w && x < def.vWalls[y].Length; x++)
                    vWalls[x, y] = def.vWalls[y][x] == '1';
            }
            else
            {
                for (int x = 0; x <= def.w; x++) vWalls[x, y] = true;
            }
        }
    }

    private void EnsureSolvable(Vector2Int start, Vector2Int goal)
    {
        // BFS to check if path exists; if not, carve one
        bool[,] visited = new bool[mazeWidth, mazeHeight];
        Vector2Int[,] parent = new Vector2Int[mazeWidth, mazeHeight];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        for (int x = 0; x < mazeWidth; x++)
            for (int y = 0; y < mazeHeight; y++)
                parent[x, y] = new Vector2Int(-1, -1);

        visited[start.x, start.y] = true;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur == goal) return; // Path exists

            Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            foreach (var d in dirs)
            {
                var n = cur + d;
                if (n.x >= 0 && n.x < mazeWidth && n.y >= 0 && n.y < mazeHeight
                    && !visited[n.x, n.y] && CanMove(cur, n))
                {
                    visited[n.x, n.y] = true;
                    parent[n.x, n.y] = cur;
                    queue.Enqueue(n);
                }
            }
        }

        // No path found — carve a random walk from start to goal
        var path = new List<Vector2Int>();
        Vector2Int pos = start;
        bool[,] cvisited = new bool[mazeWidth, mazeHeight];
        cvisited[pos.x, pos.y] = true;

        while (pos != goal)
        {
            // Move toward goal with some randomness
            List<Vector2Int> candidates = new List<Vector2Int>();
            Vector2Int[] allDirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            foreach (var d in allDirs)
            {
                var n = pos + d;
                if (n.x >= 0 && n.x < mazeWidth && n.y >= 0 && n.y < mazeHeight && !cvisited[n.x, n.y])
                    candidates.Add(n);
            }
            if (candidates.Count == 0) break;

            // Prefer direction toward goal
            candidates.Sort((a, b) =>
            {
                float da = Mathf.Abs(a.x - goal.x) + Mathf.Abs(a.y - goal.y);
                float db = Mathf.Abs(b.x - goal.x) + Mathf.Abs(b.y - goal.y);
                return da.CompareTo(db);
            });

            Vector2Int next = Random.value < 0.7f ? candidates[0] : candidates[Random.Range(0, candidates.Count)];
            RemoveWallBetween(pos, next);
            cvisited[next.x, next.y] = true;
            pos = next;
        }
    }

    private void RemoveWallBetween(Vector2Int a, Vector2Int b)
    {
        Vector2Int diff = b - a;
        if (diff.x == 1) vWalls[b.x, a.y] = false;
        else if (diff.x == -1) vWalls[a.x, a.y] = false;
        else if (diff.y == 1) hWalls[a.x, b.y] = false;
        else if (diff.y == -1) hWalls[a.x, a.y] = false;
    }

    private void ClearMaze()
    {
        foreach (var obj in mazeObjects)
            if (obj != null) Destroy(obj);
        mazeObjects.Clear();

        foreach (var obj in trailObjects)
            if (obj != null) Destroy(obj);
        trailObjects.Clear();
        trailCells.Clear();
    }

    // ── drawing ──────────────────────────────────────────────────────
    private void DrawMaze()
    {
        float containerW = mazeContainer.rect.width;
        float containerH = mazeContainer.rect.height;

        if (containerW < 1f || containerH < 1f)
        {
            Debug.LogError($"MazeController: container size is {containerW}x{containerH} — layout not ready!");
            return;
        }

        cellW = containerW / mazeWidth;
        cellH = containerH / mazeHeight;
        mazeOffsetX = -containerW / 2f;
        mazeOffsetY = -containerH / 2f;

        float wallThick = Mathf.Max(8f, Mathf.Min(cellW, cellH) * 0.12f);

        // Draw path cells
        for (int x = 0; x < mazeWidth; x++)
        {
            for (int y = 0; y < mazeHeight; y++)
            {
                var cellGO = CreateRect(mazeContainer, $"Cell_{x}_{y}",
                    CellToLocal(new Vector2Int(x, y)),
                    new Vector2(cellW + 1, cellH + 1), pathColor);
                cellGO.GetComponent<Image>().raycastTarget = false;
                mazeObjects.Add(cellGO);
            }
        }

        // Draw horizontal walls (thick, rounded)
        for (int x = 0; x < mazeWidth; x++)
        {
            for (int y = 0; y <= mazeHeight; y++)
            {
                if (!hWalls[x, y]) continue;
                var pos = new Vector2(mazeOffsetX + x * cellW + cellW / 2f, mazeOffsetY + y * cellH);
                var wallGO = CreateRoundedWall(mazeContainer, $"HW_{x}_{y}", pos,
                    new Vector2(cellW + wallThick, wallThick));
                mazeObjects.Add(wallGO);
            }
        }

        // Draw vertical walls (thick, rounded)
        for (int x = 0; x <= mazeWidth; x++)
        {
            for (int y = 0; y < mazeHeight; y++)
            {
                if (!vWalls[x, y]) continue;
                var pos = new Vector2(mazeOffsetX + x * cellW, mazeOffsetY + y * cellH + cellH / 2f);
                var wallGO = CreateRoundedWall(mazeContainer, $"VW_{x}_{y}", pos,
                    new Vector2(wallThick, cellH + wallThick));
                mazeObjects.Add(wallGO);
            }
        }
    }

    private GameObject CreateRoundedWall(RectTransform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = wallColor;
        img.raycastTarget = false;
        return go;
    }

    private void PlacePlayerAndGoal()
    {
        float pSize = Mathf.Min(cellW, cellH) * 0.9f;

        if (playerRT != null)
        {
            playerRT.sizeDelta = new Vector2(pSize, pSize);
            playerRT.anchoredPosition = CellToLocal(playerCell);
            playerRT.localScale = Vector3.one;
        }

        if (goalRT != null)
        {
            float gSize = Mathf.Min(cellW, cellH) * 0.55f;
            goalRT.sizeDelta = new Vector2(gSize, gSize);
            goalRT.anchoredPosition = CellToLocal(goalCell);

            if (goalImage != null)
            {
                goalImage.color = new Color(1f, 0.84f, 0.2f);
                if (circleSprite != null) goalImage.sprite = circleSprite;
            }
        }

        // Star decoration on goal
        CreateGoalStar();
    }

    private void CreateGoalStar()
    {
        // Small white shine on goal
        if (goalRT == null) return;
        var shineGO = new GameObject("GoalShine");
        shineGO.transform.SetParent(goalRT, false);
        var shineRT = shineGO.AddComponent<RectTransform>();
        shineRT.anchorMin = new Vector2(0.15f, 0.55f);
        shineRT.anchorMax = new Vector2(0.45f, 0.85f);
        shineRT.offsetMin = Vector2.zero;
        shineRT.offsetMax = Vector2.zero;
        var shineImg = shineGO.AddComponent<Image>();
        if (circleSprite != null) shineImg.sprite = circleSprite;
        shineImg.color = new Color(1f, 1f, 1f, 0.5f);
        shineImg.raycastTarget = false;
        mazeObjects.Add(shineGO);
    }

    // ── coordinate helpers ───────────────────────────────────────────
    private Vector2 CellToLocal(Vector2Int cell)
    {
        return new Vector2(
            mazeOffsetX + cell.x * cellW + cellW / 2f,
            mazeOffsetY + cell.y * cellH + cellH / 2f);
    }

    private Vector2Int LocalToCell(Vector2 localPos)
    {
        int x = Mathf.FloorToInt((localPos.x - mazeOffsetX) / cellW);
        int y = Mathf.FloorToInt((localPos.y - mazeOffsetY) / cellH);
        return new Vector2Int(
            Mathf.Clamp(x, 0, mazeWidth - 1),
            Mathf.Clamp(y, 0, mazeHeight - 1));
    }

    private bool CanMove(Vector2Int from, Vector2Int to)
    {
        Vector2Int diff = to - from;
        if (Mathf.Abs(diff.x) + Mathf.Abs(diff.y) != 1) return false;

        if (diff.x == 1) return !vWalls[to.x, from.y];
        if (diff.x == -1) return !vWalls[from.x, from.y];
        if (diff.y == 1) return !hWalls[from.x, to.y];
        if (diff.y == -1) return !hWalls[from.x, from.y];
        return false;
    }

    // ── trail ────────────────────────────────────────────────────────
    private void AddTrail(Vector2Int fromCell)
    {
        float lineThick = Mathf.Min(cellW, cellH) * 0.35f;

        // Place a cap (rounded joint) at the from-cell if first trail segment
        if (!trailCells.Contains(fromCell))
        {
            trailCells.Add(fromCell);
            CreateTrailCap(CellToLocal(fromCell), lineThick);
        }

        // Draw a segment from fromCell to playerCell's new position (the next cell)
        // Note: AddTrail is called BEFORE playerCell is updated, so the next cell
        // is playerCell + moveDir. We draw the segment after the move in MovePlayerTowardFinger.
    }

    private void AddTrailSegment(Vector2Int from, Vector2Int to)
    {
        float lineThick = Mathf.Min(cellW, cellH) * 0.35f;

        if (!trailCells.Contains(from))
        {
            trailCells.Add(from);
            CreateTrailCap(CellToLocal(from), lineThick);
        }

        Vector2 posA = CellToLocal(from);
        Vector2 posB = CellToLocal(to);
        Vector2 mid = (posA + posB) / 2f;

        Vector2Int diff = to - from;
        Vector2 size;
        if (diff.x != 0) // horizontal segment
            size = new Vector2(cellW + lineThick, lineThick);
        else // vertical segment
            size = new Vector2(lineThick, cellH + lineThick);

        var segGO = new GameObject("TrailSeg");
        segGO.transform.SetParent(trailContainer, false);
        var rt = segGO.AddComponent<RectTransform>();
        rt.anchoredPosition = mid;
        rt.sizeDelta = size;
        var img = segGO.AddComponent<Image>();
        img.color = trailColor;
        img.raycastTarget = false;
        trailObjects.Add(segGO);

        // Cap at destination
        if (!trailCells.Contains(to))
        {
            trailCells.Add(to);
            CreateTrailCap(CellToLocal(to), lineThick);
        }
    }

    private void CreateTrailCap(Vector2 pos, float size)
    {
        var capGO = new GameObject("TrailCap");
        capGO.transform.SetParent(trailContainer, false);
        var rt = capGO.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);
        var img = capGO.AddComponent<Image>();
        if (circleSprite != null) img.sprite = circleSprite;
        img.color = trailColor;
        img.raycastTarget = false;
        trailObjects.Add(capGO);
    }

    // ── idle bounce ──────────────────────────────────────────────────
    private IEnumerator IdleBounce()
    {
        while (!isComplete)
        {
            float t = 0f;
            float dur = 0.6f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float bounce = 1f + 0.05f * Mathf.Sin(t / dur * Mathf.PI * 2f);
                if (playerRT != null)
                    playerRT.localScale = Vector3.one * bounce;
                yield return null;
            }
            if (playerRT != null)
                playerRT.localScale = Vector3.one;
            yield return new WaitForSeconds(0.3f);
        }
    }

    // ── success ──────────────────────────────────────────────────────
    private IEnumerator OnReachedGoal()
    {
        ConfettiController.Instance.Play();
        // Jump animation
        yield return JumpAnimation(playerRT);

        // Sparkles from goal
        SpawnSuccessSparkles();

        // Flash trail
        yield return FlashTrail();

        yield return new WaitForSeconds(0.8f);

        // Next level
        if (!GameCompletionBridge.WillJourneyNavigate)
        {
            currentLevel++;
            LoadLevel();
        }
    }

    private IEnumerator JumpAnimation(RectTransform rt)
    {
        Vector2 startPos = rt.anchoredPosition;
        Vector3 origScale = rt.localScale;
        float jumpHeight = cellH * 0.6f;

        // Jump up
        float dur = 0.2f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            rt.anchoredPosition = startPos + new Vector2(0, jumpHeight * Mathf.Sin(p * Mathf.PI));
            rt.localScale = origScale * (1f + 0.3f * Mathf.Sin(p * Mathf.PI));
            yield return null;
        }
        rt.anchoredPosition = startPos;
        rt.localScale = origScale;

        // Second smaller jump
        dur = 0.15f;
        t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            rt.anchoredPosition = startPos + new Vector2(0, jumpHeight * 0.4f * Mathf.Sin(p * Mathf.PI));
            rt.localScale = origScale * (1f + 0.15f * Mathf.Sin(p * Mathf.PI));
            yield return null;
        }
        rt.anchoredPosition = startPos;
        rt.localScale = origScale;
    }

    private void SpawnSuccessSparkles()
    {
        if (mazeContainer == null) return;
        Vector2 pos = CellToLocal(goalCell);

        for (int i = 0; i < 16; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(200f, 500f);
            float size = Random.Range(8f, 18f);
            float lifetime = Random.Range(0.4f, 0.7f);
            Color c;
            float r = Random.value;
            if (r < 0.3f) c = new Color(1f, 0.84f, 0.2f); // gold
            else if (r < 0.6f) c = Color.white;
            else c = new Color(1f, 0.6f, 0.2f); // orange

            var go = new GameObject("Sparkle");
            go.transform.SetParent(trailContainer, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            if (circleSprite != null) img.sprite = circleSprite;
            img.color = c;
            img.raycastTarget = false;

            StartCoroutine(AnimateSparkle(rt, img, angle, speed, lifetime));
        }
    }

    private IEnumerator AnimateSparkle(RectTransform rt, Image img,
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
            float fade = 1f - (t / lifetime);
            rt.localScale = Vector3.one * fade;
            img.color = new Color(startColor.r, startColor.g, startColor.b, fade);
            yield return null;
        }
        Destroy(rt.gameObject);
    }

    private IEnumerator FlashTrail()
    {
        // Briefly brighten all trail dots
        Color bright = new Color(trailColor.r * 1.3f, trailColor.g * 1.3f, trailColor.b * 1.3f, 1f);
        foreach (var obj in trailObjects)
        {
            if (obj == null) continue;
            var img = obj.GetComponent<Image>();
            if (img != null) img.color = bright;
        }
        yield return new WaitForSeconds(0.3f);
        foreach (var obj in trailObjects)
        {
            if (obj == null) continue;
            var img = obj.GetComponent<Image>();
            if (img != null) img.color = trailColor;
        }
    }

    // ── helpers ──────────────────────────────────────────────────────
    private GameObject CreateRect(RectTransform parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    private Sprite PickRandomAnimalSprite()
    {
        var game = GameContext.CurrentGame;
        if (game != null && game.subItems != null && game.subItems.Count > 0)
        {
            int index;
            if (game.subItems.Count == 1) index = 0;
            else
            {
                do { index = Random.Range(0, game.subItems.Count); }
                while (index == lastAnimalIndex);
            }
            lastAnimalIndex = index;
            var item = game.subItems[index];
            return item.thumbnail != null ? item.thumbnail : item.contentAsset;
        }
        return null;
    }

    public void OnHomePressed() => NavigationManager.GoToMainMenu();
    public void OnRestartPressed() => LoadLevel();
}
