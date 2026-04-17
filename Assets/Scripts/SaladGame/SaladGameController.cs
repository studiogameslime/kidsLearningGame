using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Salad preparation game — pick, free-cut, bowl focus, shake-mix, serve.
///
/// Cutting: free swipe anywhere, auto-corrected into clean slices.
/// Pieces visibly separate after each cut (spacing, rotation, offset).
/// Bowl focus moment: bowl moves to center, pieces arc-fall in, brief pause.
/// Mix: accelerometer shakes pieces in bowl with physics simulation.
/// </summary>
public class SaladGameController : BaseMiniGame
{
    [Header("UI Areas")]
    public RectTransform playArea;

    private enum Phase { ShowRequest, PickItem, CutItem, BowlFocus, MixSalad, Serve }
    private Phase _phase;

    // Data
    private Sprite[] _allFruits;
    private List<int> _requestedIndices = new List<int>();
    private int _currentRequestIdx;
    private Canvas _canvas;
    private Sprite _roundedRect;

    // UI panels
    private GameObject _basketPanel;
    private GameObject _cuttingBoardPanel;
    private GameObject _bowlPanel;
    private GameObject _thoughtBubble;
    private GameObject _confirmBtn;
    private GameObject _fallbackMixBtn;
    private RectTransform _bowlRT;

    // Cutting state
    private Sprite _currentFullSprite;
    private RectTransform _fruitContainer;
    private readonly List<RectTransform> _currentPieces = new List<RectTransform>();
    private int _cutCount;
    private const int MaxCuts = 6;

    // Bowl pieces (persist across items)
    private readonly List<GameObject> _bowlPieceGOs = new List<GameObject>();
    private readonly List<CutPiece> _physicsPieces = new List<CutPiece>();

    // Memory tracking
    private readonly List<Texture2D> _createdTextures = new List<Texture2D>();
    private readonly List<Sprite> _createdSprites = new List<Sprite>();

    // Picking
    private int _pickedItemIdx = -1;
    private RectTransform _pickedItemRT;

    // Shake
    private float _shakeAccum;
    private const float ShakeNeeded = 8f;
    private bool _shakeReady;
    private float _noShakeTimer;

    // ── BaseMiniGame ──

    protected override string GetFallbackGameId() => "saladgame";

    protected override void OnGameInit()
    {
        totalRounds = 1;
        isEndless = true;
        contentCategory = "fruits";
        playConfettiOnRoundWin = true;
        _canvas = GetComponentInParent<Canvas>();
        _allFruits = Resources.LoadAll<Sprite>("Tractor/Fruits");
        _roundedRect = Resources.Load<Sprite>("UI/RoundedRect");
    }

    protected override void OnRoundSetup()
    {
        _currentRequestIdx = 0;
        _bowlPieceGOs.Clear();
        _physicsPieces.Clear();
        _shakeAccum = 0f;
        _shakeReady = false;

        int itemCount = GetItemCount();
        _requestedIndices.Clear();
        var pool = new List<int>();
        for (int i = 0; i < _allFruits.Length; i++) pool.Add(i);
        ShuffleList(pool);
        for (int i = 0; i < itemCount && i < pool.Count; i++)
            _requestedIndices.Add(pool[i]);

        StartCoroutine(RunRound());
    }

    protected override void OnRoundCleanup()
    {
        if (_basketPanel != null) Destroy(_basketPanel);
        if (_cuttingBoardPanel != null) Destroy(_cuttingBoardPanel);
        if (_bowlPanel != null) Destroy(_bowlPanel);
        if (_thoughtBubble != null) Destroy(_thoughtBubble);
        if (_confirmBtn != null) Destroy(_confirmBtn);
        if (_fallbackMixBtn != null) Destroy(_fallbackMixBtn);
        foreach (var g in _bowlPieceGOs) if (g != null) Destroy(g);
        _bowlPieceGOs.Clear();
        _physicsPieces.Clear();
        _currentPieces.Clear();
        CleanupNative();
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        CleanupNative();
    }

    private void CleanupNative()
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

    // ══════════════════════════════════════════════
    //  MAIN FLOW
    // ══════════════════════════════════════════════

