// ChemLabSim v3 — Screenshot tour (verification tool, inactive by default)
// Drives the real LabV3 UI like a student would (dropdowns, sliders, Mix button) through a
// list of showcase experiments and saves a screenshot after each step, then quits.
// It only runs when the player is started with:   -chemlab-tour <output folder>
// e.g.  ChemLabSim.x86_64 -screen-width 2400 -screen-height 1080 -chemlab-tour /tmp/shots
// Without that argument it does nothing, so it is safe in every build.

using System;
using System.Collections;
using System.IO;
using System.Linq;
using ChemLabSimV3.Views;
using ChemLabSimV3.Views.Bench3D;
using ChemLabSimV3.Views.Layout;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChemLabSimV3.DebugTools
{
    public class ScreenshotTour : MonoBehaviour
    {
        private const string Arg = "-chemlab-tour";

        // name | reagent A | reagent B (empty = one reactant) | medium | °C | stirring | grinding | catalyst | DC current
        //      | amount A | amount B   (optional, in the row's default unit; empty = keep the default)
        //      | checks   (optional, ';'-separated, read from the real UI after the reaction:
        //                  panel~text  Result-tab amounts panel contains text
        //                  phv=text    pH card value is exactly text      phc~text  pH caption contains text
        //                  tc~text     temperature caption contains text   tv=text  temperature value
        //                  labelA~text / labelB~text  reagent label caption contains text)
        //                  vol=text    volume tag next to the beaker
        //      | ready checks (optional, same syntax, read before Mix is pressed)
        //        Each check logs "[TourCheck] PASS|FAIL step: check"; the totals are logged at the end.
        private static readonly string[] Steps =
        {
            "neutralization|HCl|NaOH|Neutral|25|0.6|0.5|0",
            "neutralization_amounts|HCl|NaOH|Neutral|25|0.6|0.5|0|0|20|30|" +
                "panel~Limiting reactant: <b>HCl</b>;panel~excess, 10 mmol left;panel~= 1.169 g;phv=13.30;" +
                "phc~approx.;tc~calculated from ΔH_neut;tv=30.3 °C;labelA~20 mL  ·  1.0 M bench stock;labelB~30 mL  ·  1.0 M bench stock;" +
                "vol=50 mL;volc~volumes taken as additive|" +
                // Before mixing (right after the previous experiment's 25 + 25 mL): only reagent A, 20 mL.
                "vol=20 mL;labelA~20 mL",
            "magnesium_acid_amounts|Mg|HCl|Neutral|25|0.6|0.6|0|0|0.5|10|" +
                "panel~Limiting reactant: <b>HCl</b>;panel~10.08 mg;phv=--;phc~not supported;labelA~0.5 g;labelB~10 mL;vol=10 mL;" +
                // ΔH° = −467.0 kJ/mol; ξ = 5 mmol in 10 mL → 0.005 × 467 000 / (10 × 4.184) = 55.8 K.
                "panel~ΔH° = −467 kJ per mol of reaction;panel~2.335 kJ given out;tc~calculated from ΔH° and your amounts;tv=80.8 °C;beaker=80.8 held",
            "silver_chloride|AgNO3|NaCl|Neutral|25|0.6|0.5|0|0|25|25|" +
                "panel~Exact proportions;panel~= 0.358 g;phv=--;phc~AgNO3 is not a strong acid or base;labelA~0.1 M bench stock;vol=50 mL;" +
                // ΔH° = −65.72 kJ/mol; 2.5 mmol in 50 mL → +0.79 K.
                "panel~ΔH° = −65.72 kJ per mol of reaction;panel~Sources: CODATA Key Values 1989;tc~calculated from ΔH° and your amounts;tv=25.8 °C",
            // Endothermic: the calculated cooling drives the condensation on the beaker.
            "cold_pack|NH4NO3|H2O|Neutral|25|0.5|0.6|0|0|||tc~calculated from ΔH° and your amounts;panel~endothermic: takes in heat;beaker=9.8 held",
            "copper_hydroxide|CuSO4|NaOH|Basic|25|0.6|0.5|0",
            "lead_iodide|KI|Pb(NO3)2|Neutral|25|0.6|0.5|0",
            "zinc_acid|Zn|HCl|Acidic|25|0.6|0.6|0",
            "vinegar_soda|CH3COOH|NaHCO3|Acidic|25|0.6|0.5|0",
            "iron_copper|Fe|CuSO4|Neutral|25|0.6|0.6|0",
            "iron_copper_acidic_hot|Fe|CuSO4|Acidic|52|0.6|0|0|0",
            "magnesium_burn|Mg|O2|Neutral|600|0.6|0.6|0",
            "dilution|HCl|H2O|Neutral|25|0.5|0|0",
            "sodium_chlorine|Na|Cl2|Neutral|300|0.6|0.6|0",
            "baking_soda_heat|NaHCO3||Neutral|300|0.5|0.5|0|0",
            "peroxide_catalyst|H2O2||Neutral|25|0.5|0|1|0",
            "water_no_current|H2O||Neutral|25|0.3|0|0|0",
            "water_electrolysis|H2O||Neutral|25|0.3|0|0|1",
            "copper_electrolysis|CuSO4||Acidic|25|0.3|0|0|1",
            "magnesium_cold|Mg|O2|Neutral|25|0.6|0.6|0|0|||panel~did not proceed;phv=--;phc~did not proceed;tc~set on the hot plate",
        };

        private string outDir;
        private int shot;
        private int checksPassed, checksFailed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string dir = ReadArg();
            if (string.IsNullOrEmpty(dir) || SceneManager.GetActiveScene().name != "LabV3") return;
            // Unattended runs: keep rendering when the window is hidden or the screen is off
            // (with vsync the player can wait minutes for the compositor between frames).
            UnityEngine.Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            UnityEngine.Application.targetFrameRate = 30;
            var go = new GameObject("ScreenshotTour");
            DontDestroyOnLoad(go);
            go.AddComponent<ScreenshotTour>().outDir = dir;
        }

        private static string ReadArg()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, Arg);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(outDir);
            yield return new WaitForSeconds(2.5f);
            yield return Capture("idle");

            foreach (var step in Steps)
            {
                var p = step.Split('|');
                Select("ReagentA", p[1]);
                Select("ReagentB", p[2]);
                Select("ReagentC", null);
                Select("ReagentD", null);
                SetMedium(p[3]);
                SetSlider<TemperatureSliderView>(float.Parse(p[4], System.Globalization.CultureInfo.InvariantCulture));
                SetSlider<StirringSliderView>(float.Parse(p[5], System.Globalization.CultureInfo.InvariantCulture));
                SetSlider<GrindingSliderView>(float.Parse(p[6], System.Globalization.CultureInfo.InvariantCulture));
                SetCatalyst(p[7] == "1");
                SetCurrent(p.Length > 8 && p[8] == "1");
                if (p.Length > 10)
                {
                    SetAmount("ReagentAAmountRow", p[9]);
                    SetAmount("ReagentBAmountRow", p[10]);
                }
                yield return new WaitForSeconds(1f);
                yield return Capture(p[0] + "_a_ready");
                if (p.Length > 12) Verify(p[0] + "_ready", p[12]);

                Click(FindObjectOfType<MixButtonView>()?.GetComponentInChildren<Button>(true));
                yield return new WaitForSeconds(1.2f);
                yield return Capture(p[0] + "_b_early");
                yield return new WaitForSeconds(4f);
                yield return Capture(p[0] + "_c_late");
                if (p.Length > 11)
                {
                    // Like a student, read the instruments once they have settled: the probes ease
                    // towards their value, and a slow frame during the pour can delay the reveal.
                    yield return new WaitForSeconds(2f);
                    yield return ThermometerSteady();
                    Verify(p[0], p[11]);
                }
            }

            yield return TitrationTour();
            yield return FlameTour();
            yield return HeatingTour();
            yield return PeriodicTableTour();
            yield return LearnTour();
            yield return PredictTour();

            Click(GameObject.Find("AssistantTab")?.GetComponent<Button>());
            yield return new WaitForSeconds(0.6f);
            yield return Capture("assistant_what_happened");
            Click(GameObject.Find("WhatToChangeChip")?.GetComponent<Button>());
            yield return new WaitForSeconds(0.6f);
            yield return Capture("assistant_what_to_change");
            Click(GameObject.Find("TryNextChip")?.GetComponent<Button>());
            yield return new WaitForSeconds(0.6f);
            yield return Capture("assistant_try_next");

            Click(GameObject.Find("ProgressTab")?.GetComponent<Button>());
            yield return new WaitForSeconds(0.8f);
            yield return Capture("progress_tab");

            Debug.Log($"[TourCheck] TOTAL passed={checksPassed} failed={checksFailed}");
            Debug.Log($"[ScreenshotTour] Done — {shot} screenshots in {outDir}");
            UnityEngine.Application.Quit(0);
        }

        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(outDir, $"{shot++:00}_{name}.png");
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            Debug.Log($"[ScreenshotTour] {path}");
        }

        private static void Select(string slot, string formula)
        {
            var view = FindObjectsOfType<ReagentDropdownView>(true).FirstOrDefault(v => v.name.StartsWith(slot));
            if (view == null) { Debug.LogWarning($"[ScreenshotTour] {slot} dropdown not found"); return; }
            if (string.IsNullOrEmpty(formula))
            {
                var dd = view.GetComponentInChildren<TMP_Dropdown>(true);
                if (dd != null) dd.value = 0;
                return;
            }
            view.SelectFormula(formula);
            if (view.GetSelectedValue() != formula)
                Debug.LogWarning($"[ScreenshotTour] {slot}: '{formula}' not available");
        }

        private static void SetMedium(string medium)
        {
            var dd = FindObjectOfType<MediumDropdownView>()?.GetComponentInChildren<TMP_Dropdown>(true);
            if (dd == null) return;
            int i = dd.options.FindIndex(o => o.text.IndexOf(medium, StringComparison.OrdinalIgnoreCase) >= 0);
            if (i >= 0) dd.value = i;
        }

        private static void SetSlider<T>(float value) where T : Component
        {
            var slider = FindObjectOfType<T>()?.GetComponentInChildren<Slider>(true);
            if (slider != null) slider.value = value;
        }

        private static void SetCatalyst(bool on)
        {
            // The DC switch is a copy of the catalyst row, so pick the catalyst row by name.
            var view = FindObjectsOfType<CatalystToggleView>(true).FirstOrDefault(v => v.name != "ElectricCurrentRow");
            var toggle = view != null ? view.GetComponentInChildren<Toggle>(true) : null;
            if (toggle != null) toggle.isOn = on;
        }

        private static void SetAmount(string row, string value)
        {
            var view = GameObject.Find(row)?.GetComponent<ReagentAmountView>();
            if (view != null && !string.IsNullOrEmpty(value) && double.TryParse(value, System.Globalization.NumberStyles.Float,
                                                System.Globalization.CultureInfo.InvariantCulture, out double v))
                view.SetValue(v);
        }

        // ── Stage 6: titration and flame test, driven through the Tests tab ──

        private IEnumerator TitrationTour()
        {
            // 20 mL of 1.0 M HCl titrated with 1.0 M NaOH: equivalence at 20.00 mL.
            Select("ReagentA", "HCl");
            Select("ReagentB", "NaOH");
            SetAmount("ReagentAAmountRow", "20");
            yield return new WaitForSeconds(0.8f);
            Click(GameObject.Find("TestsTab")?.GetComponent<Button>());
            Click(GameObject.Find("TitrationSetup")?.GetComponent<Button>());
            Click(GameObject.Find("IndicatorButton")?.GetComponent<Button>());   // phenolphthalein
            yield return new WaitForSeconds(1.5f);
            yield return Capture("titration_a_setup");
            Verify("titration_setup", "tsetup~Equivalence point at <b>20.00 mL</b>;phv=0.00;phc~titration;titr~no titrant added yet");

            for (int i = 0; i < 3; i++) Click(GameObject.Find("Add5")?.GetComponent<Button>());
            yield return new WaitForSeconds(1.5f);
            yield return Capture("titration_b_15ml");
            Verify("titration_15ml", "titr~Added <b>15.00 mL</b>;titr~pH <b>0.85</b>;titr~before the equivalence point;phv=0.85;titr~Phenolphthalein: <b>colourless</b>");

            for (int i = 0; i < 5; i++) Click(GameObject.Find("Add1")?.GetComponent<Button>());
            yield return new WaitForSeconds(1.5f);
            yield return Capture("titration_c_equivalence");
            Verify("titration_20ml", "titr~pH <b>7.00</b>;titr~equivalence point;phv=7.00;tc~calculated from ΔH_neut");

            for (int i = 0; i < 5; i++) Click(GameObject.Find("Add1")?.GetComponent<Button>());
            yield return new WaitForSeconds(1.5f);
            yield return Capture("titration_d_25ml");
            Verify("titration_25ml", "titr~pH <b>13.05</b>;titr~after the equivalence point;phv=13.05;titr~Phenolphthalein: <b>pink</b>;vol=45 mL");

            // A weak acid is outside the model: no curve, a reason instead.
            Select("ReagentA", "CH3COOH");
            yield return new WaitForSeconds(0.8f);
            Click(GameObject.Find("TitrationSetup")?.GetComponent<Button>());
            yield return new WaitForSeconds(1.2f);
            yield return Capture("titration_e_weak_acid");
            Verify("titration_weak_acid", "tsetup~Not supported;tsetup~weak acid;phv=--;phc~weak acid");
        }

        private IEnumerator FlameTour()
        {
            foreach (var test in new[]
            {
                "NaCl|flame~yellow-orange;flame~(Na⁺);tsetup~The reagents or the volume changed;phc~no acid–base model for NaCl",
                "CuSO4|flame~blue-green;flame~(Cu²⁺)",
                "LiOH|flame~crimson;flame~(Li⁺)",
                "KMnO4|flame~Not supported;flame~Mn is not in this flame-test table",
            })
            {
                var p = test.Split('|');
                Select("ReagentA", p[0]);
                yield return new WaitForSeconds(0.6f);
                Click(GameObject.Find("FlameButton")?.GetComponent<Button>());
                yield return new WaitForSeconds(1.5f);
                yield return Capture("flame_" + p[0]);
                Verify("flame_" + p[0], p[1]);
            }
        }

        // ── Heating lab (physical change) ─────────────────────────────

        private IEnumerator HeatingTour()
        {
            Select("ReagentA", "H2O");
            Select("ReagentB", null);
            SetAmount("ReagentAAmountRow", "50");
            SetSlider<TemperatureSliderView>(25f);
            yield return new WaitForSeconds(0.8f);
            Click(GameObject.Find("TestsTab")?.GetComponent<Button>());
            Click(GameObject.Find("HeatButton")?.GetComponent<Button>());
            yield return new WaitForSeconds(1.0f);
            yield return Capture("heating_a_warming");
            Verify("heating_warming", "heat~liquid;heat~heater on;heat~evaporation;tc~heating model");

            // 50 mL of water reaches 99.97 °C after ~52 simulated seconds at 300 W (≈ 2.6 s here at
            // normal frame rates). Wait for the model to say "boiling" (slow frames slow the simulated
            // clock), then let the thermometer settle, as a student would, before reading it.
            var tests = FindObjectOfType<LabTestsView>(true);
            for (float t = 0f; t < 20f && (tests == null || !tests.HeatingText.Contains("boiling")); t += 0.25f)
                yield return new WaitForSeconds(0.25f);
            yield return new WaitForSeconds(2.5f);
            yield return ThermometerSteady();
            yield return Capture("heating_b_boiling");
            Verify("heating_boiling", "heat~boiling;heat~100.0 °C;heat~latent;tv=100.0 °C;phv=--;phc~25 °C only;volc~liquid left");

            Click(GameObject.Find("HeaterOffButton")?.GetComponent<Button>());
            yield return new WaitForSeconds(2f);
            yield return Capture("heating_c_heater_off");
            Verify("heating_off", "heat~liquid;heat~heater off;heat~cools towards room temperature");

            Click(GameObject.Find("HeatResetButton")?.GetComponent<Button>());
            Click(GameObject.Find("FrozenButton")?.GetComponent<Button>());
            Click(GameObject.Find("HeatButton")?.GetComponent<Button>());
            yield return new WaitForSeconds(1f);
            yield return Capture("heating_d_ice_melting");
            Verify("heating_ice", "heat~melting;heat~0.0 °C;heat~mL solid");
            Click(GameObject.Find("HeatResetButton")?.GetComponent<Button>());
            Click(GameObject.Find("FrozenButton")?.GetComponent<Button>());

            // Substances without sourced data, and one that reacts when heated.
            Select("ReagentA", "CH3OH");
            yield return new WaitForSeconds(0.6f);
            Click(GameObject.Find("HeatButton")?.GetComponent<Button>());
            yield return new WaitForSeconds(0.6f);
            yield return Capture("heating_e_no_data");
            Verify("heating_no_data", "heat~Not supported;heat~no sourced heat-capacity");
            Select("ReagentA", "NaHCO3");
            yield return new WaitForSeconds(0.6f);
            Click(GameObject.Find("HeatButton")?.GetComponent<Button>());
            yield return new WaitForSeconds(0.6f);
            Verify("heating_decomposes", "heat~decomposes when heated;heat~chemical change");
            Click(GameObject.Find("HeatResetButton")?.GetComponent<Button>());
        }

        // ── Periodic table ─────────────────────────────────────────────

        // Learn tab: the first guided experiment from start to finish, checked against the live controls.
        private IEnumerator LearnTour()
        {
            ResetConditions();
            Select("ReagentA", "Mg");
            Select("ReagentB", "O2");
            Click(GameObject.Find("LearnTab")?.GetComponent<Button>());
            yield return new WaitForSeconds(0.8f);
            yield return Capture("learn_list");
            Verify("learn_list", "learnlist~learn_neutralisation: Acid-Base;learnlist~learn_electrolysis: Electrochemistry");

            var learn = FindObjectOfType<LearnView>(true);
            learn.Begin("learn_neutralisation");
            yield return new WaitForSeconds(0.6f);
            Verify("learn_step1_waiting", "learn~Acid meets base;learn~Step 1 of 3;learn~Waiting: choose HCl and NaOH");
            Select("ReagentA", "HCl");
            Select("ReagentB", "NaOH");
            yield return new WaitForSeconds(0.8f);
            yield return Capture("learn_step1_done");
            Verify("learn_step1_done", "learn~✓ Done;learn~Look:;learn~about pH 0");

            Click(GameObject.Find("LearnNext")?.GetComponent<Button>());
            yield return new WaitForSeconds(0.4f);
            Verify("learn_step2_waiting", "learn~Step 2 of 3;learn~Waiting: press Mix");
            Click(FindObjectOfType<MixButtonView>()?.GetComponentInChildren<Button>(true));
            yield return new WaitForSeconds(6f);
            yield return Capture("learn_step2_done");
            Verify("learn_step2_done", "learn~✓ Done;learn~55.8 kJ for every mole of water;tc~calculated from ΔH_neut");

            Click(GameObject.Find("LearnNext")?.GetComponent<Button>());
            yield return new WaitForSeconds(0.4f);
            Verify("learn_step3", "learn~Step 3 of 3;learn~Look and read");
            Click(GameObject.Find("LearnNext")?.GetComponent<Button>());
            yield return new WaitForSeconds(0.6f);
            yield return Capture("learn_complete");
            Verify("learn_complete", "learn~Experiment complete;learn~Acid + base → salt + water");
            Click(GameObject.Find("LearnNext")?.GetComponent<Button>());
            yield return new WaitForSeconds(0.6f);
            Verify("learn_list_done", "learnlist~learn_neutralisation: Acid-Base  ·  ●○○○○  ·  about 3 min  ·  <color=#4DD980>✓ done");
        }

        // Predict: answer for HCl + NaOH (25 + 25 mL), mix, and read the comparison.
        private IEnumerator PredictTour()
        {
            ResetConditions();
            Select("ReagentA", "HCl");
            Select("ReagentB", "NaOH");
            SetAmount("ReagentAAmountRow", "25");
            SetAmount("ReagentBAmountRow", "25");
            yield return new WaitForSeconds(0.6f);
            Click(GameObject.Find("PredictButton")?.GetComponent<Button>());
            yield return new WaitForSeconds(0.6f);
            var predict = FindObjectOfType<PredictView>(true);
            predict.Answer(0, false, false);
            yield return new WaitForSeconds(0.4f);
            yield return Capture("predict_questions");
            Verify("predict_open", "predict~what do you think will happen");
            Click(GameObject.Find("PredictMix")?.GetComponent<Button>());
            yield return new WaitForSeconds(5.5f);
            yield return Capture("predict_result");
            Verify("predict_result", "predict~3 of 3 right;predict~calculated from your amounts: +6.7 °C;predict~no product is a gas");
            Click(GameObject.Find("PredictClose")?.GetComponent<Button>());
            yield return new WaitForSeconds(0.4f);
        }

        private static void ResetConditions()
        {
            Select("ReagentC", null);
            Select("ReagentD", null);
            SetSlider<TemperatureSliderView>(25f);
            SetSlider<StirringSliderView>(0.5f);
            SetSlider<GrindingSliderView>(0.5f);
            SetCatalyst(false);
            SetCurrent(false);
        }

        private IEnumerator PeriodicTableTour()
        {
            Click(GameObject.Find("PeriodicTableButton")?.GetComponent<Button>());
            yield return new WaitForSeconds(1f);
            yield return Capture("periodic_a_table");
            Verify("periodic_open", "ptn=118");
            var view = FindObjectOfType<PeriodicTableView>(true);
            if (view == null) yield break;

            view.SetSearch("26");
            yield return new WaitForSeconds(0.6f);
            yield return Capture("periodic_b_iron");
            Verify("periodic_iron", "ptn=1;ptd~Iron;ptd~55.845;ptd~1811 K;ptd~IN THIS LAB;ptd~Reactions in the lab");

            view.SetSearch("118");
            yield return new WaitForSeconds(0.5f);
            yield return Capture("periodic_c_oganesson");
            Verify("periodic_og", "ptd~Oganesson;ptd~predicted;ptd~Standard atomic weight: <b>none</b>");

            view.SetSearch("2");
            yield return new WaitForSeconds(0.4f);
            Verify("periodic_he", "ptd~Helium;ptd~not at 1 atm;ptd~does not solidify");

            view.SetSearch("");
            view.SetCategory("halogen");
            yield return new WaitForSeconds(0.5f);
            yield return Capture("periodic_d_halogens");
            Verify("periodic_halogens", "ptn=6");
            view.SetCategory(null);

            view.SetSearch("11");
            yield return new WaitForSeconds(0.5f);
            yield return Capture("periodic_e_sodium");
            Verify("periodic_na", "ptd~Sodium;ptd~Flame test: <b>intense yellow-orange</b>;ptd~As the element");
            Click(GameObject.Find("UseAsReagentA_Na")?.GetComponent<Button>());
            yield return new WaitForSeconds(0.8f);
            yield return Capture("periodic_f_back_in_lab");
            Verify("periodic_use", "ra=Na;ptopen=no");
        }

        // ── Checks on what the UI actually shows ───────────────────────

        private void Verify(string step, string spec)
        {
            foreach (var check in spec.Split(';'))
            {
                if (string.IsNullOrEmpty(check)) continue;
                bool exact = check.Contains("=") && (check.IndexOf('=') < check.IndexOf('~') || !check.Contains("~"));
                int at = exact ? check.IndexOf('=') : check.IndexOf('~');
                string key = check.Substring(0, at), expected = check.Substring(at + 1);
                string actual = ShownText(key);
                bool ok = actual != null && (exact ? actual == expected : actual.Contains(expected));
                if (ok) checksPassed++; else checksFailed++;
                Debug.Log($"[TourCheck] {(ok ? "PASS" : "FAIL")} {step}: {check}" + (ok ? "" : $"  (shown: '{actual}')"));
            }
        }

        private static string ShownText(string key)
        {
            switch (key)
            {
                case "panel":  return Texts("StoichiometryPanel", "Body");
                case "phv":    return CardLine("PhCard", 1);
                case "phc":    return CardLine("PhCard", 2);
                case "tc":     return CardLine("TemperatureCard", 2);
                case "tv":     return CardLine("TemperatureCard", 1);
                case "labelA": return CardLine("ReagentALabel", 2);
                case "labelB": return CardLine("ReagentBLabel", 2);
                case "titr":   return FindObjectOfType<LabTestsView>(true)?.TitrationText;
                case "tsetup": return Texts("TitrationPanel", "Setup");
                case "flame":  return FindObjectOfType<LabTestsView>(true)?.FlameText;
                case "vol":    return CardLine("VolumeTag", 1);
                case "heat":   return FindObjectOfType<LabTestsView>(true)?.HeatingText;
                case "ptn":    return FindObjectOfType<PeriodicTableView>(true)?.VisibleCount.ToString();
                case "ptd":    return FindObjectOfType<PeriodicTableView>(true)?.DetailText;
                case "ptopen": var pt = FindObjectOfType<PeriodicTableView>(true); return pt == null ? null : pt.IsOpen ? "yes" : "no";
                case "ra":     return FindObjectsOfType<ReagentDropdownView>(true).FirstOrDefault(v => v.name.StartsWith("ReagentA"))?.GetSelectedValue();
                case "volc":   return CardLine("VolumeTag", 2);
                case "learn":  return FindObjectOfType<LearnView>(true)?.StepText;
                case "learnlist": return FindObjectOfType<LearnView>(true)?.ListText;
                case "predict": return FindObjectOfType<PredictView>(true)?.CardText;
                case "beaker": var bench = FindObjectOfType<LabBench3D>(); return bench == null ? null
                    : bench.BeakerTemperatureC.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + (bench.HeatFromAmounts ? " held" : "");
                default:       return null;
            }
        }

        private static Transform Named(string name)
            => FindObjectsOfType<RectTransform>(true).FirstOrDefault(t => t.name == name && t.gameObject.activeInHierarchy);

        private static string Texts(string parent, string child)
        {
            var root = Named(parent);
            var t = root != null ? root.Find(child)?.GetComponent<TMP_Text>() : null;
            return t != null ? t.text : null;
        }

        /// <summary>Line of a pinned card: 0 title, 1 value, 2 caption.</summary>
        /// <summary>
        /// Waits (at most 15 s) until the thermometer has shown the same value for 2 s: the probe eases
        /// towards its value (time constant 0.4 s), and slow frames (phone layout) can leave a large
        /// jump unfinished. One unchanged second can still leave ~0.05 K to go; two leave < 0.005 K.
        /// </summary>
        private static IEnumerator ThermometerSteady()
        {
            string last = CardLine("TemperatureCard", 1);
            int unchanged = 0;
            for (float t = 0f; t < 15f && unchanged < 2; t += 1f)
            {
                yield return new WaitForSeconds(1f);
                string now = CardLine("TemperatureCard", 1);
                unchanged = now == last ? unchanged + 1 : 0;
                last = now;
            }
        }

        private static string CardLine(string card, int line)
        {
            var root = Named(card);
            if (root == null) return null;
            var texts = root.GetComponentsInChildren<TMP_Text>(true);
            return line < texts.Length ? texts[line].text : null;
        }

        private static void SetCurrent(bool on)
        {
            var toggle = GameObject.Find("ElectricCurrentRow")?.GetComponentInChildren<Toggle>(true);
            if (toggle != null) toggle.isOn = on;
        }

        private static void Click(Button button)
        {
            if (button != null && button.interactable) button.onClick.Invoke();
        }
    }
}
