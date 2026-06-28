using UnityEngine;

/// <summary>
/// Lets the lumberjack (the VR player) pick up fallen nuts by touching them. Put this on each hand /
/// controller — or on any moving object that should collect nuts on the player's behalf.
///
/// Each frame it checks for a <see cref="Peanut"/> that has fallen to the ground and is touching this
/// hand's pickup bubble, and collects it for the lumberjack's score (see <see cref="Peanut.Collect"/>).
/// It works by proximity rather than physics trigger callbacks, so it doesn't matter how the VR
/// hand's colliders / rigidbodies are set up — the hand needs nothing but its Transform. The matching
/// <see cref="GameManager"/> does the scoring.
///
/// Reach = <see cref="pickupRadius"/> around this object's pivot, plus the nut's own small collider.
/// Select the hand in the Scene view to see the bubble; a VR controller's pivot usually sits back
/// from where you physically reach, so enlarge it if pickups feel unreliable.
/// </summary>
public class LumberjackHand : MonoBehaviour
{
    [Tooltip("Radius (metres) of the pickup bubble around this hand's pivot. Shown as a gizmo when " +
             "the object is selected. Enlarge it if your controller's pivot sits back from the nut.")]
    [SerializeField] private float pickupRadius = 0.18f;

    [Tooltip("Restrict the check to the layer(s) nuts live on. Leave as Everything if unsure.")]
    [SerializeField] private LayerMask nutLayers = ~0;

    [Tooltip("Log what the hand sees to the Console — leave on while setting up, then turn it off.")]
    [SerializeField] private bool debugLog = true;

    // Reused across frames so the per-frame proximity check allocates nothing.
    private static readonly Collider[] overlaps = new Collider[16];

    private void OnEnable()
    {
        // If you never see this line when entering play mode, the hand object is inactive (e.g. a
        // disabled interactor) or the component is disabled — that's why nothing gets picked up.
        if (debugLog)
            Debug.Log($"[LumberjackHand] active on '{name}', pickupRadius {pickupRadius} m.", this);
    }

    private void Update()
    {
        // QueryTriggerInteraction.Collide so the nut's trigger collider (its eat range) is included.
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, pickupRadius, overlaps, nutLayers, QueryTriggerInteraction.Collide);

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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}
