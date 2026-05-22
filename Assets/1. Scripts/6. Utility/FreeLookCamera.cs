using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems; // REQUIRED: Adds native access to Unity's core Event System tracking loops

public class FreeLookCamera : MonoBehaviour
{
    [SerializeField] private BuildMenuUI buildMenuUI;

    public float movementSpeed = 10f;
    public float fastMovementSpeed = 25f;
    public float freeLookSensitivity = 3f;
    public float zoomSensitivity = 10f;
    public float fastZoomSensitivity = 50f;
    public float heightMax = 6f;
    public float heightMin = 1f;
    public float X_Min = -18f;
    public float X_Max = 18f;
    public float Z_Min = -8f;
    public float Z_Max = 18f;

    private bool looking = false;

    void Update()
    {
        var fastMode = Keyboard.current[Key.LeftShift].isPressed;
        var currentMovementSpeed = fastMode ? this.fastMovementSpeed : this.movementSpeed;

        // FIXED: Combines your script check with Unity's global event system to capture button and list hover layouts perfectly
        bool isMouseOverUI = (buildMenuUI != null && buildMenuUI.IsPointerOverBuildMenu) ||
                             (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());

        // -----------------------------------------------------------------
        // KEYBOARD TRANSLATION MOVEMENT (Always allowed, never locked by UI)
        // -----------------------------------------------------------------
        if (Keyboard.current[Key.A].isPressed || Keyboard.current[Key.LeftArrow].isPressed)
        {
            transform.position = transform.position + (-transform.right * currentMovementSpeed * Time.deltaTime);
        }
        if (Keyboard.current[Key.D].isPressed || Keyboard.current[Key.RightArrow].isPressed)
        {
            transform.position = transform.position + (transform.right * currentMovementSpeed * Time.deltaTime);
        }
        if (Mouse.current.leftButton.isPressed && Mouse.current.rightButton.isPressed && !isMouseOverUI)
        {
            transform.position = transform.position + (transform.forward * currentMovementSpeed * Time.deltaTime);
        }
        if (Keyboard.current[Key.W].isPressed || Keyboard.current[Key.UpArrow].isPressed)
        {
            transform.position = transform.position + (transform.forward * currentMovementSpeed * Time.deltaTime);
        }
        if (Keyboard.current[Key.S].isPressed || Keyboard.current[Key.DownArrow].isPressed)
        {
            transform.position = transform.position + (-transform.forward * currentMovementSpeed * Time.deltaTime);
        }
        if (Keyboard.current[Key.Q].isPressed)
        {
            transform.position = transform.position + (transform.up * currentMovementSpeed * Time.deltaTime);
        }
        if (Keyboard.current[Key.E].isPressed)
        {
            transform.position = transform.position + (-transform.up * currentMovementSpeed * Time.deltaTime);
        }
        if (Keyboard.current[Key.R].isPressed || Keyboard.current[Key.PageUp].isPressed)
        {
            transform.position = transform.position + (Vector3.up * currentMovementSpeed * Time.deltaTime);
        }
        if (Keyboard.current[Key.F].isPressed || Keyboard.current[Key.PageDown].isPressed)
        {
            transform.position = transform.position + (-Vector3.up * currentMovementSpeed * Time.deltaTime);
        }

        // Height Clamps
        if (transform.position.y < heightMin) transform.position = new Vector3(transform.position.x, heightMin, transform.position.z);
        else if (transform.position.y > heightMax) transform.position = new Vector3(transform.position.x, heightMax, transform.position.z);

        // X Clamps
        if (transform.position.x < X_Min) transform.position = new Vector3(X_Min, transform.position.y, transform.position.z);
        else if (transform.position.x > X_Max) transform.position = new Vector3(X_Max, transform.position.y, transform.position.z);

        // Z Clamps
        if (transform.position.z < Z_Min) transform.position = new Vector3(transform.position.x, transform.position.y, Z_Min);
        else if (transform.position.z > Z_Max) transform.position = new Vector3(transform.position.x, transform.position.y, Z_Max);

        // -----------------------------------------------------------------
        // MOUSE LOOK ROTATION (Rotation processing ignores UI limits while active)
        // -----------------------------------------------------------------
        if (looking)
        {
            float newRotationX = transform.localEulerAngles.y + Mouse.current.delta.x.ReadValue() * freeLookSensitivity;
            float newRotationY = transform.localEulerAngles.x - Mouse.current.delta.y.ReadValue() * freeLookSensitivity;
            transform.localEulerAngles = new Vector3(newRotationY, newRotationX, 0f);
        }

        // -----------------------------------------------------------------
        // SCROLL WHEEL ZOOM MECHANIC (Blocked explicitly if mouse is over UI)
        // -----------------------------------------------------------------
        if (!isMouseOverUI)
        {
            float axis = Mouse.current.scroll.ReadValue().y;
            if (axis != 0)
            {
                var currentZoomSensitivity = fastMode ? this.fastZoomSensitivity : this.zoomSensitivity;
                GetComponentInChildren<Camera>().transform.position = transform.position + transform.forward * (axis * 0.01f) * currentZoomSensitivity;
            }
        }

        // -----------------------------------------------------------------
        // INTERACTION CLICK ACTIONS (Cannot activate free look if hovering UI)
        // -----------------------------------------------------------------
        if (Mouse.current.rightButton.wasPressedThisFrame && !isMouseOverUI)
        {
            StartLooking();
        }
        else if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            StopLooking();
        }
    }

    void OnDisable()
    {
        StopLooking();
    }

    public void StartLooking()
    {
        looking = true;
        Cursor.visible = false;
    }

    public void StopLooking()
    {
        looking = false;
        Cursor.visible = true;
    }

    public CameraSaveData GetState()
    {
        return new CameraSaveData { focusPoint = transform.position, pitch = transform.localEulerAngles.x, yaw = transform.localEulerAngles.y, distance = 0 };
    }

    public void SetState(CameraSaveData state)
    {
        if (state == null) return;
        transform.position = state.focusPoint;
        transform.localEulerAngles = new Vector3(state.pitch, state.yaw, 0);
    }
}
