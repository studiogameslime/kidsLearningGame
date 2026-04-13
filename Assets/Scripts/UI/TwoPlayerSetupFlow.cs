using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 2-player setup flow: Choose Partner → Identity Screen → Start Game.
/// Shows when tapping a 2-player game with 2+ profiles.
/// </summary>
public static class TwoPlayerSetupFlow
{
    /// <summary>
    /// Start the 2-player setup flow. Called instead of directly loading the game.
    /// </summary>
    public static void Start(MonoBehaviour host, GameItemData game, System.Action<GameItemData> onStartGame)
    {
        var profiles = ProfileManager.Instance.Profiles;
        var current = ProfileManager.ActiveProfile;

        // Only 1 profile — go straight to single player
        if (profiles.Count < 2 || current == null)
        {
            onStartGame?.Invoke(game);
            return;
        }

        // Get other profiles
        var others = new List<UserProfile>();
        foreach (var p in profiles)
            if (p.id != current.id) others.Add(p);

        ShowChoosePartner(host, game, current, others, onStartGame);
    }

    // ── Choose Partner Screen ──

    private static void ShowChoosePartner(MonoBehaviour host, GameItemData game,
        UserProfile currentPlayer, List<UserProfile> otherPlayers, System.Action<GameItemData> onStartGame)
    {
        var canvas = host.GetComponentInParent<Canvas>();
        if (canvas == null) { onStartGame?.Invoke(game); return; }

        var circleSprite = Resources.Load<Sprite>("Circle");

        var modal = new GameObject("ChoosePartner");
        modal.transform.SetParent(canvas.transform, false);
        modal.transform.SetAsLastSibling();
        var modalRT = modal.AddComponent<RectTransform>();
        modalRT.anchorMin = Vector2.zero; modalRT.anchorMax = Vector2.one;
        modalRT.offsetMin = Vector2.zero; modalRT.offsetMax = Vector2.zero;
        modal.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
        modal.GetComponent<Image>().raycastTarget = true;

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(modal.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 0.78f); titleRT.anchorMax = new Vector2(1, 0.92f);
        titleRT.offsetMin = Vector2.zero; titleRT.offsetMax = Vector2.zero;
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(titleTMP, "\u05DE\u05D9 \u05DE\u05E6\u05D8\u05E8\u05E3?"); // מי מצטרף?
        titleTMP.fontSize = 42;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;

        // Current player indicator
        var hostGO = new GameObject("Host");
        hostGO.transform.SetParent(modal.transform, false);
        var hostRT = hostGO.AddComponent<RectTransform>();
        hostRT.anchorMin = new Vector2(0.3f, 0.62f); hostRT.anchorMax = new Vector2(0.7f, 0.78f);
        hostRT.offsetMin = Vector2.zero; hostRT.offsetMax = Vector2.zero;
        var hostTMP = hostGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(hostTMP, currentPlayer.displayName + " \u05DE\u05E9\u05D7\u05E7 \u05E2\u05DD..."); // X משחק עם...
        hostTMP.fontSize = 28;
        hostTMP.color = new Color(1, 1, 1, 0.7f);
        hostTMP.alignment = TextAlignmentOptions.Center;

        // Partner cards (horizontal layout)
        var cardsGO = new GameObject("Cards");
        cardsGO.transform.SetParent(modal.transform, false);
        var cardsRT = cardsGO.AddComponent<RectTransform>();
        cardsRT.anchorMin = new Vector2(0.1f, 0.2f); cardsRT.anchorMax = new Vector2(0.9f, 0.6f);
        cardsRT.offsetMin = Vector2.zero; cardsRT.offsetMax = Vector2.zero;
        var cardsLayout = cardsGO.AddComponent<HorizontalLayoutGroup>();
        cardsLayout.spacing = 30;
        cardsLayout.childAlignment = TextAnchor.MiddleCenter;
        cardsLayout.childForceExpandWidth = false;
        cardsLayout.childControlWidth = false;
        cardsLayout.childControlHeight = false;

        // Partner profile cards
        foreach (var partner in otherPlayers)
        {
            var captured = partner;
            var card = CreateProfileCard(cardsGO.transform, partner, circleSprite, () =>
            {
                Object.Destroy(modal);
                TwoPlayerManager.Start(currentPlayer, captured);
                ShowIdentityScreen(host, game, onStartGame);
            });
        }

        // "Play Alone" card
        CreatePlayAloneCard(cardsGO.transform, circleSprite, () =>
        {
            Object.Destroy(modal);
            TwoPlayerManager.End();
            onStartGame?.Invoke(game);
        });

        // Back button
        var backGO = new GameObject("Back");
        backGO.transform.SetParent(modal.transform, false);
        var backRT = backGO.AddComponent<RectTransform>();
        backRT.anchorMin = new Vector2(0, 0.9f); backRT.anchorMax = new Vector2(0.1f, 1);
        backRT.offsetMin = new Vector2(20, 0); backRT.offsetMax = Vector2.zero;
        var backTMP = backGO.AddComponent<TextMeshProUGUI>();
        backTMP.text = "\u25C0"; // ◀
        backTMP.fontSize = 36; backTMP.color = Color.white;
        backTMP.alignment = TextAlignmentOptions.Center;
        var backBtn = backGO.AddComponent<Button>();
        backBtn.transition = Selectable.Transition.None;
        backBtn.onClick.AddListener(() => Object.Destroy(modal));
    }

