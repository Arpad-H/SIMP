using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Turns the phone's accelerometer stream (sent over OSC by the Android companion) into two
/// distinct gestures and keeps them separated:
///
///   * JUMP  – the phone is quickly lifted and brought back to neutral. In acceleration terms this
///             is a single strong impulse with at most a couple of direction reversals (up, then a
///             return). Fires once the motion settles.
///
///   * SHAKE – the phone is actively shaken back and forth. This is a burst of rapid direction
///             reversals. Fires as soon as enough reversals accumulate, so it always wins over the
///             jump when the motion is oscillatory.
///
/// The discriminator is the number of direction reversals within one continuous motion:
///   reversals &gt;= <see cref="shakeMinReversals"/>            -> shake  (AttemptToEatNut)
///   strong peak and reversals &lt;= <see cref="jumpMaxReversals"/> -> jump
///
/// Detection is orientation-independent: it uses the magnitude of acceleration and the angle
/// between successive "swings", not a fixed axis, so it works regardless of how the phone is held.
///
/// Both <see cref="Jump"/> and <see cref="AttemptToEatNut"/> are intentionally blank stubs for now;
/// wire them up (or hook the <see cref="onJump"/> / <see cref="onEatNut"/> UnityEvents in the
/// Inspector) once the detection feels right.
/// </summary>
public class PhoneGestureDetector : MonoBehaviour
{
    public enum AccelerationSource
    {
        /// <summary>/linaccel – gravity already removed on the phone. Preferred.</summary>
        LinearAcceleration,
        /// <summary>/accel – includes gravity, which is high-pass filtered out here.</summary>
        Accelerometer
    }

    [Header("References")]
    [SerializeField] private OSCReceiver oscReceiver;

    [Tooltip("Which OSC stream to read. LinearAcceleration (/linaccel) already has gravity removed " +
             "and is the recommended choice. Accelerometer (/accel) includes gravity, which is " +
             "filtered out with a high-pass filter here.")]
    [SerializeField] private AccelerationSource source = AccelerationSource.LinearAcceleration;

    [Header("Motion gating (m/s^2)")]
    [Tooltip("A gesture begins when the acceleration magnitude rises above this.")]
    [SerializeField] private float motionEnterThreshold = 4.0f;

    [Tooltip("A gesture is considered finished once the magnitude drops back below this.")]
    [SerializeField] private float motionExitThreshold = 2.0f;

    [Tooltip("Samples weaker than this are treated as noise and ignored when tracking direction.")]
    [SerializeField] private float directionNoiseFloor = 2.0f;

    [Header("Shake -> AttemptToEatNut")]
    [Tooltip("Number of rapid direction reversals within one motion that count as a shake.")]
    [SerializeField] private int shakeMinReversals = 3;

    [Tooltip("Two successive swings count as a reversal when their directions differ by at least " +
             "this many degrees.")]
    [Range(90f, 180f)]
    [SerializeField] private float reversalAngle = 100f;

    [Header("Continuous shake (held actions, e.g. eating a nut)")]
    [Tooltip("Once this many reversals occur close together, IsShaking turns on and stays on while " +
             "the shaking continues. Unlike the one-shot shake above, this drives held actions.")]
    [SerializeField] private int continuousShakeReversals = 2;

    [Tooltip("Each reversal keeps IsShaking alive for this long. If no new reversal arrives within " +
             "this window, the shaking is considered to have stopped. Wider = more forgiving of slow/" +
             "gentle shakes (fewer drop-outs), at the cost of taking slightly longer to release.")]
    [SerializeField] private float continuousShakeWindow = 0.6f;

    [Header("Jump -> lift & return")]
    [Tooltip("Peak magnitude the lift must reach to qualify as a jump (enforces 'quick' lift).")]
    [SerializeField] private float jumpMinPeak = 6.0f;

    [Tooltip("A jump may contain at most this many reversals. A clean lift-and-return has ~1.")]
    [SerializeField] private int jumpMaxReversals = 2;

    [Tooltip("Longest a single motion may last before it is discarded as neither jump nor shake.")]
    [SerializeField] private float jumpMaxDuration = 0.8f;

    [Tooltip("The motion must stay below the exit threshold this long before a jump is confirmed. " +
             "Long enough to bridge the brief zero-crossings of a shake, short enough to feel snappy.")]
    [SerializeField] private float settleTime = 0.12f;

