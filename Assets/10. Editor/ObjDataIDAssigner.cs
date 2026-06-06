using UnityEditor;
using UnityEngine;

public static class ObjDataIDAssigner
{
    [MenuItem("Tools/ObjData/Auto‑Assign Unique IDs")]
    public static void AssignIDs()
    {
        string[] guids = AssetDatabase.FindAssets("t:ObjDataSO");

        // First pass: collect all currently assigned IDs to detect duplicates
        var seenIDs = new System.Collections.Generic.HashSet<int>();
        var duplicates = new System.Collections.Generic.HashSet<int>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ObjDataSO so = AssetDatabase.LoadAssetAtPath<ObjDataSO>(path);
            if (so == null) continue;
            if (so.id > 0 && !seenIDs.Add(so.id))
                duplicates.Add(so.id);
        }

        // Second pass: assign IDs only to assets with id==0 or a duplicate ID
        int nextID = 1;
        int changed = 0;
        // Advance nextID past all valid unique IDs
        while (seenIDs.Contains(nextID) && !duplicates.Contains(nextID)) nextID++;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ObjDataSO so = AssetDatabase.LoadAssetAtPath<ObjDataSO>(path);

            if (so == null)
                continue;

            // Only reassign if unset or a known duplicate
            if (so.id == 0 || duplicates.Contains(so.id))
            {
                while (seenIDs.Contains(nextID) && !duplicates.Contains(nextID)) nextID++;
                seenIDs.Add(nextID);
                so.id = nextID;
                nextID++;
                EditorUtility.SetDirty(so);
                changed++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Auto‑Assign IDs: Updated {changed} ObjDataSO assets.");
    }
}