    private static GameObject CreateProfileCard(Transform parent, UserProfile profile, Sprite circleSprite, System.Action onTap)
    {
        var go = new GameObject($"Partner_{profile.displayName}");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200, 250);

        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12; layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        layout.childControlWidth = true; layout.childControlHeight = true;

        // Avatar circle
        var avatarGO = new GameObject("Avatar");
        avatarGO.transform.SetParent(go.transform, false);
        avatarGO.AddComponent<RectTransform>();
        avatarGO.AddComponent<LayoutElement>().preferredHeight = 140;
        var avatarContainer = new GameObject("Circle");
        avatarContainer.transform.SetParent(avatarGO.transform, false);
        var acRT = avatarContainer.AddComponent<RectTransform>();
        acRT.anchorMin = acRT.anchorMax = new Vector2(0.5f, 0.5f);
        acRT.sizeDelta = new Vector2(130, 130);
        var acImg = avatarContainer.AddComponent<Image>();
        if (circleSprite != null) acImg.sprite = circleSprite;
        acImg.color = profile.AvatarColor;
        acImg.raycastTarget = true;

        // Initial letter
        var initGO = new GameObject("Init");
        initGO.transform.SetParent(avatarContainer.transform, false);
        var initRT = initGO.AddComponent<RectTransform>();
        initRT.anchorMin = Vector2.zero; initRT.anchorMax = Vector2.one;
        initRT.offsetMin = Vector2.zero; initRT.offsetMax = Vector2.zero;
        var initTMP = initGO.AddComponent<TextMeshProUGUI>();
        initTMP.text = profile.displayName.Length > 0 ? profile.displayName.Substring(0, 1) : "?";
        initTMP.fontSize = 52; initTMP.fontStyle = FontStyles.Bold;
        initTMP.color = Color.white;
        initTMP.alignment = TextAlignmentOptions.Center;

        // Name
        var nameGO = new GameObject("Name");
        nameGO.transform.SetParent(go.transform, false);
        nameGO.AddComponent<RectTransform>();
        nameGO.AddComponent<LayoutElement>().preferredHeight = 40;
        var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(nameTMP, profile.displayName);
        nameTMP.fontSize = 28; nameTMP.color = Color.white;
        nameTMP.alignment = TextAlignmentOptions.Center;

