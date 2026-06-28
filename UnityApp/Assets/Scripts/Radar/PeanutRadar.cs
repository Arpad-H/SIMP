using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A top-down "peanut radar" for the squirrel, modelled on an aircraft PPI (plan-position
/// indicator): a sweep line spins around the centre at a configurable rate, and each time it
/// passes over the bearing of a nearby <see cref="Peanut"/> that nut lights up as a blip. The
/// blip then fades out over a configurable linger time, so a nut only shows for a while after
/// each pass — like the fading phosphor trail on a real radar.
///
/// This component is the <b>logic</b> only. It owns the sweep, finds the peanuts, works out
/// where each one sits in radar space (a normalised −1..1 square centred on the squirrel) and
/// how bright its blip is right now. It draws nothing on the HUD itself: a renderer reads
/// <see cref="Blips"/> and <see cref="SweepAngle"/> to draw the dial. To validate the behaviour
/// with no UI at all, just select this object and watch the scene-view gizmos.
///
/// The two headline knobs are <see cref="sweepPeriod"/> (how long one full revolution takes)
/// and <see cref="blipLinger"/> (how long a blip stays lit after the sweep hits it). Both can
/// also be changed at runtime via <see cref="SweepPeriod"/> / <see cref="BlipLinger"/>.
/// </summary>
public class PeanutRadar : MonoBehaviour
{
    public enum RadarOrientation
    {
        // "Up" on the radar is the direction the heading reference faces, so blips swing around
        // as you turn — like a car sat-nav / game minimap. Best for "is a nut ahead of me?".
        HeadingUp,
        // "Up" on the radar is locked to world +Z. The dial never rotates with you.
        NorthUp
    }

    [Header("Sweep")]
    [Tooltip("Seconds for the sweep line to make one full 360° revolution. Lower = faster spin.")]
    [Min(0.01f)]
    [SerializeField] private float sweepPeriod = 4f;

    [Tooltip("Sweep direction. Real radars sweep clockwise; turn this off for a counter-clockwise dial.")]
    [SerializeField] private bool clockwise = true;

    [Header("Blip Lifetime")]
    [Tooltip("Seconds a blip stays visible after the sweep passes over it, fading from full bright " +
             "to nothing. This is the 'how long the dots light up' time.")]
    [Min(0f)]
    [SerializeField] private float blipLinger = 2f;

    [Tooltip("Shape of the fade from 1 (just hit) to 0 (gone) across the linger time. Linear by " +
             "default; ease it out for a soft phosphor glow that lingers then drops away quickly.")]
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Header("Range")]
    [Tooltip("World-space radius (metres) the radar covers. A nut this far away sits on the rim; " +
             "the squirrel is the centre.")]
    [Min(0.01f)]
    [SerializeField] private float radarRange = 30f;

    [Tooltip("On: nuts beyond the range are pinned to the rim so you still get a bearing. " +
             "Off: they're ignored until you're close enough.")]
    [SerializeField] private bool clampOutOfRangeToRim = true;

    [Header("References")]
    [Tooltip("Centre of the radar — usually the squirrel. Auto-found from the SquirrelController if empty.")]
    [SerializeField] private Transform center;

    [Tooltip("Defines 'up' on the radar when Orientation is Heading Up — usually the camera or the " +
             "squirrel body. Falls back to the centre, then Camera.main, if left empty.")]
    [SerializeField] private Transform headingReference;

    [SerializeField] private RadarOrientation orientation = RadarOrientation.HeadingUp;

    [Header("Peanut Tracking")]
    [Tooltip("How often (seconds) to rescan the scene for peanuts. They spawn and get eaten at " +
             "runtime, so the radar refreshes its list instead of caching once. 0 = every frame.")]
    [Min(0f)]
    [SerializeField] private float rescanInterval = 0.5f;

    [Header("Events")]
    [Tooltip("Fired every time the sweep passes over a peanut (a 'ping'). Hook a blip SFX here.")]
    public UnityEvent onPing;

    /// <summary>One peanut as the radar sees it this frame.</summary>
    public readonly struct Blip
    {
        /// <summary>Radar-space position: (0,0) centre, +x right, +y up, each component in −1..1.</summary>
        public readonly Vector2 position;
        /// <summary>Brightness: 1 right after a sweep pass, fading to 0 over the linger time.</summary>
        public readonly float intensity;
        /// <summary>Distance from the squirrel as a fraction of the range (0 centre, 1 rim, &gt;1 off-radar).</summary>
        public readonly float distance01;
        /// <summary>The peanut this blip represents (may be null the frame after it's eaten).</summary>
        public readonly Peanut peanut;

        public Blip(Vector2 position, float intensity, float distance01, Peanut peanut)
        {
            this.position = position;
            this.intensity = intensity;
            this.distance01 = distance01;
            this.peanut = peanut;
        }
    }

    /// <summary>Current sweep heading in degrees: 0 = up, increasing in the sweep direction.</summary>
    public float SweepAngle => sweepAngle;

    /// <summary>The blips a renderer should draw this frame (only those currently lit).</summary>
    public IReadOnlyList<Blip> Blips => activeBlips;

    /// <summary>Fired (in code) each time the sweep pings a peanut, carrying the freshly-lit blip.</summary>
    public event Action<Blip> Pinged;

    /// <summary>Seconds per full revolution. Settable at runtime (e.g. a "radar boost" power-up).</summary>
    public float SweepPeriod { get => sweepPeriod; set => sweepPeriod = Mathf.Max(0.01f, value); }

    /// <summary>Seconds a blip stays lit after a pass. Settable at runtime.</summary>
    public float BlipLinger { get => blipLinger; set => blipLinger = Mathf.Max(0f, value); }

    /// <summary>Radar coverage radius in metres. Settable at runtime.</summary>
    public float Range { get => radarRange; set => radarRange = Mathf.Max(0.01f, value); }

    // When the sweep last passed over each tracked peanut. Drives the blip fade between frames.
    private readonly Dictionary<Peanut, float> lastPingTime = new();
    // The peanuts found by the most recent rescan. Iterated each frame (cheap, no allocation).
    private readonly List<Peanut> tracked = new();
    // Scratch list so removing stale entries doesn't mutate the dictionary mid-enumeration.
    private readonly List<Peanut> staleKeys = new();
    // Rebuilt each frame and handed to renderers via Blips — reused so drawing doesn't allocate.
    private readonly List<Blip> activeBlips = new();

    private float sweepAngle;        // normalised 0..360, current frame
    private float prevSweepAngle;    // normalised 0..360, previous frame (for the crossing test)
    private float lastSweptArc;      // degrees the sweep advanced this frame
    private float nextRescanTime;

    private void Reset()
    {
        var squirrel = FindAnyObjectByType<SquirrelController>();
        if (squirrel != null) center = squirrel.transform;
        if (Camera.main != null) headingReference = Camera.main.transform;
    }

    private void Awake()
    {
        if (center == null)
        {
            var squirrel = FindAnyObjectByType<SquirrelController>();
            if (squirrel != null) center = squirrel.transform;
        }

        if (center == null)
            Debug.LogWarning($"{nameof(PeanutRadar)} '{name}': no centre transform and no " +
                             $"{nameof(SquirrelController)} in the scene — the radar has nothing to " +
                             "centre on. Assign 'center' in the inspector.", this);

        Rescan();
    }

    private void Update()
    {
        AdvanceSweep();

        if (rescanInterval <= 0f || Time.time >= nextRescanTime)
            Rescan();

        BuildBlips();
    }

    // Spin the sweep line, remembering where it started so BuildBlips can tell which peanuts it
    // crossed in between. Capped at a full turn so a huge dt (or a tiny period) can't skip nuts.
    private void AdvanceSweep()
    {
        prevSweepAngle = sweepAngle;

        float degPerSec = 360f / Mathf.Max(sweepPeriod, 0.0001f);
        float delta = degPerSec * Time.deltaTime * (clockwise ? 1f : -1f);

        lastSweptArc = Mathf.Min(Mathf.Abs(delta), 360f);
        sweepAngle = Mod(sweepAngle + delta, 360f);
    }

    // Refresh the tracked-peanut set from the scene: add newcomers, drop the eaten/destroyed.
    private void Rescan()
    {
        nextRescanTime = Time.time + rescanInterval;

        tracked.Clear();
        // FindObjectsByType is the runtime-friendly successor to FindObjectsOfType; order doesn't
        // matter to us, so skip the sort for speed.
        tracked.AddRange(FindObjectsByType<Peanut>(FindObjectsSortMode.None));

        // Forget anything we were tracking that's gone now (Destroy nulls the reference; eaten
        // nuts are about to be destroyed). Collect first, then remove — never mutate mid-enumerate.
        staleKeys.Clear();
        foreach (var p in lastPingTime.Keys)
            if (p == null || p.IsEaten || !tracked.Contains(p))
                staleKeys.Add(p);
        foreach (var p in staleKeys)
            lastPingTime.Remove(p);

        // Register newcomers as "pinged in the distant past" so they stay dark until the sweep
        // actually reaches them, rather than flashing on the moment they're discovered.
        foreach (var p in tracked)
            if (p != null && !lastPingTime.ContainsKey(p))
                lastPingTime[p] = float.NegativeInfinity;
    }

    // Place every tracked peanut on the dial, ping the ones the sweep crossed this frame, and
    // collect those still lit into activeBlips for a renderer to draw.
    private void BuildBlips()
    {
        activeBlips.Clear();
        if (center == null) return;

        Vector3 origin = center.position;
        GetRadarBasis(out Vector3 radarRight, out Vector3 radarForward);

        // Iterate the cached list (not the dictionary) so we can safely write ping times back.
        foreach (Peanut peanut in tracked)
        {
            if (peanut == null) continue;

            Vector3 offset = peanut.transform.position - origin;

            // Flatten onto the horizontal plane: a PPI radar reads bearing + ground distance, not
            // altitude. (The squirrel climbs in 3D, but the dial is top-down.)
            float x = Vector3.Dot(offset, radarRight);
            float y = Vector3.Dot(offset, radarForward);

            float dist = Mathf.Sqrt(x * x + y * y);
            float dist01 = dist / radarRange;

            Vector2 pos = new Vector2(x, y) / radarRange;
            if (dist01 > 1f)
            {
                if (!clampOutOfRangeToRim) continue;       // out of range and hidden — skip entirely
                pos = pos.normalized;                       // otherwise pin the blip to the rim
            }

            // Bearing of the nut, 0 = up and increasing clockwise (Atan2(x, y) measures from +y
            // toward +x). Did the sweep pass it during this frame's arc?
            float bearing = Mod(Mathf.Atan2(x, y) * Mathf.Rad2Deg, 360f);
            float fromPrev = clockwise
                ? Mod(bearing - prevSweepAngle, 360f)
                : Mod(prevSweepAngle - bearing, 360f);
            bool crossed = lastSweptArc > 0f && fromPrev > 0f && fromPrev <= lastSweptArc;

            if (crossed)
                lastPingTime[peanut] = Time.time;

            float intensity = BlipIntensity(crossed, lastPingTime[peanut]);
            if (intensity <= 0f) continue;                  // fully faded — nothing to draw

            var blip = new Blip(pos, intensity, dist01, peanut);
            activeBlips.Add(blip);

            if (crossed)
            {
                onPing?.Invoke();
                Pinged?.Invoke(blip);
            }
        }
    }

    // Brightness of a blip given when it was last hit. 1 on the hit frame, then follows the fade
    // curve across the linger time down to 0.
    private float BlipIntensity(bool crossed, float pingedAt)
    {
        if (crossed) return 1f;
        if (blipLinger <= 0f) return 0f;            // instantaneous mode: only lit on the hit frame

        float t = (Time.time - pingedAt) / blipLinger;
        if (t >= 1f || t < 0f) return 0f;
        return Mathf.Clamp01(fadeCurve.Evaluate(t));
    }

    // The radar's right/forward axes in world space, flattened to the horizontal plane. "Forward"
    // is what points up on the dial.
    private void GetRadarBasis(out Vector3 right, out Vector3 forward)
    {
        Vector3 fwd;
        if (orientation == RadarOrientation.NorthUp)
        {
            fwd = Vector3.forward;
        }
        else
        {
            Transform refT = headingReference != null ? headingReference
                           : center != null ? center
                           : Camera.main != null ? Camera.main.transform : null;
            fwd = refT != null ? refT.forward : Vector3.forward;
        }

        fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward; // looking straight up/down — fall back
        fwd.Normalize();

        forward = fwd;
        right = Vector3.Cross(Vector3.up, fwd); // Unity (left-handed): up × forward = right
    }

    // Positive modulo, so results land in [0, m) even for negative inputs.
    private static float Mod(float a, float m) => a - m * Mathf.Floor(a / m);

    // ---- Scene-view visualisation: a flat radar disc around the squirrel, so you can see the
    // logic working (sweep + blips) before any HUD exists. ----
    private void OnDrawGizmos()
    {
        if (center == null) return;

        Vector3 origin = center.position;
        GetRadarBasis(out Vector3 right, out Vector3 forward);

        // Range ring.
        Gizmos.color = new Color(0.3f, 0.8f, 0.3f, 0.6f);
        DrawGizmoCircle(origin, right, forward, radarRange, 64);

        // Sweep line (only meaningful in play mode, but harmless at rest).
        float rad = sweepAngle * Mathf.Deg2Rad;
        Vector3 sweepDir = forward * Mathf.Cos(rad) + right * Mathf.Sin(rad);
        Gizmos.color = new Color(0.5f, 1f, 0.5f, 0.9f);
        Gizmos.DrawLine(origin, origin + sweepDir * radarRange);

        // Blips, brightness shown via alpha + size.
        foreach (Blip blip in activeBlips)
        {
            Vector3 wp = origin + (right * blip.position.x + forward * blip.position.y) * radarRange;
            Gizmos.color = new Color(0.6f, 1f, 0.4f, blip.intensity);
            Gizmos.DrawSphere(wp, 0.3f + 0.6f * blip.intensity);
        }
    }

    private static void DrawGizmoCircle(Vector3 c, Vector3 right, Vector3 forward, float radius, int segments)
    {
        Vector3 prev = c + forward * radius;
        for (int i = 1; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            Vector3 p = c + (forward * Mathf.Cos(a) + right * Mathf.Sin(a)) * radius;
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }
}
