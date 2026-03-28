using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Custom input handler for the World scene. Routes touch/mouse to:
/// - WorldToyBox tap (start journey)
/// - WorldAnimal tap/drag
/// - WorldBalloon tap
/// - WorldCloud tap
/// - WorldProp tap
/// - Sun/Moon tap (day/night toggle)
/// - World pan (empty area drag)
/// No ScrollRect used to avoid drag conflicts.
/// </summary>
public class WorldInputHandler : MonoBehaviour
{
    [Header("References")]
    public RectTransform worldContent;
    public RectTransform viewport;
    public WorldEnvironment environment;

    [Header("Sun/Moon")]
    public RectTransform sunRT;
    public RectTransform moonRT;

    [Header("Settings")]
    public float dragThreshold = 20f;

    [Header("Inactivity Hint")]
    public float inactivityDelay = 5f;
    public Sprite[] hintHandFrames;

    private bool isDragging;
    private bool isWorldPan;
    private Vector2 touchStartPos;
    private Vector2 contentStartPos;
    private WorldAnimal draggedAnimal;

    // Inactivity tutorial hand
    private float _lastInputTime;
    private TutorialHand _hintHand;
    private bool _hintShown;

    private void Start()
    {
        _lastInputTime = Time.time;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            _lastInputTime = Time.time;
            DismissHint();
            OnTouchStart(Input.mousePosition);
        }
        else if (Input.GetMouseButton(0))
        {
            OnTouchMove(Input.mousePosition);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            OnTouchEnd(Input.mousePosition);
        }

        // Show hint hand on game shelf after inactivity
        if (!_hintShown && Time.time - _lastInputTime >= inactivityDelay)
        {
            ShowShelfHint();
        }
    }

    private void ShowShelfHint()
    {
        _hintShown = true;

        var toyBox = FindObjectOfType<WorldToyBox>();
        if (toyBox == null) return;

        var toyBoxRT = toyBox.GetComponent<RectTransform>();
        if (toyBoxRT == null) return;

        // Create hand as child of toy box so it follows its position
        var go = new GameObject("TutorialHand");
        go.transform.SetParent(toyBoxRT, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(30, -40);
        rt.sizeDelta = new Vector2(450, 450);

        var img = go.AddComponent<UnityEngine.UI.Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;

        var cg = go.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        var hand = go.AddComponent<TutorialHand>();
        hand.tutorialKey = ""; // always show on inactivity, no per-profile memory
        hand.fps = 20f;

        if (hintHandFrames == null || hintHandFrames.Length == 0)
        {
            Destroy(go);
            return;
        }

        img.sprite = hintHandFrames[0];
        hand.frames = hintHandFrames;

        _hintHand = hand;
    }

    private void DismissHint()
    {
        if (_hintHand != null)
        {
            _hintHand.Dismiss();
            _hintHand = null;
        }
        _hintShown = false;
    }

    private void OnTouchStart(Vector2 screenPos)
    {
        touchStartPos = screenPos;
        contentStartPos = worldContent.anchoredPosition;
        isDragging = false;
        isWorldPan = false;
        draggedAnimal = null;

        // Raycast to check what we hit
        var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        var pointerData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
        {
            position = screenPos
        };
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointerData, results);

        // Check if a blocking overlay (tutorial/spotlight/album/gallery) is active.
        // If so, only allow gift box taps (for spotlight cutout) and block everything else.
        bool overlayBlocking = false;
        foreach (var r in results)
        {
            string n = r.gameObject.name;
            if (n == "SpotlightOverlay" || n == "AlbumOverlay" || n == "Dim" || n == "DimBackground")
            {
                overlayBlocking = true;
                break;
            }
        }

        if (overlayBlocking)
        {
            // Only allow gift box tap through the spotlight overlay
            foreach (var r in results)
            {
                var gift = r.gameObject.GetComponent<GiftBoxController>();
                if (gift != null)
                {
                    gift.OnTap();
                    return;
                }
            }
            return; // block everything else
        }

        foreach (var result in results)
        {
            var go = result.gameObject;

            // Check for gift box
            var gift2 = go.GetComponent<GiftBoxController>();
            if (gift2 != null)
            {
                gift2.OnTap();
                return;
            }

            // Check for animal
            var animal = go.GetComponent<WorldAnimal>();
            if (animal != null)
            {
                draggedAnimal = animal;
                animal.OnTouchStart(screenPos);
                return;
            }

            // Check for balloon
            var balloon = go.GetComponent<WorldBalloon>();
            if (balloon != null)
            {
                balloon.Pop();
                return;
            }

            // Check for cloud
            var cloud = go.GetComponent<WorldCloud>();
            if (cloud != null)
            {
                cloud.OnTap();
                return;
            }

            // Check for sticker tree
            var stickerTree = go.GetComponent<StickerTreeController>();
            if (stickerTree != null)
            {
                stickerTree.OnTap();
                return;
            }

            // Check for game shelf (game selection entry point)
            var shelf = go.GetComponent<WorldGameShelf>();
            if (shelf != null)
            {
                shelf.OnTap();
                return;
            }

            // Check for toy box (main play button)
            var toyBox = go.GetComponent<WorldToyBox>();
            if (toyBox != null)
            {
                toyBox.OnTap();
                return;
            }

            // Check for easel
            var easel = go.GetComponent<WorldEasel>();
            if (easel != null)
            {
                easel.OnTap();
                return;
            }

            // Check for prop (tree/bush)
            var prop = go.GetComponent<WorldProp>();
            if (prop != null)
            {
                prop.OnTap();
                return;
            }

            // Check for sun
            if (sunRT != null && go.transform == sunRT.transform)
            {
                if (environment != null) environment.OnSunTapped();
                return;
            }

            // Check for moon
            if (moonRT != null && go.transform == moonRT.transform)
            {
                if (environment != null) environment.OnMoonTapped();
                return;
            }
        }

        // No interactive object hit — will be a world pan
        isWorldPan = true;
    }

    private void OnTouchMove(Vector2 screenPos)
    {
        Vector2 delta = screenPos - touchStartPos;

        if (!isDragging && delta.magnitude > dragThreshold)
            isDragging = true;

        if (!isDragging) return;

        if (draggedAnimal != null)
        {
            draggedAnimal.OnDrag(screenPos);
        }
        else if (isWorldPan)
        {
            PanWorld(delta);
        }
    }

    private void OnTouchEnd(Vector2 screenPos)
    {
        if (draggedAnimal != null)
        {
            if (!isDragging)
                draggedAnimal.OnTap();
            else
                draggedAnimal.OnDragEnd();
            draggedAnimal = null;
        }

        isDragging = false;
        isWorldPan = false;
    }

    private void PanWorld(Vector2 totalDelta)
    {
        float newX = contentStartPos.x + totalDelta.x;

        float contentWidth = worldContent.rect.width;
        float viewportWidth = viewport != null ? viewport.rect.width : 1080f;

        if (contentWidth <= viewportWidth)
        {
            newX = 0f;
        }
        else
        {
            // Cylindrical wrap: when scrolled past edge, wrap around
            float range = contentWidth - viewportWidth;
            // Wrap newX into [-range, 0] range
            newX = newX % range;
            if (newX > 0) newX -= range;
            if (newX < -range) newX += range;
        }

        worldContent.anchoredPosition = new Vector2(newX, worldContent.anchoredPosition.y);
    }
}
