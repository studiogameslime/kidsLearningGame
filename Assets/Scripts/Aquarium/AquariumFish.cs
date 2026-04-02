using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls a single fish in the Aquarium scene.
/// Handles free swimming, direction changes, and feeding response.
/// When chasing food, arrives at the food item and notifies the controller.
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

        Vector2 target = isChasing
            ? targetFood.GetComponent<RectTransform>().anchoredPosition + foodOffset
            : targetPos;

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

        float speed = isChasing ? baseSpeed * 1.5f : baseSpeed;
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
}
