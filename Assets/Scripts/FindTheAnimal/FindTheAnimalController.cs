using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Find The Animal — structured landscape world with 3 depth zones.
///
/// Key rules:
/// - Animals NEVER overlap each other (strict grid placement with guaranteed spacing)
/// - Decorations may partially overlap animals but never block taps (raycastTarget=false)
/// - Correct tap → play real success animation → then fade out
/// - Only ONE target hints at a time after inactivity
/// - Generous tap zones for toddlers (age 3)
/// </summary>
public class FindTheAnimalController : BaseMiniGame
{
    [Header("UI")]
    public Image targetImage;
    public TextMeshProUGUI remainingText;

    [Header("World Layers")]
    public Image skyImage;
    public Image groundImage;
    public Image groundFrontImage;
    public RectTransform cloudLayer;
    public RectTransform backRowLayer;
    public RectTransform animalLayer;
    public RectTransform frontRowLayer;

    [Header("World Art — Clouds")]
    public Sprite[] cloudSprites;

    [Header("World Art — Decoration")]
    public Sprite[] treeSprites;
    public Sprite[] smallTreeSprites;
    public Sprite[] houseSprites;

    [Header("World Art — Hiding")]
    public Sprite[] bushSprites;
    public Sprite[] fenceSprites;

    // ── Layout Constants ──

    private const int TotalAnimals = 12;
    private const float AnimalVisualSize = 170f;
    private const float TapZonePadding = 50f;
    private const float RefW = 1920f;
    private const float RefH = 1000f;
    private const float GroundTop = 0.45f;

    // Zone Y ranges (pixels from bottom of world area)
    private const float BackRowMinY = 290f;
    private const float BackRowMaxY = 390f;
    private const float MiddleMinY = 90f;
    private const float MiddleMaxY = 260f;
    private const float FrontRowMinY = 5f;
    private const float FrontRowMaxY = 65f;

    private const int NumSlots = 8;

    // Hint system
    private const float HintDelay = 5f;
    private const float HintInterval = 3.0f;

    // ── Game State ──

    private SubItemData targetAnimal;
    private int targetCount;
    private int targetsFound;
    private bool isRoundActive;
    private int roundNumber;
    private float lastInteractionTime;
    private string[] _allowedAnimals;

    private List<GameObject> spawnedAnimals = new List<GameObject>();
    private List<int> targetIndices = new List<int>();
    private HashSet<int> foundIndices = new HashSet<int>();
    private List<GameObject> worldObjects = new List<GameObject>();
    private List<Coroutine> activeCoroutines = new List<Coroutine>();
    private Coroutine hintCoroutine;
    private int currentlyHintingIndex = -1;

    // ── Color Palettes ──

    private static readonly Color[] SkyColors = {
        HC("#B8DBF7"), HC("#A8D8EA"), HC("#C5E8F7"),
        HC("#E8F0FE"), HC("#D6ECFF"),
    };
    private static readonly Color[] GroundColors = {
        HC("#E8D5A3"), HC("#DECA94"), HC("#D4C08A"),
        HC("#E0CFA0"), HC("#D8C592"),
    };
    private static readonly Color[] GroundFrontColors = {
        HC("#F0E0B5"), HC("#EAD8A8"), HC("#E2D09E"),
        HC("#ECDCB0"), HC("#E5D4A5"),
    };

    private static float GetVisualScale(string key)
    {
        switch (key)
        {
            case "fish": return 1.3f;
            case "frog": return 1.2f;
            case "bird": return 1.15f;
            case "duck": return 1.15f;
            case "turtle": return 1.15f;
            case "chicken": return 1.1f;
            case "snake": return 1.1f;
            case "cat": return 1.05f;
            case "monkey": return 1.05f;
            case "giraffe": return 0.88f;
            case "elephant": return 0.92f;
            case "horse": return 0.95f;
            case "donkey": return 0.95f;
            case "zebra": return 0.95f;
            default: return 1.0f;
        }
    }

    // ── Base Mini Game Hooks ──

    protected override string GetFallbackGameId() => "findtheanimal";

