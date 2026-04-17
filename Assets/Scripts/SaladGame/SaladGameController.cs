using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Salad preparation game.
/// Flow: Alin requests → pick vegetable → cut on board → pieces to bowl → repeat → shake to mix → serve.
///
/// Phases:
///   1. ShowRequest — Alin shows thought bubble with requested items
///   2. PickItem — player taps correct item from basket
///   3. CutItem — player swipes along guide lines to slice
///   4. AddToBowl — pieces fall into bowl
///   5. (repeat 2-4 for each item)
///   6. MixSalad — shake device to mix
///   7. Serve — Alin reacts happily
///
/// Uses fruit sprites from Resources/Tractor/Fruits (Fruits_0..19).
/// </summary>
public class SaladGameController : BaseMiniGame
{
    [Header("UI Areas")]
    public RectTransform playArea;

    // Game phases
    private enum Phase { ShowRequest, PickItem, CutItem, AddToBowl, MixSalad, Serve }
    private Phase _phase;

    // Round data
    private Sprite[] _allFruits;
    private List<int> _requestedIndices = new List<int>();
    private int _currentRequestIdx;
    private Canvas _canvas;

    // UI references (created at runtime)
    private GameObject _basketPanel;
    private GameObject _cuttingBoardPanel;
    private GameObject _bowlPanel;
    private GameObject _thoughtBubble;
    private GameObject _fallbackMixBtn;
    private RectTransform _bowlRT;
    private readonly List<GameObject> _bowlPieces = new List<GameObject>();
    private readonly List<CutPiece> _physicsPieces = new List<CutPiece>();
    private readonly List<Texture2D> _createdTextures = new List<Texture2D>();
    private readonly List<Sprite> _createdSprites = new List<Sprite>();
    private Sprite _roundedRectCached;

    // Cutting state
    private Sprite _currentFullSprite;
    private readonly List<RectTransform> _cutLines = new List<RectTransform>();
    private readonly List<bool> _cutDone = new List<bool>();
    private readonly List<GameObject> _cutPieceGOs = new List<GameObject>();
    private int _cutsCompleted;
    private int _totalCuts;

    // Shake detection
    private float _shakeAccum;
    private const float ShakeThreshold = 2.5f;
    private const float ShakeNeeded = 8f; // accumulated shake force needed
    private bool _shakeReady;
    private float _noShakeTimer;

    // ── BaseMiniGame overrides ──

    protected override string GetFallbackGameId() => "saladgame";

    protected override void OnGameInit()
    {
        totalRounds = 1;
        isEndless = true;
        contentCategory = "fruits";
        playConfettiOnRoundWin = true;
        _canvas = GetComponentInParent<Canvas>();

        _allFruits = Resources.LoadAll<Sprite>("Tractor/Fruits");
        if (_allFruits == null || _allFruits.Length == 0)
            Debug.LogError("[SaladGame] No fruit sprites found!");

        _roundedRectCached = Resources.Load<Sprite>("UI/RoundedRect");
    }

    protected override void OnRoundSetup()
    {
        _currentRequestIdx = 0;
        _bowlPieces.Clear();
        _physicsPieces.Clear();
        _shakeAccum = 0f;
        _shakeReady = false;

        // Pick items for this salad
        int itemCount = GetItemCount();
        _requestedIndices.Clear();
        var pool = new List<int>();
        for (int i = 0; i < _allFruits.Length; i++) pool.Add(i);
        ShuffleList(pool);
        for (int i = 0; i < itemCount && i < pool.Count; i++)
            _requestedIndices.Add(pool[i]);

        Debug.Log($"[SaladGame] Round: {itemCount} items, difficulty={Difficulty}");

        StartCoroutine(RunRound());
    }

    protected override void OnRoundCleanup()
    {
        if (_basketPanel != null) Destroy(_basketPanel);
        if (_cuttingBoardPanel != null) Destroy(_cuttingBoardPanel);
        if (_bowlPanel != null) Destroy(_bowlPanel);
        if (_thoughtBubble != null) Destroy(_thoughtBubble);
        if (_fallbackMixBtn != null) Destroy(_fallbackMixBtn);
        foreach (var g in _bowlPieces) if (g != null) Destroy(g);
        _bowlPieces.Clear();
        _physicsPieces.Clear();
        _cutLines.Clear();
        _cutDone.Clear();
        _cutPieceGOs.Clear();
        CleanupNativeAssets();
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        CleanupNativeAssets();
    }

