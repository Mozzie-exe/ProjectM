# Giriş sinematiği modelleri: pikap, evrak, el feneri, eller.
# Çalıştırma (Blender arayüzü gerekmez):
#   blender --background --factory-startup --python build_intro_models.py -- <proje_klasoru>
# Ölçü gerçek metre. Unity'de araba kökü oyun dünyasına göre (~1.78x) ölçeklenir.
# Blender'da araba +Y yönüne bakar, sürücü solda (-X).
import bpy, bmesh, sys, os, math
import numpy as np
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PROJECT = argv[0] if argv else os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(PROJECT, "Assets", "Models", "IntroCinematic")
TEX = os.path.join(OUT, "Textures")
os.makedirs(TEX, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)

# ---------------------------------------------------------------- yardımcılar
_mats = {}
def mat(name):
    if name not in _mats:
        _mats[name] = bpy.data.materials.new(name)
    return _mats[name]

def euler(rot):
    return Matrix.Rotation(math.radians(rot[2]), 4, 'Z') @ \
           Matrix.Rotation(math.radians(rot[1]), 4, 'Y') @ \
           Matrix.Rotation(math.radians(rot[0]), 4, 'X')

class Part:
    """Tek bir Blender objesi; parçalar dünya koordinatında eklenir, pivot sonra ayarlanır."""
    def __init__(self, name, pivot=(0, 0, 0), uv_fit=False):
        self.name, self.pivot, self.uv_fit = name, Vector(pivot), uv_fit
        self.bm = bmesh.new()
        self.mats = []

    def _tag(self, before, m):
        if m not in self.mats:
            self.mats.append(m)
        idx = self.mats.index(m)
        self.bm.faces.ensure_lookup_table()
        for f in list(self.bm.faces)[before:]:
            f.material_index = idx

    def box(self, center, size, m, rot=(0, 0, 0)):
        n = len(self.bm.faces)
        M = (Matrix.Translation(center) @ euler(rot) @
             Matrix.Diagonal((size[0], size[1], size[2], 1)))
        bmesh.ops.create_cube(self.bm, size=1.0, matrix=M)
        self._tag(n, m)

    def cyl(self, center, r, depth, m, rot=(0, 0, 0), segs=14):
        n = len(self.bm.faces)
        M = Matrix.Translation(center) @ euler(rot)
        bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segs,
                              radius1=r, radius2=r, depth=depth, matrix=M)
        self._tag(n, m)

    def seg(self, a, b, w, h, m):
        """a'dan b'ye uzanan kutu (parmak boğumu gibi)."""
        a, b = Vector(a), Vector(b)
        d = b - a
        q = Vector((0, 0, 1)).rotation_difference(d.normalized())
        M = (Matrix.Translation((a + b) / 2) @ q.to_matrix().to_4x4() @
             Matrix.Diagonal((w, h, d.length, 1)))
        n = len(self.bm.faces)
        bmesh.ops.create_cube(self.bm, size=1.0, matrix=M)
        self._tag(n, m)

    def torus(self, center, R, r, m, rot=(0, 0, 0), seg_major=20, seg_minor=6):
        n = len(self.bm.faces)
        M = Matrix.Translation(center) @ euler(rot)
        rings = []
        for i in range(seg_major):
            a = 2 * math.pi * i / seg_major
            ring = []
            for j in range(seg_minor):
                b = 2 * math.pi * j / seg_minor
                p = Vector(((R + r * math.cos(b)) * math.cos(a),
                            (R + r * math.cos(b)) * math.sin(a),
                            r * math.sin(b)))
                ring.append(self.bm.verts.new(M @ p))
            rings.append(ring)
        for i in range(seg_major):
            for j in range(seg_minor):
                i2, j2 = (i + 1) % seg_major, (j + 1) % seg_minor
                self.bm.faces.new((rings[i][j], rings[i2][j], rings[i2][j2], rings[i][j2]))
        self._tag(n, m)

    def build(self, flip=False):
        bm = self.bm
        if flip:
            bmesh.ops.reverse_faces(bm, faces=bm.faces)
        bm.normal_update()
        uv = bm.loops.layers.uv.new("UVMap")
        if self.uv_fit:
            cs = [v.co for v in bm.verts]
            mn = Vector((min(c.x for c in cs), min(c.y for c in cs), min(c.z for c in cs)))
            ext = Vector((max(c.x for c in cs), max(c.y for c in cs), max(c.z for c in cs))) - mn
        for f in bm.faces:
            nrm = f.normal
            ax = max(range(3), key=lambda i: abs(nrm[i]))
            u_i, v_i = [(1, 2), (0, 2), (0, 1)][ax]
            for l in f.loops:
                c = l.vert.co
                if self.uv_fit:
                    l[uv].uv = ((c[u_i] - mn[u_i]) / max(ext[u_i], 1e-6),
                                (c[v_i] - mn[v_i]) / max(ext[v_i], 1e-6))
                else:
                    l[uv].uv = (c[u_i], c[v_i])  # 1 UV = 1 metre
        bmesh.ops.translate(bm, verts=bm.verts, vec=-self.pivot)
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        for m in self.mats:
            me.materials.append(mat(m))
        ob = bpy.data.objects.new(self.name, me)
        ob.location = self.pivot
        bpy.context.scene.collection.objects.link(ob)
        return ob

