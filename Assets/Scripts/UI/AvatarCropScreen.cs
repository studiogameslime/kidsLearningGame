using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Full-screen avatar crop screen. Image keeps original aspect ratio.
/// Circle mask shows the final crop area. Drag + pinch to position.
/// Live preview mirrors exact crop result.
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

        // Base scale: shortest dimension fills the crop circle exactly
        float minDim = Mathf.Min(sourceTex.width, sourceTex.height);
        float baseScale = cropDiameter / minDim;

        // ── Modal (full screen dark) ──
        var modal = new GameObject("CropModal");
        modal.transform.SetParent(canvas.transform, false);
        modal.transform.SetAsLastSibling();
        var modalRT = modal.AddComponent<RectTransform>();
        modalRT.anchorMin = Vector2.zero; modalRT.anchorMax = Vector2.one;
        modalRT.offsetMin = Vector2.zero; modalRT.offsetMax = Vector2.zero;
        modal.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 1f);
        modal.GetComponent<Image>().raycastTarget = true;

        // Store modal reference for cleanup
        var dashCtrl = host as ParentDashboardController;

        // ═══ TOP BAR ═══
        var topBar = new GameObject("TopBar");
        topBar.transform.SetParent(modal.transform, false);
        var tbRT = topBar.AddComponent<RectTransform>();
        tbRT.anchorMin = new Vector2(0, 1); tbRT.anchorMax = Vector2.one;
        tbRT.pivot = new Vector2(0.5f, 1); tbRT.sizeDelta = new Vector2(0, 56);
        var tbL = topBar.AddComponent<HorizontalLayoutGroup>();
        tbL.padding = new RectOffset(24, 24, 6, 6); tbL.spacing = 12;
        tbL.childAlignment = TextAnchor.MiddleCenter;
        tbL.childForceExpandWidth = false; tbL.childControlWidth = true; tbL.childControlHeight = true;

        // Back
        var backGO = MakeTMP(topBar.transform, "\u25C0", 26, Color.white);
        backGO.AddComponent<LayoutElement>().preferredWidth = 44;
        var backBtn = backGO.AddComponent<Button>();
        backBtn.transition = Selectable.Transition.None;
        backBtn.onClick.AddListener(() => { Object.Destroy(sourceTex); Object.Destroy(modal); });

        // Title
        var titleGO = MakeTMP(topBar.transform, "", 24, Color.white);
        HebrewText.SetText(titleGO.GetComponent<TextMeshProUGUI>(), "\u05EA\u05DE\u05D5\u05E0\u05EA \u05E4\u05E8\u05D5\u05E4\u05D9\u05DC"); // תמונת פרופיל
        titleGO.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        titleGO.AddComponent<LayoutElement>().flexibleWidth = 1;

        // Save
        var saveGO = MakeTMP(topBar.transform, "", 22, new Color(0.3f, 0.85f, 0.45f));
        HebrewText.SetText(saveGO.GetComponent<TextMeshProUGUI>(), "\u05E9\u05DE\u05D5\u05E8"); // שמור
        saveGO.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        saveGO.AddComponent<LayoutElement>().preferredWidth = 70;

        // ═══ WORKSPACE ═══
        var ws = new GameObject("Workspace");
        ws.transform.SetParent(modal.transform, false);
        var wsRT = ws.AddComponent<RectTransform>();
        wsRT.anchorMin = new Vector2(0.05f, 0.1f); wsRT.anchorMax = new Vector2(0.72f, 0.92f);
        wsRT.offsetMin = Vector2.zero; wsRT.offsetMax = Vector2.zero;
        ws.AddComponent<RectMask2D>();

        // Photo image — natural aspect ratio
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

        // ── Dim overlay (4 panels around circle) ──
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

        // ═══ HELPER TEXT ═══
        var helperGO = MakeTMP(modal.transform, "", 18, new Color(1, 1, 1, 0.4f));
        HebrewText.SetText(helperGO.GetComponent<TextMeshProUGUI>(),
            "\u05D4\u05D6\u05D9\u05D6\u05D5 \u05D0\u05EA \u05D4\u05EA\u05DE\u05D5\u05E0\u05D4 \u05DB\u05DA \u05E9\u05D4\u05E4\u05E0\u05D9\u05DD \u05D9\u05D4\u05D9\u05D5 \u05D1\u05EA\u05D5\u05DA \u05D4\u05E2\u05D9\u05D2\u05D5\u05DC");
        helperGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        var helpRT = helperGO.GetComponent<RectTransform>();
        helpRT.anchorMin = new Vector2(0.05f, 0.03f); helpRT.anchorMax = new Vector2(0.72f, 0.09f);
        helpRT.offsetMin = Vector2.zero; helpRT.offsetMax = Vector2.zero;

        // ═══ RIGHT PANEL ═══
        var rp = new GameObject("RightPanel");
        rp.transform.SetParent(modal.transform, false);
        var rpRT = rp.AddComponent<RectTransform>();
        rpRT.anchorMin = new Vector2(0.76f, 0.15f); rpRT.anchorMax = new Vector2(0.96f, 0.85f);
        rpRT.offsetMin = Vector2.zero; rpRT.offsetMax = Vector2.zero;
        var rpVL = rp.AddComponent<VerticalLayoutGroup>();
        rpVL.spacing = 14; rpVL.childAlignment = TextAnchor.UpperCenter;
        rpVL.childForceExpandWidth = true; rpVL.childForceExpandHeight = false;
        rpVL.childControlWidth = true; rpVL.childControlHeight = true;

        // Preview label
        var plGO = MakeTMP(rp.transform, "", 15, new Color(1, 1, 1, 0.45f));
        HebrewText.SetText(plGO.GetComponent<TextMeshProUGUI>(), "\u05EA\u05E6\u05D5\u05D2\u05D4 \u05DE\u05E7\u05D3\u05D9\u05DE\u05D4");
        plGO.AddComponent<LayoutElement>().preferredHeight = 20;

        // Live circular preview
        float prevSize = 100f;
        var lpGO = new GameObject("LP");
        lpGO.transform.SetParent(rp.transform, false);
        lpGO.AddComponent<RectTransform>();
        lpGO.AddComponent<LayoutElement>().preferredHeight = prevSize + 10;

        var pCircle = new GameObject("PC");
        pCircle.transform.SetParent(lpGO.transform, false);
        var pcRT = pCircle.AddComponent<RectTransform>();
        pcRT.anchorMin = pcRT.anchorMax = new Vector2(0.5f, 0.5f);
        pcRT.sizeDelta = new Vector2(prevSize, prevSize);
        if (circleSprite != null) { var pci = pCircle.AddComponent<Image>(); pci.sprite = circleSprite; pci.raycastTarget = false; }
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

        // ═══ INTERACTIONS ═══
        var dragger = imgGO.AddComponent<AvatarImageDragger>();
        dragger.canvas = canvas;
        dragger.Init(sourceTex.width, sourceTex.height, baseScale, cropRadius);

        var updater = imgGO.AddComponent<AvatarPreviewUpdater>();
        updater.sourceRT = imgRT;
        updater.previewImageRT = piRT;
        updater.cropDiameter = cropDiameter;
        updater.previewDiameter = prevSize;

        // Save handler
        var saveBtn = saveGO.AddComponent<Button>();
        saveBtn.transition = Selectable.Transition.None;
        saveBtn.onClick.AddListener(() =>
        {
            SaveAvatar(sourceTex, imgRT, cropDiameter, dragger.CurrentScale);
            Object.Destroy(modal);
        });
    }

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
}
