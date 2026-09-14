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
        RebuildLookup();
    }

    // Separated from OnEnable so GetByID can self-heal: OnEnable only runs on load/domain-reload,
    // so an entry added to buttonSOs afterward (e.g. via SerializedObject in an Editor tool, with
    // no subsequent recompile) would otherwise leave a live-in-memory registry permanently unable
    // to find it — exactly what happened when Cross-Wall was added to buttonSOs but GetByID(919)
    // kept missing until the dictionary was rebuilt.
    private void RebuildLookup()
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
        if (_lookup == null)
            RebuildLookup();

        if (_lookup.TryGetValue(id, out var result))
            return result;

        // Cache miss doesn't necessarily mean the id is really missing — buttonSOs may have grown
        // since _lookup was last built. Rebuild once and retry before reporting a real miss.
        RebuildLookup();
        if (_lookup.TryGetValue(id, out result))
            return result;

        Debug.LogWarning($"ObjDataRegistry: No ObjDataSO found with id {id} — object will be skipped on load.");
        return null;
    }

}
