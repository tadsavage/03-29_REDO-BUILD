"""
stylized_lowpoly_head.py
========================
Procedurally builds ONE stylised low-poly female head mesh (a cartoon blue-eyed woman
with full red lips, arched brows and long black hair) from scratch.

  * one object, one mesh, X-axis Mirror modifier (only the +X half is modelled)
  * strictly < 1,000 faces after the mirror (the script asserts it)
  * concentric quad loops (inset rings) around the eye sockets and the mouth,
    a centre-line nose bridge, a tapered jaw/chin and a neck stub
  * volumetric eyeballs, a procedural lid inset between eyeball and lid edge loop,
    volumetric eyebrow loops, an eyeliner ring with a winged lash strip
  * flat colours: one material per face group, each also written to a vertex group
    of the same name

Run it from Blender's Scripting tab (Run Script) or headless:
    blender --background --python stylized_lowpoly_head.py

Units: the shape is authored in "head units" (crown ~ +1, chin ~ -1) and scaled by
FINAL_SCALE at the end, so the finished head is real-world size (~0.25 m crown to
chin) and its bounding box is centred on world (0, 0, 0). Front of the face is -Y.
"""

import math

import bmesh
import bpy
from mathutils import Matrix, Vector

# ----------------------------------------------------------------------------------
# Configuration
# ----------------------------------------------------------------------------------
OBJ_NAME = "StylizedHead"
FINAL_SCALE = 0.12          # head units -> metres
MAX_FACES = 1000

# Face groups. Each one gets its own material AND vertex group with this name.
G_SKIN, G_LINER, G_IRIS, G_PUPIL, G_WHITE, G_BROW, G_LIPS, G_HAIR = range(8)
GROUPS = [
    # name,              sRGB colour,            roughness
    ("Skin",            (0.96, 0.82, 0.74),     0.60),
    ("Eyeliner_Lashes", (0.02, 0.02, 0.02),     0.45),
    ("Iris",            (0.10, 0.45, 0.95),     0.25),
    ("Pupil",           (0.01, 0.01, 0.015),    0.20),
    ("EyeWhite",        (0.97, 0.97, 0.97),     0.30),
    ("Eyebrow",         (0.42, 0.22, 0.10),     0.70),
    ("Lips",            (0.85, 0.04, 0.07),     0.22),
    ("Hair",            (0.025, 0.022, 0.022),  0.40),
]

# Head cross-sections, top to bottom: (z, half-width W, front depth Df, back depth Db,
# centre y, superellipse exponent n). Each ring is a superellipse |x/W|^n + |y/D|^n = 1,
# using Df in front of the centre and Db behind it.
# Proportions are measured off the front/profile reference: eyes on the head's
# midline, a near-vertical face plane from brow to chin, a narrow egg-shaped skull
# and a thick neck that leans back.
CROWN_Z = 1.02
HEAD_ROWS = [
    (0.96,  0.30,  0.27, 0.29, 0.00, 2.0),   # 0  crown ring
    (0.83,  0.48,  0.49, 0.50, 0.00, 2.1),   # 1
    (0.66,  0.58,  0.62, 0.66, 0.00, 2.2),   # 2
    (0.46,  0.63,  0.69, 0.76, 0.00, 2.6),   # 3  upper forehead
    (0.27,  0.625, 0.73, 0.79, 0.00, 3.0),   # 4  brow line
    (0.14,  0.62,  0.73, 0.80, 0.00, 3.2),   # 5  eye top
    (0.06,  0.615, 0.72, 0.80, 0.00, 3.2),   # 6  eye centre
    (-0.02, 0.61,  0.72, 0.79, 0.00, 3.2),   # 7  eye bottom
    (-0.14, 0.60,  0.72, 0.77, 0.00, 3.0),   # 8  cheekbone / nose bridge
    (-0.22, 0.58,  0.72, 0.74, 0.00, 2.8),   # 9  nose tip
    (-0.30, 0.56,  0.73, 0.70, 0.00, 2.7),   # 10 nose base / nostrils
    (-0.42, 0.545, 0.74, 0.64, 0.00, 2.5),   # 11 upper lip
    (-0.50, 0.525, 0.74, 0.58, 0.00, 2.3),   # 12 mouth line
    (-0.59, 0.505, 0.75, 0.53, 0.00, 2.2),   # 13 lower lip
    (-0.78, 0.49,  0.77, 0.47, 0.00, 1.6),   # 14 chin front -> jaw angle at the side
    (-0.92, 0.42,  0.60, 0.45, 0.02, 1.5),   # 15 chin bottom -> under the jaw
    (-1.00, 0.38,  0.45, 0.45, 0.05, 2.0),   # 16 throat / top of the neck
    (-1.32, 0.45,  0.31, 0.44, 0.08, 2.0),   # 17 neck stub base (throat slopes back)
]
# Jaw loops: these rows rise by `lift * sin(phi)^2` - zero at the chin and the nape,
# full height under the ear - so one loop runs chin -> jawline -> jaw angle, and the
# next forms the flat underside of the jaw back to the throat.
JAW_LIFT = {12: 0.03, 13: 0.07, 14: 0.16, 15: 0.24, 16: 0.16}
R_BROW, R_EYE_TOP, R_EYE_MID, R_EYE_BOT, R_CHEEK, R_NOSE, R_NOSE_BASE = 4, 5, 6, 7, 8, 9, 10
R_LIP_TOP, R_MOUTH, R_LIP_BOT = 11, 12, 13

