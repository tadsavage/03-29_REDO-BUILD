using System.Linq;
using UnityEditor;
using UnityEngine;
using static BuildBarEvents;

[CustomEditor(typeof(ObjDataRegistry))]
public class ObjDataRegistryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ObjDataRegistry registry = (ObjDataRegistry)target;

        if (GUILayout.Button("Auto‑Populate ObjDataSO List"))
        {
            Populate(registry);
        }
    }

    private void Populate(ObjDataRegistry registry)
    {
        string[] guids = AssetDatabase.FindAssets("t:ObjDataSO");

        ObjDataSO[] all = guids
            .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
            .Select(path => AssetDatabase.LoadAssetAtPath<ObjDataSO>(path))
            .ToArray();

        registry.buttonSOs = all;

        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();

        Debug.Log($"ObjDataRegistry auto‑populated with {all.Length} ObjDataSO assets.");
    }
}