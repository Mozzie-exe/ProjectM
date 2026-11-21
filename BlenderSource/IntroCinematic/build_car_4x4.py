# Giriş sinematiği arabası: kısa şasili, brandalı eski tip 4x4 (logo/marka yok).
# İki şekilde çalışır:
#   1) Açık Blender'da (MCP):  exec(open(r"<bu dosya>").read(), {"PROJECT": r"<proje>"})
#   2) Arka planda:            blender --background --python build_car_4x4.py -- <proje>
# Sadece "IC_Car4x4" koleksiyonunu siler/kurar; sahnedeki diğer şeylere dokunmaz.
# Ölçü gerçek metre. Araba +Y yönüne bakar, sürücü solda (-X). Unity'de kök ~1.78x ölçeklenir.
# Kabin ölçüleri (göz, direksiyon, kontak, kapı menteşesi, koltuk) IntroCinematicBuilder ile eşleşir.
# Kural: görünen iki yüzey asla aynı düzlemde çakışmaz (titreme / z-fighting olmasın).
import bpy, bmesh, sys, os, math
import numpy as np
from mathutils import Matrix, Vector as V

if "PROJECT" not in globals():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    PROJECT = argv[0] if argv else os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(PROJECT, "Assets", "Models", "IntroCinematic")
TEX = os.path.join(OUT, "Textures")
os.makedirs(TEX, exist_ok=True)
COLL = "IC_Car4x4"

# ---------------------------------------------------------------- koleksiyon
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

_mats = {}
def mat(name):
    if name not in _mats:
        _mats[name] = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    return _mats[name]

def euler(rot):
    return Matrix.Rotation(math.radians(rot[2]), 4, 'Z') @ \
           Matrix.Rotation(math.radians(rot[1]), 4, 'Y') @ \
           Matrix.Rotation(math.radians(rot[0]), 4, 'X')

AXIS_ROT = {'Z': Matrix.Identity(4), 'X': Matrix.Rotation(math.radians(90), 4, 'Y'),
            'Y': Matrix.Rotation(math.radians(-90), 4, 'X')}

