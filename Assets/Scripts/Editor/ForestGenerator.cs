using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Genera un bosque procedural (con semilla fija) alrededor de la oficina en la escena Oficina.
/// Menú: Tools > Bosque > Generar bosque exterior.
/// Volver a ejecutarlo reemplaza el bosque anterior.
/// </summary>
public static class ForestGenerator
{
    const string ScenePath = "Assets/Scenes/Oficina.unity";
    const string ModelsFolder = "Assets/Objects/Trees/";
    const string GroundMaterialPath = "Assets/Objects/Trees/SueloBosque.mat";
    const string RootName = "Bosque exterior";
    const int Seed = 1337;

    // Límites del área del bosque y del claro que rodea la oficina (16 x 14 m centrada en el origen).
    const float ForestHalfSize = 45f;
    const float ClearingHalfX = 10.5f;
    const float ClearingHalfZ = 9.5f;

    enum Fit { Height, Width }

    struct Entry
    {
        public string file; public float min, max; public Fit fit; public bool collider;
        public Entry(string f, float mn, float mx, Fit ft = Fit.Height, bool col = false)
        { file = f; min = mn; max = mx; fit = ft; collider = col; }
    }

    static readonly Entry[] Trees =
    {
        new Entry("TREE_01_OakLarge", 11f, 14f, Fit.Height, true),
        new Entry("TREE_02_OakMedium", 8f, 10f, Fit.Height, true),
        new Entry("TREE_03_OakSmall", 5f, 7f, Fit.Height, true),
        new Entry("TREE_04_PineLarge", 14f, 18f, Fit.Height, true),
        new Entry("TREE_04_PineLarge", 14f, 18f, Fit.Height, true),
        new Entry("TREE_05_PineMedium", 10f, 13f, Fit.Height, true),
        new Entry("TREE_05_PineMedium", 10f, 13f, Fit.Height, true),
        new Entry("TREE_06_PineSmall", 6f, 8f, Fit.Height, true),
        new Entry("TREE_07_Birch", 9f, 12f, Fit.Height, true),
        new Entry("TREE_08_WoodlandThin", 8f, 11f, Fit.Height, true),
        new Entry("TREE_09_WideCanopy", 10f, 12f, Fit.Height, true),
        new Entry("TREE_10_Sapling", 2f, 3.5f, Fit.Height, true),
        new Entry("TREE_11_TwistedOld", 7f, 10f, Fit.Height, true),
        new Entry("TREE_12_DeadForked", 7f, 10f, Fit.Height, true),
        new Entry("TREE_12_DeadForked", 7f, 10f, Fit.Height, true),
        new Entry("TREE_13_DeadTwisted", 6f, 9f, Fit.Height, true),
        new Entry("TREE_13_DeadTwisted", 6f, 9f, Fit.Height, true),
    };

    static readonly Entry[] Deadwood =
    {
        new Entry("TREE_14_Stump", 0.9f, 1.4f, Fit.Width, true),
        new Entry("TREE_15_StumpBroken", 0.9f, 1.4f, Fit.Width, true),
        new Entry("TREE_16_FallenLog", 4f, 6f, Fit.Width, true),
        new Entry("TREE_17_FallenLogMossy", 4f, 6f, Fit.Width, true),
        new Entry("TREE_18_HollowLog", 3f, 4.5f, Fit.Width, true),
        new Entry("TREE_19_RootCluster", 1.5f, 2.5f, Fit.Width),
        new Entry("TREE_20_ExposedRoots", 2f, 3f, Fit.Width),
    };

    static readonly Entry[] Rocks =
    {
        new Entry("ROCK_01_Small", 0.4f, 0.8f, Fit.Width, true),
        new Entry("ROCK_02_Medium", 0.9f, 1.5f, Fit.Width, true),
        new Entry("ROCK_03_BoulderLarge", 2f, 3f, Fit.Width, true),
        new Entry("ROCK_04_BoulderTall", 1.8f, 2.6f, Fit.Height, true),
        new Entry("ROCK_05_BoulderFlat", 2f, 3f, Fit.Width, true),
        new Entry("ROCK_06_BoulderMossy", 1.8f, 2.8f, Fit.Width, true),
        new Entry("ROCK_07_ClusterSmall", 1f, 1.8f, Fit.Width),
        new Entry("ROCK_08_ClusterLarge", 2.5f, 3.5f, Fit.Width, true),
    };

