"""
FORK IT! — Forklift + Rats Logo Generator
==========================================
Generates a low-poly stylized scene:
  - A forklift at an action angle, slightly left of centre, heading toward camera
  - Two cartoonish rats in front of it, tongues out, eyes looking back in terror
  - Simple Disney-esque forms — chunky, readable, charming

Paste into Blender Scripting tab and Run Script.
The scene is set up as a logo/hero image — render from Camera.001
Recommended render: 1024x512, transparent background (RGBA)
"""

import bpy
import bmesh
from mathutils import Vector, Matrix
import math

# ─── HELPERS ──────────────────────────────────────────────────────────────────

def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    for m in list(bpy.data.materials): bpy.data.materials.remove(m)

def mat(name, r, g, b, a=1.0, emit=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (r, g, b, a)
    bsdf.inputs["Roughness"].default_value = 0.8
    bsdf.inputs["Metallic"].default_value = 0.1
    if emit > 0:
        bsdf.inputs["Emission"].default_value = (r, g, b, a)
        bsdf.inputs["Emission Strength"].default_value = emit
    return m

def apply_mat(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(mat)

def box(name, loc, size, rot=(0,0,0)):
    bpy.ops.mesh.primitive_cube_add(location=loc, rotation=[math.radians(r) for r in rot])
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = (size[0]/2, size[1]/2, size[2]/2)
    bpy.ops.object.transform_apply(scale=True)
    return obj

def cylinder(name, loc, radius, depth, rot=(90,0,0)):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=8, radius=radius, depth=depth,
        location=loc, rotation=[math.radians(r) for r in rot])
    obj = bpy.context.active_object
    obj.name = name
    return obj

def sphere(name, loc, radius, sub=2):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub, radius=radius, location=loc)
    obj = bpy.context.active_object
    obj.name = name
    return obj

# ─── MATERIALS ────────────────────────────────────────────────────────────────

clear_scene()

M_YELLOW    = mat("Yellow",    1.0,  0.75, 0.05)   # forklift body
M_BLACK     = mat("Black",     0.05, 0.05, 0.05)   # tyres, outlines
M_ORANGE    = mat("Orange",    0.9,  0.45, 0.1)    # forklift accents
M_GREY      = mat("Grey",      0.55, 0.55, 0.55)   # forks, metal
M_RED       = mat("Red",       0.85, 0.12, 0.12)   # stripes / lights
M_WHITE     = mat("White",     0.95, 0.95, 0.95)   # eyes whites
M_DARK_GREY = mat("DarkGrey",  0.2,  0.2,  0.2)    # engine block
M_PINK      = mat("Pink",      0.95, 0.55, 0.65)   # rat tongues / ears
M_RAT       = mat("Rat",       0.72, 0.65, 0.60)   # rat fur
M_RAT_DARK  = mat("RatDark",   0.50, 0.43, 0.40)   # rat shadow
M_PUPIL     = mat("Pupil",     0.02, 0.02, 0.02)   # eye pupils
M_HEADLIGHT = mat("Headlight", 1.0,  0.95, 0.7, emit=2.0)

# ─── FORKLIFT ─────────────────────────────────────────────────────────────────
# Positioned heading toward camera, angled ~20° to the left

FK_X   = -0.4   # slightly left of centre
FK_Y   = -0.5   # back a bit
FK_ROT = -25    # yaw toward camera-left

# Body
body = box("FK_Body", (FK_X, FK_Y, 0.45), (1.4, 1.8, 0.9))
body.rotation_euler[2] = math.radians(FK_ROT)
apply_mat(body, M_YELLOW)

# Cab / roof
cab = box("FK_Cab", (FK_X, FK_Y+0.05, 0.95+0.25), (1.1, 1.2, 0.5))
cab.rotation_euler[2] = math.radians(FK_ROT)
apply_mat(cab, M_YELLOW)

# Roll cage bars
for dx in (-0.45, 0.45):
    bar = box(f"FK_Bar_{dx}", (FK_X + dx*math.cos(math.radians(FK_ROT)),
                                FK_Y + dx*math.sin(math.radians(FK_ROT)), 1.05), (0.08, 0.08, 0.9))
    apply_mat(bar, M_ORANGE)