    protected override void OnGameInit()
    {
        totalRounds = 1;
        isEndless = true;
        playWinSound = true;
        playConfettiOnRoundWin = true;
        delayBeforeNextRound = 0.6f;

        roundNumber = 0;

        // Apply difficulty — filter animal pool by tier
        _allowedAnimals = GameDifficultyConfig.GetAnimalPool(Difficulty);
        Debug.Log($"[Difficulty] Game=findtheanimal Level={Difficulty} Tier={GameDifficultyConfig.FindAnimalTier(Difficulty)} Pool={string.Join(",", _allowedAnimals)}");
    }

    protected override void OnRoundSetup()
    {
        isRoundActive = true;
        targetsFound = 0;
        lastInteractionTime = Time.time;
        foundIndices.Clear();
        targetIndices.Clear();

        var game = GameContext.CurrentGame;
        if (game == null || game.subItems == null || game.subItems.Count < 2)
        {
            Debug.LogError("FindTheAnimal: Not enough sub-items!");
            return;
        }

        targetCount = Random.Range(3, 6);
        int distractorCount = TotalAnimals - targetCount;

        // Filter pool by difficulty-allowed animals
        var pool = new List<SubItemData>();
        var allPool = new List<SubItemData>();
        foreach (var item in game.subItems)
        {
            allPool.Add(item);
            if (_allowedAnimals != null && _allowedAnimals.Length > 0)
            {
                foreach (var allowed in _allowedAnimals)
                {
                    if (item.categoryKey != null &&
                        item.categoryKey.Equals(allowed, System.StringComparison.OrdinalIgnoreCase))
                    {
                        pool.Add(item);
                        break;
                    }
                }
            }
        }
        // Fallback: if no allowed animals matched, use all
        if (pool.Count < 2) pool = allPool;
        Shuffle(pool);
        targetAnimal = pool[0];

        // Distractors: prefer same-tier animals, but use all animals if pool is too small
        var distractorSource = pool.Count >= 4 ? pool : allPool;
        var distractorTypes = new List<SubItemData>();
        foreach (var item in distractorSource)
            if (item.id != targetAnimal.id)
                distractorTypes.Add(item);
        Shuffle(distractorTypes);

        var toPlace = new List<(SubItemData data, bool isTarget)>();
        for (int i = 0; i < targetCount; i++)
            toPlace.Add((targetAnimal, true));
        for (int i = 0; i < distractorCount; i++)
            toPlace.Add((distractorTypes[i % distractorTypes.Count], false));
        Shuffle(toPlace);

        // Build world
        SetThemeColors();
        PlaceClouds();
        BuildBackRow();
        PlaceAnimals(toPlace);
        BuildFrontRow();

        // UI
        Sprite spr = targetAnimal.thumbnail ?? targetAnimal.contentAsset;
        if (targetImage != null && spr != null)
        {
            targetImage.sprite = spr;
            targetImage.preserveAspect = true;
        }
        UpdateRemainingText();

        // Announce animal
        string key = targetAnimal.categoryKey;
        if (!string.IsNullOrEmpty(key))
            SoundLibrary.PlayAnimalName(char.ToUpper(key[0]) + key.Substring(1));

        hintCoroutine = StartCoroutine(HintLoop());

        // Position tutorial hand on one of the target animals
        PositionTutorialHand();

        roundNumber++;
    }

    protected override void OnRoundCleanup()
    {
        if (hintCoroutine != null) { StopCoroutine(hintCoroutine); hintCoroutine = null; }
        currentlyHintingIndex = -1;
        foreach (var c in activeCoroutines) if (c != null) StopCoroutine(c);
        activeCoroutines.Clear();
        foreach (var go in spawnedAnimals) if (go != null) Destroy(go);
        spawnedAnimals.Clear();
        foreach (var go in worldObjects) if (go != null) Destroy(go);
        worldObjects.Clear();
    }

    protected override void OnBeforeComplete()
    {
        Stats?.SetCustom("targetsFound", targetCount);
    }

    protected override IEnumerator OnAfterComplete()
    {
        // Wait for the last animal's success animation to finish
        yield return new WaitForSeconds(1.5f);

        // Fade remaining animals
        foreach (var animal in spawnedAnimals)
        {
            if (animal == null) continue;
            var cg = animal.GetComponent<CanvasGroup>() ?? animal.AddComponent<CanvasGroup>();
            cg.alpha = 0.15f;
        }

        yield return new WaitForSeconds(0.6f);
    }