    private void CleanupNativeAssets()
    {
        foreach (var s in _createdSprites) if (s != null) Destroy(s);
        foreach (var t in _createdTextures) if (t != null) Destroy(t);
        _createdSprites.Clear();
        _createdTextures.Clear();
    }

    private int GetItemCount()
    {
        if (Difficulty <= 3) return 2;
        if (Difficulty <= 6) return 3;
        return 4;
    }

    private int GetCutsPerItem()
    {
        if (Difficulty <= 3) return 2;
        if (Difficulty <= 6) return 3;
        return 4;
    }

    // ══════════════════════════════════════════════
    //  MAIN FLOW
    // ══════════════════════════════════════════════

    private IEnumerator RunRound()
    {
        yield return null; // wait one frame for layout

        // Create bowl (persists across all items)
        CreateBowl();

        // Show request
        yield return StartCoroutine(ShowRequestPhase());

        // Process each item
        for (_currentRequestIdx = 0; _currentRequestIdx < _requestedIndices.Count; _currentRequestIdx++)
        {
            // Highlight current item in thought bubble
            UpdateThoughtBubbleHighlight();

            // Pick
            yield return StartCoroutine(PickItemPhase());

            // Cut
            yield return StartCoroutine(CutItemPhase());

            // Add to bowl
            yield return StartCoroutine(AddToBowlPhase());
        }

        // Mix
        yield return StartCoroutine(MixSaladPhase());

        // Serve
        yield return StartCoroutine(ServePhase());

        // Complete round
        CompleteRound();
    }

    // ══════════════════════════════════════════════
    //  PHASE 1: SHOW REQUEST
    // ══════════════════════════════════════════════

    private IEnumerator ShowRequestPhase()
    {
        _phase = Phase.ShowRequest;

        CreateThoughtBubble();

        if (AlinGuide.Instance != null)
        {
            AlinGuide.Instance.Show();
            AlinGuide.Instance.PlayTalking();
        }

        yield return new WaitForSeconds(2f);

        if (AlinGuide.Instance != null)
            AlinGuide.Instance.StopTalking();
    }

