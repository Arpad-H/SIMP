using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

/// <summary>
/// Asymmetric two-player setup: the VR player sees the headset (the BuildingBlock
/// Camera Rig / CenterEyeAnchor), while the monitor player sees a separate flat
/// camera.
///
/// By default Unity blits a *mirror* of the HMD onto the desktop window, which
/// paints over any flat camera. We turn that mirror off so the desktop shows the
/// monitor camera instead, and force that camera to render only to the flat
/// screen (never into the headset).
///
/// Two different mirrors have to be disabled:
///   - In the Editor, the Game-view mirror -> <see cref="XRSettings.gameViewRenderMode"/>.
///   - In a standalone build, the XR "mirror blit" -> the display subsystem's
///     preferred blit mode. gameViewRenderMode does nothing in a build.
///
/// Put this on any GameObject in the scene and drag the monitor camera into
/// <see cref="monitorCamera"/>.
/// </summary>
public class SpectatorMonitorView : MonoBehaviour
{
    [Tooltip("The flat camera the monitor player looks through (e.g. Cam_Squirrel_MAIN).")]
    [SerializeField] private Camera monitorCamera;

    private static readonly List<XRDisplaySubsystem> s_Displays = new();
    private bool _mirrorDisabled;

    private void Start()
    {
        // Editor Game view: show the flat camera, not a mirror of the HMD.
        XRSettings.gameViewRenderMode = GameViewRenderMode.None;

        if (monitorCamera != null)
        {
            // This camera renders to the monitor only — never into the headset.
            monitorCamera.stereoTargetEye = StereoTargetEyeMask.None;

            if (monitorCamera.TryGetComponent(out UniversalAdditionalCameraData camData))
                camData.allowXRRendering = false;
        }
        else
        {
            Debug.LogWarning($"{nameof(SpectatorMonitorView)}: 'monitorCamera' is not assigned — " +
                             "the desktop will keep showing the HMD view. Assign the flat monitor " +
                             "camera in the Inspector (in Edit mode, then save the scene).", this);
        }

        TryDisableBuildMirror();
    }

    private void Update()
    {
        // The XR display subsystem may not be running on the first frame(s), so keep
        // trying until the mirror is actually off. If a build ever re-enables the
        // mirror later, delete the early-out below so this re-applies every frame.
        if (!_mirrorDisabled)
            TryDisableBuildMirror();
    }

    /// <summary>
    /// Standalone builds ignore gameViewRenderMode; the desktop mirror there is the
    /// XR "mirror blit". Setting the preferred mode to None lets the flat camera own
    /// the window. Note: this is a *preference* — providers may ignore unsupported
    /// modes, but None is honored by the Oculus/Meta OpenXR runtime.
    /// </summary>
    private void TryDisableBuildMirror()
    {
        SubsystemManager.GetSubsystems(s_Displays);
        if (s_Displays.Count == 0)
            return; // XR not up yet — retry next frame

        foreach (var display in s_Displays)
            display.SetPreferredMirrorBlitMode((int)XRMirrorViewBlitMode.None);

        _mirrorDisabled = true;
    }
}
