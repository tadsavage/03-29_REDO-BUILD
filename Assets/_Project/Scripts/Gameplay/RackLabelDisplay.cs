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

    private void OnEnable()
    {
        if (!_allLabels.Contains(this)) _allLabels.Add(this);
        Debug.Log($"[RackLabelDisplay] Loaded at position {transform.position}, Perp(Z)={transform.position.z:F2}");
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
            if (tmp != null) tmp.gameObject.SetActive(highDetail);
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
