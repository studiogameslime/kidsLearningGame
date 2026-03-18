using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the Tower Builder (בנה את המגדל) game.
/// Shows a reference tower on the left, silhouette slots on the right,
/// and a palette of draggable bricks at the bottom.
/// Player drags bricks to match the reference tower.
///
/// Difficulty: 0=Easy (3-4 bricks), 1=Medium (6-8), 2=Hard (10-12), 3=Very Hard (15+).
/// </summary>
public class TowerBuilderController : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform playArea;

    [Header("Sprites")]
    [HideInInspector] public List<string> spriteKeys = new List<string>();
    [HideInInspector] public List<Sprite> spriteValues = new List<Sprite>();
    public Sprite roundedRectSprite;

    [Header("Settings")]
    public int difficulty;

    // ── runtime state ────────────────────────────────────────────
    private Canvas canvas;
    private Dictionary<string, Sprite> spriteLookup;
    private int currentLevelIndex;
    private TowerLevel currentLevel;
    private List<GameObject> spawnedObjects = new List<GameObject>();
    private List<SlotData> buildSlots = new List<SlotData>();
    private List<DraggableBrick> paletteBricks = new List<DraggableBrick>();
    private GameObject[] refBrickObjects; // reference tower bricks, indexed by brick index
    private int placedCount;
    private bool isComplete;

    // Structural support graph: for each brick index, which brick indices must be placed first
    private List<int>[] brickSupporters;

    // Build-order and guidance
    private int wrongAttemptCount;
    private Coroutine highlightCoroutine;
    private GameStatsCollector _stats;

    // Tower layout calculation cache
    private float studW;
    private float rowH;

    // Row overlap: upper brick overlaps lower brick's studs by this fraction of rowH.
    private const float ROW_OVERLAP = 0.15f;

    // Ground line: the Y anchor fraction in playArea where the grass top is.
    // Background groundLayer1 top anchor = 0.35 of full canvas, but playArea
    // starts at y=0 (below 80px header). The ground top relative to playArea
    // is approximately at 28% (accounting for header offset).
    private const float GROUND_Y_FRAC = 0.28f;

    private struct SlotData
    {
        public string brickType;
        public string color;
        public RectTransform slotRT; // the silhouette RectTransform — source of truth for position
        public bool filled;
        public GameObject silhouetteGO;
        public int brickIndex; // index into currentLevel.bricks
    }

    // ── lifecycle ────────────────────────────────────────────────
    private void Start()
    {
        canvas = GetComponentInParent<Canvas>();

        // Build sprite lookup
        spriteLookup = new Dictionary<string, Sprite>();
        for (int i = 0; i < spriteKeys.Count && i < spriteValues.Count; i++)
            spriteLookup[spriteKeys[i]] = spriteValues[i];

        // Get difficulty from GameContext
        if (GameContext.CurrentSelection != null)
        {
            int d;
            if (int.TryParse(GameContext.CurrentSelection.categoryKey, out d))
                difficulty = d;
        }

        string gameId = GameContext.CurrentGame != null ? GameContext.CurrentGame.id : "towerbuilder";
        _stats = new GameStatsCollector(gameId);
        if (GameCompletionBridge.Instance != null)
            GameCompletionBridge.Instance.ActiveCollector = _stats;

        StartCoroutine(StartAfterLayout());
    }

    private IEnumerator StartAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(playArea);

        PickLevel();
        LoadLevel();
    }

    private void PickLevel()
    {
        var levels = TowerLevels.GetLevelsForDifficulty(difficulty);
        currentLevelIndex = levels[Random.Range(0, levels.Count)];
    }

    // ── level loading ────────────────────────────────────────────
    private void LoadLevel()
    {
        ClearAll();

        currentLevel = TowerLevels.All[currentLevelIndex];
        placedCount = 0;
        isComplete = false;
        wrongAttemptCount = 0;
        ClearHighlights();

        // Build structural support graph
        ComputeSupportGraph(currentLevel);

        // Calculate tower bounds in grid units
        float maxStudX = 0f, maxRowY = 0f;
        foreach (var b in currentLevel.bricks)
        {
            float right = b.gridX + b.StudWidth;
            float top = b.gridY + b.HeightUnits;
            if (right > maxStudX) maxStudX = right;
            if (top > maxRowY) maxRowY = top;
        }

        float areaW = playArea.rect.width;
        float areaH = playArea.rect.height;

        // Tower display areas — generous sizing so towers are the focal point
        float towerAreaW = areaW * 0.40f;
        float towerAreaH = areaH * 0.62f;

        // studW = horizontal pixel size per stud unit
        studW = Mathf.Min(towerAreaW / maxStudX, towerAreaH / maxRowY);
        studW = Mathf.Min(studW, 120f);
        // rowH = vertical step between rows (shorter than brick height for overlap)
        rowH = studW * (1f - ROW_OVERLAP);

        // Pixel dimensions of the tower footprint
        float towerPixelW = maxStudX * studW;
        float towerPixelH = maxRowY * rowH;

        // Ground line in playArea pixel coords
        float groundY = areaH * GROUND_Y_FRAC;

        // Reference tower origin — bottom-left of tower sits on ground
        float refCenterX = areaW * 0.25f;
        float refOriginX = refCenterX - towerPixelW / 2f;
        float refOriginY = groundY;

        // Build tower origin — same ground level, right side
        float buildCenterX = areaW * 0.75f;
        float buildOriginX = buildCenterX - towerPixelW / 2f;
        float buildOriginY = groundY;

        // Ground platform under each tower
        CreateGroundPlatform(refOriginX, refOriginY, towerPixelW);
        CreateGroundPlatform(buildOriginX, buildOriginY, towerPixelW);

        // Labels above towers
        CreateLabel("\u05D3\u05D5\u05D2\u05DE\u05D4",
            refCenterX, refOriginY + towerPixelH + 20f);
        CreateLabel("\u05D1\u05E0\u05D4 \u05DB\u05D0\u05DF",
            buildCenterX, buildOriginY + towerPixelH + 20f);

        // Render reference tower (fully colored, sorted bottom-to-top)
        BuildTowerDisplay(currentLevel, refOriginX, refOriginY);

        // Render build area (silhouettes, sorted bottom-to-top)
        BuildSilhouettes(currentLevel, buildOriginX, buildOriginY);

        // Create palette
        BuildPalette(currentLevel);
    }

    private void ClearAll()
    {
        foreach (var go in spawnedObjects)
            if (go != null) Destroy(go);
        spawnedObjects.Clear();
        buildSlots.Clear();
        paletteBricks.Clear();
    }

    /// <summary>
    /// Returns brick indices sorted by gridY ascending (bottom first),
    /// then by gridX ascending for proper rendering order.
    /// </summary>
    private List<int> GetSortedBrickIndices(TowerLevel level)
    {
        var indices = new List<int>();
        for (int i = 0; i < level.bricks.Length; i++) indices.Add(i);
        indices.Sort((a, b) =>
        {
            float dy = level.bricks[a].gridY - level.bricks[b].gridY;
            if (dy != 0f) return dy < 0 ? -1 : 1;
            return level.bricks[a].gridX - level.bricks[b].gridX;
        });
        return indices;
    }

    // ── tower rendering ──────────────────────────────────────────
    private void BuildTowerDisplay(TowerLevel level, float originX, float originY)
    {
        refBrickObjects = new GameObject[level.bricks.Length];
        var sorted = GetSortedBrickIndices(level);
        foreach (int idx in sorted)
        {
            var b = level.bricks[idx];
            float x = originX + b.gridX * studW;
            float y = originY + b.gridY * rowH;
            float w = b.StudWidth * studW;
            // Brick rendered at full sprite height (studW-based) for overlap
            float h = b.HeightUnits * studW;

            Sprite sprite = GetSprite(b.SpriteKey);
            var go = CreateBrickImage("Ref_" + b.SpriteKey, sprite,
                new Vector2(x, y), new Vector2(w, h), Color.white);
            go.GetComponent<Image>().raycastTarget = false;
            refBrickObjects[idx] = go;
        }
    }

    private void BuildSilhouettes(TowerLevel level, float originX, float originY)
    {
        // Create all silhouettes first, store them for reordering
        var silhouettes = new GameObject[level.bricks.Length];

        for (int i = 0; i < level.bricks.Length; i++)
        {
            var b = level.bricks[i];
            float x = originX + b.gridX * studW;
            float y = originY + b.gridY * rowH;
            float w = b.StudWidth * studW;
            float h = b.HeightUnits * studW;

            // Alternate tint by row to prevent adjacent blending
            float rowParity = (Mathf.RoundToInt(b.gridY) % 2 == 0) ? 0.30f : 0.22f;
            Color silColor = new Color(0.50f, 0.52f, 0.58f, rowParity);

            Sprite sprite = GetSprite(b.SpriteKey);
            var go = CreateBrickImage("Slot_" + i, sprite,
                new Vector2(x, y), new Vector2(w, h), silColor);
            go.GetComponent<Image>().raycastTarget = false;

            // Outline border behind silhouette
            var outline = new GameObject("Outline");
            outline.transform.SetParent(go.transform, false);
            var olRT = outline.AddComponent<RectTransform>();
            olRT.anchorMin = new Vector2(-0.02f, -0.03f);
            olRT.anchorMax = new Vector2(1.02f, 1.03f);
            olRT.offsetMin = Vector2.zero;
            olRT.offsetMax = Vector2.zero;
            var olImg = outline.AddComponent<Image>();
            if (roundedRectSprite != null) olImg.sprite = roundedRectSprite;
            olImg.type = Image.Type.Sliced;
            olImg.color = new Color(0.40f, 0.42f, 0.50f, 0.20f);
            olImg.raycastTarget = false;
            outline.transform.SetAsFirstSibling();

            silhouettes[i] = go;

            var slot = new SlotData
            {
                brickType = b.brickType,
                color = b.color,
                slotRT = go.GetComponent<RectTransform>(),
                filled = false,
                silhouetteGO = go,
                brickIndex = i
            };
            buildSlots.Add(slot);
        }

        // Reorder siblings: lower bricks first so upper bricks render on top
        var sorted = GetSortedBrickIndices(level);
        foreach (int idx in sorted)
        {
            if (silhouettes[idx] != null)
                silhouettes[idx].transform.SetAsLastSibling();
        }
    }

    // ── palette ──────────────────────────────────────────────────
    private const int TWO_ROW_THRESHOLD = 6;

    private void BuildPalette(TowerLevel level)
    {
        float areaW = playArea.rect.width;
        float maxW = areaW * 0.92f;
        float spacing = 10f;
        float baseRowY = playArea.rect.height * 0.04f;

        // Shuffle brick order
        var indices = new List<int>();
        for (int i = 0; i < level.bricks.Length; i++) indices.Add(i);
        ShuffleList(indices);

        // Split into rows: >6 bricks → two rows, otherwise one
        var rows = new List<List<int>>();
        if (indices.Count > TWO_ROW_THRESHOLD)
        {
            int half = Mathf.CeilToInt(indices.Count / 2f);
            rows.Add(indices.GetRange(0, half));
            rows.Add(indices.GetRange(half, indices.Count - half));
        }
        else
        {
            rows.Add(indices);
        }

        // Compute palette scale: find the widest row and scale so it fits
        float palScale = 1.0f;
        foreach (var row in rows)
        {
            float rowW = 0f;
            foreach (int idx in row)
                rowW += level.bricks[idx].StudWidth * studW + spacing;
            rowW -= spacing;
            if (rowW > maxW)
                palScale = Mathf.Min(palScale, maxW / rowW);
        }

        // Find the tallest brick (scaled) to determine row height
        float maxBrickH = 0f;
        foreach (var b in level.bricks)
        {
            float h = b.HeightUnits * studW * palScale;
            if (h > maxBrickH) maxBrickH = h;
        }

        float rowSpacing = 8f;

        // Place each row, bottom row first (row index 0 = bottom)
        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            float rowY = baseRowY + r * (maxBrickH + rowSpacing);

            // Compute this row's total width for centering
            float rowW = 0f;
            foreach (int idx in row)
                rowW += level.bricks[idx].StudWidth * studW * palScale + spacing;
            rowW -= spacing;

            float curX = (areaW - rowW) / 2f;

            foreach (int idx in row)
            {
                var b = level.bricks[idx];
                float w = b.StudWidth * studW * palScale;
                float h = b.HeightUnits * studW * palScale;

                Sprite sprite = GetSprite(b.SpriteKey);

                var go = new GameObject("Pal_" + b.SpriteKey);
                go.transform.SetParent(playArea, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(curX, rowY);
                rt.sizeDelta = new Vector2(w, h);
                rt.localScale = Vector3.one;

                var img = go.AddComponent<Image>();
                if (sprite != null) img.sprite = sprite;
                img.color = Color.white;
                img.raycastTarget = true;

                var brick = go.AddComponent<DraggableBrick>();
                brick.Init(b.brickType, b.color, canvas,
                    Vector3.one, Vector3.one, OnBrickDropped);
                brick.SaveHomePosition();

                spawnedObjects.Add(go);
                paletteBricks.Add(brick);
                curX += w + spacing;
            }
        }
    }

    // ── structural support logic ──────────────────────────────────

    /// <summary>
    /// Builds the support graph: for each brick, determines which other bricks
    /// must be placed before it based on physical structural support.
    ///
    /// Brick B supports brick A if:
    ///   1. B's top edge meets A's bottom edge (B.gridY + B.HeightUnits == A.gridY)
    ///   2. They overlap horizontally (stud ranges intersect)
    ///
    /// Ground-level bricks (gridY == 0 or no supporters) need no support.
    /// </summary>
    private void ComputeSupportGraph(TowerLevel level)
    {
        int count = level.bricks.Length;
        brickSupporters = new List<int>[count];

        for (int a = 0; a < count; a++)
        {
            brickSupporters[a] = new List<int>();
            var brick = level.bricks[a];

            // Ground-level bricks need no support
            if (brick.gridY <= 0f) continue;

            float brickLeft = brick.gridX;
            float brickRight = brick.gridX + brick.StudWidth;

            for (int b = 0; b < count; b++)
            {
                if (b == a) continue;
                var below = level.bricks[b];

                // Check if below's top edge meets this brick's bottom edge
                float belowTop = below.gridY + below.HeightUnits;
                if (!Mathf.Approximately(belowTop, brick.gridY)) continue;

                // Check horizontal overlap
                float belowLeft = below.gridX;
                float belowRight = below.gridX + below.StudWidth;
                if (brickLeft < belowRight && belowLeft < brickRight)
                    brickSupporters[a].Add(b);
            }
        }
    }

    /// <summary>
    /// A slot is ready when ALL of its structural supporters are already placed.
    /// </summary>
    private bool IsSlotReady(int slotIndex)
    {
        int brickIdx = buildSlots[slotIndex].brickIndex;

        foreach (int supporter in brickSupporters[brickIdx])
        {
            if (!IsBrickPlaced(supporter))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Checks if a given brick index has been placed (its slot is filled).
    /// </summary>
    private bool IsBrickPlaced(int brickIndex)
    {
        for (int i = 0; i < buildSlots.Count; i++)
        {
            if (buildSlots[i].brickIndex == brickIndex)
                return buildSlots[i].filled;
        }
        return false;
    }

    /// <summary>
    /// Returns slot indices for all currently valid (supported + unfilled) bricks.
    /// Multiple bricks can be valid simultaneously when their supports are independent.
    /// </summary>
    private List<int> GetNextValidSlots()
    {
        var result = new List<int>();
        for (int i = 0; i < buildSlots.Count; i++)
        {
            if (buildSlots[i].filled) continue;
            if (IsSlotReady(i))
                result.Add(i);
        }
        return result;
    }

    // ── drag-drop handling ───────────────────────────────────────
    private void OnBrickDropped(DraggableBrick brick)
    {
        if (isComplete) return;

        var brickRT = brick.GetComponent<RectTransform>();

        // Find nearest matching unfilled slot using world position (pivot-independent)
        int bestSlot = -1;
        float bestDist = float.MaxValue;
        float snapThreshold = studW * 2.5f;

        Vector3 brickWorldCenter = brickRT.TransformPoint(
            new Vector3(brickRT.rect.width * 0.5f, brickRT.rect.height * 0.5f, 0));

        for (int i = 0; i < buildSlots.Count; i++)
        {
            var slot = buildSlots[i];
            if (slot.filled) continue;
            if (slot.brickType != brick.brickType || slot.color != brick.brickColor) continue;

            var slotRT = slot.slotRT;
            Vector3 slotWorldCenter = slotRT.TransformPoint(
                new Vector3(slotRT.rect.width * 0.5f, slotRT.rect.height * 0.5f, 0));

            float dist = Vector2.Distance(brickWorldCenter, slotWorldCenter);

            if (dist < snapThreshold && dist < bestDist)
            {
                bestDist = dist;
                bestSlot = i;
            }
        }

        if (bestSlot >= 0 && IsSlotReady(bestSlot))
        {
            // Successful placement
            ClearHighlights();
            wrongAttemptCount = 0;

            var slot = buildSlots[bestSlot];
            slot.filled = true;
            buildSlots[bestSlot] = slot;

            // Hide silhouette
            if (slot.silhouetteGO != null)
                slot.silhouetteGO.SetActive(false);

            // Snap: copy the exact transform from the slot
            brick.SnapToSlot(slot.slotRT, this);
            _stats?.RecordCorrect();
            placedCount++;

            if (placedCount >= currentLevel.bricks.Length)
                StartCoroutine(CompletionSequence());
        }
        else
        {
            // Wrong placement
            _stats?.RecordMistake();
            wrongAttemptCount++;
            brick.ReturnToStart(this);

            if (wrongAttemptCount >= 3)
                ShowGuidanceHighlights();
        }
    }

    // ── guidance highlights ──────────────────────────────────────

    private void ShowGuidanceHighlights()
    {
        ClearHighlights();
        highlightCoroutine = StartCoroutine(PulseHighlights());
    }

    private void ClearHighlights()
    {
        if (highlightCoroutine != null)
        {
            StopCoroutine(highlightCoroutine);
            highlightCoroutine = null;
        }

        // Reset any highlighted silhouettes and reference bricks to original color
        for (int i = 0; i < buildSlots.Count; i++)
        {
            var slot = buildSlots[i];
            if (slot.filled || slot.silhouetteGO == null) continue;
            var img = slot.silhouetteGO.GetComponent<Image>();
            if (img != null)
            {
                float rowParity = (Mathf.RoundToInt(currentLevel.bricks[slot.brickIndex].gridY) % 2 == 0) ? 0.30f : 0.22f;
                img.color = new Color(0.50f, 0.52f, 0.58f, rowParity);
            }
        }

        if (refBrickObjects != null)
        {
            foreach (var go in refBrickObjects)
            {
                if (go == null) continue;
                var img = go.GetComponent<Image>();
                if (img != null) img.color = Color.white;
            }
        }
    }

    /// <summary>
    /// Pulses one valid ghost slot and its corresponding reference tower brick
    /// with a gentle glow to guide the player. When multiple valid slots exist,
    /// highlights one (the lowest, leftmost) to keep guidance clear.
    /// </summary>
    private IEnumerator PulseHighlights()
    {
        var nextSlots = GetNextValidSlots();
        if (nextSlots.Count == 0) yield break;

        // Pick one slot to highlight — lowest gridY, then leftmost gridX
        nextSlots.Sort((a, b) =>
        {
            var ba = currentLevel.bricks[buildSlots[a].brickIndex];
            var bb = currentLevel.bricks[buildSlots[b].brickIndex];
            float dy = ba.gridY - bb.gridY;
            if (dy != 0f) return dy < 0 ? -1 : 1;
            return ba.gridX - bb.gridX;
        });
        nextSlots = new List<int> { nextSlots[0] };

        // Gather the objects to pulse
        var slotImages = new List<Image>();
        var slotBaseColors = new List<Color>();
        var refImages = new List<Image>();

        foreach (int si in nextSlots)
        {
            var slot = buildSlots[si];
            if (slot.silhouetteGO != null)
            {
                var img = slot.silhouetteGO.GetComponent<Image>();
                if (img != null)
                {
                    slotImages.Add(img);
                    slotBaseColors.Add(img.color);
                }
            }

            int bi = slot.brickIndex;
            if (refBrickObjects != null && bi < refBrickObjects.Length && refBrickObjects[bi] != null)
            {
                var img = refBrickObjects[bi].GetComponent<Image>();
                if (img != null)
                    refImages.Add(img);
            }
        }

        Color highlightColor = new Color(1f, 0.92f, 0.3f, 0.7f); // warm yellow glow

        while (true)
        {
            float t = Time.time * 3f; // 3 Hz pulse
            float pulse = (Mathf.Sin(t) + 1f) * 0.5f; // 0..1

            // Pulse ghost slots: blend between base color and highlight
            for (int i = 0; i < slotImages.Count; i++)
            {
                if (slotImages[i] == null) continue;
                slotImages[i].color = Color.Lerp(slotBaseColors[i], highlightColor, pulse * 0.7f);
            }

            // Pulse reference bricks: subtle brightness oscillation
            foreach (var img in refImages)
            {
                if (img == null) continue;
                float brightness = 1f + pulse * 0.3f; // 1.0 to 1.3
                img.color = new Color(brightness, brightness, brightness, 1f);
            }

            yield return null;
        }
    }

    // ── completion ───────────────────────────────────────────────
    private IEnumerator CompletionSequence()
    {
        isComplete = true;
        yield return new WaitForSeconds(0.3f);

        ConfettiController.Instance.Play();

        // Sort placed bricks by Y position (bottom to top) for sparkle wave
        var placedBricks = new List<DraggableBrick>();
        foreach (var brick in paletteBricks)
            if (brick.isPlaced) placedBricks.Add(brick);

        placedBricks.Sort((a, b) =>
        {
            float ya = a.GetComponent<RectTransform>().anchoredPosition.y;
            float yb = b.GetComponent<RectTransform>().anchoredPosition.y;
            return ya.CompareTo(yb);
        });

        // Sparkle wave: flash each brick white from bottom to top
        foreach (var brick in placedBricks)
        {
            StartCoroutine(SparkleFlash(brick.GetComponent<Image>()));
            yield return new WaitForSeconds(0.06f);
        }

        yield return new WaitForSeconds(0.4f);

        // Gentle whole-tower bounce: all placed bricks scale together
        yield return StartCoroutine(TowerBounce(placedBricks));

        yield return new WaitForSeconds(1.2f);

        // Next level
        int nextLevel = currentLevelIndex + 1;
        if (nextLevel < TowerLevels.All.Length)
        {
            currentLevelIndex = nextLevel;
            LoadLevel();
        }
        else
        {
            PickLevel();
            LoadLevel();
        }
    }

    /// <summary>
    /// Flash a brick briefly to white then back — a sparkle sweep effect.
    /// No scale change, so the tower structure stays stable.
    /// </summary>
    private IEnumerator SparkleFlash(Image img)
    {
        if (img == null) yield break;
        Color orig = img.color;

        // Flash to bright white
        float flashIn = 0.1f;
        float t = 0;
        while (t < flashIn)
        {
            t += Time.deltaTime;
            float p = t / flashIn;
            img.color = Color.Lerp(orig, Color.white, p * 0.6f);
            yield return null;
        }

        // Fade back to original
        float flashOut = 0.3f;
        t = 0;
        Color peak = img.color;
        while (t < flashOut)
        {
            t += Time.deltaTime;
            float p = t / flashOut;
            float ease = p * p; // ease-in for smooth settle
            img.color = Color.Lerp(peak, orig, ease);
            yield return null;
        }
        img.color = orig;
    }

    /// <summary>
    /// Gentle bounce: all placed bricks scale up slightly together then settle.
    /// Scaling is uniform and small so the tower looks like it "pops" with satisfaction
    /// without distorting the structure.
    /// </summary>
    private IEnumerator TowerBounce(List<DraggableBrick> bricks)
    {
        float dur = 0.5f;
        float t = 0;

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;

            // Elastic settle: overshoot to 1.06 then ease back to 1.0
            float scale;
            if (p < 0.3f)
            {
                // Rise to peak
                float rise = p / 0.3f;
                scale = 1f + 0.06f * Mathf.Sin(rise * Mathf.PI * 0.5f);
            }
            else
            {
                // Settle back with slight undershoot
                float settle = (p - 0.3f) / 0.7f;
                scale = 1f + 0.06f * Mathf.Exp(-settle * 5f) * Mathf.Cos(settle * Mathf.PI);
            }

            foreach (var brick in bricks)
            {
                if (brick == null) continue;
                brick.transform.localScale = Vector3.one * scale;
            }
            yield return null;
        }

        // Ensure all bricks end at exactly scale 1
        foreach (var brick in bricks)
        {
            if (brick == null) continue;
            brick.transform.localScale = Vector3.one;
        }
    }

    // ── helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Creates a brick Image with pivot at bottom-left (0,0).
    /// All tower bricks — reference, silhouettes, and placed pieces — use this same pivot
    /// so positions are directly comparable.
    /// </summary>
    private GameObject CreateBrickImage(string name, Sprite sprite,
        Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(playArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        if (sprite != null) img.sprite = sprite;
        img.color = color;

        spawnedObjects.Add(go);
        return go;
    }

    /// <summary>
    /// Creates a subtle ground platform strip under a tower base.
    /// </summary>
    private void CreateGroundPlatform(float originX, float originY, float towerW)
    {
        var shadow = new GameObject("GroundShadow");
        shadow.transform.SetParent(playArea, false);
        var shRT = shadow.AddComponent<RectTransform>();
        shRT.anchorMin = Vector2.zero;
        shRT.anchorMax = Vector2.zero;
        shRT.pivot = new Vector2(0f, 1f);
        shRT.anchoredPosition = new Vector2(originX - 6f, originY);
        shRT.sizeDelta = new Vector2(towerW + 12f, 8f);
        var shImg = shadow.AddComponent<Image>();
        shImg.color = new Color(0.15f, 0.30f, 0.12f, 0.35f);
        shImg.raycastTarget = false;
        spawnedObjects.Add(shadow);

        var plat = new GameObject("GroundPlatform");
        plat.transform.SetParent(playArea, false);
        var pRT = plat.AddComponent<RectTransform>();
        pRT.anchorMin = Vector2.zero;
        pRT.anchorMax = Vector2.zero;
        pRT.pivot = new Vector2(0f, 1f);
        pRT.anchoredPosition = new Vector2(originX - 4f, originY + 2f);
        pRT.sizeDelta = new Vector2(towerW + 8f, 5f);
        var pImg = plat.AddComponent<Image>();
        if (roundedRectSprite != null) pImg.sprite = roundedRectSprite;
        pImg.type = Image.Type.Sliced;
        pImg.color = new Color(0.28f, 0.52f, 0.25f, 0.50f);
        pImg.raycastTarget = false;
        spawnedObjects.Add(plat);
    }

    private void CreateLabel(string text, float centerX, float y)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(playArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(centerX, y);
        rt.sizeDelta = new Vector2(300, 50);

        var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
        HebrewText.SetText(tmp, text);
        tmp.fontSize = 28;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.color = new Color(0.95f, 0.95f, 1.0f);
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        // Drop shadow for readability
        var shadowGO = new GameObject("Shadow");
        shadowGO.transform.SetParent(go.transform, false);
        var sRT = shadowGO.AddComponent<RectTransform>();
        sRT.anchorMin = Vector2.zero;
        sRT.anchorMax = Vector2.one;
        sRT.offsetMin = Vector2.zero;
        sRT.offsetMax = Vector2.zero;
        sRT.anchoredPosition = new Vector2(1.5f, -1.5f);
        var sTMP = shadowGO.AddComponent<TMPro.TextMeshProUGUI>();
        HebrewText.SetText(sTMP, text);
        sTMP.fontSize = 28;
        sTMP.fontStyle = TMPro.FontStyles.Bold;
        sTMP.color = new Color(0.10f, 0.25f, 0.10f, 0.45f);
        sTMP.alignment = TMPro.TextAlignmentOptions.Center;
        sTMP.raycastTarget = false;
        shadowGO.transform.SetAsFirstSibling();

        spawnedObjects.Add(go);
    }

    private Sprite GetSprite(string key)
    {
        Sprite s;
        if (spriteLookup != null && spriteLookup.TryGetValue(key, out s))
            return s;
        return null;
    }

    private static void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }

    // ── navigation ──────────────────────────────────────────────
    public void OnHomePressed() => NavigationManager.GoToMainMenu();
}
