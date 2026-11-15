using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Wynnyng.Configuration;

namespace Wynnyng.Presentation
{
    public static class OverviewCameraFactory
    {
        private const int DefaultRendererIndex = 0;
        private const float ResetCameraStateDeltaTime = -1f;
        public static Camera Build(Transform root, PresentationConfig config)
        {
            RenderingResources.RequireConfigured();
            Quaternion orientation = Quaternion.LookRotation(config.OverviewTarget - config.OverviewPosition, Vector3.up);

            // Initialize the virtual camera completely before enabling its output brain.
            var overview = new GameObject("Phase 1A Overview - Cinemachine 3");
            overview.SetActive(false);
            overview.transform.SetParent(root, false);
            overview.transform.SetPositionAndRotation(config.OverviewPosition, orientation);
            CinemachineCamera virtualCamera = overview.AddComponent<CinemachineCamera>();
            LensSettings lens = virtualCamera.Lens;
            lens.FieldOfView = config.FieldOfView;
            lens.NearClipPlane = config.NearClip;
            lens.FarClipPlane = config.FarClip;
            virtualCamera.Lens = lens;
            overview.SetActive(true);
            virtualCamera.ForceCameraPosition(config.OverviewPosition, orientation);
            virtualCamera.UpdateCameraState(Vector3.up, ResetCameraStateDeltaTime);

            var output = new GameObject("Main Camera") { tag = "MainCamera" };
            output.SetActive(false);
            output.transform.SetParent(root, false);
            output.transform.SetPositionAndRotation(config.OverviewPosition, orientation);
            Camera camera = output.AddComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = config.FieldOfView;
            camera.cullingMask = ~0;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = config.Background;
            camera.nearClipPlane = config.NearClip;
            camera.farClipPlane = config.FarClip;
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderType = CameraRenderType.Base;
            cameraData.SetRenderer(DefaultRendererIndex);
            cameraData.renderPostProcessing = false;
            output.AddComponent<AudioListener>();
            output.AddComponent<CinemachineBrain>();
            output.SetActive(true);
            var lightObject = new GameObject("Blockout Sun");
            lightObject.transform.SetParent(root, false);
            lightObject.transform.localRotation = Quaternion.Euler(config.LightEulerAngles);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = config.LightIntensity;
            light.shadows = LightShadows.None;
            return camera;
        }
    }
}
