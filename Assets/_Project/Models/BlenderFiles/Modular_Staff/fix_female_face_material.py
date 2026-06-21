"""
fix_female_face_material.py
────────────────────────────────────────────────────────────────────────────────
Run this from Blender's Scripting workspace (Text Editor → Run Script) to assign
a dark-gray material to every female face mesh without touching the material GUI
(which crashes on this file).

What it does
────────────
1. Looks for an existing material whose name contains "face" to detect the gray
   value already used on male parts.
2. Creates (or reuses) a material called "female_face_DarkGray".
3. Assigns it to every object whose name starts with "female_face_".
4. Saves the file.
────────────────────────────────────────────────────────────────────────────────
"""

import bpy

# ── Target gray (linear-space) ─────────────────────────────────────────────────
# 0.18 linear ≈ the standard "mid gray / base skin" used in many low-poly packs.
# Change GRAY_VALUE if you want it lighter or darker.
GRAY_VALUE   = 0.18
MATERIAL_NAME = "female_face_DarkGray"


def get_or_create_material():
    """Return the target material, creating it fresh if it doesn't exist."""
    mat = bpy.data.materials.get(MATERIAL_NAME)
    if mat is not None:
        print(f"[fix_face] Reusing existing material '{MATERIAL_NAME}'.")
        return mat

    # Check if there is an existing *male* face material we can match
    reference_mat = None
    for m in bpy.data.materials:
        if "face" in m.name.lower() and "male" in m.name.lower():
            reference_mat = m
            break

    mat = bpy.data.materials.new(name=MATERIAL_NAME)
    mat.use_nodes = True

    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf is None:
        # Safety: find the BSDF node however it's named
        for node in mat.node_tree.nodes:
            if node.type == "BSDF_PRINCIPLED":
                bsdf = node
                break

    if bsdf is None:
        print("[fix_face] ERROR: Could not find Principled BSDF node in new material.")
        return mat

    if reference_mat is not None and reference_mat.use_nodes:
        # Try to read the base color from the reference material's BSDF
        ref_bsdf = None
        for node in reference_mat.node_tree.nodes:
            if node.type == "BSDF_PRINCIPLED":
                ref_bsdf = node
                break
        if ref_bsdf is not None:
            r, g, b, a = ref_bsdf.inputs["Base Color"].default_value
            print(f"[fix_face] Matched male face color: ({r:.3f}, {g:.3f}, {b:.3f}) "
                  f"from '{reference_mat.name}'.")
            bsdf.inputs["Base Color"].default_value = (r, g, b, a)
            mat["matched_from"] = reference_mat.name
        else:
            bsdf.inputs["Base Color"].default_value = (GRAY_VALUE, GRAY_VALUE, GRAY_VALUE, 1.0)
            print(f"[fix_face] Male BSDF not found; using fallback gray {GRAY_VALUE}.")
    else:
        bsdf.inputs["Base Color"].default_value = (GRAY_VALUE, GRAY_VALUE, GRAY_VALUE, 1.0)
        print(f"[fix_face] No male face material found; using gray {GRAY_VALUE}.")

    # Minimal PBR values — matte skin-like appearance
    bsdf.inputs["Roughness"].default_value   = 0.9
    bsdf.inputs["Specular IOR Level"].default_value = 0.05 if "Specular IOR Level" in bsdf.inputs else 0.0

    print(f"[fix_face] Created material '{MATERIAL_NAME}'.")
    return mat


def assign_to_female_faces(mat):
    """Assign mat to every object whose name starts with 'female_face_'."""
    count = 0
    for obj in bpy.data.objects:
        if not obj.name.startswith("female_face_"):
            continue
        if obj.type != "MESH":
            continue

        mesh = obj.data

        # Ensure at least one material slot
        if len(obj.material_slots) == 0:
            obj.data.materials.append(mat)
        else:
            # Replace the first slot; add extra slots if somehow empty
            for slot in obj.material_slots:
                slot.material = mat

        print(f"[fix_face] Assigned to '{obj.name}'.")
        count += 1

    return count


def run():
    mat   = get_or_create_material()
    count = assign_to_female_faces(mat)

    if count == 0:
        print("[fix_face] WARNING: No objects found with name starting 'female_face_'. "
              "Check that your mesh objects follow the naming convention gender_slot_variant "
              "(e.g. female_face_Neutral, female_face_Smile).")
    else:
        print(f"[fix_face] Done — material assigned to {count} object(s).")

    # Save the blend file
    bpy.ops.wm.save_mainfile()
    print("[fix_face] File saved.")


run()
