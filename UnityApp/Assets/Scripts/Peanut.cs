using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// A peanut the squirrel can eat. The peanut hovers and spins as an idle collectible. When the
/// squirrel is standing inside the peanut's trigger collider, each tap on the companion play screen
/// advances the eat progress by one step; once <see cref="tapsToEat"/> taps have landed the nut is
/// fully eaten and consumed. Progress persists while the squirrel lingers (it never decays),
/// mirroring <c>CutProgress</c>.
///
/// Proximity comes from this object's trigger collider (auto-created if missing); the tap signal
/// comes from <see cref="OSCReceiver.OnTap"/> (the companion's /tap message).
/// </summary>
public class Peanut : MonoBehaviour
{
    [Header("Floating Settings")]
    [SerializeField] private float floatSpeed = 2f;       // How fast it moves up and down
    [SerializeField] private float floatAmplitude = 0.5f; // How far up and down it goes

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 50f;   // Degrees per second

    [Header("Eating")]
    [Tooltip("Number of taps (with the squirrel in range) needed to fully eat the nut. Each tap adds " +
             "1 / this much progress.")]
    [SerializeField] private int tapsToEat = 5;

    [Tooltip("Radius of the trigger that defines 'close enough'. Only used when this object has no " +
             "Collider yet and one is created automatically; if you add your own trigger collider " +
             "it is used as-is.")]
    [SerializeField] private float eatRange = 1.5f;

    [Header("Falling")]
    [Tooltip("Downward acceleration (m/s^2) applied while the nut drops after its tree is sliced.")]
    [SerializeField] private float fallGravity = 9.81f;

    [Tooltip("How high above the ground the nut settles, and keeps hovering, once it lands.")]
    [SerializeField] private float groundHoverOffset = 0.4f;

    [Tooltip("Layers the drop ray treats as ground. The Sliceable layer (the tree itself) is " +
             "always ignored on top of this, so the nut falls past the fresh stump to the real ground.")]
    [SerializeField] private LayerMask groundMask = ~0;

    [Tooltip("How far below the nut to search for ground when it starts falling.")]
    [SerializeField] private float maxFallDistance = 100f;

    [Header("References")]
    [Tooltip("Source of the tap signal. Found automatically in the scene if left empty.")]
    [SerializeField] private OSCReceiver oscReceiver;

    [Header("Eat Progress UI")]
    [Tooltip("Image (Image Type = Filled) whose fillAmount is driven by the eat progress 0..1.")]
    [SerializeField] private Image eatProgressFill;

    [Tooltip("Object shown only while the squirrel is in range — e.g. the progress bar root. Defaults " +
             "to the fill image's GameObject if left empty.")]
    [SerializeField] private GameObject eatProgressUI;

    [Header("Outline Highlight")]
    [Tooltip("While the squirrel is in range to eat, the nut's outline is tinted to this colour, then " +
             "reverts to its original colour once the squirrel leaves. Only the outline COLOUR changes " +
             "— the outline type (e.g. Fade With Distance), thickness, etc. are left untouched.")]
    [SerializeField] private Color inRangeOutlineColor = Color.green;

    [Tooltip("Keep the original outline colour's alpha when highlighting, so the green matches the " +
             "existing fade strength and only the hue changes. Turn off to use the highlight colour's " +
             "own alpha.")]
    [SerializeField] private bool preserveOutlineAlpha = true;

    [Tooltip("Renderer whose material's outline colour is tinted. Auto-found (first child mesh " +
             "renderer) if left empty.")]
    [SerializeField] private Renderer outlineRenderer;

    [Header("Events")]
    [Tooltip("Fired once when the nut has been fully eaten (hook up score, SFX, VFX, ...).")]
    public UnityEvent onEaten;

    [Tooltip("Fired once when the lumberjack picks up this fallen nut (hook up score, SFX, VFX, ...).")]
    public UnityEvent onCollected;

    /// <summary>0..1 progress toward eating this nut.</summary>
    public float EatProgress => eatProgress;
    public bool IsEaten => eaten;
    public bool IsCollected => collected;
    /// <summary>True once the nut has dropped off its tree — only then can the lumberjack collect it.</summary>
    public bool HasDropped => hasDropped;
    public bool SquirrelInRange => squirrelInRange;

    // Eaten by the squirrel or collected by the lumberjack — either way it's spoken for and out of play.
    private bool Consumed => eaten || collected;

    private Vector3 startPosition;
    private float eatProgress;
    private bool eaten;
    private bool collected;   // picked up by the lumberjack
    private bool hasDropped;  // has fallen off its tree, so the lumberjack can now collect it
    private bool squirrelInRange;

