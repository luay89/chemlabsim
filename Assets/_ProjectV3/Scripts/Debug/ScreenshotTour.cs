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
        //                  tc~text     temperature caption contains text
        //                  labelA~text / labelB~text  reagent label caption contains text)
        //        Each check logs "[TourCheck] PASS|FAIL step: check"; the totals are logged at the end.
        private static readonly string[] Steps =
        {
            "neutralization|HCl|NaOH|Neutral|25|0.6|0.5|0",
            "neutralization_amounts|HCl|NaOH|Neutral|25|0.6|0.5|0|0|20|30|" +
                "panel~Limiting reactant: <b>HCl</b>;panel~excess, 10 mmol left;panel~= 1.169 g;phv=13.30;" +
                "phc~approx.;tc~estimated;labelA~20 mL  ·  1.0 M bench stock;labelB~30 mL  ·  1.0 M bench stock",
            "magnesium_acid_amounts|Mg|HCl|Neutral|25|0.6|0.6|0|0|0.5|10|" +
                "panel~Limiting reactant: <b>HCl</b>;panel~10.08 mg;phv=--;phc~not supported;labelA~0.5 g;labelB~10 mL",
            "silver_chloride|AgNO3|NaCl|Neutral|25|0.6|0.5|0|0|25|25|" +
                "panel~Exact proportions;panel~= 0.358 g;phv=--;phc~AgNO3 is not a strong acid or base;labelA~0.1 M bench stock",
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

                Click(FindObjectOfType<MixButtonView>()?.GetComponentInChildren<Button>(true));
                yield return new WaitForSeconds(1.2f);
                yield return Capture(p[0] + "_b_early");
                yield return new WaitForSeconds(4f);
                yield return Capture(p[0] + "_c_late");
                if (p.Length > 11) Verify(p[0], p[11]);
            }

            yield return TitrationTour();
            yield return FlameTour();

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
            yield return new WaitForSeconds(1.5f);
            yield return Capture("titration_a_setup");
            Verify("titration_setup", "tsetup~Equivalence point at <b>20.00 mL</b>;phv=0.00;phc~titration;titr~no titrant added yet");

            for (int i = 0; i < 3; i++) Click(GameObject.Find("Add5")?.GetComponent<Button>());
            yield return new WaitForSeconds(1.5f);
            yield return Capture("titration_b_15ml");
            Verify("titration_15ml", "titr~Added <b>15.00 mL</b>;titr~pH <b>0.85</b>;titr~before the equivalence point;phv=0.85");

            for (int i = 0; i < 5; i++) Click(GameObject.Find("Add1")?.GetComponent<Button>());
            yield return new WaitForSeconds(1.5f);
            yield return Capture("titration_c_equivalence");
            Verify("titration_20ml", "titr~pH <b>7.00</b>;titr~equivalence point;phv=7.00");

            for (int i = 0; i < 5; i++) Click(GameObject.Find("Add1")?.GetComponent<Button>());
            yield return new WaitForSeconds(1.5f);
            yield return Capture("titration_d_25ml");
            Verify("titration_25ml", "titr~pH <b>13.05</b>;titr~after the equivalence point;phv=13.05");

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
                case "labelA": return CardLine("ReagentALabel", 2);
                case "labelB": return CardLine("ReagentBLabel", 2);
                case "titr":   return FindObjectOfType<LabTestsView>(true)?.TitrationText;
                case "tsetup": return Texts("TitrationPanel", "Setup");
                case "flame":  return FindObjectOfType<LabTestsView>(true)?.FlameText;
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
