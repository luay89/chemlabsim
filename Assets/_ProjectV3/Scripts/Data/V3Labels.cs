// ChemLabSim v3 — V3Labels
// Static English-only label resolver for UI strings.
// Provides a single source of truth for all user-facing text in the lab.

using System.Collections.Generic;

namespace ChemLabSimV3.Data
{
    public static class V3Labels
    {
        /// <summary>Current language index. Always 0 (English) in this build.</summary>
        public static int CurrentLanguage { get; set; } = 0;

        private static readonly Dictionary<string, string> Table = new Dictionary<string, string>
        {
            // -- Progress labels --
            { "score",        "Score:" },
            { "level",        "Level" },
            { "experiments",  "Experiments:" },
            { "levelUp",      "Level Up!" },

            // -- Challenge / Objective labels --
            { "challenge",    "Challenge" },
            { "objective",    "Objective" },
            { "inProgress",   "In Progress" },
            { "completed",    "Completed!" },

            // -- Mix button --
            { "mix",          "Mix Reagents" },

            // -- Reaction headlines --
            { "success",      "Reaction Successful" },
            { "partial",      "Partial Reaction" },
            { "fail",         "No Reaction Observed" },
            { "invalid",      "Invalid Input" },
            { "notFound",     "Unknown Combination" },

            // -- Result section headers --
            { "reactionStatus",    "Reaction Status" },
            { "productsLabel",     "Products" },
            { "observationsLabel", "Observations" },
            { "explanationLabel",  "Explanation" },
            { "safetyLabel",       "Safety Notes" },
            { "conditionsLabel",   "Conditions" },
            { "reactionTypeLabel", "Reaction Type" },

            // -- Guidance --
            { "selectAndMix",          "Select two reactants and press Mix." },
            { "guidedMode",            "Guided Mode" },
            { "readyMix",              "\u2713 Ready \u2014 press Mix to evaluate the reaction." },
            { "hintExtraReactant",     "\u25b8 Hint: This selection may need an extra reactant in slot 3 or 4." },
            { "tipLowStirGrind",       "\u25b8 Tip: Very low stirring and grinding may reduce contact quality." },
            { "tipLowStirring",        "\u25b8 Tip: Consider increasing stirring for better reagent contact." },
            { "tipLowGrinding",        "\u25b8 Tip: Consider increasing grinding for better reagent contact." },
            { "tipLowTemp",            "\u25b8 Tip: Low temperature may prevent some reactions from activating." },
            { "stepChooseReagents",    "Choose at least two different reactants to begin." },
            { "stepDuplicateReagents", "Each chosen reactant must be different from the others." },
            { "on",  "On" },
            { "off", "Off" },

            // -- Achievement toast --
            { "achievementUnlocked", "Achievement Unlocked!" },

            // -- Notebook / History --
            { "recentExperiments", "Recent Experiments" },
            { "noExperimentsYet",  "No experiments yet." },

            // -- Reaction Identity --
            { "equation",       "Equation:" },
            { "requiredMedium", "Required Medium:" },
            { "activationTemp", "Activation Temp:" },
            { "catalystAllowed","Catalyst:" },
            { "producesGas",    "Produces Gas:" },
            { "yes",            "Yes" },
            { "no",             "No" },
            { "allowed",        "Allowed" },
            { "notAllowed",     "Not Allowed" },

            // -- Reaction Details (factor indicators) --
            { "medium",        "Medium:" },
            { "temperature",   "Temperature:" },
            { "contact",       "Contact:" },
            { "catalyst",      "Catalyst:" },
            { "rate",          "Rate:" },
            { "correct",       "\u2713 Correct" },
            { "mismatch",      "\u2717 Mismatch" },
            { "reached",       "\u2713 Reached" },
            { "notReachedLbl", "\u2717 Not Reached" },
            { "strong",        "\u25cf\u25cf Strong" },
            { "adequate",      "\u2713 Adequate" },
            { "weak",          "\u26a0 Weak" },
            { "applied",       "\u2713 Applied" },
            { "notApplied",    "\u2013 Not Applied" },
            { "notApplicable", "n/a" },

            // -- Scientific Explanation --
            { "scientificExplanation", "Scientific Explanation" },

            // -- Safety Note --
            { "safetyNote",    "Safety Note" },
            { "noSafetyData",  "No safety data." },

            // -- Quiz Hint --
            { "quizHint",      "Think About It" },

            // -- Quiz Questions (contextual) --
            { "quizMediumMismatch",       "Why was the selected medium unsuitable for this reaction?" },
            { "quizActivationNotReached", "What condition must change for this reaction to start?" },
            { "quizCatalystRole",         "What role did the catalyst play in this reaction?" },
            { "quizLowContact",           "How would better grinding or stirring affect the yield?" },
            { "quizPartialReaction",      "What single change would push this to a complete reaction?" },
            { "quizSuccessFactors",       "Which factors made this reaction succeed?" },
            { "quizHelpConditions",       "What conditions would help this reaction proceed?" },

            // -- Quiz Answer Options --
            { "quizMediumMismatch_correct",        "The reaction requires a specific medium that wasn't selected." },
            { "quizMediumMismatch_d1",             "The temperature was too low for the selected medium." },
            { "quizMediumMismatch_d2",             "The medium has no effect on chemical reactions." },
            { "quizActivationNotReached_correct",  "The temperature must reach the activation threshold for molecules to react." },
            { "quizActivationNotReached_d1",       "The medium must be changed to acidic." },
            { "quizActivationNotReached_d2",       "More stirring is always enough to start any reaction." },
            { "quizCatalystRole_correct",          "It lowered the activation energy, allowing the reaction at a lower temperature." },
            { "quizCatalystRole_d1",               "It increased the total amount of product." },
            { "quizCatalystRole_d2",               "It changed the type of products formed." },
            { "quizLowContact_correct",            "Better contact increases the reaction rate and yield." },
            { "quizLowContact_d1",                 "Grinding changes the chemical composition of reactants." },
            { "quizLowContact_d2",                 "Stirring only affects the temperature of the mixture." },
            { "quizPartialReaction_correct",       "Improve contact quality through more stirring or grinding." },
            { "quizPartialReaction_d1",            "Add a completely different medium." },
            { "quizPartialReaction_d2",            "Remove all catalysts from the reaction." },
            { "quizSuccessFactors_correct",        "Correct medium, sufficient temperature, and good contact quality." },
            { "quizSuccessFactors_d1",             "Only the temperature determines reaction success." },
            { "quizSuccessFactors_d2",             "Reactions always succeed if reagents are mixed." },
            { "quizHelpConditions_correct",        "Ensure medium, temperature, and contact all match the reaction's needs." },
            { "quizHelpConditions_d1",             "Only increasing the temperature will fix everything." },
            { "quizHelpConditions_d2",             "This reaction cannot proceed under any conditions." },

            // -- Quiz Feedback --
            { "quizFeedbackCorrect",  "Correct! Well done." },
            { "quizFeedbackWrong",    "Not quite. Think about which conditions affect this reaction." },

            // -- Not-Found Fallback --
            { "unknownReaction",       "Unknown Reaction" },
            { "noVerifiedEquation",    "No verified equation available" },
            { "notFoundExplanation",   "The selected combination does not match a verified reaction in the current database. Try a different pair of reactants." },
            { "noSafetyDataForCombo",  "No specific safety data available for this combination." },
            { "quizNotFoundQuestion",  "Why does this combination not form a valid known reaction?" },

            // -- Demo default / empty state --
            { "noProductsYet",  "" },
            { "conditionsNotMet", "Some required conditions were not met. Check medium, temperature, and contact quality." },
            { "selectAndMixWelcome", "Welcome to the Chemistry Lab! Select two reactants from the dropdowns, adjust the conditions, and press Mix to observe the reaction." },

            // -- Effect / observation labels --
            { "gasEvolution",      "Gas bubbles evolved" },
            { "precipitateFormed", "Precipitate formed" },
            { "colorChanged",      "Color change observed" },
            { "exothermicHeat",    "Exothermic heat released" },
            { "catalystActive",    "Catalyst accelerated reaction" },
            { "noVisibleChange",   "No visible change" },

            // ── Guided Experiments ──
            { "guidedExp_title_acidBase",       "Acid-Base Neutralization" },
            { "guidedExp_desc_acidBase",        "Neutralize hydrochloric acid with sodium hydroxide to produce salt and water." },
            { "guidedExp_title_catalyst",       "Catalyst Exploration" },
            { "guidedExp_desc_catalyst",        "Explore how a catalyst accelerates hydrogen peroxide decomposition using potassium iodide." },
            { "guidedExp_title_precipitation",  "Silver Chloride Precipitation" },
            { "guidedExp_desc_precipitation",   "Create a white precipitate by mixing silver nitrate with sodium chloride." },
            { "guidedExp_title_gas",            "Hydrogen Gas Generation" },
            { "guidedExp_desc_gas",             "Generate hydrogen gas by reacting zinc metal with hydrochloric acid." },
            { "guidedExp_title_tempEffect",     "Temperature Effect on Reaction Rate" },
            { "guidedExp_desc_tempEffect",      "Observe how temperature changes affect the rate of calcium carbonate reaction with acid." },

            // Step instructions
            { "guidedExp_step1_acidBase",       "Step 1: Select HCl (hydrochloric acid) and NaOH (sodium hydroxide) as your reactants." },
            { "guidedExp_hint1_acidBase",       "Hint: Look for HCl in the first dropdown and NaOH in the second dropdown." },
            { "guidedExp_step2_acidBase",       "Step 2: Set the medium to Neutral. This reaction requires a neutral environment." },
            { "guidedExp_hint2_acidBase",       "Hint: Use the Medium dropdown and select 'Neutral'." },
            { "guidedExp_step3_acidBase",       "Step 3: Set the temperature above 25°C to ensure the activation energy is met." },
            { "guidedExp_hint3_acidBase",       "Hint: Drag the temperature slider above 25°C." },
            { "guidedExp_step4_acidBase",       "Step 4: Press the Mix button to start the reaction." },
            { "guidedExp_hint4_acidBase",       "Hint: The Mix button is at the bottom of the control panel." },
            { "guidedExp_step5_acidBase",       "Step 5: Answer the quiz question about what happened." },
            { "guidedExp_hint5_acidBase",       "Hint: Think about what products formed in the reaction." },

            { "guidedExp_step1_catalyst",       "Step 1: Select H₂O₂ (hydrogen peroxide) and KI (potassium iodide)." },
            { "guidedExp_hint1_catalyst",       "Hint: H₂O₂ and KI are found in the reagent dropdowns." },
            { "guidedExp_step2_catalyst",       "Step 2: Keep the temperature below 30°C. We want to show the catalyst effect." },
            { "guidedExp_hint2_catalyst",       "Hint: Set the temperature slider below 30°C." },
            { "guidedExp_step3_catalyst",       "Step 3: Enable the catalyst toggle. KI acts as a catalyst." },
            { "guidedExp_hint3_catalyst",       "Hint: Toggle the catalyst switch to On." },
            { "guidedExp_step4_catalyst",       "Step 4: Press Mix and observe the decomposition." },
            { "guidedExp_hint4_catalyst",       "Hint: Watch how fast the reaction proceeds with the catalyst." },
            { "guidedExp_step5_catalyst",       "Step 5: Answer: How did the catalyst affect the reaction?" },
            { "guidedExp_hint5_catalyst",       "Hint: Think about activation energy." },

            { "guidedExp_step1_precip",         "Step 1: Select AgNO₃ (silver nitrate) and NaCl (sodium chloride)." },
            { "guidedExp_hint1_precip",         "Hint: Silver nitrate and sodium chloride are available in the dropdowns." },
            { "guidedExp_step2_precip",         "Step 2: Set stirring to at least 50% for good mixing." },
            { "guidedExp_hint2_precip",         "Hint: Drag the stirring slider to the right." },
            { "guidedExp_step3_precip",         "Step 3: Press Mix and observe the white precipitate." },
            { "guidedExp_hint3_precip",         "Hint: A white solid (AgCl) should form in the liquid." },
            { "guidedExp_step4_precip",         "Step 4: Observe the precipitate that formed." },
            { "guidedExp_hint4_precip",         "Hint: The white solid is silver chloride (AgCl)." },

            { "guidedExp_step1_gas",            "Step 1: Select Zn (zinc metal) and HCl (hydrochloric acid)." },
            { "guidedExp_hint1_gas",            "Hint: Zinc and hydrochloric acid produce hydrogen gas." },
            { "guidedExp_step2_gas",            "Step 2: Set temperature above 30°C for faster reaction." },
            { "guidedExp_hint2_gas",            "Hint: Higher temperature speeds up gas production." },
            { "guidedExp_step3_gas",            "Step 3: Press Mix and watch for gas bubbles." },
            { "guidedExp_hint3_gas",            "Hint: Look for H₂ gas bubbles rising in the flask." },
            { "guidedExp_step4_gas",            "Step 4: Observe the gas evolution." },
            { "guidedExp_hint4_gas",            "Hint: The bubbles are hydrogen gas (H₂)." },
            { "guidedExp_step5_gas",            "Step 5: Answer: What gas was produced and how can you test for it?" },
            { "guidedExp_hint5_gas",            "Hint: Hydrogen gas produces a 'pop' sound with a lit splint." },

            { "guidedExp_step1_temp",           "Step 1: Select CaCO₃ (calcium carbonate) and HCl (hydrochloric acid)." },
            { "guidedExp_hint1_temp",           "Hint: Calcium carbonate and hydrochloric acid produce CO₂." },
            { "guidedExp_step2_temp",           "Step 2: Set temperature LOW (below 25°C) first." },
            { "guidedExp_hint2_temp",           "Hint: We want to see the slow reaction rate first." },
            { "guidedExp_step3_temp",           "Step 3: Press Mix and observe the reaction rate at low temperature." },
            { "guidedExp_hint3_temp",           "Hint: Note how slowly the CO₂ bubbles form." },
            { "guidedExp_step4_temp",           "Step 4: Now increase temperature above 50°C." },
            { "guidedExp_hint4_temp",           "Hint: Drag the temperature slider well above 50°C." },
            { "guidedExp_step5_temp",           "Step 5: Press Mix again and observe the faster rate." },
            { "guidedExp_hint5_temp",           "Hint: The reaction should be much faster now!" },
            { "guidedExp_step6_temp",           "Step 6: Answer: How did temperature affect the reaction rate?" },
            { "guidedExp_hint6_temp",           "Hint: Think about molecular kinetic energy and collision theory." },

            // Guided experiment feedback
            { "guidedExp_stepPerfect",          "🌟 Perfect! Excellent technique!" },
            { "guidedExp_stepPassed",           "✅ Step passed. Good work!" },
            { "guidedExp_stepFailed",           "❌ Not quite. Try adjusting your conditions." },
            { "guidedExp_error_stepNotFound",   "Error: Step not found. Please restart the experiment." },
            { "guidedExp_completed",            "🎉 Experiment Complete!" },
            { "guidedExp_perfectScore",         "🏆 Perfect Score! You are a true chemist!" },
            { "guidedExp_passingScore",         "Good effort! Try again for a perfect score." },
            { "guidedExp_failingScore",         "Keep practicing! Review the instructions and try again." },
            { "guidedExp_hint",                 "💡 Hint" },
            { "guidedExp_skipStep",             "Skip Step" },
            { "guidedExp_restart",              "Restart Experiment" },
            { "guidedExp_backToList",           "Back to Experiments" },
            { "guidedExp_selectExperiment",     "Select an Experiment" },
            { "guidedExp_noExperiments",        "No experiments available yet." },
            { "guidedExp_locked",               "🔒 Locked" },
            { "guidedExp_timeRemaining",        "Time remaining:" },

            // Evaluation criteria
            { "criterion_correctReagents",      "Correct Reagents" },
            { "criterion_desc_correctReagents", "Did you select the right reagents?" },
            { "criterion_correctMedium",        "Correct Medium" },
            { "criterion_desc_correctMedium",   "Was the medium set correctly?" },
            { "criterion_correctTemp",          "Correct Temperature" },
            { "criterion_desc_correctTemp",     "Was the temperature appropriate?" },
            { "criterion_successfulReaction",   "Successful Reaction" },
            { "criterion_desc_successfulReaction", "Did the reaction complete successfully?" },
            { "criterion_quiz",                 "Quiz Question" },
            { "criterion_desc_quiz",            "Did you answer correctly?" },
            { "criterion_temperature",          "Temperature Control" },
            { "criterion_desc_temperature",     "Was the temperature properly controlled?" },
            { "criterion_catalystUsage",        "Catalyst Usage" },
            { "criterion_desc_catalystUsage",   "Did you use the catalyst correctly?" },
            { "criterion_stirring",             "Stirring" },
            { "criterion_desc_stirring",        "Was stirring adequate?" },
            { "criterion_observation",          "Observation" },
            { "criterion_desc_observation",     "Did you observe the results?" },
            { "criterion_lowTemp",              "Low Temperature Test" },
            { "criterion_desc_lowTemp",         "Did you test at low temperature first?" },
            { "criterion_highTemp",             "High Temperature Test" },
            { "criterion_desc_highTemp",        "Did you test at high temperature?" },
            { "criterion_comparison",           "Comparison" },
            { "criterion_desc_comparison",      "Did you compare rates at different temperatures?" },
        };

        /// <summary>Get the English string for the given key.</summary>
        public static string Get(string key)
        {
            if (Table.TryGetValue(key, out var text))
                return text;
            return key; // fallback: return key itself
        }
    }
}
