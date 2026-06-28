using System;
using UnityEngine;
using uOSC;

public class OSCReceiver : MonoBehaviour
{
    public Action<Quaternion> OnRotation;
    public Action<Vector2> OnMove;

    // Fired once each time the player taps a corner tap-zone on the companion play screen (/tap).
    // The argument is which zone was hit: 0 = bottom-left, 1 = bottom-right.
    public Action<int> OnTap;

    // Raw motion streams from the phone, mapped into Unity's left-handed space.
    public Action<Vector3> OnAcceleration;        // /accel    m/s^2 (includes gravity)
    public Action<Vector3> OnLinearAcceleration;  // /linaccel m/s^2 (gravity removed)

    public enum PhoneOrientationMode{Portrait, LandscapeLeft, LandscapeRight}

    [Header("Phone Movement")]
    [SerializeField] private PhoneOrientationMode phoneOrientationMode = PhoneOrientationMode.Portrait;
    [SerializeField] private float maxTiltDegrees = 25f;
    [SerializeField] private float deadZone = 0.12f;
    [SerializeField] private bool invertForwardBack = true;
    [SerializeField] private bool invertLeftRight = false;

    private Quaternion neutralRotation = Quaternion.identity;
    private Quaternion latestRotation = Quaternion.identity;
    private bool hasNeutralRotation = false;

    public Vector2 MoveInput { get; private set; }

    void Start()
    {
        var server = GetComponent<uOscServer>();
        server.onDataReceived.AddListener(OnDataReceived);
    }

    private void OnDataReceived(Message message)
    {
        //Debug.Log($"OSC: {message.address}");

        switch (message.address)
        {
            case "/gyro":
            {
                float pitch = (float)message.values[0];
                float roll = (float)message.values[1];
                float yaw = (float)message.values[2];

                //Debug.Log($"Pitch={pitch} Roll={roll} Yaw={yaw}");
                break;
            }

            case "/accel":
            {
                OnAcceleration?.Invoke(ReadVec3(message));
                break;
            }

            case "/linaccel":
            {
                OnLinearAcceleration?.Invoke(ReadVec3(message));
                break;
            }

            case "/attitude":
            {
                float x = (float)message.values[0];
                float y = (float)message.values[1];
                float z = (float)message.values[2];
                float w = (float)message.values[3];

                // Debug.Log($"x: {x}, y: {y}, z: {z}, w: {w}");

                // Swap Y and Z, and adjust signs for Left-Handed Unity space
                Quaternion unityQ = new Quaternion(x, z, y, -w);

                latestRotation = unityQ;

                if (!hasNeutralRotation)
                {
                    Calibrate();
                }

                OnRotation?.Invoke(unityQ);

                Vector2 move = RotationToMoveInput(unityQ);
                MoveInput = move;
                OnMove?.Invoke(move);

                break;
            }

            case "/hello":
            {
                Debug.Log("Phone connected");
                break;
            }

            case "/calibrate":
            {
                Calibrate();
                Debug.Log("Phone calibrated");
                break;
            }

            case "/tap":
            {
                // Argument: 0 = bottom-left zone, 1 = bottom-right zone.
                int side = message.values.Length > 0 ? Mathf.RoundToInt((float)message.values[0]) : 0;
                OnTap?.Invoke(side);
                Debug.Log($"Phone tapped ({(side == 1 ? "right" : "left")})");
                break;
            }
        }
    }

    public void Calibrate()
    {
        neutralRotation = latestRotation;
        hasNeutralRotation = true;
    }

    public void ResetCalibration()
    {
        hasNeutralRotation = false;
    }

    private Vector2 RotationToMoveInput(Quaternion currentRotation)
    {
        Quaternion relativeQ = Quaternion.Inverse(neutralRotation) * currentRotation;

        Vector3 euler = relativeQ.eulerAngles;

        float pitch = NormalizeAngle(euler.x); // forward / backward tilt
        float roll = NormalizeAngle(euler.z);  // left / right tilt

        float moveX = Mathf.Clamp(roll / maxTiltDegrees, -1f, 1f);
        float moveY = Mathf.Clamp(pitch / maxTiltDegrees, -1f, 1f);

        switch (phoneOrientationMode) {
            case PhoneOrientationMode.LandscapeLeft:
            {
                float oldX = moveX;
                float oldY = moveY;

                moveX = oldY;
                moveY = -oldX;
                break;
            }

            case PhoneOrientationMode.LandscapeRight:
            {
                float oldX = moveX;
                float oldY = moveY;

                moveX = -oldY;
                moveY = oldX;
                break;
            }
        }

        if (invertLeftRight)
            moveX *= -1f;

        if (invertForwardBack)
            moveY *= -1f;

        moveX = ApplyDeadZone(moveX, deadZone);
        moveY = ApplyDeadZone(moveY, deadZone);

        return new Vector2(moveX, moveY);
    }

    // Reads 3 floats and maps the phone's axes into Unity's left-handed space by swapping Y and Z,
    // matching the /attitude quaternion conversion above.
    private static Vector3 ReadVec3(Message message)
    {
        float x = (float)message.values[0];
        float y = (float)message.values[1];
        float z = (float)message.values[2];
        return new Vector3(x, z, y);
    }

    private float NormalizeAngle(float angle)
    {
        if (angle > 180f)
            angle -= 360f;

        return angle;
    }

    private float ApplyDeadZone(float value, float deadZone)
    {
        if (Mathf.Abs(value) < deadZone)
            return 0f;

        return value;
    }
}