    [Header("Debounce")]
    [Tooltip("Minimum quiet time after a gesture before another can fire.")]
    [SerializeField] private float cooldown = 0.35f;

    [Tooltip("Abandon an in-progress gesture if no samples arrive for this long (stream lost).")]
    [SerializeField] private float streamTimeout = 0.3f;

    [Header("Events (wire later)")]
    public UnityEvent onJump;
    public UnityEvent onEatNut;

    /// <summary>Code-level hooks, equivalent to the UnityEvents above.</summary>
    public event Action OnJumpDetected;
    public event Action OnEatNutDetected;

    /// <summary>
    /// True while the phone is being actively shaken back and forth. Unlike the one-shot
    /// <see cref="onEatNut"/> event, this stays true for the whole duration of the shaking, so it
    /// can gate held actions such as filling a peanut's eat-progress bar over several seconds.
    /// </summary>
    public bool IsShaking { get; private set; }

    [Header("Debug")]
    [Tooltip("Log every detected gesture to the Console so you can verify separation.")]
    [SerializeField] private bool logDetections = true;

    private enum State { Idle, Active, Cooldown }
    private State state = State.Idle;

    // Direction of the current "swing" of motion, used to detect reversals.
    private Vector3 swingDir = Vector3.zero;
    private bool hasSwingDir;

    // Per-gesture accumulators.
    private float gestureStartTime;
    private float peakMagnitude;
    private int gestureReversals;
    private float belowExitSince = -1f;

    private float lastActionTime = -999f;
    private float lastSampleTime;

    // Continuous-shake signal: counts reversals that arrive close together, independent of the
    // one-shot jump/shake state machine above.
    private float lastReversalTime = -999f;
    private int sustainedReversals;

    // High-pass state for the /accel fallback (rough running estimate of gravity).
    private Vector3 gravityEstimate;
    private bool hasGravityEstimate;

    private float reversalCosThreshold;

    private void Reset()
    {
        oscReceiver = FindAnyObjectByType<OSCReceiver>();
    }

    private void OnEnable()
    {
        reversalCosThreshold = Mathf.Cos(reversalAngle * Mathf.Deg2Rad);

        if (oscReceiver == null)
        {
            Debug.LogWarning($"{nameof(PhoneGestureDetector)}: no {nameof(OSCReceiver)} assigned; " +
                             "no gestures will be detected.", this);
            return;
        }

        if (source == AccelerationSource.LinearAcceleration)
            oscReceiver.OnLinearAcceleration += HandleAcceleration;
        else
            oscReceiver.OnAcceleration += HandleAcceleration;
    }

    private void OnDisable()
    {
        if (oscReceiver == null)
            return;

        oscReceiver.OnLinearAcceleration -= HandleAcceleration;
        oscReceiver.OnAcceleration -= HandleAcceleration;
    }

    private void Update()
    {
        // Safety net: if the sensor stream stalls mid-gesture, don't get stuck in Active.
        if (state == State.Active && Time.time - lastSampleTime > streamTimeout)
            ResetGesture(State.Idle);

        // The continuous shake turns off once reversals stop arriving.
        if (IsShaking && Time.time - lastReversalTime > continuousShakeWindow)
        {
            IsShaking = false;
            sustainedReversals = 0;
        }
    }

    // Called once per incoming acceleration sample (main thread, via uOSC).
    private void HandleAcceleration(Vector3 raw)
    {
        Vector3 a = source == AccelerationSource.Accelerometer ? HighPass(raw) : raw;
        float now = Time.time;
        float mag = a.magnitude;
        lastSampleTime = now;

        TrackReversal(a, mag);

        switch (state)
        {
            case State.Idle:
                if (mag >= motionEnterThreshold)
                {
                    state = State.Active;
                    gestureStartTime = now;
                    peakMagnitude = mag;
                    gestureReversals = 0;
                    belowExitSince = -1f;
                }
                break;

            case State.Active:
                peakMagnitude = Mathf.Max(peakMagnitude, mag);

                // Oscillatory motion: fire the shake as soon as it is unambiguous.
                if (gestureReversals >= shakeMinReversals)
                {
                    Fire(isShake: true, now);
                    break;
                }

                // Has the motion settled back to neutral?
                if (mag < motionExitThreshold)
                {
                    if (belowExitSince < 0f)
                        belowExitSince = now;

                    if (now - belowExitSince >= settleTime)
                    {
                        bool isJump = peakMagnitude >= jumpMinPeak &&
                                      gestureReversals <= jumpMaxReversals;

                        if (isJump)
                            Fire(isShake: false, now);
                        else
                            ResetGesture(State.Cooldown); // too weak / ambiguous: discard
                    }
                }
                else
                {
                    belowExitSince = -1f;
                }

                // Sustained motion that never became a shake and never settled: discard.
                if (state == State.Active && now - gestureStartTime > jumpMaxDuration)
                    ResetGesture(State.Cooldown);
                break;

            case State.Cooldown:
                if (now - lastActionTime >= cooldown && mag < motionExitThreshold)
                    state = State.Idle;
                break;
        }
    }

