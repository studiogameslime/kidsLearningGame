#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// Creates or updates the UI-based Alin guide prefab with:
/// - RectTransform + Image (not SpriteRenderer)
/// - Animator with UI-compatible animation clips
/// - AlinGuide controller script
///
/// IMPORTANT: Updates existing assets in-place to preserve GUIDs.
/// Deleting and recreating would break all scene references.
///
/// Run via Tools > Kids Learning Game > Setup Alin Guide
/// </summary>
public static class AlinGuideSetup
{
    private const string TalkingSourcePath = "Assets/Art/Alin/Talking/New Animation.anim";
    private const string IdleSourcePath = "Assets/Art/Alin/IdleAnimation.anim";
    private const string OutputFolder = "Assets/Prefabs/UI";

    private const string TalkingClipPath = "Assets/Prefabs/UI/AlinTalking_UI.anim";
    private const string IdleClipPath = "Assets/Prefabs/UI/AlinGuide_Idle.anim";
    private const string ControllerPath = "Assets/Prefabs/UI/AlinGuide.controller";
    private const string PrefabPath = "Assets/Prefabs/UI/AlinGuide.prefab";

    [MenuItem("Tools/Kids Learning Game/Setup Alin Guide")]
    public static void Setup()
    {
        // 1. Extract talking sprite frames
        var talkingSource = AssetDatabase.LoadAssetAtPath<AnimationClip>(TalkingSourcePath);
        if (talkingSource == null)
        {
            Debug.LogError($"Talking animation not found at {TalkingSourcePath}");
            return;
        }

        var talkingFrames = ExtractSpritesFromClip(talkingSource);
        if (talkingFrames.Count == 0)
        {
            Debug.LogError("No sprite frames found in talking animation");
            return;
        }
        Debug.Log($"[AlinSetup] Extracted {talkingFrames.Count} talking frames");

        // 2. Extract idle sprite frames
        var idleSource = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleSourcePath);
        List<FrameData> idleFrames = null;
        if (idleSource != null)
        {
            idleFrames = ExtractSpritesFromClip(idleSource);
            if (idleFrames.Count > 0)
                Debug.Log($"[AlinSetup] Extracted {idleFrames.Count} idle frames ({idleSource.frameRate} FPS)");
            else
                Debug.LogWarning($"[AlinSetup] No sprite frames in {IdleSourcePath}, using single-frame fallback");
        }
        else
        {
            Debug.LogWarning($"[AlinSetup] {IdleSourcePath} not found, using single-frame fallback");
        }

        EnsureFolder(OutputFolder);

        // 3. Create or update animation clips (preserving GUIDs)
        var talkingClip = CreateOrUpdateClip(TalkingClipPath, "AlinTalking_UI",
            talkingFrames, talkingSource.frameRate, loop: true);

        AnimationClip idleClip;
        if (idleFrames != null && idleFrames.Count > 0)
        {
            idleClip = CreateOrUpdateClip(IdleClipPath, "AlinGuide_Idle",
                idleFrames, idleSource.frameRate, loop: true);
        }
        else
        {
            idleClip = CreateOrUpdateSingleFrameClip(IdleClipPath, "AlinGuide_Idle",
                talkingFrames[0].sprite);
        }

        // 4. Create or update animator controller (preserving GUID)
        var controller = CreateOrUpdateController(ControllerPath, idleClip, talkingClip);

        AssetDatabase.SaveAssets();

        // 5. Create or update prefab (preserving GUID so scene references survive)
        Sprite initialSprite = (idleFrames != null && idleFrames.Count > 0)
            ? idleFrames[0].sprite
            : talkingFrames[0].sprite;