    // ═══════════════════════════════════════
    //  THEME COLORS
    // ═══════════════════════════════════════

    private void SetThemeColors()
    {
        int t = roundNumber % SkyColors.Length;
        if (skyImage != null) skyImage.color = SkyColors[t];
        if (groundImage != null) groundImage.color = GroundColors[t];
        if (groundFrontImage != null) groundFrontImage.color = GroundFrontColors[t];
    }

    // ═══════════════════════════════════════
    //  CLOUDS
    // ═══════════════════════════════════════

    private void PlaceClouds()
    {
        if (cloudSprites == null || cloudSprites.Length == 0 || cloudLayer == null) return;
        int count = Random.Range(3, 6);
        for (int i = 0; i < count; i++)
        {
            var spr = cloudSprites[Random.Range(0, cloudSprites.Length)];
            float cx = (float)(i + 1) / (count + 1) + Random.Range(-0.06f, 0.06f);
            float cy = Random.Range(0.55f, 0.92f);
            float sz = Random.Range(90f, 170f);
            var go = AddSprite(cloudLayer, spr,
                new Vector2(cx, cy), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(sz * 1.6f, sz));
            if (go != null)
                activeCoroutines.Add(StartCoroutine(CloudDrift(go.GetComponent<RectTransform>(), i)));
        }
    }

    // ═══════════════════════════════════════
    //  BACK ROW — decoration groups
    // ═══════════════════════════════════════

    private void BuildBackRow()
    {
        if (backRowLayer == null) return;
        float slotW = RefW / NumSlots;
        int[] slots = { 0, 2, 4, 5, 7 };
        int[] groupPattern = { 0, 1, 2, 1, 0 };

        for (int i = 0; i < slots.Length; i++)
        {
            float cx = (slots[i] + 0.5f) * slotW;
            float cy = Random.Range(BackRowMinY, BackRowMaxY);
            PlaceBackGroup(cx, cy, groupPattern[i % groupPattern.Length]);
        }
    }

    private void PlaceBackGroup(float cx, float cy, int groupType)
    {
        switch (groupType)
        {
            case 0:
                PlaceBackItem(cx, cy, treeSprites, 220f, 340f, 0.50f);
                if (bushSprites != null && bushSprites.Length > 0)
                    PlaceBackItem(cx + Random.Range(40f, 70f), cy - 15f, bushSprites, 60f, 90f, 1.3f);
                break;
            case 1:
                PlaceBackItem(cx, cy, houseSprites, 140f, 200f, 0.75f);
                if (fenceSprites != null && fenceSprites.Length > 0)
                    PlaceBackItem(cx + Random.Range(-80f, 80f), cy - 10f, fenceSprites, 50f, 70f, 2.2f);
                break;
            case 2:
                PlaceBackItem(cx - 35f, cy, smallTreeSprites, 130f, 200f, 0.65f);
                PlaceBackItem(cx + 35f, cy + Random.Range(-15f, 15f), smallTreeSprites, 110f, 180f, 0.65f);
                break;
        }
    }

    private void PlaceBackItem(float cx, float cy, Sprite[] sprites, float minH, float maxH, float aspect)
    {
        if (sprites == null || sprites.Length == 0 || backRowLayer == null) return;
        var spr = sprites[Random.Range(0, sprites.Length)];
        float h = Random.Range(minH, maxH);
        AddSprite(backRowLayer, spr, Vector2.zero, new Vector2(0.5f, 0f),
            new Vector2(cx, cy), new Vector2(h * aspect, h));
    }

    // ═══════════════════════════════════════
    //  FRONT ROW — foreground decoration
    // ═══════════════════════════════════════

    private void BuildFrontRow()
    {
        if (frontRowLayer == null) return;
        float slotW = RefW / NumSlots;
        int[] slots = { 1, 4, 6 };

        for (int i = 0; i < slots.Length; i++)
        {
            float cx = (slots[i] + 0.5f) * slotW + Random.Range(-30f, 30f);
            float cy = Random.Range(FrontRowMinY, FrontRowMaxY);
            Sprite spr;
            float h, w;

            if (Random.value < 0.7f && bushSprites != null && bushSprites.Length > 0)
            {
                spr = bushSprites[Random.Range(0, bushSprites.Length)];
                h = Random.Range(80f, 120f);
                w = h * Random.Range(1.2f, 1.6f);
            }
            else if (smallTreeSprites != null && smallTreeSprites.Length > 0)
            {
                spr = smallTreeSprites[Random.Range(0, smallTreeSprites.Length)];
                h = Random.Range(110f, 160f);
                w = h * 0.6f;
            }
            else continue;

            AddSprite(frontRowLayer, spr, Vector2.zero, new Vector2(0.5f, 0f),
                new Vector2(cx, cy), new Vector2(w, h));
        }
    }

