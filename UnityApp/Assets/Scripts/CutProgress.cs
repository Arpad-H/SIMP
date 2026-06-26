using UnityEngine;

/// <summary>
/// Tracks how far a chainsaw has sawn through a single object. Lives ON the object
/// being cut, so progress persists when the blade leaves and comes back: SliceObject
/// looks the component up again and keeps adding. When progress reaches 1 the caller
/// does the real EzySlice along the locked plane.
///
/// Visual feedback is a stub for now (UpdateProgressBar) — to be filled in later.
/// </summary>
[DisallowMultipleComponent]
public class CutProgress : MonoBehaviour
{
    private bool initialized;
    private float progress; // 0..1, persists for the lifetime of this object

    // Locked cut plane (set once, on first contact).
    private Vector3 planePoint;
    private Vector3 planeNormal;

    public float   Progress    => progress;
    public bool    IsComplete  => progress >= 1f;
    public Vector3 PlanePoint  => planePoint;
    public Vector3 PlaneNormal => planeNormal;

    /// <summary>Call once, on first blade contact, to lock the cut plane.</summary>
    public void Begin(Vector3 entryPoint, Vector3 normal)
    {
        if (initialized) return;
        initialized = true;
        planeNormal = normal.sqrMagnitude > 1e-8f ? normal.normalized : Vector3.up;
        planePoint  = entryPoint;
        UpdateProgressBar(progress);
    }

    /// <summary>Advance the cut by dt seconds (timeToCut = seconds for a full cut-through).</summary>
    public void Advance(float deltaSeconds, float timeToCut)
    {
        if (!initialized || timeToCut <= 0f) return;
        progress = Mathf.Clamp01(progress + deltaSeconds / timeToCut);
        UpdateProgressBar(progress);
    }

    /// <summary>Stub: drive the cut progress bar (progress01 is 0..1). Empty for now.</summary>
    private void UpdateProgressBar(float progress01)
    {
        // TODO: update the progress bar here.
    }
}
