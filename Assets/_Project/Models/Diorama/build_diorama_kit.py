# Builds the cutaway diorama ground kit (v3) + preview render + build-menu icons.
# Run headless:
#   blender.exe -b --factory-startup --python build_diorama_kit.py -- <projectRoot>
#
# Every block: 2.65 x 2.65 x 2.65 m, authored with the pivot at the centre of the TOP face
# (paving top at z=0). Unity's Grounds2Setup editor script re-pivots the mesh to the game's
# grounds convention (first cell centre, top at y=1.05).
# Seams: the vertex column at every block corner sits exactly on the base layer heights with
# no outward push, so blocks meet cleanly in any layout / any 90-degree rotation.
import bpy, bmesh, math, sys, os, random
import numpy as np
from mathutils import Vector

ROOT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.getcwd()
OUT = os.path.join(ROOT, "Assets/_Project/Models/Diorama")
ICONS = os.path.join(ROOT, "Assets/_Project/Sprites/Icons/ItemThimbnails")
CAT_ICONS = os.path.join(ROOT, "Assets/_Project/Sprites/Icons/Category Icons")
for d in (OUT, ICONS, CAT_ICONS):
    os.makedirs(d, exist_ok=True)

S = 2.65
H = S / 2
C = 4                 # columns per side (and top grid resolution)
GRASS_RISE = 0.08

# (name, thickness, colour sRGB, extra subdivision rows). Surface colour comes from the tile.
LAYERS = [
    ("Surface", 0.26, None, 1),                      # straight-cut slab, like frosting
    ("Topsoil", 0.20, (0.460, 0.330, 0.210), 1),
    ("Dirt",    0.64, (0.300, 0.210, 0.140), 2),
    ("Rock",    1.55, (0.380, 0.370, 0.355), 3),
]
assert abs(sum(l[1] for l in LAYERS) - S) < 1e-6

COLOURS = {
    "Asphalt":  (0.270, 0.280, 0.300),
    "Concrete": (0.600, 0.610, 0.610),
    "Grass":    (0.420, 0.620, 0.200),
    "Curb":     (0.800, 0.800, 0.780),
    "RockDark": (0.300, 0.290, 0.280),
    "RockLight": (0.500, 0.490, 0.470),
}

def lin(c):
    return tuple(x ** 2.2 for x in c)

def mat(name, rgb):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (*lin(rgb), 1)
    b.inputs["Roughness"].default_value = 0.95
    m.diffuse_color = (*lin(rgb), 1)
    return m

for n, c in COLOURS.items():
    mat("Dio_" + n, c)
for n, t, c, _ in LAYERS[1:]:
    mat("Dio_" + n, c)

def add_chunk(bm, rnd, centre, normal, size, mat_index):
    """Jagged low-poly chunk: convex hull of random points, squashed along the face normal."""
    n = Vector(normal)
    pts = []
    for _ in range(9):
        d = Vector((rnd.gauss(0, 1), rnd.gauss(0, 1), rnd.gauss(0, 1))).normalized()
        d *= size * rnd.uniform(0.7, 1.0)
        along = d.dot(n)
        d = d - n * along * 0.30          # flatter toward the face
        pts.append(bm.verts.new(centre + d))
    res = bmesh.ops.convex_hull(bm, input=pts)
    bmesh.ops.delete(bm, geom=[g for g in res["geom_interior"] if isinstance(g, bmesh.types.BMVert)], context="VERTS")
    for g in res["geom"]:
        if isinstance(g, bmesh.types.BMFace):
            g.material_index = mat_index

