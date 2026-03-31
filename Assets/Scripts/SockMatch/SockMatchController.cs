using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sock Match Game — tap pairs of matching socks, they fly to the clothesline.
/// Tap-to-select: first tap selects, second tap on matching sock = match.
/// </summary>
public class SockMatchController : BaseMiniGame
{
    [Header("References")]
    public RectTransform clotheslineArea;  // top — where matched pairs hang
    public RectTransform socksArea;        // bottom — scattered socks
    public Sprite[] sockSprites;           // Socks_0..11
    public Sprite[] clothespinSprites;     // Clothespins_0..3
    public Sprite circleSprite;

    private Canvas canvas;
    private List<SockItem> allSocks = new List<SockItem>();
    private SockItem selectedSock;
    private int pairCount;
    private int matchedPairs;
    private int nextLineSlot;
    private bool inputLocked;

    private class SockItem
    {
        public GameObject go;
        public RectTransform rt;
        public Image img;
        public int sockId;
        public bool isMatched;
        public Vector2 startPos;
        public Coroutine wiggle;
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
        nextLineSlot = 0;
        selectedSock = null;
        inputLocked = false;

        pairCount = SockMatchLevels.PairCount(Difficulty);
        int[] indices = SockMatchLevels.PickSockIndices(pairCount);

        SpawnSocks(indices);

        // Tutorial hint: highlight first pair
        if (allSocks.Count >= 2 && TutorialHand != null)
        {
            SockItem first = allSocks[0];
            SockItem match = null;
            foreach (var s in allSocks)
                if (s != first && s.sockId == first.sockId) { match = s; break; }
            if (match != null)
            {
                var hp = TutorialHand.transform.parent as RectTransform;
                TutorialHand.SetMovePath(
                    WorldToLocal(first.rt, hp),
                    WorldToLocal(match.rt, hp), 1.5f);
            }
        }
    }

    protected override void OnRoundCleanup()
    {
        foreach (var s in allSocks) if (s.go != null) Destroy(s.go);
        allSocks.Clear();
        selectedSock = null;
        // Clear clothesline children (hung pairs)
        for (int i = clotheslineArea.childCount - 1; i >= 0; i--)
            Destroy(clotheslineArea.GetChild(i).gameObject);
    }

    // ── Spawn socks ──

    private void SpawnSocks(int[] pairIndices)
    {
        // Create two of each sock index
        var sockIds = new List<int>();
        foreach (int id in pairIndices) { sockIds.Add(id); sockIds.Add(id); }

        // Shuffle
        for (int i = sockIds.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = sockIds[i]; sockIds[i] = sockIds[j]; sockIds[j] = tmp;
        }

        float areaW = socksArea.rect.width;
        float areaH = socksArea.rect.height;
        if (areaW <= 0) areaW = 1600f;
        if (areaH <= 0) areaH = 400f;

        int total = sockIds.Count;
        int cols = Mathf.CeilToInt(total / 2f);
        int rows = 2;
        float padding = 20f;
        float cellW = (areaW - padding * 2) / cols;
        float cellH = (areaH - padding * 2) / rows;
        float sockSize = Mathf.Min(cellW, cellH) * 0.8f;

        for (int i = 0; i < total; i++)
        {
            int col = i % cols;
            int row = i / cols;

            float cx = -areaW * 0.5f + padding + cellW * (col + 0.5f) + Random.Range(-8f, 8f);
            float cy = areaH * 0.5f - padding - cellH * (row + 0.5f) + Random.Range(-5f, 5f);

            var go = new GameObject($"Sock_{sockIds[i]}_{i}");
            go.transform.SetParent(socksArea, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(sockSize, sockSize);
            rt.anchoredPosition = new Vector2(cx, cy);

            var img = go.AddComponent<Image>();
            int spriteIdx = sockIds[i] % sockSprites.Length;
            img.sprite = sockSprites[spriteIdx];
            img.preserveAspect = true;
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;

            var item = new SockItem
            {
                go = go, rt = rt, img = img,
                sockId = sockIds[i],
                startPos = rt.anchoredPosition
            };

            // Idle wiggle
            item.wiggle = StartCoroutine(IdleWiggle(item));

            int capturedIdx = allSocks.Count;
            btn.onClick.AddListener(() => OnSockTapped(capturedIdx));

            allSocks.Add(item);
        }
    }

    // ── Tap logic ──

    private void OnSockTapped(int index)
    {
        if (inputLocked) return;
        if (index < 0 || index >= allSocks.Count) return;

        var sock = allSocks[index];
        if (sock.isMatched) return;

        DismissTutorial();

        if (selectedSock == null)
        {
            // First selection
            selectedSock = sock;
            StartCoroutine(SelectAnim(sock, true));
        }
        else if (selectedSock == sock)
        {
            // Deselect
            StartCoroutine(SelectAnim(sock, false));
            selectedSock = null;
        }
        else
        {
            // Second selection — check match
            if (selectedSock.sockId == sock.sockId)
                StartCoroutine(CorrectMatch(selectedSock, sock));
            else
                StartCoroutine(WrongMatch(selectedSock, sock));
            selectedSock = null;
        }
    }

    // ── Animations ──

    private IEnumerator SelectAnim(SockItem sock, bool selected)
    {
        float target = selected ? 1.2f : 1f;
        float dur = 0.15f;
        float t = 0f;
        float from = sock.rt.localScale.x;
        while (t < dur)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(from, target, Mathf.SmoothStep(0, 1, t / dur));
            sock.rt.localScale = Vector3.one * s;
            yield return null;
        }
        sock.rt.localScale = Vector3.one * target;
    }