class Part:
    """Tek obje. Parçalar dünya koordinatında eklenir; pivot sonradan ayarlanır."""
    def __init__(self, name, pivot=(0, 0, 0), bevel=0.0, smooth=False):
        self.name, self.pivot, self.bevel, self.smooth = name, V(pivot), bevel, smooth
        self.bm = bmesh.new()
        self.mats = []
        self.fit = []   # (yüz aralığı, merkez, u, v, yarıçap): diske oturan UV (kadranlar)

    def _new(self, before):
        """before (önceki yüz kümesi) sonrası eklenen yüzler. bmesh silinen yüz yerlerini
        yeniden kullandığı için yeni yüzler listenin sonunda olmayabilir; indeks aralığı güvenilmez."""
        return [f for f in self.bm.faces if f not in before]

    def _tag(self, before, m):
        if m not in self.mats:
            self.mats.append(m)
        idx = self.mats.index(m)
        new = self._new(before)
        for f in new:
            f.material_index = idx
        return new

    def box(self, center, size, m, rot=(0, 0, 0)):
        n = set(self.bm.faces)
        M = Matrix.Translation(center) @ euler(rot) @ Matrix.Diagonal((size[0], size[1], size[2], 1))
        bmesh.ops.create_cube(self.bm, size=1.0, matrix=M)
        return self._tag(n, m)

    def box2(self, a, b, m):
        """İki karşı köşeyle kutu (sıra önemsiz)."""
        a, b = V(a), V(b)
        return self.box((a + b) / 2, (abs(b.x - a.x), abs(b.y - a.y), abs(b.z - a.z)), m)

    def cyl(self, center, r, depth, m, axis='Z', segs=16, fit=False):
        n = set(self.bm.faces)
        rot = AXIS_ROT[axis] if isinstance(axis, str) else axis
        bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segs,
                              radius1=r, radius2=r, depth=depth, matrix=Matrix.Translation(center) @ rot)
        rng = self._tag(n, m)
        if fit:  # Y ekseninde: -Y'den bakana göre u sağa, v yukarı
            u, v = (V((1, 0, 0)), V((0, 0, 1))) if axis == 'Y' else (rot.to_3x3() @ V((1, 0, 0)), rot.to_3x3() @ V((0, 1, 0)))
            self.fit.append((rng, V(center), u, v, r))
        return rng

    def seg(self, a, b, w, h, m, up=(0, 0, 1)):
        """a'dan b'ye dikdörtgen kesitli çubuk; w: (up x yön) ekseninde, h: diğerinde."""
        a, b = V(a), V(b)
        z = (b - a).normalized()
        x = V(up).cross(z)
        if x.length < 1e-6:
            x = V((1, 0, 0)).cross(z)
        x.normalize()
        y = z.cross(x)
        R = Matrix((x, y, z)).transposed().to_4x4()
        M = Matrix.Translation((a + b) / 2) @ R @ Matrix.Diagonal((w, h, (b - a).length, 1))
        n = set(self.bm.faces)
        bmesh.ops.create_cube(self.bm, size=1.0, matrix=M)
        return self._tag(n, m)

    def tube(self, pts, r, m, segs=12):
        """Noktalar boyunca yuvarlak boru (spor bar, basamak, tutamak)."""
        pts = [V(p) for p in pts]
        n = set(self.bm.faces)
        rings = []
        for i, p in enumerate(pts):
            if i == 0:
                t = (pts[1] - p).normalized()
            elif i == len(pts) - 1:
                t = (p - pts[i - 1]).normalized()
            else:
                t = ((pts[i + 1] - p).normalized() + (p - pts[i - 1]).normalized()).normalized()
            ref = V((0, 0, 1)) if abs(t.z) < 0.9 else V((1, 0, 0))
            u = t.cross(ref).normalized()
            v = t.cross(u)
            rings.append([self.bm.verts.new(p + (u * math.cos(2 * math.pi * k / segs) +
                                                 v * math.sin(2 * math.pi * k / segs)) * r)
                          for k in range(segs)])
        for i in range(len(rings) - 1):
            a, b = rings[i], rings[i + 1]
            for k in range(segs):
                k2 = (k + 1) % segs
                self.bm.faces.new((a[k], a[k2], b[k2], b[k]))
        self.bm.faces.new(list(reversed(rings[0])))
        self.bm.faces.new(rings[-1])
        bmesh.ops.recalc_face_normals(self.bm, faces=self._new(n))
        return self._tag(n, m)

    def prism(self, poly, a0, a1, m, axis='X'):
        """2B çokgen (diğer iki eksende), axis boyunca a0..a1 arasında kalınlaştırılır."""
        def P3(u, v, w):
            return {'X': V((w, u, v)), 'Y': V((u, w, v))}[axis]
        n = set(self.bm.faces)
        lo = [self.bm.verts.new(P3(u, v, a0)) for u, v in poly]
        hi = [self.bm.verts.new(P3(u, v, a1)) for u, v in poly]
        caps = [self.bm.faces.new(list(reversed(lo))), self.bm.faces.new(hi)]
        k = len(poly)
        for i in range(k):
            j = (i + 1) % k
            self.bm.faces.new((lo[i], lo[j], hi[j], hi[i]))
        bmesh.ops.recalc_face_normals(self.bm, faces=self._new(n))
        bmesh.ops.triangulate(self.bm, faces=[f for f in caps if len(f.verts) > 4],
                              quad_method='BEAUTY', ngon_method='EAR_CLIP')
        return self._tag(n, m)

    def lathe(self, center, profile, m, axis='X', segs=32, radial=None, closed=False):
        """profile: [(eksen boyunca konum, yarıçap)] eksen etrafında döndürülür.
        closed: profil kapalı halka (lastik, çerçeve); kapak yok.
        radial(i, k): i. kesit, k. açıda yarıçap çarpanı (diş deseni)."""
        n = set(self.bm.faces)
        c = V(center)
        ax, u, v = {'X': (V((1, 0, 0)), V((0, 1, 0)), V((0, 0, 1))),
                    'Y': (V((0, 1, 0)), V((1, 0, 0)), V((0, 0, 1)))}[axis]
        rings, new = [], []
        for i, (s, r) in enumerate(profile):
            ring = []
            for k in range(segs):
                a = 2 * math.pi * k / segs
                rr = r * (radial(i, k) if radial else 1.0)
                vt = self.bm.verts.new(c + ax * s + (u * math.cos(a) + v * math.sin(a)) * rr)
                ring.append(vt)
                new.append(vt)
            rings.append(ring)
        count = len(rings) if closed else len(rings) - 1
        for i in range(count):
            a, b = rings[i], rings[(i + 1) % len(rings)]
            for k in range(segs):
                k2 = (k + 1) % segs
                self.bm.faces.new((a[k], a[k2], b[k2], b[k]))
        if not closed:
            if profile[0][1] > 0:
                self.bm.faces.new(list(reversed(rings[0])))
            if profile[-1][1] > 0:
                self.bm.faces.new(rings[-1])
        bmesh.ops.remove_doubles(self.bm, verts=new, dist=1e-6)  # eksendeki uç noktalar
        faces = [f for f in self._new(n) if f.is_valid]
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        return self._tag(n, m)

    def arch(self, cy, cz, r_in, r_out, x0, x1, m, a0=12, a1=168, segs=20):
        """Tekerlek kemeri bandı (YZ düzleminde yay), x0..x1 kalınlığında."""
        n = set(self.bm.faces)
        verts = []
        for i in range(segs + 1):
            a = math.radians(a0 + (a1 - a0) * i / segs)
            ca, sa = math.cos(a), math.sin(a)
            verts.append([self.bm.verts.new(V((x, cy + ca * r, cz + sa * r)))
                          for (x, r) in ((x0, r_in), (x1, r_in), (x1, r_out), (x0, r_out))])
        for i in range(segs):
            a, b = verts[i], verts[i + 1]
            for k in range(4):
                k2 = (k + 1) % 4
                self.bm.faces.new((a[k], a[k2], b[k2], b[k]))
        self.bm.faces.new(list(reversed(verts[0])))
        self.bm.faces.new(verts[-1])
        bmesh.ops.recalc_face_normals(self.bm, faces=self._new(n))
        return self._tag(n, m)

    def build(self):
        bm = self.bm
        bm.normal_update()
        uv = bm.loops.layers.uv.new("UVMap")
        fitted = set()
        for faces, c, u, v, r in self.fit:
            for f in faces:
                if not f.is_valid:
                    continue
                fitted.add(f)
                for l in f.loops:
                    d = l.vert.co - c
                    l[uv].uv = (0.5 + d.dot(u) / (2 * r), 0.5 + d.dot(v) / (2 * r))
        for f in bm.faces:
            if f in fitted:
                continue
            nrm = f.normal
            ax = max(range(3), key=lambda i: abs(nrm[i]))
            u_i, v_i = [(1, 2), (0, 2), (0, 1)][ax]
            for l in f.loops:
                cc = l.vert.co
                l[uv].uv = (cc[u_i], cc[v_i])  # 1 UV = 1 metre (Unity'de malzeme döşemesi)
        bmesh.ops.translate(bm, verts=bm.verts, vec=-self.pivot)
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        for m in self.mats:
            me.materials.append(mat(m))
        ob = bpy.data.objects.new(self.name, me)
        ob.location = self.pivot
        COLLECTION.objects.link(ob)
        if self.smooth or self.bevel:
            me.shade_smooth()
            me.set_sharp_from_angle(angle=math.radians(40))
        if self.bevel:
            b = ob.modifiers.new("Bevel", 'BEVEL')
            b.width = self.bevel
            b.segments = 2
            b.limit_method = 'ANGLE'
            b.angle_limit = math.radians(40)
            b.harden_normals = True
        return ob

# ---------------------------------------------------------------- malzeme adları
PAINT, FLARE, TOP = "IC_CarPaint", "IC_Flare", "IC_SoftTop"
TRIM, GLASS, RUBBER, METAL = "IC_DarkPlastic", "IC_Glass", "IC_Rubber", "IC_Metal"
CHROME, RIM, SEAT, LEATHER = "IC_Chrome", "IC_Rim", "IC_SeatFabric", "IC_Leather"
LENS, LAMP_RED, GRILLE = "IC_LampLens", "IC_TailLens", "IC_Grille"
GLOW, TGLOW, GAUGE, GAUGEGLOW = "IC_HeadlightGlow", "IC_TaillightGlow", "IC_GaugeFace", "IC_GaugeGlow"

# ---------------------------------------------------------------- ölçüler
FRONT_AXLE, REAR_AXLE, TRACK, WHEEL_R = 1.38, -1.18, 0.80, 0.40
BELT = 1.20                    # kapı / kasa üst çizgisi
FLOOR = 0.60                   # kabin tabanı üstü
ROOF = 1.88                    # branda üstü
RZ0 = ROOF - 0.04              # branda altı
TUB_X = 0.88                   # kasa yan dış yüzü
TUB_FRONT, TUB_REAR = 0.80, -1.96
REAR_FACE = TUB_REAR - 0.045   # bagaj kapağı dış yüzü
DOOR_FRONT, DOOR_REAR = 0.70, -0.60
DASH_Y = 0.47                  # torpido ön yüzü
WS_TILT, WS_H = 25.9, 0.70     # ön cam eğimi ve boyu
def ws_point(x, h):
    t = math.radians(WS_TILT)
    return V((x, 0.80 - math.sin(t) * h, BELT + math.cos(t) * h))