def build_block(name, surf, seed, top_jitter, rise=0.0):
    rnd = random.Random(seed)
    me = bpy.data.meshes.new(name)
    ob = bpy.data.objects.new(name, me)
    names = ["Dio_" + surf] + ["Dio_" + l[0] for l in LAYERS[1:]] + ["Dio_RockDark", "Dio_RockLight"]
    for m in names:
        me.materials.append(bpy.data.materials[m])
    IDX = {n.replace("Dio_", ""): i for i, n in enumerate(names)}
    bm = bmesh.new()

    rows = [(rise, 0)]          # (z, layer index of the row)
    z = 0.0
    for li, (_, t, _, sub) in enumerate(LAYERS):
        top = z
        z -= t
        for r in range(1, sub):
            rows.append((top - t * r / sub, li))
        rows.append((z, li + 1 if li + 1 < len(LAYERS) else li))
    surf_bottom = -LAYERS[0][1]
    soil_bottom = -(LAYERS[0][1] + LAYERS[1][1] + LAYERS[2][1])

    sides = [((-H, -H), (H, -H), (0, -1)),
             ((H, -H), (H, H), (1, 0)),
             ((H, H), (-H, H), (0, 1)),
             ((-H, H), (-H, -H), (-1, 0))]
    bottom_ring = []
    for (ax, ay), (bx, by), (nx, ny) in sides:
        grid = []
        for ri, (zr, _) in enumerate(rows):
            row = []
            for k in range(C + 1):
                t = k / C
                x, y, zz = ax + (bx - ax) * t, ay + (by - ay) * t, zr
                interior = 0 < k < C
                last = ri == len(rows) - 1
                if interior and zr < surf_bottom - 1e-6:       # never touch the slab band
                    if zr > soil_bottom + 1e-6:                  # soil: lumpy dirt
                        push = rnd.uniform(0.0, 0.09)
                        zz += rnd.uniform(-0.05, 0.05)
                    elif abs(zr - soil_bottom) < 1e-6:            # soil/rock line: wavy
                        push = rnd.uniform(0.0, 0.06)
                        zz += rnd.uniform(-0.10, 0.10)
                    else:                                         # rock: jagged
                        push = rnd.uniform(0.05, 0.26)
                        zz += rnd.uniform(0.0, 0.45) if last else rnd.uniform(-0.14, 0.14)
                    x += nx * push; y += ny * push
                row.append(bm.verts.new((x, y, zz)))
            grid.append(row)
        for ri in range(len(rows) - 1):
            zmid = (rows[ri][0] + rows[ri + 1][0]) / 2
            if zmid > surf_bottom:
                band = 0
            elif zmid > surf_bottom - LAYERS[1][1]:
                band = IDX["Topsoil"]
            elif zmid > soil_bottom:
                band = IDX["Dirt"]
            else:
                band = IDX["Rock"]
            for k in range(C):
                a, b = grid[ri][k], grid[ri + 1][k]
                c2, d = grid[ri + 1][k + 1], grid[ri][k + 1]
                if band == 0:
                    bm.faces.new((a, b, c2, d)).material_index = 0   # clean straight slab face
                    continue
                fs = [(a, b, c2), (a, c2, d)] if (k + ri) % 2 == 0 else [(a, b, d), (b, c2, d)]
                for f in fs:
                    face = bm.faces.new(f)
                    face.material_index = IDX["RockDark"] if band == IDX["Rock"] and rnd.random() < 0.3 else band
        bottom_ring += grid[-1][:-1]

    top = [[None] * (C + 1) for _ in range(C + 1)]
    for i in range(C + 1):
        for j in range(C + 1):
            x, y = -H + S * i / C, -H + S * j / C
            zz = rise
            if 0 < i < C and 0 < j < C:
                zz += rnd.uniform(-top_jitter, top_jitter)
                x += rnd.uniform(-0.15, 0.15); y += rnd.uniform(-0.15, 0.15)
            top[i][j] = bm.verts.new((x, y, zz))
    for i in range(C):
        for j in range(C):
            a, b, c2, d = top[i][j], top[i + 1][j], top[i + 1][j + 1], top[i][j + 1]
            fs = [(a, b, c2), (a, c2, d)] if rnd.random() < 0.5 else [(a, b, d), (b, c2, d)]
            for f in fs:
                bm.faces.new(f).material_index = 0

    centre = bm.verts.new((0, 0, -S + 0.2))
    for i in range(len(bottom_ring)):
        bm.faces.new((bottom_ring[(i + 1) % len(bottom_ring)], bottom_ring[i], centre)).material_index = IDX["Rock"]

    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)

    # Chunks poking out of the cut: dirt clumps + grey rocks in the soil, bigger rocks below.
    for (ax, ay), (bx, by), (nx, ny) in sides:
        n = Vector((nx, ny, 0))
        def on_face(t, zc, out):
            return Vector((ax + (bx - ax) * t, ay + (by - ay) * t, zc)) + n * out
        for _ in range(4):   # dirt clumps
            sz = rnd.uniform(0.13, 0.22)
            add_chunk(bm, rnd, on_face(rnd.uniform(0.10, 0.90), rnd.uniform(soil_bottom + 0.05, surf_bottom - 0.12 - sz), rnd.uniform(0.05, 0.10)),
                      n, sz, IDX["Dirt"] if rnd.random() < 0.6 else IDX["Topsoil"])
        for _ in range(3):   # small grey rocks in the dirt
            sz = rnd.uniform(0.10, 0.18)
            add_chunk(bm, rnd, on_face(rnd.uniform(0.10, 0.90), rnd.uniform(soil_bottom, surf_bottom - 0.15 - sz), rnd.uniform(0.05, 0.10)),
                      n, sz, IDX["RockLight"])
        for _ in range(6):   # big jagged rocks lower down
            add_chunk(bm, rnd, on_face(rnd.uniform(0.10, 0.90), rnd.uniform(-S + 0.35, soil_bottom - 0.10), rnd.uniform(0.12, 0.24)),
                      n, rnd.uniform(0.24, 0.42), IDX["RockLight"] if rnd.random() < 0.65 else IDX["Rock"])

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        f.smooth = False
    bm.to_mesh(me)
    bm.free()
    return ob

