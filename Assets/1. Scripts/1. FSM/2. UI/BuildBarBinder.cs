using UnityEngine;
using UnityEngine.UIElements;
using static BuildBarEvents;



public class BuildBarBinder : MonoBehaviour
{
    [SerializeField] private ObjDataRegistry registry;

    // Callback for when the DELETE button is clicked
    public System.Action OnDeleteClicked;

    // Callback for when the build bar is ready, allowing other systems to subscribe
    public System.Action OnBuildBarReady;

    // Callback for when a build button is clicked, passing the associated ObjDataSO
    public System.Action<ObjDataSO> OnBuildButtonClickedEvent;

    private void OnEnable()
    {
        var uiDoc = GetComponent<UIDocument>();
        var root = uiDoc.rootVisualElement;

        var buttons = root.Query<Button>().ToList();

        int count = Mathf.Min(buttons.Count, registry.buttonSOs.Length);

        // Handle the DELETE button separately
        var deleteButton = root.Q<Button>("ERASE");
        if (deleteButton != null)
        {
            deleteButton.clicked += () =>
            {
                OnDeleteClicked?.Invoke();
            };
        }

        for (int i = 0; i < count; i++)
        {
            Button b = buttons[i];
            ObjDataSO data = registry.buttonSOs[i];

            // Optional: set icon if using VisualElement backgrounds
            if (data.icon != null)
                b.style.backgroundImage = new StyleBackground(data.icon);

            // Tooltip
            b.tooltip = $"{data.name}\nCost: {data.cost}";

            // Click event
            int index = i;
            b.clicked += () => OnBuildButtonClicked(index);
        }
        // NOW schedule the text assignment AND the ready event
        root.schedule.Execute(() =>
        {
            for (int i = 0; i < count; i++)
            {
                Button b = buttons[i];
                ObjDataSO data = registry.buttonSOs[i];
                b.text = "$" + data.cost.ToString();
            }
            OnBuildBarReady?.Invoke();
        });
    }

    private void OnBuildButtonClicked(int index)
    {
        ObjDataSO data = registry.buttonSOs[index];

        // Invoke the event to notify subscribers about the button click    
        OnBuildButtonClickedEvent?.Invoke(data);
    }
}
