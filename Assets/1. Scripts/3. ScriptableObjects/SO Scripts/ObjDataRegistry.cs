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
        {
            if (data == null)
            {
                Debug.LogWarning("[ObjDataRegistry] Null entry in buttonSOs — remove it in the Inspector.");
                continue;
            }
            _lookup[data.id] = data;
        }
    }
    public ObjDataSO GetByID(int id)
    {
        if (_lookup.TryGetValue(id, out var result))
            return result;

        Debug.LogWarning($"ObjDataRegistry: No ObjDataSO found with id {id} — object will be skipped on load.");
        return null;
    }

}
