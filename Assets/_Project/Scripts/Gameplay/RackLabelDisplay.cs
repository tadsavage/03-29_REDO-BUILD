using UnityEngine;
using TMPro;
using System.Collections.Generic;

[ExecuteAlways]
public class RackLabelDisplay : MonoBehaviour
{
    [Header("Data")]
    public string labelText = "A-01-01";

    [Header("References")]
    [SerializeField] private List<TextMeshPro> textComponents = new List<TextMeshPro>();
    [SerializeField] private GameObject lowDetailVisual; // Optional billboard for far distance

    private static List<RackLabelDisplay> _allLabels = new List<RackLabelDisplay>();
    public static IReadOnlyList<RackLabelDisplay> AllLabels => _allLabels;

    private bool _isVisible = true;
    // True once this rack has been submitted/initialized through the aisle setup. Acts as a
    // "hands-off" flag: other systems must not re-process, re-ghost, or toggle its labels.
    // Only the aisle setup code (AisleInitializer) is allowed to set/clear this.
    public bool IsRackLive { get; set; } = false;

    private void OnEnable()
    {
        if (!_allLabels.Contains(this)) _allLabels.Add(this);

        // Parent this rack to the "Racking" object if it exists
        Transform rackingParent = GameObject.Find("Racking")?.transform;
        if (rackingParent != null && transform.parent != rackingParent)
        {
            transform.SetParent(rackingParent, worldPositionStays: true);
        }

        UpdateDisplay();
    }

    private void OnDisable()
    {
        _allLabels.Remove(this);
    }

    private void OnValidate()
    {
        UpdateDisplay();
    }

    public void UpdateDisplay()
    {
        foreach (var tmp in textComponents)
        {
            if (tmp != null) tmp.text = labelText;
        }
    }

    public void SetDetailLevel(bool highDetail)
    {
        if (_isVisible == highDetail) return;
        
        _isVisible = highDetail;

        foreach (var tmp in textComponents)
        {
            if (tmp == null) continue;

            if (!highDetail)
            {
                // Hiding for distance LOD is always safe.
                tmp.gameObject.SetActive(false);
                continue;
            }

            // Re-show for near LOD, but never resurrect a label whose container (the parent
            // face) has been switched off. On a live rack only the chevron-facing side's
            // containers are active, so this keeps the disabled side hidden — hands off.
            Transform container = tmp.transform.parent;
            bool containerActive = container == null || container.gameObject.activeSelf;
            if (containerActive) tmp.gameObject.SetActive(true);
        }

        if (lowDetailVisual != null)
        {
            lowDetailVisual.SetActive(!highDetail);
        }
    }

    [ContextMenu("Auto-Assign TMP Children")]
    public void AutoAssign()
    {
        textComponents.Clear();
        textComponents.AddRange(GetComponentsInChildren<TextMeshPro>(true));
        UpdateDisplay();
    }
}
