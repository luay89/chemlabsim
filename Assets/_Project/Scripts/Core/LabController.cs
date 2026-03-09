using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LabController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Dropdown reagentA;
    [SerializeField] private TMP_Dropdown reagentB;
    [SerializeField] private Slider stirring01;      // Contact for liquids
    [SerializeField] private Slider grinding01;      // Surface area for solids
    [SerializeField] private Slider temperatureC;    // Activation energy proxy
    [SerializeField] private TMP_Dropdown mediumPH;  // Neutral/Acidic/Basic
    [SerializeField] private Toggle catalystToggle;
    [SerializeField] private Button mixButton;
    [SerializeField] private Button backButton;
    [SerializeField] private TMP_Text resultText;

    [Header("Optional FX")]
    [SerializeField] private ParticleSystem gasFx;

    private ReactionDB db;

    // Keep Medium enum stable (dropdown indices)
    private enum Medium { Neutral = 0, Acidic = 1, Basic = 2 }

    // Unicode direction helpers (prevents English tokens from flipping inside Arabic)
    private const string LRM = "\u200E"; // Left-to-right mark
    private const string RLM = "\u200F"; // Right-to-left mark
    private static string LTR(string s) => $"{LRM}{s}{LRM}";
    private static string RTL(string s) => $"{RLM}{s}{RLM}";

    private void Start()
    {
        // ---- Defensive UI checks
        if (!reagentA || !reagentB || !mixButton || !backButton || !resultText)
        {
            Debug.LogError("[LabController] UI references missing. اربط كل الحقول بالـInspector (Dropdowns/Buttons/Text).", this);
            return;
        }

        // Ensure medium dropdown options are meaningful (avoid default Option A)
        EnsureMediumOptions();

        // 1) Get DB from AppManager
        if (AppManager.Instance == null || AppManager.Instance.ReactionDatabase == null)
        {
            SetResult($"DB غير محمّلة. ارجع إلى {LTR("Boot")} وتأكد {LTR("AppManager")} شغّال.");
            return;
        }

        db = AppManager.Instance.ReactionDatabase;

        if (db.reactions == null || db.reactions.Count == 0)
        {
            SetResult("لا توجد تفاعلات داخل DB.");
            return;
        }

        // 2) Populate dropdowns from reactions endpoints
        List<string> chems = db.reactions
            .SelectMany(r => new[] { r.GetReactantA(), r.GetReactantB() })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        if (chems.Count == 0)
        {
            SetResult("DB محمّلة لكن أسماء المواد فارغة داخل التفاعلات.");
            return;
        }

        reagentA.ClearOptions();
        reagentB.ClearOptions();
        reagentA.AddOptions(chems);
        reagentB.AddOptions(chems);

        // 3) Wire buttons
        mixButton.onClick.RemoveAllListeners();
        mixButton.onClick.AddListener(OnMix);

        backButton.onClick.RemoveAllListeners();
        backButton.onClick.AddListener(() => SceneManager.LoadScene("Menu"));

        // 4) Defaults
        if (stirring01) stirring01.value = 0.5f;
        if (grinding01) grinding01.value = 0.5f;
        if (temperatureC) temperatureC.value = Mathf.Clamp(25f, temperatureC.minValue, temperatureC.maxValue);
        if (mediumPH) mediumPH.value = (int)Medium.Neutral;
        if (catalystToggle) catalystToggle.isOn = false;

        SetResult("جاهز. اختر المواد واضبط الظروف ثم اضغط Mix.");
    }

    private void EnsureMediumOptions()
    {
        if (!mediumPH) return;

        // If options look like defaults (Option A / Option B) or empty => replace with Arabic labels.
        bool looksDefault = mediumPH.options == null || mediumPH.options.Count == 0 ||
                            mediumPH.options.Any(o => o != null && o.text != null && o.text.Trim().ToLowerInvariant().StartsWith("option"));

        if (!looksDefault) return;

        mediumPH.ClearOptions();
        mediumPH.AddOptions(new List<string> { "محايد", "حمضي", "قاعدي" });
        mediumPH.value = (int)Medium.Neutral;
        mediumPH.RefreshShownValue();
    }

    private void OnMix()
    {
        if (db == null || db.reactions == null || db.reactions.Count == 0)
        {
            SetResult("لا توجد تفاعلات في DB.");
            return;
        }

        if (reagentA.options == null || reagentA.options.Count == 0 ||
            reagentB.options == null || reagentB.options.Count == 0)
        {
            SetResult("قوائم المواد فارغة. تأكد من تحميل DB قبل هذه الشاشة.");
            return;
        }

        string a = reagentA.options[reagentA.value].text;
        string b = reagentB.options[reagentB.value].text;

        ReactionEntry rx = FindReaction(a, b);
        if (rx == null)
        {
            SetResult($"لا يوجد تفاعل معروف بين: {LTR(a)} + {LTR(b)} (ضمن بياناتنا الحالية).");
            StopFx();
            return;
        }

        // Read educational controls
        float stirring = stirring01 ? Mathf.Clamp01(stirring01.value) : 0.5f;
        float grinding = grinding01 ? Mathf.Clamp01(grinding01.value) : 0.5f;
        float tempC = temperatureC ? temperatureC.value : 25f;
        Medium med = mediumPH ? (Medium)mediumPH.value : Medium.Neutral;
        bool hasCatalyst = catalystToggle && catalystToggle.isOn;

        // Medium validation
        if (!string.IsNullOrEmpty(rx.requiredMedium))
        {
            if (!MediumMatches(rx.requiredMedium, med))
            {
                StopFx();
                SetResult(
                    "الوسط غير مناسب.\n" +
                    $"هذا التفاعل يحتاج وسط: {LTR(rx.requiredMedium)}.\n" +
                    "اختر الوسط الصحيح ثم أعد المحاولة."
                );
                return;
            }
        }

        // Scientific model (simple but meaningful)
        float contactFactor =
            Mathf.Lerp(0.6f, 1.6f, stirring) *
            Mathf.Lerp(0.6f, 1.6f, grinding);

        float activationThreshold = rx.activationTempC;
        if (hasCatalyst && rx.catalystAllowed)
            activationThreshold -= rx.catalystDeltaTempC;

        if (tempC < activationThreshold)
        {
            StopFx();
            SetResult(
                "التفاعل بطيء/متوقف.\n" +
                "السبب: طاقة التنشيط غير كافية.\n" +
                $"ارفع الحرارة حتى ≥ {LTR($"{activationThreshold:0}°C")}" +
                (rx.catalystAllowed ? " أو فعّل المحفّز لتقليل طاقة التنشيط." : ".")
            );
            return;
        }

        float tempFactor = Mathf.InverseLerp(activationThreshold, activationThreshold + 30f, tempC);
        float rate = Mathf.Clamp01(0.25f + 0.55f * tempFactor) * Mathf.Clamp01(contactFactor);

        string productsText = GetProductsText(rx);
        string explain =
            $"تم التفاعل: {LTR(a)} + {LTR(b)}\n" +
            $"النواتج: {LTR(productsText)}\n\n" +
            "تفسير علمي:\n" +
            $"- التلامس (Contact): {(contactFactor >= 1.0f ? "جيد" : "ضعيف")} (تحريك/طحن)\n" +
            $"- طاقة التنشيط: متحققة عند {LTR($"{tempC:0}°C")}\n" +
            $"- الوسط: {MediumLabel(med)}\n" +
            $"- محفّز: {(hasCatalyst ? "موجود" : "غير موجود")}\n\n" +
            $"شدة/سرعة تقريبية: {LTR($"{rate * 100f:0}%")}";

        SetResult(explain);

        if (rx.GetProducesGas()) PlayGasFx(rate);
        else StopFx();
    }

    private ReactionEntry FindReaction(string a, string b)
    {
        // Match both orders
        return db.reactions.FirstOrDefault(r =>
            (MatchesChemical(r.GetReactantA(), a) && MatchesChemical(r.GetReactantB(), b)) ||
            (MatchesChemical(r.GetReactantA(), b) && MatchesChemical(r.GetReactantB(), a)));
    }

    private static bool MatchesChemical(string x, string y)
    {
        return string.Equals(x?.Trim(), y?.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }

    private static string GetProductsText(ReactionEntry rx)
    {
        if (rx?.products != null && rx.products.Count > 0)
        {
            var formulas = rx.products
                .Where(p => p != null && !string.IsNullOrWhiteSpace(p.formula))
                .Select(p => p.formula.Trim())
                .Distinct()
                .ToList();

            if (formulas.Count > 0)
                return string.Join(" + ", formulas);
        }

        return rx?.GetPrimaryProduct() ?? "غير معروف";
    }

    private bool MediumMatches(string required, Medium actual)
    {
        string req = required.Trim().ToLowerInvariant();
        if (req == "acidic") return actual == Medium.Acidic;
        if (req == "basic") return actual == Medium.Basic;
        if (req == "neutral") return actual == Medium.Neutral;
        return true; // unknown requirement => don't block
    }

    private string MediumLabel(Medium m)
    {
        return m switch
        {
            Medium.Neutral => "محايد",
            Medium.Acidic => "حمضي",
            Medium.Basic => "قاعدي",
            _ => "غير معروف"
        };
    }

    private void SetResult(string msg)
    {
        if (resultText) resultText.text = RTL(msg); // wrap whole message as RTL for UI
        Debug.Log("[Lab] " + msg.Replace("\n", " | "), this);
    }

    private void PlayGasFx(float intensity01)
    {
        if (!gasFx) return;
        var main = gasFx.main;
        main.startSpeed = Mathf.Lerp(0.5f, 2.5f, intensity01);
        main.startLifetime = Mathf.Lerp(0.5f, 2.0f, intensity01);
        if (!gasFx.isPlaying) gasFx.Play();
    }

    private void StopFx()
    {
        if (gasFx && gasFx.isPlaying)
            gasFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
