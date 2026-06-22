// Editor/PlacementGridEditor.cs
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlacementGrid))]
public class PlacementGridEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var grid = (PlacementGrid)target;
        if (GUILayout.Button("Populate DebugCells"))
            grid.PopulateDebugCells();
    }
}
