using System;
using UnityEngine;
using uOSC;

public class OSCReceiver : MonoBehaviour
{
    public Action<Quaternion> OnRotation;
    void Start()
    {
        var server = GetComponent<uOscServer>();
        server.onDataReceived.AddListener(OnDataReceived);
    }
    

    private void OnDataReceived(Message message)
    {
        Debug.Log($"OSC: {message.address}");

        switch (message.address)
        {
            case "/gyro":
            {
                float pitch = (float)message.values[0];
                float roll = (float)message.values[1];
                float yaw = (float)message.values[2];

                Debug.Log($"Pitch={pitch} Roll={roll} Yaw={yaw}");
                break;
            }

            case "/attitude":
            {
                float x = (float)message.values[0];
                float y = (float)message.values[1];
                float z = (float)message.values[2];
                float w = (float)message.values[3];

                // Swap Y and Z, and adjust signs for Left-Handed Unity space
                Quaternion unityQ = new Quaternion(x, z, y, -w);

                OnRotation?.Invoke(unityQ);
                break;
            }

            case "/hello":
            {
                Debug.Log("Phone connected");
                break;
            }
        }
    }
}