using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Global finger trail effect — spawns colorful fading particles that follow
/// the player's finger / mouse. Auto-creates via [RuntimeInitializeOnLoadMethod]
/// so it works in every scene with zero setup.
/// </summary>
public class FingerTrail : MonoBehaviour
{
    // ── Configuration ──────────────────────────────────────────────
    const int PoolSize = 60;
    const float ParticleDuration = 0.5f;
    const int SpawnInterval = 1; // spawn every frame
    const float MinSize = 20f;
    const float MaxSize = 35f;
    const float DriftStrength = 30f;
    const float EndScale = 0.3f;
    const int SortOrder = 998; // below TutorialHand (999)

    // Kid-friendly rainbow palette
    static readonly Color[] Palette = new Color[]
    {
        new Color(1.00f, 0.42f, 0.70f), // Pink
        new Color(1.00f, 0.25f, 0.25f), // Red
        new Color(1.00f, 0.60f, 0.15f), // Orange
        new Color(1.00f, 0.90f, 0.20f), // Yellow
        new Color(0.30f, 0.85f, 0.40f), // Green
        new Color(0.20f, 0.85f, 0.90f), // Cyan
        new Color(0.30f, 0.50f, 1.00f), // Blue
        new Color(0.70f, 0.35f, 0.95f), // Purple
    };

    // ── Public API ─────────────────────────────────────────────────
    public static void SetEnabled(bool enabled)
    {
        if (_instance != null && _instance._canvasGroup != null)
            _instance._canvasGroup.alpha = enabled ? 1f : 0f;
        _disabled = !enabled;
    }
    private static bool _disabled;

    // ── Runtime state ──────────────────────────────────────────────
    static FingerTrail _instance;

    Canvas _canvas;
    CanvasGroup _canvasGroup;
    RectTransform _canvasRect;
    Sprite _circleSprite;

    Image[] _pool;
    RectTransform[] _poolRects;
    bool[] _inUse;
    int _frameCounter;

    // ── Auto-bootstrap ─────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (_instance != null) return;

        var go = new GameObject("[FingerTrail]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<FingerTrail>();
    }

    // ── Lifecycle ──────────────────────────────────────────────────
    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        CreateCircleSprite();
        CreateCanvas();
        CreatePool();
        Debug.Log($"[FingerTrail] Initialized — pool={PoolSize}, canvas={_canvas != null}");
    }

    void Update()
    {
        if (_canvas == null || _pool == null) return;

        if (_disabled) return;

        bool hasInput = Input.touchCount > 0 || Input.GetMouseButton(0);
        if (!hasInput) return;

        Vector2 pos = Input.touchCount > 0
            ? (Vector2)Input.GetTouch(0).position
            : (Vector2)Input.mousePosition;

        _frameCounter++;
        if (_frameCounter % SpawnInterval != 0) return;

        SpawnParticle(pos);
    }

    // ── Setup helpers ──────────────────────────────────────────────
    void CreateCircleSprite()
    {
        // Tiny 8x8 soft circle generated in code — no external asset needed.
        const int size = 8;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        var center = new Vector2(size / 2f - 0.5f, size / 2f - 0.5f);
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(1f - dist / radius);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();

        _circleSprite = Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f
        );
    }

    void CreateCanvas()
    {
        var canvasGo = new GameObject("TrailCanvas");
        canvasGo.transform.SetParent(transform);

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = SortOrder;

        // Make sure trail never blocks any raycasts / touches
        _canvasGroup = canvasGo.AddComponent<CanvasGroup>();
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;

        _canvasRect = canvasGo.GetComponent<RectTransform>();
    }

    void CreatePool()
    {
        _pool = new Image[PoolSize];
        _poolRects = new RectTransform[PoolSize];
        _inUse = new bool[PoolSize];

        for (int i = 0; i < PoolSize; i++)
        {
            var go = new GameObject($"P{i}");
            go.transform.SetParent(_canvas.transform, false);

            var img = go.AddComponent<Image>();
            img.sprite = _circleSprite;
            img.raycastTarget = false;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            go.SetActive(false);

            _pool[i] = img;
            _poolRects[i] = rt;
            _inUse[i] = false;
        }
    }

    // ── Spawning ───────────────────────────────────────────────────
    void SpawnParticle(Vector3 screenPos)
    {
        int idx = GetFreeIndex();
        if (idx < 0) return; // pool exhausted, skip frame

        _inUse[idx] = true;

        var img = _pool[idx];
        var rt = _poolRects[idx];

        // Random size & color
        float size = Random.Range(MinSize, MaxSize);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = new Vector2(screenPos.x, screenPos.y);
        rt.localScale = Vector3.one;

        Color c = Palette[Random.Range(0, Palette.Length)];
        c.a = 1f;
        img.color = c;

        img.gameObject.SetActive(true);

        StartCoroutine(FadeParticle(idx, c));
    }

    int GetFreeIndex()
    {
        for (int i = 0; i < PoolSize; i++)
        {
            if (!_inUse[i]) return i;
        }
        return -1;
    }

    // ── Fade + drift coroutine ─────────────────────────────────────
    IEnumerator FadeParticle(int idx, Color baseColor)
    {
        var img = _pool[idx];
        var rt = _poolRects[idx];

        // Random drift direction
        Vector2 drift = Random.insideUnitCircle.normalized * DriftStrength;

        float elapsed = 0f;
        Vector2 startPos = rt.anchoredPosition;

        while (elapsed < ParticleDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / ParticleDuration;

            // Ease-out quad for a pleasant fade
            float easedT = t * (2f - t);

            // Alpha: 1 → 0
            baseColor.a = 1f - easedT;
            img.color = baseColor;

            // Scale: 1 → EndScale
            float s = Mathf.Lerp(1f, EndScale, easedT);
            rt.localScale = new Vector3(s, s, 1f);

            // Drift
            rt.anchoredPosition = startPos + drift * easedT;

            yield return null;
        }

        // Return to pool
        img.gameObject.SetActive(false);
        _inUse[idx] = false;
    }
}
