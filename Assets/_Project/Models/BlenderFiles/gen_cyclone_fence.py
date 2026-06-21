"""
Cyclone Fence Generator — Low Poly Stylized
============================================
Paste this into Blender's Scripting tab and click Run Script.

Generates:
  - One fence segment (1m wide x 1m tall) with diamond wire pattern
  - Top and bottom rails
  - Two end posts
  - Barbed wire arm at 45 degrees with barbs (Array modifier applied)

All joined into a single mesh named "CycloneFence_Segment".
Ready to export as FBX for Unity.

Adjust the parameters below to taste.
"""

import bpy
import bmesh
from mathutils import Vector
import math

# ─── PARAMETERS ───────────────────────────────────────────────────────────────

SEGMENT_WIDTH       = 1.325    # metres wide  (matches your grid cell)
FENCE_HEIGHT        = 2.0      # metres tall (body only, barbed wire adds ~0.25m)
POST_RADIUS         = 0.025    # post thickness
RAIL_RADIUS         = 0.015    # top/bottom rail thickness
WIRE_RADIUS         = 0.006    # fence wire strand thickness
DIAMOND_COLS        = 8        # 8 columns per 1.325m segment
DIAMOND_ROWS        = 8        # 8 rows per 2.0m height
BARB_ARM_ANGLE_DEG  = 45       # outward angle of barbed wire arm
BARB_ARM_LENGTH     = 0.18     # length of the barbed wire arm
BARB_COUNT          = 5        # number of barbs along the arm
BARB_SIZE           = 0.025    # size of each barb spike
POST_SIDES          = 6        # polygon count on posts/rails (keep low)
WIRE_SIDES          = 4        # polygon count on wire strands

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

# ─── BUILD ────────────────────────────────────────────────────────────────────

clear_scene()

parts = []

# ── 1. Left and Right Posts ───────────────────────────────────────────────────
post_height = FENCE_HEIGHT + 0.1   # posts stick up slightly above the fence

for x in (0.0, SEGMENT_WIDTH):
    p = add_cylinder(
        radius=POST_RADIUS,
        depth=post_height,
        location=(x, 0, post_height * 0.5),
        sides=POST_SIDES,
        name="Post")
    parts.append(p)

# ── 2. Top and Bottom Rails ───────────────────────────────────────────────────
for z in (0.02, FENCE_HEIGHT):
    r = add_cylinder(
        radius=RAIL_RADIUS,
        depth=SEGMENT_WIDTH,
        location=(SEGMENT_WIDTH * 0.5, 0, z),
        rotation=(0, math.radians(90), 0),
        sides=POST_SIDES,
        name="Rail")
    parts.append(r)

# ── 3. Diamond Wire Pattern ───────────────────────────────────────────────────
# Each diamond is formed by two crossing diagonal wire strands.
# We create a grid of short diagonal cylinders that together read as chain-link.

cell_w   = SEGMENT_WIDTH / DIAMOND_COLS
cell_h   = FENCE_HEIGHT  / DIAMOND_ROWS
diag_len = math.sqrt(cell_w**2 + cell_h**2)  # exact corner-to-corner length

# ── Correct rotation: orient cylinder along actual diagonal direction ──────
# Blender cylinder default axis = Z.  Positive Y rotation turns Z toward X.
# Forward (/) : direction (cell_w, 0, cell_h)  →  Y-rotate by +atan2(cell_w, cell_h)
# Backward (\): direction (-cell_w, 0, cell_h) →  Y-rotate by -atan2(cell_w, cell_h)
rot_fwd = (0,  math.atan2(cell_w, cell_h), 0)
rot_bck = (0, -math.atan2(cell_w, cell_h), 0)

for col in range(DIAMOND_COLS):
    for row in range(DIAMOND_ROWS):
        cx = col * cell_w + cell_w * 0.5
        cz = row * cell_h + cell_h * 0.5

        # forward diagonal ( / )  — bottom-left to top-right
        w1 = add_cylinder(
            radius=WIRE_RADIUS,
            depth=diag_len,
            location=(cx, 0, cz),
            rotation=rot_fwd,
            sides=WIRE_SIDES,
            name="Wire")
        parts.append(w1)

        # backward diagonal ( \ )  — bottom-right to top-left
        w2 = add_cylinder(
            radius=WIRE_RADIUS,
            depth=diag_len,
            location=(cx, 0, cz),
            rotation=rot_bck,
            sides=WIRE_SIDES,
            name="Wire")
        parts.append(w2)

# ── 4. Barbed Wire Arms (one per post, angled 45° outward) ───────────────────
arm_angle_rad = math.radians(BARB_ARM_ANGLE_DEG)