    static readonly Entry[] Undergrowth =
    {
        new Entry("PLANT_01_GrassShort", 0.2f, 0.35f),
        new Entry("PLANT_02_GrassMedium", 0.35f, 0.55f),
        new Entry("PLANT_03_GrassTall", 0.6f, 0.9f),
        new Entry("PLANT_04_GrassSparse", 0.3f, 0.5f),
        new Entry("PLANT_05_GrassDense", 0.35f, 0.55f),
        new Entry("PLANT_06_WildTuft", 0.4f, 0.6f),
        new Entry("PLANT_07_FernSmall", 0.4f, 0.6f),
        new Entry("PLANT_08_FernLarge", 0.8f, 1.2f),
        new Entry("PLANT_08_FernLarge", 0.8f, 1.2f),
        new Entry("PLANT_09_WoodlandPlant", 0.5f, 0.8f),
        new Entry("PLANT_10_BroadleafPlant", 0.6f, 0.9f),
        new Entry("PLANT_11_MossPatch", 1f, 1.8f, Fit.Width),
        new Entry("PLANT_12_CloverCover", 0.8f, 1.4f, Fit.Width),
    };

    static readonly Entry[] Mushrooms =
    {
        new Entry("MUSH_01_RedSmall", 0.1f, 0.18f),
        new Entry("MUSH_02_BrownSmall", 0.1f, 0.18f),
        new Entry("MUSH_03_White", 0.1f, 0.2f),
        new Entry("MUSH_04_Tall", 0.2f, 0.3f),
        new Entry("MUSH_05_WideCap", 0.15f, 0.25f),
        new Entry("MUSH_06_Cluster", 0.3f, 0.5f, Fit.Width),
        new Entry("MUSH_07_ForestCluster", 0.4f, 0.6f, Fit.Width),
        new Entry("MUSH_08_ShelfFungus", 0.25f, 0.4f, Fit.Width),
    };

    static readonly Entry[] Details =
    {
        new Entry("DETAIL_01_LeafPile", 0.8f, 1.4f, Fit.Width),
        new Entry("DETAIL_02_FallenBranch", 1.2f, 2f, Fit.Width),
        new Entry("DETAIL_03_TwigCluster", 0.5f, 0.9f, Fit.Width),
        new Entry("DETAIL_04_PineCones", 0.4f, 0.7f, Fit.Width),
        new Entry("DETAIL_05_AcornsSeeds", 0.3f, 0.5f, Fit.Width),
        new Entry("DETAIL_06_Pebbles", 0.5f, 0.9f, Fit.Width),
        new Entry("DETAIL_07_BarkDebris", 0.5f, 0.9f, Fit.Width),
        new Entry("GROUND_02_MudPatch", 2.5f, 4f, Fit.Width),
        new Entry("GROUND_04_ForestFloor", 3f, 5f, Fit.Width),
    };

    static readonly Entry FenceStraight = new Entry("PROP_05_FenceStraight", 2.4f, 2.4f, Fit.Width, true);
    static readonly Entry FenceBroken = new Entry("PROP_07_FenceBroken", 2.4f, 2.4f, Fit.Width, true);
    static readonly Entry FenceCorner = new Entry("PROP_06_FenceCorner", 1.2f, 1.2f, Fit.Width, true);
    static readonly Entry AncientTree = new Entry("HERO_01_AncientTree", 16f, 16f, Fit.Height, true);
    static readonly Entry HollowTree = new Entry("HERO_02_HollowTree", 10f, 10f, Fit.Height, true);
    static readonly Entry MossyLog = new Entry("HERO_05_MossyFallenLog", 6f, 6f, Fit.Width, true);

    static System.Random rng;
    static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();
    static readonly List<Vector3> trunkPositions = new List<Vector3>();
    static readonly List<string> missing = new List<string>();

