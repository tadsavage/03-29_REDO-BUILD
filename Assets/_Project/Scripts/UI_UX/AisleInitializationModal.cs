using UnityEngine;
using UnityEngine.UIElements;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using Warehouse;

public class AisleInitializationModal : MonoBehaviour
{
    private VisualElement _root;
    private VisualElement _modal;
    private List<GameObject> _rackLocations;
    private Camera _isometricCamera;
    private RenderTexture _rackRenderTexture;

    private IntegerField _aisleNumberField;
    private RadioButton _leftSideRadio;
    private RadioButton _rightSideRadio;
    private VisualElement _levelsContainer;
    private Button _submitButton;
    private Button _cancelButton;

    private List<LevelControlGroup> _levelControls = new List<LevelControlGroup>();
    private AisleSide _selectedSide = AisleSide.Right;

    public void Open(List<GameObject> rackLocations)
    {
        _rackLocations = rackLocations;

        // Get or create UIDocument
        var uiDoc = GetComponent<UIDocument>();
        if (uiDoc == null)
        {
            uiDoc = gameObject.AddComponent<UIDocument>();
        }

        _root = new VisualElement();
        _root.style.width = Length.Percent(100);
        _root.style.height = Length.Percent(100);
        _root.style.justifyContent = Justify.Center;
        _root.style.alignItems = Align.Center;
        _root.style.backgroundColor = new Color(0, 0, 0, 0.5f);

        uiDoc.rootVisualElement.Add(_root);

        // Create modal background
        _modal = new VisualElement();
        _modal.style.width = 900;
        _modal.style.height = 700;
        _modal.style.backgroundColor = new Color(0.1f, 0.1f, 0.15f, 1f);
        _modal.style.borderLeftWidth = _modal.style.borderRightWidth =
            _modal.style.borderTopWidth = _modal.style.borderBottomWidth = 2;
        _modal.style.borderLeftColor = _modal.style.borderRightColor =
            _modal.style.borderTopColor = _modal.style.borderBottomColor = new Color(0.3f, 0.3f, 0.4f);
        _modal.style.paddingLeft = _modal.style.paddingRight =
            _modal.style.paddingTop = _modal.style.paddingBottom = 20;

        _root.Add(_modal);

        BuildModalContent();
        CaptureIsometricView();
    }