WS_TOP = ws_point(0, WS_H)
SW = V((-0.40, 0.20, 1.26))    # direksiyon merkezi (Unity: WheelGripL/R ile aynı)
R59 = euler((59, 0, 0))

def arch_cut(cy, cz, r, zcut, front_first, n=16):
    """Kemerin zcut üstündeki kısmı + kesişim noktaları."""
    dy = math.sqrt(max(r * r - (zcut - cz) ** 2, 0))
    a_lo = math.degrees(math.asin((zcut - cz) / r))
    pts = [(cy + math.cos(math.radians(a)) * r, cz + math.sin(math.radians(a)) * r)
           for a in np.linspace(a_lo, 180 - a_lo, n)]
    pts = [(cy + dy, zcut)] + pts[1:-1] + [(cy - dy, zcut)]
    return pts if front_first else pts[::-1]

def xr(s, a, b):
    """s tarafında (±1) a..b mesafesi için sıralı x aralığı."""
    return (min(s * a, s * b), max(s * a, s * b))

# ================================================================= GÖVDE
body = Part("Body", bevel=0.012)

# Şasi, taban
for x in (-0.45, 0.45):
    body.box2((x - 0.05, -2.02, 0.40), (x + 0.05, 2.12, 0.50), METAL)
body.box2((-0.86, TUB_REAR, FLOOR - 0.045), (0.86, TUB_FRONT, FLOOR), PAINT)

# Arka çeyrek panelleri (tekerlek kemerli)
quarter = [(DOOR_REAR, BELT), (TUB_REAR + 0.05, BELT), (TUB_REAR, BELT - 0.05), (TUB_REAR, 0.56)] + \
          arch_cut(REAR_AXLE, WHEEL_R, 0.47, 0.56, front_first=False) + [(DOOR_REAR, 0.56)]
fender = [(0.84, 1.005), (2.05, 1.005), (2.10, 0.96), (2.10, 0.66)] + \
         arch_cut(FRONT_AXLE, WHEEL_R, 0.50, 0.66, front_first=True) + [(0.84, 0.66)]
for s in (-1, 1):
    body.prism(quarter, *xr(s, TUB_X - 0.04, TUB_X), PAINT, axis='X')
    x0, x1 = xr(s, TUB_X - 0.04, TUB_X)
    body.box2((x0, DOOR_REAR - 0.01, 0.56), (x1, DOOR_FRONT + 0.11, 0.625), PAINT)       # eşik
    body.box2((x0, DOOR_FRONT + 0.005, 0.62), (x1, TUB_FRONT + 0.05, BELT), PAINT)       # torpido yanı
    r0, r1 = xr(s, TUB_X - 0.07, TUB_X + 0.005)
    body.box2((r0, TUB_REAR + 0.01, BELT - 0.03), (r1, DOOR_REAR, BELT + 0.012), PAINT)  # kasa üst rayı
    # ön çamurluk + siyah kemer kabartmaları
    body.prism(fender, *xr(s, 0.79, 0.955), PAINT, axis='X')
    body.arch(FRONT_AXLE, WHEEL_R, 0.49, 0.565, *xr(s, 0.775, 1.0), FLARE)
    body.arch(REAR_AXLE, WHEEL_R, 0.46, 0.54, *xr(s, 0.83, 0.99), FLARE)
    # yan basamak ve ayakları
    body.tube([(s * 0.94, DOOR_REAR - 0.05, 0.50), (s * 0.94, DOOR_FRONT + 0.06, 0.50)], 0.035, METAL)
    for y in (DOOR_REAR + 0.05, DOOR_FRONT - 0.05):
        body.box2((s * 0.86, y - 0.02, 0.46), (s * 0.935, y + 0.02, 0.53), METAL)
    # kaput mandalı, menteşe
    body.box2((s * 0.817, 1.62, 1.03), (s * 0.833, 1.68, 1.13), RUBBER)
    body.box2((s * 0.78 - 0.02, 0.88, 1.15), (s * 0.78 + 0.02, 0.96, 1.19), METAL)
body.cyl((-TUB_X - 0.004, -1.55, 1.02), 0.055, 0.012, METAL, axis='X')                 # yakıt kapağı

# Bagaj kapağı
body.box2((-0.86, REAR_FACE, 0.58), (0.86, TUB_REAR - 0.005, BELT), PAINT)
body.box2((-0.10, REAR_FACE - 0.015, 1.08), (0.10, REAR_FACE, 1.11), TRIM)

# Torpido duvarı, kaput arkası, motor altı
body.box2((-0.84, 0.76, FLOOR), (0.84, 0.80, 1.14), METAL)
body.box2((-0.86, 0.76, 1.14), (0.86, 0.86, BELT - 0.005), PAINT)
body.box2((-0.80, 0.86, 0.50), (0.80, 2.06, 0.62), METAL)

# Kaput: hafif bombeli kabuk
hood = [(-0.815, 1.012), (-0.815, 1.125), (-0.72, 1.158), (0, 1.178), (0.72, 1.158), (0.815, 1.125), (0.815, 1.012),
        (0.795, 1.012), (0.795, 1.115), (0.71, 1.143), (0, 1.163), (-0.71, 1.143), (-0.795, 1.115), (-0.795, 1.012)]
body.prism(hood, 0.865, 2.14, PAINT, axis='Y')   # ızgara çerçevesinin üstünü örter (açık kalan ön dudak gölge yapıyordu)
body.box2((0.18, 1.55, 1.172), (0.46, 1.80, 1.192), TRIM)                                 # hava girişi

# Izgara: gövde renkli, 9 dikey yarık
GY0, GY1 = 2.095, 2.135
body.box2((-0.70, 2.08, 0.64), (0.70, 2.088, 1.08), GRILLE)
# çerçeve çamurluklara kadar uzanır (x 0.72..0.79 arası motor boşluğu görünüyordu)
body.box2((-0.80, GY0, 1.035), (0.80, GY1, 1.10), PAINT)
body.box2((-0.80, GY0, 0.62), (0.80, GY1, 0.70), PAINT)
for s in (-1, 1):
    body.box2((s * 0.34, GY0, 0.70), (s * 0.80, GY1, 1.035), PAINT)
bar_w, n_slot = 0.032, 9
slot_w = (0.68 - (n_slot - 1) * bar_w) / n_slot
for i in range(n_slot - 1):
    x = -0.34 + (i + 1) * slot_w + i * bar_w
    body.box2((x, GY0 + 0.004, 0.70), (x + bar_w, GY1 - 0.004, 1.035), PAINT)
