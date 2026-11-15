using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Wynnyng.Configuration;

namespace Wynnyng.Tests.EditMode
{
    public sealed class ConfigurationTests
    {
        [Test]
        public void DefaultScriptableObjectsAreValid()
        {
            var simulation = ScriptableObject.CreateInstance<SimulationConfig>();
            var ball = ScriptableObject.CreateInstance<BallConfig>();
            var pitch = ScriptableObject.CreateInstance<PitchConfig>();
            var presentation = ScriptableObject.CreateInstance<PresentationConfig>();
            try
            {
                Assert.DoesNotThrow(simulation.ValidateOrThrow);
                Assert.DoesNotThrow(ball.ValidateOrThrow);
                Assert.DoesNotThrow(() => pitch.ValidateOrThrow(ball.Radius));
                Assert.DoesNotThrow(presentation.ValidateOrThrow);
            }
            finally
            {
                Object.DestroyImmediate(simulation); Object.DestroyImmediate(ball);
                Object.DestroyImmediate(pitch); Object.DestroyImmediate(presentation);
            }
        }
        [Test]
        public void ShippedConfigsAndShadersCanBeLoaded()
        {
            Assert.That(Resources.Load<SimulationConfig>("Config/Simulation"), Is.Not.Null);
            Assert.That(Resources.Load<PitchConfig>("Config/Pitch"), Is.Not.Null);
            Assert.That(Resources.Load<BallConfig>("Config/Ball"), Is.Not.Null);
            Assert.That(Resources.Load<PresentationConfig>("Config/Presentation"), Is.Not.Null);
            Assert.That(Resources.Load<Shader>("Shaders/Blockout"), Is.Not.Null);
            Assert.That(Resources.Load<Shader>("Shaders/Ball"), Is.Not.Null);
        }
        [Test]
        public void ActionMapsAreCompleteAndCopiesAreIsolated()
        {
            InputActionAsset source = Resources.Load<InputActionAsset>("Input/Match");
            Assert.That(source, Is.Not.Null);
            InputActionAsset one = Object.Instantiate(source), two = Object.Instantiate(source);
            try
            {
                string[] gameplay = { "Move", "Shoot", "Pass", "Through", "Lob", "Sprint", "Switch", "Pause" };
                string[] lab = { "Previous", "Next", "Launch", "Reset", "Bindings" };
                for (int i = 0; i < gameplay.Length; i++)
                    Assert.That(source.FindActionMap("Gameplay", true).FindAction(gameplay[i], true), Is.Not.Null);
                for (int i = 0; i < lab.Length; i++)
                    Assert.That(source.FindActionMap("Laboratory", true).FindAction(lab[i], true), Is.Not.Null);
                InputAction shoot = one.FindAction("Gameplay/Shoot", true);
                shoot.ApplyBindingOverride(0, "<Gamepad>/leftShoulder");
                Assert.That(two.FindAction("Gameplay/Shoot", true).bindings[0].overridePath, Is.Null);
                string json = one.SaveBindingOverridesAsJson();
                two.LoadBindingOverridesFromJson(json);
                Assert.That(two.FindAction("Gameplay/Shoot", true).bindings[0].overridePath, Is.EqualTo("<Gamepad>/leftShoulder"));
            }
            finally { Object.DestroyImmediate(one); Object.DestroyImmediate(two); }
        }
    }
}