    // ═══════════════════════════════════════
    //  ANIMAL PLACEMENT — ABSOLUTE NO OVERLAP
    //
    //  Uses a fixed grid with deterministic cell assignment.
    //  Each animal gets its own cell — overlap is impossible.
    //  Random jitter within each cell for visual variety.
    // ═══════════════════════════════════════

    private void PlaceAnimals(List<(SubItemData data, bool isTarget)> toPlace)
    {
        if (animalLayer == null) return;

        float tapSize = AnimalVisualSize + TapZonePadding * 2f;

        // Generate guaranteed non-overlapping positions
        var positions = GenerateAnimalPositions(toPlace.Count, tapSize);

        var hiddenSet = DecideHiding(toPlace);

        for (int i = 0; i < toPlace.Count; i++)
        {
            var item = toPlace[i].data;
            bool isTarget = toPlace[i].isTarget;
            Sprite sprite = item.thumbnail ?? item.contentAsset;
            if (sprite == null) continue;

            string key = item.categoryKey ?? "";
            float vScale = GetVisualScale(key);
            float visualSize = AnimalVisualSize * vScale;
            bool hidden = hiddenSet.Contains(i);

            var go = new GameObject($"Animal_{i}_{key}");
            go.transform.SetParent(animalLayer, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = positions[i];
            rt.sizeDelta = new Vector2(tapSize, visualSize + TapZonePadding);

            // Invisible tap area — ONLY target animals are clickable
            var tapImg = go.AddComponent<Image>();
            tapImg.color = new Color(1, 1, 1, 0);
            tapImg.raycastTarget = isTarget;  // non-targets get NO raycast at all

            // Visual child
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            var vrt = visual.AddComponent<RectTransform>();
            vrt.anchorMin = new Vector2(0.5f, 0f);
            vrt.anchorMax = new Vector2(0.5f, 0f);
            vrt.pivot = new Vector2(0.5f, 0f);
            vrt.anchoredPosition = Vector2.zero;
            vrt.sizeDelta = new Vector2(visualSize, visualSize);
            vrt.localRotation = Quaternion.Euler(0, 0, Random.Range(-6f, 6f));

            var img = visual.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;

            // Load sprite animation data (idle + success)
            string animalId = key;
            if (!string.IsNullOrEmpty(animalId))
            {
                string capId = char.ToUpper(animalId[0]) + animalId.Substring(1);
                var animData = AnimalAnimData.Load(capId);
                if (animData != null && animData.idleFrames != null && animData.idleFrames.Length > 0)
                {
                    var anim = visual.AddComponent<UISpriteAnimator>();
                    anim.targetImage = img;
                    anim.idleFrames = animData.idleFrames;
                    anim.floatingFrames = animData.floatingFrames;
                    anim.successFrames = animData.successFrames;
                    anim.framesPerSecond = animData.idleFps > 0 ? animData.idleFps : 30f;
                }
            }

            // Only target animals get a Button — non-targets have no click at all
            if (isTarget)
            {
                int capturedIndex = i;
                string capturedId = item.id;
                var btn = go.AddComponent<Button>();
                btn.targetGraphic = tapImg;
                btn.onClick.AddListener(() => OnAnimalTapped(capturedId, capturedIndex, go));
            }

            go.AddComponent<CanvasGroup>();
            spawnedAnimals.Add(go);

            if (isTarget) targetIndices.Add(i);

            // Decorations that partially cover animals — all have raycastTarget=false
            // so they NEVER block the animal's tap zone
            if (hidden)
                AddAnimalHiding(positions[i], visualSize, isTarget);
        }
    }

    /// <summary>
    /// Deterministic grid placement. Each animal is assigned a unique cell.
    /// Cells are large enough that no two animals can overlap even with jitter.
    /// </summary>
    private List<Vector2> GenerateAnimalPositions(int count, float size)
    {
        var positions = new List<Vector2>();

        // Determine grid dimensions
        // Use enough columns so cellWidth >= size, guaranteeing no horizontal overlap
        float playWidth = RefW - size;  // usable width (half-size margin each side)
        float playHeight = MiddleMaxY - MiddleMinY;

        // Calculate rows needed: try 2 rows first, use 3 if too many animals per row
        int rows = 2;
        int cols = Mathf.CeilToInt((float)count / rows);

        // Ensure cells are wide enough — if not, add a row
        float cellW = playWidth / cols;
        if (cellW < size)
        {
            rows = 3;
            cols = Mathf.CeilToInt((float)count / rows);
            cellW = playWidth / cols;
        }

        float cellH = playHeight / rows;

        // Build a list of all grid cells and shuffle for randomness
        var cells = new List<(int col, int row)>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                cells.Add((c, r));
        Shuffle(cells);

        // Jitter margin: how far from cell center we allow, constrained so
        // no animal can reach into a neighboring cell
        float jitterX = Mathf.Max(0, (cellW - size) * 0.4f);
        float jitterY = Mathf.Max(0, (cellH - size * 0.5f) * 0.3f);

        float startX = size * 0.5f;  // left margin
        float startY = MiddleMinY;

        for (int i = 0; i < count && i < cells.Count; i++)
        {
            int col = cells[i].col;
            int row = cells[i].row;

            // Cell center
            float cx = startX + (col + 0.5f) * cellW;
            float cy = startY + (row + 0.5f) * cellH;

            // Add bounded jitter
            float jx = Random.Range(-jitterX, jitterX);
            float jy = Random.Range(-jitterY, jitterY);

            positions.Add(new Vector2(cx + jx, cy + jy));
        }

        return positions;
    }

    private HashSet<int> DecideHiding(List<(SubItemData data, bool isTarget)> toPlace)
    {
        var hidden = new HashSet<int>();
        int targetHidden = 0;

        for (int i = 0; i < toPlace.Count; i++)
        {
            bool isTarget = toPlace[i].isTarget;
            float chance = isTarget ? 0.55f : 0.50f;

            if (Random.value < chance)
            {
                if (isTarget && targetHidden >= targetCount - 1) continue;
                hidden.Add(i);
                if (isTarget) targetHidden++;
            }
        }
        return hidden;
    }

    /// <summary>
    /// Add decorative hiding object near an animal. All decoration images have
    /// raycastTarget=false so they NEVER block the animal's tap input.
    /// </summary>
    private void AddAnimalHiding(Vector2 animalPos, float animalSize, bool isTarget)
    {
        if (frontRowLayer == null) return;

        Sprite spr;
        float h, w;

        float r = Random.value;
        if (r < 0.55f && bushSprites != null && bushSprites.Length > 0)
        {
            spr = bushSprites[Random.Range(0, bushSprites.Length)];
            w = animalSize * Random.Range(0.8f, 1.1f);
            h = animalSize * Random.Range(0.30f, 0.45f);
        }
        else if (r < 0.80f && fenceSprites != null && fenceSprites.Length > 0)
        {
            spr = fenceSprites[Random.Range(0, fenceSprites.Length)];
            w = animalSize * Random.Range(1.0f, 1.3f);
            h = animalSize * 0.28f;
        }
        else if (smallTreeSprites != null && smallTreeSprites.Length > 0)
        {
            spr = smallTreeSprites[Random.Range(0, smallTreeSprites.Length)];
            w = animalSize * 0.5f;
            h = animalSize * Random.Range(0.45f, 0.60f);
        }
        else return;

        if (isTarget) h = Mathf.Min(h, animalSize * 0.40f);

        // raycastTarget is already false via AddSprite — decorations never block taps
        AddSprite(frontRowLayer, spr, Vector2.zero, new Vector2(0.5f, 0f),
            new Vector2(animalPos.x + Random.Range(-15f, 15f), animalPos.y - h * 0.1f),
            new Vector2(w, h));
    }

    // ═══════════════════════════════════════
    //  HINT SYSTEM — one target at a time
    // ═══════════════════════════════════════

    private IEnumerator HintLoop()
    {
        while (isRoundActive)
        {
            yield return new WaitForSeconds(0.5f);

            float idle = Time.time - lastInteractionTime;
            if (idle < HintDelay) continue;

            var unfound = new List<int>();
            foreach (int idx in targetIndices)
            {
                if (!foundIndices.Contains(idx) && idx < spawnedAnimals.Count)
                {
                    var go = spawnedAnimals[idx];
                    if (go != null && go.activeSelf)
                        unfound.Add(idx);
                }
            }

            if (unfound.Count == 0) continue;

            int hintIdx = unfound[Random.Range(0, unfound.Count)];
            currentlyHintingIndex = hintIdx;

            yield return StartCoroutine(HintOneAnimal(hintIdx));

            currentlyHintingIndex = -1;
            yield return new WaitForSeconds(HintInterval);
        }
    }

    private IEnumerator HintOneAnimal(int index)
    {
        if (index >= spawnedAnimals.Count) yield break;
        var go = spawnedAnimals[index];
        if (go == null) yield break;

        var visual = go.transform.Find("Visual");
        if (visual == null) yield break;
        var vrt = visual.GetComponent<RectTransform>();
        if (vrt == null) yield break;

        Vector3 origScale = vrt.localScale;
        Vector2 origPos = vrt.anchoredPosition;

        for (int bounce = 0; bounce < 2; bounce++)
        {
            if (!isRoundActive || foundIndices.Contains(index)) yield break;

            float t = 0f;
            while (t < 0.15f)
            {
                t += Time.deltaTime;
                float p = t / 0.15f;
                vrt.localScale = origScale * (1f + 0.12f * p);
                vrt.anchoredPosition = origPos + new Vector2(0, 8f * p);
                yield return null;
            }

            t = 0f;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                float p = t / 0.2f;
                vrt.localScale = origScale * (1f + 0.12f * (1f - p));
                vrt.anchoredPosition = origPos + new Vector2(0, 8f * (1f - p));
                yield return null;
            }

            vrt.localScale = origScale;
            vrt.anchoredPosition = origPos;

            if (bounce < 1)
                yield return new WaitForSeconds(0.15f);
        }

        vrt.localScale = origScale;
        vrt.anchoredPosition = origPos;
    }

