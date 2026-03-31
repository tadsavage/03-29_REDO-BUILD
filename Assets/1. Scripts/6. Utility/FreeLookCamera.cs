using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.UIElements.UxmlAttributeDescription;

/// <summary>
/// A simple free camera to be added to a Unity game object.
/// 
/// Keys:
///	wasd / arrows	- movement
///	q/e 			- up/down (local space)
///	r/f 			- up/down (world space)
///	pageup/pagedown	- up/down (world space)
///	hold shift		- enable fast movement mode
///	right mouse  	- enable free look
///	mouse			- free look / rotation
///     
/// </summary>
public class FreeLookCamera : MonoBehaviour
{
	/// <summary>
	/// Normal speed of camera movement.
	/// </summary>
	public float movementSpeed = 10f;

	/// <summary>
	/// Speed of camera movement when shift is held down,
	/// </summary>
	public float fastMovementSpeed = 25f;

	/// <summary>
	/// Sensitivity for free look.
	/// </summary>
	public float freeLookSensitivity = 3f;

	/// <summary>
	/// Amount to zoom the camera when using the mouse wheel.
	/// </summary>
	public float zoomSensitivity = 10f;

	/// <summary>
	/// Amount to zoom the camera when using the mouse wheel (fast mode).
	/// </summary>
	public float fastZoomSensitivity = 50f;

	/// <summary>
	/// Normal speed of camera movement.
	/// </summary>
	public float heightMax = 6f;
	
	/// <summary>
	/// Normal speed of camera movement.
	/// </summary>
	public float heightMin = 1f;

    public float X_Min = -18f;
    public float X_Max = 18f;

    public float Z_Min = -8f;
    public float Z_Max = 18f;

    /// <summary>
    /// Set to true when free looking (on right mouse button).
    /// </summary>
    private bool looking = false;

	//bool _running = false;
	//bool _leftDown = false;
	//bool _rightDown = false;

	void Update()
	{
		var fastMode = Keyboard.current[Key.LeftShift].isPressed;
		var movementSpeed = fastMode ? this.fastMovementSpeed : this.movementSpeed;

		if (Keyboard.current[Key.A].isPressed || Keyboard.current[Key.LeftArrow].isPressed)
		{
			transform.position = transform.position + (-transform.right * movementSpeed * Time.deltaTime);
		}
        if (Keyboard.current[Key.D].isPressed || Keyboard.current[Key.RightArrow].isPressed)
		{
			transform.position = transform.position + (transform.right * movementSpeed * Time.deltaTime);
		}
		if (Mouse.current.leftButton.isPressed && Mouse.current.rightButton.isPressed)
		{
			transform.position = transform.position + (transform.forward * movementSpeed * Time.deltaTime);
		}
        if (Keyboard.current[Key.W].isPressed || Keyboard.current[Key.UpArrow].isPressed)
		{
			transform.position = transform.position + (transform.forward * movementSpeed * Time.deltaTime);
		}
        if (Keyboard.current[Key.S].isPressed || Keyboard.current[Key.DownArrow].isPressed)
        {
			transform.position = transform.position + (-transform.forward * movementSpeed * Time.deltaTime);
		}

		if (Keyboard.current[Key.Q].isPressed)
		{
			transform.position = transform.position + (transform.up * movementSpeed  * Time.deltaTime);
		}

        if (Keyboard.current[Key.E].isPressed)
        {
			transform.position = transform.position + (-transform.up * movementSpeed * Time.deltaTime);
		}

		if (Keyboard.current[Key.R].isPressed || Keyboard.current[Key.PageUp].isPressed)
		{
			transform.position = transform.position + (Vector3.up * movementSpeed * Time.deltaTime);
		}

		if (Keyboard.current[Key.F].isPressed || Keyboard.current[Key.PageDown].isPressed)
        {
			transform.position = transform.position + (-Vector3.up * movementSpeed * Time.deltaTime);
		}

		if (transform.position.y < heightMin)
		{
			transform.position = new Vector3(transform.position.x, heightMin, transform.position.z);
		}
		else if (transform.position.y > heightMax)
		{
			transform.position = new Vector3(transform.position.x, heightMax, transform.position.z);
		}
		//  X Clamps
        if (transform.position.x < X_Min)
        {
            transform.position = new Vector3(X_Min, transform.position.y, transform.position.z);
        }
        else if (transform.position.x > X_Max)
        {
            transform.position = new Vector3(X_Max, transform.position.y, transform.position.z);
        }
        //  Z Clamps
        if (transform.position.z < Z_Min)
        {
            transform.position = new Vector3(transform.position.x, transform.position.y, Z_Min);
        }
        else if (transform.position.z > Z_Max)
        {
            transform.position = new Vector3(transform.position.x, transform.position.y, Z_Max);
        }

        if (looking)
		{
            


            float newRotationX = transform.localEulerAngles.y + Mouse.current.delta.x.ReadValue() * freeLookSensitivity;
			float newRotationY = transform.localEulerAngles.x - Mouse.current.delta.y.ReadValue() * freeLookSensitivity;
			transform.localEulerAngles = new Vector3(newRotationY, newRotationX, 0f);
		}

		float axis = Mouse.current.scroll.ReadValue().y;//Input.GetAxis("Mouse ScrollWheel");
		if (axis != 0)
		{
			var zoomSensitivity = fastMode ? this.fastZoomSensitivity : this.zoomSensitivity;
			GetComponentInChildren<Camera>().transform.position = transform.position + transform.forward * axis * zoomSensitivity;
		}

		if (Mouse.current.rightButton.wasPressedThisFrame)
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

	/// <summary>
	/// Enable free looking.
	/// </summary>
	public void StartLooking()
	{
		looking = true;
		Cursor.visible = false;
		Cursor.lockState = CursorLockMode.Locked;
	}

	/// <summary>
	/// Disable free looking.
	/// </summary>
	public void StopLooking()
	{
		looking = false;
		Cursor.visible = true;
		Cursor.lockState = CursorLockMode.None;
	}
}