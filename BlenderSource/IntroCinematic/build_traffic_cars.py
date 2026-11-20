# Giriş sinematiğinde yoldan geçen iki araba (marka/logo yok):
#   IC_TrafficWagon.fbx  : 70'ler uzun station wagon, ahşap kaplama yanlar, açık yeşil kaput/tavan, krom tampon
#   IC_TrafficPickup.fbx : 60'lar pikap, yuvarlak kabin, açık kasa, solmuş yeşil + beyaz ön panel
# Gece ve hızlı geçtikleri için siluet + far/stop lambası öncelikli; iç detay yok.
# Çalıştırma: blender --background --factory-startup --python build_traffic_cars.py -- <proje>
# Ölçü metre. Araba +Y'ye bakar, +Z yukarı (build_car_4x4.py ile aynı eksenler ve FBX ayarları).
import bpy, bmesh, sys, os, math
import numpy as np
from mathutils import Matrix, Vector as V

if "PROJECT" not in globals():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    PROJECT = argv[0] if argv else os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(PROJECT, "Assets", "Models", "IntroCinematic")
TEX = os.path.join(OUT, "Textures")
os.makedirs(TEX, exist_ok=True)

def mat(name):
    return bpy.data.materials.get(name) or bpy.data.materials.new(name)

# Mevcut (4x4) malzemeler: Unity'de aynı adla eşlenir
GLASS, RUBBER, METAL, CHROME = "IC_Glass", "IC_Rubber", "IC_Metal", "IC_Chrome"
TRIM, SEAT, GRILLE = "IC_DarkPlastic", "IC_SeatFabric", "IC_Grille"
LENS, LAMP_RED, GLOW, TGLOW = "IC_LampLens", "IC_TailLens", "IC_HeadlightGlow", "IC_TaillightGlow"
# Yeni
W_TOP, W_WOOD, T_PAINT, T_WHITE = "IC_WagonTop", "IC_WagonWood", "IC_TruckPaint", "IC_TruckWhite"


