// ChemLabSim v3 — digital thermometer and pH meter on the bench
// Two probes dip into the beaker; each probe's reading is shown in a card pinned next to
// the top of the probe on screen (it follows the beaker and stays readable on phones).
// The displays only show what the engine computed:
//   thermometer  set point (EnvironmentChangedEvent) → simulated temperature
//                (SimulationTickEvent / SimulationCompletedEvent) once the reaction is visible,
//                labelled set / estimated / simulated (Thermometry); for a strong acid + strong
//                base neutralisation (mix or titration) the rise calculated from the amounts
//                (Calorimetry), labelled calculated
//   pH meter     pH of reagent A's solution before mixing (PhModel.ForSolution), pH after the
//                reaction from the amounts (PhModel.AfterReaction, only if the reaction ran to
//                completion), or "not supported"
// Like a real probe the display needs a moment to settle; the value it settles on is the
// engine's value.

using System;
using ChemLabSimV3.Controllers;
using ChemLabSimV3.Engine.Chemistry;
using ChemLabSimV3.Events;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemLabSimV3.Views.Bench3D
{
    public class LabInstruments : MonoBehaviour
    {
        private const float ProbeResponseSeconds = 1.2f;

        private PinnedCards.Card tempCard, phCard;
        private LabInputController input;

        // Thermometer state
        private float setPointC = 25f;
        private float simulatedC = float.NaN;
        private TemperatureBasis basis = TemperatureBasis.SetPoint;
        private bool reactionVisible;
        private float shownC = 25f;

        // pH state
        // A struct's default status would read as "calculated pH 0", so start as unsupported.
        private PhResult beforeMix = PhResult.Unsupported("no experiment yet");
        private PhResult afterMix = PhResult.Unsupported("no experiment yet");
        private bool haveAfterMix;
        private ReactionEntry currentReaction;

        // Calculated temperature rise of a neutralisation from the amounts (NaN = not available).
        private double calculatedRiseK = double.NaN;

        // Titration (Stage 6): while one is set up the probes read the titration model.
        private bool titrating;
        private PhResult titrationPh = PhResult.Unsupported("no titration");
        private double titrationRiseK = double.NaN;

        public static LabInstruments Create(Transform parent, PinnedCards cards)
        {
            var go = new GameObject("LabInstruments");
            go.transform.SetParent(parent, false);
            var instruments = go.AddComponent<LabInstruments>();
            instruments.Build(cards);
            return instruments;
        }

        private void Build(PinnedCards cards)
        {
            var steel = Bench3DMeshes.Lit(new Color(0.75f, 0.77f, 0.80f), 0.8f, 0.6f);
            var black = Bench3DMeshes.Lit(new Color(0.05f, 0.05f, 0.06f), 0.4f);

            // Probes: a steel temperature probe and a glass pH electrode, leaning into the beaker.
            var tempProbe = AddRod("TemperatureProbe", new Vector3(0.24f, 0.62f, 0.18f), new Vector3(0f, 0f, -8f), 0.022f, 0.95f, steel);
            var phProbe = AddRod("PhElectrode", new Vector3(-0.22f, 0.62f, 0.2f), new Vector3(0f, 0f, 8f), 0.032f, 0.95f,
                                 Bench3DMeshes.Glass(new Color(0.75f, 0.85f, 0.95f, 0.35f), 3011, CullMode.Back));
            AddRod("PhElectrodeCap", new Vector3(-0.29f, 1.12f, 0.2f), new Vector3(0f, 0f, 8f), 0.04f, 0.12f, black);

            // Reading cards next to the top of each probe (cylinder primitives span y −1…1).
            var tint = new Color(0.02f, 0.06f, 0.05f, 0.88f);
            tempCard = cards.Add("TemperatureCard", "TEMPERATURE", tint, tempProbe, new Vector3(0f, 1f, 0f),
                                 new Vector2(0f, 0.5f), new Vector2(18f, 0f), 210f, 30f);
            phCard = cards.Add("PhCard", "pH", tint, phProbe, new Vector3(0f, 1f, 0f),
                               new Vector2(1f, 0.5f), new Vector2(-18f, 0f), 210f, 30f);
        }

        private Transform AddRod(string name, Vector3 pos, Vector3 euler, float radius, float length, Material mat)
        {
            var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rod.name = name;
            rod.transform.SetParent(transform, false);
            rod.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
            rod.transform.localPosition = pos;
            rod.transform.localRotation = Quaternion.Euler(euler);
            rod.GetComponent<Renderer>().sharedMaterial = mat;
            rod.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Destroy(rod.GetComponent<Collider>());
            return rod.transform;
        }

        // ── Events ─────────────────────────────────────────────────────

        private void OnEnable()
        {
            EventBus.Subscribe<EnvironmentChangedEvent>(OnEnvironment);
            EventBus.Subscribe<ReactionEvaluatedEvent>(OnEvaluated);
            EventBus.Subscribe<ReactionNotFoundEvent>(OnNotFound);
            EventBus.Subscribe<StoichiometryEvaluatedEvent>(OnStoichiometry);
            EventBus.Subscribe<SimulationTickEvent>(OnTick);
            EventBus.Subscribe<SimulationCompletedEvent>(OnCompleted);
            EventBus.Subscribe<TitrationUpdatedEvent>(OnTitration);
            EventBus.Subscribe<HeatingUpdatedEvent>(OnHeating);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EnvironmentChangedEvent>(OnEnvironment);
            EventBus.Unsubscribe<ReactionEvaluatedEvent>(OnEvaluated);
            EventBus.Unsubscribe<ReactionNotFoundEvent>(OnNotFound);
            EventBus.Unsubscribe<StoichiometryEvaluatedEvent>(OnStoichiometry);
            EventBus.Unsubscribe<SimulationTickEvent>(OnTick);
            EventBus.Unsubscribe<SimulationCompletedEvent>(OnCompleted);
            EventBus.Unsubscribe<TitrationUpdatedEvent>(OnTitration);
            EventBus.Unsubscribe<HeatingUpdatedEvent>(OnHeating);
        }

        // Heating lab: the thermometer reads the heating model's temperature.
        private bool heatingActive;
        private double heatingC = double.NaN;

        private void OnHeating(HeatingUpdatedEvent e)
        {
            heatingActive = e.Active && e.Supported;
            heatingC = heatingActive ? e.TemperatureC : double.NaN;
        }

        private void OnTitration(TitrationUpdatedEvent e)
        {
            titrating = e.Active;
            titrationRiseK = e.Supported ? e.Point.TemperatureRiseK : double.NaN;
            titrationPh = e.Supported
                ? PhResult.Calculated(e.Point.Ph, "titration model", e.Point.Approximate)
                : PhResult.Unsupported(e.Message);
        }

        private void OnEnvironment(EnvironmentChangedEvent e) => setPointC = e.Temperature;

        private void OnEvaluated(ReactionEvaluatedEvent e)
        {
            currentReaction = e.Input.reaction;
            bool runs = e.Result.IsValid && e.Result.Status != ReactionStatus.Fail;
            basis = runs ? Thermometry.BasisFor(currentReaction) : TemperatureBasis.SetPoint;
            simulatedC = float.NaN;
        }

        private void OnNotFound(ReactionNotFoundEvent e)
        {
            currentReaction = null;
            basis = TemperatureBasis.SetPoint;
            simulatedC = float.NaN;
            afterMix = PhResult.Unsupported("the substances did not react");
            haveAfterMix = true;
            calculatedRiseK = double.NaN;
        }

        private void OnStoichiometry(StoichiometryEvaluatedEvent e)
        {
            afterMix = PhAfterMix(e);
            haveAfterMix = true;
            calculatedRiseK = RiseAfterMix(e);
        }

        /// <summary>Calculated temperature rise of the mix (K), or NaN when it cannot be calculated.</summary>
        public static double RiseAfterMix(StoichiometryEvaluatedEvent e)
            => e.Complete && Calorimetry.TryNeutralisation(e.Reaction, e.Evaluation, out double rise, out _) ? rise : double.NaN;

        /// <summary>pH of the beaker after a mix: only when the engine ran the reaction to completion.</summary>
        public static PhResult PhAfterMix(StoichiometryEvaluatedEvent e)
        {
            if (!e.Proceeded) return PhResult.Unsupported("the reaction did not proceed, so the mixture is not modelled");
            if (!e.Complete) return PhResult.Unsupported("the reaction only partly proceeded, so the final mixture is not modelled");
            return PhModel.AfterReaction(e.Reaction, e.Evaluation);
        }

        /// <summary>Display text of the pH card for a result.</summary>
        public static void PhDisplay(PhResult ph, out string value, out string caption)
        {
            if (ph.IsCalculated)
            {
                value = ph.Ph.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                caption = ph.Approximate ? "calculated (approx., concentrated)" : "calculated, 25 °C";
            }
            else
            {
                value = "--";
                caption = "not supported: " + ph.Basis;
            }
        }

        private void OnTick(SimulationTickEvent e) { if (e.State != null) simulatedC = e.State.TemperatureC; }
        private void OnCompleted(SimulationCompletedEvent e) { if (e.State != null) simulatedC = e.State.TemperatureC; }

        // ── Called by the bench ────────────────────────────────────────

        /// <summary>New experiment: reagent A alone in the beaker.</summary>
        public void BeginExperiment(string reagentA)
        {
            reactionVisible = false;
            titrating = false;
            haveAfterMix = false;
            calculatedRiseK = double.NaN;
            basis = TemperatureBasis.SetPoint;
            simulatedC = float.NaN;
            if (input == null) input = FindObjectOfType<LabInputController>();

            if (string.IsNullOrWhiteSpace(reagentA))
                beforeMix = PhResult.Unsupported("the beaker is empty");
            else if (reagentA.Trim() == "H2O")
                beforeMix = PhModel.ForSolution("H2O", double.NaN);
            else if (input != null && input.TryGetStockConcentration(reagentA.Trim(), out double molar))
                beforeMix = PhModel.ForSolution(reagentA.Trim(), molar);
            else
                beforeMix = PhResult.Unsupported("not a solution");
        }

        /// <summary>Mix pressed: the readings stay on reagent A until the pour reaches the beaker.</summary>
        public void HideReaction()
        {
            reactionVisible = false;
            titrating = false;
        }

        /// <summary>The poured reagent reached the beaker: show the reaction's readings.</summary>
        public void RevealReaction() => reactionVisible = true;

        // ── Display ────────────────────────────────────────────────────

        private void Update()
        {
            // Calculated (energy balance from the amounts) wins over simulated/estimated.
            double rise = titrating ? titrationRiseK : reactionVisible && haveAfterMix ? calculatedRiseK : double.NaN;
            if (heatingActive && !double.IsNaN(heatingC))
            {
                shownC = Mathf.Lerp(shownC, (float)heatingC, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 3f / ProbeResponseSeconds));
                tempCard.Value.text = $"{shownC:0.0} °C";
                tempCard.Caption.text = "calculated: heating model (energy balance)";
                UpdatePh();
                return;
            }
            bool showCalc = !double.IsNaN(rise);
            bool showSim = !showCalc && !titrating && reactionVisible && basis != TemperatureBasis.SetPoint && !float.IsNaN(simulatedC);
            float target = showCalc ? setPointC + (float)rise : showSim ? simulatedC : setPointC;
            shownC = Mathf.Lerp(shownC, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 3f / ProbeResponseSeconds));
            tempCard.Value.text = $"{shownC:0.0} °C";
            tempCard.Caption.text = Thermometry.Label(showCalc ? TemperatureBasis.Calculated : showSim ? basis : TemperatureBasis.SetPoint);
            UpdatePh();
        }

        private void UpdatePh()
        {
            var ph = titrating ? titrationPh : reactionVisible && haveAfterMix ? afterMix : beforeMix;
            // The pH models are for 25 °C (Kw = 1.0 × 10⁻¹⁴); a heated sample is outside them.
            if (heatingActive && Math.Abs(heatingC - 25.0) > 1.0)
                ph = PhResult.Unsupported("pH is modelled at 25 °C only (Kw changes with temperature)");
            PhDisplay(ph, out string value, out string caption);
            if (titrating && ph.IsCalculated) caption += " · titration";
            phCard.Value.text = value;
            phCard.Caption.text = caption;
        }
    }
}
