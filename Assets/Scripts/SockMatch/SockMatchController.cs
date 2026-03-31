using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sock Match Game — socks hang on two clotheslines with pins.
/// Tap two matching socks → they detach, fly to center, sparkle, and disappear.
/// </summary>
public class SockMatchController : BaseMiniGame
{
    [Header("References")]
    public RectTransform line1Area;        // upper clothesline row
    public RectTransform line2Area;        // lower clothesline row
    public RectTransform meetingPoint;     // bottom-center where matched socks fly to
    public Sprite[] sockSprites;           // Socks_0..11
    public Sprite[] clothespinSprites;     // Clothespins_0..3
    public Sprite circleSprite;

    private Canvas canvas;
    private List<SockItem> allSocks = new List<SockItem>();
    private SockItem selectedSock;
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
        public Coroutine sway;
    }

    protected override string GetFallbackGameId() => "sockmatch";

    protected override void Start()
    {
        canvas = GetComponentInParent<Canvas>();
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

    protected override void OnRoundSetup()
    {
        matchedPairs = 0;
        selectedSock = null;
        inputLocked = false;

        pairCount = SockMatchLevels.PairCount(Difficulty);
        int[] indices = SockMatchLevels.PickSockIndices(pairCount);

        SpawnHangingSocks(indices);

        // Tutorial hint
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
        // Brief pause to enjoy the win before any cleanup
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
        selectedSock = null;
    }

    // ═══════════════════════════════════════════════════════════
    //  SPAWN — hang socks on two clotheslines with clothespins
    // ═══════════════════════════════════════════════════════════

    private void SpawnHangingSocks(int[] pairIndices)
    {
        // Two of each → shuffled
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

        // Spawn on line 1
        for (int i = 0; i < perLine1; i++)
            CreateHangingSock(line1Area, sockIds[i], i, perLine1);

        // Spawn on line 2
        for (int i = 0; i < perLine2; i++)
            CreateHangingSock(line2Area, sockIds[perLine1 + i], i, perLine2);
    }

    private void CreateHangingSock(RectTransform lineArea, int sockId, int slotIndex, int totalInLine)
    {
        float lineW = lineArea.rect.width;
        if (lineW <= 0) lineW = 1600f;
        float lineH = lineArea.rect.height;
        if (lineH <= 0) lineH = 250f;

        float spacing = lineW / (totalInLine + 1);
        float x = -lineW * 0.5f + spacing * (slotIndex + 1);
        float sockSize = Mathf.Min(spacing * 0.75f, lineH * 0.7f, 140f);
        float tilt = Random.Range(-6f, 6f); // slight random tilt

        // ── Clothespin (on top, attached to rope) ──
        var pinGO = new GameObject($"Pin_{sockId}_{allSocks.Count}");
        pinGO.transform.SetParent(lineArea, false);
        var pinRT = pinGO.AddComponent<RectTransform>();
        pinRT.sizeDelta = new Vector2(28, 45);
        pinRT.anchoredPosition = new Vector2(x, lineH * 0.5f - 5f); // near top of area
        var pinImg = pinGO.AddComponent<Image>();
        if (clothespinSprites != null && clothespinSprites.Length > 0)
            pinImg.sprite = clothespinSprites[allSocks.Count % clothespinSprites.Length];
        pinImg.preserveAspect = true;
        pinImg.raycastTarget = false;

        // ── Sock (hanging below pin) ──
        var go = new GameObject($"Sock_{sockId}_{allSocks.Count}");
        go.transform.SetParent(lineArea, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(sockSize, sockSize);
        rt.pivot = new Vector2(0.5f, 1f); // pivot at top for natural hang
        rt.anchoredPosition = new Vector2(x, lineH * 0.5f - 30f); // below pin
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
            sockId = sockId
        };

        // Gentle breeze sway
        item.sway = StartCoroutine(BreezeSway(item, tilt));

        int idx = allSocks.Count;
        btn.onClick.AddListener(() => OnSockTapped(idx));
        allSocks.Add(item);
    }

    // ═══════════════════════════════════════════════════════════
    //  TAP LOGIC
    // ═══════════════════════════════════════════════════════════

    private void OnSockTapped(int index)
    {
        if (inputLocked) return;
        if (index < 0 || index >= allSocks.Count) return;
        var sock = allSocks[index];
        if (sock.isMatched) return;

        DismissTutorial();

        if (selectedSock == null)
        {
            selectedSock = sock;
            StartCoroutine(SelectAnim(sock, true));
        }
        else if (selectedSock == sock)
        {
            StartCoroutine(SelectAnim(sock, false));
            selectedSock = null;
        }
        else
        {
            if (selectedSock.sockId == sock.sockId)
                StartCoroutine(CorrectMatch(selectedSock, sock));
            else
                StartCoroutine(WrongMatch(selectedSock, sock));
            selectedSock = null;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  ANIMATIONS
    // ═══════════════════════════════════════════════════════════

    private IEnumerator SelectAnim(SockItem sock, bool selected)
    {
        float target = selected ? 1.2f : 1f;
        float dur = 0.15f, t = 0f;
        float from = sock.rt.localScale.x;
        while (t < dur)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(from, target, Mathf.SmoothStep(0, 1, t / dur));
            sock.rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        sock.rt.localScale = new Vector3(target, target, 1f);
    }

    private IEnumerator CorrectMatch(SockItem a, SockItem b)
    {
        inputLocked = true;
        a.isMatched = true;
        b.isMatched = true;

        // Stop sway
        if (a.sway != null) StopCoroutine(a.sway);
        if (b.sway != null) StopCoroutine(b.sway);

        matchedPairs++;
        bool isLast = matchedPairs >= pairCount;
        RecordCorrect(isLast: isLast);

        // Fade out clothespins
        StartCoroutine(FadeAndDestroy(a.pinGO, 0.2f));
        StartCoroutine(FadeAndDestroy(b.pinGO, 0.2f));

        // Reparent socks to canvas for free movement
        a.go.transform.SetParent(canvas.transform, true);
        b.go.transform.SetParent(canvas.transform, true);
        a.go.transform.SetAsLastSibling();
        b.go.transform.SetAsLastSibling();

        // Fly both socks to meeting point (bottom-center) with slight curve
        Vector2 target = meetingPoint != null ? WToL(meetingPoint, canvas.transform as RectTransform) : Vector2.zero;

        yield return StartCoroutine(FlyToMeetingPoint(a, b, target));

        // Sparkle at meeting point
        PlayCorrectEffect(a.rt);

        // Soft pop — scale down and destroy
        yield return StartCoroutine(PopDisappear(a, b));

        inputLocked = false;

        if (isLast)
        {
            yield return new WaitForSeconds(0.2f);
            CompleteRound();
        }
    }

    private IEnumerator FlyToMeetingPoint(SockItem a, SockItem b, Vector2 target)
    {
        Vector2 startA = a.rt.anchoredPosition;
        Vector2 startB = b.rt.anchoredPosition;
        // Reset rotation and scale
        a.rt.localEulerAngles = Vector3.zero;
        b.rt.localEulerAngles = Vector3.zero;
        a.rt.localScale = Vector3.one;
        b.rt.localScale = Vector3.one;

        float dur = 0.45f, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0, 1, t / dur);
            // Slight curve via vertical overshoot
            float curve = Mathf.Sin(p * Mathf.PI) * 40f;
            a.rt.anchoredPosition = Vector2.Lerp(startA, target + new Vector2(-25, 0), p) + new Vector2(0, curve);
            b.rt.anchoredPosition = Vector2.Lerp(startB, target + new Vector2(25, 0), p) + new Vector2(0, curve);
            // Gentle spin during flight
            float spin = p * 180f;
            a.rt.localEulerAngles = new Vector3(0, 0, spin);
            b.rt.localEulerAngles = new Vector3(0, 0, -spin);
            yield return null;
        }
    }

    private IEnumerator PopDisappear(SockItem a, SockItem b)
    {
        // Quick scale up then shrink to zero
        float t = 0f;
        while (t < 0.12f)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(1f, 1.3f, t / 0.12f);
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

    private IEnumerator WrongMatch(SockItem a, SockItem b)
    {
        inputLocked = true;
        RecordMistake();

        StartCoroutine(ShakeSock(a));
        yield return StartCoroutine(ShakeSock(b));

        a.rt.localScale = Vector3.one;
        b.rt.localScale = Vector3.one;
        inputLocked = false;
    }

    private IEnumerator ShakeSock(SockItem sock)
    {
        float baseAngle = sock.rt.localEulerAngles.z;
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            float shake = Mathf.Sin(t * 40f) * 8f * (1f - t / 0.3f);
            sock.rt.localEulerAngles = new Vector3(0, 0, baseAngle + shake);
            yield return null;
        }
    }

    private IEnumerator BreezeSway(SockItem sock, float baseTilt)
    {
        float phase = Random.Range(0f, Mathf.PI * 2f);
        float speed = Random.Range(1.5f, 2.5f);
        float amplitude = Random.Range(3f, 5f);
        while (!sock.isMatched)
        {
            phase += Time.deltaTime * speed;
            sock.rt.localEulerAngles = new Vector3(0, 0, baseTilt + Mathf.Sin(phase) * amplitude);
            yield return null;
        }
    }

    private IEnumerator FadeAndDestroy(GameObject go, float dur)
    {
        if (go == null) yield break;
        var img = go.GetComponent<Image>();
        if (img == null) { Destroy(go); yield break; }
        Color c = img.color;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            img.color = new Color(c.r, c.g, c.b, Mathf.Lerp(1f, 0f, t / dur));
            yield return null;
        }
        Destroy(go);
    }

    public void OnHomePressed() => NavigationManager.GoToWorld();

    private static Vector2 WToL(RectTransform src, RectTransform par)
    {
        Vector2 scr = RectTransformUtility.WorldToScreenPoint(null, src.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(par, scr, null, out Vector2 l);
        return l;
    }
    private static Vector2 WToL(Transform src, RectTransform par)
    {
        Vector2 scr = RectTransformUtility.WorldToScreenPoint(null, src.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(par, scr, null, out Vector2 l);
        return l;
    }
}
