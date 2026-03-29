using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Home screen controller. Play starts the journey, World opens the world scene,
/// profile avatar navigates to profile selection, All Games opens the legacy main menu.
/// </summary>
public class HomeController : MonoBehaviour
{
    [Header("Data")]
    public GameDatabase gameDatabase;

    [Header("UI References")]
    public Button playButton;
    public Image playButtonImage;
    public Button worldButton;
    public Button profileButton;
    public Button allGamesButton;
    public Button parentAreaButton;
    public Image profileAvatar;
    public TextMeshProUGUI profileInitial;
    public Image[] arrowImages;

    private void Start()
    {
        if (playButton != null) playButton.onClick.AddListener(OnPlayPressed);
        if (worldButton != null) worldButton.onClick.AddListener(OnWorldPressed);
        if (profileButton != null) profileButton.onClick.AddListener(OnProfilePressed);
        if (allGamesButton != null) allGamesButton.onClick.AddListener(OnAllGamesPressed);
        if (parentAreaButton != null) parentAreaButton.onClick.AddListener(OnParentAreaPressed);

        // Game database available for visibility checks

        UpdateProfileAvatar();
        StartCoroutine(PulsePlayButton());
        StartCoroutine(BobArrows());
    }

    private void UpdateProfileAvatar()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        if (profileAvatar != null)
            profileAvatar.color = profile.AvatarColor;
        if (profileInitial != null)
            profileInitial.text = profile.Initial;

        // Color play button with profile's chosen color
        if (playButtonImage != null)
            playButtonImage.color = profile.AvatarColor;

        // Color arrows to match
        if (arrowImages != null)
        {
            Color arrowColor = new Color(
                profile.AvatarColor.r, profile.AvatarColor.g, profile.AvatarColor.b, 0.6f);
            foreach (var arrow in arrowImages)
                if (arrow != null) arrow.color = arrowColor;
        }
    }

    private IEnumerator PulsePlayButton()
    {
        if (playButton == null) yield break;
        var rt = playButton.GetComponent<RectTransform>();
        if (rt == null) yield break;

        while (true)
        {
            yield return ScaleTo(rt, Vector3.one * 1.08f, 0.8f);
            yield return ScaleTo(rt, Vector3.one, 0.8f);
        }
    }

    private IEnumerator BobArrows()
    {
        if (arrowImages == null || arrowImages.Length == 0) yield break;

        var rts = new RectTransform[arrowImages.Length];
        var basePos = new Vector2[arrowImages.Length];
        for (int i = 0; i < arrowImages.Length; i++)
        {
            if (arrowImages[i] == null) continue;
            rts[i] = arrowImages[i].GetComponent<RectTransform>();
            if (rts[i] != null) basePos[i] = rts[i].anchoredPosition;
        }

        float t = 0f;
        while (true)
        {
            t += Time.deltaTime;
            for (int i = 0; i < rts.Length; i++)
            {
                if (rts[i] == null) continue;
                // Bob along arrow direction (use local up)
                float offset = Mathf.Sin(t * 3f + i * 1.5f) * 8f;
                Vector2 dir = rts[i].up.normalized;
                rts[i].anchoredPosition = basePos[i] + new Vector2(dir.x, dir.y) * offset;
            }
            yield return null;
        }
    }

    private IEnumerator ScaleTo(RectTransform rt, Vector3 target, float duration)
    {
        Vector3 start = rt.localScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            rt.localScale = Vector3.Lerp(start, target, t);
            yield return null;
        }
        rt.localScale = target;
    }

    public void OnPlayPressed()
    {
        NavigationManager.GoToGamesCollection();
    }

    public void OnWorldPressed()
    {
        StartCoroutine(LoadWorldAsync());
    }

    private IEnumerator LoadWorldAsync()
    {
        // Disable button to prevent double-tap
        if (worldButton != null) worldButton.interactable = false;

        BubbleTransition.LoadScene("WorldScene");
        yield break;
    }

    public void OnProfilePressed()
    {
        NavigationManager.GoToProfileSelection();
    }

    public void OnAllGamesPressed()
    {
        NavigationManager.GoToMainMenu();
    }

    public void OnParentAreaPressed()
    {
        NavigationManager.GoToParentDashboard();
    }
}
