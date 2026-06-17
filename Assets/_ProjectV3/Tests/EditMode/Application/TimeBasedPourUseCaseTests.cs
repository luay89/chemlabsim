using NUnit.Framework;
using ChemLabSimV3.Application.UseCases;

namespace ChemLabSimV3.Tests.EditMode.Application
{
    /// <summary>
    /// Tests for TimeBasedPourUseCase — pure C# pour simulation logic.
    /// </summary>
    public class TimeBasedPourUseCaseTests
    {
        private TimeBasedPourUseCase CreateUseCase()
        {
            return new TimeBasedPourUseCase(null, null);
        }

        [Test]
        public void RegisterVessel_WithValidData_DoesNotThrow()
        {
            var uc = CreateUseCase();
            Assert.DoesNotThrow(() =>
                uc.RegisterVessel("bottle_01", "H2O", 100f, 500f, 50f));
        }

        [Test]
        public void RegisterVessel_WithEmptyId_Throws()
        {
            var uc = CreateUseCase();
            Assert.That(() => uc.RegisterVessel("", "H2O", 100f, 500f, 50f),
                Throws.ArgumentException);
        }

        [Test]
        public void GetVolumeMl_WithUnknownVessel_ReturnsZero()
        {
            var uc = CreateUseCase();
            Assert.That(uc.GetVolumeMl("unknown"), Is.EqualTo(0f));
        }

        [Test]
        public void GetVolumeMl_WithEmptyId_ReturnsZero()
        {
            var uc = CreateUseCase();
            Assert.That(uc.GetVolumeMl(""), Is.EqualTo(0f));
        }

        [Test]
        public void GetVolumeMl_ReturnsRegisteredVolume()
        {
            var uc = CreateUseCase();
            uc.RegisterVessel("bottle_a", "HCl", 200f, 500f, 50f);
            Assert.That(uc.GetVolumeMl("bottle_a"), Is.EqualTo(200f).Within(0.001f));
        }

        [Test]
        public void ExecutePour_TransfersFluidBetweenVessels()
        {
            var uc = CreateUseCase();
            uc.RegisterVessel("source", "HCl", 100f, 500f, 100f);
            uc.RegisterVessel("target", "", 0f, 500f, 0f);

            // Pour for 1 second at 100 ml/s
            bool stillPouring = uc.ExecutePour("source", "target", 1f);

            Assert.That(uc.GetVolumeMl("source"), Is.LessThan(100f));
            Assert.That(uc.GetVolumeMl("target"), Is.GreaterThan(0f));
        }

        [Test]
        public void ExecutePour_WithNoLiquid_ReturnsFalse()
        {
            var uc = CreateUseCase();
            uc.RegisterVessel("empty", "HCl", 0f, 500f, 50f);
            uc.RegisterVessel("target", "", 0f, 500f, 0f);

            bool result = uc.ExecutePour("empty", "target", 1f);

            Assert.That(result, Is.False);
        }

        [Test]
        public void ExecutePour_RespectsTargetCapacity()
        {
            var uc = CreateUseCase();
            uc.RegisterVessel("source", "HCl", 100f, 500f, 200f);
            uc.RegisterVessel("small_target", "", 0f, 10f, 0f); // only 10 ml capacity

            // Pour — should stop when target is full
            uc.ExecutePour("source", "small_target", 1f);

            Assert.That(uc.GetVolumeMl("small_target"), Is.LessThanOrEqualTo(10f));
        }

        [Test]
        public void ExecutePour_WithNegativeDelta_ReturnsTrueStillPouring()
        {
            var uc = CreateUseCase();
            uc.RegisterVessel("source", "HCl", 100f, 500f, 50f);
            uc.RegisterVessel("target", "", 0f, 500f, 0f);

            bool result = uc.ExecutePour("source", "target", -1f);

            Assert.That(result, Is.True); // negative delta = nothing to do, but pour active
            Assert.That(uc.GetVolumeMl("source"), Is.EqualTo(100f)); // unchanged
        }

        [Test]
        public void ExecutePour_WithUnknownSource_ReturnsFalse()
        {
            var uc = CreateUseCase();
            uc.RegisterVessel("target", "", 0f, 500f, 0f);

            bool result = uc.ExecutePour("unknown", "target", 1f);
            Assert.That(result, Is.False);
        }

        [Test]
        public void ExecutePour_SourceEmptiesOverMultipleTicks()
        {
            var uc = CreateUseCase();
            uc.RegisterVessel("source", "HCl", 100f, 500f, 75f); // 75 ml/s
            uc.RegisterVessel("target", "", 0f, 500f, 0f);

            // Tick 1: pour 75 ml
            uc.ExecutePour("source", "target", 1f);
            Assert.That(uc.GetVolumeMl("source"), Is.EqualTo(25f).Within(0.001f));

            // Tick 2: pour remaining 25 ml
            bool stillPouring = uc.ExecutePour("source", "target", 1f);
            Assert.That(uc.GetVolumeMl("source"), Is.EqualTo(0f).Within(0.001f));
            Assert.That(uc.GetVolumeMl("target"), Is.EqualTo(100f).Within(0.001f));
            Assert.That(stillPouring, Is.False); // source is now empty
        }
    }
}
