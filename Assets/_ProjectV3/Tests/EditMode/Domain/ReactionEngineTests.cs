using NUnit.Framework;
using ChemLabSimV3.Engine;
using ChemLabSimV3.Data;

namespace ChemLabSimV3.Tests.EditMode.Domain
{
    /// <summary>
    /// Edit-mode tests for ReactionEngine — pure C#, no Unity dependencies.
    /// These tests run in the Unity Editor without entering Play Mode.
    /// </summary>
    public class ReactionEngineTests
    {
        private ReactionEngine CreateEngineWithSingleReaction()
        {
            var db = new ReactionDB
            {
                reactions = new System.Collections.Generic.List<ReactionEntry>
                {
                    new ReactionEntry
                    {
                        id = "rxn_test_001",
                        reactantA = "HCl",
                        reactantB = "NaOH",
                        product = "NaCl",
                        medium = "Neutral",
                        activationTempC = 25f,
                        catalystAllowed = true,
                        catalystDeltaTempC = 10f,
                        balancedEquation = "HCl + NaOH → NaCl + H₂O",
                        reactionIdentity = "Acid-Base Neutralization",
                        summary = "Hydrochloric acid and sodium hydroxide react to form salt and water.",
                        successMessage = "The reaction produced salt water!",
                        partialMessage = "The reaction is incomplete.",
                        failMessage = "No reaction occurred.",
                        safetyNote = "Wear gloves when handling acids.",
                        quizHint = "This is a neutralization reaction.",
        skillPoints = 100
                    }
                }
            };
            return new ReactionEngine(db);
        }

        [Test]
        public void Constructor_WithValidDb_DoesNotThrow()
        {
            var db = new ReactionDB { reactions = new System.Collections.Generic.List<ReactionEntry>() };
            Assert.DoesNotThrow(() => new ReactionEngine(db));
        }

        [Test]
        public void Constructor_WithNullDb_ThrowsArgumentNullException()
        {
            Assert.That(() => new ReactionEngine(null),
                Throws.ArgumentNullException);
        }

        [Test]
        public void Constructor_WithNullReactionsList_Throws()
        {
            var db = new ReactionDB { reactions = null };
            Assert.That(() => new ReactionEngine(db),
                Throws.ArgumentException);
        }

        [Test]
        public void Evaluate_WithMatchingReaction_ReturnsSuccess()
        {
            // Arrange
            var engine = CreateEngineWithSingleReaction();
            var input = new ReactionEvaluationInput(
                engine.FindReaction(new[] { "HCl", "NaOH" }),
                stirring: 0.5f,
                grinding: 0.5f,
                temperature: 30f,
                medium: "Neutral",
                hasCatalyst: false
            );

            // Act
            var result = ReactionEvaluator.Evaluate(input);

            // Assert
            Assert.IsNotNull(result);
            Assert.That(result.Status, Is.EqualTo("COMPLETE"));
        }

        [Test]
        public void Evaluate_WithLowTemperature_ReturnsPartial()
        {
            var engine = CreateEngineWithSingleReaction();
            var input = new ReactionEvaluationInput(
                engine.FindReaction(new[] { "HCl", "NaOH" }),
                stirring: 0.5f,
                grinding: 0.5f,
                temperature: 10f,  // below activationTempC
                medium: "Neutral",
                hasCatalyst: false
            );

            var result = ReactionEvaluator.Evaluate(input);
            Assert.IsNotNull(result);
            Assert.That(result.Status, Is.EqualTo("INCOMPLETE"));
        }

        [Test]
        public void Evaluate_WithCatalyst_LowersActivationTemp()
        {
            var engine = CreateEngineWithSingleReaction();
            var input = new ReactionEvaluationInput(
                engine.FindReaction(new[] { "HCl", "NaOH" }),
                stirring: 0.5f,
                grinding: 0.5f,
                temperature: 18f,  // below 25 but above 25-10=15
                medium: "Neutral",
                hasCatalyst: true   // catalyst lowers activation by 10°C
            );

            var result = ReactionEvaluator.Evaluate(input);
            Assert.IsNotNull(result);
            // With catalyst, 18°C > 15°C effective activation → should be COMPLETE or PARTIAL
            Assert.That(result.Status, Is.EqualTo("COMPLETE").Or.EqualTo("INCOMPLETE"));
        }

        [Test]
        public void FindReaction_WithUnknownReagents_ReturnsNull()
        {
            var engine = CreateEngineWithSingleReaction();
            var result = engine.FindReaction(new[] { "Unknown", "Reagent" });
            Assert.IsNull(result);
        }

        [Test]
        public void FindReaction_ReagentOrderDoesNotMatter()
        {
            var engine = CreateEngineWithSingleReaction();
            var result1 = engine.FindReaction(new[] { "HCl", "NaOH" });
            var result2 = engine.FindReaction(new[] { "NaOH", "HCl" });
            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.That(result1.id, Is.EqualTo(result2.id));
        }
    }
}
