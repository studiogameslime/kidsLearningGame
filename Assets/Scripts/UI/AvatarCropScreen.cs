using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Full-screen avatar crop screen. Image keeps original aspect ratio.
/// Drag + pinch to position face in circle. Live preview on right.
/// Save/Cancel/Change buttons below preview.
/// </summary>
public static class AvatarCropScreen
{
    private const float CircleFraction = 0.65f;

    public static void Show(MonoBehaviour host, Texture2D sourceTex, Sprite roundedRect)
    {
        var canvas = host.GetComponentInParent<Canvas>();
        if (canvas == null) { Object.Destroy(sourceTex); return; }

        var circleSprite = Resources.Load<Sprite>("Circle");
        var canvasRT = canvas.GetComponent<RectTransform>();
        float screenH = canvasRT != null && canvasRT.rect.height > 0 ? canvasRT.rect.height : 1080f;

        float workspaceH = screenH * 0.78f;
        float cropDiameter = workspaceH * CircleFraction;
        float cropRadius = cropDiameter / 2f;

        float minDim = Mathf.Min(sourceTex.width, sourceTex.height);
        float baseScale = cropDiameter / minDim;

        // ── Modal ──
        var modal = new GameObject("CropModal");
        modal.transform.SetParent(canvas.transform, false);
        modal.transform.SetAsLastSibling();
        var modalRT = modal.AddComponent<RectTransform>();
        modalRT.anchorMin = Vector2.zero; modalRT.anchorMax = Vector2.one;
        modalRT.offsetMin = Vector2.zero; modalRT.offsetMax = Vector2.zero;
        modal.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 1f);
        modal.GetComponent<Image>().raycastTarget = true;

        // ═══ TOP BAR (title only, no save) ═══
        var topBar = new GameObject("TopBar");
        topBar.transform.SetParent(modal.transform, false);
        var tbRT = topBar.AddComponent<RectTransform>();
        tbRT.anchorMin = new Vector2(0, 1); tbRT.anchorMax = Vector2.one;
        tbRT.pivot = new Vector2(0.5f, 1); tbRT.sizeDelta = new Vector2(0, 56);

        var titleGO = MakeTMP(topBar.transform, "", 26, Color.white);
        HebrewText.SetText(titleGO.GetComponent<TextMeshProUGUI>(),
            "\u05EA\u05DE\u05D5\u05E0\u05EA \u05E4\u05E8\u05D5\u05E4\u05D9\u05DC"); // תמונת פרופיל
        titleGO.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        titleGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        var titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = Vector2.zero; titleRT.anchorMax = Vector2.one;
        titleRT.offsetMin = Vector2.zero; titleRT.offsetMax = Vector2.zero;

        // ═══ WORKSPACE ═══
        var ws = new GameObject("Workspace");
        ws.transform.SetParent(modal.transform, false);
        var wsRT = ws.AddComponent<RectTransform>();
        wsRT.anchorMin = new Vector2(0.03f, 0.08f); wsRT.anchorMax = new Vector2(0.65f, 0.94f);
        wsRT.offsetMin = Vector2.zero; wsRT.offsetMax = Vector2.zero;
        ws.AddComponent<RectMask2D>();

        // Photo
        var imgGO = new GameObject("Photo");
        imgGO.transform.SetParent(ws.transform, false);
        var imgRT = imgGO.AddComponent<RectTransform>();
        imgRT.anchorMin = imgRT.anchorMax = new Vector2(0.5f, 0.5f);
        imgRT.sizeDelta = new Vector2(sourceTex.width * baseScale, sourceTex.height * baseScale);
        var imgComp = imgGO.AddComponent<Image>();
        imgComp.sprite = Sprite.Create(sourceTex,
            new Rect(0, 0, sourceTex.width, sourceTex.height), new Vector2(0.5f, 0.5f));
        imgComp.preserveAspect = false;
        imgComp.raycastTarget = true;

