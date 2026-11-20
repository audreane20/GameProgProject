using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Adds the monster to the HospitalEscape scene without rebuilding the rest of the level:
//  - sets up Monster.fbx (Generic rig, clips, materials) and its Animator Controller
//  - bakes a NavMesh so it walks around walls and furniture
//  - places the monster, its patrol waypoints and its sounds
// Runs by itself once if the scene has no monster. Re-run: Tools > Hospital Escape > Add or Reset Monster.
// It only replaces objects it made itself ("Monster", "MonsterWaypoints", "MonsterNavMesh").
[InitializeOnLoad]
public static class MonsterSetup
{
    private const string ModelPath = "Assets/Models/Monster.fbx";
    private const string ControllerPath = "Assets/Animations/Monster.controller";
    private const string ScenePath = "Assets/Scenes/HospitalEscape.unity";
    private const string NavDataFolder = "Assets/Scenes/HospitalEscape";
    private const string NavDataPath = NavDataFolder + "/MonsterNavMesh.asset";
    private const string MaterialFolder = "Assets/Materials/Escape";
    private const string AudioFolder = "Assets/Audio/Monster";
    private const int EnemyLayer = 6;
    private const int PlayerLayer = 3;
    private const int DecorLayer = 2;
    private static readonly string[] Clips = { "Idle", "Walk", "Run", "Stare", "Attack" };