# Meridians on the +X half, degrees from the front centre line (0) to the back (180).
# Column 1 is the side of the nose bridge / nostril; columns 2/3/4 land on the inner
# corner, centre and outer corner of the eye; column 3 is also the mouth corner.
HEAD_COLS = [0, 3, 7, 26, 55, 74, 92, 104, 128, 154, 180]
C_NOSE, C_EYE_IN, C_EYE_MID, C_EYE_OUT, C_EAR = 1, 2, 3, 4, 6

# Eye, in head units (the eye on the +X side). The almond is the OUTER edge of the
# lid; three inset loops (lid, eyeliner, tuck) step in from it, so the visible opening
# is EYE_LID_RINGS narrower on every side (~0.41 x 0.15, as in the reference).
EYE_X, EYE_Z = 0.325, 0.065
EYE_A, EYE_BT, EYE_BB = 0.255, 0.135, 0.115   # half width, top and bottom heights
EYE_LID_RINGS = (0.018, 0.022, 0.008)          # lid, eyeliner, tuck inset widths
# Eyeball sphere. It is fitted through the inner and outer corners of the opening
# (which tilts back with the face), so it sits INSIDE the head; the lids are wrapped
# onto it (radius + EYE_LID_OFFSETS) so they hug the ball instead of the face.
EYE_R = 0.30
EYE_GAZE_OUT = 14.0                             # degrees the iris turns outward
EYE_LID_OFFSETS = (0.035, 0.030, 0.026, 0.006)  # outer edge, lid, liner, opening rim

# Mouth outline (x, z) per grid column 0..3: top of the upper lip, the mouth line
# (last point = corner, lifted for a smile) and bottom of the lower lip. The outline
# includes the perioral skin loop, which is MOUTH_SKIN_RING wide.
MOUTH_TOP = [(0.000, -0.418), (0.068, -0.402), (0.165, -0.420), (0.250, -0.445)]
MOUTH_LINE = [(0.000, -0.500), (0.075, -0.500), (0.170, -0.494), (0.300, -0.482)]
MOUTH_BOT = [(0.000, -0.612), (0.085, -0.607), (0.180, -0.588), (0.255, -0.550)]
MOUTH_SKIN_RING, MOUTH_LIP_RING = 0.025, 0.020
LIP_PUFF_UPPER, LIP_PUFF_LOWER = 0.068, 0.076   # how far each lip swells forward
LIP_HALF_WIDTH = 0.285                           # swelling fades to 0 here (corners)

# Hair: meridians, hairline height per meridian (None = long, falls past the
# shoulders) and where each long lock ends (uneven tips). Column 0 is the centre part.
# The hairline is high at the part, dips to the temples and runs down in front of the
# ear as a sideburn; the long hair starts behind the ear.
HAIR_COLS = [0, 14, 30, 48, 66, 82, 96, 112, 132, 155, 180]
HAIRLINE = [0.64, 0.61, 0.54, 0.44, 0.30, 0.14, 0.20, None, None, None, None]
HAIR_TIPS = [None] * 7 + [-1.62, -2.00, -1.72, -2.08]
HAIR_ROWS_LONG = [0.97, 0.86, 0.66, 0.42, 0.20, -0.35, -1.00]   # + tip
HAIR_DRAPE_Z = 0.20       # below this the hair hangs instead of hugging the skull
HAIR_OFFSET = 0.065       # how far the hair stands off the scalp
HAIR_THICKNESS = 0.06     # the hanging hair is a closed slab this thick
HAIR_LOCK = 0.05          # alternate long columns stand out -> separate locks


def hair_offset(deg, z):
    """Scalp offset: a groove at the centre part, lift beside it, fuller temples."""
    off = HAIR_OFFSET
    if z > 0.5:
        if deg == 0:
            off -= 0.030
        elif deg <= 14:
            off += 0.035
        elif deg <= 30:
            off += 0.02
    if 0.2 <= z <= 0.75 and deg >= 48:
        off += 0.015
    return off


# ----------------------------------------------------------------------------------
# Shape helpers
# ----------------------------------------------------------------------------------
def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def ring_point(phi, W, Df, Db, yc, n):
    """Point on a superellipse ring at meridian angle phi (0 = front, pi = back)."""
    s, c = math.sin(phi), math.cos(phi)
    e = 2.0 / n
    x = 0.0 if phi <= 1e-6 or phi >= math.pi - 1e-6 else W * abs(s) ** e
    y = yc - Df * abs(c) ** e if c >= 0 else yc + Db * abs(c) ** e
    return x, y


