using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Menü: Tools > Kapılar > MainHouse_V2'ye Kapıları Ekle
// MainHouse_V2 modelindeki kapı pervazlarını bulur, her boşluğa açılır kapanır bir kapı koyar.
// Tekrar çalıştırılırsa eski kapıları silip yeniden kurar.
public static class HouseDoorBuilder
{
    const string HouseName = "MainHouse_V2";
    const string DoorsRootName = "Kapilar";
    const string MatFolder = "Assets/Materials/Doors";

    struct DoorDef
    {
        public string name;
        public bool exterior;
        public string[] frames;
        public DoorDef(string name, bool exterior, params string[] frames)
        {
            this.name = name; this.exterior = exterior; this.frames = frames;
        }
    }

    // Pervaz parçalarının modeldeki isimleri (mainhouse_v2.fbx içinden çıkarıldı)
    static readonly DoorDef[] Doors =
    {
        new DoorDef("Kapi_Mutfak",            false, "Cube.082", "Cube.083", "Cube.084", "Cube.091", "Cube.092", "Cube.093"),
        new DoorDef("Kapi_Camasirhane",       false, "Cube.085", "Cube.086", "Cube.087", "Cube.094", "Cube.095", "Cube.096"),
        new DoorDef("Kapi_Banyo",             false, "Cube.088", "Cube.089", "Cube.090", "Cube.097", "Cube.098", "Cube.099"),
        new DoorDef("Kapi_YatakOdasi2",       false, "Cube.100", "Cube.101", "Cube.102", "Cube.103", "Cube.104", "Cube.105"),
        new DoorDef("Kapi_EbeveynYatakOdasi", false, "Cube.106", "Cube.107", "Cube.108", "Cube.109", "Cube.110", "Cube.111"),
        new DoorDef("Kapi_GiyinmeOdasi",      false, "Cube.112", "Cube.113", "Cube.114", "Cube.115", "Cube.116", "Cube.117"),
        new DoorDef("Kapi_EbeveynBanyo",      false, "Cube.118", "Cube.119", "Cube.120", "Cube.121", "Cube.122", "Cube.123"),
        new DoorDef("DisKapi_Veranda",        true,  "Cube.142", "Cube.143", "Cube.144", "Cube.145", "Cube.146", "Cube.147"),
        new DoorDef("DisKapi_Veranda2",       true,  "Cube.148", "Cube.149", "Cube.150", "Cube.151", "Cube.152", "Cube.153"),
        new DoorDef("DisKapi_On",             true,  "Cube.079"),
    };

    class Mats
    {
        public Material extWood, extPanel, intWood, intPanel, brass, bronze, iron;
    }

