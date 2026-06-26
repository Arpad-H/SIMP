using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

/// <summary>
/// Asymmetric two-player setup: the VR player sees the headset (the BuildingBlock
/// Camera Rig / CenterEyeAnchor), while the monitor player sees a separate flat
/// camera that follows the squirrel.
///
/// By default Unity blits a *mirror* of the HMD onto the desktop window, which
/// paints over any flat camera. We turn that mirror off so the desktop shows the
/// squirrel camera instead, and force the squirrel camera to render only to the
/// flat screen (never into the headset).
///
/// Put this on any GameObject in the scene and drag the squirrel's monitor camera
/// into <see cref="monitorCamera"/>.
/// </summary>
public class SpectatorMonitorView : MonoBehaviour
{
    [Tooltip("The flat camera the monitor player looks through (e.g. Cam_Squirrel_MAIN).")]
    [SerializeField] private Camera monitorCamera;

    private void Start()
    {
        // Desktop window shows the flat squirrel camera, not a mirror of the HMD.
        XRSettings.gameViewRenderMode = GameViewRenderMode.None;

        if (monitorCamera == null)
            return;

        // This camera renders to the monitor only — never into the headset.
        monitorCamera.stereoTargetEye = StereoTargetEyeMask.None;

        if (monitorCamera.TryGetComponent(out UniversalAdditionalCameraData camData))
            camData.allowXRRendering = false;
    }
}
