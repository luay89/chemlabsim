using NUnit.Framework;
using ChemLabSimV3.Domain.GuidedExperiments;
using ChemLabSimV3.Data;
using System.Collections.Generic;

namespace ChemLabSimV3.Tests.EditMode.Domain
{
    /// <summary>
    /// Tests for GuidedExperimentRunner — pure C# experiment validation logic.
    /// </summary>
    public class GuidedExperimentRunnerTests
    {
        private GuidedExperimentRunner CreateRunner()
        {
            var experiments = new List<GuidedExperimentDef>
            {
                new GuidedExperimentDef
                {
                    Id = "exp_test_001",
                    TitleKey = "Test Experiment",
                    Category = "Test",
                    DifficultyLevel = 1,
                    EstimatedMinutes = 5,
                    RequiredReagents = new List<string> { "HCl", "NaOH" },
                    RewardSkillPoints = 50,
                    Steps = new List<ExperimentStep>
                    {
                        new ExperimentStep
                        {
                            StepNumber = 1,
                            InstructionKey = "step1",
                            ActionType = StepActionType.SelectReagents,
                            Validation = new StepValidation
                            {
                                ExpectedReagents = new List<string> { "HCl", "NaOH" }
                            }
                        },
                        new ExperimentStep
                        {
                            StepNumber = 2,
                            InstructionKey = "step2",
                            ActionType = StepActionType.Mix,
                            Validation = new StepValidation
                            {
                                RequiredMedium = "Neutral",
                                MinTemperature = 25f
                            }
                        },
                        new ExperimentStep
                        {
                            StepNumber = 3,
                            InstructionKey = "step3",
                            ActionType = StepActionType.AnswerQuestion,
                            Validation = null // auto-pass
                        }
                    },
                    Evaluation = new ExperimentEvaluation
                    {
                        PerfectScoreThreshold = 0.9f,
                        PassingScoreThreshold = 0.5f
                    }
                },
                new GuidedExperimentDef
                {
                    Id = "exp_test_002",
                    TitleKey = "Advanced Test",
                    Category = "Test",
                    DifficultyLevel = 3,
                    EstimatedMinutes = 10,
                    PrerequisiteExperimentId = "exp_test_001",
                    RequiredReagents = new List<string> { "AgNO3", "NaCl" },
                    RewardSkillPoints = 100,
                    Steps = new List<ExperimentStep>
                    {
                        new ExperimentStep
                        {
                            StepNumber = 1,
                            InstructionKey = "step1",
                            ActionType = StepActionType.SelectReagents,
                            Validation = new StepValidation
                            {
                                ExpectedReagents = new List<string> { "AgNO3", "NaCl" }
                            }
                        }
                    },
                    Evaluation = new ExperimentEvaluation()
                }
            };

            return new GuidedExperimentRunner(experiments);
        }

        [Test]
        public void Constructor_WithValidData_HasExperiments()
        {
            var runner = CreateRunner();
            Assert.That(runner.Count, Is.EqualTo(2));
        }

        [Test]
        public void Constructor_WithNullList_HasZeroExperiments()
        {
            var runner = new GuidedExperimentRunner(null);
            Assert.That(runner.Count, Is.EqualTo(0));
        }

        [Test]
        public void Get_WithValidId_ReturnsExperiment()
        {
            var runner = CreateRunner();
            var exp = runner.Get("exp_test_001");
            Assert.IsNotNull(exp);
            Assert.That(exp.TitleKey, Is.EqualTo("Test Experiment"));
        }

        [Test]
        public void Get_WithUnknownId_ReturnsNull()
        {
            var runner = CreateRunner();
            Assert.IsNull(runner.Get("nonexistent"));
        }

        [Test]
        public void Get_WithEmptyId_ReturnsNull()
        {
            var runner = CreateRunner();
            Assert.IsNull(runner.Get(""));
        }

        [Test]
        public void GetAll_ReturnsAllExperiments()
        {
            var runner = CreateRunner();
            var all = runner.GetAll();
            Assert.That(new List<GuidedExperimentDef>(all).Count, Is.EqualTo(2));
        }

        [Test]
        public void GetByCategory_ReturnsMatching()
        {
            var runner = CreateRunner();
            var matches = runner.GetByCategory("Test");
            Assert.That(matches.Count, Is.EqualTo(2));
        }

        [Test]
        public void GetByCategory_NoMatch_ReturnsEmpty()
        {
            var runner = CreateRunner();
            var matches = runner.GetByCategory("Nonexistent");
            Assert.That(matches.Count, Is.EqualTo(0));
        }

        [Test]
        public void GetByDifficulty_ReturnsMatching()
        {
            var runner = CreateRunner();
            var matches = runner.GetByDifficulty(1);
            Assert.That(matches.Count, Is.EqualTo(1));
            Assert.That(matches[0].Id, Is.EqualTo("exp_test_001"));
        }