# Farlar: krom halka + bombeli cam (yanan disk ayrı obje), sinyaller
for s in (-1, 1):
    hx = s * 0.53
    body.lathe((hx, 2.130, 0.885), [(0, 0.118), (0.018, 0.112), (0.018, 0.096), (0.006, 0.092)], CHROME,
               axis='Y', segs=24, closed=True)
    body.lathe((hx, 2.128, 0.885), [(0, 0.094), (0.016, 0.090), (0.022, 0.06), (0.024, 0.0)], LENS, axis='Y', segs=24)
    body.box2((hx - 0.055, 2.13, 0.725), (hx + 0.055, 2.148, 0.765), LENS)
# Ön tampon, çeki kancaları, karter koruma
body.box2((-0.98, 2.16, 0.50), (0.98, 2.30, 0.66), METAL)
for s in (-1, 1):
    body.tube([(s * 0.42, 2.29, 0.60), (s * 0.42, 2.36, 0.60), (s * 0.42, 2.36, 0.53)], 0.018, METAL, segs=8)
body.box2((-0.40, 1.70, 0.36), (0.40, 2.14, 0.40), METAL)

# Arka tampon, stoplar, stepne braketi
body.box2((-0.90, -2.13, 0.48), (0.90, REAR_FACE + 0.004, 0.62), METAL)
for s in (-1, 1):
    body.box2((s * 0.76 - 0.055, REAR_FACE - 0.010, 0.86), (s * 0.76 + 0.055, REAR_FACE + 0.005, 1.08), TRIM)
    body.box2((s * 0.76 - 0.045, REAR_FACE - 0.022, 0.87), (s * 0.76 + 0.045, REAR_FACE - 0.008, 1.07), LAMP_RED)
body.box2((-0.12, REAR_FACE - 0.035, 0.88), (0.22, REAR_FACE + 0.005, 1.12), METAL)

# Ön cam çerçevesi, silecekler, dikiz aynası, güneşlikler
for s in (-1, 1):
    body.seg(ws_point(s * 0.83, 0), ws_point(s * 0.83, WS_H), 0.07, 0.06, PAINT, up=(0, 1, 0))
body.seg(ws_point(-0.86, WS_H - 0.035), ws_point(0.86, WS_H - 0.035), 0.06, 0.07, PAINT, up=(0, 1, 0))
body.seg(ws_point(-0.86, 0.03), ws_point(0.86, 0.03), 0.06, 0.06, PAINT, up=(0, 1, 0))
for s in (-1, 1):
    body.seg((s * 0.30 - 0.20, 0.80, 1.215), (s * 0.30 + 0.18, 0.785, 1.24), 0.012, 0.01, TRIM)
body.box2((-0.11, WS_TOP.y + 0.02, WS_TOP.z - 0.17), (0.11, WS_TOP.y + 0.05, WS_TOP.z - 0.10), TRIM)
body.seg((0, WS_TOP.y + 0.035, WS_TOP.z - 0.10), (0, WS_TOP.y + 0.01, WS_TOP.z - 0.03), 0.015, 0.015, TRIM)
for s in (-1, 1):
    body.box2((s * 0.18, WS_TOP.y - 0.22, RZ0 - 0.035), (s * 0.62, WS_TOP.y - 0.03, RZ0 - 0.017), TRIM)

# Branda: tavan, astar, yan ve arka pencereli paneller
body.box2((-0.885, TUB_REAR - 0.02, RZ0), (0.885, WS_TOP.y - 0.01, ROOF), TOP)
body.box2((-0.86, TUB_REAR + 0.02, RZ0 - 0.012), (0.86, WS_TOP.y - 0.04, RZ0), TRIM)
for s in (-1, 1):
    lo, hi = xr(s, TUB_X - 0.022, TUB_X - 0.002)
    body.box2((lo, TUB_REAR, BELT + 0.012), (hi, DOOR_REAR, BELT + 0.10), TOP)
    body.box2((lo, TUB_REAR, ROOF - 0.12), (hi, DOOR_REAR, RZ0), TOP)
    body.box2((lo, TUB_REAR, BELT + 0.10), (hi, TUB_REAR + 0.14, ROOF - 0.12), TOP)
    body.box2((lo, DOOR_REAR - 0.12, BELT + 0.10), (hi, DOOR_REAR, ROOF - 0.12), TOP)
RY0, RY1 = TUB_REAR - 0.02, TUB_REAR
body.box2((-0.885, RY0, BELT + 0.005), (0.885, RY1, BELT + 0.10), TOP)
body.box2((-0.885, RY0, ROOF - 0.10), (0.885, RY1, RZ0), TOP)
for s in (-1, 1):
    body.box2((s * 0.62, RY0, BELT + 0.10), (s * 0.885, RY1, ROOF - 0.10), TOP)

# Spor bar (köpük kaplı) ve tavan lambası
for s in (-1, 1):
    body.tube([(s * 0.76, -0.98, FLOOR), (s * 0.76, -0.98, 1.70), (s * 0.70, -0.98, 1.79)], 0.042, TRIM)
    body.tube([(s * 0.76, -0.98, 1.70), (s * 0.76, -1.90, 1.66), (s * 0.76, -1.90, FLOOR)], 0.035, TRIM)
body.tube([(-0.70, -0.98, 1.79), (0.70, -0.98, 1.79)], 0.042, TRIM)
body.box2((-0.07, -0.25, RZ0 - 0.035), (0.07, -0.15, RZ0 - 0.008), LENS)

# ---------------------------------------------------------------- iç mekân: torpido
dash = [(DASH_Y, 0.92), (DASH_Y, 1.16), (0.50, 1.205), (0.76, 1.195), (0.78, 1.12), (0.78, 0.80), (0.60, 0.80)]
body.prism(dash, -0.84, 0.84, TRIM, axis='X')
body.box2((-0.635, DASH_Y - 0.002, 1.10), (-0.165, 0.52, 1.235), TRIM)                     # kadran zemini
body.box2((-0.66, DASH_Y - 0.05, 1.235), (-0.14, 0.62, 1.275), TRIM)                       # siper
for x in (-0.66, -0.165):
    body.box2((x, DASH_Y - 0.04, 1.10), (x + 0.025, 0.60, 1.235), TRIM)
# orta konsol: menfezler, radyo, klima düğmeleri
body.box2((-0.13, DASH_Y - 0.03, 0.86), (0.13, DASH_Y + 0.01, 1.20), TRIM)
for x0, x1 in ((-0.10, -0.01), (0.01, 0.10)):
    body.box2((x0, DASH_Y - 0.036, 1.11), (x1, DASH_Y - 0.028, 1.17), GRILLE)
body.box2((-0.10, DASH_Y - 0.040, 1.02), (0.10, DASH_Y - 0.028, 1.085), METAL)
body.box2((-0.065, DASH_Y - 0.044, 1.05), (0.03, DASH_Y - 0.038, 1.07), GAUGE)
for x in (-0.07, 0.0, 0.07):
    body.cyl((x, DASH_Y - 0.045, 0.96), 0.018, 0.03, METAL, axis='Y', segs=12)
