// ChemLabSim v3 — titration burette and flame-test burner on the bench (Stage 6)
//   burette  appears above the beaker while a titration is set up; its liquid column goes
//            down with the added volume and a drop falls on every addition
//   burner   to the right of the beaker; a wire loop holds reagent A in the flame. The flame
//            is a normal pale-blue burner flame, coloured only by a FlameTest result for a while;
//            the result itself is read in the Tests tab
//            (TitrationUpdatedEvent / FlameTestEvent; no chemistry here).

using ChemLabSimV3.Engine.Chemistry;
using ChemLabSimV3.Events;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemLabSimV3.Views.Bench3D
{
    public class LabTestRigs : MonoBehaviour
    {
        private const float BuretteMl = 50f;            // a standard 50 mL burette
        private const float BuretteLength = 0.9f;
        private const float BuretteBottom = 1.15f;      // tip just above the beaker rim (1.10)
        private const float FlameSeconds = 8f;
        private static readonly Color BurnerBlue = new Color(0.35f, 0.55f, 1f);
        private static readonly Vector3 BurnerPos = new Vector3(0.70f, -HotPlate.Height, 0.05f);

        private GameObject burette, burner;
        private Transform buretteLiquid;
        private ParticleSystem drops, flame;
        private float flameTimer;   // the test's colour and card last this long, then the flame is plain again
        private Color flameColour = BurnerBlue;

        public static LabTestRigs Create(Transform parent, Texture2D soft)
        {
            var go = new GameObject("LabTestRigs");
            go.transform.SetParent(parent, false);
            var rigs = go.AddComponent<LabTestRigs>();
            rigs.BuildBurette(soft);
            rigs.BuildBurner(soft);
            return rigs;
        }

        private void BuildBurette(Texture2D soft)
        {
            burette = new GameObject("Burette");
            burette.transform.SetParent(transform, false);
            var glass = Bench3DMeshes.Glass(new Color(0.85f, 0.92f, 1f, 0.45f), 3012, CullMode.Back);
            Rod(burette.transform, "Tube", new Vector3(0f, BuretteBottom + 0.08f + BuretteLength * 0.5f, 0.12f), 0.045f, BuretteLength, glass);
            Rod(burette.transform, "Tip", new Vector3(0f, BuretteBottom + 0.04f, 0.12f), 0.012f, 0.08f, glass);
            Rod(burette.transform, "Stopcock", new Vector3(0f, BuretteBottom + 0.07f, 0.12f), 0.02f, 0.03f,
                Bench3DMeshes.Lit(new Color(0.15f, 0.15f, 0.18f), 0.5f));
            buretteLiquid = Rod(burette.transform, "Titrant", Vector3.zero, 0.038f, BuretteLength * 0.96f,
                                Bench3DMeshes.Glass(new Color(0.55f, 0.75f, 1f, 0.7f), 3013, CullMode.Back)).transform;
            drops = LabBench3D.CreateEmitter(burette.transform, "Drops", new Vector3(0f, BuretteBottom, 0.12f), 0.004f,
                                             Bench3DMeshes.Particle(soft, false, 3014));
            var main = drops.main;
            main.startLifetime = 0.35f;
            main.startSpeed = 0f;
            main.startSize = 0.03f;
            main.gravityModifier = 1f;
            main.startColor = new Color(0.8f, 0.9f, 1f, 0.8f);
            drops.transform.localRotation = Quaternion.identity;
            SetFill(0f);
            burette.SetActive(false);
        }

        private void BuildBurner(Texture2D soft)
        {
            burner = new GameObject("BunsenBurner");
            burner.transform.SetParent(transform, false);
            burner.transform.localPosition = BurnerPos;
            var metal = Bench3DMeshes.Lit(new Color(0.55f, 0.57f, 0.6f), 0.7f, 0.7f);
            Rod(burner.transform, "Base", new Vector3(0f, 0.02f, 0f), 0.11f, 0.04f, metal);
            Rod(burner.transform, "Barrel", new Vector3(0f, 0.27f, 0f), 0.035f, 0.46f, metal);
            // Wire loop with the sample, held into the flame from the side away from the beaker.
            var wire = Rod(burner.transform, "Wire", new Vector3(0.17f, 0.62f, 0f), 0.004f, 0.36f,
                           Bench3DMeshes.Lit(new Color(0.8f, 0.8f, 0.82f), 0.8f, 0.9f));
            wire.transform.localRotation = Quaternion.Euler(0f, 0f, -70f);

            flame = LabBench3D.CreateEmitter(burner.transform, "Flame", new Vector3(0f, 0.51f, 0f), 0.025f,
                                             Bench3DMeshes.Particle(soft, true, 3024));
            var main = flame.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.45f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
            main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var size = flame.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            SetFlameColour(BurnerBlue);
            burner.SetActive(false);
        }

        private static GameObject Rod(Transform parent, string name, Vector3 pos, float radius, float length, Material mat)
        {
            var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rod.name = name;
            rod.transform.SetParent(parent, false);
            rod.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
            rod.transform.localPosition = pos;
            rod.GetComponent<Renderer>().sharedMaterial = mat;
            rod.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Destroy(rod.GetComponent<Collider>());
            return rod;
        }

        // ── Events ─────────────────────────────────────────────────────

        private void OnEnable()
        {
            EventBus.Subscribe<TitrationUpdatedEvent>(OnTitration);
            EventBus.Subscribe<FlameTestEvent>(OnFlameTest);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TitrationUpdatedEvent>(OnTitration);
            EventBus.Unsubscribe<FlameTestEvent>(OnFlameTest);
        }

        private void OnTitration(TitrationUpdatedEvent e)
        {
            bool show = e.Active && e.Supported;
            if (burette.activeSelf != show) burette.SetActive(show);
            if (!show) return;
            SetFill((float)e.Point.AddedMl);
            if (e.Point.AddedMl > 0) drops.Emit(3);
        }

        private void OnFlameTest(FlameTestEvent e)
        {
            burner.SetActive(true);
            if (!flame.isPlaying) flame.Play();   // inactive objects stop their particle systems
            flameTimer = FlameSeconds;
            var r = e.Result;
            SetFlameColour(r.Status == FlameTestStatus.Characteristic ? new Color(r.Entry.R, r.Entry.G, r.Entry.B) : BurnerBlue);
        }

        // ── Display ────────────────────────────────────────────────────

        private void SetFill(float addedMl)
        {
            float left = Mathf.Clamp01(1f - addedMl / BuretteMl);
            float full = BuretteLength * 0.96f;
            float h = Mathf.Max(0.002f, full * left);
            buretteLiquid.localScale = new Vector3(0.076f, h * 0.5f, 0.076f);
            buretteLiquid.localPosition = new Vector3(0f, BuretteBottom + 0.08f + h * 0.5f, 0.12f);
        }

        private void SetFlameColour(Color c)
        {
            flameColour = c;
            var col = flame.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.Lerp(c, Color.white, 0.4f), 0f), new GradientColorKey(c, 0.4f),
                              new GradientColorKey(c * 0.7f, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        private void Update()
        {
            if (!burner.activeSelf) return;
            var emission = flame.emission;
            emission.rateOverTime = flameColour == BurnerBlue ? 60f : 160f;
            if (flameTimer > 0f)
            {
                flameTimer -= Time.unscaledDeltaTime;
                if (flameTimer <= 0f) SetFlameColour(BurnerBlue);
            }
        }
    }
}
