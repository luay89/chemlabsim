// ChemLabSim v3 — 3D Lab Bench (vertical slice V0)
// A dark lab scene with one glass beaker whose contents follow the chemistry:
//   • liquid color      ← reaction color_change (and Reagent A preview)
//   • precipitate layer ← precipitate + precipitate_color
//   • bubbles / foam    ← gas + effervescence level / foam
//   • steam             ← bench temperature (slider + reaction ΔT) and vapor
//   • heat glow / frost ← reaction temperature_delta; when the temperature change is calculated
//                         from the student's amounts (LabInstruments.RiseAfterMix) that value is
//                         used instead and held, as on the thermometer (no heat loss): steam from
//                         hot water, condensation on a cold beaker, and no glow (water < 100 °C)
//   • smoke / sparks    ← smoke / sparks flags
//   • flame             ← dry/molten reactions that burn; colour from the burning element
// The beaker's contents follow the phases of the reaction (ReactionEvaluatedEvent):
//   solution (aq / liquid present) → liquid column, precipitate, bubbles
//   dry (solids + gases only)      → solid layer that turns into the solid product, no liquid
//   molten (liquid product, ≥ 300 °C) → thin glowing melt (thermite)
// Volume: the beaker is a 100 mL beaker with a printed scale (GraduatedScale, GlasswareScale);
// the liquid surface stands at the mark of the volume worked out from the student's amounts
// (VesselVolume) — reagent A before mixing, all reagents after — and a small tag reads it out.
// When the volume cannot be worked out the level is illustrative and the tag says why.
// It only listens to existing events (FxTriggeredEvent, ReactionEvaluatedEvent,
// EnvironmentChangedEvent, EnvironmentFxChangedEvent, MaterialPreviewChangedEvent),
// so no controller changes.
// Created automatically when LabV3 loads; the UI canvas keeps drawing on top.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using ChemLabSimV3.Controllers;
using ChemLabSimV3.Data;
using ChemLabSimV3.Engine;
using ChemLabSimV3.Engine.Chemistry;
using ChemLabSimV3.Events;
using System.Collections.Generic;

namespace ChemLabSimV3.Views.Bench3D
{
    public class LabBench3D : MonoBehaviour
    {
        private const string TargetScene = "LabV3";

        // ── Beaker geometry (scene units) ───────────────────────────────
        private const float OuterRadius = 0.45f;
        private const float Height      = 1.10f;
        private const float Wall        = 0.022f;
        private const float BaseThick   = 0.045f;
        private const int   Segments    = 72;
        private const float InnerRadius = OuterRadius - Wall;
        private const float LiquidBottom = BaseThick + 0.003f;
        private const float MaxFill     = Height - BaseThick - 0.08f;   // world height available for contents
        private const float BeakerMarkHeight = 0.80f;                   // height of the 100 mL mark above the inside bottom

        // ── Look ────────────────────────────────────────────────────────
        private static readonly Color Background   = new Color(0.018f, 0.022f, 0.032f);
        private static readonly Color WaterColor   = new Color(0.78f, 0.88f, 1.00f, 0.20f);
        private static readonly Color PowderColor  = new Color(0.93f, 0.93f, 0.90f, 1.00f);
        private static readonly Color GlassTint    = new Color(0.10f, 0.12f, 0.14f, 0.10f);
        private static readonly Color FrostTint    = new Color(0.55f, 0.65f, 0.75f, 0.45f);
        private static readonly Color HeatColor    = new Color(1.00f, 0.45f, 0.12f);
        private static readonly Color MoltenColor  = new Color(1.00f, 0.55f, 0.15f, 0.95f);
        private static readonly Color DefaultFlame = new Color(1.00f, 0.62f, 0.22f);
        private const float GlowStartC = 15f;    // reaction ΔT below this gives no visible glow
        private const float GlowFullC  = 80f;

        // ── Runtime objects ─────────────────────────────────────────────
        private Transform liquid, meniscus;
        private Material meniscusMat;
        private Material liquidMat, glassBackMat, glassFrontMat;
        private ParticleSystem bubbles, steam, smoke, sparks, flame;
        private Light glowLight;
        private bool liquidShaderOk;

        // ── Contents state (fractions of MaxFill) ───────────────────────
        private float solidLevel, liquidLevel, foamLevel;
        private float solidTarget, liquidTarget, foamTarget;
        private Color liquidColor = WaterColor, liquidTargetColor = WaterColor;
        private Color solidColor = PowderColor, solidTargetColor = PowderColor;
        private float turbidity, turbidityTarget;

        // ── Reaction / environment drivers ──────────────────────────────
        private bool  reacting;
        private float reactionTime;
        private float benchTemperatureC = 25f;
        private float reactionDeltaC;
        private float calculatedDeltaC = float.NaN;   // from the amounts, for the current mix
        private bool heatHeld;                        // reactionDeltaC is the calculated value

        /// <summary>Temperature of the beaker's contents the effects are drawn for (screenshot checks).</summary>
        public float BeakerTemperatureC { get; private set; } = 25f;
        /// <summary>True when that temperature is the one calculated from the student's amounts.</summary>
        public bool HeatFromAmounts => heatHeld;
        private int   effervescence;
        private bool  vapor, smokeOn, glowOn, foamOn, precipitateOn;
        private float envSteamRate, stirSpin;
        private float frost;
        private bool  flameOn;

        // Electrolysis: two graphite rods while DC power is on; gas comes off at the rods
        // (water: H2 at the cathode twice as fast as O2 at the anode).
        private GameObject electrodes;
        private Material cathodeMat;
        private Color graphiteColor = new Color(0.12f, 0.12f, 0.13f), platingColor;
        private bool plating;
        private ParticleSystem cathodeBubbles, anodeBubbles;
        private bool currentOn, electrolysis;
        private float cathodeShare, anodeShare;   // gas split between the electrodes, from the equation
        private LabInputController labInput;

        // Hot plate under the beaker, reagent B in its own flask, poured in on Mix.
        private HotPlate hotPlate;
        private PourFlask pourFlask;
        private LabInstruments instruments;
        private PinnedCards cards;
        private PinnedCards.Card labelA, labelB;
        private Transform beakerRoot;
        private string shownReagentB;
        private Color flameColor = DefaultFlame;

        // Volume in the beaker (from the amounts) and its read-out tag.
        private GraduatedScale beakerScale;
        private VolumeReading beakerVolume = VolumeReading.Unknown("no reagent yet");
        private VolumeReading mixVolume = VolumeReading.Unknown("no mix yet");
        private PinnedCards.Card volumeTag;
        private string experimentSignature;

        // FxTriggeredEvent and ReactionEvaluatedEvent arrive in the same frame in either
        // order; the effect is applied on the next Update when both are known.
        private enum Phase { Solution, Dry, Molten }
        private ReactionEntry lastReaction;
        private FxState pendingFx;
        private bool fxPending;

        // ── Bootstrap ───────────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

        private static void TryCreate(Scene scene)
        {
            if (scene.name != TargetScene || FindObjectOfType<LabBench3D>() != null) return;
            new GameObject("LabBench3D").AddComponent<LabBench3D>();
        }

        // ── Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            // The older generic particle view was invisible behind the overlay canvas; with a
            // real 3D camera view it would now show duplicate, unrelated effects.
            var legacyFx = FindObjectOfType<ReactionFxView>();
            if (legacyFx != null) legacyFx.gameObject.SetActive(false);

            SetUpCameraAndLighting();
            BuildTable();
            BuildBeaker();
            BuildParticles();
            BuildElectrodes();
            BuildEquipment();
            SetIdle("liquid", string.Empty);
            liquidLevel = liquidTarget;
            liquidColor = liquidTargetColor;
        }

