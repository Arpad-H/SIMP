using UnityEngine;
using UnityEngine.InputSystem;

public class SquirrelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private OSCReceiver oscReceiver;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float turnSpeed = 12f;
    [SerializeField] private float normalBlendSpeed = 12f;

    [Header("Jump")]
    [SerializeField] private float jumpUpSpeed = 9f;
    [SerializeField] private float jumpForwardBoost = 4f;
    [SerializeField] private float gravity = 18f;
    [SerializeField] private float maxFallSpeed = 18f;
    [SerializeField] private float jumpSurfaceIgnoreTime = 0.2f;

    [Header("Gliding / Falling")]
    [SerializeField] private float glideForwardSpeed = 3f;
    [SerializeField] private float glideForwardControl = 2f;
    [SerializeField] private float glideDownSpeed = 5f;
    [SerializeField] private float glideTurnSpeed = 120f;

    [Header("Surface Detection")]
    [SerializeField] private LayerMask solidSurfaceMask;
    [SerializeField] private float probeDistance = 5f;
    [SerializeField] private float surfaceOffset = 0.3f;
    [SerializeField] private float sphereProbeRadius = 0.18f;
    [SerializeField] private float maxSnapDistance = 5f;
    [SerializeField] private float groundedDistance = 0.18f;

    [Header("Surface Transition")]
    [SerializeField, Range(0f, 180f)] private float maxSurfaceChangeAngle = 100f;
    [SerializeField] private float surfaceTransitionProbeDistance = 0.8f;

    [Header("Collision")]
    [SerializeField] private float collisionRadius = 0.25f;
    [SerializeField] private float skinWidth = 0.03f;
    [SerializeField] private int slideIterations = 3;
    [SerializeField] private float collisionCenterOffset = 0.22f;

    [Header("Phone Input")]
    [SerializeField] private bool usePhoneInput = true;
    [SerializeField] private bool combinePhoneWithKeyboard = true;

    private Vector3 surfaceNormal = Vector3.up;
    private Vector3 lastSurfaceNormal = Vector3.up;
    private bool grounded;
    private Vector3 airVelocity;
    private float ignoreSurfaceUntilTime;

    private Vector2 phoneMove;

    private void Reset()
    {
        cameraTransform = Camera.main != null ? Camera.main.transform : null;
        oscReceiver = FindAnyObjectByType<OSCReceiver>();
    }

    private void Update()
    {
        Vector2 moveInput = GetMoveInput();


        bool jumpPressed =
            Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame;

        if (Time.time >= ignoreSurfaceUntilTime)
        {
            ProbeSurface();
        }
        else
        {
            grounded = false;
        }

        if (grounded && jumpPressed)
        {
            Jump(moveInput);
        }

        if (grounded)
        {
            airVelocity = Vector3.zero;
            MoveAlongSurface(moveInput);
        }
        else
        {
            GlideWithGlobalGravity(moveInput);
        }

        if (Time.time >= ignoreSurfaceUntilTime)
        {
            ProbeSurface();
        }

        AlignToSurface();
    }

    private Vector2 GetMoveInput()
    {
        Vector2 keyboardInput = Vector2.zero;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                keyboardInput.x -= 1f;

            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                keyboardInput.x += 1f;

            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                keyboardInput.y -= 1f;

            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                keyboardInput.y += 1f;
        }

        if (keyboardInput.sqrMagnitude > 1f)
            keyboardInput.Normalize();

        Vector2 moveInput = keyboardInput;

        if (usePhoneInput)
        {
            moveInput = combinePhoneWithKeyboard
                ? keyboardInput + phoneMove
                : phoneMove;
        }

        if (moveInput.sqrMagnitude > 1f)
            moveInput.Normalize();

        return moveInput;
    }

    private void Jump(Vector2 moveInput)
    {
        Vector3 jumpDirection = surfaceNormal;

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, surfaceNormal).normalized;

        if (forward.sqrMagnitude < 0.001f && cameraTransform != null)
        {
            forward = Vector3.ProjectOnPlane(cameraTransform.forward, surfaceNormal).normalized;
        }

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = transform.forward;
        }

        airVelocity =
            jumpDirection * jumpUpSpeed +
            forward * jumpForwardBoost;

        grounded = false;

        ignoreSurfaceUntilTime = Time.time + jumpSurfaceIgnoreTime;
    }
    private void ProbeSurface()
    {
        grounded = false;

        Vector3[] probeDirections =
        {
            -surfaceNormal,
            Vector3.down,
            -transform.up
        };

        RaycastHit bestHit = new RaycastHit();
        bool foundHit = false;
        float bestDistance = float.MaxValue;

        foreach (Vector3 rawDirection in probeDirections)
        {
            Vector3 direction = rawDirection.normalized;
            Vector3 origin = transform.position - direction * 0.1f;

            RaycastHit[] hits = Physics.SphereCastAll(
                origin,
                sphereProbeRadius,
                direction,
                probeDistance,
                solidSurfaceMask,
                QueryTriggerInteraction.Ignore
            );

            foreach (RaycastHit hit in hits)
            {
                if (IsOwnCollider(hit.collider))
                    continue;

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    bestHit = hit;
                    foundHit = true;
                }
            }
        }

        if (!foundHit)
        {
            grounded = false;
            return;
        }

        float distanceFromSurface = Vector3.Dot(transform.position - bestHit.point, bestHit.normal);

        bool closeEnoughToGround =
            distanceFromSurface <= surfaceOffset + groundedDistance;

        if (!closeEnoughToGround)
        {
            grounded = false;
            return;
        }

        grounded = true;
        lastSurfaceNormal = surfaceNormal;

        surfaceNormal = Vector3.Slerp(
            surfaceNormal,
            bestHit.normal.normalized,
            normalBlendSpeed * Time.deltaTime
        ).normalized;

        if (bestHit.distance <= maxSnapDistance)
        {
            Vector3 targetPosition = bestHit.point + surfaceNormal * surfaceOffset;

            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                30f * Time.deltaTime
            );
        }
    }

    private void MoveAlongSurface(Vector2 moveInput)
    {
        // A / D rotate around the current surface normal.
        if (Mathf.Abs(moveInput.x) > 0.01f)
        {
            float yawAmount = moveInput.x * turnSpeed * 40f * Time.deltaTime;
            transform.Rotate(surfaceNormal, yawAmount, Space.World);
        }

        // Forward/backward movement is based on the squirrel's own facing direction.
        Vector3 moveForward = Vector3.ProjectOnPlane(transform.forward, surfaceNormal).normalized;

        if (moveForward.sqrMagnitude < 0.001f && cameraTransform != null)
        {
            moveForward = Vector3.ProjectOnPlane(cameraTransform.forward, surfaceNormal).normalized;
        }

        if (moveForward.sqrMagnitude < 0.001f)
            return;

        Vector3 moveDirection = moveForward * moveInput.y;

        SafeMove(moveDirection * moveSpeed * Time.deltaTime);

        if (moveDirection.sqrMagnitude > 0.001f)
        {
            TryTransitionToSurface(moveDirection);
        }
    }

    private void GlideWithGlobalGravity(Vector2 moveInput)
    {
        if (Mathf.Abs(moveInput.x) > 0.01f)
        {
            float yawAmount = moveInput.x * glideTurnSpeed * Time.deltaTime;
            transform.Rotate(Vector3.up, yawAmount, Space.World);
        }

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        if (forward.sqrMagnitude < 0.001f && cameraTransform != null)
        {
            forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        }

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = transform.forward;
        }

        float controlledForwardSpeed =
            glideForwardSpeed +
            moveInput.y * glideForwardControl;

        controlledForwardSpeed = Mathf.Max(0f, controlledForwardSpeed);

        airVelocity += Vector3.down * gravity * Time.deltaTime;

        if (airVelocity.y < -maxFallSpeed)
        {
            airVelocity.y = -maxFallSpeed;
        }

        Vector3 glideMovement =
            airVelocity +
            forward * controlledForwardSpeed;

        SafeMove(glideMovement * Time.deltaTime);
    }

    private void SafeMove(Vector3 movement)
    {
        Vector3 remainingMovement = movement;

        for (int i = 0; i < slideIterations; i++)
        {
            if (remainingMovement.sqrMagnitude < 0.000001f)
                break;

            Vector3 direction = remainingMovement.normalized;
            float distance = remainingMovement.magnitude;

            if (TryGetBlockingHit(direction, distance + skinWidth, out RaycastHit hit))
            {
                float safeDistance = Mathf.Max(0f, hit.distance - skinWidth);
                transform.position += direction * safeDistance;

                Vector3 leftoverMovement = remainingMovement - direction * safeDistance;
                remainingMovement = Vector3.ProjectOnPlane(leftoverMovement, hit.normal);
            }
            else
            {
                transform.position += remainingMovement;
                break;
            }
        }
    }

    private bool TryGetBlockingHit(Vector3 direction, float distance, out RaycastHit blockingHit)
    {
        blockingHit = new RaycastHit();

        RaycastHit[] hits = Physics.SphereCastAll(
            GetCollisionCenter(),
            collisionRadius,
            direction,
            distance,
            solidSurfaceMask,
            QueryTriggerInteraction.Ignore
        );

        bool foundHit = false;
        float closestDistance = float.MaxValue;

        foreach (RaycastHit hit in hits)
        {
            if (IsOwnCollider(hit.collider))
                continue;

            float movementAgainstSurface = Vector3.Dot(direction, hit.normal);

            if (movementAgainstSurface > -0.05f)
                continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                blockingHit = hit;
                foundHit = true;
            }
        }

        return foundHit;
    }

    private void AlignToSurface()
    {
        Vector3 targetUp = grounded ? surfaceNormal : Vector3.up;

        Quaternion surfaceAlignment =
            Quaternion.FromToRotation(transform.up, targetUp) * transform.rotation;

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            surfaceAlignment,
            normalBlendSpeed * Time.deltaTime
        );
    }

    private void TryTransitionToSurface(Vector3 moveDirection)
    {
        if (moveDirection.sqrMagnitude < 0.001f)
            return;

        Vector3 origin = transform.position + surfaceNormal * surfaceOffset;

        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            sphereProbeRadius,
            moveDirection.normalized,
            surfaceTransitionProbeDistance,
            solidSurfaceMask,
            QueryTriggerInteraction.Ignore
        );

        RaycastHit bestHit = new RaycastHit();
        bool foundSurface = false;
        float closestDistance = float.MaxValue;

        foreach (RaycastHit hit in hits)
        {
            if (IsOwnCollider(hit.collider))
                continue;

            float angleFromCurrentSurface = Vector3.Angle(surfaceNormal, hit.normal);

            if (angleFromCurrentSurface > maxSurfaceChangeAngle)
                continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                bestHit = hit;
                foundSurface = true;
            }
        }

        if (!foundSurface)
            return;

        surfaceNormal = bestHit.normal.normalized;

        Vector3 targetPosition = bestHit.point + surfaceNormal * surfaceOffset;
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            20f * Time.deltaTime
        );
    }
    private void RotateToward(Vector3 direction, Vector3 up)
    {
        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction, up);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            turnSpeed * Time.deltaTime
        );
    }

    private bool IsOwnCollider(Collider other)
    {
        if (other == null)
            return false;

        return other.transform == transform || other.transform.IsChildOf(transform);
    }

    private Vector3 GetCollisionCenter()
    {
        Vector3 upDirection = grounded ? surfaceNormal : Vector3.up;
        return transform.position + upDirection * collisionCenterOffset;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, surfaceNormal);

        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, -surfaceNormal * probeDistance);

        Gizmos.color = Color.yellow;
        Vector3 gizmoUp = Application.isPlaying ? (grounded ? surfaceNormal : Vector3.up): transform.up;

        Gizmos.DrawWireSphere(
            transform.position + gizmoUp * collisionCenterOffset,
            collisionRadius
        );

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position - surfaceNormal * surfaceOffset, sphereProbeRadius);
    }


    private void OnEnable()
    {
        if (oscReceiver != null)
            oscReceiver.OnMove += HandlePhoneMove;
    }

    private void OnDisable()
    {
        if (oscReceiver != null)
            oscReceiver.OnMove -= HandlePhoneMove;
    }
    private void HandlePhoneMove(Vector2 move)
    {
        phoneMove = move;
    }

}