for x in (0.0, SEGMENT_WIDTH):
    arm_z   = FENCE_HEIGHT
    arm_end = Vector((
        x + math.cos(arm_angle_rad) * BARB_ARM_LENGTH * (1 if x == 0 else -1),
        math.sin(arm_angle_rad) * BARB_ARM_LENGTH * (-1),   # outward (negative Y)
        arm_z + math.sin(arm_angle_rad) * BARB_ARM_LENGTH
    ))
    arm_start = Vector((x, 0, arm_z))
    arm_mid   = (arm_start + arm_end) * 0.5
    arm_len   = (arm_end - arm_start).length
    arm_dir   = (arm_end - arm_start).normalized()

    # rotation to align cylinder with arm direction
    up = Vector((0, 0, 1))
    axis = up.cross(arm_dir)
    if axis.length < 1e-6:
        rot = (0, 0, 0)
    else:
        axis.normalize()
        angle = up.angle(arm_dir)
        # convert axis-angle to Euler
        import mathutils
        q = mathutils.Quaternion(axis, angle)
        e = q.to_euler()
        rot = (e.x, e.y, e.z)

    arm = add_cylinder(
        radius=WIRE_RADIUS * 1.5,
        depth=arm_len,
        location=(arm_mid.x, arm_mid.y, arm_mid.z),
        rotation=rot,
        sides=WIRE_SIDES,
        name="BarbArm")
    parts.append(arm)

    # Barbs along the arm — small flattened diamonds
    for i in range(BARB_COUNT):
        t = (i + 0.5) / BARB_COUNT
        bp = arm_start.lerp(arm_end, t)

        # two crossed spikes per barb point
        for angle_offset in (0, math.radians(90)):
            b = add_ico(size=BARB_SIZE, location=(bp.x, bp.y, bp.z), name="Barb")
            # flatten into a spike shape
            b.scale = (1.0, 0.3, 0.3)
            b.rotation_euler[2] = angle_offset
            bpy.ops.object.transform_apply(scale=True, rotation=True)
            parts.append(b)

# ── 5. Matching barbed wire along the top rail (centre span) ─────────────────
top_arm_z   = FENCE_HEIGHT
top_arm_end = Vector((
    SEGMENT_WIDTH * 0.5,
    -math.sin(arm_angle_rad) * BARB_ARM_LENGTH,
    top_arm_z + math.sin(arm_angle_rad) * BARB_ARM_LENGTH
))
top_arm_start = Vector((SEGMENT_WIDTH * 0.5, 0, top_arm_z))
top_mid = (top_arm_start + top_arm_end) * 0.5
top_len = (top_arm_end - top_arm_start).length

up2  = Vector((0, 0, 1))
dir2 = (top_arm_end - top_arm_start).normalized()
ax2  = up2.cross(dir2)
if ax2.length > 1e-6:
    ax2.normalize()
    ang2 = up2.angle(dir2)
    import mathutils as mu2
    q2 = mu2.Quaternion(ax2, ang2)
    e2 = q2.to_euler()
    rot2 = (e2.x, e2.y, e2.z)
else:
    rot2 = (0, 0, 0)

top_arm_obj = add_cylinder(
    radius=WIRE_RADIUS * 1.5, depth=top_len,
    location=(top_mid.x, top_mid.y, top_mid.z),
    rotation=rot2, sides=WIRE_SIDES, name="TopBarbArm")
parts.append(top_arm_obj)

for i in range(BARB_COUNT):
    t  = (i + 0.5) / BARB_COUNT
    bp = top_arm_start.lerp(top_arm_end, t)
    for ao in (0, math.radians(90)):
        b = add_ico(size=BARB_SIZE, location=(bp.x, bp.y, bp.z), name="Barb")
        b.scale = (1.0, 0.3, 0.3)
        b.rotation_euler[2] = ao
        bpy.ops.object.transform_apply(scale=True, rotation=True)
        parts.append(b)

# ─── JOIN & ORIGIN ─────────────────────────────────────────────────────────────

fence = join_objects(parts, "CycloneFence_Segment")

# Set origin to bottom-left corner (0, 0, 0) so it aligns with Unity grid
bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
bpy.context.scene.cursor.location = (0, 0, 0)
bpy.ops.object.origin_set(type='ORIGIN_CURSOR')

print("=" * 50)
print("CycloneFence_Segment created successfully.")
print(f"Tris: {sum(len(p.data.polygons) * 1 for p in [fence])}")
print("Export as FBX to Assets/5. Models/")
print("=" * 50)