    // Outline highlight: we recolour an instanced copy of the material so one nut's highlight never
    // touches the shared asset or any other nut. originalOutlineColor is captured once at Awake and
    // restored when the squirrel leaves.
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private Material outlineMaterial;
    private Color originalOutlineColor;
    private bool hasOutlineColor;

    private bool falling;        // true while dropping to the ground after the tree was sliced
    private float fallVelocity;  // current downward speed during the drop
    private float groundY;       // world Y the nut settles (and then hovers) at once it lands

    private void Awake()
    {
        EnsureTriggerCollider();
        CacheOutlineMaterial();

        if (oscReceiver == null)
        {
            oscReceiver = FindAnyObjectByType<OSCReceiver>();
            if (oscReceiver == null)
                Debug.LogWarning($"{nameof(Peanut)} '{name}': no {nameof(OSCReceiver)} in the scene, " +
                                 "so taps can't be received and the nut can't be eaten. Add one (e.g. " +
                                 "on the OSC server object) or assign it in the inspector.", this);
        }

        if (eatProgressUI == null && eatProgressFill != null)
            eatProgressUI = eatProgressFill.gameObject;
        if (eatProgressUI != null)
            eatProgressUI.SetActive(false);
    }

    private void OnEnable()
    {
        if (oscReceiver != null)
            oscReceiver.OnTap += HandleTap;
    }

    private void OnDisable()
    {
        if (oscReceiver != null)
            oscReceiver.OnTap -= HandleTap;

        // Don't leave the progress bar showing — or the outline tinted — if we're disabled mid-bite.
        ShowEatProgress(false);
        SetOutlineHighlight(false);
    }

    private void Start()
    {
        // Record the starting position so the hover is relative to where it was placed.
        startPosition = transform.position;

        // Join the round so the manager can score this nut and know when every nut is gone.
        GameManager.Instance?.RegisterNut(this);
    }

    private void Update()
    {
        UpdateMotion();
    }

    // Per-frame motion. Always spins. While hovering it bobs around its anchor; while falling
    // (after the tree was sliced) it drops straight down and then resumes hovering where it lands.
    private void UpdateMotion()
    {
        transform.Rotate(Vector3.up * (rotationSpeed * Time.deltaTime));

        if (falling)
        {
            FallTowardsGround();
            return;
        }

        // Bob up and down on a sine wave around the anchor (its spawn spot, or where it landed).
        float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
        transform.position = new Vector3(startPosition.x, newY, startPosition.z);
    }