for s in (-1, 1):
    body.box2((s * 0.70, DASH_Y - 0.036, 1.10), (s * 0.80, DASH_Y - 0.028, 1.17), GRILLE)
# torpido gözü ve tutamak
body.box2((0.16, DASH_Y - 0.012, 0.95), (0.70, DASH_Y + 0.004, 1.09), TRIM)
body.tube([(0.20, DASH_Y, 1.135), (0.24, DASH_Y - 0.06, 1.14), (0.62, DASH_Y - 0.06, 1.14), (0.66, DASH_Y, 1.135)], 0.014, TRIM, segs=8)
# kontak yuvası (sürücünün sağında)
body.box2((-0.19, 0.445, 1.04), (-0.07, DASH_Y + 0.005, 1.16), TRIM)
body.cyl((-0.13, 0.43, 1.10), 0.022, 0.03, CHROME, axis='Y', segs=14)
# direksiyon kolonu, sinyal kolu, pedallar
col_dir = V((0, 0.857, -0.515))
body.tube([SW + col_dir * 0.04, SW + col_dir * 0.30], 0.04, TRIM)
body.box2((-0.46, 0.36, 1.13), (-0.34, DASH_Y + 0.005, 1.22), TRIM)
body.seg((-0.44, 0.38, 1.20), (-0.60, 0.36, 1.19), 0.012, 0.012, TRIM)
for x, w in ((-0.52, 0.06), (-0.40, 0.08), (-0.28, 0.07)):
    body.box2((x - w / 2, 0.62, 0.66), (x + w / 2, 0.64, 0.74), RUBBER)
# tünel, vites, çekiş kolu, el freni, konsol, paspaslar
body.box2((-0.15, -0.95, FLOOR), (0.15, 0.76, 0.76), TRIM)
# vites kolu ayrı obje (GearLever): sinematikte boşa alınır
body.tube([(0.08, 0.04, 0.76), (0.08, 0.0, 0.92)], 0.009, METAL, segs=8)
body.cyl((0.08, -0.005, 0.935), 0.018, 0.035, TRIM, segs=10)
body.box2((-0.04, -0.56, 0.76), (0.04, -0.46, 0.80), TRIM)           # el freni körüğü
body.box2((-0.10, -0.95, 0.76), (0.10, -0.60, 0.90), TRIM)
for s in (-1, 1):
    body.box2((s * 0.20, -0.55, FLOOR + 0.004), (s * 0.80, 0.62, FLOOR + 0.018), RUBBER)

# Kova koltuklar. Minder üstü z=0.85 (DZ ile 10 cm alçak: oturan kafaya göre omuz minderden ~32 cm yukarıda,
# kollar minderin içine girmesin). Evrak ve fener yolcu minderinde durur.
# Döşeme ayrı obje: kalın pahla yastık gibi yuvarlak kenarlı (gövdenin 1.2 cm pahı kutu gibi duruyordu).
seats = Part("Seats", bevel=0.032)
DZ = -0.10
def bucket(cx):
    body.box2((cx - 0.20, -0.70, FLOOR), (cx + 0.20, -0.20, 0.80 + DZ), METAL)
    seats.box2((cx - 0.235, -0.72, 0.80 + DZ), (cx + 0.235, -0.16, 0.935 + DZ), SEAT)
    seats.box2((cx - 0.19, -0.70, 0.935 + DZ), (cx + 0.19, -0.18, 0.95 + DZ), SEAT)
    for s in (-1, 1):
        seats.box2((cx + s * 0.195, -0.72, 0.935 + DZ), (cx + s * 0.235, -0.18, 0.985 + DZ), SEAT)  # yan destek
        seats.box((cx + s * 0.205, -0.78, 1.25 + DZ), (0.075, 0.15, 0.58), SEAT, rot=(12, 0, -s * 8))  # sırt yan desteği
    seats.box((cx, -0.80, 1.25 + DZ), (0.48, 0.13, 0.62), SEAT, rot=(12, 0, 0))                # sırt
    seats.box((cx, -0.875, 1.665 + DZ), (0.27, 0.10, 0.17), SEAT, rot=(12, 0, 0))              # baş dayama
    for s in (-1, 1):
        body.seg((cx + s * 0.08, -0.845, 1.52 + DZ), (cx + s * 0.08, -0.865, 1.60 + DZ), 0.012, 0.012, METAL)
bucket(-0.42)
bucket(0.44)
seats.box2((-0.72, -1.62, FLOOR), (0.72, -1.12, 0.90), SEAT)                                # arka koltuk
seats.box((0, -1.72, 1.20), (1.40, 0.13, 0.56), SEAT, rot=(10, 0, 0))
seats_ob = seats.build()
seats_ob.modifiers["Bevel"].segments = 4

# ---------------------------------------------------------------- kapılar (sağ: gövdede sabit)
def door_shell(p, s):
    xo, xi = TUB_X + 0.04, TUB_X - 0.005
    p.box2((s * xo, DOOR_REAR + 0.005, 0.635), (s * xi, DOOR_FRONT - 0.005, BELT), PAINT)
    p.box2((s * xi, DOOR_REAR + 0.04, 0.68), (s * (TUB_X - 0.03), DOOR_FRONT - 0.04, BELT - 0.03), TRIM)  # döşeme
    p.box2((s * (TUB_X - 0.03), -0.30, 1.04), (s * (TUB_X - 0.075), 0.28, 1.09), TRIM)                     # kol dayama
    fo, fi = TUB_X + 0.03, TUB_X + 0.005                                                                     # branda çerçeve
    p.box2((s * fo, DOOR_REAR + 0.01, BELT), (s * fi, DOOR_REAR + 0.08, ROOF - 0.045), TOP)
    p.box2((s * fo, DOOR_REAR + 0.01, ROOF - 0.10), (s * fi, 0.50, ROOF - 0.045), TOP)
    p.seg((s * (TUB_X + 0.0175), 0.62, BELT), (s * (TUB_X + 0.0175), 0.50, ROOF - 0.07), 0.06, 0.025, TOP, up=(1, 0, 0))
    p.box2((s * xo, -0.48, 1.10), (s * (xo + 0.02), -0.36, 1.13), TRIM)                                    # dış kol
    for z in (0.80, 1.08):
        p.box2((s * xo, DOOR_FRONT - 0.07, z - 0.04), (s * (xo + 0.025), DOOR_FRONT + 0.01, z + 0.04), METAL)
    p.seg((s * xo, 0.60, 1.21), (s * (TUB_X + 0.16), 0.58, 1.33), 0.018, 0.018, TRIM)                       # ayna kolu
    p.box((s * (TUB_X + 0.17), 0.58, 1.36), (0.13, 0.035, 0.10), TRIM, rot=(0, 0, s * 8))
    p.box((s * (TUB_X + 0.17), 0.560, 1.36), (0.11, 0.002, 0.08), CHROME, rot=(0, 0, s * 8))

