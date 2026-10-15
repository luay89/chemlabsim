// ChemLabSim v3 — the second reagent, poured into the beaker
// A 50 mL graduated cylinder next to the beaker holds reagent B in its real state and colour
// (solution, liquid, powder or gas). A liquid stands at the mark of its measured volume
// (GraduatedScale; illustrative level when the volume cannot be worked out). On Mix it lifts,
// tilts over the beaker and pours: a liquid stream, falling grains or a drifting gas — then
// goes back to its place.

using System;
using System.Collections;
using ChemLabSimV3.Engine.Chemistry;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemLabSimV3.Views.Bench3D
{
    public class PourFlask : MonoBehaviour
    {
        public enum Content { Empty, Liquid, Powder, Gas }

        // 50 mL graduated cylinder: tall and narrow, on a wider foot.
        private const float Radius = 0.075f;
        private const float InnerRadius = 0.068f;
        private const float FlaskHeight = 0.74f;
        private const float InnerBottom = 0.03f;          // glass base: zero volume is here
        private const float CapacityHeight = 0.58f;       // the 50 mL mark above the inner bottom
        private const float FootRadius = 0.14f;
        private const int Segments = 40;

        private static readonly Vector3 Home = new Vector3(-0.82f, 0f, 0.15f);
        private static readonly Vector3 LipTarget = new Vector3(-0.15f, 1.17f, 0f);   // lip ends just above the beaker rim (1.10)
        private const float TiltDegrees = -108f;   // tips towards +x, over the beaker

        // Where the cylinder's base must be so the tilted lip lands on LipTarget.
        private static Vector3 PourPoint => LipTarget - Quaternion.Euler(0f, 0f, TiltDegrees) * new Vector3(Radius, FlaskHeight, 0f);

        public static readonly GraduatedScale Scale = GraduatedScale.Cylinder50(CapacityHeight);

        private Transform body, fill;
        private Material fillMat;
        private ParticleSystem stream;
        private Content content;
        private Color colour;
        private float baseY;

        public bool Busy { get; private set; }
        public Content Holds => content;
        public Color Colour => colour;

        public bool Overfull => volume.Known && volume.Ml > Scale.CapacityMl * (FlaskHeight - InnerBottom) / CapacityHeight;

        public static PourFlask Create(Transform parent, float standHeight, Texture2D bead, Texture2D soft)
        {
            var go = new GameObject("PourFlask");
            go.transform.SetParent(parent, false);
            var flask = go.AddComponent<PourFlask>();
            flask.baseY = standHeight;
            flask.Build(bead, soft);
            flask.transform.localPosition = Home + Vector3.up * standHeight;
            flask.Set(Content.Empty, Color.clear);
            return flask;
        }

        private void Build(Texture2D bead, Texture2D soft)
        {
            // The flask pivots at its lip so it tips over the beaker's rim like a hand would.
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);

            var v = new System.Collections.Generic.List<Vector3>(); var n = new System.Collections.Generic.List<Vector3>();
            var uv = new System.Collections.Generic.List<Vector2>(); var t = new System.Collections.Generic.List<int>();
            Bench3DMeshes.AddWall(v, n, uv, t, Radius, 0f, FlaskHeight, Segments, inward: false);
            Bench3DMeshes.AddRing(v, n, uv, t, 0f, Radius, 0f, Segments, up: false);
            Bench3DMeshes.AddRing(v, n, uv, t, InnerRadius, Radius, FlaskHeight, Segments, up: true);
            // Foot: a short, wide disc the cylinder stands on.
            Bench3DMeshes.AddWall(v, n, uv, t, FootRadius, 0f, 0.02f, Segments, inward: false);
            Bench3DMeshes.AddRing(v, n, uv, t, Radius, FootRadius, 0.02f, Segments, up: true);
            Bench3DMeshes.AddRing(v, n, uv, t, 0f, FootRadius, 0f, Segments, up: false);
            var glass = new GameObject("Glass");
            glass.transform.SetParent(body, false);
            glass.AddComponent<MeshFilter>().sharedMesh = Bench3DMeshes.Build("PourFlaskGlass", v, n, uv, t);
            var r = glass.AddComponent<MeshRenderer>();
            r.sharedMaterial = Bench3DMeshes.Glass(new Color(0.12f, 0.14f, 0.17f, 0.16f), 3012, CullMode.Back);
            r.shadowCastingMode = ShadowCastingMode.Off;

            // Printed scale: marks every 1 mL, longer every 5 mL, numbers every 10 mL.
            GlasswareScale.Build(body, Scale, new GlasswareScale.Style
            {
                Radius = Radius, ZeroY = InnerBottom, CentreDegrees = 0f,
                MinorLength = 0.035f, MajorLength = 0.06f, MinorThickness = 0.0028f, MajorThickness = 0.0045f,
                FontSize = 0.42f, UnitText = "mL"
            });

            var f = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            f.name = "Contents";
            Destroy(f.GetComponent<Collider>());
            fill = f.transform;
            fill.SetParent(body, false);
            fillMat = Bench3DMeshes.Glass(Color.clear, 3008, CullMode.Back);
            f.GetComponent<Renderer>().sharedMaterial = fillMat;
            f.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            var s = new GameObject("PourStream");
            s.transform.SetParent(transform, false);
            stream = s.AddComponent<ParticleSystem>();
            stream.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = stream.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1.6f;
            main.startSpeed = 0.35f;
            main.startLifetime = 0.55f;
            main.maxParticles = 800;
            var emission = stream.emission;
            emission.rateOverTime = 0f;
            var shape = stream.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.02f;
            var pr = s.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = Bench3DMeshes.Particle(bead, false, 3006);
            pr.renderMode = ParticleSystemRenderMode.Stretch;
            pr.velocityScale = 0.06f;
            pr.lengthScale = 1.2f;
            gasMaterial = Bench3DMeshes.Particle(soft, false, 3006);
            beadMaterial = pr.sharedMaterial;
            stream.Play();
        }

        private Material gasMaterial, beadMaterial;

        /// <summary>The liquid stands at the mark of its volume; illustrative level when it is not known.</summary>
        public static float LiquidHeight(VolumeReading v, float inside)
        {
            if (!v.Known) return inside * 0.6f;
            return Mathf.Clamp((float)Scale.HeightOf(v.Ml), 0.0005f, inside - 0.005f);
        }

        private VolumeReading volume = VolumeReading.Unknown("not set");

        /// <summary>Shows reagent B in the cylinder: what it is, how it looks and (for a liquid) how much.</summary>
        public void Set(Content what, Color c, VolumeReading measured)
        {
            volume = measured;
            Set(what, c);
        }

        private void Set(Content what, Color c)
        {
            content = what;
            colour = c;
            gameObject.SetActive(what != Content.Empty);
            if (what == Content.Empty) return;

            float inside = FlaskHeight - InnerBottom;
            float h = what == Content.Powder ? inside * 0.22f
                    : what == Content.Gas ? inside * 0.92f
                    : LiquidHeight(volume, inside);
            fill.localScale = new Vector3(InnerRadius * 2f, h * 0.5f, InnerRadius * 2f);
            fill.localPosition = new Vector3(0f, InnerBottom + h * 0.5f, 0f);

            Color shown = what switch
            {
                Content.Powder => new Color(c.r, c.g, c.b, 1f),
                Content.Gas    => new Color(c.r, c.g, c.b, Mathf.Min(c.a, 0.22f)),
                _              => new Color(c.r, c.g, c.b, Mathf.Max(0.22f, c.a)),
            };
            fillMat.SetColor("_BaseColor", shown);
            fillMat.SetColor("_Color", shown);
        }

        /// <summary>Lift, tilt over the beaker, pour, go back. <paramref name="poured"/> runs when the stream starts.</summary>
        public void Pour(Action poured, Action finished)
        {
            if (content == Content.Empty || Busy) { poured?.Invoke(); finished?.Invoke(); return; }
            StartCoroutine(PourRoutine(poured, finished));
        }

        private IEnumerator PourRoutine(Action poured, Action finished)
        {
            Busy = true;
            Vector3 home = Home + Vector3.up * baseY;
            Vector3 lip = new Vector3(Radius, FlaskHeight, 0f);   // pouring lip, in flask space

            yield return Move(home, PourPoint, 0f, 0f, 0.55f);
            yield return Move(PourPoint, PourPoint, 0f, TiltDegrees, 0.45f);

            // Stream from the lip into the beaker.
            stream.transform.position = transform.TransformPoint(lip);   // transform is already tilted
            var main = stream.main;
            var renderer = stream.GetComponent<ParticleSystemRenderer>();
            switch (content)
            {
                case Content.Liquid:
                    main.startColor = new Color(Mathf.Lerp(colour.r, 1f, 0.35f), Mathf.Lerp(colour.g, 1f, 0.35f), Mathf.Lerp(colour.b, 1f, 0.35f), 0.85f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.05f);
                    main.gravityModifier = 1.6f;
                    renderer.sharedMaterial = beadMaterial;
                    renderer.renderMode = ParticleSystemRenderMode.Stretch;
                    SetRate(320f);
                    break;
                case Content.Powder:
                    main.startColor = new Color(colour.r, colour.g, colour.b, 1f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.022f);
                    main.gravityModifier = 1.4f;
                    renderer.sharedMaterial = beadMaterial;
                    renderer.renderMode = ParticleSystemRenderMode.Billboard;
                    SetRate(260f);
                    break;
                default:   // gas sinks or drifts down into the beaker (Cl2, NO2 … are denser than air)
                    main.startColor = new Color(colour.r, colour.g, colour.b, Mathf.Max(0.12f, colour.a * 0.5f));
                    main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
                    main.gravityModifier = 0.25f;
                    renderer.sharedMaterial = gasMaterial;
                    renderer.renderMode = ParticleSystemRenderMode.Billboard;
                    SetRate(60f);
                    break;
            }
            poured?.Invoke();

            // Empty the flask while pouring.
            float t = 0f, duration = content == Content.Gas ? 1.2f : 0.9f;
            Vector3 full = fill.localScale, fullPos = fill.localPosition;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / duration);
                fill.localScale = new Vector3(full.x, Mathf.Max(0.0005f, full.y * k), full.z);
                fill.localPosition = new Vector3(fullPos.x, fullPos.y * Mathf.Max(0.01f, k), fullPos.z);
                yield return null;
            }
            SetRate(0f);

            yield return Move(PourPoint, PourPoint, TiltDegrees, 0f, 0.4f);
            yield return Move(PourPoint, home, 0f, 0f, 0.5f);
            Set(content, colour);   // a fresh portion of reagent B for the next experiment
            Busy = false;
            finished?.Invoke();
        }

        private IEnumerator Move(Vector3 from, Vector3 to, float tiltFrom, float tiltTo, float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds));
                transform.localPosition = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.08f * (from == to ? 0f : 1f);
                transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(tiltFrom, tiltTo, k));
                yield return null;
            }
        }

        private void SetRate(float rate)
        {
            var emission = stream.emission;
            emission.rateOverTime = rate;
        }
    }
}
