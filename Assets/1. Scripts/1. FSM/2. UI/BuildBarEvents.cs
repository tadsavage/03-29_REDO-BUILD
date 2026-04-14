using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class BuildBarEvents : MonoBehaviour
{
    [SerializeField] private PlacementStateMachine fsm;

    // This is a reference to the ScriptableObject that holds all the ObjDataSO instances for the build bar buttons. It should be assigned in the Inspector.
    [SerializeField] private ObjDataRegistry registry;
    [CreateAssetMenu(fileName = "ObjDataRegistry", menuName = "Scriptable Objects/ObjDataRegistry")]
    public class ObjDataRegistry : ScriptableObject {   public ObjDataSO[] buttonSOs;  }
    private Button _button;

    private void Awake()
    {
        var uiDoc = GetComponent<UIDocument>();
        if (uiDoc == null)
            return;

        var root = uiDoc.rootVisualElement;

        // Find ALL labels under the build bar
        var labels = root.Query<Label>().ToList();

        int count = Mathf.Min(labels.Count, registry.buttonSOs.Length);

        for (int i = 0; i < count; i++)
        {
            Label label = labels[i];
            ObjDataSO data = registry.buttonSOs[i];

            // Update label text to the SO name
            label.text = data.objName;
        }
    }
    private void OnEnable()
    {
        if (_button != null)
            _button.clicked += OnClick;
    } 
    private void OnDisable()
    {
        if (_button != null)
            _button.clicked -= OnClick;
    }
    public void OnClick()
    {
        // Store the selected object type
        //fsm.BuildState.SetData(data);
        // Enter raycast mode (ray + cell indicator)
        //fsm.SetState(fsm.RaycastState);
    }
}

