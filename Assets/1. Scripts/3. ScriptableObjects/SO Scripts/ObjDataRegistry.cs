using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Obj Data Registry", menuName = "Scriptable Objects/Obj Data Registry")]
public class ObjDataRegistry : ScriptableObject
{
    public List<ObjDataSO> buttonSOs = new();
    private Dictionary<int, ObjDataSO> _lookup;

    public ObjDataSO Get(int index)
    {
        if (index < 0 || index >= buttonSOs.Count)
            return null;

        return buttonSOs[index];
    }
    private void OnEnable()
    {
        _lookup = new Dictionary<int, ObjDataSO>();
        foreach (var data in buttonSOs)
            _lookup[data.id] = data;
    }
    public ObjDataSO GetByID(int id)
    {
        if (_lookup.TryGetValue(id, out var result))
            return result;

        Debug.LogError($"ObjDataRegistry: No ObjDataSO found with id {id}");
        return null;
    }

}
