using UnityEngine;
using UnityEngine.UIElements;

public class BuildBarEvents : MonoBehaviour
{
    [SerializeField] private PlacementStateMachine fsm;
    [SerializeField] private ObjDataSO data;

    private Button _button;



    private void Awake()
    {
        // UI Toolkit: get the button on this element
        var uiDoc = GetComponent<UIDocument>();
        if (uiDoc != null)
        {
            var root = uiDoc.rootVisualElement;
            _button = root.Q<Button>();
            string buttonName = _button.name;
            _button.text = data.cost.ToString() + " " + data.objName;
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
        fsm.BuildState.SetData(data);
        // Enter raycast mode (ray + cell indicator)
        fsm.SetState(fsm.RaycastState);
    }
}

