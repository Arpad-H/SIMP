using UnityEngine;
using UnityEngine.InputSystem;

public enum SquirrelState
{
    Walking,
    Gliding
}

public class SquirrelController : MonoBehaviour
{
    // Current locomotion state, derived from the single surface probe. Grounded
    // means we're clinging to a surface (walking); otherwise we're in the air
    // (gliding). Read by SquirrelAnimator to pick the matching sprite set.
    public SquirrelState State => grounded ? SquirrelState.Walking : SquirrelState.Gliding;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private OSCReceiver oscReceiver;
    [Tooltip("Source of the phone jump gesture. Found automatically in the scene if left empty.")]
    [SerializeField] private PhoneGestureDetector gestureDetector;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float turnSpeed = 12f;
    [SerializeField] private float normalBlendSpeed = 12f;

    [Header("Jump")]
    [SerializeField] private float jumpUpSpeed = 9f;
    [SerializeField] private float jumpForwardBoost = 4f;
    [SerializeField] private float gravity = 18f;
    [SerializeField] private float jumpSurfaceIgnoreTime = 0.2f;

    [Header("Gliding / Wingsuit")]
    // While airborne the squirrel flies like a wingsuit / aircraft rather than a
    // walker: tilt (or A/D + W/S) sets a target BANK and PITCH, banking carves a
    // coordinated turn (yaw follows roll), and pitch trades height for speed. The
    // controls are self-centering — hold the phone flat / release the keys and the
    // squirrel eases back to level flight.

    // Maximum bank (roll) angle the controls steer toward, in degrees.
    [SerializeField] private float maxBankAngle = 55f;
    // Maximum nose-up / nose-down (pitch) angle the controls steer toward.
    [SerializeField] private float maxPitchAngle = 45f;
    // How hard the body chases the target attitude (deg/s of correction per deg of
    // error). Higher = crisper, twitchier; lower = heavier, gliding feel.
    [SerializeField] private float attitudeGain = 4f;
    // Caps on how fast the body can roll / pitch, so big stick throws still feel
    // like an aircraft rotating rather than snapping.
    [SerializeField] private float maxRollRate = 200f;
    [SerializeField] private float maxPitchRate = 140f;
    // Coordinated turn: yaw rate = turnCoordination * g * tan(bank) / speed.
    // 1 = physically coordinated; raise for snappier arcade turns.
    [SerializeField] private float turnCoordination = 1f;
    [SerializeField] private float maxTurnRate = 120f;
    // Wing authority: how fast lift can bend the velocity vector toward where the
    // nose points (deg/s). This is what lets you pull out of a dive and what turns
    // a banked nose into a curving flight path.
    [SerializeField] private float liftTurnRate = 160f;
    // Airspeed at which lift reaches full authority; below this control goes mushy
    // (a soft near-stall), above it stays crisp.
    [SerializeField] private float liftFullSpeed = 6f;
    // Quadratic air drag — sets the terminal dive speed (≈ sqrt(gravity / airDrag)).
    [SerializeField] private float airDrag = 0.045f;
    // Lowest forward airspeed we let the glide settle to, so control never dies.
    [SerializeField] private float minGlideSpeed = 3f;
    [SerializeField] private float speedRegain = 2f;
    // Hard cap on overall airspeed.
    [SerializeField] private float maxGlideSpeed = 22f;
    // Forward speed injected when the squirrel runs off an edge into a glide, so it
    // transitions into flight instead of dropping from a standstill.
    [SerializeField] private float glideTakeoffSpeed = 6f;
    // Flip these if a forward tilt climbs instead of dives, or a right tilt banks
    // left — depends on how the phone axes are wired.
    [SerializeField] private bool invertGlidePitch = false;
    [SerializeField] private bool invertGlideRoll = false;

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

    [Header("Collision")]
    // Extra layers that BLOCK movement but are NOT walkable — e.g. invisible fence
    // walls used to pen the squirrel in. These are combined with solidSurfaceMask for
    // collision only; the squirrel never probes, sticks or aligns to them, so it slides
    // along a barrier instead of climbing it. Leave empty for the original behaviour.
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private float collisionRadius = 0.25f;
    [SerializeField] private float skinWidth = 0.03f;
    [SerializeField] private int slideIterations = 3;
    [SerializeField] private float collisionCenterOffset = 0.22f;

    [Header("Phone Input")]
    [SerializeField] private bool usePhoneInput = true;
    [SerializeField] private bool combinePhoneWithKeyboard = true;

    [Header("Sounds")]
    public AudioSource audioSourceOneShot;
    public AudioSource audioSourceLoop;
    public AudioClip jumpSound;
    public AudioClip flySound;
    public AudioClip landSound;
    public AudioClip walkSound;

