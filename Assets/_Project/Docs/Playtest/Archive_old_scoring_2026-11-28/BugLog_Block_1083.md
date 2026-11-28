# Bug log - block 1083

Every incident as it happened. Format: day hour:minute | category | what.

- day 7 21:36 | **EXCEPTION** | UnityException: NameToLayer is not allowed to be called from a MonoBehaviour constructor (or instance field initializer), call it in Awake or Start instead. Called from MonoBehaviour 'GraphicsPresetManager' on game object 'GraphicsPresetManager'.
  ```
  UnityEngine.LayerMask.NameToLayer (System.String layerName) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  GraphicsPresetManager..cctor () (at Assets/_Project/Scripts/1. FSM/Core/Managers/GraphicsPresetManager.cs:92)
  Rethrow as TypeInitializationException: The type initializer for 'GraphicsPresetManager' threw an exception.
  FreeLookCamera.UpdateDioramaDof () (at Assets/_Project/Scripts/UI_UX/FreeLookCamera.cs:314)
  FreeLookCamera.LateUpdate () (at Assets/_Project/Scripts/UI_UX/FreeLookCamera.cs:310)
  UnityEngine.StackTraceUtility:ExtractStringFromExceptionInternal(Object, String&, String&)
  ```
- day 7 21:36 | **GHOST-SLOT** | RECORD 285045cb 801236 x43 cell=(25, 83) lane= - no object
- day 7 21:36 | **REPEATING** | x25 and counting: UnityException: NameToLayer is not allowed to be called from a MonoBehaviour constructor (or instance field initializer), call it in Awake or Start instead. Called from MonoBehaviour 'GraphicsPresetManager' on game object 'GraphicsPresetManager'.