        private void Start()
        {
            // Show the default reagent B as soon as the controllers have set up the inputs.
            labInput = FindObjectOfType<LabInputController>();
            if (labInput != null) ShowReagentB(labInput.CurrentInput.ReagentB);
            if (instruments != null) instruments.BeginExperiment(labInput != null ? labInput.CurrentInput.ReagentA : null);
            RefreshLabels();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<FxTriggeredEvent>(OnFx);
            EventBus.Subscribe<ReactionEvaluatedEvent>(OnReactionEvaluated);
            EventBus.Subscribe<ReactionNotFoundEvent>(OnReactionNotFound);
            EventBus.Subscribe<MaterialPreviewChangedEvent>(OnMaterialPreview);
            EventBus.Subscribe<EnvironmentChangedEvent>(OnEnvironment);
            EventBus.Subscribe<EnvironmentFxChangedEvent>(OnEnvironmentFx);
            EventBus.Subscribe<InputChangedEvent>(OnInputChanged);
            EventBus.Subscribe<TitrationUpdatedEvent>(OnTitration);
            EventBus.Subscribe<HeatingUpdatedEvent>(OnHeating);
            EventBus.Subscribe<StoichiometryEvaluatedEvent>(OnStoichiometry);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<FxTriggeredEvent>(OnFx);
            EventBus.Unsubscribe<ReactionEvaluatedEvent>(OnReactionEvaluated);
            EventBus.Unsubscribe<ReactionNotFoundEvent>(OnReactionNotFound);
            EventBus.Unsubscribe<MaterialPreviewChangedEvent>(OnMaterialPreview);
            EventBus.Unsubscribe<EnvironmentChangedEvent>(OnEnvironment);
            EventBus.Unsubscribe<EnvironmentFxChangedEvent>(OnEnvironmentFx);
            EventBus.Unsubscribe<InputChangedEvent>(OnInputChanged);
            EventBus.Unsubscribe<TitrationUpdatedEvent>(OnTitration);
            EventBus.Unsubscribe<HeatingUpdatedEvent>(OnHeating);
            EventBus.Unsubscribe<StoichiometryEvaluatedEvent>(OnStoichiometry);
        }

        // ── Scene construction ─────────────────────────────────────────

        // Camera framings: the beaker close up, and a wider view during a titration so the burette
        // above the beaker (up to y ≈ 2.1) is in the picture.
        private static readonly Vector3 CameraHome = new Vector3(0f, 1.30f, -3.6f), CameraHomeTarget = new Vector3(0f, 0.52f, 0f);
        private static readonly Vector3 CameraTitration = new Vector3(0f, 1.95f, -5.0f), CameraTitrationTarget = new Vector3(0f, 0.86f, 0f);
        private float titrationView;   // 0 = home framing, 1 = titration framing

        private void UpdateCameraFraming(float dt)
        {
            var cam = Camera.main;
            float target = titrationShown ? 1f : 0f;
            if (cam == null || Mathf.Approximately(titrationView, target)) return;
            titrationView = Mathf.MoveTowards(titrationView, target, dt / 0.7f);
            float k = Mathf.SmoothStep(0f, 1f, titrationView);
            cam.transform.position = Vector3.Lerp(CameraHome, CameraTitration, k);
            cam.transform.LookAt(Vector3.Lerp(CameraHomeTarget, CameraTitrationTarget, k));
        }

