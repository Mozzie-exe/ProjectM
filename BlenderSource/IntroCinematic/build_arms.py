# Giriş sinematiği kolları: dirsekli kol (üst kol + ön kol) ve parmak pozlu eller.
# Açık Blender'da:  exec(open(r"<bu dosya>").read(), {"PROJECT": r"<proje>"})
# Sadece "IC_Arms" koleksiyonunu siler/kurar. Çıktı: Assets/Models/IntroCinematic/IC_Arms.fbx
#
# Koordinatlar Unity'deki el çerçevesinde yazılır (U), Blender'a (x, z, y) olarak çevrilir;
# IntroCinematicBuilder.MeshGO'nun 180° düzeltmesiyle Unity'de aynen (x, y, z) olur.
# Sağ el çerçevesi: +Z parmak yönü, +Y başparmak tarafı, avuç -X'e bakar.
# Orijin = kavrama merkezi (direksiyon/fener ekseni buradan +Y boyunca geçer). Bütün el pozlarında aynı,
# böylece poz değişirken eldeki eşya kaymaz. Bilek eklemi WRIST'te.
# Kol parçaları: pivot proksimal eklemde, +Z boyunca uzanır (UpperArm: omuz->dirsek, Forearm: dirsek->bilek).
# Ölçüler IntroCinematicBuilder'daki UpperLen / ForeLen / WristLocal / PinchPoint ile aynı olmalı.
import bpy, bmesh, sys, os, math
from mathutils import Matrix, Vector as V

if "PROJECT" not in globals():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    PROJECT = argv[0] if argv else os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(PROJECT, "Assets", "Models", "IntroCinematic")
COLL = "IC_Arms"

UPPER_LEN, FORE_LEN = 0.34, 0.31

if COLL in bpy.data.collections:
    COLLECTION = bpy.data.collections[COLL]
    for o in list(COLLECTION.objects):
        me = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if me and me.users == 0:
            bpy.data.meshes.remove(me)
else:
    COLLECTION = bpy.data.collections.new(COLL)
    bpy.context.scene.collection.children.link(COLLECTION)

def mat(name):
    return bpy.data.materials.get(name) or bpy.data.materials.new(name)

SKIN, SLEEVE = "IC_Skin", "IC_Sleeve"