    // Updates the current swing direction and counts a reversal when the direction flips sharply.
    private void TrackReversal(Vector3 a, float mag)
    {
        if (mag < directionNoiseFloor)
            return;

        Vector3 dir = a / mag;

        if (!hasSwingDir)
        {
            swingDir = dir;
            hasSwingDir = true;
            return;
        }

        float dot = Vector3.Dot(dir, swingDir);

        if (dot <= reversalCosThreshold)
        {
            // Direction flipped past the reversal angle -> a new swing.
            swingDir = dir;
            if (state == State.Active)
                gestureReversals++;

            // Feed the continuous-shake signal too. This runs regardless of state (even during
            // cooldown), so a sustained shake keeps IsShaking alive between one-shot fires.
            RegisterReversalForShakeSignal();
        }
        else if (dot > 0.5f)
        {
            // Still heading roughly the same way: let the swing direction follow the motion.
            swingDir = dir;
        }
        // Otherwise (roughly orthogonal): keep the current swing direction, don't count it.
    }

    // Reversals that arrive within continuousShakeWindow of each other accumulate; once enough
    // pile up the phone is considered to be shaking, and it stays that way until the reversals stop
    // (handled in Update). Kept separate from the one-shot gestureReversals so the two never fight.
    private void RegisterReversalForShakeSignal()
    {
        float now = Time.time;

        sustainedReversals = (now - lastReversalTime <= continuousShakeWindow)
            ? sustainedReversals + 1
            : 1;

        lastReversalTime = now;

        if (sustainedReversals >= continuousShakeReversals)
            IsShaking = true;
    }

    private void Fire(bool isShake, float now)
    {
        if (isShake)
        {
            if (logDetections)
                Debug.Log($"[PhoneGesture] SHAKE  (reversals={gestureReversals}, peak={peakMagnitude:F1})", this);

            AttemptToEatNut();
            onEatNut?.Invoke();
            OnEatNutDetected?.Invoke();
        }
        else
        {
            if (logDetections)
                Debug.Log($"[PhoneGesture] JUMP   (reversals={gestureReversals}, peak={peakMagnitude:F1})", this);

            Jump();
            onJump?.Invoke();
            OnJumpDetected?.Invoke();
        }

        ResetGesture(State.Cooldown);
    }

    private void ResetGesture(State next)
    {
        state = next;
        lastActionTime = Time.time;
        gestureReversals = 0;
        peakMagnitude = 0f;
        belowExitSince = -1f;
        hasSwingDir = false;
    }

    // Rough gravity removal for the /accel fallback: a slow low-pass estimates gravity, which is
    // then subtracted to leave the fast linear-acceleration component.
    private Vector3 HighPass(Vector3 raw)
    {
        const float alpha = 0.9f; // higher = slower gravity tracking
        if (!hasGravityEstimate)
        {
            gravityEstimate = raw;
            hasGravityEstimate = true;
        }
        gravityEstimate = alpha * gravityEstimate + (1f - alpha) * raw;
        return raw - gravityEstimate;
    }

    // ---------------------------------------------------------------------------------------------
    // Action stubs – blank for now, wire up later.
    // ---------------------------------------------------------------------------------------------

    /// <summary>Triggered by a quick lift-and-return of the phone.</summary>
    public void Jump()
    {
        // TODO: wire up the actual jump.
    }

    /// <summary>Triggered by actively shaking the phone.</summary>
    public void AttemptToEatNut()
    {
        // TODO: wire up the actual eat-nut behaviour.
    }
}
