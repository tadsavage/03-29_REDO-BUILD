#!/usr/bin/env python3
"""Convert newline-delimited JSON shadergraph to single nested JSON (Unity format)."""

import json
import sys
from copy import deepcopy

def convert_ndjson_to_nested(input_path, output_path):
    # Read all lines and parse each as JSON
    docs = []
    with open(input_path, 'r', encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if line:
                docs.append(json.loads(line))

    # Build lookup by m_ObjectId
    obj_lookup = {}
    root = None
    for doc in docs:
        oid = doc.get("m_ObjectId")
        if oid:
            obj_lookup[oid] = doc
            if doc.get("m_Type") == "UnityEditor.ShaderGraph.GraphData":
                root = doc

    if root is None:
        print("ERROR: No root GraphData found")
        sys.exit(1)

    def resolve_refs(arr):
        """Replace list of {m_Id: x} refs with resolved objects."""
        return [obj_lookup[item["m_Id"]] for item in arr]

    def resolve_ref(item):
        """Replace single {m_Id: x} ref with resolved object."""
        return obj_lookup[item["m_Id"]]

    # Resolve property references
    root["m_Properties"] = resolve_refs(root["m_Properties"])
    
    # Resolve category data
    if "m_CategoryData" in root:
        root["m_CategoryData"] = resolve_refs(root["m_CategoryData"])
        # Also resolve child object list inside category data
        for cat in root["m_CategoryData"]:
            if "m_ChildObjectList" in cat:
                cat["m_ChildObjectList"] = [obj_lookup[ref["m_Id"]] for ref in cat["m_ChildObjectList"]]

    # Resolve node references
    root["m_Nodes"] = resolve_refs(root["m_Nodes"])
    
    # Resolve slot references inside each node, and property refs in PropertyNodes
    for node in root["m_Nodes"]:
        if "m_Slots" in node and isinstance(node["m_Slots"], list):
            node["m_Slots"] = [resolve_ref(s) if isinstance(s, dict) and list(s.keys()) == ["m_Id"] else s for s in node["m_Slots"]]
        if "m_Property" in node and isinstance(node["m_Property"], dict) and list(node["m_Property"].keys()) == ["m_Id"]:
            node["m_Property"] = resolve_ref(node["m_Property"])
        # Resolve m_Group reference if present
        if "m_Group" in node and isinstance(node["m_Group"], dict) and list(node["m_Group"].keys()) == ["m_Id"]:
            gid = node["m_Group"]["m_Id"]
            if gid and gid in obj_lookup:
                node["m_Group"] = obj_lookup[gid]

    # Resolve block references in vertex/fragment contexts
    if "m_VertexContext" in root and "m_Blocks" in root["m_VertexContext"]:
        root["m_VertexContext"]["m_Blocks"] = resolve_refs(root["m_VertexContext"]["m_Blocks"])
    if "m_FragmentContext" in root and "m_Blocks" in root["m_FragmentContext"]:
        root["m_FragmentContext"]["m_Blocks"] = resolve_refs(root["m_FragmentContext"]["m_Blocks"])

    # Resolve active target references
    root["m_ActiveTargets"] = resolve_refs(root["m_ActiveTargets"])
    for target in root["m_ActiveTargets"]:
        if "m_ActiveSubTarget" in target:
            if isinstance(target["m_ActiveSubTarget"], dict) and list(target["m_ActiveSubTarget"].keys()) == ["m_Id"]:
                target["m_ActiveSubTarget"] = resolve_ref(target["m_ActiveSubTarget"])
        if "m_SubTargets" in target and isinstance(target["m_SubTargets"], list):
            target["m_SubTargets"] = [resolve_ref(s) if isinstance(s, dict) and list(s.keys()) == ["m_Id"] else s for s in target["m_SubTargets"]]

    # Resolve sub-datas if any
    if "m_SubDatas" in root and root["m_SubDatas"]:
        root["m_SubDatas"] = [resolve_ref(s) if isinstance(s, dict) and list(s.keys()) == ["m_Id"] else s for s in root["m_SubDatas"]]

    # Write as single indented JSON
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(root, f, indent=4, ensure_ascii=False)

    print(f"Converted {len(docs)} docs -> single nested JSON ({len(json.dumps(root))} bytes)")
    print(f"Properties: {len(root.get('m_Properties', []))}")
    print(f"Nodes: {len(root.get('m_Nodes', []))}")
    print(f"Edges: {len(root.get('m_Edges', []))}")

if __name__ == "__main__":
    convert_ndjson_to_nested(
        "Assets/6. Art/Shaders/LowPolyStylized.shadergraph",
        "Assets/6. Art/Shaders/LowPolyStylized.shadergraph"
    )
