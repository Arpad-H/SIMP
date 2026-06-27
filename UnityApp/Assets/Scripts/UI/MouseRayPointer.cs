using UnityEngine;
using UnityEngine.InputSystem;
using Oculus.Interaction;

/// Drives a standalone ISDK RayInteractor from the desktop mouse, so a non-VR
/// player can point-and-click the same world-space ISDK canvases as the VR ray.
/// Put this on a GameObject that also has a RayInteractor + VirtualSelector.
[DefaultExecutionOrder(-50)]                 // aim before the RayInteractor drives itself
[RequireComponent(typeof(VirtualSelector))]
public class MouseRayPointer : MonoBehaviour
{
    [Tooltip("Camera the non-VR player looks through (the one rendering to the monitor).")]
    [SerializeField] private Camera _camera;

    [Tooltip("Transform used as the RayInteractor's Ray Origin. Leave empty to use this object.")]
    [SerializeField] private Transform _rayOrigin;

    private VirtualSelector _selector;

    void Awake()
    {
        _selector  = GetComponent<VirtualSelector>();
        if (_camera == null)    _camera = Camera.main;
        if (_rayOrigin == null) _rayOrigin = transform;
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || _camera == null) return;

        Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
        _rayOrigin.SetPositionAndRotation(ray.origin, Quaternion.LookRotation(ray.direction));

        if (mouse.leftButton.wasPressedThisFrame)  _selector.Select();
        if (mouse.leftButton.wasReleasedThisFrame) _selector.Unselect();
    }
}