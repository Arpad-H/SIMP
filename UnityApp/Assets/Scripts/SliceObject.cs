using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EzySlice;
using Oculus.Haptics;
using Oculus.Interaction;
using Unity.VisualScripting;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using UnityEngine.XR;
using Plane = UnityEngine.Plane;

//Credits, Code with help of: https://www.youtube.com/watch?v=GQzW6ZJFQ94
public class SliceObject : MonoBehaviour {
    [SerializeField] private Transform startSlicePoint;
    [SerializeField] private Transform endSlicePoint;
    [SerializeField] private VelocityEstimator velocityEstimator;
    [SerializeField] private LayerMask slicableLayer;
    [SerializeField] private Material crossSectionMaterial;
    [Tooltip("Separation speed (m/s, along the cut normal) given to each half when the cut completes. Independent of piece mass.")]
    [SerializeField] private float cutForce = 1.5f;
    [Tooltip("Mass assigned to each sliced half's Rigidbody. Higher = heavier/steadier and harder to shove around.")]
    [SerializeField] private float slicedPieceMass = 5f;
    [Tooltip("Seconds of continuous sawing needed to cut all the way through an object.")]
    [SerializeField] private float timeToCut = 4f;
    [Tooltip("After a piece is cut, how long (seconds) before it may be sliced again — stops the still-embedded blade from instantly shredding fresh halves.")]
    [SerializeField] private float reSliceCooldown = 0.4f;
    [SerializeField] private AudioClip chainsawRefuelSound;
    [SerializeField] private AudioSource chainsawCutSound;
    [SerializeField] private AudioSource chainsawIdleSound;
    [SerializeField] private HapticClip chainsawRunningHapticClip;
    [SerializeField] private HapticClip chainsawPullHapticClip;
    [SerializeField] private LineRenderer pullLine;
    [SerializeField] private Transform pullPos1;
    [SerializeField] private Transform pullPos2;
    [SerializeField] private GrabInteractable grabbable;
    [SerializeField] private Blinker highlighter;

    [SerializeField] private Animator animator; //Auskommentiert, weil error
    private float chainsawRefuelSoundLength;
    private bool canCut = true;
    private bool hasFuel = true;
    private bool started = false;
    private GameObject Fuelpointer;
    private HapticClipPlayer runningHapticPlayer;
    private HapticClipPlayer pullHapticPlayer;
    
    private bool leftGrabbed = false;
    private bool rightGrabbed = false;
    
    public static event Action<bool> OnHasFuelChanged; 
    
    void Start() {
        runningHapticPlayer = new HapticClipPlayer(chainsawRunningHapticClip);
        pullHapticPlayer = new HapticClipPlayer(chainsawPullHapticClip);
        chainsawRefuelSoundLength = chainsawRefuelSound.length;
        Fuelpointer = GameObject.Find("FuelSpin");
        pullLine.positionCount = 2;
        animator.Play("sawing");
    }
    
    float previousPullDistance = 0;
    float currentPullDistance = 0;
    void FixedUpdate() {
        animator.enabled = started;
        pullLine.SetPosition(0, pullPos1.position);
        pullLine.SetPosition(1, pullPos2.position);
        currentPullDistance = (pullPos1.position - pullPos2.position).magnitude;
        if (!started) {
            if (currentPullDistance > 0.3f && hasFuel) {
                started = true;
            }
        }
        HandleAudioAndHaptics();
        
        bool hasHit = Physics.Linecast(startSlicePoint.position, endSlicePoint.position, out RaycastHit hit, slicableLayer);

        // Green line in Scene view = masked hit, red = no masked hit. Enable Gizmos to see it.
        Debug.DrawLine(startSlicePoint.position, endSlicePoint.position, hasHit ? Color.green : Color.red);

        if (hasHit && started && hasFuel) {
            GameObject target = hit.transform.gameObject;

            // Progress lives on the target, so it survives the blade leaving and
            // returning. First contact locks the cut plane.
            CutProgress cut = target.GetComponent<CutProgress>();
            if (cut == null) {
                cut = target.AddComponent<CutProgress>();
            }

            // Fresh halves carry a cooldown so the still-embedded blade can't re-slice
            // them every frame; leave them be until it elapses, then lock the plane and saw.
            if (!cut.OnCooldown) {
                if (!cut.Initialized) {
                    cut.Begin(hit.point, ComputeCutNormal());
                }

                cut.Advance(Time.fixedDeltaTime, timeToCut);

                if (cut.IsComplete) {
                    Slice(target, cut.PlanePoint, cut.PlaneNormal);
                }
            }
        }
    }

