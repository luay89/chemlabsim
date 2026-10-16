// ChemLabSim v3 — Periodic Table & Element Explorer
// A full-screen panel over the lab: all 118 elements in their places (lanthanides and actinides
// in their own rows), search by name / symbol / atomic number, category filter, and a detail
// panel for the selected element. Data: ElementTable (Resources/Data/elements.json) — every value
// is shown with its status (listed, estimated, predicted, not at 1 atm, unknown, not sourced).
// "In this lab" links the element to the materials (substances) and reactions that already exist
// in the lab's databases; nothing here can run a reaction by itself — "Use as reagent A" only
// selects the material in Lab Controls.
// English UI; text comes from the data's English fields and can later be keyed by nameKey.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ChemLabSimV3.Controllers;
using ChemLabSimV3.Data;
using ChemLabSimV3.Engine.Chemistry;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemLabSimV3.Views.Layout
{
    public class PeriodicTableView : MonoBehaviour
    {
        private const float HeaderHeight = 44f, ChipsHeight = 30f, Pad = 10f;

        private static readonly Dictionary<string, Color> CategoryColours = new Dictionary<string, Color>
        {
            { "alkali_metal",          new Color(0.79f, 0.35f, 0.31f) },
            { "alkaline_earth_metal",  new Color(0.84f, 0.57f, 0.29f) },
            { "transition_metal",      new Color(0.66f, 0.58f, 0.33f) },
            { "post_transition_metal", new Color(0.36f, 0.58f, 0.56f) },
            { "metalloid",             new Color(0.33f, 0.60f, 0.43f) },
            { "other_nonmetal",        new Color(0.29f, 0.52f, 0.78f) },
            { "halogen",               new Color(0.53f, 0.45f, 0.80f) },
            { "noble_gas",             new Color(0.70f, 0.38f, 0.64f) },
            { "lanthanide",            new Color(0.27f, 0.62f, 0.69f) },
            { "actinide",              new Color(0.70f, 0.45f, 0.35f) },
        };

        private ElementTable table;
        private RectTransform grid, detail, detailContent, chips;
        private TMP_InputField search;
        private TextMeshProUGUI detailText, countText;
        private readonly Dictionary<int, (RectTransform Rect, Image Bg, Outline Ring)> cells = new Dictionary<int, (RectTransform, Image, Outline)>();
        private readonly List<GameObject> placeholders = new List<GameObject>();
        private readonly List<GameObject> actionButtons = new List<GameObject>();
        private readonly Dictionary<string, Image> chipImages = new Dictionary<string, Image>();
        private string category;
        private Vector2 laidOutFor;
        private TMP_FontAsset font;

        public ElementRecord Selected { get; private set; }
        public int VisibleCount { get; private set; }
        public string DetailText => detailText != null && detail.gameObject.activeSelf ? detailText.text : string.Empty;
        public bool IsOpen => gameObject.activeSelf;

        // ── Construction ───────────────────────────────────────────────

        public static PeriodicTableView Create(RectTransform parent, TMP_FontAsset font)
        {
            var go = new GameObject("PeriodicTableScreen", typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0.03f, 0.045f, 0.07f, 1f);   // opaque: in linear colour space even 2 % shows through
            // Its own canvas, drawn above the lab panels (some of which have canvases of their own).
            var canvas = go.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
            go.AddComponent<GraphicRaycaster>();
            var view = go.AddComponent<PeriodicTableView>();
            view.font = font;
            view.Build(rect);
            go.SetActive(false);
            return view;
        }

        private void Build(RectTransform root)
        {
            // Header: title, search, count, close.
            var header = Stretch("Header", root, new Vector2(0, 1), new Vector2(1, 1), new Vector2(Pad, -Pad - HeaderHeight), new Vector2(-Pad, -Pad));
            var hl = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 10f;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = true;
            var title = Label(header, "Title", "PERIODIC TABLE  ·  ELEMENT EXPLORER", 15f, LabTheme.TitleColor, FontStyles.Bold);
            title.characterSpacing = 3;
            title.gameObject.AddComponent<LayoutElement>().preferredWidth = 360f;
            search = SearchField(header);
            countText = Label(header, "Count", "118 elements", 13f, LabTheme.MutedText, FontStyles.Normal);
            countText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            MakeButton(header, "ClosePeriodicTable", "Close", LabTheme.ControlColor, 90f, Close);

            // Category filter chips.
            chips = Stretch("Categories", root, new Vector2(0, 1), new Vector2(1, 1),
                            new Vector2(Pad, -Pad * 2 - HeaderHeight - ChipsHeight), new Vector2(-Pad, -Pad * 2 - HeaderHeight));
            var cl = chips.gameObject.AddComponent<HorizontalLayoutGroup>();
            cl.spacing = 5f;
            cl.childControlWidth = cl.childControlHeight = true;
            cl.childForceExpandWidth = true;
            cl.childForceExpandHeight = true;
            Chip("All", null, new Color(0.25f, 0.3f, 0.38f));
            foreach (var c in ElementTable.Categories) Chip(c.Label, c.Key, CategoryColours[c.Key]);

            // Grid area and detail panel.
            float top = Pad * 3 + HeaderHeight + ChipsHeight;
            grid = Stretch("Grid", root, Vector2.zero, Vector2.one, new Vector2(Pad, Pad), new Vector2(-Pad, -top));
            detail = Stretch("ElementDetail", root, new Vector2(0.56f, 0), new Vector2(1, 1), new Vector2(0, Pad), new Vector2(-Pad, -top));
            LabTheme.Round(detail.gameObject.AddComponent<Image>(), new Color(0.06f, 0.09f, 0.14f, 0.98f));
            BuildDetail(detail);
            detail.gameObject.SetActive(false);
        }

        private void EnsureCells()
        {
            if (table != null) return;
            table = ElementTable.Load();
            foreach (var e in table.All)
            {
                var go = new GameObject("Cell_" + e.symbol, typeof(RectTransform), typeof(Image), typeof(Button));
                go.layer = grid.gameObject.layer;
                var rect = (RectTransform)go.transform;
                rect.SetParent(grid, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                var bg = go.GetComponent<Image>();
                LabTheme.Round(bg, CategoryColours.TryGetValue(e.category, out var c) ? c : Color.gray, 2.5f);
                var ring = go.AddComponent<Outline>();
                ring.effectColor = Color.white;
                ring.effectDistance = new Vector2(2f, -2f);
                ring.enabled = false;
                var record = e;
                go.GetComponent<Button>().onClick.AddListener(() => Select(record));
                var z = Label(rect, "Z", e.z.ToString(CultureInfo.InvariantCulture), 9f, new Color(1, 1, 1, 0.85f), FontStyles.Normal);
                Anchor(z.rectTransform, new Vector2(0.08f, 0.62f), new Vector2(1f, 0.98f));
                z.alignment = TextAlignmentOptions.TopLeft;
                z.enableAutoSizing = true; z.fontSizeMin = 6f; z.fontSizeMax = 11f;
                var sym = Label(rect, "Symbol", e.symbol, 18f, Color.white, FontStyles.Bold);
                Anchor(sym.rectTransform, new Vector2(0f, 0.04f), new Vector2(1f, 0.74f));
                sym.alignment = TextAlignmentOptions.Center;
                sym.enableAutoSizing = true; sym.fontSizeMin = 9f; sym.fontSizeMax = 22f;
                cells[e.z] = (rect, bg, ring);
            }
            foreach (var (text, row) in new[] { ("57–71", 6), ("89–103", 7) })
            {
                var go = new GameObject("Placeholder_" + row, typeof(RectTransform), typeof(Image));
                go.layer = grid.gameObject.layer;
                go.transform.SetParent(grid, false);
                LabTheme.Round(go.GetComponent<Image>(), new Color(0.2f, 0.24f, 0.3f, 0.6f), 2.5f);
                var t = Label((RectTransform)go.transform, "Text", text, 10f, LabTheme.MutedText, FontStyles.Normal);
                Anchor(t.rectTransform, Vector2.zero, Vector2.one);
                t.alignment = TextAlignmentOptions.Center;
                t.enableAutoSizing = true; t.fontSizeMin = 6f; t.fontSizeMax = 11f;
                ((RectTransform)go.transform).anchorMin = ((RectTransform)go.transform).anchorMax = ((RectTransform)go.transform).pivot = new Vector2(0, 1);
                placeholders.Add(go);
            }
            ApplyFilter();
        }

        private void LateUpdate()
        {
            if (table == null) return;
            var size = grid.rect.size;
            if (size != laidOutFor) LayOut(size);
        }

        /// <summary>Places every cell from its row and column; cells scale with the available space.</summary>
        private void LayOut(Vector2 size)
        {
            laidOutFor = size;
            // 7 rows, a small gap, then the two f-block rows: 9.4 cells high, 18 wide; centred.
            float cell = Mathf.Min(size.x / 18f, size.y / 9.4f);
            float gap = Mathf.Max(1.5f, cell * 0.06f);
            float x0 = (size.x - 18f * cell) * 0.5f;
            foreach (var e in table.All)
            {
                var r = cells[e.z].Rect;
                float y = e.row >= 9 ? (e.row - 2) * cell + cell * 0.4f : (e.row - 1) * cell;
                r.anchoredPosition = new Vector2(x0 + (e.col - 1) * cell, -y);
                r.sizeDelta = new Vector2(cell - gap, cell - gap);
            }
            for (int i = 0; i < placeholders.Count; i++)
            {
                var r = (RectTransform)placeholders[i].transform;
                r.anchoredPosition = new Vector2(x0 + 2 * cell, -(5 + i) * cell);
                r.sizeDelta = new Vector2(cell - gap, cell - gap);
            }
        }

        // ── Open / close, search, filter ───────────────────────────────

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            EnsureCells();
            laidOutFor = Vector2.zero;
        }

        public void Close() => gameObject.SetActive(false);

        public void SetSearch(string text)
        {
            search.SetTextWithoutNotify(text ?? string.Empty);
            ApplyFilter();
        }

        public void SetCategory(string key)
        {
            category = key;
            foreach (var kv in chipImages)
                kv.Value.transform.localScale = kv.Key == (key ?? "all") ? new Vector3(1f, 1.12f, 1f) : Vector3.one;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (table == null) return;
            var matches = new HashSet<int>(table.Search(search.text, category).Select(e => e.z));
            foreach (var kv in cells)
            {
                bool on = matches.Contains(kv.Key);
                var c = kv.Value.Bg.color;
                kv.Value.Bg.color = new Color(c.r, c.g, c.b, on ? 1f : 0.14f);
                foreach (var t in kv.Value.Rect.GetComponentsInChildren<TextMeshProUGUI>())
                    t.alpha = on ? 1f : 0.3f;
            }
            VisibleCount = matches.Count;
            countText.text = matches.Count == 118 ? "118 elements" : $"{matches.Count} of 118";
            // A search that finds exactly one element opens it.
            if (matches.Count == 1 && !string.IsNullOrWhiteSpace(search.text) && table.TryGet(matches.First(), out var only))
                Select(only);
        }

        // ── Detail panel ───────────────────────────────────────────────

        private void BuildDetail(RectTransform parent)
        {
            var scroll = parent.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30f;
            var viewport = Stretch("Viewport", parent, Vector2.zero, Vector2.one, new Vector2(12, 10), new Vector2(-12, -44));
            viewport.gameObject.AddComponent<RectMask2D>();
            detailContent = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            detailContent.gameObject.layer = parent.gameObject.layer;
            detailContent.SetParent(viewport, false);
            detailContent.anchorMin = new Vector2(0, 1);
            detailContent.anchorMax = new Vector2(1, 1);
            detailContent.pivot = new Vector2(0.5f, 1);
            detailContent.sizeDelta = Vector2.zero;   // exactly the viewport's width
            var vl = detailContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 8f;
            vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            detailContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = detailContent;
            detailText = Label(detailContent, "DetailText", string.Empty, 14f, LabTheme.TextColor, FontStyles.Normal);
            detailText.enableWordWrapping = true;
            var close = MakeButton(parent, "CloseDetail", "×", LabTheme.ControlColor, 34f, () => detail.gameObject.SetActive(false));
            var cr = (RectTransform)close.transform;
            cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1, 1);
            cr.anchoredPosition = new Vector2(-8, -6);
            cr.sizeDelta = new Vector2(34, 30);
        }

        public void Select(ElementRecord e)
        {
            if (e == null) return;
            if (Selected != null && cells.TryGetValue(Selected.z, out var old)) old.Ring.enabled = false;
            Selected = e;
            if (cells.TryGetValue(e.z, out var cur)) cur.Ring.enabled = true;
            var input = FindObjectOfType<LabInputController>();
            var materials = input != null ? input.AllMaterials.ToList() : new List<ChemicalMaterial>();
            var reactions = AppManager.Instance != null ? AppManager.Instance.ReactionDatabase : null;
            detailText.text = Describe(e, table, materials, reactions);

            foreach (var b in actionButtons) Destroy(b);
            actionButtons.Clear();
            if (input != null)
                foreach (var m in Elemental(e, materials).Concat(Compounds(e, materials)).Where(m => input.AvailableReagents.Contains(m.formula)).Take(4))
                {
                    var formula = m.formula;
                    var b = MakeButton(detailContent, "UseAsReagentA_" + formula, $"Use {formula} as reagent A", LabTheme.Accent, 0f, () => UseAsReagentA(formula));
                    b.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;
                    actionButtons.Add(b.gameObject);
                }
            detail.gameObject.SetActive(true);
            detail.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
        }

        private void UseAsReagentA(string formula)
        {
            var view = FindObjectsOfType<ReagentDropdownView>(true).FirstOrDefault(v => v.name.StartsWith("ReagentA"));
            if (view != null) view.SelectFormula(formula);
            Close();
        }

        // ── Text (pure, so it can be tested) ───────────────────────────

        private static IEnumerable<ChemicalMaterial> Elemental(ElementRecord e, IEnumerable<ChemicalMaterial> materials)
            => materials.Where(m => { var s = ElementTable.SymbolsIn(m.formula).ToList(); return s.Count == 1 && s[0] == e.symbol; });

        private static IEnumerable<ChemicalMaterial> Compounds(ElementRecord e, IEnumerable<ChemicalMaterial> materials)
            => materials.Where(m => { var s = ElementTable.SymbolsIn(m.formula).ToList(); return s.Count > 1 && s.Contains(e.symbol); })
                        .OrderBy(m => m.formula.Length).ThenBy(m => m.formula);

        public static string Describe(ElementRecord e, ElementTable table, IList<ChemicalMaterial> materials, ReactionDB reactions)
        {
            var sb = new StringBuilder();
            sb.Append($"<size=170%><b>{e.symbol}</b></size>  <size=125%>{e.name}</size>\n");
            sb.Append($"Atomic number <b>{e.z}</b>  ·  {ElementTable.CategorySingular(e.category)}  ·  period {e.period}  ·  ")
              .Append(e.group > 0 ? $"group {e.group}" : "f-block (no group number)").Append($"  ·  {e.block}-block\n");
            if (!string.IsNullOrEmpty(e.categoryNote)) sb.Append(Muted(e.categoryNote)).Append('\n');

            Section(sb, "OVERVIEW");
            if (e.standardAtomicWeight.status == "standard")
                Line(sb, "Standard atomic weight", Num(e.standardAtomicWeight.value), e.standardAtomicWeight);
            else
            {
                Line(sb, "Standard atomic weight", "none", e.standardAtomicWeight);
                Line(sb, "Mass of a reference isotope", e.referenceIsotopeMass.HasValue ? Num(e.referenceIsotopeMass.value) + " u" : "—", e.referenceIsotopeMass);
            }
            Line(sb, "State at room conditions", e.stateAtRoomConditions.HasValue ? e.stateAtRoomConditions.text : "—", e.stateAtRoomConditions);

            Section(sb, "PHYSICAL PROPERTIES");
            Line(sb, "Density", e.density.HasValue ? Num(e.density.value) + " g/cm³" : "—", e.density);
            Line(sb, "Melting point", Kelvin(e.meltingPoint), e.meltingPoint);
            Line(sb, "Boiling point", Kelvin(e.boilingPoint), e.boilingPoint);

            Section(sb, "CHEMICAL PROPERTIES");
            Line(sb, "Electronegativity (Pauling)", e.electronegativity.HasValue ? Num(e.electronegativity.value) : "—", e.electronegativity);
            Line(sb, "First ionization energy", e.firstIonizationEnergy.HasValue ? Num(e.firstIonizationEnergy.value) + " eV" : "—", e.firstIonizationEnergy);
            Line(sb, "Electron affinity", e.electronAffinity.HasValue ? Num(e.electronAffinity.value) + " eV" : "—", e.electronAffinity);
            Line(sb, "Common oxidation states", e.oxidationStates.HasValue ? e.oxidationStates.text : "—", e.oxidationStates);
            Line(sb, "Atomic radius", e.atomicRadius.HasValue ? Num(e.atomicRadius.value) + " pm" : "—", e.atomicRadius);

            Section(sb, "ELECTRON CONFIGURATION");
            Line(sb, "Ground state", e.electronConfiguration.HasValue ? e.electronConfiguration.text : "—", e.electronConfiguration);

            Section(sb, "DISCOVERY");
            Line(sb, "Discovered", e.discovery.HasValue ? e.discovery.text : "—", e.discovery);

            Section(sb, "OCCURRENCE · USES · SAFETY");
            sb.Append(Muted("Not yet included: these texts will be added from a citable source.")).Append('\n');

            Section(sb, "IN THIS LAB");
            var elemental = Elemental(e, materials).ToList();
            var compounds = Compounds(e, materials).ToList();
            sb.Append(elemental.Count > 0
                ? $"As the element: {string.Join(", ", elemental.Select(m => $"<b>{ChemText.Formula(m.formula)}</b> ({m.GetDisplayName(0)})"))}\n"
                : Muted($"The pure element is not a material in this lab.") + "\n");
            if (compounds.Count > 0)
                sb.Append($"In compounds: {string.Join(", ", compounds.Take(12).Select(m => ChemText.Formula(m.formula)))}" +
                          (compounds.Count > 12 ? $" and {compounds.Count - 12} more" : string.Empty) + "\n");
            var formulas = new HashSet<string>(elemental.Concat(compounds).Select(m => m.formula));
            int reactionCount = reactions?.reactions?.Count(r => r?.reactants != null && r.reactants.Any(c => formulas.Contains(c.formula))) ?? 0;
            sb.Append(reactionCount > 0 ? $"Reactions in the lab's database that use these materials: <b>{reactionCount}</b>\n"
                                        : Muted("No reaction in the lab's database uses these materials.") + "\n");
            var flame = compounds.Select(m => FlameTest.Test(m.formula)).FirstOrDefault(r => r.HasColour && r.Entry.Element == e.symbol);
            if (flame.HasColour)
                sb.Append($"Flame test: <b>{flame.Entry.ColourName}</b> ({flame.Entry.Ion}) for its salts, e.g. {ChemText.Formula(flame.Formula)} — {Muted(FlameTest.Fidelity)}\n");
            var heated = elemental.Concat(compounds).Where(m => PhaseHeatingModel.IsSupported(m, out _)).Select(m => ChemText.Formula(m.formula)).ToList();
            if (heated.Count > 0) sb.Append($"Heating lab (physical change): {string.Join(", ", heated)}\n");

            Section(sb, "REFERENCES");
            foreach (var id in new[] { "pubchem_pt", "ciaaw_2021" })
                if (table != null && table.TryGetSource(id, out var src))
                    sb.Append(Muted($"{src.citation} — {src.url} (retrieved {src.retrieved})")).Append('\n');
            return sb.ToString().TrimEnd();
        }

        private static void Section(StringBuilder sb, string title)
            => sb.Append($"\n<size=85%><color=#7FB8FF><b>{title}</b></color></size>\n");

        private static void Line(StringBuilder sb, string label, string value, ElementProperty p)
        {
            sb.Append($"{label}: <b>{value}</b>{Tag(p)}\n");
            if (!string.IsNullOrEmpty(p.note) && p.status != "standard") sb.Append(Muted(p.note)).Append('\n');
        }

        private static string Tag(ElementProperty p)
        {
            switch (p.status)
            {
                case "standard":            return "  <size=80%><color=#4DD980>CIAAW standard</color></size>";
                case "listed":              return string.Empty;
                case "estimated":           return "  <size=80%><color=#FFBD47>estimate</color></size>";
                case "predicted":           return "  <size=80%><color=#FFBD47>predicted</color></size>";
                case "reference_dependent": return "  <size=80%><color=#FFBD47>not at 1 atm</color></size>";
                case "not_applicable":      return "  <size=80%><color=#9FB3C8>not applicable</color></size>";
                case "not_sourced":         return "  <size=80%><color=#9FB3C8>not yet sourced</color></size>";
                default:                    return "  <size=80%><color=#9FB3C8>unknown</color></size>";
            }
        }

        private static string Kelvin(ElementProperty p)
            => p.HasValue ? $"{Num(p.value)} K ({Num(p.value - 273.15)} °C)" : "—";

        private static string Num(double v)
            => v.ToString(System.Math.Abs(v) >= 100 ? "0.##" : "0.####", CultureInfo.InvariantCulture);

        private static string Muted(string s) => $"<size=85%><color=#9FB3C8>{s}</color></size>";

        // ── Building blocks ────────────────────────────────────────────

        private void Chip(string label, string key, Color colour)
        {
            var b = MakeButton(chips, "Chip_" + (key ?? "all"), label, colour * 0.85f + new Color(0, 0, 0, 0.15f), 0f, () => SetCategory(key));
            chipImages[key ?? "all"] = b.GetComponent<Image>();
        }

        private TMP_InputField SearchField(RectTransform parent)
        {
            var box = new GameObject("ElementSearch", typeof(RectTransform), typeof(Image));
            box.layer = parent.gameObject.layer;
            box.transform.SetParent(parent, false);
            box.SetActive(false);
            LabTheme.Round(box.GetComponent<Image>(), new Color(0.04f, 0.06f, 0.10f, 1f), 2.5f);
            box.AddComponent<LayoutElement>().preferredWidth = 260f;
            var area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            var ar = (RectTransform)area.transform;
            ar.SetParent(box.transform, false);
            Anchor(ar, Vector2.zero, Vector2.one);
            ar.offsetMin = new Vector2(10, 0);
            ar.offsetMax = new Vector2(-10, 0);
            var placeholder = Label(ar, "Placeholder", "Search: name, symbol or number", 13f, LabTheme.MutedText, FontStyles.Italic);
            Anchor(placeholder.rectTransform, Vector2.zero, Vector2.one);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            var text = Label(ar, "Text", string.Empty, 14f, Color.white, FontStyles.Normal);
            Anchor(text.rectTransform, Vector2.zero, Vector2.one);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            var field = box.AddComponent<TMP_InputField>();
            field.textViewport = ar;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = 24;
            field.targetGraphic = box.GetComponent<Image>();
            field.onValueChanged.AddListener(_ => ApplyFilter());
            box.SetActive(true);
            return field;
        }

        private UnityEngine.UI.Button MakeButton(RectTransform parent, string name, string label, Color colour, float width, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            LabTheme.Round(go.GetComponent<Image>(), colour, 2f);
            if (width > 0) go.AddComponent<LayoutElement>().preferredWidth = width;
            var b = go.AddComponent<UnityEngine.UI.Button>();
            b.targetGraphic = go.GetComponent<Image>();
            b.onClick.AddListener(onClick);
            var t = Label((RectTransform)go.transform, "Label", label, 13f, Color.white, FontStyles.Bold);
            Anchor(t.rectTransform, Vector2.zero, Vector2.one);
            t.rectTransform.offsetMin = new Vector2(4, 0);
            t.rectTransform.offsetMax = new Vector2(-4, 0);
            t.alignment = TextAlignmentOptions.Center;
            t.enableAutoSizing = true; t.fontSizeMin = 8f; t.fontSizeMax = 13f;
            return b;
        }

        private TextMeshProUGUI Label(RectTransform parent, string name, string text, float size, Color colour, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = colour;
            t.fontStyle = style;
            t.richText = true;
            t.enableWordWrapping = false;
            t.raycastTarget = false;
            return t;
        }

        private static RectTransform Stretch(string name, RectTransform parent, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = min;
            r.anchorMax = max;
            r.offsetMin = offMin;
            r.offsetMax = offMax;
            return r;
        }

        private static void Anchor(RectTransform r, Vector2 min, Vector2 max)
        {
            r.anchorMin = min;
            r.anchorMax = max;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }
    }
}
