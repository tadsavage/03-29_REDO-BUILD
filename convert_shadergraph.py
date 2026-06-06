#!/usr/bin/env python3
"""
convert_shadergraph.py

Reads LowPolyStylized.shadergraph (NDJSON format) and injects RIM lighting
+ edge darkening nodes. Preserves exact NDJSON structure — one JSON object
per line, no extra whitespace, no trailing whitespace.
"""

import json
import uuid
import sys
import os

# ── helpers ──────────────────────────────────────────────────────────────────

def gen_id():
    """Generate a 32-char hex UUID (no dashes)."""
    return uuid.uuid4().hex


def edge(out_node_id, out_slot_id, in_node_id, in_slot_id):
    """Build an edge dict matching the existing m_Edges format."""
    return {
        "m_OutputSlot": {
            "m_Node": {"m_Id": out_node_id},
            "m_SlotId": out_slot_id,
        },
        "m_InputSlot": {
            "m_Node": {"m_Id": in_node_id},
            "m_SlotId": in_slot_id,
        },
    }


def node_base(obj_id, node_type, name, x, y, width, height, slot_refs):
    """Standard fields shared by all node objects."""
    return {
        "m_SGVersion": 0,
        "m_Type": node_type,
        "m_ObjectId": obj_id,
        "m_Group": {"m_Id": ""},
        "m_Name": name,
        "m_DrawState": {
            "m_Expanded": True,
            "m_Position": {
                "serializedVersion": "2",
                "x": float(x),
                "y": float(y),
                "width": float(width),
                "height": float(height),
            },
        },
        "m_Slots": [{"m_Id": s} for s in slot_refs],
        "synonyms": [],
        "m_Precision": 0,
        "m_PreviewExpanded": True,
        "m_DismissedVersion": 0,
        "m_PreviewMode": 0,
        "m_CustomColors": {"m_SerializableColors": []},
    }


# ── main ─────────────────────────────────────────────────────────────────────

