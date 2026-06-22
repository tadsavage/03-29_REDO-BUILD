// METADATA file_path: Assets/1. Scripts/7. EmployeeSystem/RoleIconLibrary.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Designer-friendly ScriptableObject that maps each EmployeeRole to a Sprite icon.
/// Icons are expected to be authored under <c>Assets/_Project/Art/Icons/RoleIcons/</c>
/// and assigned in the inspector on a single <c>RoleIconLibrary</c> asset.
/// </summary>
[CreateAssetMenu(fileName = "RoleIconLibrary", menuName = "ScriptableObjects/RoleIconLibrary")]
public class RoleIconLibrary : ScriptableObject
{
	[System.Serializable]
	public struct RoleIconEntry
	{
		public EmployeeRole role;
		public Sprite icon;
	}

	[SerializeField] private RoleIconEntry[] _entries;

	private Dictionary<EmployeeRole, Sprite> _lookup;

	/// <summary>Returns the sprite assigned to <paramref name="role"/>, or null.</summary>
	public Sprite GetIcon(EmployeeRole role)
	{
		if (_lookup == null) BuildLookup();
		return _lookup.TryGetValue(role, out var sprite) ? sprite : null;
	}

	private void BuildLookup()
	{
		_lookup = new Dictionary<EmployeeRole, Sprite>();
		if (_entries == null) return;
		foreach (var entry in _entries)
		{
			if (entry.icon != null && !_lookup.ContainsKey(entry.role))
				_lookup[entry.role] = entry.icon;
		}
	}

#if UNITY_EDITOR
	private void OnValidate()
	{
		// Invalidate cached lookup so inspector changes are picked up in play mode.
		_lookup = null;
	}
#endif
}
