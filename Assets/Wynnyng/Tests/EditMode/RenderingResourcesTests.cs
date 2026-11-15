using NUnit.Framework;
using Wynnyng.Presentation;

namespace Wynnyng.Tests.EditMode
{
    public sealed class RenderingResourcesTests
    {
        [Test]
        public void MissingPipelineCannotBeTreatedAsReadyForUniversalShaders()
        {
            Assert.That(RenderingResources.IsUniversalPipeline(null, null), Is.False);
        }

        [Test]
        public void PreparedProjectHasAnEffectiveUniversalPipeline()
        {
            Assert.That(RenderingResources.IsConfigured, Is.True, RenderingResources.MissingPipelineMessage);
            Assert.DoesNotThrow(RenderingResources.RequireConfigured);
        }
    }
}
