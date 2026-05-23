using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class CADJoystickController : MonoBehaviour
{
    [Header("Rotation")]
    public float rotationSpeed = 90f;
    public float rollSpeed = 90f;

    [Header("Scale")]
    public float scaleSpeed = 1.0f;
    public float minScaleFactor = 0.1f;
    public float maxScaleFactor = 10.0f;

    [Header("Input")]
    public float deadZone = 0.15f;

    private InputDevice leftController;
    private InputDevice rightController;

    private readonly List<InputDevice> leftDeviceCache = new List<InputDevice>();
    private readonly List<InputDevice> rightDeviceCache = new List<InputDevice>();

    private Vector3 initialScale;
    private float scaleFactor = 1.0f;

    private void Start()
    {
        initialScale = transform.localScale;
        FindControllers();
    }

    private void Update()
    {
        if (!leftController.isValid || !rightController.isValid)
        {
            FindControllers();
        }

        Vector2 leftStick = ReadStick(leftController);
        Vector2 rightStick = ReadStick(rightController);

        ApplyRotation(leftStick, rightStick);
        ApplyScale(rightStick);
    }

    private void FindControllers()
    {
        leftDeviceCache.Clear();
        rightDeviceCache.Clear();

        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller,
            leftDeviceCache
        );

        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller,
            rightDeviceCache
        );

        if (leftDeviceCache.Count > 0) leftController = leftDeviceCache[0];
        if (rightDeviceCache.Count > 0) rightController = rightDeviceCache[0];
    }

    private Vector2 ReadStick(InputDevice device)
    {
        if (!device.isValid) return Vector2.zero;

        if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
        {
            if (Mathf.Abs(axis.x) < deadZone) axis.x = 0f;
            if (Mathf.Abs(axis.y) < deadZone) axis.y = 0f;
            return axis;
        }
        return Vector2.zero;
    }

    private void ApplyRotation(Vector2 leftStick, Vector2 rightStick)
    {
        float dt = Time.deltaTime;
        float yaw = leftStick.x * rotationSpeed * dt;
        float pitch = -leftStick.y * rotationSpeed * dt;
        float roll = -rightStick.x * rollSpeed * dt;

        transform.Rotate(Vector3.up, yaw, Space.World);
        transform.Rotate(Vector3.right, pitch, Space.World);
        transform.Rotate(Vector3.forward, roll, Space.World);
    }

    private void ApplyScale(Vector2 rightStick)
    {
        if (Mathf.Abs(rightStick.y) < deadZone) return;

        scaleFactor += rightStick.y * scaleSpeed * Time.deltaTime;
        scaleFactor = Mathf.Clamp(scaleFactor, minScaleFactor, maxScaleFactor);
        transform.localScale = initialScale * scaleFactor;
    }
}
