using NUnit.Framework;
using ChemLabSimV3.Engine;

namespace ChemLabSimV3.Tests.EditMode.Domain
{
    /// <summary>
    /// Tests for the ConditionPipeline — verifies condition evaluation logic.
    /// </summary>
    public class ConditionPipelineTests
    {
        [Test]
        public void CreateDefault_ReturnsPipelineWithAllConditions()
        {
            var pipeline = ConditionPipeline.CreateDefault();
            Assert.IsNotNull(pipeline);
            // Default pipeline should have at least Temperature, Medium, Catalyst, SurfaceArea
            Assert.That(pipeline.ConditionCount, Is.GreaterThanOrEqualTo(4));
        }

        [Test]
        public void Execute_WithPerfectConditions_ReturnsHighScore()
        {
            var pipeline = ConditionPipeline.CreateDefault();
            var input = new ConditionInput
            {
                TemperatureC = 50f,
                Stirring = 1f,
                Grinding = 1f,
                Medium = "Neutral",
                HasCatalyst = true,
                EffectiveActivationC = 20f,
                ContactFactor = 1.6f,
                PressureAtm = 1f
            };

            var result = pipeline.Execute(input);
            Assert.IsNotNull(result);
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public void Execute_WithWrongMedium_ReturnsFailure()
        {
            var pipeline = ConditionPipeline.CreateDefault();
            var input = new ConditionInput
            {
                TemperatureC = 50f,
                Stirring = 1f,
                Grinding = 1f,
                Medium = "Acidic",
                HasCatalyst = true,
                EffectiveActivationC = 20f,
                ContactFactor = 1.6f,
                PressureAtm = 1f
            };

            var result = pipeline.Execute(input);
            Assert.IsNotNull(result);
            // Medium condition should reduce success for non-matching media
            Assert.That(result.MediumScore, Is.LessThanOrEqualTo(0.5f));
        }

        [Test]
        public void Execute_WithLowTemperature_BelowActivation()
        {
            var pipeline = ConditionPipeline.CreateDefault();
            var input = new ConditionInput
            {
                TemperatureC = 10f,
                Stirring = 0f,
                Grinding = 0f,
                Medium = "Neutral",
                HasCatalyst = false,
                EffectiveActivationC = 25f,
                ContactFactor = 0.6f,
                PressureAtm = 1f
            };

            var result = pipeline.Execute(input);
            Assert.IsNotNull(result);
            Assert.That(result.TemperatureScore, Is.LessThan(0.5f));
        }

        [Test]
        public void PipelineResult_ContainsAllExpectedScores()
        {
            var pipeline = ConditionPipeline.CreateDefault();
            var input = new ConditionInput
            {
                TemperatureC = 30f,
                Stirring = 0.5f,
                Grinding = 0.5f,
                Medium = "Neutral",
                HasCatalyst = false,
                EffectiveActivationC = 25f,
                ContactFactor = 1.0f,
                PressureAtm = 1f
            };

            var result = pipeline.Execute(input);
            Assert.That(result.TemperatureScore, Is.GreaterThanOrEqualTo(0f));
            Assert.That(result.MediumScore, Is.GreaterThanOrEqualTo(0f));
            Assert.That(result.ContactScore, Is.GreaterThanOrEqualTo(0f));
            Assert.That(result.CatalystScore, Is.GreaterThanOrEqualTo(0f));
            Assert.That(result.OverallScore, Is.GreaterThanOrEqualTo(0f));
        }
    }
}
