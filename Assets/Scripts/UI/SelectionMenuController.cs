using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives the reusable selection-menu scene.
/// Reads the current GameContext.CurrentGame and displays its sub-items.
/// Handles special card types: selfie (camera), gallery (image pick), free (blank page).
/// </summary>
public class SelectionMenuController : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI titleText;
    public Transform cardContainer;
    public GameCardView cardPrefab;

    private void Start()
    {
        var game = GameContext.CurrentGame;
        if (game == null)
        {
            Debug.LogError("SelectionMenuController: No game in GameContext!");
            return;
        }

        if (titleText != null)
            titleText.text = game.selectionScreenTitle;

        PopulateGrid(game);
    }

    private void PopulateGrid(GameItemData game)
    {
        foreach (var item in game.subItems)
        {
            var card = Instantiate(cardPrefab, cardContainer);
            var capturedItem = item;
            var capturedGame = game;

            switch (item.categoryKey)
            {
                case "selfie":
                    card.Setup(item.title, item.thumbnail, item.cardColor,
                        () => OpenSelfie(capturedGame));
                    StyleAsSelfieCard(card);
                    break;

                case "gallery":
                    card.Setup(item.title, null, item.cardColor,
                        () => OpenGallery(capturedGame));
                    StyleAsOutlinedCard(card, item.cardColor, "+");
                    break;

                case "free":
                    card.Setup(item.title, null, item.cardColor,
                        () => NavigationManager.GoToGame(capturedGame, capturedItem));
                    StyleAsBlankCard(card, item.cardColor);
                    break;

                default:
                    card.Setup(item.title, item.thumbnail, item.cardColor,
                        () => NavigationManager.GoToGame(capturedGame, capturedItem));
                    break;
            }
        }
    }

    // ── Card Styling ──

    private void StyleAsBlankCard(GameCardView card, Color accentColor)
    {
        // Totally white filled card — represents empty page
        if (card.backgroundImage != null)
            card.backgroundImage.color = Color.white;

        if (card.thumbnailImage != null)
        {
            // Fill thumbnail area with solid white
            thumbnailImage_SetSolidWhite(card.thumbnailImage);
        }

        if (card.placeholderIcon != null)
            card.placeholderIcon.gameObject.SetActive(false);

        if (card.titleText != null)
        {
            card.titleText.gameObject.SetActive(true);
            card.titleText.isRightToLeftText = true;
            card.titleText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
        }
    }

    private static void thumbnailImage_SetSolidWhite(Image img)
    {
        img.sprite = null;
        img.color = Color.white;
        img.gameObject.SetActive(true);
    }

    private void StyleAsSelfieCard(GameCardView card)
    {
        // Normal border, camera icon shown as thumbnail (set via data).
        // Just hide title — the icon speaks for itself.
        if (card.thumbnailImage != null)
            card.thumbnailImage.preserveAspect = true;

        if (card.titleText != null)
            card.titleText.gameObject.SetActive(false);
    }

    private void StyleAsOutlinedCard(GameCardView card, Color accentColor, string iconText)
    {
        if (card.backgroundImage != null)
        {
            card.backgroundImage.color = accentColor;
            card.backgroundImage.fillCenter = false;
            card.backgroundImage.pixelsPerUnitMultiplier = 0.4f;
        }

        if (card.thumbnailImage != null)
            card.thumbnailImage.gameObject.SetActive(false);

        var iconGO = new GameObject("CardIcon");
        iconGO.transform.SetParent(card.transform, false);
        var iconRT = iconGO.AddComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.15f, 0.2f);
        iconRT.anchorMax = new Vector2(0.85f, 0.75f);
        iconRT.offsetMin = Vector2.zero;
        iconRT.offsetMax = Vector2.zero;

        var iconTMP = iconGO.AddComponent<TextMeshProUGUI>();
        iconTMP.text = iconText;
        iconTMP.fontSize = 100;
        iconTMP.fontStyle = FontStyles.Bold;
        iconTMP.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.7f);
        iconTMP.alignment = TextAlignmentOptions.Center;
        iconTMP.raycastTarget = false;

        if (card.placeholderIcon != null)
            card.placeholderIcon.gameObject.SetActive(false);

        if (card.titleText != null)
        {
            card.titleText.gameObject.SetActive(true);
            card.titleText.isRightToLeftText = true;
            card.titleText.color = new Color(accentColor.r * 0.7f, accentColor.g * 0.7f, accentColor.b * 0.7f, 1f);
        }
    }

    // ── Camera (Selfie) ──

    private void OpenSelfie(GameItemData game)
    {
        string targetScene = game.targetSceneName;

#if UNITY_ANDROID || UNITY_IOS
        NativeCamera.TakePicture((path) =>
        {
            if (string.IsNullOrEmpty(path)) return;
            var tex = NativeCamera.LoadImageAtPath(path, 1024, false);
            if (tex == null) return;
            LaunchWithTexture(game, tex, targetScene);
        }, 1024, preferredCamera: NativeCamera.PreferredCamera.Front);
#else
        try
        {
            NativeCamera.TakePicture((path) =>
            {
                if (string.IsNullOrEmpty(path)) return;
                var tex = NativeCamera.LoadImageAtPath(path, 1024, false);
                if (tex == null) return;
                LaunchWithTexture(game, tex, targetScene);
            }, 1024, preferredCamera: NativeCamera.PreferredCamera.Front);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Camera capture failed: {e.Message}");
        }
#endif
    }

    // ── Gallery (Image Pick) ──

    private void OpenGallery(GameItemData game)
    {
        string targetScene = game.targetSceneName;

#if UNITY_ANDROID || UNITY_IOS
        NativeGallery.GetImageFromGallery((path) =>
        {
            if (string.IsNullOrEmpty(path)) return;
            var tex = NativeGallery.LoadImageAtPath(path, 1024, false);
            if (tex == null) return;
            LaunchWithTexture(game, tex, targetScene);
        });
#else
        try
        {
            NativeGallery.GetImageFromGallery((path) =>
            {
                if (string.IsNullOrEmpty(path)) return;
                var tex = NativeGallery.LoadImageAtPath(path, 1024, false);
                if (tex == null) return;
                LaunchWithTexture(game, tex, targetScene);
            });
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Gallery pick failed: {e.Message}");
        }
#endif
    }

    private void LaunchWithTexture(GameItemData game, Texture2D tex, string sceneName)
    {
        GameContext.CurrentGame = game;
        GameContext.CurrentSelection = null;
        GameContext.CustomTexture = tex;
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }

    public void OnBackPressed()
    {
        NavigationManager.GoToMainMenu();
    }
}
