using UnityEngine;

public class Peanut : MonoBehaviour
{
    [Header("Floating Settings")]
    [SerializeField] private float floatSpeed = 2f;     // How fast it moves up and down
    [SerializeField] private float floatAmplitude = 0.5f; // How far up and down it goes

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 50f;  // Degrees per second

    private Vector3 startPosition;

    void Start()
    {
        // Record the starting position of the peanut
        startPosition = transform.position;
    }

    void Update()
    {
        // 1. Handle the hovering/floating motion
        // Mathf.Sin gives us a smooth wave between -1 and 1 over time
        float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
        transform.position = new Vector3(startPosition.x, newY, startPosition.z);

        // 2. Handle the rotation
        // Rotates the peanut around the Y-axis (spinning in place)
        transform.Rotate(Vector3.up * (rotationSpeed * Time.deltaTime));
    }
}