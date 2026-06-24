using System;
using UnityEngine;
using uOSC;

public class OSCReceiver : MonoBehaviour
{
    [Tooltip("Log every incoming OSC message address (noisy: ~60 Hz per sensor). Use only to verify the phone feed.")]
    public bool logMessages = false;

    // Orientation (quaternion), converted to Unity's left-handed space.
    public Action<Quaternion> OnRotation;

    // Motion sensors (Vector3), converted to Unity's left-handed space.
    public Action<Vector3> OnAngularVelocity;    // /gyro          rad/s
    public Action<Vector3> OnAcceleration;       // /accel         m/s^2 (includes gravity)
    public Action<Vector3> OnLinearAcceleration; // /linaccel      m/s^2 (gravity removed)
    public Action<Vector3> OnGravity;            // /gravity       m/s^2
    public Action<Vector3> OnMagnetometer;       // /magnetometer  microtesla

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
                float x = (float)message.values[0];
                float y = (float)message.values[1];
                float z = (float)message.values[2];
                float w = (float)message.values[3];

                // Swap Y and Z, and adjust signs for left-handed Unity space.
                Quaternion unityQ = new Quaternion(x, z, y, -w);

                OnRotation?.Invoke(unityQ);
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