        // Button
        var btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => onTap?.Invoke());

        return go;
    }

    private static GameObject CreatePlayAloneCard(Transform parent, Sprite circleSprite, System.Action onTap)
    {
        var go = new GameObject("PlayAlone");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200, 250);

        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12; layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        layout.childControlWidth = true; layout.childControlHeight = true;

        // Icon circle
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(go.transform, false);
        iconGO.AddComponent<RectTransform>();
        iconGO.AddComponent<LayoutElement>().preferredHeight = 140;
        var iconContainer = new GameObject("Circle");
        iconContainer.transform.SetParent(iconGO.transform, false);
        var icRT = iconContainer.AddComponent<RectTransform>();
        icRT.anchorMin = icRT.anchorMax = new Vector2(0.5f, 0.5f);
        icRT.sizeDelta = new Vector2(130, 130);
        var icImg = iconContainer.AddComponent<Image>();
        if (circleSprite != null) icImg.sprite = circleSprite;
        icImg.color = new Color(0.4f, 0.4f, 0.45f);

        var oneTMP = new GameObject("One");
        oneTMP.transform.SetParent(iconContainer.transform, false);
        var oneRT = oneTMP.AddComponent<RectTransform>();
        oneRT.anchorMin = Vector2.zero; oneRT.anchorMax = Vector2.one;
        oneRT.offsetMin = Vector2.zero; oneRT.offsetMax = Vector2.zero;
        var oneTxt = oneTMP.AddComponent<TextMeshProUGUI>();
        oneTxt.text = "1"; oneTxt.fontSize = 52; oneTxt.fontStyle = FontStyles.Bold;
        oneTxt.color = Color.white; oneTxt.alignment = TextAlignmentOptions.Center;

        // Label
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        labelGO.AddComponent<RectTransform>();
        labelGO.AddComponent<LayoutElement>().preferredHeight = 40;
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(labelTMP, "\u05DC\u05D1\u05D3"); // לבד
        labelTMP.fontSize = 28; labelTMP.color = new Color(1, 1, 1, 0.6f);
        labelTMP.alignment = TextAlignmentOptions.Center;

        var btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => onTap?.Invoke());

        return go;
    }

    // ── Identity Screen (pre-game) ──

    private static void ShowIdentityScreen(MonoBehaviour host, GameItemData game, System.Action<GameItemData> onStartGame)
    {
        var canvas = host.GetComponentInParent<Canvas>();
        if (canvas == null) { onStartGame?.Invoke(game); return; }

        var modal = new GameObject("IdentityScreen");
        modal.transform.SetParent(canvas.transform, false);
        modal.transform.SetAsLastSibling();
        var modalRT = modal.AddComponent<RectTransform>();
        modalRT.anchorMin = Vector2.zero; modalRT.anchorMax = Vector2.one;
        modalRT.offsetMin = Vector2.zero; modalRT.offsetMax = Vector2.zero;

        // Left half (BLUE = Player 1)
        var leftGO = new GameObject("Left");
        leftGO.transform.SetParent(modal.transform, false);
        var leftRT = leftGO.AddComponent<RectTransform>();
        leftRT.anchorMin = Vector2.zero; leftRT.anchorMax = new Vector2(0.5f, 1);
        leftRT.offsetMin = Vector2.zero; leftRT.offsetMax = Vector2.zero;
        leftGO.AddComponent<Image>().color = new Color(TwoPlayerManager.Player1Color.r, TwoPlayerManager.Player1Color.g, TwoPlayerManager.Player1Color.b, 0.85f);

        var name1GO = new GameObject("Name1");
        name1GO.transform.SetParent(leftGO.transform, false);
        var n1RT = name1GO.AddComponent<RectTransform>();
        n1RT.anchorMin = new Vector2(0.1f, 0.55f); n1RT.anchorMax = new Vector2(0.9f, 0.75f);
        n1RT.offsetMin = Vector2.zero; n1RT.offsetMax = Vector2.zero;
        var n1TMP = name1GO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(n1TMP, TwoPlayerManager.GetName(1));
        n1TMP.fontSize = 48; n1TMP.fontStyle = FontStyles.Bold;
        n1TMP.color = Color.white; n1TMP.alignment = TextAlignmentOptions.Center;

        var side1GO = new GameObject("Side1");
        side1GO.transform.SetParent(leftGO.transform, false);
        var s1RT = side1GO.AddComponent<RectTransform>();
        s1RT.anchorMin = new Vector2(0.1f, 0.35f); s1RT.anchorMax = new Vector2(0.9f, 0.5f);
        s1RT.offsetMin = Vector2.zero; s1RT.offsetMax = Vector2.zero;
        var s1TMP = side1GO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(s1TMP, "\u25C0 \u05E6\u05D3 \u05E9\u05DE\u05D0\u05DC"); // ◀ צד שמאל
        s1TMP.fontSize = 30; s1TMP.color = new Color(1, 1, 1, 0.7f);
        s1TMP.alignment = TextAlignmentOptions.Center;

        // Right half (RED = Player 2)
        var rightGO = new GameObject("Right");
        rightGO.transform.SetParent(modal.transform, false);
        var rightRT = rightGO.AddComponent<RectTransform>();
        rightRT.anchorMin = new Vector2(0.5f, 0); rightRT.anchorMax = Vector2.one;
        rightRT.offsetMin = Vector2.zero; rightRT.offsetMax = Vector2.zero;
        rightGO.AddComponent<Image>().color = new Color(TwoPlayerManager.Player2Color.r, TwoPlayerManager.Player2Color.g, TwoPlayerManager.Player2Color.b, 0.85f);

        var name2GO = new GameObject("Name2");
        name2GO.transform.SetParent(rightGO.transform, false);
        var n2RT = name2GO.AddComponent<RectTransform>();
        n2RT.anchorMin = new Vector2(0.1f, 0.55f); n2RT.anchorMax = new Vector2(0.9f, 0.75f);
        n2RT.offsetMin = Vector2.zero; n2RT.offsetMax = Vector2.zero;
        var n2TMP = name2GO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(n2TMP, TwoPlayerManager.GetName(2));
        n2TMP.fontSize = 48; n2TMP.fontStyle = FontStyles.Bold;
        n2TMP.color = Color.white; n2TMP.alignment = TextAlignmentOptions.Center;

        var side2GO = new GameObject("Side2");
        side2GO.transform.SetParent(rightGO.transform, false);
        var s2RT = side2GO.AddComponent<RectTransform>();
        s2RT.anchorMin = new Vector2(0.1f, 0.35f); s2RT.anchorMax = new Vector2(0.9f, 0.5f);
        s2RT.offsetMin = Vector2.zero; s2RT.offsetMax = Vector2.zero;
        var s2TMP = side2GO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(s2TMP, "\u05E6\u05D3 \u05D9\u05DE\u05D9\u05DF \u25B6"); // צד ימין ▶
        s2TMP.fontSize = 30; s2TMP.color = new Color(1, 1, 1, 0.7f);
        s2TMP.alignment = TextAlignmentOptions.Center;

        // Center divider
        var divGO = new GameObject("Divider");
        divGO.transform.SetParent(modal.transform, false);
        var divRT = divGO.AddComponent<RectTransform>();
        divRT.anchorMin = new Vector2(0.5f, 0.1f); divRT.anchorMax = new Vector2(0.5f, 0.9f);
        divRT.sizeDelta = new Vector2(4, 0);
        divGO.AddComponent<Image>().color = new Color(1, 1, 1, 0.5f);

        // Start button (bottom center)
        var startGO = new GameObject("Start");
        startGO.transform.SetParent(modal.transform, false);
        var startRT = startGO.AddComponent<RectTransform>();
        startRT.anchorMin = new Vector2(0.35f, 0.05f); startRT.anchorMax = new Vector2(0.65f, 0.18f);
        startRT.offsetMin = Vector2.zero; startRT.offsetMax = Vector2.zero;
        var startBg = startGO.AddComponent<Image>();
        startBg.color = new Color(0.18f, 0.75f, 0.3f);
        startBg.raycastTarget = true;

        var startTextGO = new GameObject("Text");
        startTextGO.transform.SetParent(startGO.transform, false);
        var stRT = startTextGO.AddComponent<RectTransform>();
        stRT.anchorMin = Vector2.zero; stRT.anchorMax = Vector2.one;
        stRT.offsetMin = Vector2.zero; stRT.offsetMax = Vector2.zero;
        var stTMP = startTextGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(stTMP, "\u05D4\u05EA\u05D7\u05D9\u05DC\u05D5!"); // התחילו!
        stTMP.fontSize = 36; stTMP.fontStyle = FontStyles.Bold;
        stTMP.color = Color.white; stTMP.alignment = TextAlignmentOptions.Center;

        var startBtn = startGO.AddComponent<Button>();
        startBtn.targetGraphic = startBg;
        startBtn.onClick.AddListener(() =>
        {
            Object.Destroy(modal);
            onStartGame?.Invoke(game);
        });
    }
}