# Engine block hump at rear
eng = box("FK_Engine", (FK_X, FK_Y - 0.6, 0.6), (1.3, 0.5, 0.7))
eng.rotation_euler[2] = math.radians(FK_ROT)
apply_mat(eng, M_DARK_GREY)

# Counterweight
cw = box("FK_CWeight", (FK_X, FK_Y - 0.95, 0.35), (1.2, 0.3, 0.7))
cw.rotation_euler[2] = math.radians(FK_ROT)
apply_mat(cw, M_DARK_GREY)

# Mast (vertical fork tower) — front of forklift
mast_x = FK_X + 0.8 * math.cos(math.radians(FK_ROT + 90))
mast_y = FK_Y + 0.8 * math.sin(math.radians(FK_ROT + 90))
for mx in (-0.25, 0.25):
    mast = box(f"FK_Mast_{mx}", (mast_x + mx*0.5, mast_y, 1.0), (0.1, 0.15, 2.0))
    mast.rotation_euler[2] = math.radians(FK_ROT)
    apply_mat(mast, M_GREY)

# Forks
for fy in (-0.2, 0.2):
    fork = box(f"FK_Fork_{fy}", (mast_x + fy*0.5, mast_y + 0.4, 0.05), (0.12, 1.2, 0.08))
    fork.rotation_euler[2] = math.radians(FK_ROT)
    apply_mat(fork, M_GREY)

# Wheels (4 chunky cylinders)
for wx, wy in [(-0.55, -0.7), (0.55, -0.7), (-0.55, 0.65), (0.55, 0.65)]:
    # Rotate wheel position by forklift yaw
    angle = math.radians(FK_ROT)
    rx = FK_X + wx * math.cos(angle) - wy * math.sin(angle)
    ry = FK_Y + wx * math.sin(angle) + wy * math.cos(angle)
    wheel = cylinder(f"FK_Wheel_{wx}_{wy}", (rx, ry, 0.22), 0.28, 0.3, rot=(0,90,0))
    apply_mat(wheel, M_BLACK)
    # Hubcap
    hub = cylinder(f"FK_Hub_{wx}_{wy}", (rx, ry, 0.22), 0.14, 0.32, rot=(0,90,0))
    apply_mat(hub, M_ORANGE)

# Headlights
for hx in (-0.35, 0.35):
    hl_x = mast_x + hx*0.5
    hl_y = mast_y + 0.01
    hl = sphere(f"FK_Light_{hx}", (hl_x, hl_y, 0.55), 0.1)
    apply_mat(hl, M_HEADLIGHT)

# Red stripe
stripe = box("FK_Stripe", (FK_X, FK_Y, 0.45), (1.41, 1.81, 0.12))
stripe.rotation_euler[2] = math.radians(FK_ROT)
apply_mat(stripe, M_RED)

# ─── RATS ─────────────────────────────────────────────────────────────────────
# Two rats, running ahead of the forklift, looking back in terror