    [MenuItem("Tools/Kapılar/MainHouse_V2'ye Kapıları Ekle")]
    public static void Build()
    {
        Transform house = FindInOpenScenes(HouseName);
        if (house == null)
        {
            EditorUtility.DisplayDialog("Kapılar", "Sahnede '" + HouseName + "' bulunamadı.", "Tamam");
            return;
        }

        // Eski kapıları sil
        Transform old = house.Find(DoorsRootName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var byName = new Dictionary<string, Transform>();
        foreach (var t in house.GetComponentsInChildren<Transform>(true))
            if (!byName.ContainsKey(t.name)) byName[t.name] = t;

        Mats mats = CreateMaterials();

        var rootGO = new GameObject(DoorsRootName);
        rootGO.layer = house.gameObject.layer;
        rootGO.transform.SetParent(house, false);
        Undo.RegisterCreatedObjectUndo(rootGO, "Kapıları Ekle");

        int built = 0;
        var missing = new List<string>();
        foreach (var def in Doors)
        {
            var pieces = new List<Bounds>();
            foreach (var n in def.frames)
            {
                if (byName.TryGetValue(n, out Transform t) && LocalBounds(house, t, out Bounds b)) pieces.Add(b);
                else missing.Add(def.name + ":" + n);
            }
            if (pieces.Count == 0) continue;

            BuildDoor(rootGO.transform, def, pieces, mats, house.gameObject.layer);
            built++;
        }

        int interactorsAdded = EnsurePlayerInteractor();

        EditorSceneManager.MarkSceneDirty(house.gameObject.scene);
        Selection.activeGameObject = rootGO;

        string msg = built + " kapı eklendi.";
        if (interactorsAdded > 0) msg += "\nOyuncu kamerasına PlayerInteractor eklendi.";
        if (missing.Count > 0) msg += "\n\nBulunamayan parçalar:\n" + string.Join("\n", missing);
        msg += "\n\nSahneyi kaydetmeyi unutma (Ctrl+S).";
        Debug.Log("[Kapılar] " + msg);
        EditorUtility.DisplayDialog("Kapılar", msg, "Tamam");
    }

    // ---------------------------------------------------------------- yerleşim

    static void BuildDoor(Transform parent, DoorDef def, List<Bounds> pieces, Mats mats, int layer)
    {
        // Tüm hesaplar evin yerel uzayında (model birimleri), böylece ev nasıl döndürülmüş/ölçeklenmiş olursa olsun doğru oturur
        Bounds B = pieces[0];
        foreach (var p in pieces) B.Encapsulate(p);

        bool widthIsX = B.size.x >= B.size.z;
        float WidthOf(Bounds b) => widthIsX ? b.size.x : b.size.z;
        float bWidth = WidthOf(B);
        float bNormal = widthIsX ? B.size.z : B.size.x;
        float bNormalCenter = widthIsX ? B.center.z : B.center.x;

        // Dikmeler (ince dikey parçalar) -> kenar payı
        var posts = pieces.Where(p => WidthOf(p) < 0.25f * bWidth).ToList();
        float inset = posts.Count > 0 ? posts.Max(p => WidthOf(p)) : bWidth * 0.06f;

        // Üst kirişler (alçak parçalar) -> kapı boşluğunun üstü
        var lintels = pieces.Where(p => p.size.y < 0.3f * B.size.y).ToList();
        float top = lintels.Count > 0 ? lintels.Min(p => p.min.y) : B.max.y - B.size.y * 0.052f;
        float bottom = B.min.y + B.size.y * 0.022f;

        float thickness = bWidth * 0.05f;
        float gap = bWidth * 0.006f;
        float width = bWidth - 2f * inset - 2f * gap;
        float height = top - bottom - gap;

        float normalCenter;
        if (bNormal > bWidth * 0.08f)
        {
            // Duvarın iki yüzünde pervaz var -> duvarın ortası
            normalCenter = bNormalCenter;
        }
        else
        {
            // Sadece dış yüzde pervaz var (ön kapı) -> pervazın hemen içine
            float dir = Mathf.Sign(0f - bNormalCenter); // modelin merkezi evin içinde
            float innerFace = dir > 0 ? (widthIsX ? B.max.z : B.max.x) : (widthIsX ? B.min.z : B.min.x);
            normalCenter = innerFace + dir * (thickness * 0.5f + gap);
        }

        var root = new GameObject(def.name);
        root.layer = layer;
        root.transform.SetParent(parent, false);
        root.transform.localPosition = widthIsX
            ? new Vector3(B.center.x, bottom, normalCenter)
            : new Vector3(normalCenter, bottom, B.center.z);
        root.transform.localRotation = widthIsX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);

        var hinge = new GameObject("Mentese").transform;
        hinge.gameObject.layer = layer;
        hinge.SetParent(root.transform, false);
        hinge.localPosition = new Vector3(-width * 0.5f, 0f, 0f);

        var leaf = new GameObject("Kanat").transform;
        leaf.gameObject.layer = layer;
        leaf.SetParent(hinge, false);

        BuildLeafGeometry(leaf, width, height, thickness, def.exterior, mats, layer);

        var door = root.AddComponent<Door>();
        door.hinge = hinge;
        door.openAngle = def.exterior ? 100f : 95f;
    }

    // ---------------------------------------------------------------- kapı modeli

