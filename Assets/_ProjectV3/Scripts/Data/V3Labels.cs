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
