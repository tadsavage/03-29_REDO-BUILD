# Bug log - block 6

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 10:58 | **WARN** | Assets\_Project\Scripts\Actors\VehicleGrounding.cs(109,17): warning CS0184: The given expression is never of the provided ('TextMeshPro') type
- day 1 10:58 | **WARN** | Assets\_Project\Scripts\UI_UX\HalScoreModal.cs(136,80): warning CS0219: The variable 'moved' is assigned but its value is never used
- day 1 10:58 | **WARN** | Assets\_Project\Scripts\1. FSM\Core\Inventory\InventoryService.cs(43,43): warning CS0067: The event 'InventoryService.OnInventoryRestored' is never used
- day 1 10:58 | **WARN** | Assets\_Project\Scripts\Gameplay\Light_Adjustments.cs(9,36): warning CS0414: The field 'Light_Adjustments._lightIntensityAdjustment' is assigned but its value is never used
- day 1 10:58 | **WARN** | Assets\_Project\Scripts\UI_UX\FreeLookCamera.cs(40,36): warning CS0414: The field 'FreeLookCamera.dofFocalLengthMm' is assigned but its value is never used
- day 1 10:58 | **WARN** | Assets\_Project\Scripts\UI_UX\SchedulerPanel.cs(923,19): warning CS0414: The field 'SchedulerPanel._lastScheduleHScroll' is assigned but its value is never used
- day 1 10:58 | **WARN** | Assets\_Project\Scripts\UI_UX\TopBarUI.cs(533,17): warning CS0414: The field 'TopBarUI._lastRevenueToday' is assigned but its value is never used
- day 1 10:58 | **WARN** | Assets\_Project\Scripts\Gameplay\Light_Adjustments.cs(12,36): warning CS0414: The field 'Light_Adjustments._lightSpotAngleAdjustment' is assigned but its value is never used
- day 1 10:58 | **WARN** | Assets\_Project\Scripts\UI_UX\FreeLookCamera.cs(42,36): warning CS0414: The field 'FreeLookCamera.dofFStop' is assigned but its value is never used
- day 1 10:58 | **WARN** | Assets\_Project\Scripts\UI_UX\SchedulerPanel.cs(935,17): warning CS0414: The field 'SchedulerPanel._lastLiveScheduleBlock' is assigned but its value is never used
- day 1 10:58 | **WARN** | Assets\_Project\Scripts\Actors\ModularAvatar\AODPreviewStage.cs(43,19): warning CS0414: The field 'AODPreviewStage._pitch' is assigned but its value is never used
- day 1 10:58 | **WARN** | Assets\_Project\Scripts\UI_UX\SchedulerPanel.cs(917,19): warning CS0414: The field 'SchedulerPanel._lastHScroll' is assigned but its value is never used
- day 1 10:58 | **WARN** | Assets\_Project\Scripts\Gameplay\Light_Adjustments.cs(11,36): warning CS0414: The field 'Light_Adjustments._lightRangeAdjustment' is assigned but its value is never used
- day 1 10:58 | **WARN** | Assets\_Project\Scripts\UI_UX\TopBarUI.cs(534,17): warning CS0414: The field 'TopBarUI._lastRevenueYesterday' is assigned but its value is never used
- day 1 10:58 | **WARN** | Assets\_Project\Scripts\UI_UX\SchedulerPanel.cs(934,17): warning CS0414: The field 'SchedulerPanel._lastLiveScheduleDay' is assigned but its value is never used
- day 1 10:58 | **WARN** | Assets\_Project\Scripts\Actors\TruckController.cs(269,36): warning CS0414: The field 'TruckController.unloadDuration' is assigned but its value is never used
- day 1 10:58 | **WARN** | Assets\_Project\Scripts\Actors\ModularAvatar\AODPreviewStage.cs(42,19): warning CS0414: The field 'AODPreviewStage._yaw' is assigned but its value is never used
- day 1 10:58 | **WARN** | Assets\_Project\Scripts\UI_UX\TopBarUI.cs(535,17): warning CS0414: The field 'TopBarUI._lastRevenueWeek' is assigned but its value is never used
- day 1 10:58 | **WARN** | Assets\_Project\Scripts\1. FSM\Core\Diagnostics\PlaytestBot.cs(147,14): warning CS0414: The field 'PlaytestBot._blockDoneAwaitingPlan' is assigned but its value is never used
- day 1 10:58 | **WARN** | Assets\_Project\Scripts\UI_UX\TopBarUI.cs(535,40): warning CS0414: The field 'TopBarUI._lastExpensesWeek' is assigned but its value is never used
- ? | **EXCEPTION** | MissingReferenceException: The variable m_Targets of GameObjectInspector doesn't exist anymore.
  ```
  UnityEngine.Object+MarshalledUnityObject.TryThrowEditorNullExceptionObject (UnityEngine.Object unityObj, System.String parameterName) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Bindings.ThrowHelper.ThrowArgumentNullException (System.Object obj, System.String parameterName) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEditor.PrefabUtility.IsPartOfVariantPrefab (UnityEngine.Object componentOrGameObject) (at <2194b77af8ea43e69caba13ba778a69a>:0)
  UnityEditor.GameObjectInspector.OnEnable () (at <2194b77af8ea43e69caba13ba778a69a>:0)
  UnityEngine.StackTraceUtility:ExtractStringFromExceptionInternal(Object, String&, String&)
  ```
