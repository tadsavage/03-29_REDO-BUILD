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
    // This method finds all ObjDataSO assets in the project and assigns them to the registry's buttonSOs array.
    private void Populate(ObjDataRegistry registry)
    {
        string[] guids = AssetDatabase.FindAssets("t:ObjDataSO");

        ObjDataSO[] all = guids
            .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
            .Select(path => AssetDatabase.LoadAssetAtPath<ObjDataSO>(path))
            .ToArray();

        registry.buttonSOs = all;
        //
        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
    }
}