        // Dim overlay (4 panels)
        Color dimC = new Color(0.06f, 0.06f, 0.08f, 0.55f);
        MakeDim(ws.transform, new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(0, cropRadius), Vector2.zero, dimC);
        MakeDim(ws.transform, Vector2.zero, new Vector2(1, 0.5f), Vector2.zero, new Vector2(0, -cropRadius), dimC);
        MakeDim(ws.transform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -cropRadius), new Vector2(-cropRadius, cropRadius), dimC);
        MakeDim(ws.transform, new Vector2(0.5f, 0.5f), new Vector2(1, 0.5f), new Vector2(cropRadius, -cropRadius), new Vector2(0, cropRadius), dimC);

        // Circle ring
        if (circleSprite != null)
        {
            var ring = new GameObject("Ring");
            ring.transform.SetParent(ws.transform, false);
            var rRT = ring.AddComponent<RectTransform>();
            rRT.anchorMin = rRT.anchorMax = new Vector2(0.5f, 0.5f);
            rRT.sizeDelta = new Vector2(cropDiameter + 4, cropDiameter + 4);
            var rImg = ring.AddComponent<Image>();
            rImg.sprite = circleSprite; rImg.fillCenter = false;
            rImg.color = new Color(1, 1, 1, 0.6f); rImg.raycastTarget = false;
        }

        // Helper text
        var helperGO = MakeTMP(modal.transform, "", 18, new Color(1, 1, 1, 0.4f));
        HebrewText.SetText(helperGO.GetComponent<TextMeshProUGUI>(),
            "\u05D4\u05D6\u05D9\u05D6\u05D5 \u05D0\u05EA \u05D4\u05EA\u05DE\u05D5\u05E0\u05D4 \u05DB\u05DA \u05E9\u05D4\u05E4\u05E0\u05D9\u05DD \u05D9\u05D4\u05D9\u05D5 \u05D1\u05EA\u05D5\u05DA \u05D4\u05E2\u05D9\u05D2\u05D5\u05DC");
        helperGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        var helpRT = helperGO.GetComponent<RectTransform>();
        helpRT.anchorMin = new Vector2(0.03f, 0.02f); helpRT.anchorMax = new Vector2(0.65f, 0.07f);
        helpRT.offsetMin = Vector2.zero; helpRT.offsetMax = Vector2.zero;

        // ═══ RIGHT PANEL (preview + all buttons) ═══
        var rp = new GameObject("RightPanel");
        rp.transform.SetParent(modal.transform, false);
        var rpRT = rp.AddComponent<RectTransform>();
        rpRT.anchorMin = new Vector2(0.67f, 0.08f); rpRT.anchorMax = new Vector2(0.97f, 0.94f);
        rpRT.offsetMin = Vector2.zero; rpRT.offsetMax = Vector2.zero;
        var rpVL = rp.AddComponent<VerticalLayoutGroup>();
        rpVL.spacing = 12; rpVL.childAlignment = TextAnchor.UpperCenter;
        rpVL.childForceExpandWidth = true; rpVL.childForceExpandHeight = false;
        rpVL.childControlWidth = true; rpVL.childControlHeight = true;
        rpVL.padding = new RectOffset(8, 8, 0, 0);

        // Preview label
        var plGO = MakeTMP(rp.transform, "", 18, new Color(1, 1, 1, 0.5f));
        HebrewText.SetText(plGO.GetComponent<TextMeshProUGUI>(),
            "\u05EA\u05E6\u05D5\u05D2\u05D4 \u05DE\u05E7\u05D3\u05D9\u05DE\u05D4"); // תצוגה מקדימה
        plGO.AddComponent<LayoutElement>().preferredHeight = 26;

        // Large circular preview
        float prevSize = 180f;
        var lpGO = new GameObject("LP");
        lpGO.transform.SetParent(rp.transform, false);
        lpGO.AddComponent<RectTransform>();
        lpGO.AddComponent<LayoutElement>().preferredHeight = prevSize + 10;

        var pCircle = new GameObject("PC");
        pCircle.transform.SetParent(lpGO.transform, false);
        var pcRT = pCircle.AddComponent<RectTransform>();
        pcRT.anchorMin = pcRT.anchorMax = new Vector2(0.5f, 0.5f);
        pcRT.sizeDelta = new Vector2(prevSize, prevSize);
        if (circleSprite != null)
        {
            var pci = pCircle.AddComponent<Image>();
            pci.sprite = circleSprite; pci.raycastTarget = false;
        }
        pCircle.AddComponent<Mask>().showMaskGraphic = true;

        var piGO = new GameObject("PI");
        piGO.transform.SetParent(pCircle.transform, false);
        var piRT = piGO.AddComponent<RectTransform>();
        piRT.anchorMin = piRT.anchorMax = new Vector2(0.5f, 0.5f);
        piRT.sizeDelta = imgRT.sizeDelta;
        var piImg = piGO.AddComponent<Image>();
        piImg.sprite = imgComp.sprite; piImg.preserveAspect = false; piImg.raycastTarget = false;

        // Spacer
        var spGO = new GameObject("Sp");
        spGO.transform.SetParent(rp.transform, false);
        spGO.AddComponent<RectTransform>(); spGO.AddComponent<LayoutElement>().flexibleHeight = 1;

        // ── Save button (green, prominent) ──
        var saveBtnGO = MakeButton(rp.transform, "\u05E9\u05DE\u05D5\u05E8", // שמור
            new Color(0.18f, 0.7f, 0.3f), Color.white, 28, 55);
        saveBtnGO.GetComponent<Button>().onClick.AddListener(() =>
        {
            var dragger = imgGO.GetComponent<AvatarImageDragger>();
            float scale = dragger != null ? dragger.CurrentScale : baseScale;
            SaveAvatar(sourceTex, imgRT, cropDiameter, scale);
            Object.Destroy(modal);
        });

        // ── Choose another photo ──
        var changeBtnGO = MakeButton(rp.transform, "\u05EA\u05DE\u05D5\u05E0\u05D4 \u05D0\u05D7\u05E8\u05EA", // תמונה אחרת
            new Color(0.3f, 0.3f, 0.35f), Color.white, 22, 45);
        changeBtnGO.GetComponent<Button>().onClick.AddListener(() =>
        {
            Object.Destroy(sourceTex);
            Object.Destroy(modal);
            var ctrl = host as ParentDashboardController;
            if (ctrl != null) ctrl.PickAvatarImage();
        });

        // ── Cancel button ──
        var cancelBtnGO = MakeButton(rp.transform, "\u05D1\u05D9\u05D8\u05D5\u05DC", // ביטול
            new Color(0.25f, 0.25f, 0.28f), new Color(1, 1, 1, 0.7f), 22, 45);
        cancelBtnGO.GetComponent<Button>().onClick.AddListener(() =>
        {
            Object.Destroy(sourceTex);
            Object.Destroy(modal);
        });

        // ═══ INTERACTIONS ═══
        var draggerComp = imgGO.AddComponent<AvatarImageDragger>();
        draggerComp.canvas = canvas;
        draggerComp.Init(sourceTex.width, sourceTex.height, baseScale, cropRadius);

        var updater = imgGO.AddComponent<AvatarPreviewUpdater>();
        updater.sourceRT = imgRT;
        updater.previewImageRT = piRT;
        updater.cropDiameter = cropDiameter;
        updater.previewDiameter = prevSize;
    }

    // ── Save ──

    private static void SaveAvatar(Texture2D source, RectTransform imageRT, float cropDiameter, float scale)
    {
        var profile = ProfileManager.ActiveProfile;
        if (profile == null) { Object.Destroy(source); return; }

        Vector2 offset = imageRT.anchoredPosition;
        float texPerPx = 1f / scale;
        float texCropSize = cropDiameter * texPerPx;
        float cx = source.width / 2f - offset.x * texPerPx;
        float cy = source.height / 2f - offset.y * texPerPx;

        int x = Mathf.Clamp(Mathf.RoundToInt(cx - texCropSize / 2f), 0, source.width - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(cy - texCropSize / 2f), 0, source.height - 1);
        int sz = Mathf.RoundToInt(texCropSize);
        sz = Mathf.Max(1, Mathf.Min(sz, source.width - x, source.height - y));

        var cropped = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        cropped.SetPixels(source.GetPixels(x, y, sz, sz));
        cropped.Apply();

        var rt = RenderTexture.GetTemporary(256, 256);
        Graphics.Blit(cropped, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var final256 = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        final256.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
        final256.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        Object.Destroy(cropped);

        byte[] png = final256.EncodeToPNG();
        Object.Destroy(final256);
        Object.Destroy(source);

        string dir = System.IO.Path.Combine(Application.persistentDataPath, "profiles", profile.id);
        if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "avatar.png"), png);

        profile.avatarImagePath = $"profiles/{profile.id}/avatar.png";
        ProfileManager.Instance.Save();
        Debug.Log($"[Avatar] Saved: {profile.avatarImagePath}");
    }

    // ── Helpers ──

    private static void MakeDim(Transform parent, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, Color c)
    {
        var go = new GameObject("Dim");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
        go.AddComponent<Image>().color = c;
        go.GetComponent<Image>().raycastTarget = false;
    }

    private static GameObject MakeTMP(Transform parent, string text, int size, Color color)
    {
        var go = new GameObject("T");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return go;
    }

    private static GameObject MakeButton(Transform parent, string hebrewText, Color bgColor, Color textColor, int fontSize, float height)
    {
        var go = new GameObject("Btn");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        go.AddComponent<LayoutElement>().preferredHeight = height;

        var bg = go.AddComponent<Image>();
        var roundedRect = Resources.Load<Sprite>("UI/RoundedRect");
        if (roundedRect != null) { bg.sprite = roundedRect; bg.type = Image.Type.Sliced; }
        bg.color = bgColor;
        bg.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;
        btn.targetGraphic = bg;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero; textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero; textRT.offsetMax = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        HebrewText.SetText(tmp, hebrewText);
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = textColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        return go;
    }
}