class Mesh:
    def __init__(self, name, mirror=False):
        self.name, self.s = name, (-1 if mirror else 1)
        self.bm = bmesh.new()
        self.mats = []

    def B(self, p):
        """Unity el koordinatı -> Blender (sol elde x aynalanır)."""
        return V((self.s * p[0], p[2], p[1]))

    def _tag(self, before, m):
        if m not in self.mats:
            self.mats.append(m)
        idx = self.mats.index(m)
        new = [f for f in self.bm.faces if f not in before]
        for f in new:
            f.material_index = idx
        bmesh.ops.recalc_face_normals(self.bm, faces=new)

    def tube(self, pts, radii, m, segs=7, squash=1.0):
        """Noktalar boyunca kapalı uçlu boru (parmak, bilek). squash: Unity Y yönünde basıklık."""
        before = set(self.bm.faces)
        pts = [V(p) for p in pts]
        rings = []
        for i, p in enumerate(pts):
            if i == 0:
                t = pts[1] - p
            elif i == len(pts) - 1:
                t = p - pts[i - 1]
            else:
                t = (pts[i + 1] - p).normalized() + (p - pts[i - 1]).normalized()
            t.normalize()
            ref = V((0, 1, 0)) if abs(t.y) < 0.9 else V((1, 0, 0))
            u = t.cross(ref).normalized()
            v = t.cross(u)
            ring = []
            for k in range(segs):
                a = 2 * math.pi * k / segs
                q = p + (u * math.cos(a) + v * math.sin(a) * squash) * radii[i]
                ring.append(self.bm.verts.new(self.B(q)))
            rings.append(ring)
        for i in range(len(rings) - 1):
            a, b = rings[i], rings[i + 1]
            for k in range(segs):
                k2 = (k + 1) % segs
                self.bm.faces.new((a[k], a[k2], b[k2], b[k]))
        self.bm.faces.new(list(reversed(rings[0])))
        self.bm.faces.new(rings[-1])
        self._tag(before, m)

    def box(self, lo, hi, m):
        before = set(self.bm.faces)
        a, b = self.B(lo), self.B(hi)
        c = (a + b) / 2
        size = V((abs(b.x - a.x), abs(b.y - a.y), abs(b.z - a.z)))
        bmesh.ops.create_cube(self.bm, size=1.0, matrix=Matrix.Translation(c) @ Matrix.Diagonal((*size, 1)))
        self._tag(before, m)

    def capsule(self, z0, z1, r0, r1, m, segs=10, cap=4):
        """Z ekseni boyunca, iki ucu yarım küre kapaklı konik gövde (kol parçaları)."""
        before = set(self.bm.faces)
        prof = []
        for i in range(cap, 0, -1):                  # başlangıç kapağı
            a = math.pi / 2 * i / cap
            prof.append((z0 - math.sin(a) * r0, math.cos(a) * r0))
        prof += [(z0, r0), (z1, r1)]
        for i in range(1, cap + 1):                  # bitiş kapağı
            a = math.pi / 2 * i / cap
            prof.append((z1 + math.sin(a) * r1, math.cos(a) * r1))
        rings = []
        for z, r in prof:
            rings.append([self.bm.verts.new(self.B((math.cos(2 * math.pi * k / segs) * max(r, 1e-4),
                                                     math.sin(2 * math.pi * k / segs) * max(r, 1e-4), z)))
                          for k in range(segs)])
        for i in range(len(rings) - 1):
            a, b = rings[i], rings[i + 1]
            for k in range(segs):
                k2 = (k + 1) % segs
                self.bm.faces.new((a[k], a[k2], b[k2], b[k]))
        self.bm.faces.new(list(reversed(rings[0])))
        self.bm.faces.new(rings[-1])
        self._tag(before, m)

    def build(self):
        bm = self.bm
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        bm.normal_update()
        uv = bm.loops.layers.uv.new("UVMap")
        for f in bm.faces:
            n = f.normal
            ax = max(range(3), key=lambda i: abs(n[i]))
            ui, vi = [(1, 2), (0, 2), (0, 1)][ax]
            for l in f.loops:
                l[uv].uv = (l.vert.co[ui], l.vert.co[vi])
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        for m in self.mats:
            me.materials.append(mat(m))
        me.shade_smooth()
        me.set_sharp_from_angle(angle=math.radians(50))
        ob = bpy.data.objects.new(self.name, me)
        COLLECTION.objects.link(ob)
        return ob

# ---------------------------------------------------------------- el
# Parmaklar: (y, boy çarpanı, boğum z). Bükülmeler -X'e doğru (avuç içine).
FINGERS = [(0.029, 1.00, -0.012), (0.009, 1.08, -0.008), (-0.011, 1.00, -0.012), (-0.030, 0.82, -0.020)]
PHALANX = (0.045, 0.027, 0.021)
RADII = (0.0100, 0.0094, 0.0088, 0.0080)

def finger_pts(y, L, kz, bends):
    p = V((0.020, y, kz))
    pts, th = [p.copy()], 0.0
    for ln, b in zip(PHALANX, bends):
        th += math.radians(b)
        p = p + V((-math.sin(th), 0, math.cos(th))) * ln * L
        pts.append(p.copy())
    return pts