class Part:
    def __init__(self, coll, name, pivot=(0, 0, 0), smooth=False):
        self.coll, self.name, self.pivot, self.smooth = coll, name, V(pivot), smooth
        self.bm = bmesh.new()
        self.mats = []

    def _tag(self, before, m):
        if m not in self.mats:
            self.mats.append(m)
        idx = self.mats.index(m)
        for f in self.bm.faces:
            if f not in before:
                f.material_index = idx

    def box2(self, a, b, m):
        a, b = V(a), V(b)
        n = set(self.bm.faces)
        M = Matrix.Translation((a + b) / 2) @ Matrix.Diagonal((abs(b.x - a.x), abs(b.y - a.y), abs(b.z - a.z), 1))
        bmesh.ops.create_cube(self.bm, size=1.0, matrix=M)
        self._tag(n, m)

    def seg(self, a, b, w, h, m):
        a, b = V(a), V(b)
        z = (b - a).normalized()
        x = V((0, 0, 1)).cross(z)
        if x.length < 1e-6:
            x = V((1, 0, 0)).cross(z)
        x.normalize()
        y = z.cross(x)
        R = Matrix((x, y, z)).transposed().to_4x4()
        n = set(self.bm.faces)
        bmesh.ops.create_cube(self.bm, size=1.0, matrix=Matrix.Translation((a + b) / 2) @ R @ Matrix.Diagonal((w, h, (b - a).length, 1)))
        self._tag(n, m)

    def cyl(self, center, r, depth, m, axis='Y', segs=20):
        rot = {'Y': Matrix.Rotation(math.radians(-90), 4, 'X'), 'X': Matrix.Rotation(math.radians(90), 4, 'Y'),
               'Z': Matrix.Identity(4)}[axis]
        n = set(self.bm.faces)
        bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r, radius2=r,
                              depth=depth, matrix=Matrix.Translation(center) @ rot)
        self._tag(n, m)

    def tube(self, pts, r, m, segs=8):
        pts = [V(p) for p in pts]
        n = set(self.bm.faces)
        rings = []
        for i, p in enumerate(pts):
            t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
            ref = V((0, 0, 1)) if abs(t.z) < 0.9 else V((1, 0, 0))
            u = t.cross(ref).normalized()
            v = t.cross(u)
            rings.append([self.bm.verts.new(p + (u * math.cos(2 * math.pi * k / segs) + v * math.sin(2 * math.pi * k / segs)) * r)
                          for k in range(segs)])
        for a, b in zip(rings, rings[1:]):
            for k in range(segs):
                k2 = (k + 1) % segs
                self.bm.faces.new((a[k], a[k2], b[k2], b[k]))
        self.bm.faces.new(list(reversed(rings[0])))
        self.bm.faces.new(rings[-1])
        bmesh.ops.recalc_face_normals(self.bm, faces=[f for f in self.bm.faces if f not in n])
        self._tag(n, m)

    def prism(self, poly, x0, x1, m):
        """Yandan görünüş (y, z) çokgeni X boyunca x0..x1 kalınlaştırılır."""
        n = set(self.bm.faces)
        lo = [self.bm.verts.new((x0, y, z)) for y, z in poly]
        hi = [self.bm.verts.new((x1, y, z)) for y, z in poly]
        caps = [self.bm.faces.new(list(reversed(lo))), self.bm.faces.new(hi)]
        k = len(poly)
        for i in range(k):
            j = (i + 1) % k
            self.bm.faces.new((lo[i], lo[j], hi[j], hi[i]))
        new = [f for f in self.bm.faces if f not in n]
        bmesh.ops.recalc_face_normals(self.bm, faces=new)
        bmesh.ops.triangulate(self.bm, faces=[f for f in caps if len(f.verts) > 4], quad_method='BEAUTY', ngon_method='EAR_CLIP')
        self._tag(n, m)

    def lathe_x(self, center, profile, m, segs=20):
        """X ekseni etrafında döndürülmüş profil [(x, yarıçap)] (lastik)."""
        n = set(self.bm.faces)
        c = V(center)
        rings = [[self.bm.verts.new(c + V((s, r * math.cos(2 * math.pi * k / segs), r * math.sin(2 * math.pi * k / segs))))
                  for k in range(segs)] for s, r in profile]
        for a, b in zip(rings, rings[1:]):
            for k in range(segs):
                k2 = (k + 1) % segs
                self.bm.faces.new((a[k], a[k2], b[k2], b[k]))
        self.bm.faces.new(list(reversed(rings[0])))
        self.bm.faces.new(rings[-1])
        bmesh.ops.recalc_face_normals(self.bm, faces=[f for f in self.bm.faces if f not in n])
        self._tag(n, m)

    def build(self):
        bm = self.bm
        bm.normal_update()
        uv = bm.loops.layers.uv.new("UVMap")
        for f in bm.faces:
            ax = max(range(3), key=lambda i: abs(f.normal[i]))
            ui, vi = [(1, 2), (0, 2), (0, 1)][ax]
            for l in f.loops:
                l[uv].uv = (l.vert.co[ui], l.vert.co[vi])   # 1 UV = 1 metre
        bmesh.ops.translate(bm, verts=bm.verts, vec=-self.pivot)
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        for m in self.mats:
            me.materials.append(mat(m))
        ob = bpy.data.objects.new(self.name, me)
        ob.location = self.pivot
        self.coll.objects.link(ob)
        if self.smooth:
            me.shade_smooth()
            me.set_sharp_from_angle(angle=math.radians(35))
        return ob


def arch(cy, cz, R, z0, n=12):
    """Tekerlek kemeri: alt çizgi z0'dan başlayıp tekerlek merkezinin üstünden geçen yay (y artan sırada)."""
    s = max(-1.0, min(1.0, (z0 - cz) / R))
    a0 = math.asin(s)
    return [(cy + R * math.cos(a), cz + R * math.sin(a))
            for a in (math.pi - a0 - (math.pi - 2 * a0) * i / n for i in range(n + 1))]