    static MonsterSetup()
    {
        EditorApplication.delayCall += () =>
        {
            // never during play mode: scripts reload when Play is pressed, and changes made then aren't saved
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(ScenePath) || AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null) return;
            if (SceneFileHasMonster()) return;
            Scene open = SceneManager.GetSceneByPath(ScenePath);
            if (open.IsValid() && open.isLoaded && open.isDirty)
            {
                Debug.LogWarning("MonsterSetup: save the HospitalEscape scene, then use Tools > Hospital Escape > Add or Reset Monster.");
                return;
            }
            AddToEscapeScene();
        };
    }

    private static bool SceneFileHasMonster() =>
        Regex.IsMatch(File.ReadAllText(ScenePath), @"m_Name: Monster\r?\n");

    [MenuItem("Tools/Hospital Escape/Add or Reset Monster")]
    private static void Menu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Add Monster", "Stop Play mode first.", "OK");
            return;
        }
        Scene open = SceneManager.GetSceneByPath(ScenePath);
        if (open.IsValid() && open.isLoaded && open.isDirty)
        {
            if (!EditorUtility.DisplayDialog("Add Monster", "The HospitalEscape scene has unsaved changes. Save it and add the monster?", "Save and continue", "Cancel"))
                return;
            EditorSceneManager.SaveScene(open);
        }
        AddToEscapeScene();
    }

    public static void AddToEscapeScene()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasOpen = scene.IsValid() && scene.isLoaded;
        Scene previous = SceneManager.GetActiveScene();
        if (!wasOpen) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        SceneManager.SetActiveScene(scene);
        try
        {
            AddToActiveScene();
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (!wasOpen)
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
        Debug.Log("MonsterSetup: monster added to " + ScenePath);
    }

    // Used by HospitalEscapeBuilder too (the escape scene must be the active scene).
    public static void AddToActiveScene()
    {
        if (!PrepareModel(out AnimatorController controller)) return;

        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == "Monster" || root.name == "MonsterWaypoints" || root.name == "MonsterNavMesh")
                Object.DestroyImmediate(root);

        Transform[] waypoints = CreateWaypoints();
        BakeNavMesh();
        CreateMonster(controller, waypoints);
    }

    // ------------------------------------------------------------ model, materials, animator
    private static bool PrepareModel(out AnimatorController controller)
    {
        controller = null;
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError("MonsterSetup: " + ModelPath + " not found.");
            return false;
        }

        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Materials", "Escape");
        var mats = new Dictionary<string, Material>
        {
            ["MonsterSkin"] = Mat("MonsterSkin", new Color(0.022f, 0.021f, 0.026f), 0.55f, null),
            ["MonsterClaw"] = Mat("MonsterClaw", new Color(0.3f, 0.27f, 0.22f), 0.5f, null),
            ["MonsterTeeth"] = Mat("MonsterTeeth", new Color(0.5f, 0.47f, 0.4f), 0.45f, null),
            ["MonsterMouth"] = Mat("MonsterMouth", new Color(0.06f, 0f, 0f), 0.6f, null),
            ["MonsterEyes"] = Mat("MonsterEyes", new Color(0.3f, 0.01f, 0.01f), 0.8f, new Color(0.25f, 0.01f, 0.005f)),
        };

        // always refresh the clip list so frame ranges match the current FBX
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var kv in mats)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            var clips = importer.defaultClipAnimations;
            foreach (var c in clips)
            {
                c.name = c.takeName.Substring(c.takeName.LastIndexOf('|') + 1);
                c.loopTime = c.name != "Attack";
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        var anims = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name, c => c);
        foreach (string n in Clips)
        {
            if (!anims.ContainsKey(n))
            {
                Debug.LogError($"MonsterSetup: animation '{n}' missing from {ModelPath}. Found: {string.Join(", ", anims.Keys)}");
                return false;
            }
        }

        if (!AssetDatabase.IsValidFolder("Assets/Animations")) AssetDatabase.CreateFolder("Assets", "Animations");
        AssetDatabase.DeleteAsset(ControllerPath); // our own generated file
        controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        foreach (string n in Clips)
        {
            AnimatorState s = sm.AddState(n);
            s.motion = anims[n];
            if (n == "Idle") sm.defaultState = s;
        }
        AssetDatabase.SaveAssets();
        return true;
    }

    private static Material Mat(string name, Color color, float smoothness, Color? emission)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------ waypoints & navmesh
    private static Transform[] CreateWaypoints()
    {
        var points = new (string, Vector3)[]
        {
            ("WardA", new Vector3(-13.5f, 0, 16.0f)), ("Storage", new Vector3(-6f, 0, 16.5f)),
            ("Lounge", new Vector3(1.5f, 0, 15.8f)), ("Office", new Vector3(11f, 0, 16f)),
            ("WardB", new Vector3(-12f, 0, 7.2f)), ("Pharmacy", new Vector3(12f, 0, 7.8f)),
            ("Lobby", new Vector3(1.5f, 0, 5f)), ("LobbyDoor", new Vector3(-1.5f, 0, 2.2f)),
            ("Corridor_W", new Vector3(-15.5f, 0, 11.5f)), ("Corridor_MidW", new Vector3(-6f, 0, 11.5f)),
            ("Corridor_MidE", new Vector3(5f, 0, 11.5f)), ("Corridor_E", new Vector3(15.5f, 0, 11.5f)),
        };
        var parent = new GameObject("MonsterWaypoints").transform;
        var list = new List<Transform>();
        foreach (var (n, p) in points)
        {
            var t = new GameObject("Waypoint_" + n).transform;
            t.SetParent(parent);
            t.position = p;
            list.Add(t);
        }
        return list.ToArray();
    }

    private static void BakeNavMesh()
    {
        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
        settings.agentRadius = 0.35f;
        settings.agentHeight = 1.9f;
        settings.agentClimb = 0.25f;   // can't step up onto beds or tables
        settings.agentSlope = 40f;
        settings.overrideVoxelSize = true;
        settings.voxelSize = 0.07f;
        settings.minRegionArea = 2f;

        // floor up to just below the ceiling, so ceilings and wall tops aren't walkable
        var bounds = new Bounds(new Vector3(0f, 0.8f, 10.5f), new Vector3(40f, 3.6f, 26f));
        int mask = ~((1 << DecorLayer) | (1 << PlayerLayer) | (1 << EnemyLayer));
        var sources = new List<NavMeshBuildSource>();
        UnityEngine.AI.NavMeshBuilder.CollectSources(bounds, mask, NavMeshCollectGeometry.RenderMeshes, 0, new List<NavMeshBuildMarkup>(), sources);
        NavMeshData data = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
        data.name = "MonsterNavMesh";

        if (!AssetDatabase.IsValidFolder(NavDataFolder)) AssetDatabase.CreateFolder("Assets/Scenes", "HospitalEscape");
        AssetDatabase.DeleteAsset(NavDataPath);
        AssetDatabase.CreateAsset(data, NavDataPath);

        var go = new GameObject("MonsterNavMesh");
        var surface = go.AddComponent<NavMeshSurface>();
        surface.agentTypeID = 0;
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        surface.layerMask = mask;
        surface.navMeshData = data;
        Debug.Log($"MonsterSetup: baked NavMesh from {sources.Count} meshes.");
    }

    // ------------------------------------------------------------ the monster
    private static void CreateMonster(AnimatorController controller, Transform[] waypoints)
    {
        var root = new GameObject("Monster");
        root.tag = "Enemy";
        root.layer = EnemyLayer;
        root.transform.position = new Vector3(12f, 0f, 7.8f); // starts in the pharmacy, far from the kid
        root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        var agent = root.AddComponent<NavMeshAgent>();
        agent.agentTypeID = 0;
        agent.radius = 0.35f;
        agent.height = 1.9f;
        agent.speed = 1.5f;
        agent.angularSpeed = 360f;
        agent.acceleration = 14f;
        agent.stoppingDistance = 0.3f;
        agent.autoBraking = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
        model.name = "MonsterModel";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        SetLayer(model.transform, EnemyLayer);
        FaceForwardAndScale(model, 1.03f);

        var animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // eye point on the head bone, at the centre of the eye mesh
        Transform head = FindDeep(model.transform, "Head");
        Transform eyesMesh = FindDeep(model.transform, "MonsterEyes");
        Renderer eyeRenderer = eyesMesh != null ? eyesMesh.GetComponent<Renderer>() : null;
        Vector3 eyeWorld = head != null ? head.position + root.transform.forward * 0.2f + Vector3.up * 0.12f : root.transform.position + Vector3.up * 1.9f;
        if (eyeRenderer is SkinnedMeshRenderer smr && smr.sharedMesh != null)
            eyeWorld = smr.transform.TransformPoint(smr.sharedMesh.bounds.center);

        var eyePoint = new GameObject("EyePoint").transform;
        eyePoint.SetParent(head != null ? head : root.transform, true);
        eyePoint.position = eyeWorld;
        var lightGO = new GameObject("EyeGlow");
        lightGO.transform.SetParent(eyePoint, false);
        lightGO.transform.position = eyeWorld + root.transform.forward * 0.12f;
        var eyeLight = lightGO.AddComponent<Light>();
        eyeLight.type = LightType.Point;
        eyeLight.color = new Color(1f, 0.08f, 0.04f);
        eyeLight.intensity = 0.15f;
        eyeLight.range = 4.5f;
        eyeLight.shadows = LightShadows.None;

        var ai = root.AddComponent<MonsterAI>();
        var so = new SerializedObject(ai);
        so.FindProperty("animator").objectReferenceValue = animator;
        so.FindProperty("eyeRenderer").objectReferenceValue = eyeRenderer;
        so.FindProperty("eyeLight").objectReferenceValue = eyeLight;
        so.FindProperty("eyePoint").objectReferenceValue = eyePoint;
        var wp = so.FindProperty("waypoints");
        wp.arraySize = waypoints.Length;
        for (int i = 0; i < waypoints.Length; i++) wp.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
        so.FindProperty("chaseMusic").objectReferenceValue = Clip("monster_chase_music.wav");
        so.FindProperty("spottedSting").objectReferenceValue = Clip("monster_spotted.wav");
        so.FindProperty("jumpscareScream").objectReferenceValue = Clip("monster_scream.wav");
        so.FindProperty("footstep").objectReferenceValue = Clip("monster_step.wav");
        so.FindProperty("growl").objectReferenceValue = Clip("monster_growl.wav");
        so.FindProperty("dragStep").objectReferenceValue = Clip("monster_drag.wav");
        so.FindProperty("boneCrack").objectReferenceValue = Clip("monster_crack.wav");
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static AudioClip Clip(string file)
    {
        var c = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/{file}");
        if (c == null) Debug.LogWarning($"MonsterSetup: missing sound {AudioFolder}/{file}");
        return c;
    }

    private static void FaceForwardAndScale(GameObject model, float expectedHips)
    {
        Transform left = FindDeep(model.transform, "LeftUpperLeg");
        Transform right = FindDeep(model.transform, "RightUpperLeg");
        Transform hips = FindDeep(model.transform, "Hips");
        if (left == null || right == null || hips == null) return;
        Transform parent = model.transform.parent;
        Vector3 r = parent.InverseTransformDirection(right.position - left.position);
        r.y = 0f;
        Vector3 facing = Vector3.Cross(r.normalized, Vector3.up);
        if (facing.sqrMagnitude > 0.001f)
        {
            float yaw = Vector3.SignedAngle(facing, Vector3.forward, Vector3.up);
            model.transform.localRotation = Quaternion.AngleAxis(yaw, Vector3.up) * model.transform.localRotation;
        }
        float h = parent.InverseTransformPoint(hips.position).y;
        if (h > 0.01f && Mathf.Abs(h - expectedHips) > 0.08f)
            model.transform.localScale *= expectedHips / h;
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
        foreach (Transform c in t) SetLayer(c, layer);
    }
}
