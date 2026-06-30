# Racking System Setup Guide

## Scene Setup Instructions

### 1. Create a RackingSystemManager GameObject
- In your Main scene, create an empty GameObject named `RackingSystemManager`
- Add the `RackingSystemManager` script component to it
- This will automatically create and wire up all racking system components

### 2. Configure Chevron Prefab References
On the **ChevronSpawner** component (auto-created by RackingSystemManager):

**Fields to Set:**
- **Chevron Sprite:** The arrowhead sprite (downward-pointing chevron)
- **Chevron Material:** Material for chevrons (typically white/bright for visibility)
- **Chevron Height:** Default 0.1 (height above ground to prevent Z-fighting)

### 3. Configure Aisle Initializer
On the **AisleInitializer** component (auto-created by RackingSystemManager):

**Fields to Set:**
- **Real Rack Material:** The material to apply when racks are initialized (replaces orange preview)

### 4. Configure RackSetupUI
- Ensure RackSetupUI exists in your scene (should already be set up)
- The system will auto-find it and wire callbacks

### Note on Rack Prefabs
The system automatically pulls the correct prefab for each rack type from its `ObjDataSO` — the prefab reference is already attached to each placed object's data. No separate prefab configuration needed!

## How It Works

### Placement Flow:
1. Player drags and places a rack in build mode
2. `PlaceCommand.Execute()` calls `RackPlacedEvent.Fire(rackGO)`
3. `RackCollectionDetector` listens and groups adjacent racks
4. When a collection is created, `ChevronSpawner` auto-spawns chevrons:
   - **1 collection** = 2 chevrons (left & right)
   - **2 parallel collections** = 3 chevrons (left, middle, right)
5. Player right-clicks chevron to rotate (set direction of travel)
6. Player double-clicks selected chevron → `RackSetupUI` opens
7. Player enters aisle #, designates levels (Pick/Reserve)
8. Player clicks Submit → `AisleInitializer`:
   - Generates location names (AA-BB-LC format)
   - Instantiates real racks (replaces orange preview)
   - Configures labels (only aisle-facing side visible)
   - Deletes all chevrons
   - Marks collection as initialized

## Prefab Structure Requirements

### Rack Prefab
```
Rack (root)
├── LabelFront.L (VisualElement or GameObject with TextMeshPro)
│   └── TMP_Label (TextMeshPro component)
├── LabelFront.R (VisualElement or GameObject with TextMeshPro)
│   └── TMP_Label (TextMeshPro component)
├── LabelRear.L (VisualElement or GameObject with TextMeshPro)
│   └── TMP_Label (TextMeshPro component)
└── LabelRear.R (VisualElement or GameObject with TextMeshPro)
    └── TMP_Label (TextMeshPro component)
```

Each label group should have 6 levels × 2 positions = 12 TextMeshPro components that will be populated with location names.

## Testing Checklist

- [ ] RackingSystemManager is in the scene
- [ ] All prefab references are assigned
- [ ] Place a single rack → 2 chevrons appear
- [ ] Right-click chevron to rotate it
- [ ] Double-click chevron → RackSetupUI opens
- [ ] Enter aisle number (01-99)
- [ ] Designate levels (Pick/Reserve)
- [ ] Click Submit → Real rack appears, chevrons disappear
- [ ] Labels show correct location names
- [ ] Only aisle-facing labels are visible

## Troubleshooting

**No chevrons spawning:**
- Check that the rack you placed has `category == "Racking"`
- Verify RackCollectionDetector is receiving the RackPlacedEvent

**Chevrons not responding to clicks:**
- Ensure chevron has Box Collider
- Check that ChevronController script is attached

**RackSetupUI not opening:**
- Verify RackSetupUI exists in scene
- Check that the selected chevron is calling `setupUI.SetSelectedChevron(this)`

**Labels not showing location names:**
- Verify TextMeshPro components exist under each label group
- Check that location names are being generated (log in AisleInitializer)
- Confirm labels are being enabled/disabled correctly
