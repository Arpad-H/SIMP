using UnityEngine;

/// <summary>
/// Watch-style reveal for a World Space canvas worn on a VR controller / hand anchor. Parent this
/// canvas under the wrist (e.g. <c>LeftHandAnchor</c>) so it tracks the hand, then this script fades
/// it in only while the player has raised and turned their wrist to look at the face — exactly like
/// glancing at a watch — and fades it back out otherwise, so it never floats distractingly while the
/// hands are busy.
///
/// "Looking at the face" is judged purely from the canvas's own orientation: the front of a Unity
/// canvas faces +Z, so when that +Z points back toward the head the player is looking at it. No head
/// gaze test is needed because the watch is always close to the eyes when raised.
///
/// Requires a <see cref="CanvasGroup"/> (added automatically) to drive the fade. Set
/// <see cref="revealOnGlance"/> = false to keep it always visible instead.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class WristUI : MonoBehaviour
{
    [Tooltip("The headset transform to face-check against — the rig's CenterEyeAnchor. Falls back to " +
             "Camera.main if left empty.")]
    [SerializeField] private Transform head;

    [Tooltip("Fade the watch in only while it's turned toward the face. Turn OFF to keep it always on.")]
    [SerializeField] private bool revealOnGlance = true;

    [Tooltip("How directly the watch face must point at the head before it shows, in degrees. Larger = " +
             "easier to trigger (shows from a glancier angle).")]
    [Range(10f, 90f)]
    [SerializeField] private float revealAngle = 55f;

    [Tooltip("Fade speed. Higher snaps in/out faster; lower is gentler.")]
    [SerializeField] private float fadeSpeed = 10f;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        if (head == null && Camera.main != null)
            head = Camera.main.transform;

        // Start hidden when it has to be earned by a glance, visible otherwise.
        canvasGroup.alpha = revealOnGlance ? 0f : 1f;
    }

    private void Update()
    {
        float target = ShouldShow() ? 1f : 0f;
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, fadeSpeed * Time.deltaTime);
    }

    private bool ShouldShow()
    {
        if (!revealOnGlance)
            return true;
        if (head == null)
            return true; // no reference to test against — fail open rather than hide forever

        // Does the canvas front (+Z) point back toward the head? cos(angle) >= cos(threshold).
        Vector3 toHead = (head.position - transform.position).normalized;
        return Vector3.Dot(transform.forward, toHead) >= Mathf.Cos(revealAngle * Mathf.Deg2Rad);
    }
}
