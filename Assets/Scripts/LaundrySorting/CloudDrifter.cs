using UnityEngine;

/// <summary>
/// Simple cloud drift animation — slowly moves the cloud horizontally with gentle bobbing.
/// </summary>
public class CloudDrifter : MonoBehaviour
{
    private RectTransform rt;
    private Vector2 startPos;
    private float speed;
    private float amplitude;
    private float phase;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    private void Start()
    {
        startPos = rt.anchoredPosition;
        speed = Random.Range(5f, 15f);
        amplitude = Random.Range(30f, 60f);
        phase = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        float x = Mathf.Sin(Time.time * speed * 0.01f + phase) * amplitude;
        float y = Mathf.Sin(Time.time * 0.3f + phase) * 5f;
        rt.anchoredPosition = startPos + new Vector2(x, y);
    }
}
