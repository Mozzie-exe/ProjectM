using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Yolun iki tepe arasındaki yarmadan geçtiği yere (x ~540-622) tünel kurar.
// Terrain'e ve Yol_Guney_Arazi'ye dokunmaz; ikisinden sadece yükseklik okur. Tekrar çalıştırmak güvenli.
public static class TunnelBuilder
{
    const float X0 = 540f, X1 = 622f;          // tünel ağızları (dünya x)
    const float ZS = -21.5f, ZN = 1.0f;         // güney ve kuzey duvar (yol z -20.5..0)
    const float Y0 = 11.1f;                     // yol yüzeyi
    const float WallH = 3.5f, Rise = 2.5f;      // dikey duvar + kemer yüksekliği
    const float Edge = 2.5f;                    // örtü, duvarların bu kadar dışında zemine iner
    const float PortalDepth = 0.6f;
    const string MeshPath = "Assets/Models/Generated/Tunel.asset";

    static float Apex => Y0 + WallH + Rise;
    static float CoverMin => Apex + 0.8f;

    [MenuItem("Tools/Level/Build Tunnel")]
    public static void Build()
    {
        var south = GameObject.Find("Yol_Guney_Arazi")?.GetComponent<Collider>();
        var terrain = Terrain.activeTerrain;
        if (south == null || terrain == null) { Debug.LogError("Tunnel: terrain ya da Yol_Guney_Arazi bulunamadı"); return; }

        var b = new MeshBuilder(3);
        BuildShell(b);
        var zs = CoverZs();
        var xs = new List<float>();
        for (float x = X0; x < X1 - 0.01f; x += 2f) xs.Add(x);
        xs.Add(X1);
        var h = new float[xs.Count, zs.Count];
        for (int i = 0; i < xs.Count; i++)
            for (int j = 0; j < zs.Count; j++)
                h[i, j] = CoverHeight(xs[i], zs[j], terrain, south);
        BuildCover(b, xs, zs, h);
        BuildPortal(b, X0, -1f, zs, h, 0, terrain, south);
        BuildPortal(b, X1, +1f, zs, h, xs.Count - 1, terrain, south);
        BuildLamps(b);

        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (mesh == null) { mesh = new Mesh { name = "Tunel" }; AssetDatabase.CreateAsset(mesh, MeshPath); }
        b.Fill(mesh);
        EditorUtility.SetDirty(mesh);

        var go = GameObject.Find("Tunel");
        if (go == null) go = new GameObject("Tunel");
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        go.transform.localScale = Vector3.one;
        go.isStatic = true;
        var mf = go.GetComponent<MeshFilter>(); if (mf == null) mf = go.AddComponent<MeshFilter>();
        var mr = go.GetComponent<MeshRenderer>(); if (mr == null) mr = go.AddComponent<MeshRenderer>();
        var mc = go.GetComponent<MeshCollider>(); if (mc == null) mc = go.AddComponent<MeshCollider>();
        mf.sharedMesh = mesh;
        mc.sharedMesh = null; mc.sharedMesh = mesh;
        mr.sharedMaterials = new[] { Concrete(), AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Toprak_YolKenari.mat"), Lamp() };
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log($"Tunnel: {mesh.vertexCount} vertex, x {X0}-{X1}, tavan {Apex:F1}");
    }

    // Kesit (z, y): güney duvar tabanından kemer üzerinden kuzey duvar tabanına
    static List<Vector2> Profile()
    {
        var p = new List<Vector2> { new Vector2(ZS, Y0 - 0.3f), new Vector2(ZS, Y0 + WallH) };
        float zc = (ZS + ZN) * 0.5f, hw = (ZN - ZS) * 0.5f;
        const int N = 18;
        for (int i = 1; i < N; i++)
        {
            float a = Mathf.PI * i / N;
            p.Add(new Vector2(zc - hw * Mathf.Cos(a), Y0 + WallH + Rise * Mathf.Sin(a)));
        }
        p.Add(new Vector2(ZN, Y0 + WallH));
        p.Add(new Vector2(ZN, Y0 - 0.3f));
        return p;
    }

    static float ArchY(float z)
    {
        float zc = (ZS + ZN) * 0.5f, hw = (ZN - ZS) * 0.5f;
        float u = Mathf.Clamp((z - zc) / hw, -1f, 1f);
        return Y0 + WallH + Rise * Mathf.Sqrt(1f - u * u);
    }

    static void BuildShell(MeshBuilder b)
    {
        var p = Profile();
        float zc = (ZS + ZN) * 0.5f;
        float xa = X0 - PortalDepth, xb = X1 + PortalDepth;
        float s = 0f;
        for (int i = 0; i + 1 < p.Count; i++)
        {
            float s2 = s + Vector2.Distance(p[i], p[i + 1]);
            for (float x = xa; x < xb - 0.01f; x += 6f)
            {
                float x2 = Mathf.Min(x + 6f, xb);
                var a = new Vector3(x, p[i].y, p[i].x); var c = new Vector3(x2, p[i].y, p[i].x);
                var d = new Vector3(x2, p[i + 1].y, p[i + 1].x); var e = new Vector3(x, p[i + 1].y, p[i + 1].x);
                Vector3 inward = new Vector3((x + x2) * 0.5f, Y0 + 2f, zc) - (a + d) * 0.5f;
                b.Quad(0, a, c, d, e, inward,
                    new Vector2(x / 3f, s / 3f), new Vector2(x2 / 3f, s / 3f), new Vector2(x2 / 3f, s2 / 3f), new Vector2(x / 3f, s2 / 3f));
            }
            s = s2;
        }
    }

    static List<float> CoverZs()
    {
        var zs = new List<float> { ZS - Edge, ZS - 1f };
        for (float z = ZS; z < ZN - 0.01f; z += 1.5f) zs.Add(z);
        zs.Add(ZN); zs.Add(ZN + 1f); zs.Add(ZN + Edge);
        return zs;
    }

    static float Ground(float x, float z, Terrain t, Collider south)
    {
        if (z >= 0f) return t.SampleHeight(new Vector3(x, 0, z)) + t.transform.position.y;
        RaycastHit hit;
        return south.Raycast(new Ray(new Vector3(x, 200f, z), Vector3.down), out hit, 400f) ? hit.point.y : Y0;
    }

    static float CoverHeight(float x, float z, Terrain t, Collider south)
    {
        // dış kenar zemine gömülür; duvarların arası iki yamaç arasında köprü, kemerin en az 0.8 m üstünde
        if (z <= ZS - Edge + 0.01f || z >= ZN + Edge - 0.01f) return Ground(x, z, t, south) - 0.4f;
        float hS = Mathf.Max(Ground(x, ZS - 1f, t, south), CoverMin);
        float hN = Mathf.Max(Ground(x, ZN + 1f, t, south), CoverMin);
        if (z < ZS) return Mathf.Max(Ground(x, z, t, south), Mathf.Lerp(hS, CoverMin, 0.5f));
        if (z > ZN) return Mathf.Max(Ground(x, z, t, south), Mathf.Lerp(hN, CoverMin, 0.5f));
        float u = (z - ZS) / (ZN - ZS);
        float bulge = 0.5f * Mathf.Sin(u * Mathf.PI) + 0.25f * Mathf.PerlinNoise(x * 0.11f, z * 0.13f);
        return Mathf.Max(Mathf.Lerp(hS, hN, u) + bulge, CoverMin);
    }

    static void BuildCover(MeshBuilder b, List<float> xs, List<float> zs, float[,] h)
    {
        for (int i = 0; i + 1 < xs.Count; i++)
            for (int j = 0; j + 1 < zs.Count; j++)
            {
                var a = new Vector3(xs[i], h[i, j], zs[j]); var c = new Vector3(xs[i + 1], h[i + 1, j], zs[j]);
                var d = new Vector3(xs[i + 1], h[i + 1, j + 1], zs[j + 1]); var e = new Vector3(xs[i], h[i, j + 1], zs[j + 1]);
                b.Quad(1, a, c, d, e, Vector3.up,
                    new Vector2(a.x, a.z) / 4f, new Vector2(c.x, c.z) / 4f, new Vector2(d.x, d.z) / 4f, new Vector2(e.x, e.z) / 4f);
            }
    }

    // Ağız duvarı: kemerin üstünden örtünün üstüne; kemer dışında zeminin içine kadar iner
    static void BuildPortal(MeshBuilder b, float x, float dir, List<float> zs, float[,] h, int col, Terrain t, Collider south)
    {
        float xf = x + dir * PortalDepth;
        var outward = new Vector3(dir, 0, 0);
        var cols = new List<Vector3>(); // (z, alt, üst)
        for (int j = 0; j < zs.Count; j++)
        {
            float z0 = zs[j];
            float top = Mathf.Max(h[col, j], Ground(x, z0, t, south)) + 0.35f;
            if (z0 < ZS || z0 > ZN) cols.Add(new Vector3(z0, Ground(xf, z0, t, south) - 0.8f, top));
            else
            {
                if (Mathf.Approximately(z0, ZS)) cols.Add(new Vector3(ZS - 0.01f, Y0 - 0.5f, top));
                cols.Add(new Vector3(z0, ArchY(z0), top));
                if (Mathf.Approximately(z0, ZN)) cols.Add(new Vector3(ZN + 0.01f, Y0 - 0.5f, top));
            }
        }
        for (int k = 0; k + 1 < cols.Count; k++)
        {
            float za = cols[k].x, ba = cols[k].y, ta = cols[k].z;
            float zb = cols[k + 1].x, bb = cols[k + 1].y, tb = cols[k + 1].z;
            if (ta <= ba && tb <= bb) continue;
            b.Quad(0, new Vector3(xf, ba, za), new Vector3(xf, bb, zb), new Vector3(xf, tb, zb), new Vector3(xf, ta, za), outward,
                new Vector2(za, ba) / 3f, new Vector2(zb, bb) / 3f, new Vector2(zb, tb) / 3f, new Vector2(za, ta) / 3f);
            b.Quad(0, new Vector3(xf, ta, za), new Vector3(xf, tb, zb), new Vector3(x, tb, zb), new Vector3(x, ta, za), Vector3.up,
                new Vector2(za, 0) / 3f, new Vector2(zb, 0) / 3f, new Vector2(zb, PortalDepth) / 3f, new Vector2(za, PortalDepth) / 3f);
        }
    }

    // Tavanın ortasında aralıklı loş sodyum lambaları (sadece emisyon, ışık kaynağı yok)
    static void BuildLamps(MeshBuilder b)
    {
        float zc = (ZS + ZN) * 0.5f, y = Apex - 0.06f;
        for (float x = X0 + 6f; x < X1 - 3f; x += 12f)
            b.Quad(2, new Vector3(x - 0.7f, y, zc - 0.18f), new Vector3(x + 0.7f, y, zc - 0.18f),
                   new Vector3(x + 0.7f, y, zc + 0.18f), new Vector3(x - 0.7f, y, zc + 0.18f), Vector3.down,
                   Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
    }

    static Material Concrete() => GetMat("Assets/Materials/Tunel_Beton.mat", m =>
    {
        m.SetColor("_BaseColor", new Color(0.34f, 0.33f, 0.31f));
        m.SetFloat("_Smoothness", 0.15f);
    });

    static Material Lamp() => GetMat("Assets/Materials/Tunel_Lamba.mat", m =>
    {
        m.SetColor("_BaseColor", new Color(1f, 0.62f, 0.28f));
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(1f, 0.55f, 0.2f) * 2.2f);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
    });

    static Material GetMat(string path, System.Action<Material> setup)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        setup(m);
        EditorUtility.SetDirty(m);
        return m;
    }

    class MeshBuilder
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int>[] sub;
        public MeshBuilder(int subs) { sub = new List<int>[subs]; for (int i = 0; i < subs; i++) sub[i] = new List<int>(); }

        // Dörtgen; ön yüzü 'facing' yönüne bakacak şekilde sarılır
        public void Quad(int s, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
            // Unity'de (a,b,c) üçgeninin ön yüz normali (b-a)x(c-a)
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-8f) n = Vector3.Cross(c - a, d - a);
            if (Vector3.Dot(n, facing) >= 0f) sub[s].AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            else sub[s].AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
        }

        public void Fill(Mesh m)
        {
            m.Clear();
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v); m.SetUVs(0, uv);
            m.subMeshCount = sub.Length;
            for (int i = 0; i < sub.Length; i++) m.SetTriangles(sub[i], i);
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
        }
    }
}
