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
    // How far past surfaceOffset the head anchor may be and still count as stuck.
    [SerializeField] private float stickTolerance = 0.25f;
    // How fast the body is pulled back to surfaceOffset along the normal.
    [SerializeField] private float heightCorrectSpeed = 18f;

    [Header("Head Probe")]
    // The single surface probe is taken from a point near the head. This keeps
    // the camera steady (it sits near the pivot, so the tail/body swings instead
    // of the view) and makes the probe lead in the direction you face, so sharp
    // transitions are picked up early.
    [SerializeField] private float headForwardOffset = 0.25f;
    [SerializeField] private float headUpOffset = 0f;
    // How strongly the lead probe leans forward (0 = straight into the surface).
    [SerializeField] private float forwardProbeBias = 0.5f;

    [Header("Surface Transition")]
    [SerializeField, Range(0f, 180f)] private float maxSurfaceChangeAngle = 140f;

    [Header("Surface Stickiness")]
    // How long after losing grip we keep holding the surface orientation
    // before righting up. Smooths out single-frame probe misses.
    [SerializeField] private float groundCoyoteTime = 0.2f;
    // How fast we ease back to world-up once genuinely airborne/gliding.
    [SerializeField] private float airRightingSpeed = 4f;

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
    private float lastGroundedTime = -999f;
    private Vector3 airAlignUp = Vector3.up;

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

        // `grounded` here is the result of the previous frame's stick test.
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

        // A single probe, taken after we've moved, is the only thing that drives
        // grounding, the surface normal and height. No competing systems.
        if (Time.time >= ignoreSurfaceUntilTime)
        {
            StickToSurface();
        }
        else
        {
            grounded = false;
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
    // The probe point on the body, near the head. Offset forward so the probe
    // leads in the direction we face, and (optionally) up toward the head.
    private Vector3 ProbeAnchor()
    {
        return transform.position
            + transform.forward * headForwardOffset
            + transform.up * headUpOffset;
    }

    // Our facing flattened into the current surface plane. Falls back gracefully
    // if we're momentarily facing straight along the normal.
    private Vector3 SurfaceForward()
    {
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, surfaceNormal);

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(transform.up, surfaceNormal);

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(Vector3.forward, surfaceNormal);

        return forward.normalized;
    }

    // Single source of truth for grounding, surface normal and height. Casts a
    // small forward-biased fan from the head anchor. While grounded every ray is
    // expressed in the surface frame (relative to the current normal), so we are
    // never pulled toward unrelated geometry like the floor under a wall we are
    // clinging to. While airborne we add world/body-down rays to detect landing.
    private void StickToSurface()
    {
        bool wasGrounded = grounded;

        Vector3 anchor = ProbeAnchor();
        Vector3 n = surfaceNormal;
        Vector3 f = SurfaceForward();

        Vector3[] directions;
        if (wasGrounded)
        {
            directions = new[]
            {
                -n,                          // straight into the surface: primary grip
                -n + f * forwardProbeBias,   // lead forward over convex edges
                f - n * 0.5f,                // forward & down: next branch / under-edge
                f + n * 0.7f,                // forward & up: branch above the head
                -n - f * 0.4f,               // trailing grip while cresting an edge
            };
        }
        else
        {
            directions = new[]
            {
                -transform.up,
                Vector3.down,
                airVelocity.sqrMagnitude > 0.01f ? airVelocity.normalized : Vector3.down,
            };
        }

        RaycastHit bestHit = new RaycastHit();
        bool found = false;
        float nearestDistance = float.MaxValue;

        foreach (Vector3 rawDirection in directions)
        {
            if (rawDirection.sqrMagnitude < 0.0001f)
                continue;

            Vector3 direction = rawDirection.normalized;
            Vector3 origin = anchor + n * sphereProbeRadius; // start just off the surface

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

                // Reject absurd flips from the surface we're on (e.g. grabbing
                // the underside of our own branch as a separate surface).
                if (wasGrounded && Vector3.Angle(n, hit.normal) > maxSurfaceChangeAngle)
                    continue;

                // Perpendicular distance from the head anchor to this surface.
                float distAlong = Vector3.Dot(anchor - hit.point, hit.normal);

                if (distAlong < -surfaceOffset)                 // behind / inside it
                    continue;

                if (distAlong > surfaceOffset + stickTolerance) // too far to cling
                    continue;

                // Prefer the nearest surface so we stay on what's under us and
                // only transition when another surface actually gets closer.
                if (distAlong < nearestDistance)
                {
                    nearestDistance = distAlong;
                    bestHit = hit;
                    found = true;
                }
            }
        }

        if (!found)
        {
            grounded = false;
            return;
        }

        grounded = true;
        lastGroundedTime = Time.time;
        lastSurfaceNormal = surfaceNormal;

        // Decisive grab for big normal changes (corners / branch jumps), gentle
        // blend for small ones (following a curve). Snap immediately on landing.
        float changeAngle = Vector3.Angle(surfaceNormal, bestHit.normal);
        float grab = wasGrounded ? Mathf.InverseLerp(20f, 90f, changeAngle) : 1f;
        float blend = Mathf.Lerp(normalBlendSpeed * Time.deltaTime, 1f, grab);

        surfaceNormal = Vector3.Slerp(
            surfaceNormal,
            bestHit.normal.normalized,
            blend
        ).normalized;

        // Pull the body back to surfaceOffset along the (updated) normal only —
        // never toward the hit point — so a forward-leaning probe never drags us
        // forward.
        float currentDistance = Vector3.Dot(ProbeAnchor() - bestHit.point, surfaceNormal);
        float correction = surfaceOffset - currentDistance;

        transform.position +=
            surfaceNormal * correction * Mathf.Clamp01(heightCorrectSpeed * Time.deltaTime);
    }

    private void MoveAlongSurface(Vector2 moveInput)
    {
        // A / D rotate around the current surface normal.
        if (Mathf.Abs(moveInput.x) > 0.01f)
        {
            float yawAmount = moveInput.x * turnSpeed * 40f * Time.deltaTime;
            transform.Rotate(surfaceNormal, yawAmount, Space.World);
        }

        // Forward/backward movement follows our facing flattened onto the
        // surface. Transitions onto other surfaces are handled by StickToSurface
        // (called right after we move), not here.
        Vector3 moveForward = SurfaceForward();
        Vector3 moveDirection = moveForward * moveInput.y;

        SafeMove(moveDirection * moveSpeed * Time.deltaTime);
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
        Vector3 targetUp;

        // Hold the surface orientation while grounded, and for a short coyote
        // window after losing grip, so brief probe misses (common on curved
        // trunks or while crossing onto an inverted surface) don't make the
        // squirrel snap back to its neutral upright pose.
        if (grounded || Time.time - lastGroundedTime < groundCoyoteTime)
        {
            targetUp = surfaceNormal;
            airAlignUp = surfaceNormal;
        }
        else
        {
            // Genuinely airborne: ease back toward world up instead of
            // requesting an instant 180° flip (the ambiguous flip is what
            // looks like a violent snap when you're upside down).
            airAlignUp = Vector3.Slerp(
                airAlignUp,
                Vector3.up,
                airRightingSpeed * Time.deltaTime
            ).normalized;

            targetUp = airAlignUp;
        }

        // Rotate about the head anchor (not the transform pivot) so the front of
        // the body / camera stays put and the tail swings instead — this hides
        // most of the orientation wobble from the camera.
        Vector3 anchorBefore = ProbeAnchor();

        Quaternion surfaceAlignment =
            Quaternion.FromToRotation(transform.up, targetUp) * transform.rotation;

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            surfaceAlignment,
            normalBlendSpeed * Time.deltaTime
        );

        transform.position += anchorBefore - ProbeAnchor();
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
        Vector3 anchor = ProbeAnchor();

        Gizmos.color = Color.green;
        Gizmos.DrawRay(anchor, surfaceNormal);

        // Head anchor (the single probe point) and the lead probe direction.
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(anchor, sphereProbeRadius);

        Gizmos.color = Color.red;
        Vector3 lead = (-surfaceNormal + SurfaceForward() * forwardProbeBias).normalized;
        Gizmos.DrawRay(anchor, lead * probeDistance);

        Gizmos.color = Color.yellow;
        Vector3 gizmoUp = Application.isPlaying ? (grounded ? surfaceNormal : Vector3.up) : transform.up;

        Gizmos.DrawWireSphere(
            transform.position + gizmoUp * collisionCenterOffset,
            collisionRadius
        );
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