# ---------------------------------------------------------------- pikap
PAINT, TRIM, GLASS, RUBBER, METAL = "IC_CarPaint", "IC_DarkPlastic", "IC_Glass", "IC_Rubber", "IC_Metal"
SEAT, LENS, LAMP_RED, GRILLE = "IC_SeatFabric", "IC_LampLens", "IC_TailLens", "IC_Grille"
GLOW, TGLOW, GAUGE, GAUGEGLOW = "IC_HeadlightGlow", "IC_TaillightGlow", "IC_GaugeFace", "IC_GaugeGlow"

body = Part("Body")
body.box((0, 0.05, 0.70), (1.90, 5.10, 0.50), PAINT)               # alt gövde
body.box((0, 0, 0.42), (1.60, 4.60, 0.10), METAL)                  # şasi altı
body.box((0, 1.62, 1.05), (1.86, 1.76, 0.20), PAINT)               # kaput
body.box((0, 2.53, 0.86), (1.80, 0.10, 0.46), GRILLE)
body.box((0, 2.66, 0.58), (1.98, 0.14, 0.18), METAL)               # ön tampon
body.box((0, -2.64, 0.60), (1.98, 0.12, 0.16), METAL)              # arka tampon
for x in (-0.64, 0.64):
    body.cyl((x, 2.585, 0.93), 0.12, 0.06, LENS, rot=(90, 0, 0))   # far camı
    body.box((x * 1.33, -2.61, 1.05), (0.16, 0.06, 0.22), LAMP_RED)
for x in (-0.93, 0.93):
    for y in (1.65, -1.65):
        body.box((x, y, 0.98), (0.12, 1.05, 0.10), PAINT)          # çamurluk
body.box((0, -0.82, 1.15), (1.90, 0.06, 0.40), PAINT)              # kabin arka duvarı
for x in (-0.86, 0.86):
    body.box((x, -0.82, 1.60), (0.18, 0.06, 0.50), PAINT)
body.box((0, -0.82, 1.80), (1.90, 0.06, 0.10), PAINT)
body.box((0, -0.20, 1.86), (1.90, 1.30, 0.06), PAINT)              # tavan
body.box((0, -0.20, 1.825), (1.80, 1.20, 0.01), TRIM)              # tavan döşemesi
for x in (-0.92, 0.92):
    body.box((x, 0.585, 1.49), (0.07, 0.07, 0.76), PAINT, rot=(25.9, 0, 0))  # A direkleri
body.box((0, 0.42, 1.82), (1.88, 0.07, 0.06), PAINT)
body.box((0.95, -0.02, 1.15), (0.06, 1.44, 0.40), PAINT)           # yolcu kapısı (sabit)
body.box((0.915, -0.02, 1.15), (0.02, 1.36, 0.36), TRIM)
body.box((0.95, 0.66, 1.60), (0.05, 0.06, 0.50), PAINT)
body.box((0.95, -0.03, 1.83), (0.05, 1.44, 0.05), PAINT)
body.box((0, -1.74, 0.96), (1.84, 1.74, 0.04), METAL)              # kasa tabanı
for x in (-0.92, 0.92):
    body.box((x, -1.74, 1.17), (0.06, 1.74, 0.45), PAINT)
