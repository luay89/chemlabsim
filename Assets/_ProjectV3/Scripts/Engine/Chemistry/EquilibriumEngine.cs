// ChemLabSim v3 — Equilibrium Engine
// True kinetic equilibrium via forward and reverse rate laws.
//
// Classical kinetics model:
//   r_f = k_f(T) × [A]^α × [B]^β           (mol/L/s, forward)
//   r_r = k_r(T) × [C]^γ × [D]^δ           (mol/L/s, reverse)
//   r_net = r_f − r_r
//
// Thermodynamic identity: k_f / k_r = Keq  →  k_r = k_f / Keq
//
// Extent step:
//   Δξ = r_net × V × Δt      (mol)   [rate×volume×time = mol/s × L × s = mol]
//
// Equilibrium detection (Q-based, not time-based):
//   Q = Π[products]^ν / Π[reactants]^ν
//   Equilibrium ⟺ |Q/Keq − 1| < ε    (ε = 2% by default)
//   For irreversible reactions: stop when r_f ≈ 0  (reactants exhausted)
//
// Keq temperature correction (van't Hoff):
//   ln(Keq(T) / Keq_ref) = −ΔH/R × (1/T − 1/T_ref)
//   → Keq(T) = Keq_ref × exp(ΔH/R × (1/T_ref − 1/T))
//   Note: if ΔH < 0 (exothermic), increasing T decreases Keq (less product at equilibrium)

using UnityEngine;

namespace ChemLabSimV3.Engine.Chemistry
{
    /// <summary>Complete kinetic state for one simulation step.</summary>
    public struct EquilibriumState
    {
        /// <summary>Forward rate r_f (mol/L/s).</summary>
        public float ForwardRate;

        /// <summary>Reverse rate r_r (mol/L/s). Zero for irreversible reactions.</summary>
        public float ReverseRate;

        /// <summary>Net rate r_net = r_f − r_r (mol/L/s). Negative means reverse-dominant.</summary>
        public float NetRate;

        /// <summary>Reaction quotient Q = Π[P]^ν / Π[R]^ν. Compare against Keq.</summary>
        public float ReactionQuotient;

        /// <summary>Keq used this step (may differ from reference after van't Hoff adjustment).</summary>
        public float Keq;

        /// <summary>
        /// True when |Q/Keq − 1| &lt; Epsilon for reversible reactions,
        /// or when r_f ≈ 0 for irreversible reactions.
        /// </summary>
        public bool AtEquilibrium;
    }

    public static class EquilibriumEngine
    {
        /// <summary>Fractional tolerance for equilibrium detection: |Q/Keq − 1| &lt; Epsilon.</summary>
        public const float Epsilon = 0.02f;

        /// <summary>Absolute minimum concentration to prevent log(0) in Q. (mol/L)</summary>
        private const float MinConc = 1e-9f;

        /// <summary>Gas constant in kJ/(mol·K).</summary>
        private const float R_kJ = 0.008314f;

        // ═══════════════════════════════════════════════════════
        //  PRIMARY API
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Compute forward rate, reverse rate, net rate, Q, and equilibrium status
        /// for one simulation step.
        ///
        /// r_f = k_f × Π([R_i])^α_i
        /// r_r = (k_f / Keq) × Π([P_j])^β_j   (only for reversible reactions)
        /// r_net = r_f − r_r
        /// </summary>
        /// <param name="kf">Arrhenius-corrected forward rate constant (s⁻¹ or appropriate units).</param>
        /// <param name="keq">Equilibrium constant at current temperature. 0 = irreversible.</param>
        /// <param name="reactantConcs">Current [R_i] concentrations (mol/L).</param>
        /// <param name="reactantOrders">Reaction orders α_i (per-reactant). Stoich if null.</param>
        /// <param name="productConcs">Current [P_j] concentrations (mol/L).</param>
        /// <param name="productOrders">Reaction orders β_j (per-product). Stoich if null.</param>
        /// <param name="isReversible">Whether to compute reverse rate.</param>
        public static EquilibriumState Compute(
            float kf,
            float keq,
            float[] reactantConcs, float[] reactantOrders,
            float[] productConcs,  float[] productOrders,
            bool isReversible)
        {
            var state = new EquilibriumState { Keq = keq };

            // ── Forward rate: k_f × Π([R_i])^α_i ──────────────
            state.ForwardRate = kf;
            if (reactantConcs != null)
            {
                for (int i = 0; i < reactantConcs.Length; i++)
                {
                    float order = (reactantOrders != null && i < reactantOrders.Length)
                        ? reactantOrders[i] : 1f;
                    if (order <= 0f) continue;
                    state.ForwardRate *= Mathf.Pow(Mathf.Max(reactantConcs[i], MinConc), order);
                }
            }
            state.ForwardRate = Mathf.Max(0f, state.ForwardRate);

            // ── Reverse rate: (k_f / Keq) × Π([P_j])^β_j ──────
            if (isReversible && keq > 0f && productConcs != null)
            {
                float kr = kf / Mathf.Max(keq, 1e-12f);
                state.ReverseRate = kr;
                for (int j = 0; j < productConcs.Length; j++)
                {
                    float order = (productOrders != null && j < productOrders.Length)
                        ? productOrders[j] : 1f;
                    if (order <= 0f) continue;
                    state.ReverseRate *= Mathf.Pow(Mathf.Max(productConcs[j], MinConc), order);
                }
                state.ReverseRate = Mathf.Max(0f, state.ReverseRate);
            }

            // ── Net rate ────────────────────────────────────────
            state.NetRate = state.ForwardRate - state.ReverseRate;

            // ── Reaction quotient Q ─────────────────────────────
            state.ReactionQuotient = CalcQ(reactantConcs, reactantOrders, productConcs, productOrders);

            // ── Equilibrium detection ───────────────────────────
            if (isReversible && keq > 0f)
            {
                // Q-based: system is at equilibrium when Q ≈ Keq
                float ratio = state.ReactionQuotient / Mathf.Max(keq, 1e-12f);
                state.AtEquilibrium = Mathf.Abs(ratio - 1f) < Epsilon;
            }
            else
            {
                // Irreversible: equilibrium when forward rate collapses (reactants exhausted)
                state.AtEquilibrium = state.ForwardRate < 1e-12f;
            }

            return state;
        }

