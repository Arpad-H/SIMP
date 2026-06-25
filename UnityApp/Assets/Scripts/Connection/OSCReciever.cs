using System;
using UnityEngine;
using uOSC;

public class OSCReceiver : MonoBehaviour
{
    [Tooltip("Log every incoming OSC message address (noisy: ~60 Hz per sensor). Use only to verify the phone feed.")]
    public bool logMessages = false;

    // Orientation (quaternion), converted to Unity's left-handed space and re-based on the last
    // calibrated neutral pose (identity until the phone sends /calibrate).
    public Action<Quaternion> OnRotation;

    // Motion sensors (Vector3), converted to Unity's left-handed space.
    public Action<Vector3> OnAngularVelocity;    // /gyro          rad/s
    public Action<Vector3> OnAcceleration;       // /accel         m/s^2 (includes gravity)
    public Action<Vector3> OnLinearAcceleration; // /linaccel      m/s^2 (gravity removed)
    public Action<Vector3> OnGravity;            // /gravity       m/s^2
    public Action<Vector3> OnMagnetometer;       // /magnetometer  microtesla

    // Inverse of the neutral orientation captured at the last /calibrate. Pre-multiplying each raw
    // orientation by this re-bases all rotation input on that neutral pose (neutral -> identity).
    private Quaternion neutralInverse = Quaternion.identity;

    void Start()
    {
        var server = GetComponent<uOscServer>();
        server.onDataReceived.AddListener(OnDataReceived);
    }

    private void OnDataReceived(Message message)
    {
        if (logMessages)
            Debug.Log($"OSC: {message.address}");

        switch (message.address)
        {
            case "/attitude":
            {
                // Deliver the orientation relative to the calibrated neutral pose.
                Quaternion unityQ = ToUnityQuaternion(message);
                OnRotation?.Invoke(neutralInverse * unityQ);
                break;
            }

            case "/calibrate":
            {
                // The phone's current pose becomes the new neutral; store its inverse so every
                // subsequent /attitude is expressed relative to it (neutral -> identity).
                neutralInverse = Quaternion.Inverse(ToUnityQuaternion(message));
                Debug.Log("Calibrated: new neutral orientation set");
                break;
            }

            // Angular velocity (rad/s). Replaces the old pitch/roll/yaw stub.
            case "/gyro":         OnAngularVelocity?.Invoke(ReadVec3(message)); break;
            case "/accel":        OnAcceleration?.Invoke(ReadVec3(message)); break;
            case "/linaccel":     OnLinearAcceleration?.Invoke(ReadVec3(message)); break;
            case "/gravity":      OnGravity?.Invoke(ReadVec3(message)); break;
            case "/magnetometer": OnMagnetometer?.Invoke(ReadVec3(message)); break;

            case "/hello":
            {
                Debug.Log("Phone connected");
                break;
            }
        }
    }

    // Reads the 4-float quaternion (x, y, z, w) and maps it into Unity's left-handed space by
    // swapping Y and Z and flipping W, matching the original /attitude conversion.
    private static Quaternion ToUnityQuaternion(Message message)
    {
        float x = (float)message.values[0];
        float y = (float)message.values[1];
        float z = (float)message.values[2];
        float w = (float)message.values[3];
        return new Quaternion(x, z, y, -w);
    }

    // Reads 3 floats and maps the phone's right-handed axes (x right, y up, z toward the user)
    // into Unity's left-handed space by swapping Y and Z, matching the /attitude convention
    // above. Adjust the signs/order if your scene needs a different mapping.
    private static Vector3 ReadVec3(Message message)
    {
        float x = (float)message.values[0];
        float y = (float)message.values[1];
        float z = (float)message.values[2];
        return new Vector3(x, z, y);
    }
}
