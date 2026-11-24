using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Abre huecos reales en las paredes de la oficina para las ventanas y la puerta de acceso,
/// pone vidrio transparente y convierte la puerta en una puerta con bisagra (componente Door).
/// Menú: Tools > Oficina > Abrir puerta y ventanas. Solo hace falta ejecutarlo una vez.
/// </summary>
public static class OfficeOpenings
{
    const string ScenePath = "Assets/Scenes/Oficina.unity";
    const string GlassMaterialPath = "Assets/Scenes/Oficina/VidrioVentana.mat";
    const float PlayerRadius = 0.35f;

    struct Opening { public float center, width, bottom, top; }

    [MenuItem("Tools/Oficina/Abrir puerta y ventanas")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        var office = GameObject.Find("Oficina de soporte");
        var arch = office != null ? office.transform.Find("Arquitectura") : null;
        if (arch == null) { Debug.LogError("No se encontró 'Oficina de soporte/Arquitectura'."); return; }

        var east = Child(arch, "Pared Este");
        var west = Child(arch, "Pared Oeste");
        var south = Child(arch, "Pared Sur");
        if (east == null && west == null && south == null)
        {
            Debug.Log("Las paredes ya tienen huecos; no hay nada que hacer.");
            return;
        }

        var glassMat = GlassMaterial();
        var eastOpenings = new List<Opening>();
        var westOpenings = new List<Opening>();

        // Ventanas: vidrio transparente centrado en el muro y marco hueco de 4 piezas.
        foreach (Transform glass in Children(arch, "Vidrio nocturno"))
        {
            var wall = glass.localPosition.x > 0 ? east : west;
            if (wall == null) continue;
            var gp = glass.localPosition; var gs = glass.localScale;
            (glass.localPosition.x > 0 ? eastOpenings : westOpenings).Add(new Opening
            { center = gp.z, width = gs.z, bottom = gp.y - gs.y / 2f, top = gp.y + gs.y / 2f });

            float wallX = wall.localPosition.x, wallDepth = wall.localScale.x;
            glass.localPosition = new Vector3(wallX, gp.y, gp.z);
            var r = glass.GetComponent<MeshRenderer>();
            r.sharedMaterial = glassMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            if (glass.GetComponent<Collider>() == null) glass.gameObject.AddComponent<BoxCollider>();

            var frame = Children(arch, "Marco de ventana").Find(f => Mathf.Abs(f.localPosition.z - gp.z) < 0.01f
                                                                     && Mathf.Sign(f.localPosition.x) == Mathf.Sign(gp.x));
            if (frame != null) BuildFrame(frame, gp, gs, wallX, wallDepth + 0.04f);
        }

        if (east != null) SplitWall(east, 2, eastOpenings);
        if (west != null) SplitWall(west, 2, westOpenings);

        // Puerta: hueco en la pared sur y bisagra en el borde izquierdo.
        var vestibule = office.transform.Find("Vestíbulo");
        var door = vestibule != null ? vestibule.Find("Puerta de acceso") : null;
        if (door != null && south != null)
        {
            var b = door.GetComponent<Renderer>().bounds;
            var min = arch.InverseTransformPoint(b.min);
            var max = arch.InverseTransformPoint(b.max);
            float x0 = Mathf.Min(min.x, max.x) - 0.02f, x1 = Mathf.Max(min.x, max.x) + 0.02f;
            float top = Mathf.Max(min.y, max.y) + 0.02f;
            SplitWall(south, 0, new List<Opening> { new Opening { center = (x0 + x1) / 2f, width = x1 - x0, bottom = 0f, top = top } });

            var hinge = new GameObject("Puerta (bisagra)");
            hinge.transform.SetParent(vestibule, false);
            hinge.transform.position = new Vector3(b.min.x, b.min.y, b.center.z);
            door.SetParent(hinge.transform, true);
            GameObjectUtility.SetStaticEditorFlags(door.gameObject, 0);
            hinge.AddComponent<Door>().openAngle = -95f; // abre hacia dentro de la oficina
        }
        else Debug.LogWarning("No se encontró la puerta de acceso o la pared sur.");

        // El hueco de la puerta mide ~0.95 m; con radio 0.5 el jugador no cabe.
        var player = Object.FindFirstObjectByType<PlayerFPS>();
        var capsule = player != null ? player.GetComponent<CapsuleCollider>() : null;
        if (capsule != null && capsule.radius > PlayerRadius)
        {
            Undo.RecordObject(capsule, "Radio del jugador");
            capsule.radius = PlayerRadius;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Oficina: ventanas transparentes y puerta funcional listas.");
    }

    // Divide un muro (cubo escalado, sin rotación) en tramos alrededor de los huecos.
    // axis = 0 si el muro corre a lo largo de X, 2 si corre a lo largo de Z.
    static void SplitWall(Transform wall, int axis, List<Opening> openings)
    {
        var c = wall.localPosition; var s = wall.localScale;
        float start = c[axis] - s[axis] / 2f, end = c[axis] + s[axis] / 2f;
        float y0 = c.y - s.y / 2f, y1 = c.y + s.y / 2f;
        openings.Sort((a, b) => a.center.CompareTo(b.center));

        int n = 0;
        void Piece(float a0, float a1, float b0, float b1)
        {
            if (a1 - a0 < 0.001f || b1 - b0 < 0.001f) return;
            var pos = c; pos[axis] = (a0 + a1) / 2f; pos.y = (b0 + b1) / 2f;
            var size = s; size[axis] = a1 - a0; size.y = b1 - b0;
            CopyBox(wall, $"{wall.name} ({++n})", pos, size);
        }

        float cursor = start;
        foreach (var o in openings)
        {
            float o0 = o.center - o.width / 2f, o1 = o.center + o.width / 2f;
            Piece(cursor, o0, y0, y1);      // tramo completo antes del hueco
            Piece(o0, o1, y0, o.bottom);    // antepecho
            Piece(o0, o1, o.top, y1);       // dintel
            cursor = o1;
        }
        Piece(cursor, end, y0, y1);
        Object.DestroyImmediate(wall.gameObject);
    }

    static void BuildFrame(Transform frame, Vector3 gp, Vector3 gs, float wallX, float depth)
    {
        var fs = frame.localScale;
        float bv = Mathf.Max(0.05f, (fs.y - gs.y) / 2f), bh = Mathf.Max(0.05f, (fs.z - gs.z) / 2f);
        float h = gs.y + 2f * bv, w = gs.z + 2f * bh;
        CopyBox(frame, "Marco superior", new Vector3(wallX, gp.y + (gs.y + bv) / 2f, gp.z), new Vector3(depth, bv, w));
        CopyBox(frame, "Marco inferior", new Vector3(wallX, gp.y - (gs.y + bv) / 2f, gp.z), new Vector3(depth, bv, w));
        CopyBox(frame, "Marco lateral", new Vector3(wallX, gp.y, gp.z - (gs.z + bh) / 2f), new Vector3(depth, h, bh));
        CopyBox(frame, "Marco lateral", new Vector3(wallX, gp.y, gp.z + (gs.z + bh) / 2f), new Vector3(depth, h, bh));
        Object.DestroyImmediate(frame.gameObject);
    }

    static void CopyBox(Transform source, string name, Vector3 localPos, Vector3 localScale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.layer = source.gameObject.layer;
        go.transform.SetParent(source.parent, false);
        go.transform.SetSiblingIndex(source.GetSiblingIndex());
        go.transform.localPosition = localPos;
        go.transform.localRotation = source.localRotation;
        go.transform.localScale = localScale;
        var src = source.GetComponent<MeshRenderer>();
        var dst = go.GetComponent<MeshRenderer>();
        dst.sharedMaterials = src.sharedMaterials;
        dst.shadowCastingMode = src.shadowCastingMode;
        dst.receiveShadows = src.receiveShadows;
        if (source.GetComponent<Collider>() == null) Object.DestroyImmediate(go.GetComponent<Collider>());
        GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(source.gameObject));
    }

    static Material GlassMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
        if (mat != null) return mat;

        mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "VidrioVentana" };
        mat.SetColor("_BaseColor", new Color(0.55f, 0.68f, 0.78f, 0.12f));
        mat.SetFloat("_Smoothness", 0.95f);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Surface", 1f);   // Transparent
        mat.SetFloat("_Blend", 0f);     // Alpha
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cull", (float)CullMode.Off);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetShaderPassEnabled("DepthOnly", false);
        mat.SetShaderPassEnabled("ShadowCaster", false);
        AssetDatabase.CreateAsset(mat, GlassMaterialPath);
        return mat;
    }

    static Transform Child(Transform parent, string name)
    {
        foreach (Transform t in parent) if (t.name == name) return t;
        return null;
    }

    static List<Transform> Children(Transform parent, string name)
    {
        var list = new List<Transform>();
        foreach (Transform t in parent) if (t.name == name) list.Add(t);
        return list;
    }
}
