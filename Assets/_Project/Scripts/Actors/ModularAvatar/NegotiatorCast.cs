using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The people who can sit across the table in the negotiation window (Tad, 2026-10-11: a suit and tie, an HR lady, a businessman -
/// maybe the clown later). Polyperfect character prefabs, all skinned to the shared _MainRig, so NegotiatorGestures drives any of
/// them. Lives in Resources so it resolves in a built player. A customer picks its person from the ENABLED entries by a hash of
/// its company name, so the same company always sends the same person.
/// </summary>
[CreateAssetMenu(fileName = "NegotiatorCast", menuName = "ForkIT/Negotiator Cast")]
public class NegotiatorCast : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public string label;
        public GameObject prefab;
        [Tooltip("Off = never picked, but kept here so it is one click to add to the rotation later.")]
        public bool enabled = true;
    }

    public List<Entry> entries = new();

    public const string ResourcePath = "Resource_SOs/NegotiatorCast";

    /// <summary>Picks the person for this customer, or null if the cast is empty.</summary>
    public GameObject PickFor(string customerKey)
    {
        var live = entries.FindAll(e => e != null && e.enabled && e.prefab != null);
        if (live.Count == 0) return null;
        uint h = 2166136261u;
        foreach (char c in customerKey ?? "") { h ^= c; h *= 16777619u; }
        return live[(int)(h % (uint)live.Count)].prefab;
    }
}