def build_curb(name):
    """White curb strip: 2.65 long (along X), 0.12 wide, 0.10 tall, pivot = bottom centre."""
    me = bpy.data.meshes.new(name)
    ob = bpy.data.objects.new(name, me)
    me.materials.append(bpy.data.materials["Dio_Curb"])
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * S, v.co.y * 0.12, (v.co.z + 0.5) * 0.10))
    bm.to_mesh(me); bm.free()
    return ob

# --- scene
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)
kit = bpy.data.collections.new("DioramaKit")
bpy.context.scene.collection.children.link(kit)
lay = bpy.data.collections.new("PreviewLayout")
bpy.context.scene.collection.children.link(lay)

tiles = {
    "Dio_Tile_ParkingLot": build_block("Dio_Tile_ParkingLot", "Asphalt", 11, 0.025),
    "Dio_Tile_Slab":       build_block("Dio_Tile_Slab", "Concrete", 23, 0.010),
    "Dio_Tile_Grass":      build_block("Dio_Tile_Grass", "Grass", 37, 0.07, rise=GRASS_RISE),
    "Dio_Curb":            build_curb("Dio_Curb"),
}
for ob in tiles.values():
    kit.objects.link(ob)

OX = 30.0
grid = [
    "GGGPPPPSSSS",
    "GGGPPPPSSSS",
    "PPPPPPPSSSS",
    "PPPPPPPSSSS",
    "PPPPPPPSSGG",
    "PPPPPPPGG..",
]
key = {"P": "Dio_Tile_ParkingLot", "S": "Dio_Tile_Slab", "G": "Dio_Tile_Grass"}
rows_n = len(grid)
def at(r, c):
    return grid[r][c] if 0 <= r < rows_n and 0 <= c < len(grid[r]) else "."
for r, line in enumerate(grid):
    for c, ch in enumerate(line):
        if ch == ".":
            continue
        d = bpy.data.objects.new(f"L_{r}_{c}", tiles[key[ch]].data)
        d.location = (OX + c * S, (rows_n - 1 - r) * S, 0)
        d.rotation_euler.z = math.radians(90 * ((r * 7 + c * 3) % 4))
        lay.objects.link(d)
        if ch == "G":
            continue
        for dr, dc, rot, ox, oy in ((-1, 0, 0, 0, 1), (1, 0, 0, 0, -1), (0, -1, 90, -1, 0), (0, 1, 90, 1, 0)):
            if at(r + dr, c + dc) == "G":
                cb = bpy.data.objects.new(f"Curb_{r}_{c}_{dr}{dc}", tiles["Dio_Curb"].data)
                cb.location = (OX + c * S + ox * (H - 0.06), (rows_n - 1 - r) * S + oy * (H - 0.06), 0)
                cb.rotation_euler.z = math.radians(rot)
                lay.objects.link(cb)

# --- lighting / render (soft, not blown out)
sc = bpy.context.scene
eng = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
sc.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in eng else "BLENDER_EEVEE"
sc.view_settings.view_transform = "AgX"
sc.view_settings.exposure = -0.10
w = bpy.data.worlds.new("W"); sc.world = w; w.use_nodes = True
bg = w.node_tree.nodes["Background"]
bg.inputs[0].default_value = (0.020, 0.026, 0.032, 1); bg.inputs[1].default_value = 1.0
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
sun.data.energy = 2.2; sun.data.angle = math.radians(25)
sun.rotation_euler = (math.radians(45), math.radians(15), math.radians(-30))
sc.collection.objects.link(sun)

