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
}
