using UnityEngine;

/// <summary>
/// Holds real references to materials that runtime code needs but has no Inspector to receive
/// (components added at runtime, static utilities). Lives at Resources/Resource_SOs/SharedMaterials.
///
/// Why a reference instead of a path: the solid palette material was looked up by its file path, and
/// when the Materials folder moved every lookup silently failed, so received pallets stayed
/// see-through and racks kept their ghost look. A serialized reference follows the asset when it
/// moves and also works in a built player (a path into a non-Resources folder never did).
/// </summary>
[CreateAssetMenu(menuName = "Game/Shared Materials", fileName = "SharedMaterials")]
public class SharedMaterials : ScriptableObject
{
    [Tooltip("The solid low-poly palette material (AA_LowPolyCommon). Received pallets and live racks use it.")]
    [SerializeField] private Material _solidPalette;

    private const string ResourcePath = "Resource_SOs/SharedMaterials";
    private static SharedMaterials _instance;
    private static bool _warned;

    /// <summary>The solid palette material, or null (with one warning) if the asset is missing.</summary>
    public static Material SolidPalette
    {
        get
        {
            if (_instance == null) _instance = Resources.Load<SharedMaterials>(ResourcePath);
            Material m = _instance != null ? _instance._solidPalette : null;
            if (m == null && !_warned)
            {
                _warned = true;
                Debug.LogWarning("[SharedMaterials] Solid palette material is not set (Resources/" + ResourcePath +
                                 "). Received pallets will stay see-through.");
            }
            return m;
        }
    }
}
