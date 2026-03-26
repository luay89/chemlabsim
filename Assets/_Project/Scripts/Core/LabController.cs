using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LabController : MonoBehaviour
{
    [Header("UI / Reaction Inputs")]
    [FormerlySerializedAs("reagentA")]
    [SerializeField] private TMP_Dropdown reagentADropdown;
    [FormerlySerializedAs("reagentB")]
    [SerializeField] private TMP_Dropdown reagentBDropdown;
    [FormerlySerializedAs("mediumPH")]
    [SerializeField] private TMP_Dropdown mediumDropdown;
    [FormerlySerializedAs("stirring01")]
    [SerializeField] private Slider stirringSlider;
    [FormerlySerializedAs("grinding01")]
    [SerializeField] private Slider grindingSlider;
    [FormerlySerializedAs("temperatureC")]
    [SerializeField] private Slider temperatureSlider;
    [SerializeField] private Toggle catalystToggle;

    [Header("UI / Actions")]
    [SerializeField] private Button mixButton;
    [SerializeField] private Button backButton;

    [Header("UI / Output")]
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private TextMeshProUGUI historyText;

    [Header("Optional FX")]
    [SerializeField] private ParticleSystem gasFx;

    private ReactionDB db;
    private const int MaxHistoryEntries = 5;
    private const float ResultFeedbackDuration = 0.16f;
    private const float SuccessAlphaBoost = 1f;
    private const float FailureAlphaBoost = 0.75f;
    private readonly List<ExperimentHistoryEntry> experimentHistory = new List<ExperimentHistoryEntry>();
    private Coroutine resultFeedbackRoutine;

    private struct ExperimentHistoryEntry
    {
        public string ReagentA;
        public string ReagentB;
        public string Medium;
        public string Outcome;
    }

    private enum MediumUi
    {
        Neutral = 0,
        Acidic = 1,
        Basic = 2
    }

    private const string MenuSceneName = "Menu";

    private void OnEnable()
    {
        if (!ValidateUiReferences())
            return;

        mixButton.onClick.RemoveListener(OnMix);
        mixButton.onClick.AddListener(OnMix);

        backButton.onClick.RemoveListener(OnBack);
        backButton.onClick.AddListener(OnBack);
    }

    private void OnDisable()
    {
        if (resultFeedbackRoutine != null)
        {
            StopCoroutine(resultFeedbackRoutine);
            resultFeedbackRoutine = null;
        }

        if (mixButton != null)
            mixButton.onClick.RemoveListener(OnMix);

        if (backButton != null)
            backButton.onClick.RemoveListener(OnBack);
    }

    private void Start()
    {
        if (!ValidateUiReferences())
            return;

        if (AppManager.Instance == null)
        {
            SetResult("AppManager is missing.");
            return;
        }

        db = AppManager.Instance.ReactionDatabase;

        if (db == null)
        {
            SetResult("Reaction database is not loaded.");
            return;
        }

        if (db.reactions == null || db.reactions.Count == 0)
        {
            SetResult("Reaction database is empty.");
            return;
        }

        PopulateReagentDropdowns();
        PopulateMediumDropdown();

        stirringSlider.value = 0.5f;
        grindingSlider.value = 0.5f;
        temperatureSlider.value = 25f;

        SetResult("Ready for experiment.");
    }

    private bool ValidateUiReferences()
    {
        bool ok = true;

        if (reagentADropdown == null) { Debug.LogError("LabController: Missing Inspector reference 'reagentADropdown'."); ok = false; }
        if (reagentBDropdown == null) { Debug.LogError("LabController: Missing Inspector reference 'reagentBDropdown'."); ok = false; }
        if (mediumDropdown == null) { Debug.LogError("LabController: Missing Inspector reference 'mediumDropdown'."); ok = false; }
        if (stirringSlider == null) { Debug.LogError("LabController: Missing Inspector reference 'stirringSlider'."); ok = false; }
        if (grindingSlider == null) { Debug.LogError("LabController: Missing Inspector reference 'grindingSlider'."); ok = false; }
        if (temperatureSlider == null) { Debug.LogError("LabController: Missing Inspector reference 'temperatureSlider'."); ok = false; }
        if (catalystToggle == null) { Debug.LogError("LabController: Missing Inspector reference 'catalystToggle'."); ok = false; }
        if (mixButton == null) { Debug.LogError("LabController: Missing Inspector reference 'mixButton'."); ok = false; }
        if (backButton == null) { Debug.LogError("LabController: Missing Inspector reference 'backButton'."); ok = false; }
        if (resultText == null) { Debug.LogError("LabController: Missing Inspector reference 'resultText'."); ok = false; }

        if (!ok)
        {
            Debug.LogError("LabController: One or more UI references are missing. Disabling component.");

            if (resultText != null)
                resultText.text = "UI setup is incomplete.";

            enabled = false;
        }

        return ok;
    }

    private void PopulateReagentDropdowns()
    {
        if (reagentADropdown == null || reagentBDropdown == null)
        {
            Debug.LogError("LabController: Reagent dropdown references are missing.");
            return;
        }

        if (db == null || db.reactions == null)
        {
            Debug.LogWarning("LabController: Reaction database is unavailable. Keeping current reagent dropdown options as fallback.");
            return;
        }

        // NOTE: Dropdown content is sourced from ReactionDB to keep runtime options consistent.
        // NOTE: If localized labels are added later, keep mapping to stable internal IDs.
        var chems = db.reactions
            .SelectMany(r => new[] { r.GetReactantA(), r.GetReactantB() })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        if (chems.Count == 0)
        {
            Debug.LogWarning("LabController: No reagents found in database. Keeping current reagent dropdown options as fallback.");
            return;
        }

        reagentADropdown.ClearOptions();
        reagentBDropdown.ClearOptions();

        reagentADropdown.AddOptions(chems);
        reagentBDropdown.AddOptions(chems);
    }

    private void PopulateMediumDropdown()
    {
        if (mediumDropdown == null)
        {
            Debug.LogError("LabController: Medium dropdown reference is missing.");
            return;
        }

        List<string> mediums = null;

        if (db != null && db.reactions != null)
        {
            mediums = db.reactions
                .Where(r => r != null)
                .Select(r => r.requiredMedium)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(NormalizeMediumLabel)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();
        }

        if (mediums == null || mediums.Count == 0)
        {
            mediums = new List<string> { "Neutral", "Acidic", "Basic" };
        }

        mediumDropdown.ClearOptions();
        mediumDropdown.AddOptions(mediums);
        mediumDropdown.value = 0;
        mediumDropdown.RefreshShownValue();
    }

    private string NormalizeMediumLabel(string medium)
    {
        if (string.IsNullOrWhiteSpace(medium))
            return string.Empty;

        string normalized = medium.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "neutral":
                return "Neutral";
            case "acidic":
                return "Acidic";
            case "basic":
                return "Basic";
            default:
                return medium.Trim();
        }
    }

    private void OnBack()
    {
        SceneManager.LoadScene(MenuSceneName);
    }

    private void OnMix()
    {
        if (AppManager.Instance == null && db == null)
        {
            SetResult("AppManager is missing.");
            return;
        }

        if (db == null || db.reactions == null)
        {
            SetResult("Reaction database is unavailable.");
            return;
        }

        string selectedReagentA = GetSelectedDropdownText(reagentADropdown);
        string selectedReagentB = GetSelectedDropdownText(reagentBDropdown);
        string selectedMedium = GetSelectedDropdownText(mediumDropdown);

        if (!TryValidateSelectionBeforeMix(out string validationMessage))
        {
            AddHistoryEntry(selectedReagentA, selectedReagentB, selectedMedium, "Invalid");
            SetResult(validationMessage);
            return;
        }

        if (!TryBuildEvaluationInput(out ReactionEvaluationInput input))
        {
            AddHistoryEntry(selectedReagentA, selectedReagentB, selectedMedium, "Invalid");
            SetResult("Invalid reaction setup.");
            return;
        }

        ReactionEvaluationResult eval = ReactionEvaluator.Evaluate(input);
        AddHistoryEntry(selectedReagentA, selectedReagentB, selectedMedium, BuildHistoryOutcome(eval));

        if (!eval.IsValid)
        {
            Debug.LogWarning($"LabController: Invalid evaluation result. Summary='{eval.Summary}'");
            LogEvaluationDetails(eval);
            ApplyResultColor(eval);
            SetResult(BuildResultMessage(eval));
            return;
        }

        Debug.Log($"Evaluation Status: {eval.Status}");
        Debug.Log($"Evaluation Summary: {eval.Summary}");
        LogEvaluationDetails(eval);

        string message = BuildResultMessage(eval);
        ApplyResultColor(eval);
        SetResult(message);

        if (gasFx != null && IsSuccessLike(eval))
            gasFx.Play();
    }

    private bool TryValidateSelectionBeforeMix(out string message)
    {
        message = string.Empty;

        if (reagentADropdown == null || reagentBDropdown == null || mediumDropdown == null)
        {
            message = "Invalid selection: please complete all fields.";
            return false;
        }

        bool invalidReagentA = reagentADropdown.options == null ||
                               reagentADropdown.value < 0 ||
                               reagentADropdown.value >= reagentADropdown.options.Count;

        bool invalidReagentB = reagentBDropdown.options == null ||
                               reagentBDropdown.value < 0 ||
                               reagentBDropdown.value >= reagentBDropdown.options.Count;

        bool invalidMedium = mediumDropdown.options == null ||
                             mediumDropdown.value < 0 ||
                             mediumDropdown.value >= mediumDropdown.options.Count;

        if (invalidReagentA || invalidReagentB || invalidMedium)
        {
            message = "Invalid selection: please complete all fields.";
            return false;
        }

        if (reagentADropdown.value == reagentBDropdown.value)
        {
            message = "Invalid setup: please choose two different reagents.";
            return false;
        }

        return true;
    }

    private bool TryBuildEvaluationInput(out ReactionEvaluationInput input)
    {
        input = default;

        if (!TryReadUiValues(out string reagentAName, out string reagentBName, out ReactionMedium selectedMedium,
                out float stirringValue, out float grindingValue, out float temperatureValue, out bool hasCatalyst))
        {
            return false;
        }

        var reaction = db.reactions.FirstOrDefault(r =>
            (r.GetReactantA() == reagentAName && r.GetReactantB() == reagentBName) ||
            (r.GetReactantA() == reagentBName && r.GetReactantB() == reagentAName));

        if (reaction == null)
            return false;

        input = new ReactionEvaluationInput(
            reaction,
            stirringValue,
            grindingValue,
            temperatureValue,
            selectedMedium,
            hasCatalyst
        );

        return true;
    }

    private bool TryReadUiValues(
        out string reagentAName,
        out string reagentBName,
        out ReactionMedium selectedMedium,
        out float stirringValue,
        out float grindingValue,
        out float temperatureValue,
        out bool hasCatalyst)
    {
        reagentAName = string.Empty;
        reagentBName = string.Empty;
        selectedMedium = ReactionMedium.Neutral;
        stirringValue = 0f;
        grindingValue = 0f;
        temperatureValue = 0f;
        hasCatalyst = false;

        if (reagentADropdown.options == null || reagentADropdown.options.Count == 0 ||
            reagentBDropdown.options == null || reagentBDropdown.options.Count == 0)
        {
            return false;
        }

        if (reagentADropdown.value < 0 || reagentADropdown.value >= reagentADropdown.options.Count ||
            reagentBDropdown.value < 0 || reagentBDropdown.value >= reagentBDropdown.options.Count)
        {
            return false;
        }

        reagentAName = reagentADropdown.options[reagentADropdown.value].text;
        reagentBName = reagentBDropdown.options[reagentBDropdown.value].text;

        if (string.IsNullOrWhiteSpace(reagentAName) || string.IsNullOrWhiteSpace(reagentBName))
            return false;

        selectedMedium = MapMediumFromDropdown(mediumDropdown.value);
        stirringValue = stirringSlider.value;
        grindingValue = grindingSlider.value;
        temperatureValue = temperatureSlider.value;
        hasCatalyst = catalystToggle.isOn;

        Debug.Log($"Selected Reagent A: {reagentAName}");
        Debug.Log($"Selected Reagent B: {reagentBName}");
        Debug.Log($"Selected Medium: {selectedMedium}");

        return true;
    }

    private ReactionMedium MapMediumFromDropdown(int dropdownValue)
    {
        return dropdownValue switch
        {
            (int)MediumUi.Acidic => ReactionMedium.Acidic,
            (int)MediumUi.Basic => ReactionMedium.Basic,
            _ => ReactionMedium.Neutral
        };
    }

    private string BuildResultMessage(ReactionEvaluationResult r)
    {
        string headline = BuildResultHeadline(r);
        string details = $"Contact: {r.ContactFactor:F2} | Activation: {r.ActivationThresholdC:F1}°C | Rate: {r.Rate01:F2}";
        string explanation = BuildScientificExplanation(r);
        return $"{headline}\n{details}\n{explanation}";
    }

    private string BuildScientificExplanation(ReactionEvaluationResult r)
    {
        if (!r.IsValid)
            return "The selected setup does not form a valid reaction pair.";

        if (r.MediumMismatch)
            return "The chosen medium does not support this reaction.";

        if (r.ActivationNotReached)
            return "The reaction did not start because the activation energy requirement was not met.";

        if (DidReact(r))
            return "The reaction conditions were sufficient for the reaction to proceed.";

        return "The reaction did not start because the activation energy requirement was not met.";
    }

    private string BuildResultHeadline(ReactionEvaluationResult r)
    {
        if (!r.IsValid)
            return "Invalid reaction setup.";

        if (r.MediumMismatch)
            return "No reaction: medium mismatch.";

        if (r.ActivationNotReached)
            return "No reaction: activation energy not reached.";

        bool reacted = r.Status == ReactionStatus.Success || r.Status == ReactionStatus.Partial;
        if (reacted)
            return "Reaction occurred successfully.";

        return "No reaction: activation energy not reached.";
    }

    private bool DidReact(ReactionEvaluationResult eval)
    {
        return eval.Status == ReactionStatus.Success || eval.Status == ReactionStatus.Partial;
    }

    private bool IsSuccessLike(ReactionEvaluationResult eval)
    {
        return eval.Status == ReactionStatus.Success || eval.Status == ReactionStatus.Partial;
    }

    private string BuildHistoryOutcome(ReactionEvaluationResult r)
    {
        if (!r.IsValid)
            return "Invalid";

        if (r.MediumMismatch)
            return "Medium mismatch";

        if (r.ActivationNotReached)
            return "Activation not reached";

        if (DidReact(r))
            return "Success";

        return "Invalid";
    }

    private void AddHistoryEntry(string reagentA, string reagentB, string medium, string outcome)
    {
        experimentHistory.Add(new ExperimentHistoryEntry
        {
            ReagentA = string.IsNullOrWhiteSpace(reagentA) ? "N/A" : reagentA,
            ReagentB = string.IsNullOrWhiteSpace(reagentB) ? "N/A" : reagentB,
            Medium = string.IsNullOrWhiteSpace(medium) ? "N/A" : medium,
            Outcome = string.IsNullOrWhiteSpace(outcome) ? "Invalid" : outcome
        });

        while (experimentHistory.Count > MaxHistoryEntries)
        {
            experimentHistory.RemoveAt(0);
        }

        string history = BuildHistoryText();
        if (historyText != null)
        {
            historyText.text = history;
        }
        else
        {
            Debug.Log($"LabController history (last {MaxHistoryEntries}):\n{history}");
        }
    }

    private string BuildHistoryText()
    {
        if (experimentHistory.Count == 0)
            return "No experiments yet.";

        var builder = new StringBuilder();

        int lineNumber = 1;
        for (int i = experimentHistory.Count - 1; i >= 0; i--)
        {
            ExperimentHistoryEntry entry = experimentHistory[i];
            builder.Append(lineNumber)
                .Append(") ")
                .Append(entry.ReagentA)
                .Append(" + ")
                .Append(entry.ReagentB)
                .Append(" | ")
                .Append(entry.Medium)
                .Append(" | ")
                .Append(entry.Outcome)
                .AppendLine();
            lineNumber++;
        }

        return builder.ToString().TrimEnd();
    }

    private string GetSelectedDropdownText(TMP_Dropdown dropdown)
    {
        if (dropdown == null || dropdown.options == null || dropdown.value < 0 || dropdown.value >= dropdown.options.Count)
            return "N/A";

        string text = dropdown.options[dropdown.value].text;
        return string.IsNullOrWhiteSpace(text) ? "N/A" : text;
    }

    private void ApplyResultColor(ReactionEvaluationResult r)
    {
        if (resultText == null)
        {
            Debug.LogError("LabController: resultText reference is missing.");
            return;
        }

        if (!r.IsValid)
        {
            resultText.color = Color.yellow;
            TriggerResultVisualFeedback(false);
            return;
        }

        if (r.MediumMismatch || r.ActivationNotReached)
        {
            resultText.color = Color.red;
            TriggerResultVisualFeedback(false);
            return;
        }

        if (DidReact(r))
        {
            resultText.color = Color.green;
            TriggerResultVisualFeedback(true);
            return;
        }

        TriggerResultVisualFeedback(false);
    }

    private void TriggerResultVisualFeedback(bool isSuccess)
    {
        if (resultText == null)
        {
            Debug.LogError("LabController: resultText reference is missing.");
            return;
        }

        if (resultFeedbackRoutine != null)
            StopCoroutine(resultFeedbackRoutine);

        resultFeedbackRoutine = StartCoroutine(PlayResultFeedback(isSuccess));
    }

    private System.Collections.IEnumerator PlayResultFeedback(bool isSuccess)
    {
        if (resultText == null)
        {
            resultFeedbackRoutine = null;
            yield break;
        }

        Color baseColor = resultText.color;
        float targetAlpha = isSuccess ? SuccessAlphaBoost : FailureAlphaBoost;
        Color highlighted = new Color(baseColor.r, baseColor.g, baseColor.b, targetAlpha);

        resultText.color = highlighted;
        yield return new WaitForSeconds(ResultFeedbackDuration);

        if (resultText != null)
            resultText.color = baseColor;

        resultFeedbackRoutine = null;
    }

    private void SetResult(string msg)
    {
        string finalMessage = string.IsNullOrWhiteSpace(msg)
            ? "Result unavailable."
            : msg;

        if (resultText != null)
        {
            resultText.text = finalMessage;
        }
        else
        {
            Debug.LogError("LabController: resultText reference is missing.");
        }

        Debug.Log(finalMessage);
    }

    private void LogEvaluationDetails(ReactionEvaluationResult eval)
    {
        if (eval.DetailedReasons == null || eval.DetailedReasons.Count == 0)
        {
            Debug.Log("LabController: No detailed reasons provided by evaluator.");
            return;
        }

        for (int i = 0; i < eval.DetailedReasons.Count; i++)
        {
            string reason = eval.DetailedReasons[i];
            if (!string.IsNullOrWhiteSpace(reason))
            {
                Debug.Log($"Reason #{i + 1}: {reason}");
            }
        }
    }
}