def profile_at(z):
    """Interpolate the head cross-section parameters at height z."""
    table = [(CROWN_Z, 0.0, 0.0, 0.0, HEAD_ROWS[0][4], 2.0)] + HEAD_ROWS
    if z >= table[0][0]:
        return table[0][1:]
    for a, b in zip(table, table[1:]):
        if a[0] >= z >= b[0]:
            t = (a[0] - z) / (a[0] - b[0])
            return tuple(a[i] + (b[i] - a[i]) * t for i in range(1, 6))
    return table[-1][1:]


def front_y(x, z):
    """Y of the bare head surface (front half) at (x, z) - used to seat features."""
    W, Df, _Db, yc, n = profile_at(z)
    t = min(abs(x) / max(W, 1e-4), 0.999)
    return yc - Df * (1.0 - t ** n) ** (1.0 / n)


_EYE_CENTRE = None


def eyeball_centre():
    """Sphere centre whose surface passes through both corners of the eye opening."""
    global _EYE_CENTRE
    if _EYE_CENTRE is None:
        inset = sum(EYE_LID_RINGS)
        xi = EYE_X - 0.93 * EYE_A + inset
        xo = EYE_X + EYE_A - inset
        a = Vector((xi, front_y(xi, EYE_Z)))
        b = Vector((xo, front_y(xo, EYE_Z)))
        mid, chord = (a + b) / 2, b - a
        back = Vector((-chord.y, chord.x)).normalized()       # perpendicular, into head
        if back.y < 0:
            back.negate()
        d = math.sqrt(max(EYE_R ** 2 - (chord.length / 2) ** 2, 0.0))
        c = mid + back * d
        _EYE_CENTRE = Vector((c.x, c.y, EYE_Z))
    return _EYE_CENTRE.copy()


def eye_gaze():
    a = math.radians(EYE_GAZE_OUT)
    return Vector((math.sin(a), -math.cos(a), 0.0))


def eyeball_y(x, z, offset):
    """Y on the front of the eyeball sphere grown by `offset` (clamped at the rim)."""
    c, r = eyeball_centre(), EYE_R + offset
    d2 = (x - c.x) ** 2 + (z - c.z) ** 2
    return c.y - math.sqrt(max(r * r - d2, 0.0))


def interp_x(points, x):
    """Piecewise-linear z(x) through [(x, z), ...] sorted by x (clamped)."""
    x = abs(x)
    if x <= points[0][0]:
        return points[0][1]
    for (x0, z0), (x1, z1) in zip(points, points[1:]):
        if x <= x1:
            return z0 + (z1 - z0) * (x - x0) / (x1 - x0)
    return points[-1][1]


def lip_swell(x, z):
    """Forward offset of a lip vertex. Lips are fullest just short of where they meet
    and roll back to the border; the swell tapers to nothing at the corners."""
    taper = max(0.0, 1.0 - (abs(x) / LIP_HALF_WIDTH) ** 2) ** 0.6
    zl = interp_x(MOUTH_LINE, x)
    if z >= zl:
        h, amp = interp_x(MOUTH_TOP, x) - MOUTH_SKIN_RING - zl, LIP_PUFF_UPPER
        u = (z - zl) / max(h, 1e-3)
    else:
        h, amp = zl - interp_x(MOUTH_BOT, x) - MOUTH_SKIN_RING, LIP_PUFF_LOWER
        u = (zl - z) / max(h, 1e-3)
    u = min(max(u, 0.0), 1.0)
    return amp * taper * (0.2 + 0.8 * math.sin(math.pi * (0.35 + 0.65 * u)))


def lip_seam_swell(x):
    """Forward offset of the mouth line itself - just behind both lip fronts."""
    taper = max(0.0, 1.0 - (abs(x) / LIP_HALF_WIDTH) ** 2) ** 0.6
    return 0.50 * 0.5 * (LIP_PUFF_UPPER + LIP_PUFF_LOWER) * taper


def eye_point(key):
    """Named points of the eye almond (outer corner lifted for a cat-eye tilt)."""
    a, bt, bb = EYE_A, EYE_BT, EYE_BB
    pts = {
        "inner":     (-0.93 * a,   -0.005),
        "top_in":    (-0.60 * a,   0.75 * bt),
        "top":       (0.01,        bt),
        "top_out":   (0.62 * a,    0.85 * bt + 0.02),
        "outer":     (a,           0.035),
        "bot_out":   (0.62 * a,    -0.60 * bb + 0.015),
        "bot":       (0.0,         -bb),
        "bot_in":    (-0.60 * a,   -0.70 * bb),
        "centre":    (0.0,         0.0),
    }
    dx, dz = pts[key]
    return EYE_X + dx, EYE_Z + dz


