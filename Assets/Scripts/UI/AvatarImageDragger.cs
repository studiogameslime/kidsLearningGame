using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Drag + pinch-zoom for avatar crop. Image keeps original aspect ratio.
/// Constrains position so the crop circle is always fully covered.
/// </summary>
public class AvatarImageDragger : MonoBehaviour, IDragHandler
{
    public Canvas canvas;
    public float cropRadius = 150f;

    private RectTransform _rt;
    private float _baseScale;
    private float _currentZoom = 1f;
    private float _minZoom = 1f;
    private float _maxZoom = 4f;
    private int _texW, _texH;
    private float _prevPinchDist;

    public void Init(int texWidth, int texHeight, float baseScale, float cropRadius)
    {
        _rt = GetComponent<RectTransform>();
        _texW = texWidth;
        _texH = texHeight;
        _baseScale = baseScale;
        this.cropRadius = cropRadius;
        _currentZoom = 1f;
        _minZoom = 1f; // baseScale already covers circle
        _maxZoom = 4f;
        ApplyScale();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_rt == null || canvas == null) return;
        _rt.anchoredPosition += eventData.delta / canvas.scaleFactor;
        Constrain();
    }

    private void Update()
    {
        if (_rt == null) return;

        // Pinch zoom
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

            if (_prevPinchDist > 0f && dist > 0f)
            {
                float factor = dist / _prevPinchDist;
                SetZoom(_currentZoom * factor);
            }
            _prevPinchDist = dist;
        }
        else
        {
            _prevPinchDist = 0f;
        }

#if UNITY_EDITOR
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
            SetZoom(_currentZoom * (1f + scroll * 2f));
#endif
    }

    private void SetZoom(float zoom)
    {
        _currentZoom = Mathf.Clamp(zoom, _minZoom, _maxZoom);
        ApplyScale();
        Constrain();
    }

    private void ApplyScale()
    {
        if (_rt == null) return;
        float s = _baseScale * _currentZoom;
        _rt.sizeDelta = new Vector2(_texW * s, _texH * s);
    }

    /// <summary>Ensure image always covers the crop circle (no empty space).</summary>
    private void Constrain()
    {
        if (_rt == null) return;
        Vector2 half = _rt.sizeDelta * 0.5f;
        Vector2 pos = _rt.anchoredPosition;

        // Image center can't be further than (half - cropRadius) from origin
        float maxOffsetX = half.x - cropRadius;
        float maxOffsetY = half.y - cropRadius;
        maxOffsetX = Mathf.Max(maxOffsetX, 0);
        maxOffsetY = Mathf.Max(maxOffsetY, 0);

        pos.x = Mathf.Clamp(pos.x, -maxOffsetX, maxOffsetX);
        pos.y = Mathf.Clamp(pos.y, -maxOffsetY, maxOffsetY);
        _rt.anchoredPosition = pos;
    }

    public float CurrentScale => _baseScale * _currentZoom;
}
