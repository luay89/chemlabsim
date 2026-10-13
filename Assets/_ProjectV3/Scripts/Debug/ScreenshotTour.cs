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
        private static readonly string[] Steps =
        {
            "neutralization|HCl|NaOH|Neutral|25|0.6|0.5|0",
            "silver_chloride|AgNO3|NaCl|Neutral|25|0.6|0.5|0",
            "copper_hydroxide|CuSO4|NaOH|Basic|25|0.6|0.5|0",
            "lead_iodide|KI|Pb(NO3)2|Neutral|25|0.6|0.5|0",
            "zinc_acid|Zn|HCl|Acidic|25|0.6|0.6|0",
            "vinegar_soda|CH3COOH|NaHCO3|Acidic|25|0.6|0.5|0",
            "iron_copper|Fe|CuSO4|Neutral|25|0.6|0.6|0",
            "magnesium_burn|Mg|O2|Neutral|600|0.6|0.6|0",
            "dilution|HCl|H2O|Neutral|25|0.5|0|0",
            "sodium_chlorine|Na|Cl2|Neutral|300|0.6|0.6|0",
            "baking_soda_heat|NaHCO3||Neutral|300|0.5|0.5|0|0",
            "peroxide_catalyst|H2O2||Neutral|25|0.5|0|1|0",
            "water_no_current|H2O||Neutral|25|0.3|0|0|0",
            "water_electrolysis|H2O||Neutral|25|0.3|0|0|1",
            "copper_electrolysis|CuSO4||Acidic|25|0.3|0|0|1",
            "magnesium_cold|Mg|O2|Neutral|25|0.6|0.6|0|0",
        };

        private string outDir;
        private int shot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string dir = ReadArg();
            if (string.IsNullOrEmpty(dir) || SceneManager.GetActiveScene().name != "LabV3") return;
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
                yield return new WaitForSeconds(1f);
                yield return Capture(p[0] + "_a_ready");

                Click(FindObjectOfType<MixButtonView>()?.GetComponentInChildren<Button>(true));
                yield return new WaitForSeconds(1.2f);
                yield return Capture(p[0] + "_b_early");
                yield return new WaitForSeconds(4f);
                yield return Capture(p[0] + "_c_late");
            }

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