        [Test]
        public void StartSession_WithValidId_ReturnsSession()
        {
            var runner = CreateRunner();
            var session = runner.StartSession("exp_test_001");
            Assert.IsNotNull(session);
            Assert.That(session.ExperimentId, Is.EqualTo("exp_test_001"));
            Assert.That(session.CurrentStepIndex, Is.EqualTo(0));
            Assert.That(session.IsCompleted, Is.False);
        }

        [Test]
        public void StartSession_WithUnknownId_ReturnsNull()
        {
            var runner = CreateRunner();
            Assert.IsNull(runner.StartSession("unknown"));
        }

        [Test]
        public void GetCurrentStep_ReturnsFirstStep()
        {
            var runner = CreateRunner();
            var session = runner.StartSession("exp_test_001");
            var step = runner.GetCurrentStep(session);
            Assert.IsNotNull(step);
            Assert.That(step.StepNumber, Is.EqualTo(1));
        }

        [Test]
        public void GetCurrentStep_AfterCompletion_ReturnsNull()
        {
            var runner = CreateRunner();
            var session = runner.StartSession("exp_test_001");

            // Advance through all steps
            var step1Result = new StepResult { StepNumber = 1, Passed = true, Score = 1f };
            runner.AdvanceStep(session, step1Result);

            var step2Result = new StepResult { StepNumber = 2, Passed = true, Score = 1f };
            runner.AdvanceStep(session, step2Result);

            var step3Result = new StepResult { StepNumber = 3, Passed = true, Score = 1f };
            runner.AdvanceStep(session, step3Result);

            Assert.That(session.IsCompleted, Is.True);
            Assert.IsNull(runner.GetCurrentStep(session));
        }

        [Test]
        public void ValidateStep_WithCorrectReagents_Passes()
        {
            var runner = CreateRunner();
            var session = runner.StartSession("exp_test_001");
            var mixRequest = new MixRequest(
                new List<string> { "HCl", "NaOH" },
                ReactionMedium.Neutral, 25f, 0.5f, 0.5f, false);

            var result = runner.ValidateStep(session, mixRequest, null);
            Assert.IsNotNull(result);
            Assert.That(result.Passed, Is.True);
        }

        [Test]
        public void ValidateStep_WithWrongReagents_Fails()
        {
            var runner = CreateRunner();
            var session = runner.StartSession("exp_test_001");
            var mixRequest = new MixRequest(
                new List<string> { "Wrong", "Reagent" },
                ReactionMedium.Neutral, 25f, 0.5f, 0.5f, false);

            var result = runner.ValidateStep(session, mixRequest, null);
            Assert.IsNotNull(result);
            Assert.That(result.Passed, Is.False);
        }

        [Test]
        public void AdvanceStep_MovesToNextStep()
        {
            var runner = CreateRunner();
            var session = runner.StartSession("exp_test_001");
            var initialStep = session.CurrentStepIndex;

            var stepResult = new StepResult { StepNumber = 1, Passed = true, Score = 1f };
            bool hasMore = runner.AdvanceStep(session, stepResult);

            Assert.That(session.CurrentStepIndex, Is.EqualTo(initialStep + 1));
            Assert.That(hasMore, Is.True);
        }

        [Test]
        public void IsEligible_NoPrerequisite_ReturnsTrue()
        {
            var runner = CreateRunner();
            Assert.That(runner.IsEligible("exp_test_001", new HashSet<string>()), Is.True);
        }

        [Test]
        public void IsEligible_WithPrerequisiteMet_ReturnsTrue()
        {
            var runner = CreateRunner();
            var completed = new HashSet<string> { "exp_test_001" };
            Assert.That(runner.IsEligible("exp_test_002", completed), Is.True);
        }

        [Test]
        public void IsEligible_WithPrerequisiteNotMet_ReturnsFalse()
        {
            var runner = CreateRunner();
            Assert.That(runner.IsEligible("exp_test_002", new HashSet<string>()), Is.False);
        }

        [Test]
        public void PerfectScore_AchievedWhenAllStepsPerfect()
        {
            var runner = CreateRunner();
            var session = runner.StartSession("exp_test_001");

            runner.AdvanceStep(session, new StepResult { StepNumber = 1, Passed = true, Score = 1f });
            runner.AdvanceStep(session, new StepResult { StepNumber = 2, Passed = true, Score = 1f });
            runner.AdvanceStep(session, new StepResult { StepNumber = 3, Passed = true, Score = 1f });

            Assert.That(session.IsCompleted, Is.True);
            Assert.That(session.IsPerfect, Is.True);
            Assert.That(session.TotalScore, Is.GreaterThanOrEqualTo(0.9f));
        }
    }
}