        // ═══════════════════════════════════════════════════════
        //  UNIT CONVERSION
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Convert a net rate (mol/L/s) to an extent step (mol) for a given volume and time.
        ///   Δξ = r_net × V × Δt    (mol/L/s × L × s = mol)
        ///
        /// Returns 0 for negative net rate (reverse not handled here — ClampExtent handles it).
        /// </summary>
        public static float RateToExtent(float netRateMolPerLPerS, float volumeL, float dt)
        {
            float raw = netRateMolPerLPerS * volumeL * dt;
            return Mathf.Max(0f, raw);
        }

        // ═══════════════════════════════════════════════════════
        //  REACTION QUOTIENT
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Q = Π([P_j])^β_j / Π([R_i])^α_i
        ///
        /// Uses minimum concentration floor (MinConc) to avoid Q = 0 at t=0.
        /// </summary>
        public static float CalcQ(
            float[] reactantConcs, float[] reactantOrders,
            float[] productConcs,  float[] productOrders)
        {
            float num = 1f;
            float den = 1f;

            if (productConcs != null)
                for (int j = 0; j < productConcs.Length; j++)
                {
                    float order = (productOrders != null && j < productOrders.Length) ? productOrders[j] : 1f;
                    if (order <= 0f) continue;
                    num *= Mathf.Pow(Mathf.Max(productConcs[j], MinConc), order);
                }

            if (reactantConcs != null)
                for (int i = 0; i < reactantConcs.Length; i++)
                {
                    float order = (reactantOrders != null && i < reactantOrders.Length) ? reactantOrders[i] : 1f;
                    if (order <= 0f) continue;
                    den *= Mathf.Pow(Mathf.Max(reactantConcs[i], MinConc), order);
                }

            return num / Mathf.Max(den, 1e-12f);
        }

        // ═══════════════════════════════════════════════════════
        //  VAN'T HOFF Keq TEMPERATURE CORRECTION
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Adjust Keq for temperature change via van't Hoff equation:
        ///   Keq(T) = Keq_ref × exp( (ΔH/R) × (1/T_ref − 1/T) )
        ///
        /// Physical behavior:
        ///   Exothermic (ΔH &lt; 0): raising T decreases Keq (less product)
        ///   Endothermic (ΔH &gt; 0): raising T increases Keq (more product)
        /// </summary>
        /// <param name="keqRef">Reference Keq at refTempC.</param>
        /// <param name="enthalpyKJ">ΔH in kJ/mol. Negative = exothermic.</param>
        /// <param name="tempC">Current temperature (°C).</param>
        /// <param name="refTempC">Reference temperature (°C). Default = 25°C.</param>
        public static float AdjustKeq(float keqRef, float enthalpyKJ, float tempC, float refTempC = 25f)
        {
            if (keqRef <= 0f) return 0f;

            float T    = Mathf.Max(tempC    + 273.15f, 1f);
            float Tref = Mathf.Max(refTempC + 273.15f, 1f);

            // exp clamp prevents overflow/underflow at extreme temperatures
            float exponent = (enthalpyKJ / R_kJ) * (1f / Tref - 1f / T);
            exponent = Mathf.Clamp(exponent, -30f, 30f);

            return keqRef * Mathf.Exp(exponent);
        }
    }
}
