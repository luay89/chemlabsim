// ChemLabSim v3 — QuizPanelView
// Interactive quiz display: question + 3 answer buttons + feedback.
// Pure display + input relay — zero gameplay logic.
// On answer click → publishes QuizOptionSelectedEvent (controller decides correctness).
// Wire Button/TMP refs in the Inspector.

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ChemLabSimV3.Data;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Views
{
    public class QuizPanelView : V3ViewBase
    {
        [Header("Question")]
        [SerializeField] private TextMeshProUGUI questionLabel;

        [Header("Answer Buttons")]
        [SerializeField] private Button answerButtonA;
        [SerializeField] private TextMeshProUGUI answerLabelA;
        [SerializeField] private Button answerButtonB;
        [SerializeField] private TextMeshProUGUI answerLabelB;
        [SerializeField] private Button answerButtonC;
        [SerializeField] private TextMeshProUGUI answerLabelC;

        [Header("Feedback")]
        [SerializeField] private TextMeshProUGUI feedbackLabel;
        [SerializeField] private GameObject feedbackPanel;

        private QuizPanelViewModel currentVm;

        // -- Lifecycle -----------------------------------------

        private void OnEnable()
        {
            if (answerButtonA != null) answerButtonA.onClick.AddListener(() => OnAnswerClicked(0));
            if (answerButtonB != null) answerButtonB.onClick.AddListener(() => OnAnswerClicked(1));
            if (answerButtonC != null) answerButtonC.onClick.AddListener(() => OnAnswerClicked(2));
        }

        private void OnDisable()
        {
            if (answerButtonA != null) answerButtonA.onClick.RemoveAllListeners();
            if (answerButtonB != null) answerButtonB.onClick.RemoveAllListeners();
            if (answerButtonC != null) answerButtonC.onClick.RemoveAllListeners();
        }

        // -- Render --------------------------------------------

        public void Render(QuizPanelViewModel vm)
        {
            currentVm = vm;

            if (!vm.IsVisible)
            {
                Hide();
                return;
            }

            Show();

            if (questionLabel != null)
                questionLabel.text = vm.QuestionText ?? string.Empty;

            SetAnswerButton(answerButtonA, answerLabelA, vm, 0);
            SetAnswerButton(answerButtonB, answerLabelB, vm, 1);
            SetAnswerButton(answerButtonC, answerLabelC, vm, 2);

            bool showFeedback = vm.AnsweredIndex >= 0 && !string.IsNullOrEmpty(vm.FeedbackText);
            if (feedbackPanel != null) feedbackPanel.SetActive(showFeedback);
            if (feedbackLabel != null) feedbackLabel.text = showFeedback ? vm.FeedbackText : string.Empty;
        }

        public void Clear()
        {
            if (questionLabel != null) questionLabel.text = string.Empty;
            if (feedbackLabel != null) feedbackLabel.text = string.Empty;
            if (feedbackPanel != null) feedbackPanel.SetActive(false);
            Hide();
        }

        // -- Helpers -------------------------------------------

        private static void SetAnswerButton(Button btn, TextMeshProUGUI label, QuizPanelViewModel vm, int index)
        {
            if (btn == null) return;

            bool hasOption = vm.AnswerOptions != null && index < vm.AnswerOptions.Count;
            btn.gameObject.SetActive(hasOption);

            if (hasOption && label != null)
                label.text = vm.AnswerOptions[index];

            // Disable buttons after answering
            btn.interactable = vm.AnsweredIndex < 0;
        }

        private void OnAnswerClicked(int index)
        {
            if (currentVm.AnsweredIndex >= 0) return; // already answered

            EventBus.Publish(new QuizOptionSelectedEvent { SelectedIndex = index });
        }
    }
}
