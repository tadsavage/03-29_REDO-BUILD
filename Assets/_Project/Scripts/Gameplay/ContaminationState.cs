using UnityEngine;

/// <summary>
/// Added at runtime by a rat (see <see cref="RatBehavior"/>) to whatever product it
/// scavenges — a pallet of cases, or loose cases on the ground. It accumulates
/// "infestation time" each time a rat gnaws on it; if the player (or the exterminator)
/// doesn't intervene, that time crosses a threshold and the product SPOILS:
///
///   • the cases tint a gooey toxic green
///   • a toxic gas cloud + flies spawn (prefab hooks — debug-log stubs until art exists)
///   • the console logs "{playerName} has lost ${X} in product"
///   • a persistent RED world-space "$" label hovers over the product until it is deleted
///
/// On delete, <see cref="DeleteCommand"/> checks <see cref="IsContaminated"/>: spoiled
/// product refunds $0 and floats a green "$0" instead of the normal refund.
///
/// If rats are scared off before the threshold, the infestation timer decays back down,
/// so timely intervention saves the product.
/// </summary>
[DisallowMultipleComponent]
public class ContaminationState : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Seconds of accumulated rat gnawing before the product spoils.")]
    public float contaminationThreshold = 25f;
    [Tooltip("How fast the infestation timer bleeds back down when no rat is gnawing (seconds per second).")]
    public float decayPerSecond = 0.5f;

    [Header("Look")]
    [Tooltip("Tint applied to the product's materials once it spoils.")]
    public Color gooColor = new Color(0.36f, 0.70f, 0.12f);
    [Tooltip("Optional toxic gas cloud prefab spawned on spoil. If null, a debug log stands in.")]
    public GameObject toxicCloudPrefab;
    [Tooltip("Optional flies prefab spawned on spoil. If null, a debug log stands in.")]
    public GameObject fliesPrefab;
    [Tooltip("Height (world units) of the persistent red $ label above the product.")]
    public float labelHeight = 1.6f;

    /// <summary>True once the product has spoiled. Read by DeleteCommand.</summary>
    public bool IsContaminated { get; private set; }
    /// <summary>Dollar value of product lost when it spoiled (for logging / future UI).</summary>
    public int LostValue { get; private set; }

    private float _infest;
    private FloatingMoneyText _label;

    // ── Public API (called by RatBehavior) ──────────────────────────────────────

    /// <summary>A rat gnawed for <paramref name="seconds"/>. Pushes the infestation
    /// timer up; spoils the product once it crosses the threshold.</summary>
    public void AddInfestation(float seconds)
    {
        if (IsContaminated) return;
        _infest += Mathf.Max(0f, seconds);
        if (_infest >= contaminationThreshold)
            Contaminate();
    }

    // ── Decay ───────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (IsContaminated || _infest <= 0f) return;
        _infest = Mathf.Max(0f, _infest - decayPerSecond * Time.deltaTime);
    }

    // ── Spoil ─────────────────────────────────────────────────────────────────────

    private void Contaminate()
    {
        IsContaminated = true;
        LostValue = ComputeProductValue();

        TintGooey();
        SpawnSpoilVfx();
        LogLoss();
        ShowPersistentLabel();

        Debug.Log($"[Contamination] '{name}' spoiled — ${LostValue:N0} of product lost. " +
                  "Delete the pallet to clear it (no refund).");
    }

    private int ComputeProductValue()
    {
        int value = 0;

        var po = GetComponent<PlacedObject>();
        if (po != null && po.data != null) value += po.data.cost;

        var pb = GetComponent<PalletBuilder>();
        if (pb != null && pb.CurrentLoadCost > 0) value += pb.CurrentLoadCost;

        return Mathf.Max(0, value);
    }

    private void LogLoss()
    {
        string playerName = PlayerPrefs.GetString("PlayerName", "");
        if (string.IsNullOrEmpty(playerName)) playerName = "The manager";
        Debug.Log($"{playerName} has lost ${LostValue:N0} in product");
    }

    private void ShowPersistentLabel()
    {
        // Negative amount → red "spend" color, persistent (no rise/fade) until the
        // product (and this component) is destroyed.
        _label = FloatingMoneyText.ShowPersistent(
            transform,
            transform.position + Vector3.up * labelHeight,
            -LostValue);
    }

    private void TintGooey()
    {
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r == null) continue;
            // .materials creates per-renderer instances we can safely recolor.
            var mats = r.materials;
            foreach (var m in mats)
            {
                if (m == null) continue;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", gooColor);
                if (m.HasProperty("_Color"))     m.SetColor("_Color", gooColor);
            }
        }
    }

    private void SpawnSpoilVfx()
    {
        Vector3 at = transform.position + Vector3.up * 0.5f;

        if (toxicCloudPrefab != null)
            Instantiate(toxicCloudPrefab, at, Quaternion.identity, transform);
        else
            Debug.Log($"[Contamination] (stub) toxic gas cloud would appear at {name}.");

        if (fliesPrefab != null)
            Instantiate(fliesPrefab, at, Quaternion.identity, transform);
        else
            Debug.Log($"[Contamination] (stub) swarm of flies would appear at {name}.");
    }
}
