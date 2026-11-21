# Eskimiş kırsal sokak lambası (toprak yolun ana yoldan ayrıldığı yer):
#   ahşap elektrik direği (hafif yatık, çatlak gri ahşap) + kaz boynu metal kol + emaye çan şapka + ampul.
# Çıktı: Assets/Models/StreetLamp/StreetLamp_Old.fbx
#   StreetLamp_Pole : direk, kol, kelepçeler, kablo, sigorta kutusu
#   StreetLamp_Head : çan şapka (içi dışı)
#   StreetLamp_Bulb : ampul (Unity'de parlayan malzeme)
# Çalıştırma: blender --background --factory-startup --python build_street_lamp.py -- <proje>
# Ölçü metre (Unity'de dünya ölçeği K=1.78 ile büyütülür). +Z yukarı, kol +Y yönüne uzanır.
import bpy, bmesh, sys, os, math
import numpy as np
from mathutils import Matrix, Vector as V

if "PROJECT" not in globals():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    PROJECT = argv[0] if argv else os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(PROJECT, "Assets", "Models", "StreetLamp")
TEX = os.path.join(OUT, "Textures")
os.makedirs(TEX, exist_ok=True)

WOOD, ENAMEL, IRON, BULB = "SL_Wood", "SL_Enamel", "SL_Iron", "SL_BulbGlow"


def mat(name):
    return bpy.data.materials.get(name) or bpy.data.materials.new(name)


class Part:
    def __init__(self, name, smooth=False):
        self.name, self.smooth = name, smooth
        self.bm = bmesh.new()
        self.mats = []

    def _tag(self, before, m):
        if m not in self.mats:
            self.mats.append(m)
        idx = self.mats.index(m)
        for f in self.bm.faces:
            if f not in before:
                f.material_index = idx

    def box(self, center, size, m, rot=None):
        n = set(self.bm.faces)
        M = Matrix.Translation(center) @ (rot or Matrix.Identity(4)) @ Matrix.Diagonal((*size, 1))
        bmesh.ops.create_cube(self.bm, size=1.0, matrix=M)
        self._tag(n, m)

    def tube(self, pts, radii, m, segs=10, caps=True):
        """Noktalar boyunca boru; radii tek sayı ya da her nokta için yarıçap."""
        pts = [V(p) for p in pts]
        if not isinstance(radii, (list, tuple)):
            radii = [radii] * len(pts)
        n = set(self.bm.faces)
        rings = []
        prev_u = None
        for i, p in enumerate(pts):
            t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
            if prev_u is None:
                ref = V((1, 0, 0)) if abs(t.x) < 0.9 else V((0, 1, 0))
                u = t.cross(ref).normalized()
            else:   # paralel taşıma: boru bükülürken kıvrılmasın
                u = (prev_u - t * prev_u.dot(t)).normalized()
            prev_u = u
            v = t.cross(u)
            r = radii[i]
            rings.append([self.bm.verts.new(p + (u * math.cos(2 * math.pi * k / segs) + v * math.sin(2 * math.pi * k / segs)) * r)
                          for k in range(segs)])
        for a, b in zip(rings, rings[1:]):
            for k in range(segs):
                k2 = (k + 1) % segs
                self.bm.faces.new((a[k], a[k2], b[k2], b[k]))
        if caps:
            self.bm.faces.new(list(reversed(rings[0])))
            self.bm.faces.new(rings[-1])
        bmesh.ops.recalc_face_normals(self.bm, faces=[f for f in self.bm.faces if f not in n])
        self._tag(n, m)

    def lathe_z(self, center, profile, m, segs=24, close=True):
        """Z ekseni etrafında döndürülmüş profil [(z, yarıçap)]; uçlar kapatılır (yarıçap > 0 ise)."""
        c = V(center)
        n = set(self.bm.faces)
        rings = [[self.bm.verts.new(c + V((r * math.cos(2 * math.pi * k / segs), r * math.sin(2 * math.pi * k / segs), z)))
                  for k in range(segs)] for z, r in profile]
        for a, b in zip(rings, rings[1:]):
            for k in range(segs):
                k2 = (k + 1) % segs
                self.bm.faces.new((a[k], a[k2], b[k2], b[k]))
        if close:
            self.bm.faces.new(list(reversed(rings[0])))
            self.bm.faces.new(rings[-1])
        bmesh.ops.recalc_face_normals(self.bm, faces=[f for f in self.bm.faces if f not in n])
        self._tag(n, m)

    def build(self, coll):
        bm = self.bm
        bm.normal_update()
        uv = bm.loops.layers.uv.new("UVMap")
        for f in bm.faces:
            ax = max(range(3), key=lambda i: abs(f.normal[i]))
            ui, vi = [(1, 2), (0, 2), (0, 1)][ax]
            for l in f.loops:
                l[uv].uv = (l.vert.co[ui], l.vert.co[vi])   # 1 UV = 1 metre
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        for m in self.mats:
            me.materials.append(mat(m))
        ob = bpy.data.objects.new(self.name, me)
        coll.objects.link(ob)
        if self.smooth:
            me.shade_smooth()
            me.set_sharp_from_angle(angle=math.radians(40))
        return ob