    private AudioClip currentLoopClip;
    private bool hasAudioState;
    private bool wasGroundedLastFrame;

    private Vector3 surfaceNormal = Vector3.up;
    private Vector3 lastSurfaceNormal = Vector3.up;
    private bool grounded;
    private Vector3 airVelocity;
    private float ignoreSurfaceUntilTime;
    private float lastGroundedTime = -999f;

    private Vector2 phoneMove;
    private bool jumpRequested; // set by the phone jump gesture, consumed next Update

    // Reference-counted movement lock (e.g. held while the squirrel is eating a peanut). Several
    // systems can lock independently; the squirrel can move again only once every lock is released.
    private int moveLockCount;
    public bool MovementLocked => moveLockCount > 0;
    public void AddMovementLock() => moveLockCount++;
    public void RemoveMovementLock() => moveLockCount = Mathf.Max(0, moveLockCount - 1);

    private void Reset()
    {
        cameraTransform = Camera.main != null ? Camera.main.transform : null;
        oscReceiver = FindAnyObjectByType<OSCReceiver>();
        gestureDetector = FindAnyObjectByType<PhoneGestureDetector>();
    }


    private void Update()
    {
        Vector2 moveInput = GetMoveInput();

        bool jumpPressed =
            (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            || jumpRequested;
        jumpRequested = false; // consume it whether or not we actually jumped this frame

        // `grounded` here is the result of the previous frame's stick test.
        bool groundedAtFrameStart = grounded;

        if (grounded && jumpPressed && !MovementLocked)
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
            GlideWingsuit(moveInput);
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

        // Ran off an edge without jumping: carry our momentum into the glide so the
        // wingsuit eases into flight instead of dropping from a dead stop. Jumps
        // already set airVelocity, so the magnitude guard leaves them untouched.
        if (groundedAtFrameStart && !grounded && airVelocity.sqrMagnitude < 0.01f)
        {
            airVelocity = transform.forward * glideTakeoffSpeed;
        }

        AlignToSurface();
        
        //Audio Handling
        UpdateAudio(moveInput);

       

        
    }

    private void UpdateAudio(Vector2 moveInput)
{
    if (audioSourceLoop == null)
        return;

    bool isMovingOnGround = grounded && moveInput.sqrMagnitude > 0.01f;

    if (hasAudioState && !wasGroundedLastFrame && grounded)
    {
        audioSourceOneShot.PlayOneShot(landSound);
    }

    if (isMovingOnGround)
    {
        PlayLoop(walkSound);
    }
    else if (!grounded)
    {
        PlayLoop(flySound);
    }
    else
    {
        StopLoop();
    }

    wasGroundedLastFrame = grounded;
    hasAudioState = true;
}

private void PlayLoop(AudioClip clip)
{
    if (clip == null)
    {
        StopLoop();
        return;
    }

    if (currentLoopClip == clip && audioSourceLoop.isPlaying)
        return;

    audioSourceLoop.Stop();
    audioSourceLoop.clip = clip;
    audioSourceLoop.loop = true;
    audioSourceLoop.Play();

    currentLoopClip = clip;
}

private void StopLoop()
{
    if (audioSourceLoop == null)
        return;

    if (!audioSourceLoop.isPlaying && currentLoopClip == null)
        return;

    audioSourceLoop.Stop();
    audioSourceLoop.loop = false;
    audioSourceLoop.clip = null;

    currentLoopClip = null;
}

    private Vector2 GetMoveInput()
    {
        // Frozen while eating (or any other lock): ignore all movement commands.
        if (MovementLocked)
            return Vector2.zero;

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

        audioSourceOneShot.PlayOneShot(jumpSound);
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
        //audioSource.clip = walkSound;
        //audioSource.loop = true;
        //audioSource.Play();

        //TODO find stop position

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

    // Wingsuit flight. Two coupled halves: the controls set the body's attitude
    // (roll / pitch / yaw like an aircraft), and a small flight model turns that
    // attitude into velocity (lift, gravity, drag). Called every airborne frame.
    private void GlideWingsuit(Vector2 moveInput)
    {
        UpdateFlightAttitude(moveInput);
        UpdateFlightVelocity();

        SafeMove(airVelocity * Time.deltaTime);
    }

    // Roll / pitch are self-centering: input picks a target bank/pitch angle and we
    // steer the body toward it, so releasing the stick eases back to level. Banking
    // drives a coordinated turn — yaw follows roll, exactly like banking a plane.
    private void UpdateFlightAttitude(Vector2 moveInput)
    {
        float rollInput  = invertGlideRoll  ? -moveInput.x : moveInput.x;
        // Default: push forward (W / tilt the phone forward) drops the nose to dive.
        float pitchInput = invertGlidePitch ? moveInput.y : -moveInput.y;

        // Current attitude measured against the world horizon.
        float currentPitch = Mathf.Asin(Mathf.Clamp(transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg; // + = nose up
        float currentBank  = Mathf.Asin(Mathf.Clamp(-transform.right.y, -1f, 1f)) * Mathf.Rad2Deg;   // + = rolled right

        float targetPitch = pitchInput * maxPitchAngle;
        float targetBank  = rollInput * maxBankAngle;

        // Proportional steering toward the target attitude, rate-limited. Note the
        // error is (current - target): in Unity a +rate about the body's right axis
        // pitches the nose DOWN and a +rate about its forward axis banks LEFT, so the
        // rate that reduces the error is the negative of the naive (target - current).
        float pitchRate = Mathf.Clamp((currentPitch - targetPitch) * attitudeGain, -maxPitchRate, maxPitchRate);
        float rollRate  = Mathf.Clamp((currentBank - targetBank) * attitudeGain, -maxRollRate, maxRollRate);

        // Coordinated-turn yaw: a banked wing turns the aircraft (ω = g·tanφ / v).
        float speed = Mathf.Max(airVelocity.magnitude, 1f);
        float yawRate = (gravity * Mathf.Tan(currentBank * Mathf.Deg2Rad) / speed) * Mathf.Rad2Deg * turnCoordination;
        yawRate = Mathf.Clamp(yawRate, -maxTurnRate, maxTurnRate);

        // Heading turns about world up; pitch and roll about the body's own axes.
        transform.rotation =
            Quaternion.AngleAxis(yawRate * Time.deltaTime, Vector3.up) *
            transform.rotation *
            Quaternion.AngleAxis(pitchRate * Time.deltaTime, Vector3.right) *
            Quaternion.AngleAxis(rollRate * Time.deltaTime, Vector3.forward);
    }

    // Turns the body's attitude into motion: gravity always pulls, the wing redirects
    // the velocity toward where the nose points (so you dive to gain speed and pull up
    // to climb), and drag caps the top speed.
    private void UpdateFlightVelocity()
    {
        Vector3 forward = transform.forward;

        // Gravity is always acting.
        airVelocity += Vector3.down * gravity * Time.deltaTime;

        // Lift bends the velocity vector toward the nose. Authority grows with
        // airspeed, so slow flight feels soft (near-stall) and fast flight is crisp.
        float speed = airVelocity.magnitude;
        if (speed > 0.01f)
        {
            float authority = Mathf.Clamp01(speed / Mathf.Max(liftFullSpeed, 0.01f));
            float maxRadians = liftTurnRate * Mathf.Deg2Rad * authority * Time.deltaTime;
            Vector3 steeredDir = Vector3.RotateTowards(airVelocity / speed, forward, maxRadians, 0f);
            airVelocity = steeredDir * speed;
        }

        // Quadratic drag → natural terminal speed.
        airVelocity -= airVelocity * (speed * airDrag * Time.deltaTime);

        // Never let forward airspeed bleed away to nothing, or the controls go dead.
        float forwardSpeed = Vector3.Dot(airVelocity, forward);
        if (forwardSpeed < minGlideSpeed)
        {
            airVelocity += forward * (minGlideSpeed - forwardSpeed) * speedRegain * Time.deltaTime;
        }

        if (airVelocity.sqrMagnitude > maxGlideSpeed * maxGlideSpeed)
        {
            airVelocity = airVelocity.normalized * maxGlideSpeed;
        }
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
            solidSurfaceMask | obstacleMask,
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
        // Hold the surface orientation while grounded, and for a short coyote
        // window after losing grip, so brief probe misses (common on curved
        // trunks or while crossing onto an inverted surface) don't make the
        // squirrel snap back to its neutral upright pose.
        bool holdingSurface = grounded || Time.time - lastGroundedTime < groundCoyoteTime;

        if (!holdingSurface)
        {
            // Genuinely gliding: the wingsuit flight model fully owns roll / pitch /
            // yaw. Don't right toward world up here or we'd cancel every bank and
            // dive. Landing re-aligns from whatever attitude we're in.
            return;
        }

        Vector3 targetUp = surfaceNormal;

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

        if (gestureDetector == null)
            gestureDetector = FindAnyObjectByType<PhoneGestureDetector>();

        if (gestureDetector != null)
            gestureDetector.OnJumpDetected += RequestJump;
    }

    private void OnDisable()
    {
        if (oscReceiver != null)
            oscReceiver.OnMove -= HandlePhoneMove;

        if (gestureDetector != null)
            gestureDetector.OnJumpDetected -= RequestJump;
    }
    private void HandlePhoneMove(Vector2 move)
    {
        phoneMove = move;
    }

    // Phone "lift & return" gesture -> queue a jump, handled in Update exactly like the space key.
    private void RequestJump()
    {
        jumpRequested = true;
    }

}