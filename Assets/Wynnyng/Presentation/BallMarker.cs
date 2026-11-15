using UnityEngine;
using Wynnyng.Configuration;

namespace Wynnyng.Presentation
{
    public sealed class BallMarker : MonoBehaviour
    {
        private Transform target;
        private float elevation;
        public void Initialize(Transform ball, RuntimeAssets assets, PitchConfig pitch, PresentationConfig config)
        {
            target = ball;
            elevation = pitch.LineElevation * 2f;
            var line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false; line.loop = true;
            line.sharedMaterial = assets.Material(config.Accent);
            line.widthMultiplier = config.MarkerWidth;
            line.positionCount = pitch.CircleSegments;
            for (int i = 0; i < pitch.CircleSegments; i++)
            {
                float angle = Mathf.PI * 2f * i / pitch.CircleSegments;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * config.MarkerRadius);
            }
        }
        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 position = target.position;
            position.y = elevation;
            transform.position = position;
        }
    }
}
