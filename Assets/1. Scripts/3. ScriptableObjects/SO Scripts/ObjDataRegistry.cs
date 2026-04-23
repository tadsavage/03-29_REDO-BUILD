using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Obj Data Registry", menuName = "Scriptable Objects/Obj Data Registry")]
public class ObjDataRegistry : ScriptableObject
{
    public List<ObjDataSO> buttonSOs = new();

    public ObjDataSO Get(int index)
    {
        if (index < 0 || index >= buttonSOs.Count)
            return null;

        return buttonSOs[index];
    }
    public ObjDataSO GetByID(string id)
    {
        foreach (var so in buttonSOs)
        {
            if (so != null && so.objName == id)
                return so;
        }

        Debug.LogWarning($"ObjDataRegistry: No ObjDataSO found with id '{id}'");
        return null;
    }

}
