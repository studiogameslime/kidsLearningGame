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
    public Button worldButton;
    public Button profileButton;
    public Button allGamesButton;
    public Image profileAvatar;
    public TextMeshProUGUI profileInitial;

    private void Start()
    {
        if (playButton != null) playButton.onClick.AddListener(OnPlayPressed);
        if (worldButton != null) worldButton.onClick.AddListener(OnWorldPressed);
        if (profileButton != null) profileButton.onClick.AddListener(OnProfilePressed);
        if (allGamesButton != null) allGamesButton.onClick.AddListener(OnAllGamesPressed);

        // Wire game database to JourneyManager
        if (gameDatabase != null && JourneyManager.Instance != null)
            JourneyManager.Instance.SetGameDatabase(gameDatabase);

        UpdateProfileAvatar();
        StartCoroutine(PulsePlayButton());
    }

    private void UpdateProfileAvatar()
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) return;

        if (profileAvatar != null)
            profileAvatar.color = profile.AvatarColor;
        if (profileInitial != null)
            profileInitial.text = profile.Initial;
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
        JourneyManager.Instance?.StartJourney();
    }

    public void OnWorldPressed()
    {
        StartCoroutine(LoadWorldAsync());
    }

    private IEnumerator LoadWorldAsync()
    {
        // Disable button to prevent double-tap
        if (worldButton != null) worldButton.interactable = false;

        var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("WorldScene");
        while (!op.isDone)
            yield return null;
    }

    public void OnProfilePressed()
    {
        NavigationManager.GoToProfileSelection();
    }

    public void OnAllGamesPressed()
    {
        NavigationManager.GoToMainMenu();
    }
}
