using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CatalogEntry))]
public class CatalogEntryEditor : Editor
{
    private bool _childrenFoldout = true;

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

        int childCount = entry.liveInstance.transform.childCount;
        if (childCount == 0) return;

        EditorGUILayout.Space();
        _childrenFoldout = EditorGUILayout.Foldout(_childrenFoldout, $"Children ({childCount})", true);
        if (!_childrenFoldout) return;

        EditorGUI.indentLevel++;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = entry.liveInstance.transform.GetChild(i);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(child.name);
            if (GUILayout.Button("Select", GUILayout.Width(55)))
            {
                Selection.activeGameObject = child.gameObject;
                EditorGUIUtility.PingObject(child.gameObject);
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUI.indentLevel--;
    }
}
