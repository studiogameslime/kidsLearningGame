using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls a single fish in the Aquarium scene.
/// Handles free swimming, direction changes, feeding response, and tap reactions.
/// </summary>
public class AquariumFish : MonoBehaviour
{
    public string fishId;
    public RectTransform swimArea;

    private RectTransform rt;
    private Image img;
    private Vector2 targetPos;
    private float baseSpeed;
    private float bobPhase;
    private float bobAmp;
    private bool facingRight = true;

    // Feeding
    private bool isChasing;
    private AquariumFood targetFood;
    private Vector2 foodOffset;
    private float feedCooldown;
    private const float FeedCooldownDuration = 4f;

    public bool IsOnCooldown => feedCooldown > 0f;
    public bool IsChasing => isChasing;
    public AquariumFood TargetFood => targetFood;
    public System.Action onFinishedEating;

    // Finger attraction — fish swim toward finger when nearby
    public static bool FingerActive;
    public static Vector2 FingerPos;
    private const float AttractionRadius = 250f;
    private const float AttractionSpeed = 1.3f;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        img = GetComponent<Image>();
    }

    public void Initialize(RectTransform area)
    {
        swimArea = area;
        baseSpeed = Random.Range(40f, 80f);
        bobPhase = Random.Range(0f, Mathf.PI * 2f);
        bobAmp = Random.Range(3f, 8f);
        PickNewTarget();
    }

    private void Update()
    {
        if (swimArea == null) return;

        if (feedCooldown > 0f)
            feedCooldown -= Time.deltaTime;

        // If chasing food that was already eaten/destroyed/expired, look for other food
        if (isChasing && (targetFood == null || !targetFood.IsValid))
        {
            isChasing = false;
            targetFood = null;
            onFinishedEating?.Invoke();
            PickNewTarget();
        }

        // Priority: food > finger > random swim target
        Vector2 target;
        bool attractedToFinger = false;
        if (isChasing && targetFood != null)
        {
            target = targetFood.GetComponent<RectTransform>().anchoredPosition + foodOffset;
        }
        else if (FingerActive && Vector2.Distance(rt.anchoredPosition, FingerPos) < AttractionRadius)
        {
            target = FingerPos;
            attractedToFinger = true;
        }
        else
        {
            target = targetPos;
        }

        Vector2 pos = rt.anchoredPosition;
        Vector2 dir = target - pos;
        float dist = dir.magnitude;

        if (dist < 8f)
        {
            if (isChasing)
            {
                // Arrived at food — eat it
                targetFood.OnEaten(this);
                isChasing = false;
                targetFood = null;
                feedCooldown = FeedCooldownDuration;
                onFinishedEating?.Invoke();
            }
            PickNewTarget();
            return;
        }

        float speed = isChasing ? baseSpeed * 1.5f : attractedToFinger ? baseSpeed * AttractionSpeed : baseSpeed;
        float step = speed * Time.deltaTime;
        Vector2 move = dir.normalized * Mathf.Min(step, dist);

        float bob = 0f;
        if (!isChasing)
        {
            bobPhase += Time.deltaTime * 2f;
            bob = Mathf.Sin(bobPhase) * bobAmp * Time.deltaTime;
        }

        pos += move + new Vector2(0, bob);

        Rect bounds = swimArea.rect;
        float halfW = rt.sizeDelta.x * 0.5f;
        float halfH = rt.sizeDelta.y * 0.5f;
        pos.x = Mathf.Clamp(pos.x, bounds.xMin + halfW, bounds.xMax - halfW);
        pos.y = Mathf.Clamp(pos.y, bounds.yMin + halfH, bounds.yMax - halfH);

        rt.anchoredPosition = pos;

        if (dir.x > 2f && !facingRight)
        {
            facingRight = true;
            rt.localScale = new Vector3(1, 1, 1);
        }
        else if (dir.x < -2f && facingRight)
        {
            facingRight = false;
            rt.localScale = new Vector3(-1, 1, 1);
        }
    }

    private void PickNewTarget()
    {
        if (swimArea == null) return;
        Rect bounds = swimArea.rect;
        float margin = 40f;
        float x = Random.Range(bounds.xMin + margin, bounds.xMax - margin);
        float y = Random.Range(bounds.yMin + margin, bounds.yMax - margin);
        targetPos = new Vector2(x, y);
    }

    /// <summary>
    /// Try to send this fish chasing a specific food item.
    /// Returns true if the fish accepted the chase.
    /// </summary>
    public bool TryReactToFood(AquariumFood food, float proximityRadius)
    {
        if (isChasing || feedCooldown > 0f || food == null || !food.IsValid) return false;

        var foodRT = food.GetComponent<RectTransform>();
        float dist = Vector2.Distance(rt.anchoredPosition, foodRT.anchoredPosition);
        if (dist > proximityRadius) return false;

        isChasing = true;
        targetFood = food;
        foodOffset = Random.insideUnitCircle * 20f;
        return true;
    }

    // ── Tap Reaction ──

    private bool isTapAnimating;

    /// <summary>Playful flip + pop when tapped.</summary>
    public void OnTap()
    {
        if (isTapAnimating) return;
        StartCoroutine(TapReaction());
    }

    private IEnumerator TapReaction()
    {
        isTapAnimating = true;
        float scaleX = facingRight ? 1f : -1f;

        // Quick scale pop
        float t = 0f;
        while (t < 0.12f)
        {
            t += Time.deltaTime;
            float p = t / 0.12f;
            float s = 1f + 0.2f * Mathf.Sin(p * Mathf.PI);
            rt.localScale = new Vector3(scaleX * s, s, 1f);
            yield return null;
        }

        // Flip direction
        facingRight = !facingRight;
        scaleX = facingRight ? 1f : -1f;

        // Quick rotate wiggle
        t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            float p = t / 0.25f;
            float angle = Mathf.Sin(p * Mathf.PI * 3f) * 15f * (1f - p);
            rt.localRotation = Quaternion.Euler(0, 0, angle);
            rt.localScale = new Vector3(scaleX, 1f, 1f);
            yield return null;
        }

        rt.localRotation = Quaternion.identity;
        rt.localScale = new Vector3(scaleX, 1f, 1f);

        // Pick new target in the direction the fish is now facing
        PickTargetInFacingDirection();
        isTapAnimating = false;
    }

    private void PickTargetInFacingDirection()
    {
        if (swimArea == null) return;
        Rect bounds = swimArea.rect;
        float margin = 40f;
        float curX = rt.anchoredPosition.x;

        float x;
        if (facingRight)
            x = Random.Range(Mathf.Max(curX + 50f, bounds.xMin + margin), bounds.xMax - margin);
        else
            x = Random.Range(bounds.xMin + margin, Mathf.Min(curX - 50f, bounds.xMax - margin));

        float y = Random.Range(bounds.yMin + margin, bounds.yMax - margin);
        targetPos = new Vector2(x, y);
    }

    /// <summary>Briefly swim toward a point out of curiosity.</summary>
    public void Nudge(Vector2 point, float radius)
    {
        if (isChasing || isTapAnimating || feedCooldown > 0f) return;
        float dist = Vector2.Distance(rt.anchoredPosition, point);
        if (dist > radius) return;

        // Set target partway toward the tap
        Vector2 dir = (point - rt.anchoredPosition).normalized;
        targetPos = rt.anchoredPosition + dir * Mathf.Min(dist * 0.4f, 60f);
    }
}