    /// <summary>
    /// Make this nut drop straight down to the ground and then keep floating where it lands.
    /// Called when the tree it was sitting on gets sliced. No-op if it's already falling or eaten.
    /// </summary>
    public void Drop()
    {
        if (falling || Consumed)
            return;

        // From here on it's a fallen nut, so the lumberjack is allowed to scoop it up.
        hasDropped = true;

        // It has fallen off the tree — detach so it becomes a free world object (surviving any later
        // teardown of the tree) and hovers in world space from here on. Keep its current world pose.
        transform.SetParent(null, true);

        // Look for ground straight below. Ignore triggers (our own eat-range collider and other
        // nuts') and the Sliceable layer, so we drop past the freshly cut stump to the real ground.
        int mask = groundMask;
        int sliceableLayer = LayerMask.NameToLayer("Sliceable");
        if (sliceableLayer >= 0)
            mask &= ~(1 << sliceableLayer);

        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit,
                            maxFallDistance, mask, QueryTriggerInteraction.Ignore))
        {
            groundY = hit.point.y + groundHoverOffset;
            falling = true;
            fallVelocity = 0f;
        }
        else
        {
            // No ground found below — just keep hovering where we are rather than fall forever.
            startPosition = transform.position;
        }
    }

    // One frame of the drop: accelerate downward, and once we reach the ground re-anchor the
    // hover to the landing spot and switch back to floating.
    private void FallTowardsGround()
    {
        fallVelocity += fallGravity * Time.deltaTime;

        Vector3 pos = transform.position;
        pos.y -= fallVelocity * Time.deltaTime;

        if (pos.y <= groundY)
        {
            pos.y = groundY;
            startPosition = pos;   // float around where it landed from now on
            falling = false;
            fallVelocity = 0f;
        }

        transform.position = pos;
    }

    // One tap from the companion play screen. Advances the eat progress while the squirrel is in
    // range; both tap zones (the side argument) feed eating equally. Fully eats the nut once
    // tapsToEat taps have accumulated.
    private void HandleTap(int side)
    {
        if (Consumed || !squirrelInRange)
            return;

        int taps = Mathf.Max(1, tapsToEat);
        eatProgress = Mathf.Clamp01(eatProgress + 1f / taps);
        UpdateEatProgressVisual(eatProgress);

        if (eatProgress >= 1f)
            Eat();
    }

    private void Eat()
    {
        eaten = true;
        ShowEatProgress(false); // hide the bar before we vanish
        Debug.Log($"[Peanut] eaten: {name}", this);
        onEaten?.Invoke();
        GameManager.Instance?.NotifyNutEaten(this);
        Destroy(gameObject);
    }

    /// <summary>
    /// Pick this fallen nut up for the lumberjack. No-op unless the nut has dropped from its tree and
    /// hasn't already been eaten or collected. Scores it for the lumberjack and removes the nut.
    /// Called by <see cref="LumberjackHand"/> when one of the VR player's hands touches it.
    /// </summary>
    public void Collect()
    {
        if (Consumed || !hasDropped)
            return;

        collected = true;
        ShowEatProgress(false); // safety: hide the bar before we vanish
        Debug.Log($"[Peanut] collected by lumberjack: {name}", this);
        onCollected?.Invoke();
        GameManager.Instance?.NotifyNutCollected(this);
        Destroy(gameObject);
    }

    /// <summary>Drives the eat-progress fill image (progress01 is 0..1).</summary>
    private void UpdateEatProgressVisual(float progress01)
    {
        if (eatProgressFill != null)
            eatProgressFill.fillAmount = progress01;
    }

    // Shows / hides the progress bar root, refreshing the fill to the current progress when shown.
    private void ShowEatProgress(bool show)
    {
        if (eatProgressUI != null)
            eatProgressUI.SetActive(show);

        if (show)
            UpdateEatProgressVisual(eatProgress);
    }

    // Grabs an instanced copy of the outline material and remembers its starting outline colour, so
    // the in-range highlight can swap to green and back without ever touching the shared asset.
    private void CacheOutlineMaterial()
    {
        if (outlineRenderer == null)
            outlineRenderer = GetComponentInChildren<MeshRenderer>();

        if (outlineRenderer == null)
            return;

        // Accessing .material instantiates a per-renderer copy, so recolouring this nut never affects
        // the shared M_Peanut asset or any other nut using it.
        Material mat = outlineRenderer.material;
        if (mat == null || !mat.HasProperty(OutlineColorId))
        {
            Debug.LogWarning($"{nameof(Peanut)} '{name}': the outline renderer's material has no " +
                             "'_OutlineColor' property, so the in-range outline highlight is disabled.", this);
            return;
        }

        outlineMaterial = mat;
        originalOutlineColor = mat.GetColor(OutlineColorId);
        hasOutlineColor = true;
    }

    // Tints the outline to the in-range colour, or restores the original. Optionally keeps the
    // original alpha so only the hue changes and the fade strength stays the same.
    private void SetOutlineHighlight(bool highlighted)
    {
        if (!hasOutlineColor)
            return;

        if (!highlighted)
        {
            outlineMaterial.SetColor(OutlineColorId, originalOutlineColor);
            return;
        }

        Color highlight = inRangeOutlineColor;
        if (preserveOutlineAlpha)
            highlight.a = originalOutlineColor.a;

        outlineMaterial.SetColor(OutlineColorId, highlight);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<SquirrelController>() == null)
            return;

        squirrelInRange = true;
        if (!Consumed)
        {
            ShowEatProgress(true);
            SetOutlineHighlight(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<SquirrelController>() == null)
            return;

        squirrelInRange = false;
        ShowEatProgress(false);
        SetOutlineHighlight(false);
    }

    private void OnDestroy()
    {
        // Keep the manager's live-nut tally accurate however we're removed (eaten, collected, or
        // torn down some other way). Nuts already resolved via eat/collect are ignored, so this
        // never double-counts — it's the safety net that still ends the round if a nut is destroyed
        // by some other path.
        GameManager.Instance?.UnregisterNut(this);
    }

    // Guarantee a trigger collider that defines the eat range. Respects an existing collider (just
    // forces it to be a trigger so it never physically blocks the squirrel); otherwise adds a sphere.
    private void EnsureTriggerCollider()
    {
        var col = GetComponent<Collider>();
        if (col == null)
        {
            var sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = eatRange;
            col = sphere;
        }

        col.isTrigger = true;
    }
}