def look(cam, target):
    cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()

cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
sc.collection.objects.link(cam); sc.camera = cam
cx, cy = OX + 11 * S / 2, rows_n * S / 2

def render(path, rx, ry, transparent=False):
    sc.render.resolution_x, sc.render.resolution_y = rx, ry
    sc.render.film_transparent = transparent
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)

cam.data.lens = 55
cam.location = (cx - 30, cy - 50, 34); look(cam, (cx + 1, cy, -2.0))
render(os.path.join(OUT, "Diorama_Preview_v3.png"), 1600, 900)

# Close-up of a cut face so the strata/rocks can be judged.
cam.location = (OX + 3 * S - 5, -10, 4.5); look(cam, (OX + 3 * S, -1.3, -1.2))
render(os.path.join(OUT, "Diorama_Preview_v3_Closeup.png"), 1600, 900)

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "DioramaKit.blend"))

# --- FBX: kit tiles only
bpy.ops.object.select_all(action="DESELECT")
for ob in tiles.values():
    ob.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "DioramaKit.fbx"), use_selection=True,
    object_types={"MESH"}, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
    bake_space_transform=True, mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False)

# --- icons: hide the layout, show one kit object at a time
lay.hide_render = True
bg.inputs[0].default_value = (0.20, 0.24, 0.26, 1)
cam.data.type = "ORTHO"
def solo(names):
    for o in kit.objects:
        o.hide_render = o.name not in names
icon_names = {"Dio_Tile_ParkingLot": "ParkingLot", "Dio_Tile_Slab": "Slab", "Dio_Tile_Grass": "Grass", "Dio_Curb": "Curb"}
for obn, short in icon_names.items():
    solo([obn])
    tiles[obn].location = (0, 0, 0)
    if obn == "Dio_Curb":
        cam.data.ortho_scale = 3.2
        cam.location = (2.2, -3.0, 2.0); look(cam, (0, 0, 0.0))
    else:
        cam.data.ortho_scale = 5.4
        cam.location = (6.0, -7.0, 5.2); look(cam, (0, 0, -1.25))
    render(os.path.join(ICONS, f"Dio_{short} Thumbnail.png"), 512, 512)

# Category icon: a little 3-tile diorama on a rounded card, matching the other category icons.
solo(["Dio_Tile_ParkingLot", "Dio_Tile_Grass", "Dio_Tile_Slab"])
tiles["Dio_Tile_ParkingLot"].location = (0, 0, 0)
tiles["Dio_Tile_Grass"].location = (-S, 0, 0)
tiles["Dio_Tile_Slab"].location = (0, S, 0)
cam.data.ortho_scale = 9.0
cam.location = (8.0, -9.5, 7.5); look(cam, (-0.9, 0.9, -1.4))
tmp = os.path.join(OUT, "_cat_tmp.png")
render(tmp, 512, 512, transparent=True)
img = bpy.data.images.load(tmp)
px = np.array(img.pixels[:], dtype=np.float32).reshape(512, 512, 4)
yy, xx = np.mgrid[0:512, 0:512]
m, r = 14, 36
cxs = np.clip(xx, m + r, 511 - m - r); cys = np.clip(yy, m + r, 511 - m - r)
dist = np.hypot(xx - cxs, yy - cys)
card = (dist <= r) & (xx >= m) & (xx <= 511 - m) & (yy >= m) & (yy <= 511 - m)
border = card & (dist > r - 5) | card & ((xx < m + 5) | (xx > 506 - m) | (yy < m + 5) | (yy > 506 - m))
out = np.zeros_like(px)
out[card] = (0.63, 0.66, 0.69, 1.0)
out[..., :3] = out[..., :3] ** 2.2          # card colour authored in sRGB
a = px[..., 3:4]
out[..., :3] = px[..., :3] * a + out[..., :3] * (1 - a)
out[border] = (0.01, 0.01, 0.01, 1.0)
out[..., 3] = np.where(card, 1.0, 0.0)
cat = bpy.data.images.new("cat", 512, 512, alpha=True)
cat.pixels[:] = out.ravel()
cat.filepath_raw = os.path.join(CAT_ICONS, "Grounds2.png")
cat.file_format = "PNG"
cat.save()
os.remove(tmp)
print("DIORAMA_DONE", {n: len(o.data.polygons) for n, o in tiles.items()})
