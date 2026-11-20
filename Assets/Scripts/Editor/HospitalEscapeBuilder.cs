using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Builds the "HospitalEscape" scene: a dark hospital made from the project's models,
// where the kid wakes up in a ward, finds a key and escapes through the front door.
//
// It runs by itself once (when the scene doesn't exist yet) without closing the scene you have open.
// Tools > Hospital Escape > Rebuild Level  rebuilds it (overwrites HospitalEscape.unity only).
// Tools > Hospital Escape > Open Level     opens it.
//
// Layout (top view, metres; north = +Z):
//
//   z=21 +---------+-------+---------+-----------+
//        | Ward A  |Storage| Lounge  |  Office   |
//   z=13 +--D------+--D----+---D-----+----D------+
//        |               Corridor                |
//   z=10 +---D-------+---[open]---+------D-------+
//        |  Ward B   |   Lobby    |  Pharmacy    |
//   z=3  +-----------+            +--------------+
//                    |   [EXIT]   |
//   z=0              +------------+
//      x=-18       x=-6          x=6            x=18
[InitializeOnLoad]
public static class HospitalEscapeBuilder
{
    private const string ScenePath = "Assets/Scenes/HospitalEscape.unity";
    private const string PlayerPrefabPath = "Assets/Prefab/ChildPlayer.prefab";
    private const string MaterialFolder = "Assets/Materials/Escape";
    private const string VolumeProfilePath = "Assets/Settings/HospitalEscapeVolume.asset";

    private const float WallHeight = 3f;
    private const float WallThickness = 0.2f;
    private const float DoorWidth = 1.4f;
    private const float DoorHeight = 2.3f;

    private static Transform structure, props, lighting, decor, gameplay;
    private static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    private static readonly List<Transform> keySpawns = new List<Transform>();

