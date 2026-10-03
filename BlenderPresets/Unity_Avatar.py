import bpy
op = bpy.context.active_operator

op.use_selection = True
op.use_visible = False
op.use_active_collection = False
op.global_scale = 1.0
op.apply_unit_scale = True
op.apply_scale_options = 'FBX_SCALE_UNITS'
op.use_space_transform = True
op.bake_space_transform = False
op.object_types = {'ARMATURE', 'MESH'}
op.use_mesh_modifiers = True
op.use_mesh_edges = False
op.mesh_smooth_type = 'FACE'
op.use_tspace = False
op.use_custom_props = False
op.add_leaf_bones = False
op.primary_bone_axis = 'Y'
op.secondary_bone_axis = 'X'
op.use_armature_deform_only = True
op.armature_nodetype = 'NULL'
op.bake_anim = False
op.path_mode = 'AUTO'
op.embed_textures = False
op.axis_forward = '-Z'
op.axis_up = 'Y'