def wheels(coll, P, axles, track, r, w, hub_r, hub_mat):
    names = []
    for side, x in (("L", -track), ("R", track)):
        for end, y in (("F", axles[0]), ("R", axles[1])):
            nm = "%s_Wheel_%s%s" % (P, end, side)
            p = Part(coll, nm, pivot=(x, y, r), smooth=True)
            h = w / 2
            p.lathe_x((x, y, r), [(-h, r * 0.62), (-h, r * 0.92), (-h * 0.7, r), (h * 0.7, r), (h, r * 0.92), (h, r * 0.62)], RUBBER)
            out = -1 if side == "L" else 1
            p.cyl((x + out * (h + 0.004), y, r), hub_r, 0.02, hub_mat, axis='X')
            p.cyl((x + out * (h + 0.016), y, r), hub_r * 0.35, 0.02, CHROME, axis='X', segs=12)
            p.build()
            names.append(nm)
    return names


def collection(name):
    if name in bpy.data.collections:
        c = bpy.data.collections[name]
        for o in list(c.objects):
            me = o.data
            bpy.data.objects.remove(o, do_unlink=True)
            if me and me.users == 0:
                bpy.data.meshes.remove(me)
        return c
    c = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(c)
    return c


# ================================================================= STATION WAGON
def build_wagon():
    C = collection("IC_TrafficWagon")
    P = "Wagon"
    R, FY, RY = 0.36, 1.70, -1.50
    body = Part(C, P + "_Body", smooth=False)
    z0 = 0.30
    poly = [(-2.86, z0)] + arch(RY, R, 0.45, z0) + arch(FY, R, 0.45, z0) + \
           [(2.88, z0), (2.93, 0.55), (2.91, 0.84), (2.80, 0.925), (1.05, 0.955), (-2.80, 0.955), (-2.87, 0.88), (-2.89, 0.45)]
    body.prism(poly, -0.99, 0.99, W_TOP)
    # Ahşap kaplama yan paneller (kemerleri izler) ve üstündeki krom şerit
    for s in (-1, 1):
        x0, x1 = (0.992, 1.004) if s > 0 else (-1.004, -0.992)
        pz0 = 0.42
        panel = [(-2.76, pz0)] + arch(RY, R, 0.49, pz0) + arch(FY, R, 0.49, pz0) + [(2.60, pz0), (2.60, 0.86), (-2.76, 0.86)]
        body.prism(panel, x0, x1, W_WOOD)
        cx0, cx1 = (1.0, 1.012) if s > 0 else (-1.012, -1.0)
        body.box2((cx0, -2.80, 0.862), (cx1, 2.80, 0.884), CHROME)
    # Cam kabin, tavan, direkler
    body.prism([(1.02, 0.95), (0.30, 1.375), (-2.72, 1.395), (-2.80, 0.95)], -0.88, 0.88, GLASS)
    body.box2((-0.905, -2.78, 1.39), (0.905, 0.34, 1.45), W_TOP)
    for s in (-1, 1):
        body.seg((s * 0.888, 1.02, 0.95), (s * 0.888, 0.30, 1.40), 0.07, 0.05, W_TOP)          # A
        for y in (-0.62, -1.82):                                                                 # B, C
            body.box2((s * 0.870, y - 0.05, 0.95), (s * 0.898, y + 0.05, 1.40), W_TOP)
        body.box2((s * 0.860, -2.80, 0.95), (s * 0.898, -2.66, 1.40), W_TOP)                    # D
    # Tavan bagajı
    for x in (-0.62, 0.62):
        body.tube([(x, 0.05, 1.47), (x, 0.0, 1.52), (x, -2.45, 1.52), (x, -2.50, 1.47)], 0.014, CHROME)
    for y in (-0.40, -1.70):
        body.box2((-0.62, y - 0.02, 1.51), (0.62, y + 0.02, 1.53), CHROME)
    # İç (cam arkası boş görünmesin)
    body.box2((-0.86, -2.70, 0.70), (0.86, 0.95, 0.94), TRIM)
    for y in (-0.05, -1.30):
        body.box2((-0.84, y - 0.10, 0.94), (0.84, y, 1.24), SEAT)
    # Ön: tampon, ızgara, far çerçeveleri, dörtlü yuvarlak far
    body.box2((-1.01, 2.86, 0.27), (1.01, 3.00, 0.47), CHROME)
    body.box2((-0.38, 2.88, 0.55), (0.38, 2.935, 0.83), GRILLE)
    for s in (-1, 1):
        body.box2((s * 0.42, 2.87, 0.57), (s * 0.97, 2.925, 0.81), CHROME)
        for x in (0.56, 0.82):
            body.cyl((s * x, 2.932, 0.69), 0.078, 0.02, LENS)
    # Arka: tampon, geniş stop lambaları
    body.box2((-1.01, -2.99, 0.27), (1.01, -2.86, 0.45), CHROME)
    for s in (-1, 1):
        body.box2((s * 0.50, -2.905, 0.52), (s * 0.96, -2.885, 0.68), LAMP_RED)
    body.build()

    hg = Part(C, P + "_HeadGlow")
    for s in (-1, 1):
        for x in (0.56, 0.82):
            hg.cyl((s * x, 2.946, 0.69), 0.064, 0.01, GLOW)
    hg.build()
    tg = Part(C, P + "_TailGlow")
    for s in (-1, 1):
        tg.box2((s * 0.52, -2.912, 0.535), (s * 0.94, -2.906, 0.665), TGLOW)
    tg.build()
    names = [P + "_Body", P + "_HeadGlow", P + "_TailGlow"] + wheels(C, P, (FY, RY), 0.80, R, 0.22, 0.21, CHROME)
    return C, names


