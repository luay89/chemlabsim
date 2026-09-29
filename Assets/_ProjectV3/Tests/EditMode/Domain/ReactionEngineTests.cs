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
        private ReactionEntry CreateSampleReaction()
        {
            return new ReactionEntry
            {
                id = "rxn_test_001",
                reactantA = "HCl",
                reactantB = "NaOH",
                product = "NaCl",
                requiredMedium = "Neutral",
                activationTempC = 25f,
                catalystAllowed = true,
                catalystDeltaTempC = 10f,
                reactionType = "Acid-Base Neutralization",
                observation_en = "Salt and water form as the acid and base neutralize.",
                explanation_en = "Hydrochloric acid and sodium hydroxide react to form salt and water.",
                condition_notes = "Use equal molar amounts for complete neutralization."
            };
        }

        private ReactionDB CreateSingleReactionDb()
        {
            return new ReactionDB
            {
                reactions = new System.Collections.Generic.List<ReactionEntry>
                {
                    CreateSampleReaction()
                }
            };
        }

        [Test]
        public void Constructor_WithValidDb_DoesNotThrow()
        {
            var db = new ReactionDB { reactions = new System.Collections.Generic.List<ReactionEntry>() };
            Assert.DoesNotThrow(() => new ReactionEngine(db));
        }

        [Test]
        public void Constructor_WithNullDb_DoesNotThrow()
        {
            // ReactionEngine handles null db gracefully (creates empty registry)
            Assert.DoesNotThrow(() => new ReactionEngine((ReactionDB)null));
        }

        [Test]
        public void Process_WithMatchingReaction_ReturnsSuccess()
        {
            // Arrange
            var engine = new ReactionEngine(CreateSingleReactionDb());
            var request = new MixRequest(
                reagentNames: new System.Collections.Generic.List<string> { "HCl", "NaOH" },
                medium: ReactionMedium.Neutral,
                temperature: 30f,
                stirring: 0.5f,
                grinding: 0.5f,
                hasCatalyst: false
            );

            // Act
            var result = engine.Process(request);

            // Assert
            Assert.IsNotNull(result);
            Assert.That(result.Found, Is.True);
            Assert.That(result.ReactionId, Is.EqualTo("rxn_test_001"));
        }

        [Test]
        public void Process_WithLowTemperature_ReturnsFail()
        {
            // Arrange
            var engine = new ReactionEngine(CreateSingleReactionDb());
            var request = new MixRequest(
                reagentNames: new System.Collections.Generic.List<string> { "HCl", "NaOH" },
                medium: ReactionMedium.Neutral,
                temperature: 10f,  // below activationTempC
                stirring: 0.5f,
                grinding: 0.5f,
                hasCatalyst: false
            );

            // Act
            var result = engine.Process(request);

            // Assert
            Assert.IsNotNull(result);
            Assert.That(result.Found, Is.True);
            Assert.That(result.Status, Is.EqualTo(ReactionStatus.Fail));
        }

        [Test]
        public void Process_WithCatalyst_LowersActivationTemp()
        {
            // Arrange
            var engine = new ReactionEngine(CreateSingleReactionDb());
            var request = new MixRequest(
                reagentNames: new System.Collections.Generic.List<string> { "HCl", "NaOH" },
                medium: ReactionMedium.Neutral,
                temperature: 18f,  // below 25 but above 25-10=15
                stirring: 0.5f,
                grinding: 0.5f,
                hasCatalyst: true   // catalyst lowers activation by 10°C
            );

            // Act
            var result = engine.Process(request);

            // Assert
            Assert.IsNotNull(result);
            Assert.That(result.Found, Is.True);
            // With catalyst, 18°C is above effective activation (15°C)
            Assert.That(result.Status, Is.Not.EqualTo(ReactionStatus.Fail));
        }

        [Test]
        public void Process_WithUnknownReagents_ReturnsNotFound()
        {
            var engine = new ReactionEngine(CreateSingleReactionDb());
            var request = new MixRequest(
                reagentNames: new System.Collections.Generic.List<string> { "Unknown", "Reagent" },
                medium: ReactionMedium.Neutral,
                temperature: 25f,
                stirring: 0.5f,
                grinding: 0.5f,
                hasCatalyst: false
            );

            var result = engine.Process(request);
            Assert.IsNotNull(result);
            Assert.That(result.Found, Is.False);
        }

        [Test]
        public void Registry_Find_ReagentOrderDoesNotMatter()
        {
            var engine = new ReactionEngine(CreateSingleReactionDb());
            var result1 = engine.Registry.Find(new[] { "HCl", "NaOH" });
            var result2 = engine.Registry.Find(new[] { "NaOH", "HCl" });
            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.That(result1.id, Is.EqualTo(result2.id));
        }
    }
}