    private void BuildModalContent()
    {
        // Title
        var title = new Label("INITIALIZATION");
        title.style.fontSize = 24;
        title.style.color = new Color(1, 1, 1, 1);
        title.style.marginBottom = 20;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        _modal.Add(title);

        // Aisle Number Section
        var aisleSection = new VisualElement();
        aisleSection.style.flexDirection = FlexDirection.Row;
        aisleSection.style.marginBottom = 20;
        aisleSection.style.alignItems = Align.Center;

        var aisleLabel = new Label("Aisle Number:");
        aisleLabel.style.color = new Color(0.8f, 0.8f, 0.9f);
        aisleLabel.style.marginRight = 10;
        aisleSection.Add(aisleLabel);

        _aisleNumberField = new IntegerField();
        _aisleNumberField.style.width = 80;
        _aisleNumberField.value = 1;
        aisleSection.Add(_aisleNumberField);

        _modal.Add(aisleSection);

        // Main content row: Isometric view + Level controls
        var mainContent = new VisualElement();
        mainContent.style.flexDirection = FlexDirection.Row;
        mainContent.style.marginBottom = 20;
        mainContent.style.justifyContent = Justify.SpaceBetween;

        // Left side: Side selection + Isometric
        var leftPanel = new VisualElement();
        leftPanel.style.width = Length.Percent(50);
        leftPanel.style.alignItems = Align.Center;

        // Left radio button
        _leftSideRadio = new RadioButton("Workers pick from this side");
        _leftSideRadio.style.marginBottom = 10;
        _leftSideRadio.value = false;
        _leftSideRadio.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue)
            {
                _selectedSide = AisleSide.Left;
                _rightSideRadio.value = false;
            }
        });
        leftPanel.Add(_leftSideRadio);

        // Isometric image placeholder
        var isoImage = new VisualElement();
        isoImage.style.width = 250;
        isoImage.style.height = 350;
        isoImage.style.backgroundColor = new Color(0.2f, 0.2f, 0.25f);
        isoImage.style.borderLeftWidth = isoImage.style.borderRightWidth =
            isoImage.style.borderTopWidth = isoImage.style.borderBottomWidth = 1;
        isoImage.style.borderLeftColor = isoImage.style.borderRightColor =
            isoImage.style.borderTopColor = isoImage.style.borderBottomColor = new Color(0.5f, 0.5f, 0.6f);
        leftPanel.Add(isoImage);

        // Right radio button
        _rightSideRadio = new RadioButton("Workers pick from this side");
        _rightSideRadio.style.marginTop = 10;
        _rightSideRadio.value = true; // Default to right
        _rightSideRadio.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue)
            {
                _selectedSide = AisleSide.Right;
                _leftSideRadio.value = false;
            }
        });
        leftPanel.Add(_rightSideRadio);

        mainContent.Add(leftPanel);

        // Right side: Level controls
        var rightPanel = new VisualElement();
        rightPanel.style.width = Length.Percent(48);
        rightPanel.style.paddingLeft = 10;

        var levelsLabel = new Label("Level Configuration:");
        levelsLabel.style.color = new Color(0.8f, 0.8f, 0.9f);
        levelsLabel.style.marginBottom = 10;
        levelsLabel.style.fontSize = 14;
        rightPanel.Add(levelsLabel);

        _levelsContainer = new ScrollView();
        _levelsContainer.style.height = 320;
        _levelsContainer.style.backgroundColor = new Color(0.05f, 0.05f, 0.1f);
        _levelsContainer.style.paddingLeft = _levelsContainer.style.paddingRight =
            _levelsContainer.style.paddingTop = _levelsContainer.style.paddingBottom = 10;

        BuildLevelControls();
        rightPanel.Add(_levelsContainer);

        mainContent.Add(rightPanel);
        _modal.Add(mainContent);

        // Buttons
        var buttonRow = new VisualElement();
        buttonRow.style.flexDirection = FlexDirection.Row;
        buttonRow.style.justifyContent = Justify.FlexEnd;
        buttonRow.style.marginTop = 10;
        buttonRow.style.gap = 10;

        _cancelButton = new Button(() => Cancel());
        _cancelButton.text = "Cancel";
        _cancelButton.style.width = 100;
        _cancelButton.style.height = 35;
        _cancelButton.style.backgroundColor = new Color(0.3f, 0.3f, 0.35f);
        buttonRow.Add(_cancelButton);

        _submitButton = new Button(() => Submit());
        _submitButton.text = "Submit";
        _submitButton.style.width = 100;
        _submitButton.style.height = 35;
        _submitButton.style.backgroundColor = new Color(0.2f, 0.6f, 0.2f);
        buttonRow.Add(_submitButton);

        _modal.Add(buttonRow);
    }

    private void BuildLevelControls()
    {
        // Auto-detect levels from rack locations
        var heights = _rackLocations
            .Select(l => Mathf.Round(l.transform.position.y, 2))
            .Distinct()
            .OrderBy(h => h)
            .ToList();

        _levelControls.Clear();
        int levelIndex = 0;

        foreach (var height in heights)
        {
            var sampleLocation = _rackLocations.FirstOrDefault(l =>
                Mathf.Abs(l.transform.position.y - height) < 0.1f);

            float locationHeight = sampleLocation != null ? GetLocationHeight(sampleLocation) : 48;
            bool isPickLevel = locationHeight <= 80;

            var levelGroup = new LevelControlGroup
            {
                LevelNumber = levelIndex + 1,
                LocationHeight = locationHeight,
                IsPhysicallyPick = isPickLevel
            };

            var levelRow = new VisualElement();
            levelRow.style.flexDirection = FlexDirection.Row;
            levelRow.style.marginBottom = 15;
            levelRow.style.alignItems = Align.Center;
            levelRow.style.paddingBottom = 10;
            levelRow.style.borderBottomWidth = 1;
            levelRow.style.borderBottomColor = new Color(0.3f, 0.3f, 0.4f);

            // Level label
            var levelLabel = new Label($"Level {levelIndex + 1} ({locationHeight:F0}\")");
            levelLabel.style.width = 100;
            levelLabel.style.color = new Color(0.8f, 0.8f, 0.9f);
            levelLabel.style.minWidth = 100;
            levelRow.Add(levelLabel);

            // Pick radio
            var pickRadio = new RadioButton();
            pickRadio.label = "Pick";
            pickRadio.style.marginRight = 20;
            pickRadio.value = isPickLevel; // Default to pick if physically possible

            if (!isPickLevel)
            {
                pickRadio.SetEnabled(false); // Gray out if too high
            }

            pickRadio.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue && !isPickLevel)
                {
                    ShowToast("This is too high to reach");
                    reserveRadio.value = true;
                    return;
                }

                if (evt.newValue)
                {
                    levelGroup.Type = LocationType.Pick;
                    reserveRadio.value = false;
                }
            });

            levelRow.Add(pickRadio);

            // Reserve radio
            var reserveRadio = new RadioButton();
            reserveRadio.label = "Reserve";
            reserveRadio.value = !isPickLevel; // Default to reserve if too high

            reserveRadio.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    levelGroup.Type = LocationType.Reserve;
                    pickRadio.value = false;
                }
            });

            levelRow.Add(reserveRadio);

            levelGroup.PickRadio = pickRadio;
            levelGroup.ReserveRadio = reserveRadio;
            _levelControls.Add(levelGroup);

            _levelsContainer.Add(levelRow);
            levelIndex++;
        }
    }

    private void CaptureIsometricView()
    {
        // Create temporary camera for isometric capture
        var camGO = new GameObject("IsometricCapture");
        _isometricCamera = camGO.AddComponent<Camera>();

        // Position at 45-degree isometric angle
        // Look at the center of all rack locations
        var centerPos = Vector3.zero;
        foreach (var loc in _rackLocations)
        {
            centerPos += loc.transform.position;
        }
        centerPos /= _rackLocations.Count;

        // Isometric angle: 45 degrees around Y, about 30-35 degrees down
        _isometricCamera.transform.position = centerPos + new Vector3(5, 4, 5);
        _isometricCamera.transform.LookAt(centerPos);

        // Create render texture
        _rackRenderTexture = new RenderTexture(512, 512, 24);
        _isometricCamera.targetTexture = _rackRenderTexture;

        // Render once
        _isometricCamera.Render();

        Debug.Log("Isometric view captured");
        // TODO: Display _rackRenderTexture in the UI (requires TextureElement or Image element setup)
    }

    private float GetLocationHeight(GameObject location)
    {
        var collider = location.GetComponent<Collider>();
        if (collider != null)
        {
            return collider.bounds.size.y * 39.37f; // Convert meters to inches
        }
        return location.transform.localScale.y * 39.37f;
    }

    private void Submit()
    {
        // Validate aisle number
        if (_aisleNumberField.value < 1 || _aisleNumberField.value > 99)
        {
            ShowToast("Please enter a valid aisle number (01-99)");
            return;
        }

        if (WarehouseLocationsRegistry.Instance.IsAisleUsed(_aisleNumberField.value))
        {
            ShowToast("This aisle is already being used");
            return;
        }

        // Validate side selected
        if (!_leftSideRadio.value && !_rightSideRadio.value)
        {
            ShowToast("Please select a worker entry side");
            return;
        }

        // Validate all levels configured
        if (_levelControls.Any(lc => lc.Type == null))
        {
            ShowToast("Please configure all levels");
            return;
        }

        // Create level configs
        var levelConfigs = _levelControls
            .Select(lc => new LevelConfig { LevelNumber = lc.LevelNumber, Type = lc.Type.Value })
            .ToList();

        // Initialize aisle
        var locations = AisleInitializer.InitializeAisle(
            _rackLocations,
            _aisleNumberField.value,
            _selectedSide,
            levelConfigs
        );

        // Save to registry
        WarehouseLocationsRegistry.Instance.AddLocations(locations);

        ShowToast($"Aisle {_aisleNumberField.value:D2} initialized with {locations.Count} locations");
        Close();
    }

    private void Cancel()
    {
        Close();
    }

    private void Close()
    {
        // Cleanup
        if (_isometricCamera != null)
            Destroy(_isometricCamera.gameObject);
        if (_rackRenderTexture != null)
            Destroy(_rackRenderTexture);

        _root?.parent?.Remove(_root);
        Destroy(gameObject);
    }

    private void ShowToast(string message)
    {
        Debug.Log($"[Toast] {message}");
        // TODO: Integrate with your toast system
    }

    private class LevelControlGroup
    {
        public int LevelNumber;
        public float LocationHeight;
        public bool IsPhysicallyPick;
        public LocationType? Type;
        public RadioButton PickRadio;
        public RadioButton ReserveRadio;
    }
}