    // Cut plane = the plane swept by the blade as it moves. Falls back to the saw's
    // orientation when the blade is momentarily still (velocity ~ 0 gives no plane).
    private Vector3 ComputeCutNormal() {
        Vector3 blade = endSlicePoint.position - startSlicePoint.position;
        Vector3 velocity = velocityEstimator.GetVelocityEstimate();
        Vector3 normal = Vector3.Cross(blade, velocity);
        if (normal.sqrMagnitude < 1e-6f) normal = Vector3.Cross(blade, transform.forward);
        if (normal.sqrMagnitude < 1e-6f) normal = Vector3.Cross(blade, Vector3.up);
        return normal.normalized;
    }
    
    public void HandleAudioAndHaptics() {
        if (grabbable.State != InteractableState.Select) {
            started = false;
            chainsawIdleSound.Stop();
            chainsawCutSound.Stop();
            runningHapticPlayer.Stop();
            pullHapticPlayer.Stop();
        }

        if (grabbable.State == InteractableState.Select) {
            if (currentPullDistance > 0.3f && currentPullDistance > previousPullDistance) {
                pullHapticPlayer.Play(Controller.Both);
            }

            previousPullDistance = currentPullDistance;

            if (started) {
                runningHapticPlayer.Play(Controller.Both);
                if (canCut) {
                    if (!chainsawCutSound.isPlaying) {
                        chainsawCutSound.volume = 1;
                        chainsawCutSound.Play();
                    }
                    //chainsawIdleSound.volume = Mathf.Lerp(chainsawIdleSound.volume, 0, Time.deltaTime);
                }
                else {
                    if (!chainsawIdleSound.isPlaying) {
                        chainsawIdleSound.volume = 1;
                        chainsawIdleSound.Play();
                    }
                    chainsawCutSound.volume = Mathf.Lerp(chainsawCutSound.volume, 0, Time.deltaTime);
                }
            }
        }
    }
    
    public void Slice(GameObject target, Vector3 planePoint, Vector3 planeNormal) {
        SlicedHull hull = target.Slice(planePoint, planeNormal);

        if (hull != null) {
            GameObject upperHull = hull.CreateUpperHull(target, crossSectionMaterial);
            PlaceHull(upperHull, target.transform);
            Collider upperCollider = SetupSlicedComponent(upperHull, planeNormal);
            GameObject lowerHull = hull.CreateLowerHull(target, crossSectionMaterial);
            PlaceHull(lowerHull, target.transform);
            Collider lowerCollider = SetupSlicedComponent(lowerHull, -planeNormal);

            // The two convex hulls share the cut face and overlap by the colliders'
            // contact offset; without this the solver violently shoves them apart.
            Physics.IgnoreCollision(upperCollider, lowerCollider);

            // Keep the halves sliceable so they can be cut again, but give each a brief
            // cooldown first: the blade is still inside them this instant and at
            // timeToCut == 0 would otherwise re-slice them every frame into fragments.
            upperHull.layer = target.layer;
            lowerHull.layer = target.layer;
            upperHull.AddComponent<CutProgress>().StartCooldown(reSliceCooldown);
            lowerHull.AddComponent<CutProgress>().StartCooldown(reSliceCooldown);

            // React to the tree being cut (drop its peanuts, etc.) while the trunk is still
            // parented under the tree, so we can walk up to find what was riding on it.
            OnTreeSliced(target);

            // Disable the trunk this instant so a later physics substep in the SAME frame
            // can't linecast it and slice it a second time (Destroy is deferred to end of
            // frame, so until then its collider is still live and hittable).
            target.SetActive(false);
            Destroy(target);
        }
    }