    private IEnumerator CorrectMatch(SockItem a, SockItem b)
    {
        inputLocked = true;
        a.isMatched = true;
        b.isMatched = true;

        // Stop wiggles
        if (a.wiggle != null) StopCoroutine(a.wiggle);
        if (b.wiggle != null) StopCoroutine(b.wiggle);
        a.rt.localEulerAngles = Vector3.zero;
        b.rt.localEulerAngles = Vector3.zero;

        matchedPairs++;
        bool isLast = matchedPairs >= pairCount;
        RecordCorrect(isLast: isLast);
        PlayCorrectEffect(a.rt);

        // Bounce toward each other
        Vector2 midpoint = (a.rt.anchoredPosition + b.rt.anchoredPosition) * 0.5f;
        yield return StartCoroutine(MoveSock(a, midpoint + new Vector2(-30, 0), 0.25f));
        StartCoroutine(MoveSock(b, midpoint + new Vector2(30, 0), 0.01f)); // instant snap

        yield return new WaitForSeconds(0.15f);

        // Fly to clothesline
        yield return StartCoroutine(FlyToClothesline(a, b));

        inputLocked = false;

        if (isLast)
            StartCoroutine(CompletionBounce());
    }

    private IEnumerator WrongMatch(SockItem a, SockItem b)
    {
        inputLocked = true;
        RecordMistake();

        // Shake both
        yield return StartCoroutine(ShakeSock(a));
        StartCoroutine(ShakeSock(b));
        yield return new WaitForSeconds(0.15f);

        // Deselect
        a.rt.localScale = Vector3.one;
        b.rt.localScale = Vector3.one;

        inputLocked = false;
    }

