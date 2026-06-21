using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PalletBuilder))]
public class PalletBuilderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PalletBuilder builder = (PalletBuilder)target;

        EditorGUILayout.Space();
        if (GUILayout.Button("Build Pallet", GUILayout.Height(30)))
        {
            builder.Build();
        }
    }
}
