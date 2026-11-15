using System;
using UnityEngine;

namespace Wynnyng.Configuration
{
    [Serializable]
    public struct LaboratoryScenario
    {
        public string Name;
        [Tooltip("X = fraction of half-width; Y = height in metres; Z = fraction of half-length.")]
        public Vector3 Start;
        public Vector3 Velocity;
        public Vector3 Spin;
        public bool AlignWithPost;
    }

    [CreateAssetMenu(menuName = "Wynnyng/Presentation Config")]
    public sealed class PresentationConfig : ScriptableObject
    {
        public Vector3 OverviewPosition = new Vector3(78f, 72f, 0f);
        public Vector3 OverviewTarget = Vector3.zero;
        [Range(10f, 120f)] public float FieldOfView = 68f;
        [Min(0.01f)] public float NearClip = 0.1f;
        [Min(1f)] public float FarClip = 350f;
        public Color Background = new Color(0.025f, 0.045f, 0.055f);
        public Vector3 LightEulerAngles = new Vector3(55f, -35f, 0f);
        [Min(0f)] public float LightIntensity = 1.2f;
        [Min(0.01f)] public float StatusInterval = 0.1f;
        [Min(0f)] public float AutoResetDelay = 3f;
        [Min(0.1f)] public float SafetyResetHeight = 8f;
        [Min(0.1f)] public float MarkerRadius = 0.7f;
        [Min(0.01f)] public float MarkerWidth = 0.06f;
        public Color Accent = new Color(0.75f, 1f, 0.29f);
        public Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        [Min(12f)] public float FontSize = 24f;
        [Min(1f)] public float PanelPadding = 24f;
        [Min(1f)] public float ButtonHeight = 44f;
        [Min(1f)] public float RebindPanelWidth = 760f;
        [Min(1f)] public float HudWidth = 620f;
        public LaboratoryScenario[] Scenarios =
        {
            new LaboratoryScenario { Name = "Gol no fundo +Z", Start = new Vector3(0f, 0.12f, 0.82f), Velocity = new Vector3(0f, 0f, 22f) },
            new LaboratoryScenario { Name = "Gol no fundo -Z", Start = new Vector3(0f, 0.12f, -0.82f), Velocity = new Vector3(0f, 0f, -22f) },
            new LaboratoryScenario { Name = "Bola acima do travessao", Start = new Vector3(0f, 3.3f, 0.9f), Velocity = new Vector3(0f, 3f, 32f) },
            new LaboratoryScenario { Name = "Saida pela lateral", Start = new Vector3(0.9f, 0.12f, 0f), Velocity = new Vector3(18f, 0f, 0f) },
            new LaboratoryScenario { Name = "Quique de 3 metros", Start = new Vector3(0f, 3f, 0f) },
            new LaboratoryScenario { Name = "Curva / Magnus", Start = new Vector3(0f, 0.3f, -0.4f), Velocity = new Vector3(0f, 9f, 24f), Spin = new Vector3(0f, 40f, 0f) },
            new LaboratoryScenario { Name = "Rolamento", Start = new Vector3(0f, 0.12f, -0.2f), Velocity = new Vector3(0f, 0f, 12f) },
            new LaboratoryScenario { Name = "Colisao com a trave", Start = new Vector3(0f, 0.5f, 0.9f), Velocity = new Vector3(0f, 0f, 22f), AlignWithPost = true }
        };
        public void ValidateOrThrow()
        {
            if (OverviewPosition == OverviewTarget || NearClip <= 0f || FarClip <= NearClip || FieldOfView <= 0f ||
                FieldOfView >= 180f || StatusInterval <= 0f || AutoResetDelay < 0f || SafetyResetHeight <= 0f ||
                MarkerRadius <= 0f || MarkerWidth <= 0f || ReferenceResolution.x <= 0f || ReferenceResolution.y <= 0f ||
                FontSize <= 0f || PanelPadding <= 0f || ButtonHeight <= 0f || RebindPanelWidth <= 0f ||
                HudWidth <= 0f || Scenarios == null || Scenarios.Length == 0)
                throw new InvalidOperationException("PresentationConfig: invalid camera, HUD or lab settings.");
        }
    }
}
