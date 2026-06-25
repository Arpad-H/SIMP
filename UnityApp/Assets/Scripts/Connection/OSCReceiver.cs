using System;
using UnityEngine;
using uOSC;

public class OSCReceiver : MonoBehaviour
{
    public Action<Quaternion> OnRotation;
    public Action<Vector2> OnMove;
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