// ChemLabSim v3 — heating lab controller (physical change)
// Owns the one heating experiment: reagent A (its amount from the amount row) in the beaker on
// the hot plate, stepped by PhaseHeatingModel while the heater is on or the sample is cooling.
// Publishes HeatingUpdatedEvent; the Tests panel, the bench and the thermometer only display it.
// Time runs Acceleration × faster than real time (shown to the student) and follows the
// existing speed bar (Time.timeScale, including Pause). Changing reagents/amounts or mixing
// ends the experiment, so no stale state remains.

using System.Linq;
using ChemLabSimV3.Data;
using ChemLabSimV3.Engine;
using ChemLabSimV3.Engine.Chemistry;
using ChemLabSimV3.Events;
using UnityEngine;

namespace ChemLabSimV3.Controllers
{
    public class HeatingLabController : MonoBehaviour
    {
        public const float Acceleration = 20f;                 // simulated seconds per real second (1×)
        public static readonly float[] Powers = { 100f, 300f, 600f, 1000f };
        private const float PublishInterval = 0.1f;

        private LabInputController input;
        private HeatingSample sample;
        private string signature;
        private float sinceLastPublish;

        public bool Active => sample != null;
        public bool HeaterOn { get; private set; }
        public int PowerIndex { get; private set; } = 1;
        public float PowerW => Powers[PowerIndex];
        public bool StartFrozen { get; set; }
        public HeatingSample Sample => sample;
        public string LastMessage { get; private set; } = string.Empty;

        private void Awake()
        {
            EventBus.Subscribe<InputChangedEvent>(OnInputChanged);
            EventBus.Subscribe<ReactionEvaluatedEvent>(OnMix);
            EventBus.Subscribe<ReactionNotFoundEvent>(OnMixNotFound);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<InputChangedEvent>(OnInputChanged);
            EventBus.Unsubscribe<ReactionEvaluatedEvent>(OnMix);
            EventBus.Unsubscribe<ReactionNotFoundEvent>(OnMixNotFound);
        }

        private void OnInputChanged(InputChangedEvent e)
        {
            if (signature != null && Signature() != signature) End("The reagent or its amount changed. Press Heat to start again.");
        }

        private void OnMix(ReactionEvaluatedEvent e) { if (signature != null) End("The beaker was used for a mix."); }
        private void OnMixNotFound(ReactionNotFoundEvent e) { if (signature != null) End("The beaker was used for a mix."); }

        public void NextPower()
        {
            PowerIndex = (PowerIndex + 1) % Powers.Length;
            Publish();
        }

        /// <summary>Start (or resume) heating. Sets the sample up from reagent A on first use.</summary>
        public bool Heat(out string why)
        {
            if (sample == null && !SetUp(out why))
            {
                LastMessage = why;
                EventBus.Publish(new HeatingUpdatedEvent { Active = true, Supported = false, Message = why });
                signature = Signature();
                return false;
            }
            why = string.Empty;
            if (sample.Phase == SamplePhase.Empty) { why = "Nothing is left to heat. Press Reset."; return false; }
            HeaterOn = true;
            Publish();
            return true;
        }

        public void HeaterOff()
        {
            HeaterOn = false;
            Publish();
        }

        /// <summary>Ends the experiment; the beaker goes back to reagent A as selected.</summary>
        public void End(string message = "")
        {
            bool was = signature != null;
            sample = null;
            HeaterOn = false;
            signature = null;
            LastMessage = message;
            if (was) EventBus.Publish(new HeatingUpdatedEvent { Active = false, Message = message });
        }

        private bool SetUp(out string why)
        {
            if (input == null) input = FindObjectOfType<LabInputController>();
            string formula = input != null ? input.CurrentInput.ReagentA?.Trim() : null;
            if (string.IsNullOrEmpty(formula)) { why = "Choose a substance as reagent A."; return false; }
            input.TryGetMaterial(formula, out var mat);
            if (!PhaseHeatingModel.IsSupported(mat, out string reason))
            {
                why = "Not supported: " + reason + "." + (DecomposesOnHeating(formula)
                    ? $" {formula} decomposes when heated — that is a chemical change: set the temperature and press Mix."
                    : string.Empty);
                return false;
            }
            if (!TryMassG(mat, out double grams, out why)) return false;
            float startC = input.CurrentInput.Temperature;
            sample = PhaseHeatingModel.Create(mat, grams, startC, StartFrozen && PhaseHeatingModel.CanStartFrozen(mat), out why);
            if (sample == null) { why = "Not supported: " + why + "."; return false; }
            signature = Signature();
            return true;
        }

        /// <summary>Mass of reagent A from its amount row: mL × density, g, or mol × M.</summary>
        private bool TryMassG(ChemicalMaterial mat, out double grams, out string why)
        {
            grams = double.NaN;
            input.TryGetAmount(0, out double v, out string unit);
            if (double.IsNaN(v) || v <= 0) { why = "Enter a positive amount of reagent A."; return false; }
            switch (unit)
            {
                case "mL": grams = v * mat.densityGPerMl; break;
                case "g": grams = v; break;
                case "mol":
                    var m = MolarMass.Of(mat.formula);
                    if (!m.Success) { why = "No molar mass for " + mat.formula + "."; return false; }
                    grams = v * m.GramsPerMole;
                    break;
                default: why = "Unknown unit for reagent A."; return false;
            }
            why = string.Empty;
            return true;
        }

        private static bool DecomposesOnHeating(string formula)
        {
            var db = AppManager.Instance != null ? AppManager.Instance.ReactionDatabase : null;
            return db?.reactions != null && db.reactions.Any(r => r?.reactants != null && r.reactants.Count == 1
                && r.reactants[0].formula == formula && !ElectricityCondition.IsElectrolysis(r));
        }

        private string Signature()
        {
            if (input == null) input = FindObjectOfType<LabInputController>();
            if (input == null) return string.Empty;
            input.TryGetAmount(0, out double v, out string unit);
            return $"{input.CurrentInput.ReagentA}|{v}|{unit}";
        }

        private void Update()
        {
            if (!PhaseHeatingModel.NeedsStepping(sample, HeaterOn)) return;

            PhaseHeatingModel.Step(sample, Time.deltaTime * Acceleration, HeaterOn ? PowerW : 0f);
            if (sample.Phase == SamplePhase.Empty) HeaterOn = false;
            sinceLastPublish += Time.unscaledDeltaTime;
            if (sinceLastPublish >= PublishInterval || sample.Phase == SamplePhase.Empty)
            {
                sinceLastPublish = 0f;
                Publish();
            }
        }

        private void Publish()
        {
            if (sample == null) return;
            var d = sample.Data;
            float evap = sample.LiquidG > 0 && sample.Phase != SamplePhase.Boiling
                ? Mathf.Clamp01((float)((sample.TemperatureC - PhaseHeatingModel.RoomTemperatureC) / (d.boilingPointC - PhaseHeatingModel.RoomTemperatureC)))
                : 0f;
            EventBus.Publish(new HeatingUpdatedEvent
            {
                Active = true, Supported = true, Formula = sample.Formula,
                TemperatureC = sample.TemperatureC, Phase = sample.Phase,
                LiquidMl = sample.LiquidMl, SolidMl = sample.SolidG > 0 ? sample.SolidMl : 0,
                VaporizedG = sample.VaporizedG, TimeS = sample.TimeS,
                HeaterOn = HeaterOn, PowerW = PowerW, BoilingPointC = d.boilingPointC,
                EvaporationVisual = evap
            });
        }
    }
}
