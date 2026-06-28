using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Turns the phone's accelerometer stream (sent over OSC by the Android companion) into a single
/// JUMP gesture: a quick, hard flick of the phone — a fast up motion — produces a strong
/// acceleration spike, and the jump fires the instant that spike crosses <see cref="jumpThreshold"/>.
/// There is no waiting for the motion to settle or return, so the jump feels immediate. A short
/// <see cref="cooldown"/> debounces one flick into one jump (and stops the lift-and-return of a
/// single motion from firing twice).
///
/// Detection is orientation-independent: it keys off the magnitude of (linear) acceleration, not a
/// fixed axis, so it works regardless of how the phone is held. React to the jump by hooking the
/// <see cref="onJump"/> UnityEvent in the Inspector or subscribing to <see cref="OnJumpDetected"/>
/// in code (the <see cref="SquirrelController"/> does the latter).
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

    [Header("Jump -> fast flick")]
    [Tooltip("Acceleration magnitude (m/s^2) a flick must reach to fire a jump. The jump triggers " +
             "the instant this is crossed, so raise it if gentle tilting (used to walk) triggers " +
             "spurious jumps, lower it if real flicks are being missed.")]
    [SerializeField] private float jumpThreshold = 8f;

    [Tooltip("Quiet time after a jump before another can fire. Also collapses the lift-and-return of " +
             "a single flick into one jump instead of two.")]
    [SerializeField] private float cooldown = 0.35f;

    [Header("Events")]
    public UnityEvent onJump;

    /// <summary>Code-level hook, equivalent to the <see cref="onJump"/> UnityEvent above.</summary>
    public event Action OnJumpDetected;

    [Header("Debug")]
    [Tooltip("Log every detected jump to the Console.")]
    [SerializeField] private bool logDetections = true;

    private float lastJumpTime = -999f;

    // High-pass state for the /accel fallback (rough running estimate of gravity).
    private Vector3 gravityEstimate;
    private bool hasGravityEstimate;

    private void Reset()
    {
        oscReceiver = FindAnyObjectByType<OSCReceiver>();
    }

    private void OnEnable()
    {
        if (oscReceiver == null)
        {
            Debug.LogWarning($"{nameof(PhoneGestureDetector)}: no {nameof(OSCReceiver)} assigned; " +
                             "no jumps will be detected.", this);
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

    // Called once per incoming acceleration sample (main thread, via uOSC). Fires the jump on the
    // rising edge of a strong spike — the first sample over the threshold — so there's no settle
    // delay. The cooldown then suppresses the rest of that flick (including its return swing).
    private void HandleAcceleration(Vector3 raw)
    {
        Vector3 a = source == AccelerationSource.Accelerometer ? HighPass(raw) : raw;
        float now = Time.time;
        float mag = a.magnitude;

        if (mag >= jumpThreshold && now - lastJumpTime >= cooldown)
        {
            lastJumpTime = now;

            if (logDetections)
                Debug.Log($"[PhoneGesture] JUMP (peak={mag:F1})", this);

            onJump?.Invoke();
            OnJumpDetected?.Invoke();
        }
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
}