    static void BuildLeafGeometry(Transform leaf, float W, float H, float T, bool exterior, Mats mats, int layer)
    {
        Material wood = exterior ? mats.extWood : mats.intWood;
        Material panel = exterior ? mats.extPanel : mats.intPanel;
        Material handle = exterior ? mats.brass : mats.bronze;

        // Gövde (çarpışma bunun üzerinde)
        Part(leaf, PrimitiveType.Cube, "Govde", new Vector3(W * 0.5f, H * 0.5f, 0f), new Vector3(W, H, T), wood, layer, true);

        // Kabartma paneller (iki yüz)
        float m = W * 0.13f;
        float[] rowWeights = exterior ? new[] { 1f, 1.15f, 0.55f } : new[] { 1f, 1.3f };
        int rows = rowWeights.Length;
        float usableH = H - 2f * m - (rows - 1) * m;
        float colW = (W - 3f * m) * 0.5f;
        float pt = T * 0.22f;
        float pz = T * 0.5f + pt * 0.5f;
        float wSum = rowWeights.Sum();

        foreach (float s in new[] { 1f, -1f })
        {
            float y = m;
            for (int r = 0; r < rows; r++)
            {
                float rh = usableH * rowWeights[r] / wSum;
                for (int c = 0; c < 2; c++)
                {
                    float x = m + colW * 0.5f + c * (colW + m);
                    Part(leaf, PrimitiveType.Cube, "Panel", new Vector3(x, y + rh * 0.5f, s * pz), new Vector3(colW, rh, pt), panel, layer);
                }
                y += rh + m;
            }
        }

        // Kulp (iki yüz): arka plaka + boyun + kol
        float hx = W - W * 0.09f;
        float hy = H * 0.45f;
        float plateW = W * 0.06f, plateH = W * 0.22f, bt = T * 0.14f;
        float neckR = W * 0.013f, neckLen = W * 0.05f;
        float leverLen = W * 0.16f, leverT = W * 0.024f;

        foreach (float s in new[] { 1f, -1f })
        {
            float face = T * 0.5f; // kulp gövdenin yüzüne oturur
            Part(leaf, PrimitiveType.Cube, "KulpPlaka", new Vector3(hx, hy - plateH * 0.2f, s * (face + bt * 0.5f)), new Vector3(plateW, plateH, bt), handle, layer);

            var neck = Part(leaf, PrimitiveType.Cylinder, "KulpBoyun", new Vector3(hx, hy, s * (face + bt + neckLen * 0.5f)), new Vector3(neckR * 2f, neckLen * 0.5f, neckR * 2f), handle, layer);
            neck.localRotation = Quaternion.Euler(90f, 0f, 0f);

            Part(leaf, PrimitiveType.Cube, "KulpKol", new Vector3(hx - leverLen * 0.5f + neckR, hy, s * (face + bt + neckLen - leverT * 0.5f)), new Vector3(leverLen, leverT, leverT * 1.2f), handle, layer);

            if (exterior)
                Part(leaf, PrimitiveType.Cube, "AnahtarDeligi", new Vector3(hx, hy - plateH * 0.42f, s * (face + bt + bt * 0.2f)), new Vector3(plateW * 0.28f, plateH * 0.13f, bt * 0.4f), mats.iron, layer);
        }

        // Menteşeler
        float hingeR = T * 0.3f, hingeH = H * 0.06f;
        foreach (float fy in new[] { 0.12f, 0.5f, 0.88f })
            Part(leaf, PrimitiveType.Cylinder, "MenteseParca", new Vector3(0f, H * fy, 0f), new Vector3(hingeR * 2f, hingeH * 0.5f, hingeR * 2f), mats.iron, layer);
    }

    static Transform Part(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat, int layer, bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.layer = layer;
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go.transform;
    }

    // ---------------------------------------------------------------- malzemeler