    [MenuItem("Tools/Bosque/Generar bosque exterior")]
    public static void Generate()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        var old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);

        rng = new System.Random(Seed);
        cache.Clear();
        trunkPositions.Clear();
        missing.Clear();

        var root = new GameObject(RootName);
        Transform Group(string n) { var g = new GameObject(n); g.transform.SetParent(root.transform, false); return g.transform; }

        CreateGround(root.transform);

        // Árboles de referencia visibles desde las ventanas (este y oeste).
        var heroes = Group("Árboles singulares");
        Place(AncientTree, new Vector3(-19f, 0f, -1.5f), 35f, heroes);
        Place(HollowTree, new Vector3(18f, 0f, -3f), 200f, heroes);
        Place(MossyLog, new Vector3(14f, 0f, 3.5f), 70f, heroes);
        trunkPositions.Add(new Vector3(-19f, 0f, -1.5f));
        trunkPositions.Add(new Vector3(18f, 0f, -3f));

        // Árboles: más densos conforme se alejan del claro.
        var trees = Group("Árboles");
        int placed = 0, attempts = 0;
        while (placed < 320 && attempts++ < 20000)
        {
            var p = RandomPoint(1.5f);
            float edge = DistanceToClearing(p);
            if (Rand() > Mathf.Clamp01(0.25f + edge / 12f)) continue;
            if (!FarFromTrunks(p, 2.6f)) continue;
            Place(Pick(Trees), p, Rand() * 360f, trees);
            trunkPositions.Add(p);
            placed++;
        }

        var dead = Group("Troncos y tocones");
        for (int i = 0; i < 55; i++)
        {
            var p = RandomPoint(1f);
            if (FarFromTrunks(p, 2f)) Place(Pick(Deadwood), p, Rand() * 360f, dead);
        }

        var rocks = Group("Rocas");
        for (int i = 0; i < 70; i++)
        {
            var p = RandomPoint(1f);
            if (FarFromTrunks(p, 1.8f)) Place(Pick(Rocks), p, Rand() * 360f, rocks, -0.15f);
        }

        var under = Group("Sotobosque");
        for (int i = 0; i < 900; i++) Place(Pick(Undergrowth), RandomPoint(0f), Rand() * 360f, under);

        // Hongos agrupados al pie de los árboles.
        var mush = Group("Hongos");
        for (int i = 0; i < 160; i++)
        {
            var t = trunkPositions[rng.Next(trunkPositions.Count)];
            float a = Rand() * Mathf.PI * 2f, r = 0.6f + Rand() * 1.2f;
            var p = t + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            if (InClearing(p)) continue;
            Place(Pick(Mushrooms), p, Rand() * 360f, mush);
        }

        var details = Group("Detalles del suelo");
        for (int i = 0; i < 280; i++) Place(Pick(Details), RandomPoint(-1.5f), Rand() * 360f, details, 0.005f);

        BuildFence(Group("Cerca"));

        SetupNight();

        Undo.RegisterCreatedObjectUndo(root, "Generar bosque exterior");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        if (missing.Count > 0)
            Debug.LogWarning("Bosque: no se encontraron estos modelos: " + string.Join(", ", missing));
        Debug.Log($"Bosque exterior generado: {root.GetComponentsInChildren<Transform>().Length - 1} objetos.");
    }

    static readonly Color FogColor = new Color(0.035f, 0.045f, 0.06f);

    [MenuItem("Tools/Bosque/Ambiente nocturno (luna y niebla)")]
    public static void ApplyNightAtmosphere()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        SetupNight();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Ambiente nocturno aplicado: luz de luna y niebla.");
    }

    static void SetupNight()
    {
        // Luz de luna: tenue, azulada y con sombras suaves; entra en diagonal por las ventanas.
        var moonGo = GameObject.Find("Luz de luna");
        if (moonGo == null) moonGo = new GameObject("Luz de luna");
        var moon = moonGo.GetComponent<Light>();
        if (moon == null) moon = moonGo.AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.55f, 0.65f, 0.85f);
        moon.intensity = 0.35f;
        moon.shadows = LightShadows.Soft;
        moon.shadowStrength = 0.85f;
        moonGo.transform.rotation = Quaternion.Euler(32f, -65f, 0f);

        // Niebla densa que oculta el borde del bosque.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.03f;
        RenderSettings.fogColor = FogColor;

        // Ambiente más oscuro y frío; las lámparas de la oficina siguen iluminando el interior.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.16f, 0.18f, 0.22f);

        // El fondo de las cámaras igual a la niebla para que el horizonte se funda.
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FogColor;
            EditorUtility.SetDirty(cam);
        }
    }

    static void CreateGround(Transform parent)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader) { name = "SueloBosque" };
            var color = new Color(0.075f, 0.07f, 0.05f);
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            mat.SetFloat("_Smoothness", 0.05f);
            AssetDatabase.CreateAsset(mat, GroundMaterialPath);
        }

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Suelo del bosque";
        ground.transform.SetParent(parent, false);
        ground.transform.localPosition = new Vector3(0f, -0.02f, 0f);
        float s = (ForestHalfSize + 5f) * 2f / 10f; // Plane mide 10 x 10 m
        ground.transform.localScale = new Vector3(s, 1f, s);
        ground.GetComponent<MeshRenderer>().sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(ground, (StaticEditorFlags)~0);
    }

    // Cerca de madera vieja alrededor del claro, con tramos rotos y huecos.
    static void BuildFence(Transform parent)
    {
        const float hx = ClearingHalfX - 0.5f, hz = ClearingHalfZ - 0.5f, seg = 2.4f;
        void Side(Vector3 from, Vector3 to)
        {
            var dir = (to - from).normalized;
            int n = Mathf.FloorToInt(Vector3.Distance(from, to) / seg);
            float yaw = Quaternion.LookRotation(dir).eulerAngles.y;
            for (int i = 0; i < n; i++)
            {
                float roll = Rand();
                if (roll < 0.12f) continue; // hueco
                var p = from + dir * (seg * (i + 0.5f));
                if (Mathf.Abs(p.x) < 2f && p.z < 0f) continue; // entrada frente a la puerta de la oficina
                Place(roll < 0.4f ? FenceBroken : FenceStraight, p, yaw, parent, 0f, alignLongAxis: true);
            }
        }
        var a = new Vector3(-hx, 0, -hz); var b = new Vector3(hx, 0, -hz);
        var c = new Vector3(hx, 0, hz); var d = new Vector3(-hx, 0, hz);
        Side(a, b); Side(b, c); Side(c, d); Side(d, a);
        foreach (var corner in new[] { a, b, c, d }) Place(FenceCorner, corner, 0f, parent);
    }

    static GameObject Place(Entry e, Vector3 pos, float yaw, Transform parent, float yOffset = 0f, bool alignLongAxis = false)
    {
        var model = Load(e.file);
        if (model == null || parent == null) return null;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var b = GetBounds(go);
        if (b.size == Vector3.zero) { Object.DestroyImmediate(go); return null; }

        float measured = e.fit == Fit.Height ? b.size.y : Mathf.Max(b.size.x, b.size.z);
        float target = Mathf.Lerp(e.min, e.max, Rand());
        float scale = measured > 0.0001f ? target / measured : 1f;

        // Si el lado largo del modelo es Z, girarlo para que la cerca siga la dirección del tramo.
        float baseYaw = alignLongAxis && b.size.z > b.size.x ? 90f : 0f;

        go.transform.localScale = Vector3.one * scale;
        go.transform.localRotation = Quaternion.Euler(0f, yaw + baseYaw, 0f);
        go.transform.position = pos;

        // Apoyar la base en el suelo.
        b = GetBounds(go);
        go.transform.position += new Vector3(0f, -b.min.y + yOffset, 0f);
        // Centrar horizontalmente si el pivote está desplazado.
        go.transform.position += new Vector3(pos.x - b.center.x, 0f, pos.z - b.center.z);

        if (e.collider)
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                if (mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;

        foreach (var t in go.GetComponentsInChildren<Transform>())
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, (StaticEditorFlags)~0);
        return go;
    }

    static GameObject Load(string file)
    {
        if (cache.TryGetValue(file, out var m)) return m;
        m = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsFolder + file + ".fbx");
        if (m == null && !missing.Contains(file)) missing.Add(file);
        cache[file] = m;
        return m;
    }

    static Bounds GetBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    static float Rand() => (float)rng.NextDouble();
    static Entry Pick(Entry[] list) => list[rng.Next(list.Length)];

    static bool InClearing(Vector3 p, float margin = 0f) =>
        Mathf.Abs(p.x) < ClearingHalfX + margin && Mathf.Abs(p.z) < ClearingHalfZ + margin;

    static float DistanceToClearing(Vector3 p) =>
        Mathf.Max(Mathf.Abs(p.x) - ClearingHalfX, Mathf.Abs(p.z) - ClearingHalfZ, 0f);

    static Vector3 RandomPoint(float clearingMargin)
    {
        for (int i = 0; i < 100; i++)
        {
            var p = new Vector3((Rand() * 2f - 1f) * ForestHalfSize, 0f, (Rand() * 2f - 1f) * ForestHalfSize);
            if (!InClearing(p, clearingMargin)) return p;
        }
        return new Vector3(ForestHalfSize, 0f, ForestHalfSize);
    }

    static bool FarFromTrunks(Vector3 p, float minDist)
    {
        float sq = minDist * minDist;
        foreach (var t in trunkPositions) if ((t - p).sqrMagnitude < sq) return false;
        return true;
    }
}