    // ═══════════════════════════════════════
    //  SPRITE HELPER
    // ═══════════════════════════════════════

    private GameObject AddSprite(RectTransform parent, Sprite sprite,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(sprite.name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor; rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;  // decorations never block taps
        worldObjects.Add(go);
        return go;
    }

    // ═══════════════════════════════════════
    //  ANIMATIONS
    // ═══════════════════════════════════════

    private IEnumerator CloudDrift(RectTransform rt, int index)
    {
        if (rt == null) yield break;
        Vector2 orig = rt.anchoredPosition;
        float speed = Random.Range(8f, 16f);
        float amp = Random.Range(35f, 65f);
        float phase = index * 1.7f;
        while (rt != null)
        {
            rt.anchoredPosition = orig + new Vector2(Mathf.Sin((Time.time * speed * 0.01f + phase)) * amp, 0);
            yield return null;
        }
    }

    // ═══════════════════════════════════════
    //  TAP HANDLING
    // ═══════════════════════════════════════

    private void OnAnimalTapped(string animalId, int index, GameObject go)
    {
        if (!isRoundActive || IsInputLocked) return;

        DismissTutorial();
        lastInteractionTime = Time.time;

        if (animalId == targetAnimal.id)
        {
            targetsFound++;
            RecordCorrect(isLast: targetsFound >= targetCount);
            PlayCorrectEffect(go.GetComponent<RectTransform>());
            foundIndices.Add(index);
            UpdateRemainingText();
            StartCoroutine(CorrectTapAnimation(go));

            string key = targetAnimal.categoryKey;
            if (!string.IsNullOrEmpty(key))
                SoundLibrary.PlayAnimalName(char.ToUpper(key[0]) + key.Substring(1));

            if (targetsFound >= targetCount)
            {
                isRoundActive = false;
                if (hintCoroutine != null) { StopCoroutine(hintCoroutine); hintCoroutine = null; }
                currentlyHintingIndex = -1;
                CompleteRound();
            }
        }
        else
        {
            RecordMistake();
            PlayWrongEffect(go.GetComponent<RectTransform>());
            StartCoroutine(WrongTapAnimation(go));
        }
    }

    /// <summary>
    /// Correct tap: immediately disable raycast → play success animation → fade out.
    /// All accesses to go/vrt/cg check for destroyed objects since ClearRound may
    /// destroy animals while this coroutine is still running (last animal of a round).
    /// </summary>
    private IEnumerator CorrectTapAnimation(GameObject go)
    {
        // IMMEDIATELY disable all input so this animal can never block nearby targets
        var btn = go.GetComponent<Button>();
        if (btn != null) btn.interactable = false;
        var tapImg = go.GetComponent<Image>();
        if (tapImg != null) tapImg.raycastTarget = false;
        var cg = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;

        var visual = go.transform.Find("Visual");
        if (visual == null) yield break;
        var vrt = visual.GetComponent<RectTransform>();

        var spriteAnim = visual.GetComponent<UISpriteAnimator>();
        bool hasSuccessAnim = spriteAnim != null &&
            spriteAnim.successFrames != null && spriteAnim.successFrames.Length > 0;

        if (hasSuccessAnim)
        {
            Vector3 origScale = vrt.localScale;
            float t = 0f;
            while (t < 0.1f)
            {
                t += Time.deltaTime;
                float p = t / 0.1f;
                if (vrt == null) yield break;
                vrt.localScale = origScale * (1f + 0.2f * p);
                yield return null;
            }
            if (vrt == null) yield break;
            vrt.localScale = origScale * 1.2f;

            spriteAnim.PlaySuccess();

            float fps = spriteAnim.framesPerSecond > 0 ? spriteAnim.framesPerSecond : 30f;
            float animDuration = spriteAnim.successFrames.Length / fps;
            yield return new WaitForSeconds(animDuration);

            yield return new WaitForSeconds(0.3f);

            if (vrt != null) vrt.localScale = origScale;
        }
        else
        {
            Vector3 orig = vrt.localScale;
            float t = 0f;
            while (t < 0.15f)
            {
                t += Time.deltaTime;
                float p = t / 0.15f;
                if (vrt == null) yield break;
                vrt.localScale = Vector3.Lerp(orig, orig * 1.4f, p);
                yield return null;
            }
            yield return new WaitForSeconds(0.3f);
            if (vrt != null) vrt.localScale = orig;
        }

        // Fade out
        if (go == null) yield break;
        float fadeT = 0f;
        while (fadeT < 0.4f)
        {
            fadeT += Time.deltaTime;
            float p = fadeT / 0.4f;
            if (cg == null) yield break;
            cg.alpha = Mathf.Lerp(1f, 0f, p);
            yield return null;
        }
        if (cg != null) cg.alpha = 0f;
    }

    private IEnumerator WrongTapAnimation(GameObject go)
    {
        var visual = go.transform.Find("Visual");
        var vrt = visual != null ? visual.GetComponent<RectTransform>() : go.GetComponent<RectTransform>();
        Quaternion orig = vrt.localRotation;
        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            float angle = Mathf.Sin(t * 35f) * 8f * (1f - t / 0.35f);
            vrt.localRotation = orig * Quaternion.Euler(0, 0, angle);
            yield return null;
        }
        vrt.localRotation = orig;
    }

    private void UpdateRemainingText()
    {
        if (remainingText != null)
            remainingText.text = (targetCount - targetsFound).ToString();
    }

    // ── TUTORIAL HAND ──

    private void PositionTutorialHand()
    {
        if (TutorialHand == null || targetIndices.Count == 0 || spawnedAnimals.Count == 0) return;

        // Point at the target animal — show the child exactly where to tap
        int idx = targetIndices[0];
        if (idx >= spawnedAnimals.Count || spawnedAnimals[idx] == null) return;

        var animalRT = spawnedAnimals[idx].GetComponent<RectTransform>();
        Vector2 localPos = TutorialHand.GetLocalCenter(animalRT);
        TutorialHand.SetPosition(localPos);
    }

    public void OnHomePressed() => ExitGame();
    public void OnRestartPressed()
    {
        OnRoundCleanup();
        OnRoundSetup();
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }

    private static Color HC(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