    static Mats CreateMaterials()
    {
        if (!AssetDatabase.IsValidFolder(MatFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            AssetDatabase.CreateFolder("Assets/Materials", "Doors");
        }

        var mats = new Mats
        {
            // Dış kapılar: koyu kahverengi panelli ahşap
            extWood  = GetOrCreate("Door_Exterior",       new Color(0.80f, 0.58f, 0.40f), "dark_paneled_wood_diff_1k", "dark_paneled_wood_nor_gl_1k", 0f, 0.35f, new Vector2(1f, 1.5f)),
            extPanel = GetOrCreate("Door_Exterior_Panel", new Color(0.62f, 0.43f, 0.29f), "dark_paneled_wood_diff_1k", "dark_paneled_wood_nor_gl_1k", 0f, 0.4f, Vector2.one),
            // İç kapılar: evin duvarlarıyla aynı kahverengi tahta
            intWood  = GetOrCreate("Door_Interior",       new Color(0.85f, 0.70f, 0.55f), "planks_brown_10_diff_1k", "planks_brown_10_nor_gl_1k", 0f, 0.3f, new Vector2(1f, 1.5f)),
            intPanel = GetOrCreate("Door_Interior_Panel", new Color(0.68f, 0.52f, 0.38f), "planks_brown_10_diff_1k", "planks_brown_10_nor_gl_1k", 0f, 0.35f, Vector2.one),
            // Metaller
            brass  = GetOrCreate("Door_Handle_Brass",  new Color(0.78f, 0.60f, 0.28f), null, null, 1f, 0.7f, Vector2.one),
            bronze = GetOrCreate("Door_Handle_Bronze", new Color(0.42f, 0.30f, 0.18f), null, null, 1f, 0.6f, Vector2.one),
            iron   = GetOrCreate("Door_Iron",          new Color(0.07f, 0.07f, 0.07f), null, null, 0.8f, 0.45f, Vector2.one),
        };
        AssetDatabase.SaveAssets();
        return mats;
    }

    static Material GetOrCreate(string name, Color color, string tex, string normal, float metallic, float smoothness, Vector2 tiling)
    {
        string path = MatFolder + "/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing; // elle yapılan değişiklikler korunur

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(shader);
        m.SetColor("_BaseColor", color);
        m.color = color;
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);

        Texture2D baseTex = FindTexture(tex);
        if (baseTex != null)
        {
            m.SetTexture("_BaseMap", baseTex);
            m.SetTextureScale("_BaseMap", tiling);
        }

        Texture2D normalTex = FindTexture(normal);
        if (normalTex != null)
        {
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normalTex)) as TextureImporter;
            if (importer != null && importer.textureType == TextureImporterType.NormalMap)
            {
                m.SetTexture("_BumpMap", normalTex);
                m.EnableKeyword("_NORMALMAP");
            }
        }

        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Texture2D FindTexture(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Texture2D"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(p) == name)
                return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }
        return null;
    }

    // ---------------------------------------------------------------- yardımcılar

    static bool LocalBounds(Transform house, Transform t, out Bounds result)
    {
        result = default;
        var mf = t.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return false;

        Bounds mb = mf.sharedMesh.bounds;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = mb.center + Vector3.Scale(mb.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 p = house.InverseTransformPoint(t.TransformPoint(corner));
            if (i == 0) result = new Bounds(p, Vector3.zero);
            else result.Encapsulate(p);
        }
        return true;
    }

    static Transform FindInOpenScenes(string name)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded) continue;
            foreach (var go in s.GetRootGameObjects())
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t;
        }
        return null;
    }

    static int EnsurePlayerInteractor()
    {
        int added = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded) continue;
            foreach (var go in s.GetRootGameObjects())
                foreach (var pm in go.GetComponentsInChildren<PlayerMovement>(true))
                {
                    var cam = pm.GetComponentInChildren<Camera>(true);
                    if (cam != null && cam.GetComponent<PlayerInteractor>() == null)
                    {
                        Undo.AddComponent<PlayerInteractor>(cam.gameObject);
                        added++;
                    }
                }
        }
        return added;
    }
}