def door_glass(p, s):
    gx = s * (TUB_X + 0.0175)
    p.box2((gx - 0.003, DOOR_REAR + 0.07, BELT - 0.01), (gx + 0.003, 0.56, ROOF - 0.09), GLASS)

door_shell(body, 1)
body.box2((TUB_X - 0.075, 0.26, 1.105), (TUB_X - 0.06, 0.34, 1.135), METAL)                # sağ iç kol
body.build()

# ================================================================= CAMLAR
glass = Part("Glass")
glass.box(ws_point(0, WS_H / 2), (1.66, 0.008, WS_H - 0.02), GLASS, rot=(WS_TILT, 0, 0))
for s in (-1, 1):
    gx = s * (TUB_X - 0.012)
    glass.box2((gx - 0.003, TUB_REAR + 0.10, BELT + 0.08), (gx + 0.003, DOOR_REAR - 0.08, ROOF - 0.10), GLASS)
glass.box2((-0.64, TUB_REAR - 0.013, BELT + 0.08), (0.64, TUB_REAR - 0.007, ROOF - 0.08), GLASS)
door_glass(glass, 1)
glass.build()

# ================================================================= SÜRÜCÜ KAPISI (menteşede döner)
HINGE = (-0.95, 0.70, 1.15)
door = Part("Door_L", pivot=HINGE, bevel=0.01)
door_shell(door, -1)
door.box2((-TUB_X + 0.06, 0.26, 1.105), (-TUB_X + 0.075, 0.34, 1.135), METAL)            # iç kol
door.cyl((-TUB_X + 0.035, -0.20, 0.95), 0.02, 0.02, METAL, axis='X', segs=10)            # cam kolu
door.seg((-TUB_X + 0.045, -0.20, 0.95), (-TUB_X + 0.05, -0.10, 0.90), 0.012, 0.012, METAL)
door_glass(door, -1)
door.build()

# ================================================================= DİREKSİYON (jant yarıçapı 0.19 sabit)
sw = Part("SteeringWheel", pivot=tuple(SW), smooth=True)
R3 = R59.to_3x3()
def swp(x, y, z=0.0):
    return SW + R3 @ V((x, y, z))
segM, segm = 28, 8
n0 = set(sw.bm.faces)
rings = []
for i in range(segM):
    a = 2 * math.pi * i / segM
    rings.append([sw.bm.verts.new(swp((0.19 + 0.017 * math.cos(b)) * math.cos(a),
                                      (0.19 + 0.017 * math.cos(b)) * math.sin(a), 0.017 * math.sin(b)))
                  for b in (2 * math.pi * j / segm for j in range(segm))])
for i in range(segM):
    for j in range(segm):
        i2, j2 = (i + 1) % segM, (j + 1) % segm
        sw.bm.faces.new((rings[i][j], rings[i2][j], rings[i2][j2], rings[i][j2]))
sw._tag(n0, LEATHER)
sw.box(swp(0, -0.01, 0.02), (0.15, 0.12, 0.045), TRIM, rot=(59, 0, 0))                    # hava yastığı göbeği
for a in (0, 180, 270):
    d = V((math.cos(math.radians(a)), math.sin(math.radians(a)), 0))
    sw.seg(swp(*(d * 0.06)), swp(*(d * 0.18)), 0.04, 0.014, TRIM, up=tuple(R3 @ V((0, 0, 1))))
sw.cyl(swp(0, 0, -0.03), 0.045, 0.05, TRIM, axis=R59, segs=12)
sw.build()

# ================================================================= EL FRENİ (pivot arkada; sinematikte ön ucu yukarı çekilir)
HB = (0.0, -0.50, 0.815)
hb = Part("Handbrake", pivot=HB)
hb.box((0.0, -0.39, 0.815), (0.03, 0.24, 0.02), METAL)
hb.cyl((0.0, -0.27, 0.82), 0.019, 0.12, LEATHER, axis='Y', segs=12)   # tutamak: gevşek yumruk (r 0.021) sarar
hb.cyl((0.0, -0.205, 0.82), 0.008, 0.012, METAL, axis='Y', segs=10)   # kilit düğmesi
hb.build()

# ================================================================= VİTES KOLU (pivot tabanda; sinematikte vitesten boşa öne itilir)
GL = (0.0, 0.12, 0.76)
gl = Part("GearLever", pivot=GL)
gl.tube([(0.0, 0.12, 0.76), (0.0, 0.08, 0.98)], 0.010, METAL, segs=8)
gl.cyl((0.0, 0.075, 1.00), 0.020, 0.06, TRIM, segs=12)          # vites topuzu: sağ el gevşek yumrukla tutar (r 0.021)
gl.cyl((0.0, 0.12, 0.765), 0.032, 0.02, RUBBER, segs=12)        # körük
gl.build()

# ================================================================= KONTAK ANAHTARI
KEY = (-0.13, 0.41, 1.10)
key = Part("IgnitionKey", pivot=KEY)
key.box((-0.13, 0.405, 1.10), (0.006, 0.03, 0.014), METAL)
key.box((-0.13, 0.38, 1.10), (0.008, 0.026, 0.034), LEATHER)
key.cyl((-0.13, 0.36, 1.075), 0.012, 0.004, METAL, axis='X', segs=10)
key.box((-0.13, 0.36, 1.04), (0.008, 0.022, 0.05), LEATHER)
key.build()

# ================================================================= TEKERLER (arazi lastiği, 5 kollu jant)
def tire_profile(w=0.27, r=WHEEL_R, r_rim=0.235):
    h = w / 2
    return [(-h + 0.01, r_rim), (-h, r_rim + 0.04), (-h - 0.008, r - 0.10), (-h + 0.005, r - 0.035),
            (-h + 0.03, r - 0.004), (-h + 0.07, r), (h - 0.07, r), (h - 0.03, r - 0.004),
            (h - 0.005, r - 0.035), (h + 0.008, r - 0.10), (h, r_rim + 0.04), (h - 0.01, r_rim)]
