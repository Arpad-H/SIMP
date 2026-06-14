using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerCamera;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float sprintSpeed = 12f;
    [SerializeField] private float jumpHeight = 1.4f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float gravityMultiplier = 2f;

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float minLookAngle = -85f;
    [SerializeField] private float maxLookAngle = 85f;

    private CharacterController controller;

    private Vector2 moveInput;
    private Vector2 lookInput;

    private float verticalVelocity;
    private float cameraPitch;

    private bool isSprinting;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (playerCamera == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
                playerCamera = mainCamera.transform;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        ReadInput();
        HandleLook();
        HandleMovement();
    }

    private void ReadInput()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (keyboard == null || mouse == null)
            return;

        moveInput = Vector2.zero;

        if (keyboard.wKey.isPressed) moveInput.y += 1f;
        if (keyboard.sKey.isPressed) moveInput.y -= 1f;
        if (keyboard.dKey.isPressed) moveInput.x += 1f;
        if (keyboard.aKey.isPressed) moveInput.x -= 1f;

        moveInput = Vector2.ClampMagnitude(moveInput, 1f);

        lookInput = mouse.delta.ReadValue();

        isSprinting = keyboard.leftShiftKey.isPressed;
    }

    private void HandleLook()
    {
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, minLookAngle, maxLookAngle);

        playerCamera.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    private void HandleMovement()
    {
        bool grounded = controller.isGrounded;

        if (grounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

        Vector3 move =
            transform.right * moveInput.x +
            transform.forward * moveInput.y;

        if (Keyboard.current.spaceKey.wasPressedThisFrame && grounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity * gravityMultiplier);
        }

        verticalVelocity += gravity * gravityMultiplier * Time.deltaTime;

        Vector3 velocity = move * currentSpeed;
        velocity.y = verticalVelocity;

        controller.Move(velocity * Time.deltaTime);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}