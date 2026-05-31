"""
Cyclone Fence Corner Generator — Low Poly Stylized
===================================================
Generates a 90° corner piece that occupies one grid cell.
The corner post sits at the origin = cell centre when placed in-game.
Arm 1 extends ARM_LENGTH along +X.
Arm 2 extends ARM_LENGTH along +Y.

In Unity, rotate 90° / 180° / 270° for all four corner orientations.

Paste into Blender Scripting tab and click Run Script.
"""

import bpy
import bmesh
from mathutils import Vector
import math

# ─── PARAMETERS ───────────────────────────────────────────────────────────────

ARM_LENGTH          = 0.5    # metres — each arm of the corner
FENCE_HEIGHT        = 2.0    # metres tall
POST_RADIUS         = 0.025  # post thickness
RAIL_RADIUS         = 0.015  # top/bottom rail thickness
WIRE_RADIUS         = 0.006  # wire strand thickness
ARM_COLS            = 3      # diamond columns per arm (~6/m density, matches straight seg)
ARM_ROWS            = 8      # diamond rows (same as straight segment)
BARB_ARM_ANGLE_DEG  = 45     # upward tilt of barbed wire arms
BARB_ARM_LENGTH     = 0.18   # length of each barbed wire arm
BARB_COUNT          = 3      # barbs per arm (fewer on shorter arm)
BARB_SIZE           = 0.025  # barb spike size
POST_SIDES          = 6      # polygon count on posts/rails
WIRE_SIDES          = 4      # polygon count on wire strands

# ─── HELPERS ──────────────────────────────────────────────────────────────────

def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()

def add_cylinder(radius, depth, location, rotation=(0,0,0), sides=6, name="Cyl"):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=sides, radius=radius, depth=depth,
        location=location, rotation=rotation)
    obj = bpy.context.active_object
    obj.name = name
    return obj

def add_ico(size, location, name="Ico"):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=size, location=location)
    obj = bpy.context.active_object
    obj.name = name
    return obj

def join_objects(objects, final_name="Joined"):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        if o and o.name in bpy.data.objects:
            o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    bpy.context.active_object.name = final_name
    return bpy.context.active_object

def add_barb_arm(start_pos, outward_dir, parts, label):
    """Add a barbed wire arm from start_pos angled outward + upward at BARB_ARM_ANGLE_DEG."""
    import mathutils
    ang   = math.radians(BARB_ARM_ANGLE_DEG)
    horiz = Vector(outward_dir).normalized()
    start = Vector(start_pos)
    end   = start + Vector((
        horiz.x * math.cos(ang) * BARB_ARM_LENGTH,
        horiz.y * math.cos(ang) * BARB_ARM_LENGTH,
        math.sin(ang) * BARB_ARM_LENGTH
    ))
    mid     = (start + end) * 0.5
    arm_len = (end - start).length
    arm_dir = (end - start).normalized()

    up   = Vector((0, 0, 1))
    axis = up.cross(arm_dir)
    if axis.length > 1e-6:
        axis.normalize()
        q = mathutils.Quaternion(axis, up.angle(arm_dir))
        e = q.to_euler()
        rot = (e.x, e.y, e.z)
    else:
        rot = (0, 0, 0)

    arm_obj = add_cylinder(WIRE_RADIUS * 1.5, arm_len,
                           (mid.x, mid.y, mid.z), rot, WIRE_SIDES, label)
    parts.append(arm_obj)

    for i in range(BARB_COUNT):
        t  = (i + 0.5) / BARB_COUNT
        bp = start.lerp(end, t)
        for ao in (0, math.radians(90)):
            b = add_ico(BARB_SIZE, (bp.x, bp.y, bp.z), "Barb")
            b.scale = (1.0, 0.3, 0.3)
            b.rotation_euler[2] = ao
            bpy.ops.object.transform_apply(scale=True, rotation=True)
            parts.append(b)

# ─── BUILD ────────────────────────────────────────────────────────────────────

clear_scene()
parts      = []
post_h     = FENCE_HEIGHT + 0.1   # posts protrude slightly above fence
cell_h     = FENCE_HEIGHT / ARM_ROWS
barb_z     = FENCE_HEIGHT          # barbed arms flush with top rail

# ── Corner post at origin (cell centre) ───────────────────────────────────────
parts.append(add_cylinder(POST_RADIUS, post_h,
    location=(0, 0, post_h * 0.5), sides=POST_SIDES, name="Post_Corner"))

# ═════════════════════════════════════════════════════════════════════════════
# ARM 1  —  +X direction
# ═════════════════════════════════════════════════════════════════════════════

