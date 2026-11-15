using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Wynnyng.Configuration;

namespace Wynnyng.Presentation
{
    /// <summary>Original wide sideline camera. Its explicit manual Brain update has no script-order dependency.</summary>
    public sealed class FootballCameraRig : MonoBehaviour
    {
        private const int DefaultRendererIndex = 0;
        private const float ResetStateDeltaTime = -1f;
        private Transform ball;
        private Transform[] players;
        private Rigidbody ballBody;
        private FootballConfig config;
        private CinemachineCamera virtualCamera;
        private CinemachineBrain brain;
        private Vector3 focus;
        private float currentZoom = 1f;
        private bool initialized;
        public Camera OutputCamera { get; private set; }

        public static FootballCameraRig Build(Transform root, Transform ball, Transform[] players,
            FootballConfig config, PresentationConfig presentation)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (ball == null) throw new ArgumentNullException(nameof(ball));
            if (players == null || players.Length == 0) throw new ArgumentException("Camera requires players.", nameof(players));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            RenderingResources.RequireConfigured();

            var rigObject = new GameObject("Wide Football Camera Rig");
            rigObject.transform.SetParent(root, false);
            FootballCameraRig rig = rigObject.AddComponent<FootballCameraRig>();
            rig.ball = ball;
            rig.players = players;
            rig.ballBody = ball.GetComponent<Rigidbody>();
            rig.config = config;
            rig.focus = rig.DesiredFocus();
            Vector3 initialPosition = rig.focus + config.CameraOffset;
            Quaternion orientation = Quaternion.LookRotation(rig.focus - initialPosition, Vector3.up);

            var cameraObject = new GameObject("Wide - Cinemachine 3");
            cameraObject.SetActive(false);
            cameraObject.transform.SetParent(rigObject.transform, false);
            cameraObject.transform.SetPositionAndRotation(initialPosition, orientation);
            rig.virtualCamera = cameraObject.AddComponent<CinemachineCamera>();
            LensSettings lens = rig.virtualCamera.Lens;
            lens.FieldOfView = config.CameraFieldOfView;
            lens.NearClipPlane = presentation.NearClip;
            lens.FarClipPlane = presentation.FarClip;
            rig.virtualCamera.Lens = lens;
            cameraObject.SetActive(true);
            rig.virtualCamera.ForceCameraPosition(initialPosition, orientation);
            rig.virtualCamera.UpdateCameraState(Vector3.up, ResetStateDeltaTime);

            var output = new GameObject("Main Camera") { tag = "MainCamera" };
            output.SetActive(false);
            output.transform.SetParent(rigObject.transform, false);
            output.transform.SetPositionAndRotation(initialPosition, orientation);
            rig.OutputCamera = output.AddComponent<Camera>();
            rig.OutputCamera.orthographic = false;
            rig.OutputCamera.fieldOfView = config.CameraFieldOfView;
            rig.OutputCamera.cullingMask = ~0;
            rig.OutputCamera.clearFlags = CameraClearFlags.SolidColor;
            rig.OutputCamera.backgroundColor = presentation.Background;
            rig.OutputCamera.nearClipPlane = presentation.NearClip;
            rig.OutputCamera.farClipPlane = presentation.FarClip;
            UniversalAdditionalCameraData data = rig.OutputCamera.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Base;
            data.SetRenderer(DefaultRendererIndex);
            data.renderPostProcessing = false;
            output.AddComponent<AudioListener>();
            rig.brain = output.AddComponent<CinemachineBrain>();
            rig.brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            output.SetActive(true);

            var sunObject = new GameObject("Football Sun");
            sunObject.transform.SetParent(root, false);
            sunObject.transform.localRotation = Quaternion.Euler(presentation.LightEulerAngles);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = presentation.LightIntensity;
            sun.shadows = LightShadows.None;
            rig.initialized = true;
            rig.brain.ManualUpdate();
            return rig;
        }

        private Vector3 DesiredFocus()
        {
            Vector3 centre = Vector3.zero;
            int count = 0;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null) continue;
                centre += players[i].position;
                count++;
            }
            if (count > 0) centre /= count;
            Vector3 ballPoint = ball.position;
            if (ballBody != null) ballPoint += ballBody.linearVelocity * config.CameraLeadSeconds;
            Vector3 target = Vector3.Lerp(centre, ballPoint, config.CameraBallWeight);
            target.y = 0f;
            return target;
        }

        private void LateUpdate()
        {
            if (!initialized || ball == null) return;
            float blend = 1f - Mathf.Exp(-config.CameraDamping * Time.unscaledDeltaTime);
            focus = Vector3.Lerp(focus, DesiredFocus(), blend);
            float speed = ballBody == null ? 0f : ballBody.linearVelocity.magnitude;
            float desiredZoom = Mathf.Clamp(1f + speed * config.CameraSpeedZoom / config.Gameplay.ShotMaxSpeed,
                1f, config.CameraMaximumZoom);
            currentZoom = Mathf.Lerp(currentZoom, desiredZoom, blend);
            Vector3 position = focus + config.CameraOffset * currentZoom;
            Quaternion orientation = Quaternion.LookRotation(focus - position, Vector3.up);
            virtualCamera.ForceCameraPosition(position, orientation);
            brain.ManualUpdate();
        }
    }
}
