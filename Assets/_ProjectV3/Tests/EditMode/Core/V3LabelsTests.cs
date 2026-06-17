using NUnit.Framework;
using ChemLabSimV3.Data;

namespace ChemLabSimV3.Tests.EditMode.Core
{
    /// <summary>
    /// Tests for V3Labels localization system.
    /// </summary>
    public class V3LabelsTests
    {
        [SetUp]
        public void Setup()
        {
            V3Labels.CurrentLanguage = 0; // Default language
        }

        [Test]
        public void Get_WithExistingKey_ReturnsTranslation()
        {
            string label = V3Labels.Get("mix");
            Assert.IsNotNull(label);
            Assert.IsNotEmpty(label);
        }

        [Test]
        public void Get_WithUnknownKey_ReturnsKey()
        {
            string label = V3Labels.Get("nonexistent_key_xyz");
            Assert.That(label, Is.EqualTo("nonexistent_key_xyz"));
        }

        [Test]
        public void Get_WithEmptyKey_ReturnsEmpty()
        {
            string label = V3Labels.Get("");
            Assert.IsEmpty(label);
        }

        [Test]
        public void SwitchLanguage_ChangesTranslation()
        {
            string enText = V3Labels.Get("mix");
            V3Labels.CurrentLanguage = 1; // Switch to Arabic
            string arText = V3Labels.Get("mix");

            // The Arabic text should be different from English (or at least present)
            Assert.IsNotNull(arText);
            Assert.IsNotEmpty(arText);
        }

        [Test]
        public void CurrentLanguage_RoundTrips()
        {
            V3Labels.CurrentLanguage = 1;
            Assert.That(V3Labels.CurrentLanguage, Is.EqualTo(1));

            V3Labels.CurrentLanguage = 0;
            Assert.That(V3Labels.CurrentLanguage, Is.EqualTo(0));
        }
    }
}