GRIP = (50, 75, 55)          # silindir kavrama (direksiyon, fener, kapı kolu)
THUMB_BASE = (0.018, 0.030, -0.092)
# Gevşek yumruk: r=0.021 silindiri sarar, eksen el yerelinde (-0.018, y, -0.032) (fener, kapı kolu)
HOLD = [(73, 52, 48), (79, 56, 48), (73, 52, 48), (58, 40, 44)]
POSES = {
    # ad: (her parmak için bükülme, başparmak noktaları)
    "Grip":  ([GRIP] * 4,
              [THUMB_BASE, (-0.008, 0.052, -0.055), (-0.026, 0.050, -0.025), (-0.036, 0.040, -0.004)]),
    "Relax": ([(22, 30, 22), (25, 34, 25), (28, 38, 26), (32, 42, 30)],
              [THUMB_BASE, (-0.002, 0.058, -0.060), (-0.014, 0.064, -0.032), (-0.022, 0.060, -0.010)]),
    "Hold":  (HOLD,
              [THUMB_BASE, (-0.0064, 0.046, -0.0639), (-0.044, 0.054, -0.0539), (-0.0515, 0.050, -0.0261)]),
    # İşaret parmağı ile başparmak arasında anahtar; diğer üç parmak fener tutmaya devam eder.
    "Pinch": ([(40, 45, 25), HOLD[1], HOLD[2], HOLD[3]],
              [THUMB_BASE, (0.010, 0.055, -0.050), (-0.010, 0.060, 0.000), (-0.022, 0.054, 0.024)]),
    "Open":  ([(4, 6, 4), (3, 5, 4), (4, 6, 5), (6, 8, 6)],
              [THUMB_BASE, (0.006, 0.062, -0.068), (0.002, 0.078, -0.044), (0.000, 0.088, -0.022)]),
}

def hand(name, mirror, pose):
    bends, thumb = POSES[pose]
    h = Mesh(name, mirror)
    h.box((0.004, -0.043, -0.108), (0.034, 0.043, -0.010), SKIN)                    # avuç
    h.tube([(0.020, 0.0, -0.170), (0.021, 0.0, -0.118), (0.020, 0.0, -0.096)],      # bilek (manşetin içine girer)
           (0.026, 0.024, 0.026), SKIN, segs=8, squash=1.3)
    for (y, L, kz), b in zip(FINGERS, bends):
        h.tube(finger_pts(y, L, kz, b), RADII, SKIN, segs=6)
    h.tube([V(p) for p in thumb], (0.015, 0.0125, 0.0105, 0.009), SKIN, segs=6)
    return h.build()

names = []
for side, mirror in (("R", False), ("L", True)):
    for pose in POSES:
        n = "Hand_%s_%s" % (side, pose)
        hand(n, mirror, pose)
        names.append(n)

# ---------------------------------------------------------------- kol parçaları (sol/sağ aynı)
up = Mesh("UpperArm")
up.capsule(0.0, UPPER_LEN, 0.047, 0.042, SLEEVE)
up.build()
fo = Mesh("Forearm")
fo.capsule(0.0, FORE_LEN - 0.045, 0.041, 0.035, SLEEVE)
fo.capsule(FORE_LEN - 0.050, FORE_LEN + 0.008, 0.039, 0.038, SLEEVE, cap=2)       # manşet
fo.build()
names += ["UpperArm", "Forearm"]

if bpy.context.object and bpy.context.object.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.select_all(action='DESELECT')
COLLECTION.hide_viewport = False   # gizli obje seçilemez, FBX boş çıkar
for n in names:
    bpy.data.objects[n].hide_set(False)
    bpy.data.objects[n].select_set(True)
assert len(bpy.context.selected_objects) == len(names), "dışa aktarılacak objeler seçilemedi"
bpy.context.view_layer.objects.active = bpy.data.objects[names[0]]
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "IC_Arms.fbx"), use_selection=True,
                         object_types={'MESH'}, apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                         axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE',
                         add_leaf_bones=False, bake_anim=False, path_mode='STRIP')
result = {n: len(bpy.data.objects[n].data.polygons) for n in names}