# ================================================================= PİKAP
def build_pickup():
    C = collection("IC_TrafficPickup")
    P = "Pickup"
    R, FY, RY = 0.40, 1.55, -1.45
    body = Part(C, P + "_Body")
    z0 = 0.42
    # Kaput + çamurluklar
    front = [(0.72, z0)] + arch(FY, R, 0.49, z0) + [(2.40, z0), (2.47, 0.56), (2.49, 1.02), (2.40, 1.12), (0.76, 1.16), (0.72, 1.16)]
    body.prism(front, -0.98, 0.98, T_PAINT)
    # Kabin altı, cam, tavan, direkler
    body.box2((-0.93, -0.80, 0.42), (0.93, 0.73, 1.18), T_PAINT)
    body.prism([(0.70, 1.17), (0.46, 1.72), (-0.70, 1.74), (-0.78, 1.17)], -0.86, 0.86, GLASS)
    body.box2((-0.885, -0.80, 1.72), (0.885, 0.49, 1.80), T_PAINT)
    for s in (-1, 1):
        body.seg((s * 0.875, 0.71, 1.17), (s * 0.875, 0.46, 1.74), 0.07, 0.05, T_PAINT)
        body.box2((s * 0.845, -0.80, 1.17), (s * 0.888, -0.60, 1.74), T_PAINT)
    body.box2((-0.84, -0.80, 1.17), (0.84, -0.79, 1.30), T_PAINT)         # arka cam altı
    body.box2((-0.86, -0.72, 0.60), (0.86, 0.68, 1.16), TRIM)
    body.box2((-0.80, -0.72, 1.16), (0.80, -0.56, 1.46), SEAT)            # oturak sırtı
    # Kasa: taban, yanlar (arka kemerli), ön duvar, kapak
    body.box2((-0.86, -2.55, 0.70), (0.86, -0.90, 0.76), METAL)
    side = [(-2.55, 0.48)] + arch(RY, R, 0.49, 0.48) + [(-0.90, 0.48), (-0.90, 1.22), (-2.55, 1.22)]
    body.prism(side, 0.86, 0.98, T_PAINT)
    body.prism(side, -0.98, -0.86, T_PAINT)
    body.box2((-0.98, -0.92, 0.70), (0.98, -0.86, 1.22), T_PAINT)
    body.box2((-0.86, -2.58, 0.70), (0.86, -2.52, 1.20), T_PAINT)
    # Ön beyaz panel, ızgara, tampon, dörtlü far
    body.box2((-0.98, 2.47, 0.62), (0.98, 2.53, 1.00), T_WHITE)
    body.box2((-0.42, 2.525, 0.67), (0.42, 2.555, 0.85), GRILLE)
    for z in (0.71, 0.76, 0.81):
        body.box2((-0.42, 2.555, z - 0.008), (0.42, 2.57, z + 0.008), CHROME)
    body.box2((-1.02, 2.50, 0.40), (1.02, 2.64, 0.58), T_WHITE)
    for s in (-1, 1):
        for x in (0.57, 0.80):
            body.cyl((s * x, 2.538, 0.89), 0.082, 0.02, LENS)
    # Arka tampon ve stop lambaları (kasa köşelerinde)
    body.box2((-1.0, -2.66, 0.46), (1.0, -2.56, 0.58), METAL)
    for s in (-1, 1):
        body.cyl((s * 0.92, -2.562, 1.02), 0.05, 0.02, LAMP_RED)
    body.build()

    hg = Part(C, P + "_HeadGlow")
    for s in (-1, 1):
        for x in (0.57, 0.80):
            hg.cyl((s * x, 2.552, 0.89), 0.068, 0.01, GLOW)
    hg.build()
    tg = Part(C, P + "_TailGlow")
    for s in (-1, 1):
        tg.cyl((s * 0.92, -2.575, 1.02), 0.042, 0.01, TGLOW)
    tg.build()
    names = [P + "_Body", P + "_HeadGlow", P + "_TailGlow"] + wheels(C, P, (FY, RY), 0.80, R, 0.22, 0.17, METAL)
    return C, names


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