body.box((0, -2.58, 1.17), (1.90, 0.06, 0.45), PAINT)              # bagaj kapağı
body.box((0, -0.02, 0.62), (1.82, 1.50, 0.04), RUBBER)             # paspas
body.box((0, 0.60, 1.10), (1.84, 0.34, 0.26), TRIM)                # torpido
body.box((0, 0.66, 1.235), (1.84, 0.22, 0.03), TRIM, rot=(-12, 0, 0))
body.box((-0.40, 0.50, 1.27), (0.46, 0.16, 0.09), TRIM)            # gösterge siperi
body.box((0.40, 0.44, 1.06), (0.40, 0.03, 0.14), TRIM)             # torpido gözü
body.box((0, 0.32, 1.76), (0.22, 0.03, 0.07), METAL)               # dikiz aynası
body.box((0, 0.36, 1.80), (0.02, 0.06, 0.06), METAL)
body.box((0, -0.42, 0.69), (1.60, 0.50, 0.12), METAL)              # koltuk ayağı
body.box((0, -0.42, 0.86), (1.70, 0.56, 0.18), SEAT)               # kanepe oturak
body.box((0, -0.74, 1.24), (1.70, 0.14, 0.62), SEAT, rot=(-10, 0, 0))
body.cyl((-0.40, 0.43, 1.18), 0.035, 0.30, TRIM, rot=(59, 0, 0), segs=10)   # direksiyon kolonu
body.cyl((-0.13, 0.43, 1.10), 0.022, 0.04, METAL, rot=(90, 0, 0), segs=10) # kontak yuvası
body.build()

glass = Part("Glass")
glass.box((0, 0.585, 1.50), (1.80, 0.01, 0.74), GLASS, rot=(25.9, 0, 0))
glass.box((0, -0.82, 1.58), (1.54, 0.01, 0.40), GLASS)
glass.box((0.95, -0.02, 1.60), (0.01, 1.34, 0.46), GLASS)
glass.build()

HINGE = (-0.95, 0.70, 1.15)
door = Part("Door_L", pivot=HINGE)
door.box((-0.95, -0.02, 1.15), (0.06, 1.44, 0.40), PAINT)
door.box((-0.915, -0.02, 1.15), (0.02, 1.36, 0.36), TRIM)
door.box((-0.90, -0.10, 1.31), (0.05, 0.40, 0.04), TRIM)           # kol dayama
door.box((-0.89, 0.25, 1.24), (0.03, 0.10, 0.03), METAL)           # iç kol
door.box((-0.95, 0.66, 1.60), (0.05, 0.06, 0.50), PAINT)
door.box((-0.95, -0.72, 1.60), (0.05, 0.06, 0.50), PAINT)
door.box((-0.95, -0.03, 1.83), (0.05, 1.44, 0.05), PAINT)
door.box((-0.95, -0.03, 1.60), (0.01, 1.32, 0.44), GLASS)
door.build()

SW = (-0.40, 0.30, 1.26)
sw = Part("SteeringWheel", pivot=SW)
sw.torus(SW, 0.19, 0.02, TRIM, rot=(59, 0, 0))
sw.cyl(SW, 0.05, 0.05, TRIM, rot=(59, 0, 0), segs=10)
for a in (90, 210, 330):
    d = Vector((math.cos(math.radians(a)) * 0.18, math.sin(math.radians(a)) * 0.18, 0))
    d = euler((59, 0, 0)).to_3x3() @ d
    sw.seg(Vector(SW), Vector(SW) + d, 0.025, 0.012, TRIM)
sw.build()

KEY = (-0.13, 0.41, 1.10)
key = Part("IgnitionKey", pivot=KEY)
key.box((-0.13, 0.395, 1.10), (0.012, 0.03, 0.022), METAL)
key.box((-0.13, 0.37, 1.10), (0.008, 0.025, 0.045), METAL)
key.cyl((-0.13, 0.35, 1.075), 0.012, 0.004, METAL, rot=(0, 90, 0), segs=8)  # anahtarlık halkası
key.box((-0.13, 0.35, 1.035), (0.006, 0.02, 0.05), "IC_Leather")
key.build()

for name, x, y in (("Wheel_FL", -0.86, 1.65), ("Wheel_FR", 0.86, 1.65),
                   ("Wheel_RL", -0.86, -1.65), ("Wheel_RR", 0.86, -1.65)):
    w = Part(name, pivot=(x, y, 0.40))
    w.cyl((x, y, 0.40), 0.40, 0.28, RUBBER, rot=(0, 90, 0), segs=16)
    side = 1 if x > 0 else -1
    w.cyl((x + side * 0.142, y, 0.40), 0.20, 0.01, METAL, rot=(0, 90, 0), segs=12)
    w.build()

