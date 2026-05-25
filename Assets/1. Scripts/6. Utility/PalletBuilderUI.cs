using UnityEngine;
using UnityEngine.UIElements;
using System.Globalization;

[RequireComponent(typeof(UIDocument))]
public class PalletBuilderUI : MonoBehaviour
{
    [SerializeField] private PalletBuilder targetBuilder;

    private UIDocument _doc;
    private TextField _maxHeightField;
    private TextField _spaceField;
    private TextField _gapField;
    private TextField _crookedField;
    private Button _buildButton;

    public void Initialize(PalletBuilder builder)
    {
        targetBuilder = builder;
        RefreshUI();
    }

    private void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        var root = _doc.rootVisualElement;

        // CRITICAL: Ensure the full-screen root container doesn't block clicks to the build menu below
        root.pickingMode = PickingMode.Ignore;

        _maxHeightField = root.Q<TextField>("MaxHeightField");
        _spaceField = root.Q<TextField>("SpaceField");
        _gapField = root.Q<TextField>("GapField");
        _crookedField = root.Q<TextField>("CrookedField");
        _buildButton = root.Q<Button>("BuildButton");

        if (targetBuilder == null) targetBuilder = GetComponentInParent<PalletBuilder>();

        if (targetBuilder != null)
        {
            RefreshUI();
        }

        _buildButton.clicked += OnBuildClicked;
    }

    private void RefreshUI()
    {
        _maxHeightField.value = targetBuilder.maxTotalHeight.ToString(CultureInfo.InvariantCulture);
        _spaceField.value = targetBuilder.spaceBetweenCases.ToString(CultureInfo.InvariantCulture);
        _gapField.value = targetBuilder.verticalGap.ToString(CultureInfo.InvariantCulture);
        _crookedField.value = targetBuilder.crookedCase.ToString(CultureInfo.InvariantCulture);
    }

    private void OnBuildClicked()
    {
        if (targetBuilder == null) return;

        if (float.TryParse(_maxHeightField.value, NumberStyles.Float, CultureInfo.InvariantCulture, out float h))
            targetBuilder.maxTotalHeight = h;
        
        if (float.TryParse(_spaceField.value, NumberStyles.Float, CultureInfo.InvariantCulture, out float s))
            targetBuilder.spaceBetweenCases = s;

        if (float.TryParse(_gapField.value, NumberStyles.Float, CultureInfo.InvariantCulture, out float g))
            targetBuilder.verticalGap = g;

        if (float.TryParse(_crookedField.value, NumberStyles.Float, CultureInfo.InvariantCulture, out float c))
            targetBuilder.crookedCase = c;

        targetBuilder.Build();
    }
}