# Wagon kaput/tavan: soluk açık yeşil-gri, toz
c = np.ones((N, N, 3)) * np.array([0.50, 0.57, 0.50])
c = mix(c, [0.62, 0.66, 0.58], np.clip((pnoise(50, 1) - 0.45) * 1.2, 0, 0.4))
c = mix(c, [0.30, 0.28, 0.24], np.clip((pnoise(20, 2) - 0.6) * 2, 0, 0.45))
save(W_TOP, c)
# Ahşap kaplama: yatay damar (v = yükseklik), koyu kahve-bordo
warp = pnoise(40, 3) * 30 + pnoise(8, 4) * 4
grain = 0.5 + 0.5 * np.sin((yy + warp) * 2 * math.pi / 9)
c = np.ones((N, N, 3)) * np.array([0.25, 0.11, 0.07])
c = mix(c, [0.36, 0.18, 0.10], grain * 0.55)
c = mix(c, [0.14, 0.06, 0.04], np.clip((pnoise(3, 5) - 0.6) * 3, 0, 0.6) * grain)
c *= (0.9 + 0.2 * pnoise(60, 6))[..., None]
save(W_WOOD, c)
# Pikap: solmuş yeşil, boya atmış lekeler, pas
c = np.ones((N, N, 3)) * np.array([0.27, 0.48, 0.42])
c = mix(c, [0.45, 0.62, 0.56], np.clip((pnoise(45, 7) - 0.4) * 1.3, 0, 0.5))
c = mix(c, [0.32, 0.20, 0.12], np.clip((pnoise(5, 8) - 0.75) * 5, 0, 0.75))
c = mix(c, [0.22, 0.24, 0.20], np.clip((pnoise(25, 9) - 0.6) * 2, 0, 0.4))
save(T_PAINT, c)
c = np.ones((N, N, 3)) * np.array([0.80, 0.78, 0.70])
c = mix(c, [0.45, 0.40, 0.32], np.clip((pnoise(18, 10) - 0.55) * 2, 0, 0.5))
c = mix(c, [0.38, 0.22, 0.12], np.clip((pnoise(4, 11) - 0.8) * 6, 0, 0.7))
save(T_WHITE, c)

# ================================================================= DIŞA AKTAR
def export(C, names, fbx):
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT')
    C.hide_viewport = False
    for n in names:
        bpy.data.objects[n].hide_set(False)
        bpy.data.objects[n].select_set(True)
    assert len(bpy.context.selected_objects) == len(names), "dışa aktarılacak objeler seçilemedi"
    bpy.context.view_layer.objects.active = bpy.data.objects[names[0]]
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, fbx), use_selection=True,
                             object_types={'MESH'}, use_mesh_modifiers=True,
                             apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                             axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE',
                             add_leaf_bones=False, bake_anim=False, path_mode='STRIP')
    print(fbx, {n: len(bpy.data.objects[n].data.polygons) for n in names})

for builder, fbx in ((build_wagon, "IC_TrafficWagon.fbx"), (build_pickup, "IC_TrafficPickup.fbx")):
    C, names = builder()
    export(C, names, fbx)