    static HospitalEscapeBuilder()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(ScenePath) && AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) != null)
                Build();
        };
    }

    [MenuItem("Tools/Hospital Escape/Rebuild Level")]
    private static void RebuildMenu()
    {
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Rebuild Hospital Escape",
                "This rebuilds HospitalEscape.unity from scratch. Any changes you made by hand in that scene will be lost. Other scenes are not touched.",
                "Rebuild", "Cancel"))
            return;
        Build();
    }

    [MenuItem("Tools/Hospital Escape/Open Level")]
    private static void OpenMenu()
    {
        if (!File.Exists(ScenePath)) Build();
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    // ------------------------------------------------------------------ build
    public static void Build()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasOpen = scene.IsValid() && scene.isLoaded;
        Scene previousActive = SceneManager.GetActiveScene();

        if (wasOpen)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                Object.DestroyImmediate(root);
        }
        else
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        }

        SceneManager.SetActiveScene(scene);
        try
        {
            mats.Clear();
            keySpawns.Clear();
            BuildContents();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        finally
        {
            if (!wasOpen)
            {
                if (previousActive.IsValid()) SceneManager.SetActiveScene(previousActive);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        AddToBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("HospitalEscapeBuilder: built " + ScenePath + ". Open it with Tools > Hospital Escape > Open Level.");
    }

    private static void BuildContents()
    {
        CreateMaterials();
        SetUpEnvironment();

        structure = new GameObject("Structure").transform;
        props = new GameObject("Props").transform;
        lighting = new GameObject("Lighting").transform;
        decor = new GameObject("Decor").transform;
        gameplay = new GameObject("Gameplay").transform;

        BuildRooms();
        BuildProps();
        BuildLights();
        BuildDecor();
        BuildGameplay();
        BuildPostProcessing();
        MonsterSetup.AddToActiveScene();
    }

    // ------------------------------------------------------------------ materials & environment
    private static void CreateMaterials()
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/Materials", "Escape");

        Mat("Wall", new Color(0.42f, 0.45f, 0.40f), 0.15f);
        Mat("WallLower", new Color(0.24f, 0.30f, 0.29f), 0.25f);
        Mat("Floor", new Color(0.20f, 0.21f, 0.20f), 0.35f);
        Mat("FloorWard", new Color(0.27f, 0.26f, 0.23f), 0.25f);
        Mat("Ceiling", new Color(0.30f, 0.30f, 0.28f), 0.05f);
        Mat("Door", new Color(0.22f, 0.24f, 0.26f), 0.4f, 0.6f);
        Mat("DoorGlass", new Color(0.05f, 0.07f, 0.10f), 0.9f, 0f, new Color(0.02f, 0.03f, 0.06f));
        Mat("Counter", new Color(0.18f, 0.15f, 0.12f), 0.3f);
        Mat("Blood", new Color(0.18f, 0.01f, 0.01f), 0.6f);
        Mat("LampOn", new Color(0.9f, 1f, 0.9f), 0.5f, 0f, new Color(1.6f, 1.8f, 1.6f));
        Mat("LampOff", new Color(0.25f, 0.25f, 0.25f), 0.3f);
        Mat("ExitRed", new Color(0.6f, 0.02f, 0.02f), 0.5f, 0f, new Color(3f, 0.1f, 0.1f));
        Mat("KeyGold", new Color(0.85f, 0.65f, 0.2f), 0.85f, 1f, new Color(0.35f, 0.25f, 0.05f));
        Mat("Metal", new Color(0.35f, 0.35f, 0.37f), 0.6f, 1f);
    }

    private static Material Mat(string name, Color color, float smoothness, float metallic = 0f, Color? emission = null)
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
        m.SetFloat("_Metallic", metallic);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(m);
        mats[name] = m;
        return m;
    }

    private static void SetUpEnvironment()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.018f, 0.02f, 0.026f);
        RenderSettings.reflectionIntensity = 0.15f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.07f;
        RenderSettings.fogColor = new Color(0.008f, 0.01f, 0.012f);
        RenderSettings.sun = null;
    }

    // ------------------------------------------------------------------ rooms
    private struct Opening
    {
        public float centre, width, height;
        public Opening(float c, float w = DoorWidth, float h = DoorHeight) { centre = c; width = w; height = h; }
    }

    private static void BuildRooms()
    {
        // floors
        Floor("Floor_Lobby", -6, 6, 0, 10, "Floor");
        Floor("Floor_Corridor", -18, 18, 10, 13, "Floor");
        Floor("Floor_WardA", -18, -9, 13, 21, "FloorWard");
        Floor("Floor_Storage", -9, -3, 13, 21, "Floor");
        Floor("Floor_Lounge", -3, 6, 13, 21, "FloorWard");
        Floor("Floor_Office", 6, 18, 13, 21, "FloorWard");
        Floor("Floor_WardB", -18, -6, 3, 10, "FloorWard");
        Floor("Floor_Pharmacy", 6, 18, 3, 10, "Floor");
        // ceiling
        Box("Ceiling_Main", new Vector3(0, WallHeight + 0.05f, 12), new Vector3(36.4f, 0.1f, 18.4f), "Ceiling", structure);
        Box("Ceiling_Lobby", new Vector3(0, WallHeight + 0.05f, 1.5f), new Vector3(12.4f, 0.1f, 3.4f), "Ceiling", structure);

        // walls running east-west (along X) at a fixed Z
        WallX("Wall_North", 21, -18, 18);
        WallX("Wall_CorridorNorth", 13, -18, 18, new Opening(-13.5f), new Opening(-6f), new Opening(1.5f), new Opening(12f));
        WallX("Wall_CorridorSouth", 10, -18, 18, new Opening(-12f), new Opening(0f, 3f, 2.6f), new Opening(12f));
        WallX("Wall_WardB_South", 3, -18, -6);
        WallX("Wall_Pharmacy_South", 3, 6, 18);
        WallX("Wall_Lobby_South", 0, -6, 6, new Opening(0f, 2f, 2.4f));
        // walls running north-south (along Z) at a fixed X
        WallZ("Wall_West", -18, 3, 21);
        WallZ("Wall_East", 18, 3, 21);
        WallZ("Wall_WardA_Storage", -9, 13, 21);
        WallZ("Wall_Storage_Lounge", -3, 13, 21);
        WallZ("Wall_Lounge_Office", 6, 13, 21);
        WallZ("Wall_Lobby_West", -6, 0, 10);
        WallZ("Wall_Lobby_East", 6, 0, 10);
    }

    private static void Floor(string name, float x0, float x1, float z0, float z1, string mat)
    {
        Box(name, new Vector3((x0 + x1) / 2, -0.05f, (z0 + z1) / 2), new Vector3(x1 - x0, 0.1f, z1 - z0), mat, structure);
    }

    private static void WallX(string name, float z, float x0, float x1, params Opening[] openings)
    {
        var parent = new GameObject(name).transform;
        parent.SetParent(structure);
        foreach (var (a, b, y0, y1) in Segments(x0, x1, openings))
        {
            Box("Segment", new Vector3((a + b) / 2, (y0 + y1) / 2, z), new Vector3(b - a, y1 - y0, WallThickness), "Wall", parent);
            if (y0 < 0.01f) // darker lower band, like real hospital walls
                Box("Trim", new Vector3((a + b) / 2, 0.45f, z), new Vector3(b - a, 0.9f, WallThickness + 0.02f), "WallLower", parent, collider: false);
        }
    }

    private static void WallZ(string name, float x, float z0, float z1, params Opening[] openings)
    {
        var parent = new GameObject(name).transform;
        parent.SetParent(structure);
        foreach (var (a, b, y0, y1) in Segments(z0, z1, openings))
        {
            Box("Segment", new Vector3(x, (y0 + y1) / 2, (a + b) / 2), new Vector3(WallThickness, y1 - y0, b - a), "Wall", parent);
            if (y0 < 0.01f)
                Box("Trim", new Vector3(x, 0.45f, (a + b) / 2), new Vector3(WallThickness + 0.02f, 0.9f, b - a), "WallLower", parent, collider: false);
        }
    }

    // Splits a wall into solid pieces around door openings (plus the piece above each door).
    private static IEnumerable<(float, float, float, float)> Segments(float start, float end, Opening[] openings)
    {
        float cursor = start - WallThickness / 2;
        float stop = end + WallThickness / 2;
        foreach (var o in openings.OrderBy(o => o.centre))
        {
            float a = o.centre - o.width / 2, b = o.centre + o.width / 2;
            if (a > cursor) yield return (cursor, a, 0f, WallHeight);
            yield return (a, b, o.height, WallHeight);
            cursor = b;
        }
        if (stop > cursor) yield return (cursor, stop, 0f, WallHeight);
    }

    private static GameObject Box(string name, Vector3 centre, Vector3 size, string mat, Transform parent, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = centre;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mats[mat];
        if (!collider)
        {
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.layer = 2; // Ignore Raycast: decoration, AutoLevelColliders skips it
        }
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        return go;
    }

    // ------------------------------------------------------------------ props
    // Places a model from Assets/Models, scaled so its height (or its longest side) is "size" metres,
    // standing on y = baseY with its centre at (x, z).
    private static GameObject Prop(string file, float x, float z, float yaw, float size, bool byHeight = true, float baseY = 0f, string label = null, float tilt = 0f)
    {
        string path = $"Assets/Models/{file}.glb";
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            Debug.LogWarning($"HospitalEscapeBuilder: model not found: {path}");
            return null;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, props);
        go.name = label ?? file;
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.Euler(tilt, yaw, 0f) * asset.transform.rotation;

        Bounds b = WorldBounds(go);
        if (b.size == Vector3.zero) return go;
        float current = byHeight ? b.size.y : Mathf.Max(b.size.x, b.size.z);
        if (current > 0.0001f) go.transform.localScale = asset.transform.localScale * (size / current);

        b = WorldBounds(go);
        go.transform.position += new Vector3(x - b.center.x, baseY - b.min.y, z - b.center.z);
        return go;
    }

    private static Bounds WorldBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    private static void KeySpawnOnTop(GameObject prop, string label, float fallbackX, float fallbackZ)
    {
        if (prop != null)
        {
            Bounds b = WorldBounds(prop);
            AddKeySpawn(label, new Vector3(b.center.x, b.max.y + 0.06f, b.center.z));
        }
        else AddKeySpawn(label, new Vector3(fallbackX, 0.08f, fallbackZ));
    }

    private static void AddKeySpawn(string label, Vector3 pos)
    {
        var t = new GameObject("KeySpawn_" + label).transform;
        t.position = pos;
        keySpawns.Add(t);
    }

    private static void BuildProps()
    {
        // ---- Ward A (where the kid wakes up)
        Prop("bed_curtain_and_vital_signs_monitor", -15.6f, 18.6f, 90, 2.3f, label: "Bed_WakeUp");
        Prop("bed_curtain_and_vital_signs_monitor", -11.4f, 18.6f, 90, 2.3f, label: "Bed_Empty");
        Prop("drawers", -17.4f, 14.2f, 90, 0.9f);
        Prop("medical_cart", -10.0f, 14.6f, -60, 1.0f, label: "MedicalCart_Pushed");
        Prop("clock", -13.5f, 20.8f, 180, 0.4f, byHeight: false, baseY: 2.2f);

        // ---- Storage (dark, cluttered)
        Prop("hospital_medical_treatment_cabinet", -8.3f, 20.3f, 180, 1.9f);
        Prop("hospital_medical_treatment_cabinet", -3.7f, 20.3f, 180, 1.9f);
        Prop("medical_supplies_collection", -6.0f, 17.6f, 20, 1.6f, byHeight: false);
        Prop("medical_props", -8.0f, 15.0f, 70, 1.2f, byHeight: false);
        Prop("the_medical_bag", -4.3f, 14.4f, 35, 0.45f, byHeight: false);
        AddKeySpawn("Storage", new Vector3(-5.2f, 0.08f, 19.6f));

        // ---- Staff lounge
        GameObject table = Prop("school_cafeteria_table", 1.0f, 17.6f, 0, 2.0f, byHeight: false);
        Prop("the_vending_machine", -2.4f, 20.2f, 180, 1.9f);
        Prop("simple_vendine_machine", 5.2f, 20.2f, 180, 1.9f);
        Prop("coffee_cart", 4.8f, 14.6f, -90, 1.0f);
        Prop("low_poly_plants_pack", -2.2f, 14.2f, 0, 1.2f, byHeight: false);
        KeySpawnOnTop(table, "LoungeTable", 1.0f, 17.6f);

        // ---- Doctor's office
        GameObject desk = Prop("doctors_drawer", 12f, 18.8f, 180, 1.0f, label: "DoctorsDesk");
        Prop("old_leather_office_chair", 12f, 17.3f, 0, 1.1f);
        Prop("medicine_wall_cupboard_hospital", 17.6f, 16.5f, -90, 0.8f, baseY: 1.3f);
        Prop("drawers", 7.2f, 20.2f, 180, 1.0f);
        Prop("old_leather_office_chair", 8.5f, 14.4f, 140, 1.1f, label: "Chair_Knocked", tilt: 80f);
        KeySpawnOnTop(desk, "OfficeDesk", 12f, 18.8f);

        // ---- Ward B
        Prop("bed_curtain_and_vital_signs_monitor", -15.6f, 5.0f, 90, 2.3f);
        Prop("bed_curtain_and_vital_signs_monitor", -10.5f, 5.0f, 90, 2.3f);
        Prop("drawers", -17.4f, 8.6f, 90, 0.9f);
        AddKeySpawn("WardB_Floor", new Vector3(-13.0f, 0.08f, 4.0f));

        // ---- Pharmacy
        Prop("hospital_medical_treatment_cabinet", 8.0f, 3.7f, 0, 1.9f);
        Prop("hospital_medical_treatment_cabinet", 10.2f, 3.7f, 0, 1.9f);
        Prop("medicine_wall_cupboard_hospital", 17.6f, 6.5f, -90, 0.8f, baseY: 1.3f);
        GameObject pharmCart = Prop("medical_cart", 14.5f, 6.0f, 25, 1.0f);
        KeySpawnOnTop(pharmCart, "PharmacyCart", 14.5f, 6.0f);

        // ---- Corridor
        Prop("medical_cart", -4.0f, 11.0f, 0, 1.0f, label: "MedicalCart_Overturned", tilt: 85f);
        Prop("hospital_hanging_signage", -8f, 11.5f, 0, 1.4f, byHeight: false, baseY: 2.35f);
        Prop("hospital_hanging_signage", 8f, 11.5f, 0, 1.4f, byHeight: false, baseY: 2.35f);
        Prop("hospital_direction_sign", 16.8f, 11.5f, -90, 1.9f);
        Prop("vents", -17.8f, 11.5f, 90, 0.8f, byHeight: false, baseY: 2.2f);
        Prop("clock", 0f, 12.85f, 180, 0.4f, byHeight: false, baseY: 2.3f);

        // ---- Lobby / reception
        Box("Reception_Counter", new Vector3(-3.6f, 0.55f, 6.2f), new Vector3(3.2f, 1.1f, 0.6f), "Counter", props);
        Box("Reception_CounterSide", new Vector3(-5.0f, 0.55f, 4.9f), new Vector3(0.6f, 1.1f, 2.0f), "Counter", props);
        Prop("old_leather_office_chair", -3.6f, 7.2f, 180, 1.1f);
        Prop("simple_vendine_machine", 5.4f, 7.5f, -90, 1.9f);
        Prop("low_poly_plants_pack", 4.8f, 1.2f, 0, 1.2f, byHeight: false);
        Prop("low_poly_plants_pack", -4.8f, 1.2f, 0, 1.2f, byHeight: false);
        Prop("clock", -3.6f, 9.85f, 180, 0.45f, byHeight: false, baseY: 2.2f);
    }

    // ------------------------------------------------------------------ lights
    private static void BuildLights()
    {
        AudioClip buzz = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/fluorescent-light-buzz.mp3");

        CeilingLight("WardA", -13.5f, 17f, 1.1f, flicker: true, buzz);
        CeilingLight("Lounge", 1.5f, 17f, 0.9f, flicker: true, buzz);
        CeilingLight("Office", 12f, 17f, 0.7f, flicker: false, null);
        CeilingLight("WardB", -12f, 6.5f, 0.8f, flicker: false, null);
        CeilingLight("Pharmacy", 12f, 6.5f, 0.9f, flicker: true, buzz);
        CeilingLight("Corridor_W", -14f, 11.5f, 1.0f, flicker: false, null);
        CeilingLight("Corridor_MidW", -5f, 11.5f, 0f, flicker: false, null, broken: true);
        CeilingLight("Corridor_MidE", 5f, 11.5f, 1.1f, flicker: true, buzz);
        CeilingLight("Corridor_E", 14f, 11.5f, 0f, flicker: false, null, broken: true);
        CeilingLight("Lobby", 0f, 5.5f, 1.0f, flicker: true, buzz);
        CeilingLight("Storage", -6f, 17f, 0f, flicker: false, null, broken: true); // pitch black: flashlight only

        // red emergency light over the exit
        var red = NewLight("ExitLight", new Vector3(0f, 2.55f, 0.8f), LightType.Point, new Color(1f, 0.12f, 0.08f), 1.6f, 6f);
        red.transform.SetParent(lighting);
    }

    private static void CeilingLight(string name, float x, float z, float intensity, bool flicker, AudioClip buzz, bool broken = false)
    {
        var fixture = Box("Lamp_" + name, new Vector3(x, WallHeight - 0.03f, z), new Vector3(1.2f, 0.06f, 0.3f),
            broken ? "LampOff" : "LampOn", lighting, collider: false);
        if (broken) return;

        Light l = NewLight("Light_" + name, new Vector3(x, WallHeight - 0.25f, z), LightType.Point,
            new Color(0.82f, 0.95f, 0.85f), intensity, 7f);
        l.transform.SetParent(lighting, true);

        if (flicker)
        {
            var f = l.gameObject.AddComponent<FlickeringLight>();
            f.targetLight = l;
            f.minimumIntensity = 0f;
            f.maximumIntensity = intensity * 1.2f;
            f.minimumDelay = 0.05f;
            f.maximumDelay = 0.6f;
        }
        if (buzz != null)
        {
            var a = l.gameObject.AddComponent<AudioSource>();
            a.clip = buzz;
            a.loop = true;
            a.playOnAwake = true;
            a.volume = 0.12f;
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = 1f;
            a.maxDistance = 7f;
        }
    }

    private static Light NewLight(string name, Vector3 pos, LightType type, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = type;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    // ------------------------------------------------------------------ creepy details
    private static void BuildDecor()
    {
        // blood trail from Ward A down the corridor
        Stain(-13.6f, 13.6f, 0.5f, 0.35f, 20);
        Stain(-12.6f, 12.4f, 0.4f, 0.3f, 50);
        Stain(-10.8f, 11.8f, 0.6f, 0.3f, 80);
        Stain(-8.2f, 11.4f, 0.5f, 0.25f, 95);
        Stain(-6.5f, 11.3f, 0.35f, 0.25f, 100);
        Stain(-2.0f, 11.6f, 1.1f, 0.8f, 10);
        Stain(10.6f, 16.0f, 0.9f, 0.7f, 40);
        Stain(-14.0f, 6.8f, 0.7f, 0.5f, 120);
        Stain(1.2f, 3.0f, 0.5f, 0.4f, 60);

        Color signColour = new Color(0.7f, 0.7f, 0.65f);
        WallText("Writing_Corridor", "HE IS STILL HERE", new Vector3(-1.5f, 1.6f, 12.88f), 180f, 0.32f);
        WallText("Writing_WardA", "DON'T LOOK BACK", new Vector3(-17.88f, 1.5f, 17f), 90f, 0.28f);
        WallText("Writing_Office", "the key... he hides it", new Vector3(17.88f, 1.4f, 19.5f), -90f, 0.14f);
        WallText("Sign_Storage", "STORAGE", new Vector3(-6f, 2.55f, 12.88f), 180f, 0.16f, signColour);
        WallText("Sign_Office", "DR. OFFICE", new Vector3(12f, 2.55f, 12.88f), 180f, 0.16f, signColour);
        WallText("Sign_Pharmacy", "PHARMACY", new Vector3(12f, 2.55f, 10.12f), 0f, 0.16f, signColour);
        WallText("Sign_WardB", "WARD B", new Vector3(-12f, 2.55f, 10.12f), 0f, 0.16f, signColour);
        WallText("Sign_WardA", "WARD A", new Vector3(-13.5f, 2.55f, 12.88f), 180f, 0.16f, signColour);
        WallText("Sign_Lounge", "STAFF ONLY", new Vector3(1.5f, 2.55f, 12.88f), 180f, 0.16f, signColour);
    }

    private static void Stain(float x, float z, float sx, float sz, float yaw)
    {
        var s = Box("BloodStain", new Vector3(x, 0.006f, z), new Vector3(sx, 0.01f, sz), "Blood", decor, collider: false);
        s.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // Text on a wall. "facing" is the yaw the text faces (towards the room it's read from); "size" is the letter height in metres.
    private static void WallText(string name, string text, Vector3 pos, float facing, float size, Color? colour = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(decor);
        go.transform.position = pos;
        // TextMeshPro reads along its +Z facing away, so turn it to face the reader
        go.transform.rotation = Quaternion.Euler(0f, facing + 180f, 0f);
        go.layer = 2;
        var t = go.AddComponent<TextMeshPro>();
        t.text = text;
        t.fontSize = size * 10f; // in 3D TextMeshPro, font size 1 is about 0.1 m tall
        t.alignment = TextAlignmentOptions.Center;
        t.color = colour ?? new Color(0.45f, 0.02f, 0.02f, 0.95f);
        t.rectTransform.sizeDelta = new Vector2(text.Length * size * 0.85f, size * 1.6f);
        t.textWrappingMode = TextWrappingModes.NoWrap;
    }

    // ------------------------------------------------------------------ key, exit, player, manager
    private static void BuildGameplay()
    {
        // ---- key
        var keyRoot = new GameObject("Key");
        keyRoot.transform.SetParent(gameplay);
        var visual = new GameObject("KeyVisual").transform;
        visual.SetParent(keyRoot.transform, false);
        var bow = Part(PrimitiveType.Cylinder, visual, new Vector3(0f, 0.1f, 0f), new Vector3(0.07f, 0.008f, 0.07f), "KeyGold");
        bow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Part(PrimitiveType.Cube, visual, new Vector3(0f, 0.0f, 0f), new Vector3(0.018f, 0.14f, 0.012f), "KeyGold");
        Part(PrimitiveType.Cube, visual, new Vector3(0.022f, -0.05f, 0f), new Vector3(0.03f, 0.014f, 0.012f), "KeyGold");
        Part(PrimitiveType.Cube, visual, new Vector3(0.018f, -0.02f, 0f), new Vector3(0.022f, 0.012f, 0.012f), "KeyGold");
        visual.localPosition = new Vector3(0f, 0.08f, 0f);
        var glow = NewLight("KeyGlow", Vector3.zero, LightType.Point, new Color(1f, 0.75f, 0.3f), 0.8f, 1.8f);
        glow.transform.SetParent(keyRoot.transform, false);
        glow.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        var keyPickup = keyRoot.AddComponent<KeyPickup>();
        SetField(keyPickup, "visual", visual);

        var spawnParent = new GameObject("KeySpawnPoints").transform;
        spawnParent.SetParent(gameplay);
        foreach (var s in keySpawns) s.SetParent(spawnParent, true);
        if (keySpawns.Count > 0) keyRoot.transform.position = keySpawns[0].position;

        // ---- front door (in the lobby's south wall, x -1..1)
        var door = new GameObject("FrontDoor");
        door.transform.SetParent(gameplay);
        door.transform.position = new Vector3(0f, 0f, 0f);
        var leftHinge = new GameObject("LeftHinge").transform;
        leftHinge.SetParent(door.transform, false);
        leftHinge.localPosition = new Vector3(-1f, 0f, 0f);
        var rightHinge = new GameObject("RightHinge").transform;
        rightHinge.SetParent(door.transform, false);
        rightHinge.localPosition = new Vector3(1f, 0f, 0f);
        DoorPanel(leftHinge, 1f);
        DoorPanel(rightHinge, -1f);
        var padlock = Part(PrimitiveType.Cube, door.transform, new Vector3(0f, 1.1f, 0.09f), new Vector3(0.12f, 0.14f, 0.06f), "Metal");
        padlock.name = "Padlock";
        Part(PrimitiveType.Cube, door.transform, new Vector3(0f, 1.1f, 0.075f), new Vector3(0.6f, 0.03f, 0.02f), "Metal").name = "Chain";

        // EXIT sign
        Box("ExitSign", new Vector3(0f, 2.62f, 0.14f), new Vector3(0.7f, 0.25f, 0.06f), "ExitRed", door.transform, collider: false);
        var exitText = new GameObject("ExitText");
        exitText.transform.SetParent(door.transform);
        exitText.transform.position = new Vector3(0f, 2.62f, 0.18f);
        exitText.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        exitText.layer = 2;
        var et = exitText.AddComponent<TextMeshPro>();
        et.text = "EXIT";
        et.fontSize = 2.2f;
        et.alignment = TextAlignmentOptions.Center;
        et.color = Color.white;
        et.rectTransform.sizeDelta = new Vector2(0.7f, 0.25f);

        var exit = new GameObject("ExitDoorTrigger");
        exit.transform.SetParent(door.transform, false);
        exit.transform.localPosition = new Vector3(0f, 0f, 0.8f);
        var exitDoor = exit.AddComponent<ExitDoor>();
        SetField(exitDoor, "leftHinge", leftHinge);
        SetField(exitDoor, "rightHinge", rightHinge);
        SetField(exitDoor, "lockVisual", padlock);

        // ---- player: wakes up next to the bed in Ward A, facing the door
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (playerPrefab != null)
        {
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.name = "ChildPlayer";
            player.transform.position = new Vector3(-13.5f, 0.05f, 17.0f);
            player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }
        else Debug.LogWarning("HospitalEscapeBuilder: ChildPlayer prefab not found.");

        // ---- game manager
        var gmGO = new GameObject("EscapeGameManager");
        gmGO.transform.SetParent(gameplay);
        var gm = gmGO.AddComponent<EscapeGameManager>();
        SetField(gm, "key", keyPickup);
        SetArray(gm, "keySpawnPoints", keySpawns.Cast<Object>().ToArray());
        SetField(gm, "ambience", Clip("background.wav"));
        SetField(gm, "keyPickupSound", Clip("ItemPickup.mp3"));
        SetField(gm, "doorLockedSound", Clip("button-click.mp3"));
        SetField(gm, "doorOpenSound", Clip("creaking-door.mp3"));
        SetField(gm, "footstepSound", Clip("Footstep.mp3"));
        SetArray(gm, "scarySounds", new Object[] { Clip("creaking-door.mp3"), Clip("heavy_breathing.mp3"), Clip("single-heart-beat.wav"), Clip("Footstep.mp3") }.Where(c => c != null).ToArray());
    }

    private static void DoorPanel(Transform hinge, float dir)
    {
        var panel = Part(PrimitiveType.Cube, hinge, new Vector3(0.5f * dir, 1.2f, 0f), new Vector3(0.98f, 2.38f, 0.08f), "Door", keepCollider: true);
        panel.name = "Panel";
        var glass = Part(PrimitiveType.Cube, hinge, new Vector3(0.5f * dir, 1.65f, 0f), new Vector3(0.5f, 0.7f, 0.09f), "DoorGlass");
        glass.name = "Window";
        var bar = Part(PrimitiveType.Cube, hinge, new Vector3(0.7f * dir, 1.0f, 0.07f), new Vector3(0.45f, 0.05f, 0.05f), "Metal");
        bar.name = "PushBar";
    }

    private static GameObject Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, string mat, bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.GetComponent<Renderer>().sharedMaterial = mats[mat];
        if (!keepCollider)
        {
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.layer = 2;
        }
        return go;
    }

    private static AudioClip Clip(string file) => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/" + file);

    private static void SetField(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning($"HospitalEscapeBuilder: no field '{field}' on {target.GetType().Name}"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning($"HospitalEscapeBuilder: no field '{field}' on {target.GetType().Name}"); return; }
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ post-processing
    private static void BuildPostProcessing()
    {
        AssetDatabase.DeleteAsset(VolumeProfilePath); // our own generated profile only
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, VolumeProfilePath);

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.48f);
        vignette.smoothness.Override(0.55f);

        var grain = profile.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Medium3);
        grain.intensity.Override(0.5f);

        var colour = profile.Add<ColorAdjustments>(true);
        colour.saturation.Override(-45f);
        colour.contrast.Override(18f);
        colour.postExposure.Override(0.3f);
        colour.colorFilter.Override(new Color(0.86f, 0.95f, 0.9f));

        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(0.9f);
        bloom.threshold.Override(0.9f);

        var chroma = profile.Add<ChromaticAberration>(true);
        chroma.intensity.Override(0.25f);

        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);

        foreach (var c in profile.components)
            AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        var go = new GameObject("Global Volume (Horror)");
        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10;
        volume.sharedProfile = profile;
    }

    // ------------------------------------------------------------------ build settings
    private static void AddToBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == ScenePath)) return;
        // Index 1 is what the main menu's Play button loads
        int index = Mathf.Min(1, scenes.Count);
        scenes.Insert(index, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
