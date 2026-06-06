using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CatalogEntry))]
public class CatalogEntryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        var entry = (CatalogEntry)target;

        EditorGUILayout.Space();

        if (entry.liveInstance == null)
        {
            EditorGUILayout.HelpBox("Live instance is gone — rebuild the catalog.", MessageType.Warning);
            return;
        }

        if (GUILayout.Button("Select Live Instance", GUILayout.Height(30)))
        {
            Selection.activeGameObject = entry.liveInstance;
            EditorGUIUtility.PingObject(entry.liveInstance);
        }
    }
}
