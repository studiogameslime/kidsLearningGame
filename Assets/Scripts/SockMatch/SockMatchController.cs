using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sock Match — socks hang on two clotheslines.
/// Tap a sock → it detaches and moves to the waiting area (bottom-center).
/// Tap a second sock → if match: celebration + both disappear. If not: both return.
/// </summary>
public class SockMatchController : BaseMiniGame
{
    [Header("References")]
    public RectTransform line1Area;
    public RectTransform line2Area;
    public RectTransform meetingPoint;
    public Sprite[] sockSprites;
    public Sprite[] clothespinSprites;
    public Sprite circleSprite;

    private Canvas canvas;
    private List<SockItem> allSocks = new List<SockItem>();
    private SockItem waitingSock; // sock sitting at the waiting area
    private int pairCount;
    private int matchedPairs;
    private bool inputLocked;

    private class SockItem
    {
        public GameObject go;
        public RectTransform rt;
        public Image img;
        public GameObject pinGO;
        public int sockId;
        public bool isMatched;
        public Vector2 hangPos;       // original position on the line
        public RectTransform lineArea; // which line it belongs to
        public float baseTilt;
        public Coroutine sway;
    }

    protected override string GetFallbackGameId() => "sockmatch";

    // Background — same palette as World scene
    private static readonly Color SkyColor = new Color(0.56f, 0.83f, 0.96f);           // #8FD4F5
    private static readonly Color HillsLargeColor = new Color(0.72f, 0.84f, 0.84f);    // #B7D7D6
    private static readonly Color HillsColor = new Color(0.62f, 0.80f, 0.77f);         // #9FCBC5
    private static readonly Color GroundBackColor = new Color(0.56f, 0.83f, 0.42f);    // #8ED36B
    private static readonly Color GroundFrontColor = new Color(0.47f, 0.79f, 0.34f);   // #79C956
    private static readonly Color CloudTint = new Color(0.96f, 0.98f, 1f, 0.85f);
    private List<RectTransform> clouds = new List<RectTransform>();
    private float[] cloudSpeeds;

