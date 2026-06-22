using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ObjDataRegistry))]
public class ObjDataRegistryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ObjDataRegistry registry = (ObjDataRegistry)target;

        if (GUILayout.Button("Auto-Populate ObjDataSO List"))
        {
            registry.buttonSOs.Clear();

            string[] guids = AssetDatabase.FindAssets("t:ObjDataSO");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ObjDataSO obj = AssetDatabase.LoadAssetAtPath<ObjDataSO>(path);
                registry.buttonSOs.Add(obj);
            }

            EditorUtility.SetDirty(registry);
        }
    }
}
