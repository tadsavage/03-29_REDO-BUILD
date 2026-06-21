using UnityEngine;
using TMPro;

[ExecuteAlways]
public class DoorNumberDisplay : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int doorNumber = 1;
    [SerializeField] private string format = "D1";

    [Header("References")]
    [SerializeField] private TMP_Text[] textComponents;

    public int Number
    {
        get => doorNumber;
        set
        {
            doorNumber = value;
            UpdateDisplay();
        }
    }

    private void OnValidate()
    {
        UpdateDisplay();
    }

    private void Start()
    {
        UpdateDisplay();
    }
    private void Update()
    {
        //UpdateDisplay();
    }
    public void UpdateDisplay()
    {
        if (textComponents == null) return;
        string s = doorNumber.ToString(format);
        foreach (var t in textComponents)
        {
            if (t != null)
            {
                t.text = s;
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(t);
#endif
            }
        }
    }

    [ContextMenu("Auto-Assign References")]
    public void AutoAssign()
    {
        textComponents = GetComponentsInChildren<TMP_Text>(true);
        UpdateDisplay();
    }
}