    private void CreateThoughtBubble()
    {
        if (_thoughtBubble != null) Destroy(_thoughtBubble);

        _thoughtBubble = new GameObject("ThoughtBubble");
        _thoughtBubble.transform.SetParent(playArea, false);
        var rt = _thoughtBubble.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.02f, 0.75f);
        rt.anchorMax = new Vector2(0.55f, 0.98f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var roundedRect = Resources.Load<Sprite>("UI/RoundedRect");

        // Bubble background
        var bg = _thoughtBubble.AddComponent<Image>();
        if (roundedRect != null) { bg.sprite = roundedRect; bg.type = Image.Type.Sliced; }
        bg.color = new Color(1f, 1f, 1f, 0.9f);
        bg.raycastTarget = false;

        // Add requested item icons
        float iconSize = 80f;
        float spacing = 16f;
        float totalW = _requestedIndices.Count * iconSize + (_requestedIndices.Count - 1) * spacing;
        float startX = -totalW / 2f + iconSize / 2f;

        for (int i = 0; i < _requestedIndices.Count; i++)
        {
            var iconGO = new GameObject($"Icon_{i}");
            iconGO.transform.SetParent(_thoughtBubble.transform, false);
            var iconRT = iconGO.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.5f, 0.5f);
            iconRT.anchorMax = new Vector2(0.5f, 0.5f);
            iconRT.sizeDelta = new Vector2(iconSize, iconSize);
            iconRT.anchoredPosition = new Vector2(startX + i * (iconSize + spacing), 0);

            var iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = _allFruits[_requestedIndices[i]];
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // Dim initially — will highlight current
            iconImg.color = new Color(1f, 1f, 1f, 0.4f);
        }
    }

    private void UpdateThoughtBubbleHighlight()
    {
        if (_thoughtBubble == null) return;
        for (int i = 0; i < _thoughtBubble.transform.childCount; i++)
        {
            var icon = _thoughtBubble.transform.GetChild(i).GetComponent<Image>();
            if (icon == null) continue;

            if (i < _currentRequestIdx)
                icon.color = new Color(0.5f, 1f, 0.5f, 0.6f); // done — greenish
            else if (i == _currentRequestIdx)
                icon.color = Color.white; // current — full bright
            else
                icon.color = new Color(1f, 1f, 1f, 0.4f); // upcoming — dim
        }
    }

    // ══════════════════════════════════════════════
    //  PHASE 2: PICK ITEM
    // ══════════════════════════════════════════════

    private IEnumerator PickItemPhase()
    {
        _phase = Phase.PickItem;
        _pickedItemIdx = -1;

        CreateBasket();

        // Wait for correct pick
        bool picked = false;
        int correctIdx = _requestedIndices[_currentRequestIdx];

        while (!picked)
        {
            yield return null;
            if (IsInputLocked) continue;

            // Check for tap (handled via button callbacks set in CreateBasket)
            if (_pickedItemIdx >= 0)
            {
                if (_pickedItemIdx == correctIdx)
                {
                    picked = true;
                    Stats?.RecordCorrect("pick", _allFruits[correctIdx].name);
                    // Flash the correct item
                    PlayCorrectEffect(_pickedItemRT);
                    yield return new WaitForSeconds(0.3f);
                }
                else
                {
                    RecordMistake("wrong_pick");
                    PlayWrongEffect(_pickedItemRT);
                    yield return new WaitForSeconds(0.3f);
                }
                _pickedItemIdx = -1;
            }
        }

        // Remove basket
        if (_basketPanel != null) { Destroy(_basketPanel); _basketPanel = null; }
    }

    private int _pickedItemIdx = -1;
    private RectTransform _pickedItemRT;

    private void CreateBasket()
    {
        if (_basketPanel != null) Destroy(_basketPanel);

        _basketPanel = new GameObject("Basket");
        _basketPanel.transform.SetParent(playArea, false);
        var rt = _basketPanel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.05f, 0.05f);
        rt.anchorMax = new Vector2(0.95f, 0.65f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var roundedRect = Resources.Load<Sprite>("UI/RoundedRect");

        // Basket background
        var bg = _basketPanel.AddComponent<Image>();
        if (roundedRect != null) { bg.sprite = roundedRect; bg.type = Image.Type.Sliced; }
        bg.color = new Color(0.92f, 0.88f, 0.78f, 0.85f); // warm basket color
        bg.raycastTarget = false;

        // Shadow
        var shadow = _basketPanel.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.2f, 0.15f, 0.1f, 0.2f);
        shadow.effectDistance = new Vector2(3, -4);

        // Place items (requested + distractors)
        int basketSize = GetBasketSize();
        var itemIndices = new List<int>(_requestedIndices);

        // Add distractors
        var allIndices = new List<int>();
        for (int i = 0; i < _allFruits.Length; i++) allIndices.Add(i);
        ShuffleList(allIndices);
        foreach (int idx in allIndices)
        {
            if (itemIndices.Count >= basketSize) break;
            if (!itemIndices.Contains(idx)) itemIndices.Add(idx);
        }
        ShuffleList(itemIndices);

        // Layout items in grid
        int cols = Mathf.Min(basketSize, 4);
        int rows = Mathf.CeilToInt((float)basketSize / cols);
        float itemSize = 120f;
        float spacing = 20f;

        for (int i = 0; i < itemIndices.Count; i++)
        {
            int col = i % cols;
            int row = i / cols;
            float x = (col - (cols - 1) / 2f) * (itemSize + spacing);
            float y = ((rows - 1) / 2f - row) * (itemSize + spacing);

            int fruitIdx = itemIndices[i];

            var itemGO = new GameObject($"Item_{fruitIdx}");
            itemGO.transform.SetParent(_basketPanel.transform, false);
            var itemRT = itemGO.AddComponent<RectTransform>();
            itemRT.anchorMin = new Vector2(0.5f, 0.5f);
            itemRT.anchorMax = new Vector2(0.5f, 0.5f);
            itemRT.sizeDelta = new Vector2(itemSize, itemSize);
            itemRT.anchoredPosition = new Vector2(x, y);

            // Item card background
            var cardBg = itemGO.AddComponent<Image>();
            if (roundedRect != null) { cardBg.sprite = roundedRect; cardBg.type = Image.Type.Sliced; }
            cardBg.color = new Color(1f, 1f, 1f, 0.8f);
            cardBg.raycastTarget = true;

            // Fruit image
            var fruitGO = new GameObject("Fruit");
            fruitGO.transform.SetParent(itemGO.transform, false);
            var fruitRT = fruitGO.AddComponent<RectTransform>();
            fruitRT.anchorMin = new Vector2(0.08f, 0.08f);
            fruitRT.anchorMax = new Vector2(0.92f, 0.92f);
            fruitRT.offsetMin = Vector2.zero;
            fruitRT.offsetMax = Vector2.zero;
            var fruitImg = fruitGO.AddComponent<Image>();
            fruitImg.sprite = _allFruits[fruitIdx];
            fruitImg.preserveAspect = true;
            fruitImg.raycastTarget = false;

            // Button
            var btn = itemGO.AddComponent<Button>();
            btn.targetGraphic = cardBg;
            int capturedIdx = fruitIdx;
            var capturedRT = itemRT;
            btn.onClick.AddListener(() => { _pickedItemIdx = capturedIdx; _pickedItemRT = capturedRT; });
        }
    }

    private int GetBasketSize()
    {
        if (Difficulty <= 3) return 4;
        if (Difficulty <= 6) return 5;
        return 6;
    }

    // ══════════════════════════════════════════════
    //  PHASE 3: CUT ITEM
    // ══════════════════════════════════════════════

    private IEnumerator CutItemPhase()
    {
        _phase = Phase.CutItem;
        _cutsCompleted = 0;
        _totalCuts = GetCutsPerItem();
        _cutLines.Clear();
        _cutDone.Clear();
        _cutPieceGOs.Clear();

        int fruitIdx = _requestedIndices[_currentRequestIdx];
        _currentFullSprite = _allFruits[fruitIdx];

        CreateCuttingBoard();

        // Wait for all cuts
        while (_cutsCompleted < _totalCuts)
            yield return null;

        // Show confirm button, wait for tap
        yield return StartCoroutine(WaitForConfirm());
    }

    private void CreateCuttingBoard()
    {
        if (_cuttingBoardPanel != null) Destroy(_cuttingBoardPanel);

        _cuttingBoardPanel = new GameObject("CuttingBoard");
        _cuttingBoardPanel.transform.SetParent(playArea, false);
        var rt = _cuttingBoardPanel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.1f, 0.05f);
        rt.anchorMax = new Vector2(0.9f, 0.70f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Board background (wood)
        var bg = _cuttingBoardPanel.AddComponent<Image>();
        var roundedRect = Resources.Load<Sprite>("UI/RoundedRect");
        if (roundedRect != null) { bg.sprite = roundedRect; bg.type = Image.Type.Sliced; }
        bg.color = new Color(0.82f, 0.72f, 0.55f, 1f); // wood color
        bg.raycastTarget = true; // catches swipes

        var shadow = _cuttingBoardPanel.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.2f, 0.15f, 0.1f, 0.3f);
        shadow.effectDistance = new Vector2(4, -6);

        // Fruit on the board (centered, large)
        var fruitGO = new GameObject("Fruit");
        fruitGO.transform.SetParent(_cuttingBoardPanel.transform, false);
        var fruitRT = fruitGO.AddComponent<RectTransform>();
        fruitRT.anchorMin = new Vector2(0.15f, 0.1f);
        fruitRT.anchorMax = new Vector2(0.85f, 0.9f);
        fruitRT.offsetMin = Vector2.zero;
        fruitRT.offsetMax = Vector2.zero;
        var fruitImg = fruitGO.AddComponent<Image>();
        fruitImg.sprite = _currentFullSprite;
        fruitImg.preserveAspect = false; // fill container so cut lines align correctly
        fruitImg.raycastTarget = false;

        // Cut guide lines (vertical lines across the fruit)
        for (int i = 0; i < _totalCuts; i++)
        {
            float xNorm = (float)(i + 1) / (_totalCuts + 1);

            var lineGO = new GameObject($"CutLine_{i}");
            lineGO.transform.SetParent(fruitGO.transform, false);
            var lineRT = lineGO.AddComponent<RectTransform>();
            lineRT.anchorMin = new Vector2(xNorm, 0.05f);
            lineRT.anchorMax = new Vector2(xNorm, 0.95f);
            lineRT.sizeDelta = new Vector2(4, 0);
            var lineImg = lineGO.AddComponent<Image>();
            lineImg.color = new Color(1f, 1f, 1f, 0.5f);
            lineImg.raycastTarget = false;

            // Dashed effect — make it dotted by adding small gap segments
            var lineOutline = lineGO.AddComponent<Outline>();
            lineOutline.effectColor = new Color(0.4f, 0.3f, 0.2f, 0.3f);
            lineOutline.effectDistance = new Vector2(1, 0);

            _cutLines.Add(lineRT);
            _cutDone.Add(false);
        }

        // Add swipe detector
        var swipeDetector = _cuttingBoardPanel.AddComponent<SwipeDetector>();
        swipeDetector.Init(this, fruitGO.GetComponent<RectTransform>(), _canvas);
    }

    /// <summary>Called by SwipeDetector when a swipe crosses the cutting board.</summary>
    public void OnSwipe(float swipeXNormalized)
    {
        if (_phase != Phase.CutItem) return;

        // Find nearest uncut line
        int nearestLine = -1;
        float nearestDist = float.MaxValue;

        for (int i = 0; i < _cutLines.Count; i++)
        {
            if (_cutDone[i]) continue;
            float lineX = (float)(i + 1) / (_totalCuts + 1);
            float dist = Mathf.Abs(swipeXNormalized - lineX);
            if (dist < nearestDist && dist < 0.2f) // within 20% tolerance
            {
                nearestDist = dist;
                nearestLine = i;
            }
        }

        if (nearestLine < 0) return; // swipe too far from any line

        // Mark as cut
        _cutDone[nearestLine] = true;
        _cutsCompleted++;

        // Visual feedback: line becomes solid green then fades
        if (_cutLines[nearestLine] != null)
        {
            var lineImg = _cutLines[nearestLine].GetComponent<Image>();
            if (lineImg != null)
            {
                lineImg.color = new Color(0.3f, 0.9f, 0.3f, 0.8f);
                var lineRT = _cutLines[nearestLine];
                lineRT.sizeDelta = new Vector2(6, lineRT.sizeDelta.y);
            }
            PlayCorrectEffect(_cutLines[nearestLine]);
        }

        // Play chop sound
        var chopClip = Resources.Load<AudioClip>("Sounds/Correct");
        if (chopClip != null) BackgroundMusicManager.PlayOneShot(chopClip, 0.5f);

        Stats?.RecordCorrect("cut");
    }

    private IEnumerator WaitForConfirm()
    {
        // Create ✔️ button
        var confirmGO = new GameObject("ConfirmBtn");
        confirmGO.transform.SetParent(_cuttingBoardPanel.transform, false);
        var confirmRT = confirmGO.AddComponent<RectTransform>();
        confirmRT.anchorMin = new Vector2(0.5f, 0);
        confirmRT.anchorMax = new Vector2(0.5f, 0);
        confirmRT.pivot = new Vector2(0.5f, 0);
        confirmRT.anchoredPosition = new Vector2(0, 20);
        confirmRT.sizeDelta = new Vector2(100, 60);

        var roundedRect = Resources.Load<Sprite>("UI/RoundedRect");
        var confirmBg = confirmGO.AddComponent<Image>();
        if (roundedRect != null) { confirmBg.sprite = roundedRect; confirmBg.type = Image.Type.Sliced; }
        confirmBg.color = new Color(0.3f, 0.85f, 0.4f, 1f);

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(confirmGO.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero; labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero; labelRT.offsetMax = Vector2.zero;
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text = "\u2714"; // ✔
        labelTMP.fontSize = 36;
        labelTMP.color = Color.white;
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.raycastTarget = false;

        bool confirmed = false;
        var btn = confirmGO.AddComponent<Button>();
        btn.targetGraphic = confirmBg;
        btn.onClick.AddListener(() => confirmed = true);

        while (!confirmed)
            yield return null;
    }

    // ══════════════════════════════════════════════
    //  PHASE 4: ADD TO BOWL
    // ══════════════════════════════════════════════

    private IEnumerator AddToBowlPhase()
    {
        _phase = Phase.AddToBowl;

        int segments = _totalCuts + 1;
        Texture2D tex = ExtractSpriteTexture(_currentFullSprite);
        _createdTextures.Add(tex); // defer destruction to OnRoundCleanup

        // Calculate bowl center in playArea local coords (bowl uses anchor-stretch)
        float playW = playArea.rect.width;
        float playH = playArea.rect.height;
        // Bowl anchors: (0.6, 0.02) to (0.95, 0.40) — center relative to playArea bottom-left
        float bowlCenterX = (0.775f - 0.5f) * playW;  // convert to center-relative
        float bowlCenterY = (0.21f - 0.5f) * playH;
        float pieceSize = 50f;

        for (int i = 0; i < segments; i++)
        {
            int segW = tex.width / segments;
            var pieceSprite = Sprite.Create(tex,
                new Rect(i * segW, 0, segW, tex.height),
                new Vector2(0.5f, 0.5f), 100f);
            _createdSprites.Add(pieceSprite);

            var pieceGO = new GameObject($"Piece_{i}");
            pieceGO.transform.SetParent(playArea, false);
            var pieceRT = pieceGO.AddComponent<RectTransform>();
            pieceRT.anchorMin = new Vector2(0.5f, 0.5f);
            pieceRT.anchorMax = new Vector2(0.5f, 0.5f);
            pieceRT.sizeDelta = new Vector2(pieceSize, pieceSize);

            // Start position (center-ish of screen)
            pieceRT.anchoredPosition = new Vector2(Random.Range(-80f, 80f), 100f);

            var pieceImg = pieceGO.AddComponent<Image>();
            pieceImg.sprite = pieceSprite;
            pieceImg.preserveAspect = true;
            pieceImg.raycastTarget = false;

            float targetX = bowlCenterX + Random.Range(-40f, 40f);
            float targetY = bowlCenterY + Random.Range(10f, 40f);
            StartCoroutine(AnimatePieceToBowl(pieceRT, targetX, targetY, i * 0.1f));

            _bowlPieces.Add(pieceGO);
            _physicsPieces.Add(new CutPiece
            {
                rt = pieceRT,
                velocity = Vector2.zero,
                angularVelocity = 0f
            });
        }

        yield return new WaitForSeconds(segments * 0.1f + 0.5f);
        if (_cuttingBoardPanel != null) { Destroy(_cuttingBoardPanel); _cuttingBoardPanel = null; }
    }

    private IEnumerator AnimatePieceToBowl(RectTransform rt, float targetX, float targetY, float delay)
    {
        yield return new WaitForSeconds(delay);

        Vector2 start = rt.anchoredPosition;
        Vector2 end = new Vector2(targetX, targetY);
        float dur = 0.4f, elapsed = 0f;

        // Arc trajectory (up then down)
        float arcHeight = 60f;
        while (elapsed < dur && rt != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            Vector2 pos = Vector2.Lerp(start, end, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
            rt.anchoredPosition = pos;
            rt.localRotation = Quaternion.Euler(0, 0, t * 180f);
            yield return null;
        }
        if (rt != null)
        {
            rt.anchoredPosition = end;
            rt.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
        }
    }

    // ══════════════════════════════════════════════
    //  PHASE 5: MIX SALAD
    // ══════════════════════════════════════════════

    private IEnumerator MixSaladPhase()
    {
        _phase = Phase.MixSalad;

        // Show "Shake to mix!" prompt
        var promptGO = new GameObject("ShakePrompt");
        promptGO.transform.SetParent(playArea, false);
        var promptRT = promptGO.AddComponent<RectTransform>();
        promptRT.anchorMin = new Vector2(0.2f, 0.75f);
        promptRT.anchorMax = new Vector2(0.8f, 0.90f);
        promptRT.offsetMin = Vector2.zero;
        promptRT.offsetMax = Vector2.zero;
        var promptTMP = promptGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(promptTMP, "\u05E0\u05E2\u05E0\u05E2\u05D5 \u05D0\u05EA \u05D4\u05DE\u05DB\u05E9\u05D9\u05E8!"); // נענעו את המכשיר!
        promptTMP.fontSize = 38;
        promptTMP.fontStyle = FontStyles.Bold;
        promptTMP.color = new Color(0.3f, 0.3f, 0.35f);
        promptTMP.alignment = TextAlignmentOptions.Center;
        promptTMP.raycastTarget = false;

        _shakeAccum = 0f;
        _shakeReady = true;
        _noShakeTimer = 0f;

        // Fallback button (appears after 5 seconds)
        GameObject fallbackBtn = null;

        while (_shakeAccum < ShakeNeeded)
        {
            _noShakeTimer += Time.deltaTime;

            // Show fallback button after 5 seconds
            if (_noShakeTimer > 5f && fallbackBtn == null)
            {
                fallbackBtn = CreateMixButton(promptGO.transform);
            }

            yield return null;
        }

        _shakeReady = false;
        if (fallbackBtn != null) Destroy(fallbackBtn);
        if (promptGO != null) Destroy(promptGO);
    }

    private GameObject CreateMixButton(Transform parent)
    {
        var btnGO = new GameObject("MixButton");
        btnGO.transform.SetParent(parent.parent, false); // on playArea
        var btnRT = btnGO.AddComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0.3f, 0.65f);
        btnRT.anchorMax = new Vector2(0.7f, 0.75f);
        btnRT.offsetMin = Vector2.zero;
        btnRT.offsetMax = Vector2.zero;

        var roundedRect = Resources.Load<Sprite>("UI/RoundedRect");
        var bgImg = btnGO.AddComponent<Image>();
        if (roundedRect != null) { bgImg.sprite = roundedRect; bgImg.type = Image.Type.Sliced; }
        bgImg.color = new Color(0.95f, 0.65f, 0.2f, 1f);

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(btnGO.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero; labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero; labelRT.offsetMax = Vector2.zero;
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(labelTMP, "\u05E2\u05E8\u05D1\u05D1\u05D5!"); // ערבבו!
        labelTMP.fontSize = 30;
        labelTMP.fontStyle = FontStyles.Bold;
        labelTMP.color = Color.white;
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.raycastTarget = false;

        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = bgImg;
        btn.onClick.AddListener(() =>
        {
            _shakeAccum = ShakeNeeded; // complete mixing
            Destroy(btnGO);
        });

        return btnGO;
    }

    protected override void OnGameplayUpdate()
    {
        if (!_shakeReady) return;

        // Detect shake from accelerometer
        Vector3 accel = Input.acceleration;
        float force = accel.magnitude - 1f; // subtract gravity
        if (force < 0) force = 0;

        if (force > ShakeThreshold * 0.3f) // any significant movement
        {
            _shakeAccum += force * Time.deltaTime * 3f;
            _noShakeTimer = 0f;

            // Apply physics to bowl pieces
            foreach (var piece in _physicsPieces)
            {
                if (piece.rt == null) continue;
                piece.velocity += new Vector2(
                    Random.Range(-200f, 200f) * force,
                    Random.Range(-100f, 200f) * force
                ) * Time.deltaTime;
            }
        }

        // Update physics for bowl pieces
        UpdateBowlPhysics();
    }

    private void UpdateBowlPhysics()
    {
        if (_bowlRT == null || playArea == null) return;

        // Compute bowl bounds in playArea center-relative coords
        float playW = playArea.rect.width;
        float playH = playArea.rect.height;
        // Bowl anchors: (0.6, 0.02) to (0.95, 0.40)
        float bowlLeft = (0.6f - 0.5f) * playW + 10f;
        float bowlRight = (0.95f - 0.5f) * playW - 10f;
        float bowlBottom = (0.02f - 0.5f) * playH + 10f;
        float bowlTop = (0.40f - 0.5f) * playH - 10f;

        foreach (var piece in _physicsPieces)
        {
            if (piece.rt == null) continue;

            // Apply gravity
            piece.velocity.y -= 300f * Time.deltaTime;

            // Apply damping
            piece.velocity *= 0.97f;
            piece.angularVelocity *= 0.95f;

            // Move
            Vector2 pos = piece.rt.anchoredPosition;
            pos += piece.velocity * Time.deltaTime;

            // Bounce off bowl walls
            if (pos.x < bowlLeft) { pos.x = bowlLeft; piece.velocity.x = Mathf.Abs(piece.velocity.x) * 0.5f; }
            if (pos.x > bowlRight) { pos.x = bowlRight; piece.velocity.x = -Mathf.Abs(piece.velocity.x) * 0.5f; }
            if (pos.y < bowlBottom) { pos.y = bowlBottom; piece.velocity.y = Mathf.Abs(piece.velocity.y) * 0.4f; }
            if (pos.y > bowlTop) { pos.y = bowlTop; piece.velocity.y = -Mathf.Abs(piece.velocity.y) * 0.3f; }

            piece.rt.anchoredPosition = pos;

            // Rotate
            float angle = piece.rt.localRotation.eulerAngles.z + piece.angularVelocity * Time.deltaTime;
            piece.rt.localRotation = Quaternion.Euler(0, 0, angle);
            piece.angularVelocity += piece.velocity.x * 0.1f;
        }
    }

    // ══════════════════════════════════════════════
    //  PHASE 6: SERVE
    // ══════════════════════════════════════════════

    private IEnumerator ServePhase()
    {
        _phase = Phase.Serve;

        if (AlinGuide.Instance != null)
        {
            AlinGuide.Instance.Show();
            AlinGuide.Instance.PlayTalking();
        }

        // Show happy message
        var msgGO = new GameObject("ServeMsg");
        msgGO.transform.SetParent(playArea, false);
        var msgRT = msgGO.AddComponent<RectTransform>();
        msgRT.anchorMin = new Vector2(0.15f, 0.78f);
        msgRT.anchorMax = new Vector2(0.85f, 0.95f);
        msgRT.offsetMin = Vector2.zero;
        msgRT.offsetMax = Vector2.zero;
        var msgTMP = msgGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(msgTMP, "\u05D9\u05D5\u05E4\u05D9! \u05D4\u05E1\u05DC\u05D8 \u05DE\u05D5\u05DB\u05DF!"); // יופי! הסלט מוכן!
        msgTMP.fontSize = 42;
        msgTMP.fontStyle = FontStyles.Bold;
        msgTMP.color = new Color(0.25f, 0.65f, 0.25f);
        msgTMP.alignment = TextAlignmentOptions.Center;
        msgTMP.raycastTarget = false;

        SoundLibrary.PlayRandomFeedback();

        yield return new WaitForSeconds(2.5f);

        if (AlinGuide.Instance != null)
            AlinGuide.Instance.StopTalking();

        Destroy(msgGO);
    }

    // ══════════════════════════════════════════════
    //  BOWL
    // ══════════════════════════════════════════════

    private void CreateBowl()
    {
        if (_bowlPanel != null) Destroy(_bowlPanel);

        _bowlPanel = new GameObject("Bowl");
        _bowlPanel.transform.SetParent(playArea, false);
        _bowlRT = _bowlPanel.AddComponent<RectTransform>();
        _bowlRT.anchorMin = new Vector2(0.6f, 0.02f);
        _bowlRT.anchorMax = new Vector2(0.95f, 0.40f);
        _bowlRT.offsetMin = Vector2.zero;
        _bowlRT.offsetMax = Vector2.zero;

        // Bowl shape (U-shape using rounded rect)
        var roundedRect = Resources.Load<Sprite>("UI/RoundedRect");
        var bowlImg = _bowlPanel.AddComponent<Image>();
        if (roundedRect != null) { bowlImg.sprite = roundedRect; bowlImg.type = Image.Type.Sliced; }
        bowlImg.color = new Color(0.85f, 0.85f, 0.9f, 0.9f); // light ceramic
        bowlImg.raycastTarget = false;

        var bowlShadow = _bowlPanel.AddComponent<Shadow>();
        bowlShadow.effectColor = new Color(0.15f, 0.1f, 0.1f, 0.25f);
        bowlShadow.effectDistance = new Vector2(3, -4);

        // Inner darker area
        var innerGO = new GameObject("Inner");
        innerGO.transform.SetParent(_bowlPanel.transform, false);
        var innerRT = innerGO.AddComponent<RectTransform>();
        innerRT.anchorMin = new Vector2(0.08f, 0.08f);
        innerRT.anchorMax = new Vector2(0.92f, 0.85f);
        innerRT.offsetMin = Vector2.zero;
        innerRT.offsetMax = Vector2.zero;
        var innerImg = innerGO.AddComponent<Image>();
        if (roundedRect != null) { innerImg.sprite = roundedRect; innerImg.type = Image.Type.Sliced; }
        innerImg.color = new Color(0.92f, 0.92f, 0.95f, 0.7f);
        innerImg.raycastTarget = false;
    }

    // ══════════════════════════════════════════════
    //  SPRITE UTILITIES
    // ══════════════════════════════════════════════

    private static Texture2D ExtractSpriteTexture(Sprite sprite)
    {
        if (sprite == null) return null;
        var source = sprite.texture;
        Rect spriteRect = sprite.textureRect;
        int w = Mathf.RoundToInt(spriteRect.width);
        int h = Mathf.RoundToInt(spriteRect.height);

        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var extracted = new Texture2D(w, h, TextureFormat.RGBA32, false);
        extracted.ReadPixels(new Rect(spriteRect.x, spriteRect.y, w, h), 0, 0);
        extracted.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return extracted;
    }

    private static void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = list[i]; list[i] = list[j]; list[j] = temp;
        }
    }

    public void OnExitPressed() => ExitGame();

    // ── Physics data ──
    private class CutPiece
    {
        public RectTransform rt;
        public Vector2 velocity;
        public float angularVelocity;
    }
}