    private IEnumerator RunRound()
    {
        yield return null;
        CreateBowl();
        yield return StartCoroutine(ShowRequestPhase());

        for (_currentRequestIdx = 0; _currentRequestIdx < _requestedIndices.Count; _currentRequestIdx++)
        {
            UpdateThoughtHighlight();
            yield return StartCoroutine(PickItemPhase());
            yield return StartCoroutine(CutItemPhase());
            yield return StartCoroutine(BowlFocusPhase());
        }

        yield return StartCoroutine(MixSaladPhase());
        yield return StartCoroutine(ServePhase());
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
        rt.anchorMin = new Vector2(0.02f, 0.78f);
        rt.anchorMax = new Vector2(0.55f, 0.98f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var bg = _thoughtBubble.AddComponent<Image>();
        if (_roundedRect != null) { bg.sprite = _roundedRect; bg.type = Image.Type.Sliced; }
        bg.color = new Color(1f, 1f, 1f, 0.92f);
        bg.raycastTarget = false;

        var shadow = _thoughtBubble.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.15f);
        shadow.effectDistance = new Vector2(2, -3);

        float iconSize = 70f, spacing = 14f;
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
            iconImg.color = new Color(1f, 1f, 1f, 0.35f);
        }
    }

    private void UpdateThoughtHighlight()
    {
        if (_thoughtBubble == null) return;
        for (int i = 0; i < _thoughtBubble.transform.childCount; i++)
        {
            var img = _thoughtBubble.transform.GetChild(i).GetComponent<Image>();
            if (img == null) continue;
            if (i < _currentRequestIdx) img.color = new Color(0.5f, 1f, 0.5f, 0.7f);
            else if (i == _currentRequestIdx) img.color = Color.white;
            else img.color = new Color(1f, 1f, 1f, 0.35f);
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

        int correctIdx = _requestedIndices[_currentRequestIdx];
        bool picked = false;

        while (!picked)
        {
            yield return null;
            if (IsInputLocked || _pickedItemIdx < 0) continue;

            if (_pickedItemIdx == correctIdx)
            {
                picked = true;
                Stats?.RecordCorrect("pick", _allFruits[correctIdx].name);
                PlayCorrectEffect(_pickedItemRT);
                yield return new WaitForSeconds(0.4f);
            }
            else
            {
                RecordMistake("wrong_pick");
                PlayWrongEffect(_pickedItemRT);
                yield return new WaitForSeconds(0.4f);
            }
            _pickedItemIdx = -1;
        }

        if (_basketPanel != null) { Destroy(_basketPanel); _basketPanel = null; }
    }

    private void CreateBasket()
    {
        if (_basketPanel != null) Destroy(_basketPanel);

        _basketPanel = new GameObject("Basket");
        _basketPanel.transform.SetParent(playArea, false);
        var rt = _basketPanel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.05f, 0.05f);
        rt.anchorMax = new Vector2(0.95f, 0.68f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var bg = _basketPanel.AddComponent<Image>();
        if (_roundedRect != null) { bg.sprite = _roundedRect; bg.type = Image.Type.Sliced; }
        bg.color = new Color(0.93f, 0.89f, 0.80f, 0.88f);
        bg.raycastTarget = false;

        int basketSize = Difficulty <= 3 ? 4 : (Difficulty <= 6 ? 5 : 6);
        var items = new List<int>(_requestedIndices);
        var allIdx = new List<int>();
        for (int i = 0; i < _allFruits.Length; i++) allIdx.Add(i);
        ShuffleList(allIdx);
        foreach (int idx in allIdx)
        {
            if (items.Count >= basketSize) break;
            if (!items.Contains(idx)) items.Add(idx);
        }
        ShuffleList(items);

        int cols = Mathf.Min(basketSize, 3);
        int rows = Mathf.CeilToInt((float)basketSize / cols);
        float itemSize = 130f, spacing = 24f;
        int correctIdx = _requestedIndices[_currentRequestIdx];

        for (int i = 0; i < items.Count; i++)
        {
            int col = i % cols, row = i / cols;
            float x = (col - (cols - 1) / 2f) * (itemSize + spacing);
            float y = ((rows - 1) / 2f - row) * (itemSize + spacing);
            int fruitIdx = items[i];

            var itemGO = new GameObject($"Item_{fruitIdx}");
            itemGO.transform.SetParent(_basketPanel.transform, false);
            var itemRT = itemGO.AddComponent<RectTransform>();
            itemRT.anchorMin = new Vector2(0.5f, 0.5f);
            itemRT.anchorMax = new Vector2(0.5f, 0.5f);
            itemRT.sizeDelta = new Vector2(itemSize, itemSize);
            itemRT.anchoredPosition = new Vector2(x, y);

            var cardBg = itemGO.AddComponent<Image>();
            if (_roundedRect != null) { cardBg.sprite = _roundedRect; cardBg.type = Image.Type.Sliced; }
            // Highlight the correct item with a subtle warm glow
            bool isCorrect = (fruitIdx == correctIdx);
            cardBg.color = isCorrect
                ? new Color(1f, 0.97f, 0.88f, 0.95f)
                : new Color(1f, 1f, 1f, 0.8f);

            if (isCorrect)
            {
                var glow = itemGO.AddComponent<Outline>();
                glow.effectColor = new Color(1f, 0.85f, 0.3f, 0.5f);
                glow.effectDistance = new Vector2(3, -3);
            }

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

            var btn = itemGO.AddComponent<Button>();
            btn.targetGraphic = cardBg;
            int ci = fruitIdx;
            var crt = itemRT;
            btn.onClick.AddListener(() => { _pickedItemIdx = ci; _pickedItemRT = crt; });
        }
    }

    // ══════════════════════════════════════════════
    //  PHASE 3: CUT ITEM (FREE SWIPE)
    // ══════════════════════════════════════════════

    private IEnumerator CutItemPhase()
    {
        _phase = Phase.CutItem;
        _cutCount = 0;
        _currentPieces.Clear();

        int fruitIdx = _requestedIndices[_currentRequestIdx];
        _currentFullSprite = _allFruits[fruitIdx];

        CreateCuttingBoard();

        // Wait for at least 1 cut + confirm
        bool confirmed = false;
        CreateConfirmButton(() => { if (_cutCount > 0) confirmed = true; });

        while (!confirmed)
            yield return null;

        if (_confirmBtn != null) { Destroy(_confirmBtn); _confirmBtn = null; }
    }

    private void CreateCuttingBoard()
    {
        if (_cuttingBoardPanel != null) Destroy(_cuttingBoardPanel);

        _cuttingBoardPanel = new GameObject("CuttingBoard");
        _cuttingBoardPanel.transform.SetParent(playArea, false);
        var rt = _cuttingBoardPanel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.08f, 0.05f);
        rt.anchorMax = new Vector2(0.92f, 0.72f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var bg = _cuttingBoardPanel.AddComponent<Image>();
        if (_roundedRect != null) { bg.sprite = _roundedRect; bg.type = Image.Type.Sliced; }
        bg.color = new Color(0.85f, 0.74f, 0.58f, 1f);
        bg.raycastTarget = true;

        var shadow = _cuttingBoardPanel.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.2f, 0.15f, 0.1f, 0.3f);
        shadow.effectDistance = new Vector2(4, -6);

        // Wood grain lines
        for (int i = 0; i < 4; i++)
        {
            var grain = new GameObject($"Grain_{i}");
            grain.transform.SetParent(_cuttingBoardPanel.transform, false);
            var grt = grain.AddComponent<RectTransform>();
            float y = 0.2f + i * 0.2f;
            grt.anchorMin = new Vector2(0.03f, y);
            grt.anchorMax = new Vector2(0.97f, y);
            grt.sizeDelta = new Vector2(0, 1.5f);
            var gimg = grain.AddComponent<Image>();
            gimg.color = new Color(0.7f, 0.6f, 0.45f, 0.2f);
            gimg.raycastTarget = false;
        }

        // Fruit container (holds the fruit image, pieces will replace it)
        var fruitContainer = new GameObject("FruitContainer");
        fruitContainer.transform.SetParent(_cuttingBoardPanel.transform, false);
        _fruitContainer = fruitContainer.AddComponent<RectTransform>();
        _fruitContainer.anchorMin = new Vector2(0.15f, 0.1f);
        _fruitContainer.anchorMax = new Vector2(0.85f, 0.9f);
        _fruitContainer.offsetMin = Vector2.zero;
        _fruitContainer.offsetMax = Vector2.zero;

        // Full fruit (will be replaced by pieces on first cut)
        var fruitGO = new GameObject("FullFruit");
        fruitGO.transform.SetParent(fruitContainer.transform, false);
        var fruitRT = fruitGO.AddComponent<RectTransform>();
        fruitRT.anchorMin = Vector2.zero;
        fruitRT.anchorMax = Vector2.one;
        fruitRT.offsetMin = Vector2.zero;
        fruitRT.offsetMax = Vector2.zero;
        var fruitImg = fruitGO.AddComponent<Image>();
        fruitImg.sprite = _currentFullSprite;
        fruitImg.preserveAspect = true;
        fruitImg.raycastTarget = false;

        _currentPieces.Add(fruitRT);

        // Swipe detector
        var swipe = _cuttingBoardPanel.AddComponent<SwipeDetector>();
        swipe.Init(this, _fruitContainer, _canvas);
    }

    /// <summary>Called by SwipeDetector on free swipe.</summary>
    public void OnSwipe(float normalizedX, bool isVertical)
    {
        if (_phase != Phase.CutItem) return;
        if (_cutCount >= MaxCuts) return;
        if (_currentPieces.Count == 0) return;

        // Clamp to avoid edge cuts
        normalizedX = Mathf.Clamp(normalizedX, 0.12f, 0.88f);

        _cutCount++;

        // Extract texture and split into pieces
        Texture2D tex = ExtractSpriteTexture(_currentFullSprite);
        _createdTextures.Add(tex);

        int totalPieces = _cutCount + 1;
        float containerW = _fruitContainer.rect.width;
        float containerH = _fruitContainer.rect.height;
        if (containerW <= 0) containerW = 400f;
        if (containerH <= 0) containerH = 350f;

        // Destroy old pieces
        foreach (var p in _currentPieces)
            if (p != null) Destroy(p.gameObject);
        _currentPieces.Clear();

        // Create new pieces (evenly split)
        float pieceW = containerW / totalPieces;
        for (int i = 0; i < totalPieces; i++)
        {
            int segW = tex.width / totalPieces;
            var sprite = Sprite.Create(tex,
                new Rect(i * segW, 0, segW, tex.height),
                new Vector2(0.5f, 0.5f), 100f);
            _createdSprites.Add(sprite);

            var pieceGO = new GameObject($"Piece_{i}");
            pieceGO.transform.SetParent(_fruitContainer, false);
            var pieceRT = pieceGO.AddComponent<RectTransform>();
            pieceRT.anchorMin = new Vector2(0.5f, 0.5f);
            pieceRT.anchorMax = new Vector2(0.5f, 0.5f);
            pieceRT.sizeDelta = new Vector2(pieceW - 6f, containerH * 0.85f);

            // Position with spacing between pieces
            float xPos = (i - (totalPieces - 1) / 2f) * pieceW;
            // Random offset for playful feel
            float yOffset = Random.Range(-8f, 8f);
            float rot = Random.Range(-5f, 5f);
            pieceRT.anchoredPosition = new Vector2(xPos, yOffset);
            pieceRT.localRotation = Quaternion.Euler(0, 0, rot);

            var pieceImg = pieceGO.AddComponent<Image>();
            pieceImg.sprite = sprite;
            pieceImg.preserveAspect = true;
            pieceImg.raycastTarget = false;

            _currentPieces.Add(pieceRT);
        }

        // Play chop feedback
        var chopClip = Resources.Load<AudioClip>("Sounds/Correct");
        if (chopClip != null) BackgroundMusicManager.PlayOneShot(chopClip, 0.5f);

        // Show confirm button if not already visible
        if (_confirmBtn != null)
            _confirmBtn.SetActive(true);

        Stats?.RecordCorrect("cut");
    }

    private void CreateConfirmButton(System.Action onConfirm)
    {
        _confirmBtn = new GameObject("ConfirmBtn");
        _confirmBtn.transform.SetParent(playArea, false);
        var rt = _confirmBtn.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.38f, 0.01f);
        rt.anchorMax = new Vector2(0.62f, 0.08f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var bg = _confirmBtn.AddComponent<Image>();
        if (_roundedRect != null) { bg.sprite = _roundedRect; bg.type = Image.Type.Sliced; }
        bg.color = new Color(0.3f, 0.85f, 0.4f, 1f);

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(_confirmBtn.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero; labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero; labelRT.offsetMax = Vector2.zero;
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text = "\u2714"; // ✔
        labelTMP.fontSize = 36;
        labelTMP.color = Color.white;
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.raycastTarget = false;

        var btn = _confirmBtn.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(() => onConfirm?.Invoke());

        _confirmBtn.SetActive(false); // hidden until first cut
    }

    // ══════════════════════════════════════════════
    //  PHASE 4: BOWL FOCUS MOMENT
    // ══════════════════════════════════════════════

    private IEnumerator BowlFocusPhase()
    {
        _phase = Phase.BowlFocus;

        // Dim cutting board
        if (_cuttingBoardPanel != null)
        {
            var cg = _cuttingBoardPanel.AddComponent<CanvasGroup>();
            float dur = 0.3f, elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Lerp(1f, 0.2f, elapsed / dur);
                yield return null;
            }
        }

        // Move bowl to center + scale up
        Vector2 bowlOrigPos = _bowlRT.anchoredPosition;
        Vector2 bowlOrigSize = _bowlRT.sizeDelta;
        Vector2 bowlOrigAnchorMin = _bowlRT.anchorMin;
        Vector2 bowlOrigAnchorMax = _bowlRT.anchorMax;

        // Switch to center-anchored for animation
        _bowlRT.anchorMin = new Vector2(0.5f, 0.5f);
        _bowlRT.anchorMax = new Vector2(0.5f, 0.5f);
        _bowlRT.sizeDelta = new Vector2(300f, 250f);
        Vector2 targetPos = new Vector2(0, -30f);

        float moveDur = 0.4f;
        Vector2 startPos = _bowlRT.anchoredPosition;
        float elapsedMove = 0f;
        while (elapsedMove < moveDur)
        {
            elapsedMove += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsedMove / moveDur);
            _bowlRT.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            float scale = Mathf.Lerp(1f, 1.3f, t);
            _bowlRT.localScale = Vector3.one * scale;
            yield return null;
        }

        // Create and animate pieces falling into bowl
        Texture2D tex = ExtractSpriteTexture(_currentFullSprite);
        _createdTextures.Add(tex);
        int segments = _cutCount + 1;

        for (int i = 0; i < segments; i++)
        {
            int segW = tex.width / segments;
            var sprite = Sprite.Create(tex,
                new Rect(i * segW, 0, segW, tex.height),
                new Vector2(0.5f, 0.5f), 100f);
            _createdSprites.Add(sprite);

            var pieceGO = new GameObject($"BowlPiece_{i}");
            pieceGO.transform.SetParent(playArea, false);
            var pieceRT = pieceGO.AddComponent<RectTransform>();
            pieceRT.anchorMin = new Vector2(0.5f, 0.5f);
            pieceRT.anchorMax = new Vector2(0.5f, 0.5f);
            pieceRT.sizeDelta = new Vector2(45f, 45f);
            pieceRT.anchoredPosition = new Vector2(Random.Range(-60f, 60f), 200f);

            var pieceImg = pieceGO.AddComponent<Image>();
            pieceImg.sprite = sprite;
            pieceImg.preserveAspect = true;
            pieceImg.raycastTarget = false;

            float tgtX = targetPos.x + Random.Range(-50f, 50f);
            float tgtY = targetPos.y + Random.Range(-20f, 30f);
            StartCoroutine(PieceFallToBowl(pieceRT, tgtX, tgtY, i * 0.08f));

            _bowlPieceGOs.Add(pieceGO);
            _physicsPieces.Add(new CutPiece { rt = pieceRT, velocity = Vector2.zero, angularVelocity = 0f });
        }

        // Wait for all pieces to land + brief satisfying pause
        yield return new WaitForSeconds(segments * 0.08f + 0.8f);

        // Move bowl back
        if (_cuttingBoardPanel != null)
            Destroy(_cuttingBoardPanel);
        _cuttingBoardPanel = null;

        // Restore bowl to corner
        _bowlRT.anchorMin = bowlOrigAnchorMin;
        _bowlRT.anchorMax = bowlOrigAnchorMax;
        _bowlRT.offsetMin = Vector2.zero;
        _bowlRT.offsetMax = Vector2.zero;
        _bowlRT.localScale = Vector3.one;

        yield return new WaitForSeconds(0.3f);
    }

    private IEnumerator PieceFallToBowl(RectTransform rt, float tgtX, float tgtY, float delay)
    {
        yield return new WaitForSeconds(delay);
        Vector2 start = rt.anchoredPosition;
        Vector2 end = new Vector2(tgtX, tgtY);
        float dur = 0.35f, elapsed = 0f;

        while (elapsed < dur && rt != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            Vector2 pos = Vector2.Lerp(start, end, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * 50f; // arc
            rt.anchoredPosition = pos;
            rt.localRotation = Quaternion.Euler(0, 0, t * 180f);
            yield return null;
        }
        if (rt != null)
        {
            rt.anchoredPosition = end;
            // Soft bounce
            float bounceDur = 0.12f;
            elapsed = 0f;
            while (elapsed < bounceDur && rt != null)
            {
                elapsed += Time.deltaTime;
                float b = Mathf.Sin((elapsed / bounceDur) * Mathf.PI) * 8f;
                rt.anchoredPosition = end + new Vector2(0, b);
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = end;
        }
    }

    // ══════════════════════════════════════════════
    //  PHASE 5: MIX SALAD (SHAKE)
    // ══════════════════════════════════════════════

    private IEnumerator MixSaladPhase()
    {
        _phase = Phase.MixSalad;

        // Move bowl to center for mixing
        _bowlRT.anchorMin = new Vector2(0.5f, 0.5f);
        _bowlRT.anchorMax = new Vector2(0.5f, 0.5f);
        _bowlRT.sizeDelta = new Vector2(350f, 280f);
        _bowlRT.anchoredPosition = new Vector2(0, -30f);
        _bowlRT.localScale = Vector3.one * 1.2f;

        var promptGO = new GameObject("ShakePrompt");
        promptGO.transform.SetParent(playArea, false);
        var promptRT = promptGO.AddComponent<RectTransform>();
        promptRT.anchorMin = new Vector2(0.15f, 0.78f);
        promptRT.anchorMax = new Vector2(0.85f, 0.92f);
        promptRT.offsetMin = Vector2.zero;
        promptRT.offsetMax = Vector2.zero;
        var promptTMP = promptGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(promptTMP, "\u05E0\u05E2\u05E0\u05E2\u05D5 \u05D0\u05EA \u05D4\u05DE\u05DB\u05E9\u05D9\u05E8!");
        promptTMP.fontSize = 38;
        promptTMP.fontStyle = FontStyles.Bold;
        promptTMP.color = new Color(0.3f, 0.3f, 0.35f);
        promptTMP.alignment = TextAlignmentOptions.Center;
        promptTMP.raycastTarget = false;

        _shakeAccum = 0f;
        _shakeReady = true;
        _noShakeTimer = 0f;
        _fallbackMixBtn = null;

        while (_shakeAccum < ShakeNeeded)
        {
            _noShakeTimer += Time.deltaTime;
            if (_noShakeTimer > 5f && _fallbackMixBtn == null)
                _fallbackMixBtn = CreateMixButton();
            yield return null;
        }

        _shakeReady = false;
        if (_fallbackMixBtn != null) { Destroy(_fallbackMixBtn); _fallbackMixBtn = null; }
        Destroy(promptGO);

        // Restore bowl
        _bowlRT.localScale = Vector3.one;
    }

    private GameObject CreateMixButton()
    {
        var btnGO = new GameObject("MixButton");
        btnGO.transform.SetParent(playArea, false);
        var rt = btnGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.3f, 0.68f);
        rt.anchorMax = new Vector2(0.7f, 0.78f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var bg = btnGO.AddComponent<Image>();
        if (_roundedRect != null) { bg.sprite = _roundedRect; bg.type = Image.Type.Sliced; }
        bg.color = new Color(0.95f, 0.65f, 0.2f, 1f);

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(btnGO.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero; labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero; labelRT.offsetMax = Vector2.zero;
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(labelTMP, "\u05E2\u05E8\u05D1\u05D1\u05D5!");
        labelTMP.fontSize = 30;
        labelTMP.fontStyle = FontStyles.Bold;
        labelTMP.color = Color.white;
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.raycastTarget = false;

        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(() => _shakeAccum = ShakeNeeded);
        return btnGO;
    }

    protected override void OnGameplayUpdate()
    {
        if (!_shakeReady) return;

        Vector3 accel = Input.acceleration;
        float force = Mathf.Max(0, accel.magnitude - 1f);

        if (force > 0.3f)
        {
            _shakeAccum += force * Time.deltaTime * 3f;
            _noShakeTimer = 0f;

            foreach (var piece in _physicsPieces)
            {
                if (piece.rt == null) continue;
                piece.velocity += new Vector2(
                    Random.Range(-250f, 250f) * force,
                    Random.Range(-100f, 250f) * force
                ) * Time.deltaTime;
            }
        }

        // Physics update
        foreach (var piece in _physicsPieces)
        {
            if (piece.rt == null) continue;
            piece.velocity.y -= 300f * Time.deltaTime;
            piece.velocity *= 0.96f;

            Vector2 pos = piece.rt.anchoredPosition + piece.velocity * Time.deltaTime;

            // Bowl bounds (centered at 0, -30)
            float bw = 140f, bh = 100f, by = -30f;
            if (pos.x < -bw) { pos.x = -bw; piece.velocity.x *= -0.5f; }
            if (pos.x > bw) { pos.x = bw; piece.velocity.x *= -0.5f; }
            if (pos.y < by - bh) { pos.y = by - bh; piece.velocity.y *= -0.4f; }
            if (pos.y > by + bh) { pos.y = by + bh; piece.velocity.y *= -0.3f; }

            piece.rt.anchoredPosition = pos;
            piece.angularVelocity += piece.velocity.x * 0.08f;
            piece.angularVelocity *= 0.94f;
            float angle = piece.rt.localRotation.eulerAngles.z + piece.angularVelocity * Time.deltaTime;
            piece.rt.localRotation = Quaternion.Euler(0, 0, angle);
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

        var msgGO = new GameObject("ServeMsg");
        msgGO.transform.SetParent(playArea, false);
        var msgRT = msgGO.AddComponent<RectTransform>();
        msgRT.anchorMin = new Vector2(0.1f, 0.80f);
        msgRT.anchorMax = new Vector2(0.9f, 0.96f);
        msgRT.offsetMin = Vector2.zero;
        msgRT.offsetMax = Vector2.zero;
        var msgTMP = msgGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(msgTMP, "\u05D9\u05D5\u05E4\u05D9! \u05D4\u05E1\u05DC\u05D8 \u05DE\u05D5\u05DB\u05DF!");
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
        _bowlRT.anchorMin = new Vector2(0.65f, 0.02f);
        _bowlRT.anchorMax = new Vector2(0.95f, 0.35f);
        _bowlRT.offsetMin = Vector2.zero;
        _bowlRT.offsetMax = Vector2.zero;

        var bowlImg = _bowlPanel.AddComponent<Image>();
        if (_roundedRect != null) { bowlImg.sprite = _roundedRect; bowlImg.type = Image.Type.Sliced; }
        bowlImg.color = new Color(0.88f, 0.88f, 0.92f, 0.92f);
        bowlImg.raycastTarget = false;

        var shadow = _bowlPanel.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.15f, 0.1f, 0.1f, 0.2f);
        shadow.effectDistance = new Vector2(3, -4);

        var inner = new GameObject("Inner");
        inner.transform.SetParent(_bowlPanel.transform, false);
        var innerRT = inner.AddComponent<RectTransform>();
        innerRT.anchorMin = new Vector2(0.06f, 0.06f);
        innerRT.anchorMax = new Vector2(0.94f, 0.88f);
        innerRT.offsetMin = Vector2.zero;
        innerRT.offsetMax = Vector2.zero;
        var innerImg = inner.AddComponent<Image>();
        if (_roundedRect != null) { innerImg.sprite = _roundedRect; innerImg.type = Image.Type.Sliced; }
        innerImg.color = new Color(0.94f, 0.94f, 0.96f, 0.6f);
        innerImg.raycastTarget = false;
    }

    // ══════════════════════════════════════════════
    //  UTILITIES
    // ══════════════════════════════════════════════

    private static Texture2D ExtractSpriteTexture(Sprite sprite)
    {
        if (sprite == null) return null;
        var source = sprite.texture;
        Rect r = sprite.textureRect;
        int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return tex;
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

    private class CutPiece
    {
        public RectTransform rt;
        public Vector2 velocity;
        public float angularVelocity;
    }
}
