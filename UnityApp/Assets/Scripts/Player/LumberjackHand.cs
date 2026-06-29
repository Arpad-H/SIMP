using UnityEngine;

/// <summary>
/// Lets the lumberjack (the VR player) pick up fallen nuts by touching them.
///
/// Each frame it scans a small bubble around one or more "reach points" and, when a <see cref="Peanut"/>
/// that has fallen to the ground is inside it, collects it for the lumberjack's score
/// (see <see cref="Peanut.Collect"/>). It works by proximity, NOT physics trigger callbacks, so it
/// doesn't matter how the VR hand's colliders / rigidbodies are set up — but the reach point MUST be a
/// transform that actually follows the physical hand. The matching <see cref="GameManager"/> scores it.
///
/// Where to put it / what to reach from:
///  • Simplest: put this component directly on each tracked hand / controller anchor and leave
///    <see cref="reachPoints"/> empty — it then reaches from its own position.
///  • If it sits on a PARENT that doesn't move with the controllers (e.g. a container of model
///    variants), assign the tracked hand/controller anchor transforms to <see cref="reachPoints"/>.
/// Select the object to see the bubble(s) as gizmos.
/// </summary>
public class LumberjackHand : MonoBehaviour
{
    [Tooltip("Transforms that actually reach for nuts — the tracked hand / controller anchors. Leave " +
             "EMPTY to reach from this object's own position. Assign the real tracked transforms here " +
             "if this script sits on a parent that does not move with the controllers.")]
    [SerializeField] private Transform[] reachPoints;

    [Tooltip("Radius (metres) of the pickup bubble around each reach point. Shown as a gizmo when the " +
             "object is selected. Enlarge it if the controller's pivot sits back from the nut.")]
    [SerializeField] private float pickupRadius = 0.18f;

    [Tooltip("Restrict the check to the layer(s) nuts live on. Leave as Everything if unsure.")]
    [SerializeField] private LayerMask nutLayers = ~0;

    [Tooltip("Log what the hand sees to the Console (incl. a once-a-second heartbeat) — leave on while " +
             "setting up, then turn it off.")]
    [SerializeField] private bool debugLog = true;

    // Reused across frames so the per-frame proximity check allocates nothing.
    private static readonly Collider[] overlaps = new Collider[16];
    private float nextHeartbeat;

    private void OnEnable()
    {
        // If you never see this line when entering play mode, the object is inactive (e.g. a disabled
        // interactor) or the component is unchecked — that's why nothing gets picked up.
        if (debugLog)
            Debug.Log($"[LumberjackHand] active on '{name}' " +
                      $"({(HasReachPoints ? reachPoints.Length + " reach point(s)" : "own transform")}, " +
                      $"radius {pickupRadius} m).", this);
    }

    private bool HasReachPoints => reachPoints != null && reachPoints.Length > 0;

    private void Update()
    {
        if (HasReachPoints)
        {
            foreach (Transform p in reachPoints)
                if (p != null) ScanAt(p.position);
        }
        else
        {
            ScanAt(transform.position);
        }

        if (debugLog && Time.time >= nextHeartbeat)
        {
            nextHeartbeat = Time.time + 1f;
            LogHeartbeat();
        }
    }

    // Collect any fallen nut whose collider this bubble overlaps.
    private void ScanAt(Vector3 worldPos)
    {
        // QueryTriggerInteraction.Collide so the nut's trigger collider (its eat range) is included.
        int count = Physics.OverlapSphereNonAlloc(
            worldPos, pickupRadius, overlaps, nutLayers, QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Collider col = overlaps[i];
            if (col == null) continue;

            Peanut nut = col.GetComponentInParent<Peanut>();
            if (nut == null || nut.IsCollected || nut.IsEaten)
                continue;

            // A nut is only collectible once it has fallen off its tree (the tree was sliced). If you
            // can touch a nut but it won't collect, this is almost always why — slice the tree first.
            if (!nut.HasDropped)
            {
                if (debugLog)
                    Debug.Log($"[LumberjackHand] touching '{nut.name}', but it hasn't fallen yet — " +
                              "slice its tree so the nut drops, then grab it.", this);
                continue;
            }

            if (debugLog)
                Debug.Log($"[LumberjackHand] collected fallen nut '{nut.name}'.", this);

            nut.Collect();
        }
    }

    // Once-a-second snapshot so you can see whether the scan point tracks your hand, whether any nuts
    // exist, how far the nearest one is (vs pickupRadius) and whether it has fallen yet.
    private void LogHeartbeat()
    {
        Vector3 from = HasReachPoints && reachPoints[0] != null ? reachPoints[0].position : transform.position;

        Peanut[] nuts = FindObjectsByType<Peanut>(FindObjectsSortMode.None);
        if (nuts.Length == 0)
        {
            Debug.Log($"[LumberjackHand] heartbeat: scan from {from}. No Peanut in the scene yet " +
                      "(none spawned, or all gone).", this);
            return;
        }

        Peanut nearest = null;
        float bestSqr = float.MaxValue;
        foreach (Peanut n in nuts)
        {
            if (n == null) continue;
            float d = (n.transform.position - from).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; nearest = n; }
        }

        if (nearest != null)
            Debug.Log($"[LumberjackHand] heartbeat: scan from {from} (radius {pickupRadius} m). " +
                      $"Nearest of {nuts.Length} nut(s) is '{nearest.name}' at {Mathf.Sqrt(bestSqr):0.00} m, " +
                      $"hasDropped={nearest.HasDropped}.", this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
        if (HasReachPoints)
        {
            foreach (Transform p in reachPoints)
                if (p != null) Gizmos.DrawWireSphere(p.position, pickupRadius);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, pickupRadius);
        }
    }
}