    private IEnumerator MoveSock(SockItem sock, Vector2 target, float dur)
    {
        Vector2 from = sock.rt.anchoredPosition;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            sock.rt.anchoredPosition = Vector2.Lerp(from, target, Mathf.SmoothStep(0, 1, t / dur));
            yield return null;
        }
        sock.rt.anchoredPosition = target;
    }

    private IEnumerator FlyToClothesline(SockItem a, SockItem b)
    {
        // Calculate target position on clothesline
        float lineW = clotheslineArea.rect.width;
        if (lineW <= 0) lineW = 1600f;
        float slotSpacing = lineW / (pairCount + 1);
        float targetX = -lineW * 0.5f + slotSpacing * (nextLineSlot + 1);
        nextLineSlot++;

        // Convert clothesline slot to socksArea space
        Vector3 worldTarget = clotheslineArea.TransformPoint(new Vector3(targetX, 0, 0));
        Vector2 screenTarget = RectTransformUtility.WorldToScreenPoint(null, worldTarget);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            socksArea, screenTarget, null, out Vector2 localTarget);

        // Fly both socks up together
        Vector2 startA = a.rt.anchoredPosition;
        Vector2 startB = b.rt.anchoredPosition;
        float dur = 0.5f;
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0, 1, t / dur);
            a.rt.anchoredPosition = Vector2.Lerp(startA, localTarget + new Vector2(-25, 0), p);
            b.rt.anchoredPosition = Vector2.Lerp(startB, localTarget + new Vector2(25, 0), p);

            // Shrink slightly during flight
            float scale = Mathf.Lerp(1f, 0.7f, p);
            a.rt.localScale = Vector3.one * scale;
            b.rt.localScale = Vector3.one * scale;
            yield return null;
        }

        // Hide originals, create hung pair on clothesline
        a.go.SetActive(false);
        b.go.SetActive(false);

        CreateHungPair(a.sockId, targetX);
    }

    private void CreateHungPair(int sockId, float x)
    {
        // Container
        var pairGO = new GameObject("HungPair");
        pairGO.transform.SetParent(clotheslineArea, false);
        var pairRT = pairGO.AddComponent<RectTransform>();
        pairRT.anchoredPosition = new Vector2(x, -30);
        pairRT.sizeDelta = new Vector2(120, 100);

        // Clothespin
        if (clothespinSprites != null && clothespinSprites.Length > 0)
        {
            var pinGO = new GameObject("Pin");
            pinGO.transform.SetParent(pairGO.transform, false);
            var pinRT = pinGO.AddComponent<RectTransform>();
            pinRT.anchorMin = pinRT.anchorMax = new Vector2(0.5f, 1f);
            pinRT.sizeDelta = new Vector2(30, 50);
            pinRT.anchoredPosition = new Vector2(0, 10);
            var pinImg = pinGO.AddComponent<Image>();
            pinImg.sprite = clothespinSprites[sockId % clothespinSprites.Length];
            pinImg.preserveAspect = true;
            pinImg.raycastTarget = false;
        }

        // Left sock
        var leftGO = new GameObject("SockL");
        leftGO.transform.SetParent(pairGO.transform, false);
        var lrt = leftGO.AddComponent<RectTransform>();
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.3f, 0.3f);
        lrt.sizeDelta = new Vector2(55, 55);
        var limg = leftGO.AddComponent<Image>();
        limg.sprite = sockSprites[sockId % sockSprites.Length];
        limg.preserveAspect = true;
        limg.raycastTarget = false;

        // Right sock (flipped)
        var rightGO = new GameObject("SockR");
        rightGO.transform.SetParent(pairGO.transform, false);
        var rrt = rightGO.AddComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = new Vector2(0.7f, 0.3f);
        rrt.sizeDelta = new Vector2(55, 55);
        rrt.localScale = new Vector3(-1, 1, 1); // flip horizontal
        var rimg = rightGO.AddComponent<Image>();
        rimg.sprite = sockSprites[sockId % sockSprites.Length];
        rimg.preserveAspect = true;
        rimg.raycastTarget = false;

        // Pop-in animation
        StartCoroutine(PopIn(pairRT));
    }

    private IEnumerator PopIn(RectTransform rt)
    {
        rt.localScale = Vector3.zero;
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            float p = t / 0.3f;
            float s = p < 0.7f ? Mathf.Lerp(0, 1.15f, p / 0.7f) : Mathf.Lerp(1.15f, 1f, (p - 0.7f) / 0.3f);
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private IEnumerator ShakeSock(SockItem sock)
    {
        Vector2 pos = sock.rt.anchoredPosition;
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            float offset = Mathf.Sin(t * 45f) * 10f * (1f - t / 0.3f);
            sock.rt.anchoredPosition = new Vector2(pos.x + offset, pos.y);
            yield return null;
        }
        sock.rt.anchoredPosition = pos;
        sock.rt.localScale = Vector3.one;
    }

    private IEnumerator IdleWiggle(SockItem sock)
    {
        float phase = Random.Range(0f, Mathf.PI * 2f);
        while (!sock.isMatched)
        {
            phase += Time.deltaTime;
            sock.rt.localEulerAngles = new Vector3(0, 0, Mathf.Sin(phase * 2.5f) * 4f);
            yield return null;
        }
        sock.rt.localEulerAngles = Vector3.zero;
    }

    private IEnumerator CompletionBounce()
    {
        yield return new WaitForSeconds(0.3f);
        // Bounce all hung pairs
        for (int i = 0; i < clotheslineArea.childCount; i++)
        {
            var child = clotheslineArea.GetChild(i);
            StartCoroutine(BounceOnce(child.GetComponent<RectTransform>(), i * 0.08f));
        }
        yield return new WaitForSeconds(0.5f);
        CompleteRound();
    }

    private IEnumerator BounceOnce(RectTransform rt, float delay)
    {
        if (rt == null) yield break;
        yield return new WaitForSeconds(delay);
        Vector2 orig = rt.anchoredPosition;
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            float bounce = Mathf.Abs(Mathf.Sin(t / 0.3f * Mathf.PI)) * 20f;
            rt.anchoredPosition = orig + new Vector2(0, bounce);
            yield return null;
        }
        rt.anchoredPosition = orig;
    }

    public void OnHomePressed() => NavigationManager.GoToWorld();

    private static Vector2 WorldToLocal(RectTransform source, RectTransform parent)
    {
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, source.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out Vector2 local);
        return local;
    }
}