def collection(name):
    if name in bpy.data.collections:
        c = bpy.data.collections[name]
        for o in list(c.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        return c
    c = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(c)
    return c


# ================================================================= MODEL
H = 7.6                     # direk boyu
LEAN = math.radians(2.2)    # yola doğru hafif yatık (eski direk)
ARM_Z = 6.3                 # kolun direğe bağlandığı yükseklik
ARM_REACH = 1.55            # kolun yatay uzanımı


def pole_axis(z):
    """Direğin z yüksekliğindeki merkezi (yatıklık +Y yönünde, kolla aynı tarafa)."""
    return V((0.0, math.sin(LEAN) * z, math.cos(LEAN) * z))


def build():
    C = collection("StreetLamp")

    pole = Part("StreetLamp_Pole", smooth=True)
    # Ahşap direk: aşağıdan yukarı incelir, hafif yamuk (eski ağaç gövdesi)
    zs = [0.0, 0.6, 1.8, 3.2, 4.6, 5.8, 6.8, H]
    pts = [pole_axis(z) + V((0.012 * math.sin(z * 1.3), 0.0, 0.0)) for z in zs]
    pole.tube(pts, [0.15 - 0.05 * z / H for z in zs], WOOD, segs=12)
    # Tepe: demir başlık
    pole.tube([pole_axis(H - 0.02), pole_axis(H + 0.05)], 0.105, IRON, segs=12)

    # Kaz boynu kol: direkten çıkar, yukarı kavis yapar, uçta aşağı döner
    base = pole_axis(ARM_Z)
    arm = []
    for i in range(13):
        s = i / 12
        y = 0.10 + ARM_REACH * s
        z = ARM_Z + 0.55 * math.sin(math.pi * min(s * 1.15, 1.0)) * (1 - s * 0.35) - 0.12 * s ** 3
        arm.append(V((0.0, base.y + y, z)))
    # Uç: aşağı kıvrılan kısa boyun (çan şapka buna asılı)
    tip = arm[-1]
    arm += [tip + V((0, 0.10, -0.06)), tip + V((0, 0.14, -0.18))]
    pole.tube(arm, 0.028, IRON, segs=8)
    # Alt destek çubuğu (kolu direğe bağlayan çapraz)
    pole.tube([pole_axis(ARM_Z - 0.75) + V((0, 0.12, 0)), V((0, base.y + 0.95, ARM_Z + 0.36))], 0.016, IRON, segs=6)
    # Kelepçeler (direğe sarılı iki demir bant)
    for z in (ARM_Z, ARM_Z - 0.75):
        c = pole_axis(z)
        r = 0.15 - 0.05 * z / H + 0.008
        pole.lathe_z(c + V((0, 0, -0.04)), [(0.0, r), (0.08, r)], IRON, segs=12, close=False)
    # Sigorta kutusu ve direkten yukarı çıkan kablo
    pole.box(pole_axis(3.0) + V((0, -0.17, 0)), (0.22, 0.10, 0.32), IRON)
    cable = [pole_axis(z) + V((0.0, -0.13, 0.0)) for z in (3.16, 4.2, 5.2, ARM_Z - 0.1)] + [base + V((0, 0.05, 0.02))]
    pole.tube(cable, 0.010, IRON, segs=5)
    # Tırmanma demirleri
    for i, z in enumerate((2.2, 2.7, 3.2, 3.7, 4.2)):
        c = pole_axis(z)
        side = -1 if i % 2 else 1
        pole.tube([c + V((side * 0.12, 0, 0)), c + V((side * 0.26, 0, 0.01))], 0.012, IRON, segs=5)
    pole_ob = pole.build(C)

    # Çan şapka: dışı ve içi (alttan bakınca görünür), kalın kenar
    neck = arm[-1]
    top = neck + V((0, 0, -0.02))
    head = Part("StreetLamp_Head", smooth=True)
    outer = [(0.0, 0.05), (-0.04, 0.085), (-0.10, 0.15), (-0.17, 0.25), (-0.22, 0.33), (-0.235, 0.345)]
    inner = [(-0.235, 0.33), (-0.215, 0.31), (-0.16, 0.235), (-0.09, 0.135), (-0.04, 0.075), (-0.012, 0.045)]
    head.lathe_z(top, outer + inner, ENAMEL, segs=28)
    head.lathe_z(top + V((0, 0, -0.02)), [(0.0, 0.03), (-0.07, 0.03)], IRON, segs=12)   # duy
    head_ob = head.build(C)

    bulb = Part("StreetLamp_Bulb", smooth=True)
    bz = top.z - 0.15
    prof = [(math.cos(a) * 0.055, math.sin(a) * 0.055) for a in np.linspace(0.15, math.pi - 0.05, 9)]
    bulb.lathe_z(V((top.x, top.y, bz)), [(-0.02, 0.025)] + [(-p[0] - 0.03, p[1]) for p in prof], BULB, segs=16)
    bulb_ob = bulb.build(C)

    print("bulb world", (top.x, top.y, bz - 0.03))
    return C, [pole_ob.name, head_ob.name, bulb_ob.name]


# ================================================================= DOKULAR (512, 1 UV = 1 m)
N = 512
yy, xx = np.mgrid[0:N, 0:N]


def pnoise(scale, seed):
    r = np.random.default_rng(seed).standard_normal((N, N))
    f = np.fft.fftfreq(N)
    fx, fy = np.meshgrid(f, f)
    out = np.real(np.fft.ifft2(np.fft.fft2(r) * np.exp(-(fx ** 2 + fy ** 2) * (scale ** 2) * 2)))
    out -= out.min()
    return out / max(out.max(), 1e-9)


def mix(a, b, t):
    return a * (1 - t[..., None]) + np.asarray(b) * t[..., None]


def save(name, rgb):
    rgba = np.concatenate([np.clip(rgb, 0, 1), np.ones((N, N, 1))], axis=2)[::-1]
    img = bpy.data.images.get(name) or bpy.data.images.new(name, N, N, alpha=False)
    img.pixels.foreach_set(rgba.astype(np.float32).ravel())
    img.filepath_raw = os.path.join(TEX, name + ".png")
    img.file_format = 'PNG'
    img.save()


# Güneşte ağarmış gri ahşap: dikey damar (v = yükseklik), boyuna çatlaklar, koyu lekeler
warp = pnoise(30, 21) * 18 + pnoise(6, 22) * 3
grain = 0.5 + 0.5 * np.sin((xx + warp) * 2 * math.pi / 7)
c = np.ones((N, N, 3)) * np.array([0.36, 0.33, 0.29])
c = mix(c, [0.47, 0.44, 0.39], grain * 0.5)
cracks = np.clip((np.abs(np.sin((xx + pnoise(60, 23) * 40) * 2 * math.pi / 64)) - 0.985) * 70, 0, 1)
c = mix(c, [0.10, 0.09, 0.08], cracks * 0.85)
c = mix(c, [0.20, 0.17, 0.13], np.clip((pnoise(12, 24) - 0.6) * 2.5, 0, 0.55))
c *= (0.88 + 0.24 * pnoise(50, 25))[..., None]
save(WOOD, c)
# Emaye: koyu yeşil boya, yer yer atmış, pas lekeleri
c = np.ones((N, N, 3)) * np.array([0.12, 0.22, 0.17])
c = mix(c, [0.20, 0.30, 0.24], np.clip((pnoise(40, 31) - 0.45) * 1.4, 0, 0.5))
c = mix(c, [0.36, 0.17, 0.07], np.clip((pnoise(3, 32) - 0.55) * 3 + (pnoise(6, 33) - 0.7) * 4, 0, 0.85))
save(ENAMEL, c)


# ================================================================= DIŞA AKTAR
def export(names, fbx):
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT')
    for n in names:
        bpy.data.objects[n].select_set(True)
    bpy.context.view_layer.objects.active = bpy.data.objects[names[0]]
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, fbx), use_selection=True,
                             object_types={'MESH'}, use_mesh_modifiers=True,
                             apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                             axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE',
                             add_leaf_bones=False, bake_anim=False, path_mode='STRIP')
    print(fbx, {n: len(bpy.data.objects[n].data.polygons) for n in names})


for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)
C, names = build()
export(names, "StreetLamp_Old.fbx")