# ----------------------------------------------------------------------------------
# Scene / materials
# ----------------------------------------------------------------------------------
def clear_scene():
    """Remove the default cube and any previous run of this script."""
    for name in ("Cube", OBJ_NAME):
        obj = bpy.data.objects.get(name)
        if obj is not None:
            mesh = obj.data if obj.type == "MESH" else None
            bpy.data.objects.remove(obj, do_unlink=True)
            if mesh is not None and mesh.users == 0:
                bpy.data.meshes.remove(mesh)
    for name, _c, _r in GROUPS:
        mat = bpy.data.materials.get("Head_" + name)
        if mat is not None and mat.users == 0:
            bpy.data.materials.remove(mat)


def make_material(name, srgb, roughness):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    lin = tuple(srgb_to_linear(c) for c in srgb) + (1.0,)
    mat.diffuse_color = lin                     # viewport / Workbench colour
    mat.roughness = roughness
    mat.use_nodes = True
    bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf is not None:
        bsdf.inputs["Base Color"].default_value = lin
        bsdf.inputs["Roughness"].default_value = roughness
        for key in ("Specular IOR Level", "Specular"):
            if key in bsdf.inputs:
                bsdf.inputs[key].default_value = 0.3
                break
    return mat


# ----------------------------------------------------------------------------------
# Geometry builders (all work on the +X half in a shared bmesh)
# ----------------------------------------------------------------------------------
class HeadBuilder:
    def __init__(self):
        self.bm = bmesh.new()
        self.grp = self.bm.faces.layers.int.new("grp")
        self.grid = []          # grid[row][col] -> BMVert
        self.cell = {}          # (row, col) -> BMFace (quad between row/row+1, col/col+1)

    # -- utilities ------------------------------------------------------------------
    def face(self, verts, group):
        f = self.bm.faces.new(verts)
        f[self.grp] = group
        return f

    def set_group(self, faces, group):
        for f in faces:
            f[self.grp] = group

    def inset(self, faces, thickness, depth=0.0):
        """Inset a face region; returns the NEW rim faces (one clean quad loop)."""
        self.bm.normal_update()           # sculpting moves verts; insets use normals
        ret = bmesh.ops.inset_region(
            self.bm, faces=faces, thickness=thickness, depth=depth,
            use_even_offset=True, use_boundary=False, use_interpolate=True)
        return ret["faces"]

    # -- 1. head shell --------------------------------------------------------------
    def build_shell(self):
        bm = self.bm
        cols = [math.radians(d) for d in HEAD_COLS]
        for r, (z, W, Df, Db, yc, n) in enumerate(HEAD_ROWS):
            row = []
            lift = JAW_LIFT.get(r, 0.0)
            for phi in cols:
                x, y = ring_point(phi, W, Df, Db, yc, n)
                row.append(bm.verts.new((x, y, z + lift * math.sin(phi) ** 2)))
            self.grid.append(row)
        pole = bm.verts.new((0.0, HEAD_ROWS[0][4], CROWN_Z))

        nc = len(cols)
        for c in range(nc - 1):                         # crown fan (hidden by hair)
            self.face((pole, self.grid[0][c], self.grid[0][c + 1]), G_SKIN)
        for r in range(len(HEAD_ROWS) - 1):             # quad bands
            for c in range(nc - 1):
                g = self.grid
                self.cell[(r, c)] = self.face(
                    (g[r][c], g[r + 1][c], g[r + 1][c + 1], g[r][c + 1]), G_SKIN)
        cap = self.face(list(reversed(self.grid[-1])), G_SKIN)   # neck cut
        if cap.normal.z > 0:
            cap.normal_flip()

    # -- 2. sculpt the face on the grid --------------------------------------------
    def sculpt_face(self):
        g = self.grid
        # Nose: a narrow bridge from the brow, a rounded slightly upturned tip,
        # flared nostril wings and an underside that tucks back to the lip.
        # (row, col): (x, z, forward offset from the bare head surface)
        nose = {
            (R_BROW, 0):      (0.000,  0.270, 0.028),            # glabella
            (R_EYE_TOP, 0):   (0.000,  0.140, 0.004),            # nasion
            (R_EYE_TOP, 1):   (0.045,  0.140, 0.000),
            (R_EYE_MID, 0):   (0.000,  0.060, 0.040),
            (R_EYE_MID, 1):   (0.045,  0.060, 0.022),
            (R_EYE_BOT, 0):   (0.000, -0.020, 0.120),
            (R_EYE_BOT, 1):   (0.048, -0.020, 0.070),
            (R_CHEEK, 0):     (0.000, -0.140, 0.190),
            (R_CHEEK, 1):     (0.064, -0.145, 0.130),
            (R_CHEEK, 2):     (0.120, -0.150, 0.040),            # bridge -> cheek
            (R_NOSE, 0):      (0.000, -0.195, 0.240),            # tip
            (R_NOSE, 1):      (0.070, -0.215, 0.205),            # tip side
            (R_NOSE, 2):      (0.122, -0.248, 0.110),            # nostril wing
            (R_NOSE_BASE, 0): (0.000, -0.272, 0.165),            # columella base
            (R_NOSE_BASE, 1): (0.068, -0.262, 0.145),            # nostril
            (R_NOSE_BASE, 2): (0.120, -0.265, 0.050),            # wing crease
        }
        for (r, c), (x, z, fwd) in nose.items():
            g[r][c].co = Vector((x, front_y(x, z) - fwd, z))

        # Brow ridge over the eye socket.
        for c in (C_EYE_IN, C_EYE_MID, C_EYE_OUT):
            g[R_BROW][c].co.y -= 0.02

        # Eye almond: snap the 3x3 verts around the eye onto the almond outline.
        eye = {
            (R_EYE_TOP, C_EYE_IN): "top_in", (R_EYE_TOP, C_EYE_MID): "top",
            (R_EYE_TOP, C_EYE_OUT): "top_out",
            (R_EYE_MID, C_EYE_IN): "inner", (R_EYE_MID, C_EYE_MID): "centre",
            (R_EYE_MID, C_EYE_OUT): "outer",
            (R_EYE_BOT, C_EYE_IN): "bot_in", (R_EYE_BOT, C_EYE_MID): "bot",
            (R_EYE_BOT, C_EYE_OUT): "bot_out",
        }
        for (r, c), key in eye.items():
            x, z = eye_point(key)
            y = 0.5 * (front_y(x, z) + eyeball_y(x, z, EYE_LID_OFFSETS[0]))
            g[r][c].co = Vector((x, y, z))

        # Skin round the socket must stay in front of the eyeball.
        for r in range(R_BROW - 1, R_NOSE + 1):
            for c in range(C_NOSE, C_EYE_OUT + 2):
                v, c3 = g[r][c], eyeball_centre()
                if (v.co.x - c3.x) ** 2 + (v.co.z - c3.z) ** 2 < (EYE_R + 0.03) ** 2:
                    v.co.y = min(v.co.y, eyeball_y(v.co.x, v.co.z, 0.03))

        # Mouth: cupid's bow, a gently smiling mouth line and full lower lip.
        mouth = {}
        for c in range(4):
            mouth[(R_LIP_TOP, c)] = MOUTH_TOP[c]
            mouth[(R_MOUTH, c)] = MOUTH_LINE[c]
            mouth[(R_LIP_BOT, c)] = MOUTH_BOT[c]
        for (r, c), (x, z) in mouth.items():
            g[r][c].co = Vector((x, front_y(x, z), z))

        # Soft cheekbones: nudge the cheek ring forward a touch.
        for c in (C_EYE_MID, C_EYE_OUT, C_EYE_OUT + 1):
            g[R_CHEEK][c].co.y -= 0.035
        for r, c, d in ((R_NOSE, C_EYE_MID, 0.035), (R_NOSE_BASE, C_EYE_MID, 0.03),
                        (R_NOSE, C_EYE_OUT, 0.02)):
            g[r][c].co.y -= d                     # cheek 'apples' beside the nose
        g[R_LIP_TOP][C_EYE_OUT].co.y -= 0.03      # smile: cheek rises beside the mouth
        g[R_MOUTH][C_EYE_OUT].co.y -= 0.02

    def carve_nostril(self):
        """Shallow inset on the underside of the nose wing - reads as a nostril."""
        hole = [self.cell[(R_NOSE, C_NOSE)]]
        self.bm.normal_update()
        ret = bmesh.ops.inset_individual(self.bm, faces=hole, thickness=0.012,
                                         depth=-0.02, use_even_offset=True)
        rim = ret["faces"]
        self.set_group(rim, G_SKIN)

    # -- 3. eye socket: concentric loops + lid inset, open for the eyeball ----------
    def carve_eye(self):
        region = [self.cell[(R_EYE_TOP, C_EYE_IN)], self.cell[(R_EYE_TOP, C_EYE_MID)],
                  self.cell[(R_EYE_MID, C_EYE_IN)], self.cell[(R_EYE_MID, C_EYE_MID)]]

        def inner_loop():
            """Verts on the current edge of the shrinking eye region."""
            rs = set(region)
            return {v for f in region for e in f.edges
                    if not all(lf in rs for lf in e.link_faces) for v in e.verts}

        def wrap(verts, offset):
            for v in verts:
                v.co.y = eyeball_y(v.co.x, v.co.z, offset)

        groups = (G_SKIN, G_LINER, G_LINER)          # lid, eyeliner, tuck
        for width, offset, group in zip(EYE_LID_RINGS, EYE_LID_OFFSETS[1:], groups):
            ring = self.inset(region, width)
            self.set_group(ring, group)
            wrap(inner_loop(), offset)
        bmesh.ops.delete(self.bm, geom=region, context="FACES")   # eye opening

    # -- 4. mouth: perioral loop + two red lip loops -------------------------------
    def carve_mouth(self):
        region = [self.cell[(r, c)] for r in (R_LIP_TOP, R_MOUTH) for c in (0, 1, 2)]
        mouth_line = {self.grid[R_MOUTH][c] for c in (0, 1, 2)}
        ring_skin = self.inset(region, MOUTH_SKIN_RING)
        ring_lip = self.inset(region, MOUTH_LIP_RING)
        self.set_group(ring_skin, G_SKIN)
        self.set_group(ring_lip + region, G_LIPS)

        red = {v for f in ring_lip + region for v in f.verts}
        for v in red:
            base = front_y(v.co.x, v.co.z)
            if v in mouth_line:
                v.co.y = base - lip_seam_swell(v.co.x)   # closed seam
            else:
                v.co.y = base - lip_swell(v.co.x, v.co.z)
        # The mouth corner tucks back into a small dimple.
        corner = self.grid[R_MOUTH][3]
        corner.co.y = front_y(corner.co.x, corner.co.z) + 0.004

    # -- 5. ears --------------------------------------------------------------------
    def build_ear(self):
        region = [self.cell[(r, C_EAR)] for r in (R_EYE_TOP, R_EYE_MID, R_EYE_BOT, R_CHEEK, R_NOSE)]
        ret = bmesh.ops.extrude_face_region(self.bm, geom=region)
        new_verts = [e for e in ret["geom"] if isinstance(e, bmesh.types.BMVert)]
        new_faces = [e for e in ret["geom"] if isinstance(e, bmesh.types.BMFace)]
        bmesh.ops.delete(self.bm, geom=region, context="FACES")
        # The extruded cap is a 2 x 6 grid of verts (front/back edge, top -> bottom).
        # Bend it into a C: wide, rounded top swept back, narrowing to a soft lobe,
        # standing further off the head at the back than where it joins at the front.
        rows = {}
        for v in new_verts:
            rows.setdefault(round(v.co.z, 4), []).append(v)
        out_x = [0.060, 0.090, 0.095, 0.088, 0.070, 0.045]
        front_dy = [0.060, 0.010, 0.000, 0.000, 0.005, 0.035]
        back_dy = [0.030, 0.060, 0.065, 0.045, 0.015, -0.020]
        dz = [0.050, 0.020, 0.000, 0.000, -0.015, -0.040]
        for i, z in enumerate(sorted(rows, reverse=True)):
            front, back = sorted(rows[z], key=lambda v: v.co.y)
            front.co += Vector((out_x[i], front_dy[i], dz[i]))
            back.co += Vector((out_x[i] + 0.025, back_dy[i], dz[i]))
        caps = [f for f in new_faces if all(v in new_verts for v in f.verts)]
        sides = [f for f in self.bm.faces if f not in caps and any(v in new_verts for v in f.verts)]
        self.set_group(sides + caps, G_SKIN)
        bowl = self.inset(caps, 0.022, depth=-0.035)       # concha
        self.set_group(bowl, G_SKIN)

    # -- 6. eyeball -------------------------------------------------------------------
    def build_eyeball(self):
        bm = self.bm
        ret = bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=EYE_R)
        verts = ret["verts"]
        # Pull the front rings forward so pupil/iris rings are the right size.
        remap = [(0, 0), (22.5, 8), (45, 21), (67.5, 33), (90, 54),
                 (112.5, 84), (135, 118), (157.5, 150), (180, 180)]

        def remap_theta(t):
            for (a0, b0), (a1, b1) in zip(remap, remap[1:]):
                if t <= a1 + 1e-6:
                    return b0 + (b1 - b0) * (t - a0) / (a1 - a0)
            return t

        for v in verts:
            r = v.co.length
            theta = math.degrees(math.acos(max(-1.0, min(1.0, v.co.z / r))))
            az = math.atan2(v.co.y, v.co.x)
            t2 = math.radians(remap_theta(theta))
            v.co = Vector((math.sin(t2) * math.cos(az), math.sin(t2) * math.sin(az),
                           math.cos(t2))) * EYE_R

        centre = eyeball_centre()
        rot = Vector((0.0, 0.0, 1.0)).rotation_difference(eye_gaze()).to_matrix().to_4x4()
        bmesh.ops.transform(bm, matrix=Matrix.Translation(centre) @ rot, verts=verts)

        faces = {f for v in verts for f in v.link_faces}
        forward = eye_gaze()
        for f in faces:
            ang = math.degrees((f.calc_center_median() - centre).angle(forward))
            f[self.grp] = G_PUPIL if ang < 8.0 else G_IRIS if ang < 21.0 else G_WHITE

    # -- 7. eyebrow (closed, volumetric quad loop strip) -----------------------------
    def build_brow(self):
        xs = [0.08, 0.20, 0.32, 0.44, 0.57]
        zs = [0.228, 0.258, 0.278, 0.272, 0.235]
        hs = [0.062, 0.066, 0.060, 0.048, 0.026]
        depth = 0.038
        rings = []
        for x, z, h in zip(xs, zs, hs):
            yb = front_y(x, z) - 0.008                  # seated on the brow ridge
            yf = yb - depth
            rings.append([self.bm.verts.new(p) for p in (
                (x, yb, z - h / 2), (x, yb, z + h / 2), (x, yf, z + h / 2), (x, yf, z - h / 2))])
        faces = []
        for a, b in zip(rings, rings[1:]):
            for i in range(4):
                j = (i + 1) % 4
                faces.append(self.face((a[i], a[j], b[j], b[i]), G_BROW))
        faces.append(self.face(rings[0], G_BROW))
        faces.append(self.face(list(reversed(rings[-1])), G_BROW))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)

    # -- 8. lashes: a winged strip on the upper lid, outer half -----------------------
    def build_lashes(self):
        """A sawtooth fringe of lashes along the upper lid, flaring at the outer corner."""
        # Points along the upper edge of the eyeliner (inset from the lid almond).
        keys = ["top_in", "top", "top_out", "outer"]
        pts = [eye_point(k) for k in keys]
        line = []
        for (x0, z0), (x1, z1) in zip(pts, pts[1:]):
            line += [(x0 + (x1 - x0) * t, z0 + (z1 - z0) * t) for t in (0.0, 0.5)]
        line.append(pts[-1])
        inset = EYE_LID_RINGS[0] + 0.6 * EYE_LID_RINGS[1]
        lengths = [0.020, 0.032, 0.024, 0.042, 0.032, 0.052, 0.060]   # sawtooth
        base, tip = [], []
        n = len(line)
        for i, ((x, z), ln) in enumerate(zip(line, lengths)):
            d = Vector((x - EYE_X, 0.0, z - EYE_Z)).normalized()
            p = Vector((x, 0.0, z)) - d * inset
            p.y = eyeball_y(p.x, p.z, EYE_LID_OFFSETS[2]) - 0.004
            flare = i / (n - 1)                                  # 0 inner .. 1 outer
            out = (d * (0.25 + 0.45 * flare) + Vector((0.0, 0.0, 1.0 - 0.4 * flare))).normalized()
            q = p + out * ln + Vector((0.0, -0.010, 0.0))
            base.append(self.bm.verts.new(p))
            tip.append(self.bm.verts.new(q))
        faces = [self.face((base[i], base[i + 1], tip[i + 1], tip[i]), G_LINER)
                 for i in range(n - 1)]
        for f in faces:
            f.normal_update()
            if f.normal.y > 0:
                f.normal_flip()

    # -- 9. long black hair -----------------------------------------------------------
    def hair_point(self, phi, z, off):
        if z >= HAIR_DRAPE_Z:
            W, Df, Db, yc, n = profile_at(z)
            flare = 1.0
        else:                           # hang straight down from the widest ring
            W, Df, Db, yc, n = profile_at(HAIR_DRAPE_Z)
            flare = 1.0 + 0.30 * (HAIR_DRAPE_Z - z) / 2.0
            off += 0.02 * (HAIR_DRAPE_Z - z)
        x, y = ring_point(phi, (W + off) * flare, (Df + off) * flare,
                          (Db + off) * flare, yc, n)
        return Vector((x, y, z))

    def build_hair(self):
        n_rows = len(HAIR_ROWS_LONG) + 1
        columns, inner = [], {}
        for i, (deg, hl, tipz) in enumerate(zip(HAIR_COLS, HAIRLINE, HAIR_TIPS)):
            phi = math.radians(deg)
            if hl is None:
                zs = HAIR_ROWS_LONG + [tipz]
            else:
                # Share the long columns' crown rows (no sheared quads over the
                # skull), then step evenly down to this meridian's hairline.
                zs = [z for z in HAIR_ROWS_LONG if z > hl + 0.06]
                top, m = zs[-1], n_rows - len(zs)
                zs += [top + (hl - top) * (k + 1) / m for k in range(m)]
            col = []
            for z in zs:
                off = hair_offset(deg, z)
                if hl is None and z < HAIR_DRAPE_Z and i % 2 == 0:
                    off += HAIR_LOCK
                p = self.hair_point(phi, z, off)
                if deg in (0, 180):
                    p.x = 0.0
                col.append(self.bm.verts.new(p))
            columns.append(col)
            if hl is None:
                # Inner surface of the hanging part, HAIR_THICKNESS towards the neck.
                yc = profile_at(HAIR_DRAPE_Z)[3]
                inner[i] = {}
                for r, v in enumerate(col):
                    if v.co.z <= HAIR_DRAPE_Z + 1e-6:
                        d = Vector((v.co.x, v.co.y - yc, 0.0)).normalized()
                        q = v.co - d * HAIR_THICKNESS
                        if deg in (0, 180):
                            q.x = 0.0
                        inner[i][r] = self.bm.verts.new(q)

        pole = self.bm.verts.new((0.0, HEAD_ROWS[0][4] + 0.02, CROWN_Z + HAIR_OFFSET + 0.02))
        first_long = min(inner)
        top = min(inner[first_long])               # first row of the hanging part
        last = n_rows - 1
        faces = []
        F = lambda vs: faces.append(self.face(vs, G_HAIR))
        for c in range(len(columns) - 1):
            a, b = columns[c], columns[c + 1]
            F((pole, a[0], b[0]))
            if c + 1 == first_long:
                # Sideburn column -> first long lock. Above the drape this is
                # ordinary surface; below it a small fan closes the hair over the top
                # of the ear, and the long slab's front edge is closed on its own.
                bi = inner[c + 1]
                for r in range(top):
                    F((a[r], a[r + 1], b[r + 1], b[r]))
                for r in range(top, last):
                    F((a[r], a[r + 1], b[top]))
                for r in range(top, last):
                    F((b[r], b[r + 1], bi[r + 1], bi[r]))
                continue
            for r in range(last):
                F((a[r], a[r + 1], b[r + 1], b[r]))
            if c in inner and c + 1 in inner:
                ai, bi = inner[c], inner[c + 1]
                for r in range(top, last):              # inner surface
                    F((ai[r], bi[r], bi[r + 1], ai[r + 1]))
                F((b[last], a[last], ai[last], bi[last]))   # tips

        # Make the winding consistent across the whole hair piece.
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)

    # -- 10. tidy -----------------------------------------------------------------------
    def snap_seam(self):
        """Anything left on the mirror plane must be exactly x = 0 for a clean weld."""
        for e in self.bm.edges:
            if e.is_boundary:
                for v in e.verts:
                    if abs(v.co.x) < 0.03:
                        v.co.x = 0.0

    def finish(self):
        bm = self.bm
        bmesh.ops.scale(bm, vec=(FINAL_SCALE,) * 3, verts=bm.verts)
        ys = [v.co.y for v in bm.verts]
        zs = [v.co.z for v in bm.verts]
        shift = Vector((0.0, -(min(ys) + max(ys)) / 2, -(min(zs) + max(zs)) / 2))
        bmesh.ops.translate(bm, vec=shift, verts=bm.verts)
        bm.normal_update()

    def build(self):
        self.build_shell()
        self.sculpt_face()
        self.carve_nostril()
        self.carve_eye()
        self.carve_mouth()
        self.build_ear()
        self.build_eyeball()
        self.build_brow()
        self.build_lashes()
        self.build_hair()
        self.snap_seam()
        self.finish()
        return self.bm