def tread(i, k):
    if i < 3 or i > 8:
        return 1.0
    side = 0 if i < 6 else 1
    return 1.0 if (((k // 2) % 2) ^ side) else 0.965

def wheel_parts(p, c, out, axis='X'):
    """c: merkez; out: dış yan (+1/-1, eksen boyunca)."""
    A = V((1, 0, 0)) if axis == 'X' else V((0, 1, 0))
    c = V(c)
    p.lathe(c, tire_profile(), RUBBER, axis=axis, segs=36, radial=tread, closed=True)
    rim = [(-out * 0.11, 0.0), (-out * 0.11, 0.232), (out * 0.09, 0.236), (out * 0.10, 0.225), (out * 0.06, 0.20), (out * 0.06, 0.0)]
    p.lathe(c, rim, RIM, axis=axis, segs=24)
    hub = c + A * (out * 0.075)
    p.cyl(hub, 0.055, 0.03, RIM, axis=axis, segs=12)
    p.cyl(hub + A * (out * 0.018), 0.03, 0.008, CHROME, axis=axis, segs=10)
    u, v = (V((0, 1, 0)), V((0, 0, 1))) if axis == 'X' else (V((1, 0, 0)), V((0, 0, 1)))
    for k in range(5):
        a = 2 * math.pi * k / 5 + 0.3
        d = u * math.cos(a) + v * math.sin(a)
        p.seg(hub + d * 0.04, c + A * (out * 0.085) + d * 0.205, 0.035, 0.05, RIM, up=tuple(A))
        d2 = u * math.cos(a + 0.63) + v * math.sin(a + 0.63)
        p.cyl(hub + A * (out * 0.016) + d2 * 0.038, 0.007, 0.012, CHROME, axis=axis, segs=6)

for name, x, y in (("Wheel_FL", -TRACK, FRONT_AXLE), ("Wheel_FR", TRACK, FRONT_AXLE),
                   ("Wheel_RL", -TRACK, REAR_AXLE), ("Wheel_RR", TRACK, REAR_AXLE)):
    w = Part(name, pivot=(x, y, WHEEL_R), smooth=True)
    wheel_parts(w, (x, y, WHEEL_R), 1 if x > 0 else -1)
    w.build()

spare = Part("SpareTire", smooth=True)
wheel_parts(spare, (0.05, -2.19, 1.00), -1, axis='Y')
spare.build()

# ================================================================= YANAN PARÇALAR (Unity'de açılıp kapanır)
hg = Part("HeadlightGlow")
for s in (-1, 1):
    hg.cyl((s * 0.53, 2.158, 0.885), 0.082, 0.006, GLOW, axis='Y', segs=20)
hg.build()
tg = Part("TaillightGlow")
for s in (-1, 1):
    tg.box2((s * 0.76 - 0.04, REAR_FACE - 0.028, 0.88), (s * 0.76 + 0.04, REAR_FACE - 0.025, 1.06), TGLOW)
tg.build()

# Kadranlar: Gauges sönük, GaugeGlow 2 mm önünde yanan yüz
GAUGES = [(-0.47, 1.175, 0.058), (-0.33, 1.175, 0.058), (-0.59, 1.16, 0.034), (-0.21, 1.16, 0.034)]
gauge = Part("Gauges")
for x, z, r in GAUGES:
    gauge.cyl((x, DASH_Y - 0.010, z), r, 0.016, GAUGE, axis='Y', segs=24, fit=True)
    gauge.lathe((x, DASH_Y - 0.020, z), [(0, r + 0.010), (0.008, r + 0.010), (0.008, r), (0, r)], CHROME,
                axis='Y', segs=24, closed=True)
gauge.build()
gg = Part("GaugeGlow")
for x, z, r in GAUGES:
    gg.cyl((x, DASH_Y - 0.019, z), r - 0.002, 0.002, GAUGEGLOW, axis='Y', segs=24, fit=True)
gg.box2((-0.065, DASH_Y - 0.047, 1.05), (0.03, DASH_Y - 0.0445, 1.07), GAUGEGLOW)
gg.build()

# ================================================================= DOKULAR (512px, döşenebilir)
N = 512
yy, xx = np.mgrid[0:N, 0:N]

def pnoise(scale, seed):
    """Döşenebilir gürültü: FFT alçak geçiren. scale ~ özellik boyu (piksel)."""
    r = np.random.default_rng(seed).standard_normal((N, N))
    f = np.fft.fftfreq(N)
    fx, fy = np.meshgrid(f, f)
    out = np.real(np.fft.ifft2(np.fft.fft2(r) * np.exp(-(fx ** 2 + fy ** 2) * (scale ** 2) * 2)))
    out -= out.min()
    return out / max(out.max(), 1e-9)

def save(name, rgb):
    rgba = np.concatenate([np.clip(rgb, 0, 1), np.ones((N, N, 1))], axis=2)[::-1]
    img = bpy.data.images.get(name) or bpy.data.images.new(name, N, N, alpha=False)
    if tuple(img.size) != (N, N):
        img.scale(N, N)
    img.pixels.foreach_set(rgba.astype(np.float32).ravel())
    img.filepath_raw = os.path.join(TEX, name + ".png")
    img.file_format = 'PNG'
    img.save()

def mix(a, b, t):
    return a * (1 - t[..., None]) + np.asarray(b) * t[..., None]

def scratches(count, length, seed):
    m = np.zeros((N, N))
    r = np.random.default_rng(seed)
    for _ in range(count):
        x, y = r.uniform(0, N, 2)
        a = r.uniform(0, math.pi)
        for t in np.linspace(0, r.uniform(length * 0.3, length), int(length * 2)):
            m[int(y + math.sin(a) * t) % N, int(x + math.cos(a) * t) % N] = 1
    return m

# Boya: solmuş kırmızı-turuncu, toz, çizik, pas noktaları
c = np.ones((N, N, 3)) * np.array([0.56, 0.19, 0.09])
c = mix(c, [0.70, 0.33, 0.20], np.clip((pnoise(60, 1) - 0.45) * 1.4, 0, 0.45))
c = mix(c, [0.26, 0.19, 0.13], np.clip((pnoise(25, 2) - 0.55) * 2.0, 0, 0.55))
c *= (0.92 + 0.16 * pnoise(1.5, 3))[..., None]
c = mix(c, [0.80, 0.62, 0.52], scratches(40, 70, 4) * 0.35)
c = mix(c, [0.30, 0.13, 0.05], np.clip((pnoise(4, 5) - 0.78) * 6, 0, 0.8))
save("IC_CarPaint", c)

c = np.ones((N, N, 3)) * 0.075                                         # siyah plastik kabartma
c *= (0.85 + 0.3 * pnoise(1.2, 11))[..., None]
c = mix(c, [0.24, 0.21, 0.18], np.clip((pnoise(30, 12) - 0.55) * 1.8, 0, 0.45))
save("IC_Flare", c)

weave = 0.5 + 0.25 * np.sin(xx * 2 * math.pi * 96 / N) + 0.25 * np.sin(yy * 2 * math.pi * 96 / N)
c = np.ones((N, N, 3)) * 0.055 * (0.8 + 0.4 * weave)[..., None]      # siyah kanvas branda
c = mix(c, [0.16, 0.15, 0.14], np.clip((pnoise(40, 21) - 0.5) * 1.2, 0, 0.35))
c = mix(c, [0.20, 0.19, 0.17], scratches(25, 120, 22) * 0.25)
save("IC_SoftTop", c)

c = np.ones((N, N, 3)) * np.array([0.68, 0.68, 0.66])                # jant
c = mix(c, [0.30, 0.27, 0.23], np.clip((pnoise(18, 31) - 0.45) * 1.6, 0, 0.6))
c *= (0.93 + 0.12 * pnoise(1.5, 32))[..., None]
save("IC_Rim", c)

c = np.ones((N, N, 3)) * 0.78 * (0.85 + 0.2 * pnoise(50, 41))[..., None]   # krom
c = mix(c, [0.35, 0.32, 0.28], np.clip((pnoise(10, 42) - 0.7) * 3, 0, 0.5))
save("IC_Chrome", c)

def dial(bg, tick, needle, ring):
    u = (xx + 0.5) / N - 0.5
    v = 0.5 - (yy + 0.5) / N            # satır 0 = görüntünün üstü
    r = np.sqrt(u * u + v * v) * 2
    a = (np.degrees(np.arctan2(v, u)) + 360) % 360
    c = np.ones((N, N, 3)) * np.array(bg)
    c = mix(c, ring, ((r > 0.90) & (r < 1.0)).astype(float))
    arc = ~((a > 240) & (a < 300))      # altta 60° boşluk
    big = ((a % 30) < 2.2) & (r > 0.62) & (r < 0.86) & arc
    small = ((a % 6) < 1.0) & (r > 0.76) & (r < 0.86) & arc
    c = mix(c, tick, (big | small).astype(float))
    na = math.radians(225)              # ibre: rölanti, sol alt
    d = np.abs(u * math.sin(na) - v * math.cos(na))
    along = u * math.cos(na) + v * math.sin(na)
    c = mix(c, needle, ((d < 0.014) & (along > -0.06) & (along < 0.42)).astype(float))
    return mix(c, [0.1, 0.1, 0.1], (r < 0.09).astype(float))
save("IC_GaugeFace", dial([0.035, 0.035, 0.04], [0.55, 0.55, 0.52], [0.45, 0.12, 0.06], [0.25, 0.25, 0.25]))
save("IC_GaugeGlow", dial([0.10, 0.06, 0.03], [1.0, 0.70, 0.35], [1.0, 0.25, 0.08], [0.35, 0.22, 0.10]))

# ================================================================= BLENDER ÖNİZLEME MALZEMELERİ
# Unity'deki IntroCinematicBuilder.SetupMaterials ile aynı doku/döşeme (sadece görsel kontrol için).
PREVIEW = {  # ad: (doku, döşeme, renk, metal, pürüzsüzlük, ışıma)
    PAINT: ("IC_CarPaint", 0.5, None, 0.15, 0.25, None), FLARE: ("IC_Flare", 1, None, 0, 0.15, None),
    TOP: ("IC_SoftTop", 1, None, 0, 0.05, None), TRIM: ("IC_DarkPlastic", 1, None, 0, 0.2, None),
    RUBBER: ("IC_Rubber", 1, None, 0, 0.1, None), METAL: ("IC_Metal", 1, None, 0.8, 0.45, None),
    CHROME: ("IC_Chrome", 1, None, 1.0, 0.7, None), RIM: ("IC_Rim", 2, None, 0.6, 0.35, None),
    SEAT: ("IC_SeatFabric", 2, None, 0, 0.05, None), LEATHER: ("IC_Leather", 4, None, 0, 0.3, None),
    GRILLE: ("IC_Grille", 2, None, 0.6, 0.35, None),
    GAUGE: ("IC_GaugeFace", 1, None, 0, 0.6, None), GAUGEGLOW: ("IC_GaugeGlow", 1, None, 0, 0.6, 1.2),
    GLASS: (None, 1, (0.55, 0.6, 0.62, 0.14), 0, 0.92, None), LENS: (None, 1, (0.75, 0.75, 0.72, 1), 0.2, 0.85, None),
    LAMP_RED: (None, 1, (0.35, 0.04, 0.03, 1), 0, 0.8, None),
    GLOW: (None, 1, (1, 0.95, 0.85, 1), 0, 0.8, 4.0), TGLOW: (None, 1, (0.6, 0.05, 0.03, 1), 0, 0.8, 2.0),
}
for name, (tex, tile, col, metal, smooth, emit) in PREVIEW.items():
    m = mat(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Roughness"].default_value = 1 - smooth
    if tex:
        path = os.path.join(TEX, tex + ".png")
        if os.path.exists(path):
            img = bpy.data.images.load(path, check_existing=True)
            img.reload()
            ti = nt.nodes.new("ShaderNodeTexImage")
            ti.image = img
            mp = nt.nodes.new("ShaderNodeMapping")
            mp.inputs["Scale"].default_value = (tile, tile, 1)
            tc = nt.nodes.new("ShaderNodeTexCoord")
            nt.links.new(tc.outputs["UV"], mp.inputs[0])
            nt.links.new(mp.outputs[0], ti.inputs[0])
            nt.links.new(ti.outputs[0], bsdf.inputs["Base Color"])
            if emit:
                nt.links.new(ti.outputs[0], bsdf.inputs["Emission Color"])
                bsdf.inputs["Emission Strength"].default_value = emit
    if col:
        bsdf.inputs["Base Color"].default_value = col
        bsdf.inputs["Alpha"].default_value = col[3]
        if col[3] < 1:
            m.surface_render_method = 'BLENDED'
        if emit:
            bsdf.inputs["Emission Color"].default_value = col
            bsdf.inputs["Emission Strength"].default_value = emit
    m.diffuse_color = col if col else (0.5, 0.5, 0.5, 1)

# ================================================================= DIŞA AKTAR
NAMES = ["Body", "Seats", "SpareTire", "Glass", "Door_L", "SteeringWheel", "IgnitionKey", "Handbrake", "GearLever",
         "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR",
         "HeadlightGlow", "TaillightGlow", "Gauges", "GaugeGlow"]
if bpy.context.object and bpy.context.object.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.select_all(action='DESELECT')
COLLECTION.hide_viewport = False   # gizli obje seçilemez, FBX boş çıkar
for n in NAMES:
    bpy.data.objects[n].hide_set(False)
    bpy.data.objects[n].select_set(True)
assert len(bpy.context.selected_objects) == len(NAMES), "dışa aktarılacak objeler seçilemedi"
bpy.context.view_layer.objects.active = bpy.data.objects["Body"]
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "IC_Car4x4.fbx"), use_selection=True,
                         object_types={'MESH', 'EMPTY'}, use_mesh_modifiers=True,
                         apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                         axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE',
                         add_leaf_bones=False, bake_anim=False, path_mode='STRIP')
result = {n: len(bpy.data.objects[n].data.polygons) for n in NAMES}
print(result)
