using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// One-time setup for the kid model:
//  1. Sets the FBX import (Generic rig, looping clips, project materials)
//  2. Builds the Animator Controller (idle/walk/run, crouch idle/walk, jump)
//  3. Adds the model to the ChildPlayer prefab and hides the old placeholder body
// Runs by itself once after the files are added. Re-run any time from Tools > Child Player > Set Up Kid Model.
[InitializeOnLoad]
public static class KidModelSetup
{
    private const string ModelPath = "Assets/Models/Kid.fbx";
    private const string ControllerPath = "Assets/Animations/ChildPlayer.controller";
    private const string PrefabPath = "Assets/Prefab/ChildPlayer.prefab";
    private const string ModelChildName = "KidModel";
    // Kid size (from the Blender model): 1.04 m tall, eyes at 0.87 m, hips at 0.416 m
    private const float KidHeight = 1.05f;
    private const float KidRadius = 0.2f;
    private const float HipsHeight = 0.416f;
    private static readonly Vector3 EyePosition = new Vector3(0f, 0.87f, 0.07f);

    private static readonly string[] MaterialNames = { "ChildGown", "ChildSkin", "ChildHair", "ChildEyes", "ChildShoes" };

    static KidModelSetup()
    {
        EditorApplication.delayCall += () =>
        {
            // never during play mode: scripts reload when Play is pressed, and changes made then aren't saved
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) != null && NeedsSetup())
                Run();
        };
    }

    private static bool NeedsSetup()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) == null)
            return true;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
            return false;
        var cc = prefab.GetComponent<CharacterController>();
        return prefab.transform.Find(ModelChildName) == null ||
               cc == null || Mathf.Abs(cc.height - KidHeight) > 0.01f;
    }

    [MenuItem("Tools/Child Player/Set Up Kid Model")]
    public static void Run()
    {
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"KidModelSetup: {ModelPath} not found.");
            return;
        }

        // ---------- 1. import settings ----------
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        foreach (string matName in MaterialNames)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/{matName}.mat");
            if (mat != null)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);
        }
        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            string shortName = clip.takeName.Substring(clip.takeName.LastIndexOf('|') + 1);
            clip.name = shortName;
            clip.loopTime = shortName != "Jump";
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();

        Dictionary<string, AnimationClip> anims = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToDictionary(c => c.name, c => c);
        foreach (string needed in new[] { "Idle", "Walk", "Run", "Jump", "CrouchIdle", "CrouchWalk" })
        {
            if (!anims.ContainsKey(needed))
            {
                Debug.LogError($"KidModelSetup: animation '{needed}' missing from {ModelPath}. Found: {string.Join(", ", anims.Keys)}");
                return;
            }
        }

        // ---------- 2. animator controller ----------
        if (!AssetDatabase.IsValidFolder("Assets/Animations"))
            AssetDatabase.CreateFolder("Assets", "Animations");
        AssetDatabase.DeleteAsset(ControllerPath); // only ever our own generated file
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Crouch", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        controller.parameters = controller.parameters.Select(p =>
        {
            if (p.name == "Grounded") p.defaultBool = true;
            return p;
        }).ToArray();

        AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree locoTree, 0);
        locoTree.blendParameter = "Speed";
        locoTree.useAutomaticThresholds = false;
        locoTree.AddChild(anims["Idle"], 0f);
        locoTree.AddChild(anims["Walk"], 2.2f);
        locoTree.AddChild(anims["Run"], 4.0f);

        AnimatorState crouch = controller.CreateBlendTreeInController("Crouch", out BlendTree crouchTree, 0);
        crouchTree.blendParameter = "Speed";
        crouchTree.useAutomaticThresholds = false;
        crouchTree.AddChild(anims["CrouchIdle"], 0f);
        crouchTree.AddChild(anims["CrouchWalk"], 1.3f);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState jump = sm.AddState("Jump");
        jump.motion = anims["Jump"];
        sm.defaultState = locomotion;

        AddTransition(locomotion, crouch, 0.15f, ("Crouch", true));
        AddTransition(crouch, locomotion, 0.15f, ("Crouch", false));
        AddTransition(locomotion, jump, 0.08f, ("Grounded", false));
        AddTransition(crouch, jump, 0.08f, ("Grounded", false));
        AddTransition(jump, crouch, 0.1f, ("Grounded", true), ("Crouch", true));
        AddTransition(jump, locomotion, 0.1f, ("Grounded", true));
        AssetDatabase.SaveAssets();

        // ---------- 3. add the model to the prefab ----------
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform old = root.transform.Find(ModelChildName);
            if (old != null)
                Object.DestroyImmediate(old.gameObject); // replaces a model added by an earlier run of this tool

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
            model.name = ModelChildName;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            SetLayer(model.transform, root.layer);

            FaceForwardAndScale(model);

            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // keep the old placeholder body, just switch it off
            Transform placeholder = root.transform.Find("Body");
            if (placeholder != null) placeholder.gameObject.SetActive(false);

            var driver = root.GetComponent<ChildPlayerAnimator>();
            if (driver == null) driver = root.AddComponent<ChildPlayerAnimator>();
            var so = new SerializedObject(driver);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.ApplyModifiedPropertiesWithoutUndo();

            // collider and camera sized to the kid
            var cc = root.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.height = KidHeight;
                cc.radius = KidRadius;
                cc.center = new Vector3(0f, KidHeight / 2f, 0f);
                cc.stepOffset = 0.25f;
            }
            Transform cam = root.transform.Find("PlayerCamera");
            if (cam != null) cam.localPosition = EyePosition;

            var move = root.GetComponent<ChildPlayerController>();
            if (move != null)
            {
                var ms = new SerializedObject(move);
                ms.FindProperty("walkSpeed").floatValue = 2.2f;
                ms.FindProperty("sprintSpeed").floatValue = 4.0f;
                ms.FindProperty("jumpHeight").floatValue = 0.6f;
                ms.FindProperty("crouchHeight").floatValue = 0.76f;
                ms.FindProperty("crouchCameraHeight").floatValue = 0.57f;
                ms.FindProperty("crouchCameraForward").floatValue = 0.20f;
                ms.FindProperty("thirdPersonOffset").vector3Value = new Vector3(0.3f, 0.12f, -1.4f);
                ms.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("KidModelSetup: kid model, animations and Animator Controller are set up on the ChildPlayer prefab.");
    }

    private static void AddTransition(AnimatorState from, AnimatorState to, float duration, params (string param, bool value)[] conditions)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = duration;
        foreach (var c in conditions)
            t.AddCondition(c.value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, c.param);
    }

    // Makes sure the kid faces the ChildPlayer's forward (+Z) and is the right size, whatever the FBX axis settings.
    private static void FaceForwardAndScale(GameObject model)
    {
        Transform left = FindDeep(model.transform, "LeftUpperLeg");
        Transform right = FindDeep(model.transform, "RightUpperLeg");
        Transform hips = FindDeep(model.transform, "Hips");
        if (left == null || right == null || hips == null)
        {
            Debug.LogWarning("KidModelSetup: couldn't find leg bones to check facing; leaving model rotation as imported.");
            return;
        }

        Vector3 parentRight = model.transform.parent.InverseTransformDirection(right.position - left.position);
        parentRight.y = 0f;
        Vector3 facing = Vector3.Cross(parentRight.normalized, Vector3.up);
        if (facing.sqrMagnitude > 0.001f)
        {
            float yaw = Vector3.SignedAngle(facing, Vector3.forward, Vector3.up);
            model.transform.localRotation = Quaternion.AngleAxis(yaw, Vector3.up) * model.transform.localRotation;
        }

        float hipsHeight = model.transform.parent.InverseTransformPoint(hips.position).y;
        const float expectedHips = HipsHeight;
        if (hipsHeight > 0.01f && Mathf.Abs(hipsHeight - expectedHips) > 0.05f)
            model.transform.localScale *= expectedHips / hipsHeight;
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    private static void SetLayer(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform child in t) SetLayer(child, layer);
    }
}