# End post
parts.append(add_cylinder(POST_RADIUS, post_h,
    location=(ARM_LENGTH, 0, post_h * 0.5), sides=POST_SIDES, name="Post_Arm1"))

# Bottom & top rails along X
for z in (0.02, FENCE_HEIGHT):
    parts.append(add_cylinder(RAIL_RADIUS, ARM_LENGTH,
        location=(ARM_LENGTH * 0.5, 0, z),
        rotation=(0, math.radians(90), 0), sides=POST_SIDES, name="Rail1"))

# Diamond wires for arm 1 — in the XZ plane (Y = 0)
cw1      = ARM_LENGTH / ARM_COLS            # cell width along X
diag1    = math.sqrt(cw1**2 + cell_h**2)   # exact corner-to-corner length
rFwd1    = (0,  math.atan2(cw1, cell_h), 0)
rBck1    = (0, -math.atan2(cw1, cell_h), 0)

for col in range(ARM_COLS):
    for row in range(ARM_ROWS):
        cx = col * cw1   + cw1   * 0.5
        cz = row * cell_h + cell_h * 0.5
        parts.append(add_cylinder(WIRE_RADIUS, diag1, (cx, 0, cz), rFwd1, WIRE_SIDES, "Wire"))
        parts.append(add_cylinder(WIRE_RADIUS, diag1, (cx, 0, cz), rBck1, WIRE_SIDES, "Wire"))

# Barbed arm on arm-1 end post: points outward in +X
add_barb_arm((ARM_LENGTH, 0, barb_z), (1, 0, 0), parts, "Barb_Arm1End")

# ═════════════════════════════════════════════════════════════════════════════
# ARM 2  —  +Y direction
# ═════════════════════════════════════════════════════════════════════════════

# End post
parts.append(add_cylinder(POST_RADIUS, post_h,
    location=(0, ARM_LENGTH, post_h * 0.5), sides=POST_SIDES, name="Post_Arm2"))

# Bottom & top rails along Y
for z in (0.02, FENCE_HEIGHT):
    parts.append(add_cylinder(RAIL_RADIUS, ARM_LENGTH,
        location=(0, ARM_LENGTH * 0.5, z),
        rotation=(math.radians(90), 0, 0), sides=POST_SIDES, name="Rail2"))

# Diamond wires for arm 2 — in the YZ plane (X = 0)
# Same cell dimensions as arm 1 (arm is same length)
# Arm 2 runs along Y, so X-rotation orients wire in YZ plane
rFwd2    = ( math.atan2(cw1, cell_h), 0, 0)
rBck2    = (-math.atan2(cw1, cell_h), 0, 0)

for col in range(ARM_COLS):
    for row in range(ARM_ROWS):
        cy = col * cw1    + cw1    * 0.5
        cz = row * cell_h + cell_h * 0.5
        parts.append(add_cylinder(WIRE_RADIUS, diag1, (0, cy, cz), rFwd2, WIRE_SIDES, "Wire"))
        parts.append(add_cylinder(WIRE_RADIUS, diag1, (0, cy, cz), rBck2, WIRE_SIDES, "Wire"))

# Barbed arm on arm-2 end post: points outward in +Y
add_barb_arm((0, ARM_LENGTH, barb_z), (0, 1, 0), parts, "Barb_Arm2End")

# ═════════════════════════════════════════════════════════════════════════════
# CORNER POST barbed arm — points diagonally outward (bisects -X / -Y angle)
# The "outside" of the corner is in the -X / -Y quadrant
# ═════════════════════════════════════════════════════════════════════════════
d = 1.0 / math.sqrt(2)
add_barb_arm((0, 0, barb_z), (-d, -d, 0), parts, "Barb_Corner")

# ── Join everything & set origin to corner post (already at world 0,0,0) ─────
corner = join_objects(parts, "CycloneFence_Corner")
bpy.context.scene.cursor.location = (0, 0, 0)
bpy.ops.object.origin_set(type='ORIGIN_CURSOR')

print("=" * 55)
print("CycloneFence_Corner created successfully.")
print(f"  Corner post : origin (0, 0, 0)  = cell centre in-game")
print(f"  Arm 1 : +X  0 → {ARM_LENGTH}m")
print(f"  Arm 2 : +Y  0 → {ARM_LENGTH}m")
print(f"  Height: {FENCE_HEIGHT}m")
print()
print("In Unity: rotate 0 / 90 / 180 / 270 on Y for all four")
print("corner orientations from this single mesh.")
print("Export FBX to Assets/5. Models/")
print("=" * 55)