        CreateOrUpdatePrefab(PrefabPath, controller, initialSprite);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[AlinSetup] Done!");
        Debug.Log($"  Talking: {talkingFrames.Count} frames, {talkingClip.length:F2}s");
        Debug.Log($"  Idle: {idleClip.length:F2}s ({(idleFrames != null && idleFrames.Count > 0 ? $"{idleFrames.Count} frames" : "single frame")})");
        Debug.Log($"  Animator: Idle <-> Talking (bool 'IsTalking')");
    }

    // ── Asset creation with GUID preservation ──────────────────

    private static AnimationClip CreateOrUpdateClip(string path, string clipName,
        List<FrameData> frames, float frameRate, bool loop)
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null)
        {
            // Update in-place: clear old curves, apply new ones
            existing.ClearCurves();
            // Clear object reference curves too
            foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(existing))
                AnimationUtility.SetObjectReferenceCurve(existing, b, null);

            existing.frameRate = frameRate;
            ApplyUISpriteCurve(existing, frames);

            var settings = AnimationUtility.GetAnimationClipSettings(existing);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(existing, settings);

            EditorUtility.SetDirty(existing);
            Debug.Log($"[AlinSetup] Updated existing clip: {path}");
            return existing;
        }

        // Create new
        var clip = new AnimationClip();
        clip.name = clipName;
        clip.frameRate = frameRate;
        ApplyUISpriteCurve(clip, frames);

        var s = AnimationUtility.GetAnimationClipSettings(clip);
        s.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, s);

        AssetDatabase.CreateAsset(clip, path);
        Debug.Log($"[AlinSetup] Created new clip: {path}");
        return clip;
    }

    private static AnimationClip CreateOrUpdateSingleFrameClip(string path, string clipName, Sprite sprite)
    {
        var frames = new List<FrameData> { new FrameData { time = 0, sprite = sprite } };
        return CreateOrUpdateClip(path, clipName, frames, 1, loop: false);
    }

    private static AnimatorController CreateOrUpdateController(string path,
        AnimationClip idleClip, AnimationClip talkingClip)
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (existing != null)
        {
            // Update existing: rewire motions on existing states
            var sm = existing.layers[0].stateMachine;
            foreach (var state in sm.states)
            {
                if (state.state.name == "Idle")
                    state.state.motion = idleClip;
                else if (state.state.name == "Talking")
                    state.state.motion = talkingClip;
            }
            EditorUtility.SetDirty(existing);
            Debug.Log($"[AlinSetup] Updated existing controller: {path}");
            return existing;
        }

        // Create new controller
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("IsTalking", AnimatorControllerParameterType.Bool);

        var rootSM = controller.layers[0].stateMachine;

        var idleState = rootSM.AddState("Idle", new Vector3(250, 50, 0));
        idleState.motion = idleClip;

        var talkingState = rootSM.AddState("Talking", new Vector3(250, 150, 0));
        talkingState.motion = talkingClip;

        var toTalking = idleState.AddTransition(talkingState);
        toTalking.AddCondition(AnimatorConditionMode.If, 0, "IsTalking");
        toTalking.hasExitTime = false;
        toTalking.duration = 0;

        var toIdle = talkingState.AddTransition(idleState);
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsTalking");
        toIdle.hasExitTime = false;
        toIdle.duration = 0;

        rootSM.defaultState = idleState;

        Debug.Log($"[AlinSetup] Created new controller: {path}");
        return controller;
    }

    private static void CreateOrUpdatePrefab(string path,
        RuntimeAnimatorController controller, Sprite initialSprite)
    {
        var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existingPrefab != null)
        {
            // Update existing prefab in-place — preserve GUID and scene references
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(existingPrefab);

            var image = instance.GetComponent<Image>();
            if (image != null)
                image.sprite = initialSprite;

            var animator = instance.GetComponent<Animator>();
            if (animator != null)
                animator.runtimeAnimatorController = controller;

            // Do NOT change RectTransform — scenes have their own overrides
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            Debug.Log($"[AlinSetup] Updated existing prefab (GUID preserved): {path}");
            return;
        }

        // Create new prefab from scratch
        var go = new GameObject("AlinGuide");

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.86f, 0f);
        rt.anchorMax = new Vector2(0.86f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(250f, 560f);
        rt.anchoredPosition = new Vector2(0f, 10f);

        var img = go.AddComponent<Image>();
        img.sprite = initialSprite;
        img.preserveAspect = true;
        img.raycastTarget = false;

        var anim = go.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        anim.updateMode = AnimatorUpdateMode.UnscaledTime;

        go.AddComponent<AlinGuide>();

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log($"[AlinSetup] Created new prefab: {path}");
    }

    // ── Helpers ────────────────────────────────────────────────

    private struct FrameData
    {
        public float time;
        public Sprite sprite;
    }

    private static List<FrameData> ExtractSpritesFromClip(AnimationClip clip)
    {
        var frames = new List<FrameData>();
        var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);

        foreach (var binding in bindings)
        {
            if (binding.propertyName != "m_Sprite") continue;

            var keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            foreach (var kf in keyframes)
            {
                if (kf.value is Sprite sprite)
                    frames.Add(new FrameData { time = kf.time, sprite = sprite });
            }
        }

        return frames;
    }

    private static void ApplyUISpriteCurve(AnimationClip clip, List<FrameData> frames)
    {
        var keyframes = new ObjectReferenceKeyframe[frames.Count];
        for (int i = 0; i < frames.Count; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = frames[i].time,
                value = frames[i].sprite
            };
        }

        var binding = new EditorCurveBinding
        {
            path = "",
            type = typeof(Image),
            propertyName = "m_Sprite"
        };

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