        private void SetUpCameraAndLighting()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Background;
                cam.fieldOfView = 30f;
                cam.nearClipPlane = 0.05f;
                cam.transform.position = CameraHome;
                cam.transform.LookAt(CameraHomeTarget);
            }

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.07f, 0.08f, 0.10f);
            RenderSettings.fog = false;

            foreach (var existing in FindObjectsOfType<Light>())
            {
                if (existing.type == LightType.Directional)
                {
                    existing.intensity = 0.25f;
                    existing.color = new Color(0.70f, 0.80f, 1.00f);
                }
            }

            // Warm key light from the front-left, cool rim light from behind.
            var key = CreateLight("KeyLight", LightType.Spot, new Vector3(-1.6f, 2.8f, -1.8f), new Color(1f, 0.95f, 0.88f), 6f, 7f);
            key.spotAngle = 38f;
            key.innerSpotAngle = 20f;
            key.transform.LookAt(new Vector3(0f, 0.4f, 0f));
            key.shadows = LightShadows.Soft;

            var rim = CreateLight("RimLight", LightType.Point, new Vector3(0.9f, 1.4f, 1.3f), new Color(0.45f, 0.65f, 1f), 2.2f, 4f);
            rim.shadows = LightShadows.None;

            glowLight = CreateLight("HeatGlow", LightType.Point, new Vector3(0f, 0.40f, 0f), HeatColor, 0f, 1.6f);
        }

        private Light CreateLight(string name, LightType type, Vector3 position, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            return light;
        }

        private void BuildTable()
        {
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "BenchTop";
            table.transform.SetParent(transform, false);
            table.transform.localScale = new Vector3(8f, 0.1f, 4f);
            table.transform.position = new Vector3(0f, -0.05f - HotPlate.Height, 1f);   // the beaker stands on the hot plate
            // Epoxy-resin lab bench: charcoal, satin — a step lighter than the wall so the bench reads.
            table.GetComponent<Renderer>().sharedMaterial = Bench3DMeshes.Lit(new Color(0.085f, 0.09f, 0.10f), 0.6f);
            Destroy(table.GetComponent<Collider>());

            var wall = GameObject.CreatePrimitive(PrimitiveType.Quad);
            wall.name = "BackWall";
            wall.transform.SetParent(transform, false);
            wall.transform.localScale = new Vector3(14f, 6f, 1f);
            wall.transform.position = new Vector3(0f, 2.5f, 3f);
            // Tiled lab wall (dark, so the glassware and labels stay the brightest things in view).
            var wallMat = Bench3DMeshes.Lit(new Color(0.06f, 0.07f, 0.085f), 0.35f);
            var tiles = Bench3DMeshes.TileTexture(64, 3);
            wallMat.SetTexture("_BaseMap", tiles);
            wallMat.SetTexture("_MainTex", tiles);
            wallMat.SetTextureScale("_BaseMap", new Vector2(14f / 0.32f, 6f / 0.32f));
            wallMat.SetTextureScale("_MainTex", new Vector2(14f / 0.32f, 6f / 0.32f));
            wall.GetComponent<Renderer>().sharedMaterial = wallMat;
            Destroy(wall.GetComponent<Collider>());
        }

        private void BuildBeaker()
        {
            var beaker = new GameObject("Beaker").transform;
            beakerRoot = beaker;
            beaker.SetParent(transform, false);

            // Back half: inner wall + inside of the base, drawn before the liquid.
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            Bench3DMeshes.AddWall(v, n, uv, t, InnerRadius, BaseThick, Height, Segments, inward: true);
            Bench3DMeshes.AddRing(v, n, uv, t, 0f, InnerRadius, BaseThick, Segments, up: true);
            glassBackMat = Bench3DMeshes.Glass(GlassTint, 2990, CullMode.Back);
            AddRenderer(beaker, "GlassInner", Bench3DMeshes.Build("GlassInner", v, n, uv, t), glassBackMat);

            // Front half: outer wall, rim and underside, drawn after the liquid.
            v.Clear(); n.Clear(); uv.Clear(); t.Clear();
            Bench3DMeshes.AddWall(v, n, uv, t, OuterRadius, 0f, Height, Segments, inward: false);
            Bench3DMeshes.AddRing(v, n, uv, t, InnerRadius, OuterRadius, Height, Segments, up: true);
            Bench3DMeshes.AddRing(v, n, uv, t, 0f, OuterRadius, 0f, Segments, up: false);
            glassFrontMat = Bench3DMeshes.Glass(GlassTint, 3010, CullMode.Back);
            AddRenderer(beaker, "GlassOuter", Bench3DMeshes.Build("GlassOuter", v, n, uv, t), glassFrontMat);

            // Printed volume scale on the side facing the camera: a 100 mL beaker, marks every
            // 10 mL, numbers every 20 mL. Beaker marks are approximate.
            beakerScale = GraduatedScale.Beaker100(BeakerMarkHeight);
            GlasswareScale.Build(beaker, beakerScale, new GlasswareScale.Style
            {
                Radius = OuterRadius, ZeroY = LiquidBottom, CentreDegrees = -22f,
                MinorLength = 0.07f, MajorLength = 0.12f, MinorThickness = 0.006f, MajorThickness = 0.009f,
                FontSize = 0.6f, UnitText = "mL (approx.)"
            });

            // Liquid column (height 1, scaled on Y to the current fill).
            var liquidShader = Shader.Find("ChemLabSim/Liquid");
            liquidShaderOk = liquidShader != null;
            if (liquidShaderOk)
            {
                liquidMat = new Material(liquidShader) { renderQueue = 3000 };
            }
            else
            {
                Debug.LogWarning("[LabBench3D] ChemLabSim/Liquid shader not found; using a plain transparent material.");
                liquidMat = Bench3DMeshes.Glass(WaterColor, 3000, CullMode.Back);
            }
            // Lip highlight: a thin unlit ring on the rim keeps the glass silhouette readable.
            v.Clear(); n.Clear(); uv.Clear(); t.Clear();
            Bench3DMeshes.AddRing(v, n, uv, t, InnerRadius - 0.002f, OuterRadius + 0.002f, Height + 0.001f, Segments, up: true);
            var lipMat = Bench3DMeshes.Particle(Texture2D.whiteTexture, false, 3015);
            lipMat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.35f));
            AddRenderer(beaker, "GlassLip", Bench3DMeshes.Build("GlassLip", v, n, uv, t), lipMat);

            // Meniscus line: a thin band where the liquid meets the glass, moved with the level, so
            // the surface can be read against the scale.
            v.Clear(); n.Clear(); uv.Clear(); t.Clear();
            Bench3DMeshes.AddWall(v, n, uv, t, InnerRadius - 0.003f, -0.004f, 0f, Segments, inward: false);
            meniscusMat = Bench3DMeshes.Particle(Texture2D.whiteTexture, false, 3005);
            meniscus = AddRenderer(beaker, "Meniscus", Bench3DMeshes.Build("Meniscus", v, n, uv, t), meniscusMat).transform;

            var liquidGo = AddRenderer(beaker, "Liquid", Bench3DMeshes.LiquidColumn(InnerRadius - 0.004f, Segments), liquidMat);
            liquidGo.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            liquid = liquidGo.transform;
            liquid.localPosition = new Vector3(0f, LiquidBottom, 0f);
        }

        private static GameObject AddRenderer(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private void BuildParticles()
        {
            var soft = Bench3DMeshes.SoftCircle(64, 0.1f);
            var bead = Bench3DMeshes.SoftCircle(32, 0.75f);

            bubbles = CreateEmitter("Bubbles", new Vector3(0f, LiquidBottom + 0.02f, 0f), InnerRadius * 0.8f,
                                    Bench3DMeshes.Particle(bead, false, 3005));
            var main = bubbles.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.032f);
            main.startColor = new Color(0.92f, 0.97f, 1f, 0.42f);
            main.maxParticles = 600;
            var noise = bubbles.noise;
            noise.enabled = true;
            noise.strength = 0.08f;
            noise.frequency = 1.5f;

            steam = CreateEmitter("Steam", new Vector3(0f, Height - 0.05f, 0f), InnerRadius * 0.7f,
                                  Bench3DMeshes.Particle(soft, false, 3020));
            main = steam.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startColor = new Color(0.92f, 0.95f, 1f, 0.16f);
            main.maxParticles = 300;
            GrowAndFade(steam, 2.6f);
            noise = steam.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.6f;

            smoke = CreateEmitter("Smoke", new Vector3(0f, Height - 0.05f, 0f), InnerRadius * 0.6f,
                                  Bench3DMeshes.Particle(soft, false, 3021));
            main = smoke.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 4.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            main.startColor = new Color(0.55f, 0.56f, 0.58f, 0.35f);
            main.maxParticles = 300;
            GrowAndFade(smoke, 3f);
            noise = smoke.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.5f;

            sparks = CreateEmitter("Sparks", new Vector3(0f, Height * 0.75f, 0f), InnerRadius * 0.5f,
                                   Bench3DMeshes.Particle(bead, true, 3022));
            main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.5f, 0.1f));
            main.gravityModifier = 0.8f;
            main.maxParticles = 400;
            var shape = sparks.shape;
            shape.angle = 40f;

            flame = CreateEmitter("Flame", new Vector3(0f, LiquidBottom + 0.06f, 0f), InnerRadius * 0.45f,
                                  Bench3DMeshes.Particle(soft, true, 3023));
            main = flame.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.3f);
            main.maxParticles = 500;
            shape = flame.shape;
            shape.angle = 10f;
            var flameSize = flame.sizeOverLifetime;
            flameSize.enabled = true;
            flameSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.25f));
            noise = flame.noise;
            noise.enabled = true;
            noise.strength = 0.18f;
            noise.frequency = 2.2f;
            SetFlameColor(DefaultFlame);
        }

        private void BuildElectrodes()
        {
            electrodes = new GameObject("Electrodes");
            electrodes.transform.SetParent(transform, false);
            var graphite = Bench3DMeshes.Lit(graphiteColor, 0.35f);
            cathodeMat = Bench3DMeshes.Lit(graphiteColor, 0.35f);
            var cathode = AddRod(electrodes.transform, "Cathode", -0.16f, cathodeMat);
            var anode   = AddRod(electrodes.transform, "Anode",    0.16f, graphite);

            cathodeBubbles = Instantiate(bubbles, cathode.position + Vector3.down * 0.42f, bubbles.transform.rotation, transform);
            anodeBubbles   = Instantiate(bubbles, anode.position + Vector3.down * 0.42f, bubbles.transform.rotation, transform);
            foreach (var ps in new[] { cathodeBubbles, anodeBubbles })
            {
                var shape = ps.shape;
                shape.radius = 0.02f;
                SetRate(ps, 0f);
                ps.Play();   // copies start stopped (playOnAwake is off)
            }
            electrodes.SetActive(false);
        }

        /// <summary>
        /// Which electrode gives off which gas: H2 at the cathode, O2 / Cl2 at the anode, in the
        /// ratio of their coefficients (water: 2 H2 : 1 O2). A metal deposit gives no gas.
        /// </summary>
        private void SplitElectrodeGas(ReactionEntry r)
        {
            float cathode = 0f, anode = 0f;
            if (r?.products != null)
                foreach (var p in r.products)
                {
                    if (p.state != "g") continue;
                    if (p.formula == "H2") cathode += p.stoich;
                    else if (p.formula == "O2" || p.formula == "Cl2") anode += p.stoich;
                }
            float total = cathode + anode;
            cathodeShare = total > 0f ? cathode / total : 0f;
            anodeShare = total > 0f ? anode / total : 0f;
        }

        /// <summary>Metal deposited on the cathode: 0 = clean graphite, 1 = fully plated.</summary>
        private void SetPlating(float amount)
        {
            if (cathodeMat == null) return;
            var c = Color.Lerp(graphiteColor, platingColor, Mathf.Clamp01(amount));
            cathodeMat.SetColor("_BaseColor", c);
            cathodeMat.SetColor("_Color", c);
            cathodeMat.SetFloat("_Metallic", Mathf.Lerp(0f, 0.8f, amount));
        }

        private static Transform AddRod(Transform parent, string name, float x, Material mat)
        {
            var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rod.name = name;
            rod.transform.SetParent(parent, false);
            rod.transform.localScale = new Vector3(0.035f, 0.45f, 0.035f);       // 0.9 tall
            rod.transform.localPosition = new Vector3(x, 0.72f, 0f);              // tip at 0.27
            rod.GetComponent<Renderer>().sharedMaterial = mat;
            Destroy(rod.GetComponent<Collider>());
            return rod.transform;
        }

        private void OnInputChanged(InputChangedEvent e)
        {
            if (labInput == null) labInput = FindObjectOfType<LabInputController>();
            currentOn = labInput != null && labInput.CurrentInput.HasElectricCurrent;
            electrodes.SetActive(currentOn);
            ShowReagentB(labInput != null ? labInput.CurrentInput.ReagentB : null);
            RefreshLabels();

            // New reagents or amounts: the beaker shows the new reagent A (conditions alone do not reset it).
            string signature = ExperimentSignature();
            if (signature != experimentSignature && !titrationShown && (pourFlask == null || !pourFlask.Busy))
            {
                experimentSignature = signature;
                PrepareFreshBeaker();
                if (instruments != null) instruments.BeginExperiment(labInput != null ? labInput.CurrentInput.ReagentA : null);
            }
        }

        private string ExperimentSignature()
        {
            if (labInput == null) labInput = FindObjectOfType<LabInputController>();
            if (labInput == null) return string.Empty;
            var sb = new System.Text.StringBuilder();
            var input = labInput.CurrentInput;
            var formulas = new[] { input.ReagentA, input.ReagentB, input.ReagentC, input.ReagentD };
            for (int i = 0; i < formulas.Length; i++)
            {
                labInput.TryGetAmount(i, out double v, out string unit);
                sb.Append(formulas[i]).Append(':').Append(v.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(unit).Append('|');
            }
            return sb.ToString();
        }

        private void BuildEquipment()
        {
            var bead = Bench3DMeshes.SoftCircle(32, 0.75f);
            var soft = Bench3DMeshes.SoftCircle(64, 0.1f);
            hotPlate = HotPlate.Create(transform, soft);
            hotPlate.transform.localPosition = new Vector3(0f, -HotPlate.Height, 0f);
            hotPlate.TargetC = benchTemperatureC;
            pourFlask = PourFlask.Create(transform, -HotPlate.Height, bead, soft);
            cards = PinnedCards.Create(transform);
            instruments = LabInstruments.Create(transform, cards);
            LabTestRigs.Create(transform, soft);

            // Volume read-out next to the liquid surface (moved with the level in Update).
            volumeTag = cards.Add("VolumeTag", string.Empty, new Color(0.03f, 0.07f, 0.12f, 0.88f), beakerRoot,
                                  new Vector3(OuterRadius, LiquidBottom, 0f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), 150f, 17f);
            volumeTag.Hidden = true;

            // Labels of the selected reagents: A in the beaker, B in the pouring flask.
            var labelTint = new Color(0.07f, 0.1f, 0.16f, 0.9f);
            labelA = cards.Add("ReagentALabel", string.Empty, labelTint, beakerRoot, new Vector3(0f, 0f, -OuterRadius),
                               new Vector2(0.5f, 1f), new Vector2(0f, -8f), 230f, 16f);
            labelB = cards.Add("ReagentBLabel", string.Empty, labelTint, pourFlask.transform, new Vector3(0f, 0f, -0.17f),
                               new Vector2(0.5f, 1f), new Vector2(0f, -8f), 190f, 16f);
            LabAtmosphere.SetUp(Camera.main, transform, UnityEngine.Application.isMobilePlatform);
        }

        /// <summary>Bottle-style labels: formula and name, amount and the bottle's concentration.</summary>
        private void RefreshLabels()
        {
            if (labInput == null || labelA == null) return;
            Label(labelA, labInput.CurrentInput.ReagentA, 0);
            Label(labelB, labInput.CurrentInput.ReagentB, 1);
        }

        private void Label(PinnedCards.Card card, string formula, int slot)
        {
            bool has = !string.IsNullOrWhiteSpace(formula);
            card.Hidden = !has;
            if (!has) return;
            string f = formula.Trim();
            string name = labInput.TryGetMaterial(f, out var mat) ? mat.GetDisplayName(0) : string.Empty;
            card.Value.text = $"{ChemText.Formula(f)}  <size=70%><color=#B8C6D8>{name}</color></size>";
            bool hasAmount = labInput.TryGetAmount(slot, out double v, out string unit);
            card.Caption.text = LabelCaption(mat, hasAmount, v, unit);
            if (slot == 1 && pourFlask != null && pourFlask.Overfull)
                card.Caption.text += "\n<color=#FFBD47>more than the 50 mL cylinder holds</color>";
        }

        /// <summary>Second line of a reagent label: the amount and, for a solution, the bottle's concentration.</summary>
        public static string LabelCaption(ChemicalMaterial mat, bool hasAmount, double value, string unit)
        {
            string amount = hasAmount
                ? $"{value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} {unit}" : "no amount";
            return mat != null && mat.stockConcentrationM > 0f ? $"{amount}  ·  {mat.stockLabel}" : amount;
        }

        /// <summary>Puts reagent B in the graduated cylinder, in its real state, colour and measured volume.</summary>
        private void ShowReagentB(string formula)
        {
            if (pourFlask == null || pourFlask.Busy) return;
            VolumeReading volume = TryAmount(1, formula, out var amountB) ? VesselVolume.Of(amountB) : VolumeReading.Unknown("no reagent B");
            string shown = $"{formula}|{volume.Known}|{volume.Ml}";
            if (shown == shownReagentB) return;
            shownReagentB = shown;
            if (string.IsNullOrWhiteSpace(formula) || labInput == null)
            {
                pourFlask.Set(PourFlask.Content.Empty, Color.clear, volume);
                return;
            }

            labInput.TryGetMaterialLook(formula.Trim(), out string state, out string hex);
            bool hasColour = TryParseHex(hex, out var c);
            string s = (state ?? string.Empty).Trim().ToLowerInvariant();
            if (s == "solid" || s == "s")
                pourFlask.Set(PourFlask.Content.Powder, hasColour ? c : PowderColor, volume);
            else if (s == "gas" || s == "g")
                pourFlask.Set(PourFlask.Content.Gas, hasColour ? new Color(c.r, c.g, c.b, 0.25f) : new Color(0.9f, 0.92f, 0.95f, 0.06f), volume);
            else
                pourFlask.Set(PourFlask.Content.Liquid, hasColour ? new Color(c.r, c.g, c.b, 0.55f) : WaterColor, volume);
        }

        /// <summary>What the pour adds to the beaker before the reaction itself takes over.</summary>
        private void AddPouredReagent()
        {
            switch (pourFlask.Holds)
            {
                case PourFlask.Content.Liquid:
                    beakerVolume = mixVolume;
                    liquidTarget = LevelFor(mixVolume, Mathf.Max(liquidLevel + 0.3f, 0.55f));
                    liquidTargetColor = Color.Lerp(liquidColor, pourFlask.Colour, liquidLevel > 0.05f ? 0.5f : 1f);
                    break;
                case PourFlask.Content.Powder:
                    solidTarget = Mathf.Max(solidLevel, 0.06f);
                    solidTargetColor = pourFlask.Colour;
                    break;
            }
        }

        private void SetFlameColor(Color c)
        {
            flameColor = c;
            var col = flame.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.Lerp(c, Color.white, 0.6f), 0f),
                              new GradientColorKey(c, 0.35f),
                              new GradientColorKey(Color.Lerp(c, new Color(0.6f, 0.15f, 0.05f), 0.5f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.1f),
                              new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        private ParticleSystem CreateEmitter(string name, Vector3 position, float radius, Material mat)
            => CreateEmitter(transform, name, position, radius, mat);

        /// <summary>An upward cone emitter (world space, nothing emitted until its rate is set).</summary>
        public static ParticleSystem CreateEmitter(Transform parent, string name, Vector3 position, float radius, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // cone +Z → world up
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1.5f;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 4f;
            shape.radius = radius;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;

            ps.Play();
            return ps;
        }

        private static void GrowAndFade(ParticleSystem ps, float growTo)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, growTo));
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            color.color = g;
        }

        // ── Event handlers ─────────────────────────────────────────────

        // Picking a new reagent sets up a new experiment: the beaker shows the new reagent.
        private void OnMaterialPreview(MaterialPreviewChangedEvent e)
        {
            previewState = e.State;
            previewHex = e.ColorHex;
            experimentSignature = ExperimentSignature();
            PrepareFreshBeaker();
            if (instruments != null) instruments.BeginExperiment(e.Formula);   // new reagent A: new experiment
        }

        private string previewState = "liquid", previewHex = string.Empty;

        /// <summary>A clean beaker holding only reagent A, as at the start of every experiment.</summary>
        private void PrepareFreshBeaker()
        {
            reacting = false;
            precipitateOn = flameOn = glowOn = vapor = smokeOn = foamOn = false;
            effervescence = 0;
            reactionDeltaC = 0f;
            heatHeld = false;
            SetPlating(0f);
            beakerVolume = VolumeOfReagentA();
            SetIdle(previewState, previewHex);
            // The mix's results have already arrived; they are shown when the pour reaches the beaker.
            if (instruments != null) instruments.HideReaction();

            // A clean beaker right away: nothing from the previous experiment fades out slowly.
            solidLevel = solidTarget;
            liquidLevel = liquidTarget;
            foamLevel = foamTarget = 0f;
            turbidity = turbidityTarget = 0f;
            solidColor = solidTargetColor;
            liquidColor = liquidTargetColor;
        }

        /// <summary>Beaker before mixing: powder for solids, liquid for liquids/solutions, empty for gases.</summary>
        private void SetIdle(string state, string colorHex)
        {
            string s = (state ?? string.Empty).Trim().ToLowerInvariant();
            bool hasColor = TryParseHex(colorHex, out var c);
            foamTarget = 0f;
            turbidityTarget = 0f;

            if (s == "solid" || s == "s")
            {
                solidTarget = 0.05f;   // a thin layer of powder (its height is illustrative, not a volume)
                liquidTarget = 0f;
                solidTargetColor = hasColor ? new Color(c.r, c.g, c.b, 1f) : PowderColor;
            }
            else if (s == "gas" || s == "g")
            {
                solidTarget = 0f;
                liquidTarget = 0f;
            }
            else
            {
                solidTarget = 0f;
                liquidTarget = LevelFor(beakerVolume, 0.55f);
                liquidTargetColor = hasColor ? new Color(c.r, c.g, c.b, 0.55f) : WaterColor;
            }
        }

        /// <summary>The beaker holds a water-based solution (aqueous reagent or reaction).</summary>
        private bool AqueousBeaker()
        {
            if (previewState == "aqueous" || previewState == "aq") return true;
            if (lastReaction == null) return false;
            foreach (var c in lastReaction.reactants) if (c.state == "aq") return true;
            foreach (var c in lastReaction.products) if (c.state == "aq") return true;
            return false;
        }

        // ── Volume ─────────────────────────────────────────────────────

        // Text is rebuilt only when the volume (or whether there is liquid) changes.
        private VolumeReading tagVolume;
        private bool tagLiquid, tagValid;

        private void UpdateVolumeTag()
        {
            if (volumeTag == null) return;
            bool liquidShown = liquidLevel > 0.004f || liquidTarget > 0.004f;
            bool changed = !tagValid || liquidShown != tagLiquid || beakerVolume.Known != tagVolume.Known
                        || beakerVolume.Basis != tagVolume.Basis
                        || (beakerVolume.Known && beakerVolume.Ml != tagVolume.Ml);
            if (changed)
            {
                tagValid = true;
                tagLiquid = liquidShown;
                tagVolume = beakerVolume;
                var text = VolumeTagText(liquidShown);
                volumeTag.Hidden = text == null;
                if (text != null)
                {
                    volumeTag.Value.text = text[0];
                    volumeTag.Caption.text = text[1];
                }
            }
            if (!volumeTag.Hidden)
                volumeTag.LocalPoint = new Vector3(OuterRadius, LiquidBottom + liquidLevel * MaxFill, 0f);
        }

        /// <summary>The amount of reagent slot 0–3 as the engine receives it (same data, same helper).</summary>
        private bool TryAmount(int slot, string formula, out ReagentAmountInput amount)
        {
            amount = default;
            if (labInput == null || string.IsNullOrWhiteSpace(formula)) return false;
            labInput.TryGetMaterial(formula.Trim(), out var mat);
            labInput.TryGetAmount(slot, out double value, out string unit);
            amount = LabInputController.AmountInputFor(formula.Trim(), mat, value, unit);
            return true;
        }

        /// <summary>Volume of reagent A alone (the beaker before mixing).</summary>
        private VolumeReading VolumeOfReagentA()
        {
            if (labInput == null) labInput = FindObjectOfType<LabInputController>();
            return labInput != null && TryAmount(0, labInput.CurrentInput.ReagentA, out var a)
                ? VesselVolume.Of(a) : VolumeReading.Unknown("no reagent A");
        }

        /// <summary>Volume of everything in the mix (volumes taken as additive).</summary>
        private VolumeReading VolumeOfMix()
        {
            if (labInput == null) labInput = FindObjectOfType<LabInputController>();
            if (labInput == null) return VolumeReading.Unknown("no reagents");
            var input = labInput.CurrentInput;
            var formulas = new[] { input.ReagentA, input.ReagentB, input.ReagentC, input.ReagentD };
            var amounts = new List<ReagentAmountInput>();
            for (int i = 0; i < formulas.Length; i++)
                if (TryAmount(i, formulas[i], out var a)) amounts.Add(a);
            return VesselVolume.Total(amounts);
        }

        /// <summary>Liquid height (fraction of MaxFill) for a volume; the fallback when it is not known.</summary>
        private float LevelFor(VolumeReading v, float fallback)
        {
            if (!v.Known || beakerScale == null) return fallback;
            return Mathf.Clamp01((float)(beakerScale.HeightOf(v.Ml) / MaxFill));
        }

        private double BrimMl => beakerScale != null ? beakerScale.CapacityMl * MaxFill / beakerScale.CapacityHeight : double.NaN;

        /// <summary>Text of the volume tag for the beaker's liquid, or null to hide it.</summary>
        private string[] VolumeTagText(bool liquidShown)
        {
            if (!liquidShown) return null;
            if (!beakerVolume.Known)
                return new[] { "? mL", "level not to scale: " + beakerVolume.Basis };
            if (beakerVolume.Ml <= 0) return null;
            string ml = beakerVolume.Ml.ToString(beakerVolume.Ml >= 10 ? "0" : "0.#", System.Globalization.CultureInfo.InvariantCulture);
            string caption = beakerVolume.Basis;
            if (beakerVolume.Ml > BrimMl) caption = $"more than the beaker holds (≈ {BrimMl:0} mL)";
            else if (beakerScale.Exceeds(beakerVolume.Ml)) caption += " · above the 100 mL mark";
            return new[] { $"{ml} mL", caption };
        }

        private void OnEnvironment(EnvironmentChangedEvent e)
        {
            benchTemperatureC = e.Temperature;
            if (hotPlate != null) hotPlate.TargetC = e.Temperature;
        }

        private void OnEnvironmentFx(EnvironmentFxChangedEvent e)
        {
            envSteamRate = e.SteamEmissionRate;
            stirSpin = e.ShowVortex ? e.VortexSpinSpeed : 0f;
        }

        private void OnReactionEvaluated(ReactionEvaluatedEvent e)
        {
            lastReaction = e.Input.reaction;
            calculatedDeltaC = float.NaN;   // StoichiometryEvaluatedEvent follows with this mix's value
            mixVolume = VolumeOfMix();
            experimentSignature = ExperimentSignature();
        }

        // Heating lab: the bench shows the model's state — liquid left, ice, boiling, vapour.
        private bool heatingShown, heatingBoiling;
        private float heatingTempC, heatingEvaporation;
        private static readonly Color IceColor = new Color(0.86f, 0.93f, 1f, 0.95f);

        private void OnHeating(HeatingUpdatedEvent e)
        {
            if (!e.Active || !e.Supported)
            {
                if (!heatingShown) return;
                heatingShown = heatingBoiling = false;
                if (hotPlate != null) hotPlate.TargetC = benchTemperatureC;
                if (!reacting && (pourFlask == null || !pourFlask.Busy))
                {
                    beakerVolume = VolumeOfReagentA();
                    foamTarget = 0f;
                    SetIdle(previewState, previewHex);
                }
                return;
            }
            if (!heatingShown && !reacting && (pourFlask == null || !pourFlask.Busy))
                PrepareFreshBeaker();   // heating starts from reagent A alone, not from an earlier mixture
            heatingShown = true;
            heatingTempC = (float)e.TemperatureC;
            heatingBoiling = e.Phase == SamplePhase.Boiling;
            heatingEvaporation = e.EvaporationVisual;
            // The plate's ring shows only that the heater is on (its surface temperature is not modelled).
            if (hotPlate != null) hotPlate.TargetC = e.HeaterOn ? 150f : benchTemperatureC;

            beakerVolume = double.IsNaN(e.LiquidMl)
                ? VolumeReading.Unknown("no density")
                : VolumeReading.Of(e.LiquidMl, e.Phase == SamplePhase.Empty ? "all boiled away" : "liquid left (heating model)");
            liquidTarget = e.LiquidMl > 0 ? LevelFor(beakerVolume, 0f) : 0f;
            // Ice floats on its melt water; on its own it sits on the bottom. Its height follows its volume.
            float ice = e.SolidMl > 0 && beakerScale != null ? Mathf.Clamp01((float)(beakerScale.HeightOf(e.SolidMl) / MaxFill)) : 0f;
            bool floating = e.LiquidMl > 0.5;
            solidTarget = floating ? 0f : ice;
            foamTarget = floating ? ice : 0f;
            if (ice > 0f) solidTargetColor = IceColor;
        }

        // Titration: the beaker fills with the added titrant and takes the indicator's colour.
        private bool titrationShown;

        private void OnTitration(TitrationUpdatedEvent e)
        {
            if (!e.Active || !e.Supported)
            {
                if (!titrationShown) return;
                titrationShown = false;
                if (!reacting && (pourFlask == null || !pourFlask.Busy))
                {
                    beakerVolume = VolumeOfReagentA();
                    SetIdle(previewState, previewHex);
                }
                return;
            }
            titrationShown = true;
            SetIdle(previewState, previewHex);
            beakerVolume = VolumeReading.Of(e.AnalyteMl + e.Point.AddedMl,
                $"{e.AnalyteMl:0.##} mL + {e.Point.AddedMl:0.##} mL added (additive)");
            liquidTarget = LevelFor(beakerVolume, 0.55f);
            if (e.Indicator >= 0 && e.Indicator < ChemLabSimV3.Engine.Chemistry.Indicators.All.Length)
            {
                var c = ChemLabSimV3.Engine.Chemistry.Indicators.ColourAt(ChemLabSimV3.Engine.Chemistry.Indicators.All[e.Indicator], e.Point.Ph);
                if (c != null) liquidTargetColor = new Color(c[0], c[1], c[2], c[3]);
            }
        }
        private void OnReactionNotFound(ReactionNotFoundEvent e)
        {
            lastReaction = null;
            calculatedDeltaC = float.NaN;
        }

        private void OnStoichiometry(StoichiometryEvaluatedEvent e)
            => calculatedDeltaC = (float)LabInstruments.RiseAfterMix(e, benchTemperatureC, out _);

        private void OnFx(FxTriggeredEvent e)
        {
            pendingFx = e.State;
            fxPending = true;
        }

        private void ApplyFx(FxState fx, ReactionEntry reaction)
        {
            if (instruments != null) instruments.RevealReaction();
            if (fx.StopAll)
            {
                effervescence = 0;
                vapor = smokeOn = glowOn = foamOn = precipitateOn = flameOn = false;
                reactionDeltaC = 0f;
                foamTarget = 0f;
                turbidityTarget = 0f;
            }
            if (!fx.PlaySuccess)
            {
                reacting = false;
                return;
            }

            reacting = true;
            reactionTime = 0f;

            Phase phase = PhaseOf(reaction);
            electrolysis = ElectricityCondition.IsElectrolysis(reaction);
            plating = electrolysis && fx.PlaySuccess && TryParseHex(fx.PrecipitateColorHex, out platingColor);
            if (!plating) SetPlating(0f);
            SplitElectrodeGas(reaction);
            Color target = Color.clear;
            bool hasColour = fx.PlayColorChange && TryParseHex(fx.ColorChangeHex, out target);

            if (phase == Phase.Solution)
            {
                // The products end up as a solution in the beaker; its volume is the mix's.
                beakerVolume = mixVolume;
                liquidTarget = LevelFor(mixVolume, Mathf.Max(liquidLevel, 0.6f));
                solidTarget = 0f;
                liquidTargetColor = hasColour ? new Color(target.r, target.g, target.b, 0.6f) : WaterColor;
            }
            else
            {
                // Solids and gases only: no liquid. A solid product replaces the solid reactant
                // (grey Mg → white MgO); with gaseous products only, the solid is used up.
                // A dry reaction starts in a clean, dry vessel.
                liquidTarget = phase == Phase.Molten ? 0.16f : 0f;
                beakerVolume = VolumeReading.Of(0, "no liquid");   // a melt is not a measured volume
                foamLevel = turbidity = turbidityTarget = 0f;
                if (phase == Phase.Molten) liquidTargetColor = liquidColor = MoltenColor;
                else liquidLevel = 0f;
                bool solidProduct = HasProduct(reaction, "s");
                solidTarget = solidProduct ? Mathf.Max(solidLevel, 0.08f) : 0f;
                if (solidProduct)
                    solidTargetColor = TryParseHex(fx.PrecipitateColorHex, out var p0) ? new Color(p0.r, p0.g, p0.b, 1f)
                                     : hasColour ? new Color(target.r, target.g, target.b, 1f)
                                     : PowderColor;
            }

            flameOn = phase != Phase.Solution && Burns(reaction, fx);
            if (flameOn) SetFlameColor(FlameColour(reaction));

            precipitateOn = fx.PlayPrecipitate && phase == Phase.Solution;
            if (precipitateOn)
            {
                solidTargetColor = TryParseHex(fx.PrecipitateColorHex, out var p) ? new Color(p.r, p.g, p.b, 1f) : PowderColor;
                solidColor = solidTargetColor;
                turbidity = 0f;
                turbidityTarget = 1f;
            }

            effervescence = fx.EffervescenceLevel > 0 ? fx.EffervescenceLevel : (fx.PlayGas ? 1 : 0);
            foamOn = fx.PlayFoam;
            foamTarget = foamOn ? 0.25f : 0f;
            vapor = fx.PlayVapor;
            smokeOn = fx.PlaySmoke;
            glowOn = fx.PlayGlow || phase == Phase.Molten;
            reactionDeltaC = fx.TemperatureDelta;
            if (reactionDeltaC <= -8f) frost = 1f;

            // A change calculated from the student's amounts replaces the record's estimate.
            heatHeld = fx.PlaySuccess && !float.IsNaN(calculatedDeltaC);
            if (heatHeld)
            {
                reactionDeltaC = calculatedDeltaC;
                glowOn = false;
            }

            if (fx.PlaySparks)
                sparks.Emit(90);
        }

        // ── Per-frame update ───────────────────────────────────────────

        private void Update()
        {
            if (fxPending)
            {
                fxPending = false;
                var fx = pendingFx;
                var reaction = lastReaction;
                // Every Mix starts from a clean beaker with reagent A; reagent B is poured in and
                // the reaction starts as the stream reaches the beaker.
                if (pourFlask != null && pourFlask.Holds != PourFlask.Content.Empty && !pourFlask.Busy)
                {
                    PrepareFreshBeaker();
                    pourFlask.Pour(() => { AddPouredReagent(); ApplyFx(fx, reaction); }, null);
                }
                else
                    ApplyFx(fx, reaction);
            }

            float dt = Time.deltaTime;
            if (reacting) reactionTime += dt;

            // Reaction heat fades back toward the bench temperature over ~20 s; a calculated change
            // is held, as the thermometer holds it (its model has no heat loss).
            float heatFade = reacting ? (heatHeld ? 1f : Mathf.Exp(-reactionTime / 20f)) : 0f;
            float temperatureC = heatingShown ? heatingTempC : benchTemperatureC + reactionDeltaC * heatFade;
            BeakerTemperatureC = temperatureC;

            // Precipitate: the liquid clouds up first, then the solid settles to the bottom.
            if (precipitateOn)
            {
                if (reactionTime > 1.5f) turbidityTarget = 0.25f;
                solidTarget = Mathf.Lerp(0f, 0.12f, Mathf.Clamp01((reactionTime - 0.8f) / 5f));
            }
            if (foamOn && reactionTime > 7f) foamTarget = 0.06f;

            solidLevel  = Approach(solidLevel, solidTarget, 0.6f, dt);
            liquidLevel = Approach(liquidLevel, liquidTarget, 1.2f, dt);
            foamLevel   = Approach(foamLevel, foamTarget, 1.5f, dt);
            turbidity   = Approach(turbidity, turbidityTarget, 1.5f, dt);
            liquidColor = Color.Lerp(liquidColor, liquidTargetColor, 1f - Mathf.Exp(-dt * 1.4f));
            solidColor  = Color.Lerp(solidColor, solidTargetColor, 1f - Mathf.Exp(-dt * 2f));
            // Condensation on a cold beaker: held with a calculated cooling (full at −8 K), else it clears.
            frost       = heatHeld && reactionDeltaC < 0f ? Mathf.Clamp01(-reactionDeltaC / 8f) : Mathf.Max(0f, frost - dt / 25f);

            UpdateLiquid(temperatureC);
            UpdateVolumeTag();
            UpdateCameraFraming(Time.unscaledDeltaTime);
            UpdateParticles(temperatureC);
            UpdateGlassAndGlow(temperatureC, heatFade);
        }

        private void UpdateLiquid(float temperatureC)
        {
            // With liquid present its surface is the volume line: a precipitate settles inside the
            // liquid (its own volume is not modelled), it does not lift the surface.
            bool hasLiquid = liquidLevel > 0.004f;
            float liquidBand = hasLiquid ? Mathf.Max(0f, liquidLevel - solidLevel) : 0f;
            float body = hasLiquid ? Mathf.Max(liquidLevel, solidLevel) : solidLevel;
            float total = Mathf.Clamp(body + foamLevel, 0f, 1f);
            float meshHeight = Mathf.Max(0.0005f, total * MaxFill);
            liquid.localScale = new Vector3(1f, meshHeight, 1f);
            liquid.gameObject.SetActive(total > 0.004f);

            var shownLiquid = Color.Lerp(liquidColor, new Color(solidColor.r, solidColor.g, solidColor.b, 0.85f), turbidity * 0.8f);
            if (meniscus != null)
            {
                meniscus.gameObject.SetActive(hasLiquid);
                if (hasLiquid)
                {
                    meniscus.localPosition = new Vector3(0f, LiquidBottom + body * MaxFill, 0f);
                    meniscusMat.SetColor("_BaseColor", new Color(Mathf.Lerp(shownLiquid.r, 1f, 0.55f), Mathf.Lerp(shownLiquid.g, 1f, 0.55f),
                                                                 Mathf.Lerp(shownLiquid.b, 1f, 0.55f), 0.55f));
                }
            }

            if (!liquidShaderOk)
            {
                liquidMat.SetColor("_BaseColor", total > 0f ? shownLiquid : Color.clear);
                return;
            }

            // Band heights are fractions of the column; foam gets a small margin above the top
            // so the cap is never cut by the shader's soft top edge.
            float inv = total > 0f ? 1f / total : 0f;
            liquidMat.SetFloat("_ContainerBottomY", liquid.position.y);
            liquidMat.SetFloat("_ContainerHeight", meshHeight * transform.lossyScale.y);
            liquidMat.SetFloat("_SolidHeight", solidLevel * inv);
            liquidMat.SetFloat("_LiquidHeight", liquidBand * inv);
            liquidMat.SetFloat("_FoamHeight", foamLevel * inv + 0.03f);
            liquidMat.SetColor("_LiquidColor", shownLiquid);
            liquidMat.SetColor("_TopColor", Color.Lerp(shownLiquid, Color.white, 0.25f));
            liquidMat.SetColor("_SolidColor", solidColor);
            liquidMat.SetColor("_FoamColor", foamLevel > 0.01f ? new Color(0.86f, 0.9f, 0.95f, 0.7f) : shownLiquid);
            liquidMat.SetFloat("_FoamAmount", Mathf.Clamp01(foamLevel / 0.05f));   // no foam grain on a calm surface
            liquidMat.SetFloat("_SolidSettle", precipitateOn && reactionTime < 4f ? 0.2f : 0.9f);

            float agitation = effervescence * 0.004f + Mathf.Clamp01(stirSpin / 360f) * 0.02f;
            float boil = temperatureC >= 100f ? 0.012f : 0f;
            liquidMat.SetFloat("_WobbleX", agitation + boil);
            liquidMat.SetFloat("_WobbleZ", agitation + boil);
            liquidMat.SetFloat("_BubbleIntensity", Mathf.Clamp01(effervescence / 3f + boil * 40f));
            liquidMat.SetFloat("_LiquidAnimSpeed", 1f + stirSpin / 90f);
        }

        private void UpdateParticles(float temperatureC)
        {
            float fade = reacting ? Mathf.Clamp01(1f - reactionTime / 10f) : 0f;
            float liquidHeight = liquidLevel * MaxFill;

            // Bubbles rise only through the liquid.
            float bubbleRate = 0f;
            if (liquidHeight > 0.05f)
            {
                bubbleRate = new[] { 0f, 35f, 110f, 260f }[Mathf.Clamp(effervescence, 0, 3)] * fade;
                if (heatingShown) bubbleRate = heatingBoiling ? 160f : 0f;           // boiling, as the heating model says
                else if (temperatureC >= 100f && AqueousBeaker()) bubbleRate += 80f;  // water-based solution near 100 °C
                                                                                      // (boiling-point elevation not modelled)
                var main = bubbles.main;
                main.startLifetime = liquidHeight / 0.4f;
                bubbles.transform.position = liquid.position + Vector3.up * (solidLevel * MaxFill + 0.02f);
            }

            // Electroplating: the metal builds up on the cathode while the current flows.
            if (plating && currentOn && reacting) SetPlating(reactionTime / 6f);

            // Electrolysis: gas forms only at the electrodes, and only while current flows.
            if (electrolysis && currentOn && reacting && liquidHeight > 0.05f)
            {
                float rate = Mathf.Max(bubbleRate, 60f);
                float surfaceY = liquid.position.y + (solidLevel + liquidLevel) * MaxFill;
                foreach (var ps in new[] { cathodeBubbles, anodeBubbles })
                {
                    var m = ps.main;
                    m.startLifetime = Mathf.Max(0.1f, (surfaceY - ps.transform.position.y) / 0.55f);   // pop at the surface
                }
                SetRate(cathodeBubbles, rate * cathodeShare);
                SetRate(anodeBubbles, rate * anodeShare);
                bubbleRate = 0f;
            }
            else
            {
                SetRate(cathodeBubbles, 0f);
                SetRate(anodeBubbles, 0f);
            }
            SetRate(bubbles, bubbleRate);

            // Steam: from the bench temperature (slider) and from a hot reaction.
            float hot = Mathf.Clamp01((temperatureC - 55f) / 45f);
            float steamRate = Mathf.Max(envSteamRate, hot * 30f);
            if (vapor) steamRate = Mathf.Max(steamRate, 25f * Mathf.Max(fade, 0.2f));
            // Heating lab: vapour pours off while boiling; below the boiling point a few faint wisps
            // stand for evaporation at the surface (illustrative: its rate is not modelled).
            if (heatingShown) steamRate = heatingBoiling ? 40f : 8f * heatingEvaporation * heatingEvaporation;
            if (liquidHeight < 0.02f && !vapor) steamRate = 0f;
            SetRate(steam, steamRate);

            SetRate(smoke, smokeOn ? 30f * Mathf.Clamp01(1f - reactionTime / 6f) : 0f);

            // A burning solid flares up, then dies down over ~8 s.
            float burn = flameOn ? Mathf.Clamp01(reactionTime / 0.4f) * Mathf.Clamp01(1f - (reactionTime - 4f) / 4f) : 0f;
            flame.transform.position = liquid.position + Vector3.up * ((solidLevel + liquidLevel) * MaxFill + 0.03f);
            SetRate(flame, 160f * burn);
        }

        private void UpdateGlassAndGlow(float temperatureC, float heatFade)
        {
            var tint = Color.Lerp(GlassTint, FrostTint, frost);
            glassFrontMat.SetColor("_BaseColor", tint);
            glassBackMat.SetColor("_BaseColor", tint);

            // Only a clearly hot reaction glows; a few degrees of warming is felt, not seen.
            float reactionGlow = heatHeld ? 0f : Mathf.InverseLerp(GlowStartC, GlowFullC, reactionDeltaC) * heatFade;
            if (glowOn) reactionGlow = Mathf.Max(reactionGlow, Mathf.Clamp01(1f - reactionTime / 6f));
            float glow = reactionGlow;   // heating from below is shown by the hot plate

            float burn = flameOn ? Mathf.Clamp01(1f - (reactionTime - 4f) / 4f) : 0f;
            glowLight.color = burn > 0f ? flameColor : HeatColor;
            glowLight.intensity = Mathf.Max(glow * 4f, burn * (flameColor.b > 0.85f && flameColor.r > 0.95f ? 9f : 5f));
            if (liquidShaderOk)
            {
                liquidMat.SetFloat("_GlowIntensity", glow);
                liquidMat.SetColor("_GlowColor", HeatColor);
            }
        }

        // ── Chemistry → look ────────────────────────────────────────────

        private const float MoltenMinC = 300f;

        private static Phase PhaseOf(ReactionEntry r)
        {
            if (r == null) return Phase.Solution;
            bool any(List<ReactionChemical> list, string state) => list != null && list.Exists(c => c.state == state);
            if (any(r.reactants, "aq") || any(r.products, "aq") || any(r.reactants, "l")) return Phase.Solution;
            if (any(r.products, "l") && r.activationTempC >= MoltenMinC) return Phase.Molten;
            if (any(r.products, "l")) return Phase.Solution;
            return Phase.Dry;
        }

        private static bool HasProduct(ReactionEntry r, string state)
            => r?.products != null && r.products.Exists(c => c.state == state);

        private static bool Burns(ReactionEntry r, FxState fx)
            => fx.PlayGlow || fx.PlaySparks || (r != null && r.reactionType == "Combustion");

        private static bool HasReactant(ReactionEntry r, params string[] formulas)
            => r?.reactants != null && r.reactants.Exists(c => System.Array.IndexOf(formulas, c.formula) >= 0);

        /// <summary>Flame colour of the burning element (flame-test colours).</summary>
        private static Color FlameColour(ReactionEntry r)
        {
            if (HasReactant(r, "Mg", "Al"))  return new Color(1.00f, 1.00f, 0.96f);   // blinding white
            if (HasReactant(r, "Na"))        return new Color(1.00f, 0.80f, 0.20f);   // yellow-orange
            if (HasReactant(r, "K"))         return new Color(0.78f, 0.55f, 1.00f);   // lilac
            if (HasReactant(r, "Li"))        return new Color(1.00f, 0.18f, 0.30f);   // crimson
            if (HasReactant(r, "Ca"))        return new Color(1.00f, 0.42f, 0.18f);   // orange-red
            if (HasReactant(r, "Cu"))        return new Color(0.30f, 1.00f, 0.50f);   // green
            if (HasReactant(r, "Ba"))        return new Color(0.70f, 1.00f, 0.40f);   // pale green
            if (HasReactant(r, "Sr"))        return new Color(1.00f, 0.10f, 0.10f);   // red
            if (HasReactant(r, "S", "H2", "CH4", "C2H6", "C3H8", "C4H10", "CO"))
                                             return new Color(0.40f, 0.58f, 1.00f);   // blue
            return DefaultFlame;
        }

        // ── Helpers ────────────────────────────────────────────────────

        private static void SetRate(ParticleSystem ps, float rate)
        {
            var emission = ps.emission;
            emission.rateOverTime = rate;
        }

        private static float Approach(float current, float target, float speed, float dt)
            => Mathf.Lerp(current, target, 1f - Mathf.Exp(-dt * speed * 2f));

        private static bool TryParseHex(string hex, out Color color)
        {
            color = Color.clear;
            if (string.IsNullOrWhiteSpace(hex)) return false;
            string s = hex.Trim();
            if (!s.StartsWith("#")) s = "#" + s;
            return ColorUtility.TryParseHtmlString(s, out color);
        }
    }
}