    // Central hook for everything that should happen when a tree trunk is fully cut through.
    // Add more reactions here later — falling-leaf particles, a tree-snap sound, score, etc.
    private void OnTreeSliced(GameObject slicedTrunk) {
        DropTreePeanuts(slicedTrunk);
        // TODO: spawn falling-leaf particles, play a snap SFX, ...
    }

    // Find every peanut riding on the sliced tree and make it fall to the ground.
    private void DropTreePeanuts(GameObject slicedTrunk) {
        // Peanuts are spawned as children of the tree's NutSpawner (NutSpawner.parentToTree),
        // which lives on the tree root — an ancestor of the trunk we just cut. Walk up to it.
        NutSpawner tree = slicedTrunk.GetComponentInParent<NutSpawner>();
        if (tree == null) return; // not a tree (e.g. re-slicing a detached half) — nothing to drop.

        foreach (Peanut peanut in tree.GetComponentsInChildren<Peanut>(true)) {
            peanut.Drop();
        }
    }

    // EzySlice creates each hull as a parent-less object and copies the ORIGINAL's *local*
    // transform onto it. That's only correct if the sliced object sat at the scene root;
    // our trunk is a child of the tree prefab, so those local values resolve to the wrong
    // world pose (origin, unrotated) and the half can spawn embedded in the terrain — which
    // the solver then ejects violently. Snap each hull to the trunk's true WORLD pose.
    private static void PlaceHull(GameObject hullObject, Transform source) {
        hullObject.transform.position   = source.position;
        hullObject.transform.rotation   = source.rotation;
        hullObject.transform.localScale = source.lossyScale;
    }

    public Collider SetupSlicedComponent(GameObject slicedObject, Vector3 pushDirection) {
        Rigidbody rb = slicedObject.AddComponent<Rigidbody>();
        rb.mass = slicedPieceMass;
        // Continuous detection so a light, fast piece can't tunnel through the terrain.
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        // The hard guarantee against "explosions": cap how fast PhysX may eject this body
        // out of an overlap. Depenetration speed ignores mass, so uncapped it launches
        // pieces of any size; clamped low, any leftover overlap resolves as a gentle push.
        rb.maxDepenetrationVelocity = 1f;

        MeshCollider collider = slicedObject.AddComponent<MeshCollider>();
        collider.convex = true;
        // Small nudge along the cut so the two halves part and drop, rather than exploding.
        // VelocityChange (not Impulse) so the separation speed stays independent of mass.
        rb.AddForce(pushDirection.normalized * cutForce, ForceMode.VelocityChange);
        return collider;
    }

    public void fueledUp() {
        hasFuel = true;
        Destroy(highlighter);
        OnHasFuelChanged?.Invoke(hasFuel);
    }
    
    float timePlaying = 0;
    public void fueling(float fuel) {
        if (chainsawRefuelSoundLength - timePlaying >= chainsawRefuelSoundLength) {
         //   SoundFXManager.instance.PlaySoundFX(chainsawRefuelSound, this.transform, .5f);
        }
        timePlaying += Time.deltaTime;
        if (chainsawRefuelSoundLength - timePlaying <= 0) {
            timePlaying = 0;
        }
        
        Fuelpointer.transform.Rotate(0, fuel, 0);
    }

    public void sawing() {
        canCut = started;
        animator.SetFloat("Speed", 4f);
    }
    
    public void notSawing() {
        animator.SetFloat("Speed", 0.5f);
        chainsawCutSound.Stop();
        canCut = false;
    }
}