# ----------------------------------------------------------------------------------
# Object assembly
# ----------------------------------------------------------------------------------
def create_object(bm, grp_layer_name="grp"):
    mesh = bpy.data.meshes.new(OBJ_NAME)
    grp = bm.faces.layers.int.get(grp_layer_name)
    face_groups = [f[grp] for f in bm.faces]            # bm.faces order == polygons order
    bm.verts.index_update()
    face_verts = [[v.index for v in f.verts] for f in bm.faces]
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(OBJ_NAME, mesh)
    bpy.context.scene.collection.objects.link(obj)

    for name, colour, rough in GROUPS:
        mesh.materials.append(make_material("Head_" + name, colour, rough))
    for poly, g in zip(mesh.polygons, face_groups):
        poly.material_index = g
        poly.use_smooth = False                          # flat, faceted shading

    # One vertex group per material.
    for gi, (name, _c, _r) in enumerate(GROUPS):
        vg = obj.vertex_groups.new(name=name)
        idx = sorted({i for fv, g in zip(face_verts, face_groups) if g == gi for i in fv})
        if idx:
            vg.add(idx, 1.0, "REPLACE")

    mod = obj.modifiers.new("Mirror", "MIRROR")
    mod.use_axis = (True, False, False)
    mod.use_clip = True
    mod.use_mirror_merge = True
    mod.merge_threshold = 0.0002
    mod.use_mirror_vertex_groups = True

    obj.location = (0.0, 0.0, 0.0)
    for o in list(bpy.context.selected_objects):
        if o is not None:
            o.select_set(False)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    return obj


def evaluated_face_count(obj):
    deps = bpy.context.evaluated_depsgraph_get()
    return len(obj.evaluated_get(deps).data.polygons)


def main():
    clear_scene()
    bm = HeadBuilder().build()
    obj = create_object(bm)
    faces = evaluated_face_count(obj)
    print(f"[{OBJ_NAME}] built: {faces} faces after mirror "
          f"({len(obj.data.polygons)} modelled on the +X half)")
    assert faces < MAX_FACES, f"face budget blown: {faces} >= {MAX_FACES}"
    return obj


if __name__ == "__main__":
    main()