# yanan ışık parçaları (Unity'de açılıp kapatılır)
hg = Part("HeadlightGlow")
for x in (-0.64, 0.64):
    hg.cyl((x, 2.62, 0.93), 0.11, 0.01, GLOW, rot=(90, 0, 0))
hg.build()
tg = Part("TaillightGlow")
for x in (-0.85, 0.85):
    tg.box((x, -2.645, 1.05), (0.14, 0.01, 0.20), TGLOW)
tg.build()
gauge = Part("Gauges")
for x in (-0.51, -0.29):
    gauge.cyl((x, 0.43, 1.19), 0.06, 0.01, GAUGE, rot=(80, 0, 0))
gauge.build()
gg = Part("GaugeGlow")
for x in (-0.51, -0.29):
    gg.cyl((x, 0.425, 1.19), 0.058, 0.005, GAUGEGLOW, rot=(80, 0, 0))
gg.build()

def export(path, names):
    bpy.ops.object.select_all(action='DESELECT')
    for n in names:
        bpy.data.objects[n].select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'},
                             apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                             axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE',
                             add_leaf_bones=False, bake_anim=False, path_mode='STRIP')

export(os.path.join(OUT, "IC_Pickup.fbx"), [o.name for o in bpy.data.objects])

# ---------------------------------------------------------------- evrak ve el feneri
docs = Part("Documents", uv_fit=True)
docs.box((0, 0, 0.006), (0.24, 0.31, 0.012), "IC_Folder")
docs.box((0.008, 0.012, 0.0135), (0.21, 0.29, 0.003), "IC_Paper", rot=(0, 0, 4))
docs.box((-0.004, -0.006, 0.016), (0.21, 0.29, 0.002), "IC_Paper", rot=(0, 0, -2))
docs.box((-0.05, 0.13, 0.018), (0.01, 0.04, 0.004), METAL)          # ataş
docs.build()

# Flashlight.cs'teki modelle aynı oranlar (uzunluk ekseni +Y)
fl = Part("Flashlight")
fl.cyl((0, -0.03, 0), 0.0175, 0.16, "IC_FlashBody", rot=(90, 0, 0), segs=12)
fl.cyl((0, 0.08, 0), 0.025, 0.07, "IC_FlashBody", rot=(90, 0, 0), segs=12)
fl.cyl((0, 0.116, 0), 0.021, 0.004, "IC_FlashLens", rot=(90, 0, 0), segs=12)
fl.cyl((0.0175, -0.05, 0), 0.004, 0.02, METAL, rot=(90, 0, 0), segs=6)   # düğme
fl.build()
export(os.path.join(OUT, "IC_Props.fbx"), ["Documents", "Flashlight"])

# ---------------------------------------------------------------- eller
# Sağ el: parmaklar +Y yönünde, avuç -X'e bakar, orijin kavrama merkezinde.
SKIN, SLEEVE = "IC_Skin", "IC_Sleeve"
def hand(name, mirror):
    s = -1 if mirror else 1
    G = Vector((-0.035, 0.11, 0))
    P = lambda x, y, z: Vector((s * (x - G.x), y - G.y, z - G.z))
    h = Part(name)
    h.box(P(0, 0.045, 0), (0.028, 0.095, 0.085), SKIN)            # avuç
    for i, z in enumerate((0.03, 0.01, -0.01, -0.03)):
        L = 1.0 - abs(i - 1.2) * 0.12
        k = P(0, 0.092, z)
        a = P(0, 0.092 + 0.042 * L, z)
        b = P(-0.038 * L, 0.10 + 0.045 * L, z)
        c = P(-0.05 * L, 0.10 + 0.012 * L, z)
        h.seg(k, a, 0.02, 0.018, SKIN)
        h.seg(a, b, 0.018, 0.017, SKIN)
        h.seg(b, c, 0.016, 0.015, SKIN)
    t0, t1, t2 = P(-0.012, 0.02, 0.04), P(-0.04, 0.055, 0.05), P(-0.06, 0.085, 0.035)
    h.seg(t0, t1, 0.024, 0.022, SKIN)
    h.seg(t1, t2, 0.02, 0.019, SKIN)
    h.seg(P(0, -0.04, 0), P(0, 0.005, 0), 0.05, 0.065, SKIN)       # bilek
    h.seg(P(0.008, -0.62, 0), P(0.002, -0.03, 0), 0.10, 0.11, SLEEVE)   # kol (ceket), dirsek kadraj dışında kalsın
    h.box(P(0.002, -0.04, 0), (0.105, 0.025, 0.115), SLEEVE)        # manşet
    return h.build()