def make_rat(name, loc, flip=False):
    grp = []
    x, y, z = loc
    scl = 1.0 if not flip else -1.0  # mirror second rat

    # Body — chunky oval
    body = sphere(f"{name}_Body", (x, y, z+0.18), 0.22)
    body.scale = (0.85, 1.1, 0.75)
    bpy.ops.object.transform_apply(scale=True)
    apply_mat(body, M_RAT)
    grp.append(body)

    # Head — big round ball
    head = sphere(f"{name}_Head", (x + scl*0.02, y + 0.28, z+0.32), 0.20)
    head.scale = (1.05, 0.95, 1.0)
    bpy.ops.object.transform_apply(scale=True)
    apply_mat(head, M_RAT)
    grp.append(head)

    # Snout
    snout = sphere(f"{name}_Snout", (x + scl*0.02, y + 0.46, z+0.28), 0.09)
    snout.scale = (0.9, 1.3, 0.8)
    bpy.ops.object.transform_apply(scale=True)
    apply_mat(snout, M_RAT)
    grp.append(snout)

    # Nose
    nose = sphere(f"{name}_Nose", (x + scl*0.02, y + 0.54, z+0.29), 0.04)
    apply_mat(nose, M_PINK)
    grp.append(nose)

    # Tongue (hanging out)
    tongue = box(f"{name}_Tongue", (x + scl*0.02, y + 0.53, z+0.22), (0.06, 0.12, 0.05))
    apply_mat(tongue, M_PINK)
    grp.append(tongue)

    # Eyes — looking BACK (toward the forklift) in terror
    for ex, ez in [(-0.09*scl, 0.38), (0.09*scl, 0.38)]:
        # White
        eye_w = sphere(f"{name}_EyeW_{ex}", (x + ex, y + 0.40, z+ez), 0.065)
        apply_mat(eye_w, M_WHITE)
        grp.append(eye_w)
        # Pupil — offset backward to look terrified/looking behind
        pupil = sphere(f"{name}_Pupil_{ex}", (x + ex, y + 0.36, z+ez+0.01), 0.038)
        apply_mat(pupil, M_PUPIL)
        grp.append(pupil)

    # Ears
    for ex in [-0.12*scl, 0.1*scl]:
        ear_out = sphere(f"{name}_EarO_{ex}", (x + ex, y+0.32, z+0.50), 0.075)
        apply_mat(ear_out, M_RAT)
        grp.append(ear_out)
        ear_in = sphere(f"{name}_EarI_{ex}", (x + ex, y+0.32, z+0.50), 0.045)
        apply_mat(ear_in, M_PINK)
        grp.append(ear_in)

    # Tail (curved line of small spheres)
    tail_pts = [(x - scl*0.1*i, y - 0.15 - 0.08*i, z+0.1 + 0.02*i) for i in range(5)]
    for i, tp in enumerate(tail_pts):
        t = sphere(f"{name}_Tail_{i}", tp, 0.025 - i*0.002)
        apply_mat(t, M_RAT_DARK)
        grp.append(t)

    # Legs (running pose)
    for lx, ly, lz in [(-0.12*scl, -0.1, 0.0), (0.1*scl, 0.05, 0.0),
                        (-0.08*scl, -0.05, 0.0), (0.08*scl, 0.1, 0.0)]:
        leg = box(f"{name}_Leg_{lx}", (x+lx, y+ly, z+lz), (0.07, 0.07, 0.18))
        leg.rotation_euler[0] = math.radians(20)
        apply_mat(leg, M_RAT)
        grp.append(leg)

    return grp

make_rat("Rat1", (-0.55, 0.6, 0.0), flip=False)
make_rat("Rat2", ( 0.25, 0.9, 0.0), flip=True)

# ─── GROUND SHADOW ────────────────────────────────────────────────────────────
shadow = box("Ground_Shadow", (0, 0.2, -0.005), (3.5, 3.0, 0.01))
apply_mat(shadow, mat("Shadow", 0.08, 0.06, 0.04))

# ─── CAMERA ───────────────────────────────────────────────────────────────────
# Low angle, slightly right of centre, looking at the action
bpy.ops.object.camera_add(location=(1.8, -3.5, 1.4))
cam = bpy.context.active_object
cam.name = "LogoCam"
cam.rotation_euler = (math.radians(72), 0, math.radians(18))
bpy.context.scene.camera = cam

cam_data = cam.data
cam_data.type = 'PERSP'
cam_data.lens = 35
cam_data.clip_end = 100

# ─── LIGHTING ─────────────────────────────────────────────────────────────────
# Key light (warm)
bpy.ops.object.light_add(type='SUN', location=(3, -2, 5))
sun = bpy.context.active_object
sun.name = "KeyLight"
sun.rotation_euler = (math.radians(45), math.radians(20), math.radians(30))
sun.data.energy = 3.0
sun.data.color = (1.0, 0.92, 0.75)

# Fill light (cool blue rim)
bpy.ops.object.light_add(type='AREA', location=(-4, 1, 3))
fill = bpy.context.active_object
fill.name = "FillLight"
fill.data.energy = 200
fill.data.color = (0.6, 0.75, 1.0)
fill.data.size = 3

# ─── RENDER SETTINGS ──────────────────────────────────────────────────────────
bpy.context.scene.render.resolution_x = 1024
bpy.context.scene.render.resolution_y = 512
bpy.context.scene.render.film_transparent = True
bpy.context.scene.render.image_settings.color_mode = 'RGBA'

print("=" * 55)
print("FORK IT! Logo scene ready.")
print("Camera: LogoCam | Resolution: 1024x512 | RGBA transparent")
print("Render → Image → Save to Assets/6. Art/UI/ForkItLogo.png")
print("=" * 55)