    protected override void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        BuildBackground();
        base.Start();
    }

    protected override void OnGameInit()
    {
        totalRounds = 1;
        isEndless = true;
        playWinSound = true;
        playConfettiOnRoundWin = true;
        delayBeforeNextRound = 0.8f;
    }

    private void LateUpdate()
    {
        // Drift clouds continuously (even during completion animations)
        for (int i = 0; i < clouds.Count; i++)
        {
            if (clouds[i] == null) continue;
            var pos = clouds[i].anchoredPosition;
            pos.x += cloudSpeeds[i] * Time.deltaTime;
            if (pos.x > 2100f) pos.x = -300f;
            if (pos.x < -300f) pos.x = 2100f;
            clouds[i].anchoredPosition = pos;
        }
    }

    private void BuildBackground()
    {
        // Find the existing Background object and repurpose it as sky
        var bgTransform = canvas.transform.Find("Background");
        if (bgTransform == null) return;

        var bgImg = bgTransform.GetComponent<Image>();
        if (bgImg != null)
        {
            bgImg.color = SkyColor;
            bgImg.sprite = null; // solid color sky
        }

        var bgRT = bgTransform.GetComponent<RectTransform>();

        // Load layer sprites (same assets as World scene)
        var hillsLargeSprite = Resources.Load<Sprite>("WorldArt/hillsLarge");
        var hillsSprite = Resources.Load<Sprite>("WorldArt/hills");
        var groundBackSprite = Resources.Load<Sprite>("WorldArt/groundLayer1");
        var groundFrontSprite = Resources.Load<Sprite>("WorldArt/groundLayer2");

        // Layers at bottom of screen — same order/style as World scene (Image.Type.Simple)
        // Hills large — distant hills peeking behind the lower content
        CreateSpriteLayer(bgRT, "HillsLarge", hillsLargeSprite, HillsLargeColor,
            new Vector2(0, 0.15f), new Vector2(1, 0.42f));

        // Hills — closer hills
        CreateSpriteLayer(bgRT, "Hills", hillsSprite, HillsColor,
            new Vector2(0, 0.08f), new Vector2(1, 0.30f));

        // Ground back layer — green field
        CreateSpriteLayer(bgRT, "GroundBack", groundBackSprite, GroundBackColor,
            new Vector2(0, 0), new Vector2(1, 0.20f));

        // Ground front layer — darker foreground grass
        CreateSpriteLayer(bgRT, "GroundFront", groundFrontSprite, GroundFrontColor,
            new Vector2(0, 0), new Vector2(1, 0.10f));

        // Clouds (decorative, drifting in the sky area)
        SpawnClouds(bgRT);
    }

    private void SpawnClouds(RectTransform bgRT)
    {
        var cloudSprites = new Sprite[8];
        for (int i = 1; i <= 8; i++)
            cloudSprites[i - 1] = Resources.Load<Sprite>($"WorldArt/cloud{i}");

        int cloudCount = 6;
        clouds = new List<RectTransform>(cloudCount);
        cloudSpeeds = new float[cloudCount];

        for (int i = 0; i < cloudCount; i++)
        {
            var sprite = cloudSprites[Random.Range(0, cloudSprites.Length)];
            if (sprite == null) continue;

            var go = new GameObject($"Cloud_{i}");
            go.transform.SetParent(bgRT, false);
            var rt = go.AddComponent<RectTransform>();

            float scale = Random.Range(0.5f, 1.0f);
            rt.sizeDelta = new Vector2(sprite.rect.width * scale, sprite.rect.height * scale);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);

            float x = Random.Range(100f, 1800f);
            float y = Random.Range(-80f, -300f); // upper sky area
            rt.anchoredPosition = new Vector2(x, y);

            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = CloudTint;

            clouds.Add(rt);
            cloudSpeeds[i] = Random.Range(8f, 22f) * (Random.value > 0.5f ? 1f : -1f);
        }
    }

    /// <summary>Create a background layer with a sprite, Simple type (stretched), like World scene.</summary>
    private static void CreateSpriteLayer(RectTransform parent, string name, Sprite sprite,
        Color tint, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        img.color = tint;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
        }
    }

    protected override void OnRoundSetup()
    {
        matchedPairs = 0;
        waitingSock = null;
        inputLocked = false;

        pairCount = SockMatchLevels.PairCount(Difficulty);
        int[] indices = SockMatchLevels.PickSockIndices(pairCount);
        SpawnHangingSocks(indices);

        if (allSocks.Count >= 2 && TutorialHand != null)
        {
            SockItem first = allSocks[0];
            SockItem match = null;
            foreach (var s in allSocks)
                if (s != first && s.sockId == first.sockId) { match = s; break; }
            if (match != null)
            {
                var hp = TutorialHand.transform.parent as RectTransform;
                TutorialHand.SetMovePath(WToL(first.rt, hp), WToL(match.rt, hp), 1.5f);
            }
        }
    }

    protected override IEnumerator OnAfterComplete()
    {
        yield return new WaitForSeconds(0.5f);
    }

    protected override void OnRoundCleanup()
    {
        foreach (var s in allSocks)
        {
            if (s.go != null) Destroy(s.go);
            if (s.pinGO != null) Destroy(s.pinGO);
        }
        allSocks.Clear();
        waitingSock = null;
    }

    // ═══════════════════════════════════════════════════════════
    //  SPAWN
    // ═══════════════════════════════════════════════════════════

    private void SpawnHangingSocks(int[] pairIndices)
    {
        var sockIds = new List<int>();
        foreach (int id in pairIndices) { sockIds.Add(id); sockIds.Add(id); }
        for (int i = sockIds.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = sockIds[i]; sockIds[i] = sockIds[j]; sockIds[j] = tmp;
        }

        int total = sockIds.Count;
        int perLine1 = Mathf.CeilToInt(total / 2f);
        int perLine2 = total - perLine1;

        for (int i = 0; i < perLine1; i++)
            CreateHangingSock(line1Area, sockIds[i], i, perLine1);
        for (int i = 0; i < perLine2; i++)
            CreateHangingSock(line2Area, sockIds[perLine1 + i], i, perLine2);
    }

    private void CreateHangingSock(RectTransform lineArea, int sockId, int slot, int totalInLine)
    {
        float lineW = lineArea.rect.width > 0 ? lineArea.rect.width : 1600f;
        float lineH = lineArea.rect.height > 0 ? lineArea.rect.height : 250f;

        float spacing = lineW / (totalInLine + 1);
        float x = -lineW * 0.5f + spacing * (slot + 1);
        float sockSize = Mathf.Min(spacing * 0.7f, lineH * 0.65f, 120f);
        float tilt = Random.Range(-6f, 6f);

        // Pin — sits on top of the sock, overlapping the rope
        float pinW = sockSize * 0.3f;
        float pinH = sockSize * 0.45f;
        float ropeY = lineH * 0.5f; // rope is near top of line area

        var pinGO = new GameObject($"Pin_{allSocks.Count}");
        pinGO.transform.SetParent(lineArea, false);
        var pinRT = pinGO.AddComponent<RectTransform>();
        pinRT.sizeDelta = new Vector2(pinW, pinH);
        pinRT.anchoredPosition = new Vector2(x, ropeY - pinH * 0.15f);
        var pinImg = pinGO.AddComponent<Image>();
        if (clothespinSprites != null && clothespinSprites.Length > 0)
            pinImg.sprite = clothespinSprites[allSocks.Count % clothespinSprites.Length];
        pinImg.preserveAspect = true;
        pinImg.raycastTarget = false;

        // Sock — hangs directly under the pin, top touching the pin bottom
        var go = new GameObject($"Sock_{sockId}_{allSocks.Count}");
        go.transform.SetParent(lineArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(sockSize, sockSize);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(x, ropeY - pinH * 0.55f);
        rt.localEulerAngles = new Vector3(0, 0, tilt);

        var img = go.AddComponent<Image>();
        img.sprite = sockSprites[sockId % sockSprites.Length];
        img.preserveAspect = true;
        img.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None;

        var item = new SockItem
        {
            go = go, rt = rt, img = img, pinGO = pinGO,
            sockId = sockId,
            hangPos = rt.anchoredPosition,
            lineArea = lineArea,
            baseTilt = tilt
        };

        item.sway = StartCoroutine(BreezeSway(item));

        int idx = allSocks.Count;
        btn.onClick.AddListener(() => OnSockTapped(idx));
        allSocks.Add(item);
    }

    // ═══════════════════════════════════════════════════════════
    //  TAP LOGIC — sock goes down, then match check
    // ═══════════════════════════════════════════════════════════

    private void OnSockTapped(int index)
    {
        if (inputLocked) return;
        if (index < 0 || index >= allSocks.Count) return;
        var sock = allSocks[index];
        if (sock.isMatched) return;

        DismissTutorial();

        if (waitingSock == null)
        {
            // First tap — detach sock and send to waiting area
            StartCoroutine(DetachToWaiting(sock));
        }
        else if (waitingSock == sock)
        {
            // Tapped the same waiting sock — send it back
            StartCoroutine(ReturnToLine(sock));
            waitingSock = null;
        }
        else
        {
            // Second tap — detach and check match
            StartCoroutine(DetachAndCheck(waitingSock, sock));
            waitingSock = null;
        }
    }

    private IEnumerator DetachToWaiting(SockItem sock)
    {
        inputLocked = true;

        // Stop sway
        if (sock.sway != null) { StopCoroutine(sock.sway); sock.sway = null; }

        // Fade pin
        StartCoroutine(FadeOut(sock.pinGO, 0.15f));

        // Reparent to canvas for free movement
        sock.go.transform.SetParent(canvas.transform, true);
        sock.go.transform.SetAsLastSibling();

        // Fly to waiting spot (left of center)
        Vector2 target = GetWaitingPosition(-60f);
        yield return StartCoroutine(FlyTo(sock, target, 0.3f, true));

        sock.rt.localScale = new Vector3(1.15f, 1.15f, 1f); // slightly larger = "selected"
        waitingSock = sock;
        inputLocked = false;
    }

    private IEnumerator DetachAndCheck(SockItem first, SockItem second)
    {
        inputLocked = true;

        // Stop sway on second sock
        if (second.sway != null) { StopCoroutine(second.sway); second.sway = null; }
        StartCoroutine(FadeOut(second.pinGO, 0.15f));

        // Reparent second to canvas
        second.go.transform.SetParent(canvas.transform, true);
        second.go.transform.SetAsLastSibling();

        // Fly second to right of center
        Vector2 target2 = GetWaitingPosition(60f);
        yield return StartCoroutine(FlyTo(second, target2, 0.3f, true));
        second.rt.localScale = new Vector3(1.15f, 1.15f, 1f);

        yield return new WaitForSeconds(0.15f);

        if (first.sockId == second.sockId)
        {
            // ── MATCH! ──
            first.isMatched = true;
            second.isMatched = true;
            matchedPairs++;
            bool isLast = matchedPairs >= pairCount;
            RecordCorrect(isLast: isLast);

            // Bounce together
            Vector2 center = GetWaitingPosition(0f);
            StartCoroutine(FlyTo(first, center + new Vector2(-20, 0), 0.15f, false));
            yield return StartCoroutine(FlyTo(second, center + new Vector2(20, 0), 0.15f, false));

            // Sparkles
            PlayCorrectEffect(first.rt);

            // Cute spin
            yield return StartCoroutine(CelebrationSpin(first, second));

            // Pop disappear
            yield return StartCoroutine(PopDisappear(first, second));

            inputLocked = false;

            if (isLast)
                CompleteRound();
        }
        else
        {
            // ── NO MATCH ──
            RecordMistake();

            // Shake both
            StartCoroutine(Shake(first));
            yield return StartCoroutine(Shake(second));

            yield return new WaitForSeconds(0.1f);

            // Return both to their lines
            StartCoroutine(ReturnToLine(first));
            yield return StartCoroutine(ReturnToLine(second));

            inputLocked = false;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  ANIMATIONS
    // ═══════════════════════════════════════════════════════════

    private Vector2 GetWaitingPosition(float xOffset)
    {
        if (meetingPoint != null)
        {
            Vector2 scr = RectTransformUtility.WorldToScreenPoint(null, meetingPoint.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, scr, null, out Vector2 pos);
            return pos + new Vector2(xOffset, 0);
        }
        return new Vector2(xOffset, -350f);
    }

    private IEnumerator FlyTo(SockItem sock, Vector2 target, float dur, bool resetRotation)
    {
        Vector2 start = sock.rt.anchoredPosition;
        float startAngle = sock.rt.localEulerAngles.z;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0, 1, t / dur);
            sock.rt.anchoredPosition = Vector2.Lerp(start, target, p);
            if (resetRotation)
                sock.rt.localEulerAngles = new Vector3(0, 0, Mathf.Lerp(startAngle, 0, p));
            yield return null;
        }
        sock.rt.anchoredPosition = target;
        if (resetRotation) sock.rt.localEulerAngles = Vector3.zero;
    }

    private IEnumerator ReturnToLine(SockItem sock)
    {
        // Reparent back to line
        sock.go.transform.SetParent(sock.lineArea, true);

        // Restore pin
        if (sock.pinGO != null)
        {
            sock.pinGO.SetActive(true);
            var pinImg = sock.pinGO.GetComponent<Image>();
            if (pinImg != null) pinImg.color = new Color(pinImg.color.r, pinImg.color.g, pinImg.color.b, 1f);
        }

        // Convert hang position to current space
        sock.rt.pivot = new Vector2(0.5f, 1f);
        Vector2 start = sock.rt.anchoredPosition;
        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0, 1, t / 0.35f);
            sock.rt.anchoredPosition = Vector2.Lerp(start, sock.hangPos, p);
            sock.rt.localEulerAngles = new Vector3(0, 0, Mathf.Lerp(0, sock.baseTilt, p));
            sock.rt.localScale = Vector3.Lerp(Vector3.one * 1.15f, Vector3.one, p);
            yield return null;
        }
        sock.rt.anchoredPosition = sock.hangPos;
        sock.rt.localEulerAngles = new Vector3(0, 0, sock.baseTilt);
        sock.rt.localScale = Vector3.one;

        // Restart sway
        sock.sway = StartCoroutine(BreezeSway(sock));
    }

    private IEnumerator CelebrationSpin(SockItem a, SockItem b)
    {
        float t = 0f;
        float dur = 0.3f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float angle = t / dur * 360f;
            a.rt.localEulerAngles = new Vector3(0, 0, angle);
            b.rt.localEulerAngles = new Vector3(0, 0, -angle);
            float bounce = 1f + 0.15f * Mathf.Sin(t / dur * Mathf.PI);
            a.rt.localScale = Vector3.one * bounce;
            b.rt.localScale = Vector3.one * bounce;
            yield return null;
        }
    }

    private IEnumerator PopDisappear(SockItem a, SockItem b)
    {
        // Scale up then shrink to zero
        float t = 0f;
        while (t < 0.1f)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(1f, 1.3f, t / 0.1f);
            a.rt.localScale = Vector3.one * s;
            b.rt.localScale = Vector3.one * s;
            yield return null;
        }
        t = 0f;
        while (t < 0.15f)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(1.3f, 0f, Mathf.SmoothStep(0, 1, t / 0.15f));
            a.rt.localScale = Vector3.one * s;
            b.rt.localScale = Vector3.one * s;
            yield return null;
        }
        a.go.SetActive(false);
        b.go.SetActive(false);
    }

    private IEnumerator Shake(SockItem sock)
    {
        Vector2 pos = sock.rt.anchoredPosition;
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            float offset = Mathf.Sin(t * 40f) * 10f * (1f - t / 0.3f);
            sock.rt.anchoredPosition = new Vector2(pos.x + offset, pos.y);
            yield return null;
        }
        sock.rt.anchoredPosition = pos;
    }

    private IEnumerator BreezeSway(SockItem sock)
    {
        float phase = Random.Range(0f, Mathf.PI * 2f);
        float speed = Random.Range(1.5f, 2.5f);
        float amp = Random.Range(3f, 5f);
        while (!sock.isMatched)
        {
            phase += Time.deltaTime * speed;
            sock.rt.localEulerAngles = new Vector3(0, 0, sock.baseTilt + Mathf.Sin(phase) * amp);
            yield return null;
        }
    }

    private IEnumerator FadeOut(GameObject go, float dur)
    {
        if (go == null) yield break;
        var img = go.GetComponent<Image>();
        if (img == null) { go.SetActive(false); yield break; }
        Color c = img.color;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            img.color = new Color(c.r, c.g, c.b, 1f - t / dur);
            yield return null;
        }
        go.SetActive(false);
    }

    public void OnHomePressed() => ExitGame();

    private static Vector2 WToL(RectTransform src, RectTransform par)
    {
        Vector2 scr = RectTransformUtility.WorldToScreenPoint(null, src.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(par, scr, null, out Vector2 l);
        return l;
    }
}