def main():
    project_root = os.path.dirname(os.path.abspath(__file__))
    shader_path = os.path.join(
        project_root, "Assets", "6. Art", "Shaders", "LowPolyStylized.shadergraph"
    )

    if not os.path.exists(shader_path):
        print(f"ERROR: File not found: {shader_path}")
        sys.exit(1)

    # ── 1. Read all lines ────────────────────────────────────────────────
    with open(shader_path, "r", encoding="utf-8") as f:
        lines = [ln.strip() for ln in f if ln.strip()]

    # Parse every line as JSON
    objects = [json.loads(ln) for ln in lines]

    # First object MUST be the GraphData
    graph_data = objects[0]
    assert graph_data.get("m_Type") == "UnityEditor.ShaderGraph.GraphData", \
        "First line is not GraphData!"

    # ── 2. Known existing IDs ────────────────────────────────────────────
    CUSTOM_LIGHTING_ID = "00a2b5dbc5f64208ade31d59cccc039d"
    BASECOLOR_BLOCK_ID = "bbd782c007424d44bb94e854a4eb4313"
    MAT_PROPS_CATEGORY_ID = "0e1197184b6043a0888b7090abfc640e"

    # CustomLighting output slot 1 (Base_Color)
    CUSTOM_LIGHTING_OUT_SLOT = 1
    # BaseColor block input slot 0
    BASECOLOR_IN_SLOT = 0

    # ── 3. Generate all new IDs ──────────────────────────────────────────
    ID_RIM_POWER_PROP      = gen_id()
    ID_RIM_COLOR_PROP      = gen_id()

    ID_NORMAL_VEC_NODE     = gen_id()
    ID_VIEW_DIR_NODE       = gen_id()
    ID_FRESNEL_NODE        = gen_id()
    ID_RIM_POWER_PROP_NODE = gen_id()
    ID_ONE_MINUS_NODE      = gen_id()
    ID_RIM_COLOR_PROP_NODE = gen_id()
    ID_MUL_RIM_COLOR       = gen_id()
    ID_MUL_EDGE_DARKEN     = gen_id()
    ID_ADD_FINAL           = gen_id()

    # Slot IDs for FresnelNode
    ID_FRESNEL_SLOT_NORMAL  = gen_id()
    ID_FRESNEL_SLOT_VIEWDIR = gen_id()
    ID_FRESNEL_SLOT_POWER   = gen_id()
    ID_FRESNEL_SLOT_OUT     = gen_id()

    # Slot IDs for OneMinusNode
    ID_ONEMINUS_SLOT_IN  = gen_id()
    ID_ONEMINUS_SLOT_OUT = gen_id()

    # Slot IDs for Multiply_RimColor
    ID_MUL_RC_SLOT_A   = gen_id()
    ID_MUL_RC_SLOT_B   = gen_id()
    ID_MUL_RC_SLOT_OUT = gen_id()

    # Slot IDs for Multiply_EdgeDarken
    ID_MUL_ED_SLOT_A   = gen_id()
    ID_MUL_ED_SLOT_B   = gen_id()
    ID_MUL_ED_SLOT_OUT = gen_id()

    # Slot IDs for Add_Final
    ID_ADD_FINAL_SLOT_A   = gen_id()
    ID_ADD_FINAL_SLOT_B   = gen_id()
    ID_ADD_FINAL_SLOT_OUT = gen_id()

    # Slot IDs for NormalVectorNode and ViewDirectionNode
    ID_NORMAL_VEC_SLOT_OUT = gen_id()
    ID_VIEW_DIR_SLOT_OUT   = gen_id()

    # Slot IDs for PropertyNodes
    ID_RIM_POWER_PROP_SLOT = gen_id()
    ID_RIM_COLOR_PROP_SLOT = gen_id()

    GUID_RIM_POWER = str(uuid.uuid4())
    GUID_RIM_COLOR = str(uuid.uuid4())

    # ── 4. Build new Property objects ────────────────────────────────────

    prop_rim_power = {
        "m_SGVersion": 0,
        "m_Type": "UnityEditor.ShaderGraph.Internal.Vector1ShaderProperty",
        "m_ObjectId": ID_RIM_POWER_PROP,
        "m_Guid": {"m_GuidSerialized": GUID_RIM_POWER},
        "m_Name": "Rim Power",
        "m_DefaultRefNameVersion": 1,
        "m_RefNameGeneratedByDisplayName": "Rim Power",
        "m_DefaultReferenceName": "_RimPower",
        "m_OverrideReferenceName": "",
        "m_GeneratePropertyBlock": True,
        "m_UseCustomSlotLabel": False,
        "m_CustomSlotLabel": "",
        "m_DismissedVersion": 0,
        "m_Precision": 0,
        "overrideHLSLDeclaration": False,
        "hlslDeclarationOverride": 0,
        "m_Hidden": False,
        "m_Value": 3.0,
        "m_FloatType": 1,
        "m_RangeValues": {"x": 0.01, "y": 10.0},
    }

    prop_rim_color = {
        "m_SGVersion": 0,
        "m_Type": "UnityEditor.ShaderGraph.Internal.ColorShaderProperty",
        "m_ObjectId": ID_RIM_COLOR_PROP,
        "m_Guid": {"m_GuidSerialized": GUID_RIM_COLOR},
        "m_Name": "Rim Color",
        "m_DefaultRefNameVersion": 1,
        "m_RefNameGeneratedByDisplayName": "Rim Color",
        "m_DefaultReferenceName": "_RimColor",
        "m_OverrideReferenceName": "",
        "m_GeneratePropertyBlock": True,
        "m_UseCustomSlotLabel": False,
        "m_CustomSlotLabel": "",
        "m_DismissedVersion": 0,
        "m_Precision": 0,
        "overrideHLSLDeclaration": False,
        "hlslDeclarationOverride": 0,
        "m_Hidden": False,
        "m_ColorMode": 0,
        "m_Value": {"r": 1.0, "g": 1.0, "b": 1.0, "a": 0.0},
        "isMainColor": False,
    }

    # ── 5. Build slot objects ────────────────────────────────────────────

    # NormalVector slot (output)
    slot_normal_vec_out = {
        "m_SGVersion": 0,
        "m_Type": "UnityEditor.ShaderGraph.Vector3MaterialSlot",
        "m_ObjectId": ID_NORMAL_VEC_SLOT_OUT,
        "m_Id": 0,
        "m_DisplayName": "Out",
        "m_SlotType": 1,
        "m_Hidden": False,
        "m_ShaderOutputName": "Out",
        "m_StageCapability": 3,
        "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0},
        "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0},
        "m_Labels": [],
    }

    # ViewDirection slot (output)
    slot_view_dir_out = {
        "m_SGVersion": 0,
        "m_Type": "UnityEditor.ShaderGraph.Vector3MaterialSlot",
        "m_ObjectId": ID_VIEW_DIR_SLOT_OUT,
        "m_Id": 0,
        "m_DisplayName": "Out",
        "m_SlotType": 1,
        "m_Hidden": False,
        "m_ShaderOutputName": "Out",
        "m_StageCapability": 3,
        "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0},
        "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0},
        "m_Labels": [],
    }

    # Fresnel slots
    slot_fresnel_normal = {
        "m_SGVersion": 0,
        "m_Type": "UnityEditor.ShaderGraph.Vector3MaterialSlot",
        "m_ObjectId": ID_FRESNEL_SLOT_NORMAL,
        "m_Id": 0,
        "m_DisplayName": "Normal",
        "m_SlotType": 0,
        "m_Hidden": False,
        "m_ShaderOutputName": "Normal",
        "m_StageCapability": 3,
        "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0},
        "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0},
        "m_Labels": [],
    }

    slot_fresnel_viewdir = {
        "m_SGVersion": 0,
        "m_Type": "UnityEditor.ShaderGraph.Vector3MaterialSlot",
        "m_ObjectId": ID_FRESNEL_SLOT_VIEWDIR,
        "m_Id": 1,
        "m_DisplayName": "ViewDir",
        "m_SlotType": 0,
        "m_Hidden": False,
        "m_ShaderOutputName": "ViewDir",
        "m_StageCapability": 3,
        "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0},
        "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0},
        "m_Labels": [],
    }

    slot_fresnel_power = {
        "m_SGVersion": 0,
        "m_Type": "UnityEditor.ShaderGraph.Vector1MaterialSlot",
        "m_ObjectId": ID_FRESNEL_SLOT_POWER,
        "m_Id": 2,
        "m_DisplayName": "Power",
        "m_SlotType": 0,
        "m_Hidden": False,
        "m_ShaderOutputName": "Power",
        "m_StageCapability": 3,
        "m_Value": 1.0,
        "m_DefaultValue": 1.0,
        "m_Labels": [],
    }

    slot_fresnel_out = {
        "m_SGVersion": 0,
        "m_Type": "UnityEditor.ShaderGraph.Vector1MaterialSlot",
        "m_ObjectId": ID_FRESNEL_SLOT_OUT,
        "m_Id": 3,
        "m_DisplayName": "Out",
        "m_SlotType": 1,
        "m_Hidden": False,
        "m_ShaderOutputName": "Out",
        "m_StageCapability": 3,
        "m_Value": 0.0,
        "m_DefaultValue": 0.0,
        "m_Labels": [],
    }

    # OneMinus slots
    slot_oneminus_in = {
        "m_SGVersion": 0,
        "m_Type": "UnityEditor.ShaderGraph.DynamicVectorMaterialSlot",
        "m_ObjectId": ID_ONEMINUS_SLOT_IN,
        "m_Id": 0,
        "m_DisplayName": "In",
        "m_SlotType": 0,
        "m_Hidden": False,
        "m_ShaderOutputName": "In",
        "m_StageCapability": 3,
        "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0},
        "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0},
    }

    slot_oneminus_out = {
        "m_SGVersion": 0,
        "m_Type": "UnityEditor.ShaderGraph.DynamicVectorMaterialSlot",
        "m_ObjectId": ID_ONEMINUS_SLOT_OUT,
        "m_Id": 1,
        "m_DisplayName": "Out",
        "m_SlotType": 1,
        "m_Hidden": False,
        "m_ShaderOutputName": "Out",
        "m_StageCapability": 3,
        "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0},
        "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0},
    }

    # Multiply_RimColor slots (DynamicValueMaterialSlot)
    slot_mul_rc_a = {
        "m_SGVersion": 0,
        "m_Type": "UnityEditor.ShaderGraph.DynamicValueMaterialSlot",
        "m_ObjectId": ID_MUL_RC_SLOT_A,
        "m_Id": 0,
        "m_DisplayName": "A",
        "m_SlotType": 0,
        "m_Hidden": False,
        "m_ShaderOutputName": "A",
        "m_StageCapability": 3,
        "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0},
        "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0},
    }

    slot_mul_rc_b = {
        "m_SGVersion": 0,
        "m_Type": "UnityEditor.ShaderGraph.DynamicValueMaterialSlot",
        "m_ObjectId": ID_MUL_RC_SLOT_B,
        "m_Id": 1,
        "m_DisplayName": "B",
        "m_SlotType": 0,
        "m_Hidden": False,
        "m_ShaderOutputName": "B",
        "m_StageCapability": 3,
        "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0},
        "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0},
    }

    slot_mul_rc_out = {
        "m_SGVersion": 0,
        "m_Type": "UnityEditor.ShaderGraph.DynamicValueMaterialSlot",
        "m_ObjectId": ID_MUL_RC_SLOT_OUT,
        "m_Id": 2,
        "m_DisplayName": "Out",
        "m_SlotType": 1,
        "m_Hidden": False,
        "m_ShaderOutputName": "Out",
        "m_StageCapability": 3,
        "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0},
        "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0},
    }

    # Multiply_EdgeDarken slots
    slot_mul_ed_a = {
        "m_SGVersion": 0,
        "m_Type": "UnityEditor.ShaderGraph.DynamicValueMaterialSlot",
        "m_ObjectId": ID_MUL_ED_SLOT_A,
        "m_Id": 0,
        "m_DisplayName": "A",
        "m_SlotType": 0,
        "m_Hidden": False,
        "m_ShaderOutputName": "A",
        "m_StageCapability": 3,
        "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0},
        "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0},
    }

    slot_mul_ed_b = {
        "m_SGVersion": 0,
        "m_Type": "UnityEditor.ShaderGraph.DynamicValueMaterialSlot",
        "m_ObjectId": ID_MUL_ED_SLOT_B,
        "m_Id": 1,
        "m_DisplayName": "B",
        "m_SlotType": 0,
        "m_Hidden": False,
        "m_ShaderOutputName": "B",
        "m_StageCapability": 3,
        "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0},
        "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0},
    }

    slot_mul_ed_out = {
        "m_SGVersion": 0,
        "m_Type": "UnityEditor.ShaderGraph.DynamicValueMaterialSlot",
        "m_ObjectId": ID_MUL_ED_SLOT_OUT,
        "m_Id": 2,
        "m_DisplayName": "Out",
        "m_SlotType": 1,
        "m_Hidden": False,
        "m_ShaderOutputName": "Out",
        "m_StageCapability": 3,
        "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0},
        "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0},
    }

    # Add_Final slots
    slot_add_a = {
        "m_SGVersion": 0,
        "m_Type": "UnityEditor.ShaderGraph.DynamicValueMaterialSlot",
        "m_ObjectId": ID_ADD_FINAL_SLOT_A,
        "m_Id": 0,
        "m_DisplayName": "A",
        "m_SlotType": 0,
        "m_Hidden": False,
        "m_ShaderOutputName": "A",
        "m_StageCapability": 3,
        "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0},
        "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0},
    }

    slot_add_b = {
        "m_SGVersion": 0,
        "m_Type": "UnityEditor.ShaderGraph.DynamicValueMaterialSlot",
        "m_ObjectId": ID_ADD_FINAL_SLOT_B,
        "m_Id": 1,
        "m_DisplayName": "B",
        "m_SlotType": 0,
        "m_Hidden": False,
        "m_ShaderOutputName": "B",
        "m_StageCapability": 3,
        "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0},
        "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0},
    }

    slot_add_out = {
        "m_SGVersion": 0,
        "m_Type": "UnityEditor.ShaderGraph.DynamicValueMaterialSlot",
        "m_ObjectId": ID_ADD_FINAL_SLOT_OUT,
        "m_Id": 2,
        "m_DisplayName": "Out",
        "m_SlotType": 1,
        "m_Hidden": False,
        "m_ShaderOutputName": "Out",
        "m_StageCapability": 3,
        "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0},
        "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0},
    }

    # PropertyNode slots
    slot_rim_power_prop = {
        "m_SGVersion": 0,
        "m_Type": "UnityEditor.ShaderGraph.Vector1MaterialSlot",
        "m_ObjectId": ID_RIM_POWER_PROP_SLOT,
        "m_Id": 0,
        "m_DisplayName": "Rim Power",
        "m_SlotType": 1,
        "m_Hidden": False,
        "m_ShaderOutputName": "Out",
        "m_StageCapability": 3,
        "m_Value": 0.0,
        "m_DefaultValue": 0.0,
        "m_Labels": [],
    }

    slot_rim_color_prop = {
        "m_SGVersion": 0,
        "m_Type": "UnityEditor.ShaderGraph.Vector4MaterialSlot",
        "m_ObjectId": ID_RIM_COLOR_PROP_SLOT,
        "m_Id": 0,
        "m_DisplayName": "Rim Color",
        "m_SlotType": 1,
        "m_Hidden": False,
        "m_ShaderOutputName": "Out",
        "m_StageCapability": 3,
        "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0},
        "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0},
        "m_Labels": [],
    }

    # ── 6. Build node objects ────────────────────────────────────────────

    node_normal_vec = node_base(
        ID_NORMAL_VEC_NODE,
        "UnityEditor.ShaderGraph.NormalVectorNode",
        "Normal Vector",
        -700, 450, 150, 34,
        [ID_NORMAL_VEC_SLOT_OUT],
    )
    node_normal_vec["m_SGVersion"] = 1

    node_view_dir = node_base(
        ID_VIEW_DIR_NODE,
        "UnityEditor.ShaderGraph.ViewDirectionNode",
        "View Direction",
        -700, 530, 150, 34,
        [ID_VIEW_DIR_SLOT_OUT],
    )
    node_view_dir["m_SGVersion"] = 1

    node_fresnel = node_base(
        ID_FRESNEL_NODE,
        "UnityEditor.ShaderGraph.FresnelNode",
        "Fresnel Effect",
        -500, 490, 160, 158,
        [ID_FRESNEL_SLOT_NORMAL, ID_FRESNEL_SLOT_VIEWDIR,
         ID_FRESNEL_SLOT_POWER, ID_FRESNEL_SLOT_OUT],
    )
    node_fresnel["m_SGVersion"] = 1

    node_rim_power_prop = node_base(
        ID_RIM_POWER_PROP_NODE,
        "UnityEditor.ShaderGraph.PropertyNode",
        "Rim Power",
        -700, 610, 120, 34,
        [ID_RIM_POWER_PROP_SLOT],
    )
    node_rim_power_prop["m_Property"] = {"m_Id": ID_RIM_POWER_PROP}

    node_oneminus = node_base(
        ID_ONE_MINUS_NODE,
        "UnityEditor.ShaderGraph.OneMinusNode",
        "One Minus",
        -300, 490, 150, 60,
        [ID_ONEMINUS_SLOT_IN, ID_ONEMINUS_SLOT_OUT],
    )
    node_oneminus["m_SGVersion"] = 1

    node_rim_color_prop = node_base(
        ID_RIM_COLOR_PROP_NODE,
        "UnityEditor.ShaderGraph.PropertyNode",
        "Rim Color",
        -500, 690, 120, 34,
        [ID_RIM_COLOR_PROP_SLOT],
    )
    node_rim_color_prop["m_Property"] = {"m_Id": ID_RIM_COLOR_PROP}

    node_mul_rim_color = node_base(
        ID_MUL_RIM_COLOR,
        "UnityEditor.ShaderGraph.MultiplyNode",
        "Multiply",
        -100, 520, 150, 60,
        [ID_MUL_RC_SLOT_A, ID_MUL_RC_SLOT_B, ID_MUL_RC_SLOT_OUT],
    )

    node_mul_edge_darken = node_base(
        ID_MUL_EDGE_DARKEN,
        "UnityEditor.ShaderGraph.MultiplyNode",
        "Multiply",
        -100, 330, 150, 60,
        [ID_MUL_ED_SLOT_A, ID_MUL_ED_SLOT_B, ID_MUL_ED_SLOT_OUT],
    )

    node_add_final = node_base(
        ID_ADD_FINAL,
        "UnityEditor.ShaderGraph.AddNode",
        "Add",
        100, 425, 150, 60,
        [ID_ADD_FINAL_SLOT_A, ID_ADD_FINAL_SLOT_B, ID_ADD_FINAL_SLOT_OUT],
    )

    # ── 7. Modify GraphData ─────────────────────────────────────────────

    # Add new property IDs
    graph_data["m_Properties"].append({"m_Id": ID_RIM_POWER_PROP})
    graph_data["m_Properties"].append({"m_Id": ID_RIM_COLOR_PROP})

    # Add new node IDs
    new_node_ids = [
        ID_NORMAL_VEC_NODE,
        ID_VIEW_DIR_NODE,
        ID_FRESNEL_NODE,
        ID_RIM_POWER_PROP_NODE,
        ID_ONE_MINUS_NODE,
        ID_RIM_COLOR_PROP_NODE,
        ID_MUL_RIM_COLOR,
        ID_MUL_EDGE_DARKEN,
        ID_ADD_FINAL,
    ]
    for nid in new_node_ids:
        graph_data["m_Nodes"].append({"m_Id": nid})

    # Remove old edge: CustomLighting slot 1 → BaseColor slot 0
    old_edges = graph_data["m_Edges"]
    new_edges = []
    for e in old_edges:
        out_node = e["m_OutputSlot"]["m_Node"]["m_Id"]
        out_slot = e["m_OutputSlot"]["m_SlotId"]
        in_node  = e["m_InputSlot"]["m_Node"]["m_Id"]
        in_slot  = e["m_InputSlot"]["m_SlotId"]
        if out_node == CUSTOM_LIGHTING_ID and out_slot == CUSTOM_LIGHTING_OUT_SLOT \
           and in_node == BASECOLOR_BLOCK_ID and in_slot == BASECOLOR_IN_SLOT:
            continue  # drop this edge
        new_edges.append(e)

    # Add 11 new edges
    new_edges += [
        # 1. NormalVector slot 0 → Fresnel slot 0
        edge(ID_NORMAL_VEC_NODE, 0, ID_FRESNEL_NODE, 0),
        # 2. ViewDirection slot 0 → Fresnel slot 1
        edge(ID_VIEW_DIR_NODE, 0, ID_FRESNEL_NODE, 1),
        # 3. RimPower PropertyNode slot 0 → Fresnel slot 2
        edge(ID_RIM_POWER_PROP_NODE, 0, ID_FRESNEL_NODE, 2),
        # 4. Fresnel slot 3 → OneMinus slot 0
        edge(ID_FRESNEL_NODE, 3, ID_ONE_MINUS_NODE, 0),
        # 5. OneMinus slot 1 → Multiply_RimColor slot 0
        edge(ID_ONE_MINUS_NODE, 1, ID_MUL_RIM_COLOR, 0),
        # 6. RimColor PropertyNode slot 0 → Multiply_RimColor slot 1
        edge(ID_RIM_COLOR_PROP_NODE, 0, ID_MUL_RIM_COLOR, 1),
        # 7. Fresnel slot 3 → Multiply_EdgeDarken slot 0
        edge(ID_FRESNEL_NODE, 3, ID_MUL_EDGE_DARKEN, 0),
        # 8. CustomLighting output slot 1 → Multiply_EdgeDarken slot 1
        edge(CUSTOM_LIGHTING_ID, CUSTOM_LIGHTING_OUT_SLOT, ID_MUL_EDGE_DARKEN, 1),
        # 9. Multiply_EdgeDarken slot 2 → Add_Final slot 0
        edge(ID_MUL_EDGE_DARKEN, 2, ID_ADD_FINAL, 0),
        # 10. Multiply_RimColor slot 2 → Add_Final slot 1
        edge(ID_MUL_RIM_COLOR, 2, ID_ADD_FINAL, 1),
        # 11. Add_Final slot 2 → SurfaceDescription.BaseColor input slot 0
        edge(ID_ADD_FINAL, 2, BASECOLOR_BLOCK_ID, BASECOLOR_IN_SLOT),
    ]

    graph_data["m_Edges"] = new_edges

    # Update CategoryData "MaterialProperties" children
    for cat in graph_data["m_CategoryData"]:
        if cat["m_Id"] == MAT_PROPS_CATEGORY_ID:
            # cat just has {"m_Id": "..."} — need to find the separate
            # CategoryData object in the non-GraphData objects and modify it
            pass

    # ── 8. Rebuild output ───────────────────────────────────────────────

    # Keep track of which objects we've already emitted (by ObjectId)
    # GraphData is always first
    output_objects = [graph_data]

    # All other existing objects (non-GraphData), preserving order
    for obj in objects[1:]:
        output_objects.append(obj)

    # Append all new objects
    new_objects = [
        # Properties
        prop_rim_power,
        prop_rim_color,
        # PropertyNode slots
        slot_rim_power_prop,
        slot_rim_color_prop,
        # Fresnel slots
        slot_fresnel_normal,
        slot_fresnel_viewdir,
        slot_fresnel_power,
        slot_fresnel_out,
        # OneMinus slots
        slot_oneminus_in,
        slot_oneminus_out,
        # Multiply_RimColor slots
        slot_mul_rc_a,
        slot_mul_rc_b,
        slot_mul_rc_out,
        # Multiply_EdgeDarken slots
        slot_mul_ed_a,
        slot_mul_ed_b,
        slot_mul_ed_out,
        # Add_Final slots
        slot_add_a,
        slot_add_b,
        slot_add_out,
        # NormalVector + ViewDirection slots
        slot_normal_vec_out,
        slot_view_dir_out,
        # Nodes
        node_normal_vec,
        node_view_dir,
        node_fresnel,
        node_rim_power_prop,
        node_oneminus,
        node_rim_color_prop,
        node_mul_rim_color,
        node_mul_edge_darken,
        node_add_final,
    ]
    output_objects.extend(new_objects)

    # Also update the separate CategoryData object for MaterialProperties
    # (it lives in objects[1:], not embedded in GraphData)
    for i, obj in enumerate(output_objects):
        oid = obj.get("m_ObjectId", "")
        if oid == MAT_PROPS_CATEGORY_ID:
            obj["m_ChildObjectList"].append({"m_Id": ID_RIM_POWER_PROP})
            obj["m_ChildObjectList"].append({"m_Id": ID_RIM_COLOR_PROP})
            break

    # ── 9. Write back NDJSON ────────────────────────────────────────────
    with open(shader_path, "w", encoding="utf-8", newline="\n") as f:
        for obj in output_objects:
            # Compact JSON: no extra spaces, no trailing whitespace
            line = json.dumps(obj, separators=(",", ":"), ensure_ascii=False)
            f.write(line + "\n")

    print("✅ Done. Injected RIM lighting + edge darkening into:")
    print(f"   {shader_path}")
    print(f"   Nodes: {len(graph_data['m_Nodes'])} total ({len(new_node_ids)} new)")
    print(f"   Properties: {len(graph_data['m_Properties'])} total (2 new)")
    print(f"   Edges: {len(graph_data['m_Edges'])} total")
    print(f"   New objects written: {len(new_objects)}")


if __name__ == "__main__":
    main()
