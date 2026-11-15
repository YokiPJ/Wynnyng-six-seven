using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wynnyng.Configuration;
using Wynnyng.Core;
using Object = UnityEngine.Object;

namespace Wynnyng.Tests.EditMode
{
    public sealed class FootballConfigurationTests
    {
        private FootballConfig config;
        [SetUp] public void SetUp() { config = ScriptableObject.CreateInstance<FootballConfig>(); }
        [TearDown] public void TearDown() { Object.DestroyImmediate(config); }
        [Test]
        public void FictitiousTeamsAndDefaultTuningAreReadyWithoutExternalModels()
        {
            Assert.DoesNotThrow(config.ValidateOrThrow);
            Assert.That(config.IsPlaceholder, Is.True);
            Assert.That(config.HomeName, Is.Not.EqualTo(config.AwayName));
            Assert.That(config.HomeColor, Is.Not.EqualTo(config.AwayColor));
            Assert.That(config.Gameplay.Formation.Length, Is.EqualTo(11));
        }
        [Test]
        public void MatchCloneKeepsRuntimeTuningOutsideTheScriptableObject()
        {
            FootballParameters runtime = config.Gameplay.Clone();
            runtime.JogSpeed = 4f; runtime.Formation[1].Width = 0.2f;
            Assert.That(config.Gameplay.JogSpeed, Is.EqualTo(5.2f));
            Assert.That(config.Gameplay.Formation[1].Width, Is.EqualTo(-0.78f));
        }
        [Test]
        public void NonFiniteCameraAndColorsAreRejected()
        {
            config.CameraOffset = new Vector3(float.NaN, 30f, 0f);
            Assert.Throws<InvalidOperationException>(config.ValidateOrThrow);
            config.CameraOffset = new Vector3(42f, 36f, 0f);
            config.HomeColor = new Color(1f, float.PositiveInfinity, 0f);
            Assert.Throws<InvalidOperationException>(config.ValidateOrThrow);
            config.HomeColor = Color.blue;
            config.SkinColors[0] = new Color(float.NaN, 0f, 0f);
            Assert.Throws<InvalidOperationException>(config.ValidateOrThrow);
        }
        [Test]
        public void BrokenGameplayRangesAndColliderDimensionsAreRejected()
        {
            FootballParameters saved = config.Gameplay;
            config.Gameplay = null;
            Assert.Throws<InvalidOperationException>(config.ValidateOrThrow);
            config.Gameplay = saved;
            config.PassRange = new Vector2(25f, 10f);
            Assert.Throws<InvalidOperationException>(config.ValidateOrThrow);
            config.PassRange = new Vector2(12f, 26f);
            config.Gameplay.PlayerRadius = config.PlayerHeight;
            Assert.Throws<InvalidOperationException>(config.ValidateOrThrow);
        }
        [Test]
        public void DefaultBuildStartsAtThePlayableMatchScene()
        {
            const string path = "Assets/Wynnyng/Scenes/FootballMatch.unity";
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null);
            Assert.That(EditorBuildSettings.scenes.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo(path));
            Assert.That(EditorBuildSettings.scenes[0].enabled, Is.True);
            FootballConfig imported = Resources.Load<FootballConfig>("Config/Football");
            Assert.That(imported, Is.Not.Null);
            Assert.DoesNotThrow(imported.ValidateOrThrow);
        }
    }
}