- ? | **EXCEPTION** | SerializedObjectNotCreatableException: Object at index 0 is null
  ```
  UnityEditor.Editor.CreateSerializedObject () (at <2194b77af8ea43e69caba13ba778a69a>:0)
  UnityEditor.Editor.GetSerializedObjectInternal () (at <2194b77af8ea43e69caba13ba778a69a>:0)
  UnityEditor.Editor.get_serializedObject () (at <2194b77af8ea43e69caba13ba778a69a>:0)
  UnityEditor.TransformInspector.OnEnable () (at <2194b77af8ea43e69caba13ba778a69a>:0)
  UnityEngine.StackTraceUtility:ExtractStringFromExceptionInternal(Object, String&, String&)
  ```
- ? | **EXCEPTION** | NullReferenceException: Object reference not set to an instance of an object
  ```
  DockLightController.WriteEmission (UnityEngine.Renderer r, UnityEngine.Color color) (at Assets/_Project/Scripts/Gameplay/DockLightController.cs:66)
  DockLightController.SetOccupied (System.Boolean occupied) (at Assets/_Project/Scripts/Gameplay/DockLightController.cs:48)
  TruckController.OnDocked () (at Assets/_Project/Scripts/Actors/TruckController.cs:3527)
  TruckController.UpdateStraighteningIntoDoor () (at Assets/_Project/Scripts/Actors/TruckController.cs:3221)
  TruckController.UpdateStateMachine () (at Assets/_Project/Scripts/Actors/TruckController.cs:2618)
  TruckController.Update () (at Assets/_Project/Scripts/Actors/TruckController.cs:1780)
  UnityEngine.StackTraceUtility:ExtractStringFromExceptionInternal(Object, String&, String&)
  ```
- day 1 13:10 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 3 task(s) wait
- day 2 19:04 | **WARN** | [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Nancy Stout' as its operator though nobody is on it or running it - clearing the stale link.
- day 2 19:10 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 840s while 1 task(s) wait
- day 2 19:10 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 960s while 1 task(s) wait
- day 5 14:10 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 4020s while 1 task(s) wait
- day 5 18:10 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 240s while 1 task(s) wait
- day 5 22:10 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 240s while 1 task(s) wait
- day 6 00:10 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 240s while 1 task(s) wait
- day 6 03:10 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 300s while 1 task(s) wait
- day 6 05:10 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 300s while 1 task(s) wait
- day 6 08:10 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 300s while 1 task(s) wait
- day 6 09:10 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 240s while 1 task(s) wait
- day 6 13:10 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 300s while 1 task(s) wait
- day 6 13:10 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 240s while 1 task(s) wait
- day 6 18:10 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 300s while 1 task(s) wait
- day 6 18:10 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 300s while 1 task(s) wait
- day 6 23:10 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 300s while 1 task(s) wait
- day 6 23:10 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 300s while 1 task(s) wait
- day 7 03:10 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 240s while 1 task(s) wait
- day 7 03:10 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 240s while 1 task(s) wait
- day 7 07:10 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 240s while 1 task(s) wait
- day 7 07:10 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 240s while 1 task(s) wait
- day 7 12:10 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 300s while 1 task(s) wait
- day 7 12:10 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 300s while 1 task(s) wait
- day 7 17:10 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 300s while 1 task(s) wait
- day 7 17:10 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 300s while 1 task(s) wait
- day 7 22:10 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 300s while 1 task(s) wait
- day 7 22:10 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 300s while 1 task(s) wait
