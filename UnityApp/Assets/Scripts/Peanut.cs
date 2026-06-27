using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// A peanut the squirrel can eat. The peanut hovers and spins as an idle collectible. When the
/// squirrel is standing inside the peanut's trigger collider AND the player is shaking the phone,
/// the eat progress fills up over <see cref="timeToEat"/> seconds; once it reaches 1 the nut is
/// eaten and consumed. While actively eating, the squirrel's movement is locked so it stays put.
///
/// Proximity comes from this object's trigger collider (auto-created if missing); the "is the
/// phone shaking" signal comes from <see cref="PhoneGestureDetector.IsShaking"/>. Progress
/// persists while the squirrel lingers (it never decays), mirroring <c>CutProgress</c>.
/// </summary>
public class Peanut : MonoBehaviour
{
    [Header("Floating Settings")]
    [SerializeField] private float floatSpeed = 2f;       // How fast it moves up and down
    [SerializeField] private float floatAmplitude = 0.5f; // How far up and down it goes

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 50f;   // Degrees per second

    [Header("Eating")]
    [Tooltip("Seconds of continuous shaking (with the squirrel in range) needed to fully eat the nut.")]
    [SerializeField] private float timeToEat = 2f;

    [Tooltip("Radius of the trigger that defines 'close enough'. Only used when this object has no " +
             "Collider yet and one is created automatically; if you add your own trigger collider " +
             "it is used as-is.")]
    [SerializeField] private float eatRange = 1.5f;

    [Tooltip("How long the squirrel stays frozen after the last detected shake. Bridges the gaps " +
             "in a gentle shake (so incidental tilt never leaks into movement) and gives a short " +
             "tail before the squirrel can walk away.")]
    [SerializeField] private float moveLockHold = 0.5f;

    [Header("References")]
    [Tooltip("Source of the shake signal. Found automatically in the scene if left empty.")]
    [SerializeField] private PhoneGestureDetector gestureDetector;

    [Header("Eat Progress UI")]
    [Tooltip("Image (Image Type = Filled) whose fillAmount is driven by the eat progress 0..1.")]
    [SerializeField] private Image eatProgressFill;

    [Tooltip("Object shown only while actively eating — e.g. the progress bar root. Defaults to the " +
             "fill image's GameObject if left empty.")]
    [SerializeField] private GameObject eatProgressUI;

    [Header("Events")]
    [Tooltip("Fired once when the nut has been fully eaten (hook up score, SFX, VFX, ...).")]
    public UnityEvent onEaten;

    /// <summary>0..1 progress toward eating this nut.</summary>
    public float EatProgress => eatProgress;
    public bool IsEaten => eaten;
    public bool SquirrelInRange => squirrelInRange;

    private Vector3 startPosition;
    private float eatProgress;
    private bool eaten;
    private bool squirrelInRange;

    private SquirrelController squirrel; // the squirrel currently in range (captured on enter)
    private bool isEating;               // true while the squirrel is frozen for this stretch
    private float lastShakeTime = -999f; // time of the most recent shake sample, for the lock hold

    private void Awake()
    {
        EnsureTriggerCollider();

        if (gestureDetector == null)
        {
            gestureDetector = FindAnyObjectByType<PhoneGestureDetector>();
            if (gestureDetector == null)
                Debug.LogWarning($"{nameof(Peanut)} '{name}': no {nameof(PhoneGestureDetector)} in the " +
                                 "scene, so shaking can't be detected. Add one (e.g. on the OSC " +
                                 "receiver object) or assign it in the inspector.", this);
        }

        if (eatProgressUI == null && eatProgressFill != null)
            eatProgressUI = eatProgressFill.gameObject;
        if (eatProgressUI != null)
            eatProgressUI.SetActive(false);
    }

    private void Start()
    {
        // Record the starting position so the hover is relative to where it was placed.
        startPosition = transform.position;
    }

    private void Update()
    {
        Hover();

        // Progress only on frames the phone is actually shaking and the squirrel is in range.
        bool shakingNow = squirrelInRange && !eaten &&
                          gestureDetector != null && gestureDetector.IsShaking;

        if (shakingNow)
        {
            lastShakeTime = Time.time;
            TryEatNut();
        }

        // Keep the squirrel frozen through the brief gaps in a gentle shake, and for a short tail
        // afterwards, so incidental tilt while shaking never leaks into movement. The lock follows
        // this held state, not the flickering instant-by-instant shake signal.
        bool lockMovement = squirrelInRange && !eaten &&
                            Time.time - lastShakeTime <= moveLockHold;

        SetEating(lockMovement);
    }

    // Idle motion: bob up and down on a sine wave and spin slowly in place.
    private void Hover()
    {
        float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
        transform.position = new Vector3(startPosition.x, newY, startPosition.z);
        transform.Rotate(Vector3.up * (rotationSpeed * Time.deltaTime));
    }

    /// <summary>
    /// Advances the eat progress by one frame's worth. Safe to call every frame while shaking:
    /// it only does anything when the squirrel is in range and the nut isn't eaten yet, and fully
    /// eats the nut once <see cref="timeToEat"/> seconds of progress have accumulated.
    /// </summary>
    public void TryEatNut()
    {
        if (eaten || !squirrelInRange || timeToEat <= 0f)
            return;

        eatProgress = Mathf.Clamp01(eatProgress + Time.deltaTime / timeToEat);
        UpdateEatProgressVisual(eatProgress);

        if (eatProgress >= 1f)
            Eat();
    }

    // Enters / leaves the "eating" state: locks the squirrel in place and shows the progress bar
    // while eating, releases both when it stops. Idempotent, so it's safe to call from Eat/OnDisable.
    private void SetEating(bool eating)
    {
        if (eating == isEating)
            return;

        isEating = eating;

        if (squirrel != null)
        {
            if (eating) squirrel.AddMovementLock();
            else        squirrel.RemoveMovementLock();
        }

        if (eatProgressUI != null)
            eatProgressUI.SetActive(eating);
    }

    private void Eat()
    {
        eaten = true;
        SetEating(false); // release the movement lock / hide the bar before we vanish
        Debug.Log($"[Peanut] eaten: {name}", this);
        onEaten?.Invoke();
        Destroy(gameObject);
    }

    /// <summary>Drives the eat-progress fill image (progress01 is 0..1).</summary>
    private void UpdateEatProgressVisual(float progress01)
    {
        if (eatProgressFill != null)
            eatProgressFill.fillAmount = progress01;
    }

    private void OnTriggerEnter(Collider other)
    {
        var controller = other.GetComponentInParent<SquirrelController>();
        if (controller == null)
            return;

        squirrel = controller;
        squirrelInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<SquirrelController>() != null)
            squirrelInRange = false;
    }

    private void OnDisable()
    {
        // Never leave the squirrel frozen if we're disabled/destroyed mid-bite.
        SetEating(false);
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
