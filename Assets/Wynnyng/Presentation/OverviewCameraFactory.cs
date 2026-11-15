using Unity.Cinemachine;
using UnityEngine;
using Wynnyng.Configuration;

namespace Wynnyng.Presentation
{
    public static class OverviewCameraFactory
    {
        public static Camera Build(Transform root, PresentationConfig config)
        {
            var output = new GameObject("Main Camera") { tag = "MainCamera" };
            output.transform.SetParent(root, false);
            Camera camera = output.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = config.Background;
            camera.nearClipPlane = config.NearClip;
            camera.farClipPlane = config.FarClip;
            output.AddComponent<AudioListener>();
            output.AddComponent<CinemachineBrain>();
            var overview = new GameObject("Phase 1A Overview - Cinemachine 3");
            overview.transform.SetParent(root, false);
            Quaternion orientation = Quaternion.LookRotation(config.OverviewTarget - config.OverviewPosition, Vector3.up);
            overview.transform.SetPositionAndRotation(config.OverviewPosition, orientation);
            output.transform.SetPositionAndRotation(config.OverviewPosition, orientation);
            CinemachineCamera virtualCamera = overview.AddComponent<CinemachineCamera>();
            LensSettings lens = virtualCamera.Lens;
            lens.FieldOfView = config.FieldOfView;
            lens.NearClipPlane = config.NearClip;
            lens.FarClipPlane = config.FarClip;
            virtualCamera.Lens = lens;
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
