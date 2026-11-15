using NUnit.Framework;
using TMPro;
using Wynnyng.Presentation;

namespace Wynnyng.Tests.EditMode
{
    public sealed class TmpResourcesTests
    {
        [Test]
        public void MissingSettingsNeverCallsTheUnsafeFontGetter()
        {
            bool getterCalled = false;
            bool found = TmpResources.TryGetDefaultFont(out TMP_FontAsset font,
                () => false, () =>
                {
                    getterCalled = true;
                    throw new System.NullReferenceException("Font getter must not run without TMP settings.");
                });
            Assert.That(found, Is.False);
            Assert.That(font, Is.Null);
            Assert.That(getterCalled, Is.False);
        }

        [Test]
        public void SettingsWithoutDefaultFontAreNotReady()
        {
            bool found = TmpResources.TryGetDefaultFont(out TMP_FontAsset font, () => true, () => null);
            Assert.That(found, Is.False);
            Assert.That(font, Is.Null);
        }

        [Test]
        public void PreparedProjectIncludesItsDefaultTmpFont()
        {
            Assert.That(TmpResources.RequireDefaultFont(), Is.Not.Null, TmpResources.MissingResourcesMessage);
        }
    }
}
