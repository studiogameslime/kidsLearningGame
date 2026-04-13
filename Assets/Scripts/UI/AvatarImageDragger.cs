using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Drag + pinch-zoom handler for avatar crop screen.
/// Constrains zoom so the crop circle is always filled.
/// </summary>
public class AvatarImageDragger : MonoBehaviour, IDragHandler, IPointerDownHandler
{
    public Canvas canvas;
    public float minScale = 0.5f;
    public float maxScale = 3f;
    public float currentScale = 1f;

    private RectTransform _rt;
    private Vector2 _initialSize;

    // Pinch zoom
    private int _touchCount;
    private float _prevPinchDist;

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
    }

    private void Start()
    {
        _initialSize = _rt.sizeDelta / currentScale;
    }

    public void OnPointerDown(PointerEventData eventData) { }

    public void OnDrag(PointerEventData eventData)
    {
        if (_rt == null || canvas == null) return;
        _rt.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    private void Update()
    {
        // Pinch zoom (multi-touch)
        if (Input.touchCount == 2)
        {
            var t0 = Input.GetTouch(0);
            var t1 = Input.GetTouch(1);
            float dist = Vector2.Distance(t0.position, t1.position);

            if (t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began)
            {
                _prevPinchDist = dist;
                return;
            }

            if (_prevPinchDist > 0f)
            {
                float delta = dist / _prevPinchDist;
                float newScale = Mathf.Clamp(currentScale * delta, minScale, maxScale);
                currentScale = newScale;
                _rt.sizeDelta = _initialSize * currentScale;
            }
            _prevPinchDist = dist;
        }
        else
        {
            _prevPinchDist = 0f;
        }

        // Editor: scroll wheel zoom
#if UNITY_EDITOR
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            float newScale = Mathf.Clamp(currentScale * (1f + scroll * 2f), minScale, maxScale);
            currentScale = newScale;
            _rt.sizeDelta = _initialSize * currentScale;
        }
#endif
    }
}
