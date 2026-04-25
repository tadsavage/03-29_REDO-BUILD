using UnityEditor;
using UnityEngine;

public static class ObjDataIDAssigner
{
    [MenuItem("Tools/ObjData/Auto‑Assign Unique IDs")]
    public static void AssignIDs()
    {
        string[] guids = AssetDatabase.FindAssets("t:ObjDataSO");

        int nextID = 1;
        int changed = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ObjDataSO so = AssetDatabase.LoadAssetAtPath<ObjDataSO>(path);

            if (so == null)
                continue;

            // Assign new ID only if missing or duplicate
            if (so.id <= 0)
            {
                so.id = nextID;
                EditorUtility.SetDirty(so);
                changed++;
            }

            nextID++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Auto‑Assign IDs: Updated {changed} ObjDataSO assets.");
    }
}