hand("Hand_R", False)
hand("Hand_L", True)
export(os.path.join(OUT, "IC_Hands.fbx"), ["Hand_R", "Hand_L"])

# ---------------------------------------------------------------- dokular (512px, sade, oyundaki gerçekçi dokulara yakın tonlar)
rng = np.random.default_rng(7)
def noise(n, scales=(4, 16, 64), weights=(0.5, 0.3, 0.2)):
    out = np.zeros((n, n))
    for sc, w in zip(scales, weights):
        g = rng.random((sc + 1, sc + 1))
        xs = np.linspace(0, sc, n, endpoint=False)
        i = xs.astype(int); f = xs - i
        f = f * f * (3 - 2 * f)
        a = g[i][:, i]; b = g[i][:, i + 1]; c = g[i + 1][:, i]; d = g[i + 1][:, i + 1]
        fx = f[None, :]; fy = f[:, None]
        out += w * ((a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy)
    return out

def save(name, rgb):
    n = rgb.shape[0]
    img = bpy.data.images.new(name, n, n, alpha=False)
    rgba = np.concatenate([np.clip(rgb, 0, 1), np.ones((n, n, 1))], axis=2)
    img.pixels.foreach_set(rgba.astype(np.float32).ravel())
    img.filepath_raw = os.path.join(TEX, name + ".png")
    img.file_format = 'PNG'
    img.save()

N = 512
nz = noise(N)
base = np.array([0.30, 0.34, 0.25])                                 # solmuş zeytin yeşili
rust = np.array([0.36, 0.20, 0.10])
rmask = np.clip((noise(N, (6, 24, 96), (0.5, 0.3, 0.2)) - 0.62) * 6, 0, 1)[..., None]
paint = base * (0.8 + 0.35 * nz[..., None])
paint = paint * (1 - rmask) + rust * (0.7 + 0.5 * nz[..., None]) * rmask
save("IC_CarPaint", paint * np.linspace(0.75, 1.05, N)[:, None, None])
save("IC_DarkPlastic", np.array([0.10, 0.10, 0.10]) * (0.8 + 0.4 * noise(N, (32, 128), (0.5, 0.5))[..., None]))
weave = (np.sin(np.arange(N) * 1.6)[None, :] * np.sin(np.arange(N) * 1.6)[:, None]) * 0.08
save("IC_SeatFabric", np.array([0.33, 0.24, 0.17]) * (0.75 + 0.35 * nz[..., None] + weave[..., None]))
save("IC_Rubber", np.array([0.06, 0.06, 0.06]) * (0.8 + 0.4 * noise(N, (64, 256), (0.5, 0.5))[..., None]))
save("IC_Metal", np.array([0.30, 0.30, 0.31]) * (0.7 + 0.5 * nz[..., None]))
save("IC_Grille", np.where((np.arange(N) // 24) % 2 == 0, 0.04, 0.16)[:, None, None] * np.ones((N, N, 3)))
paper = np.ones((N, N, 3)) * np.array([0.86, 0.84, 0.78]) * (0.92 + 0.1 * nz[..., None])
for row in range(70, N - 60, 16):                                   # daktilo satırları
    paper[row:row + 3, 50:int(N * (0.55 + 0.3 * rng.random()))] *= 0.35
paper[N - 40:N - 36, 60:220] *= 0.3
save("IC_Paper", paper)
save("IC_Folder", np.array([0.62, 0.50, 0.30]) * (0.85 + 0.2 * nz[..., None]))
save("IC_Skin", np.array([0.62, 0.46, 0.38]) * (0.9 + 0.15 * nz[..., None]))
save("IC_Sleeve", np.array([0.16, 0.15, 0.13]) * (0.75 + 0.35 * noise(N, (8, 64, 256), (0.4, 0.3, 0.3))[..., None]))
save("IC_Leather", np.array([0.20, 0.12, 0.07]) * (0.8 + 0.3 * nz[..., None]))
print("INTRO_MODELS_DONE", OUT)
