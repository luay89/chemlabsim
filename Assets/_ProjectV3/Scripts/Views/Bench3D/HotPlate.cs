// ChemLabSim v3 — hot plate under the beaker
// The beaker stands on a hot plate. Its heating ring glows with the temperature set on the
// slider (dark → dull red → orange → yellow-white, roughly the colour of hot metal), its
// indicator lamp shows when it is on, and it lights the bottom of the beaker.

using UnityEngine;

namespace ChemLabSimV3.Views.Bench3D
{
    public class HotPlate : MonoBehaviour
    {
        public const float Height = 0.09f;   // the beaker stands this much higher than the bench

        private const float AmbientC = 30f;
        private const float FullGlowC = 500f;

        private Material ring, lamp;
        private Light underLight;
        private float shown;   // displayed heat 0..1, eases towards the target like a real element

        public float TargetC { get; set; } = 25f;

        public static HotPlate Create(Transform parent, Texture2D soft)
        {
            var go = new GameObject("HotPlate");
            go.transform.SetParent(parent, false);
            var plate = go.AddComponent<HotPlate>();
            plate.Build(soft);
            return plate;
        }

        private void Build(Texture2D soft)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(transform, false);
            body.transform.localScale = new Vector3(1.25f, Height - 0.012f, 1.15f);
            body.transform.localPosition = new Vector3(0f, (Height - 0.012f) * 0.5f, 0.05f);
            // Mid-grey satin casing: a light glossy casing was the brightest thing in view and pulled
            // the eye away from the beaker.
            body.GetComponent<Renderer>().sharedMaterial = Bench3DMeshes.Lit(new Color(0.40f, 0.42f, 0.45f), 0.4f, 0.2f);
            Destroy(body.GetComponent<Collider>());

            var top = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            top.name = "CeramicTop";
            top.transform.SetParent(transform, false);
            top.transform.localScale = new Vector3(1.0f, 0.006f, 1.0f);
            top.transform.localPosition = new Vector3(0f, Height - 0.006f, 0f);
            top.GetComponent<Renderer>().sharedMaterial = Bench3DMeshes.Lit(new Color(0.05f, 0.05f, 0.06f), 0.85f);
            Destroy(top.GetComponent<Collider>());

            // Heating ring: additive quad lying on the ceramic top, coloured by temperature.
            ring = Bench3DMeshes.Particle(Bench3DMeshes.SoftCircle(128, 0.55f), true, 2995);
            ring.SetColor("_BaseColor", Color.black);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "HeatingRing";
            quad.transform.SetParent(transform, false);
            quad.transform.localScale = new Vector3(0.95f, 0.95f, 1f);
            quad.transform.localPosition = new Vector3(0f, Height + 0.002f, 0f);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.GetComponent<Renderer>().sharedMaterial = ring;
            Destroy(quad.GetComponent<Collider>());

            // Front panel: knob and indicator lamp.
            var knob = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            knob.name = "Knob";
            knob.transform.SetParent(transform, false);
            knob.transform.localScale = new Vector3(0.09f, 0.02f, 0.09f);
            knob.transform.localPosition = new Vector3(0.38f, Height * 0.5f, -0.53f);
            knob.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            knob.GetComponent<Renderer>().sharedMaterial = Bench3DMeshes.Lit(new Color(0.1f, 0.1f, 0.11f), 0.4f);
            Destroy(knob.GetComponent<Collider>());

            lamp = Bench3DMeshes.Particle(soft, true, 2996);
            lamp.SetColor("_BaseColor", Color.black);
            var led = GameObject.CreatePrimitive(PrimitiveType.Quad);
            led.name = "Lamp";
            led.transform.SetParent(transform, false);
            led.transform.localScale = new Vector3(0.05f, 0.05f, 1f);
            led.transform.localPosition = new Vector3(0.24f, Height * 0.5f, -0.531f);
            led.GetComponent<Renderer>().sharedMaterial = lamp;
            Destroy(led.GetComponent<Collider>());

            var lightGo = new GameObject("PlateGlow");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, Height + 0.12f, 0f);
            underLight = lightGo.AddComponent<Light>();
            underLight.type = LightType.Point;
            underLight.range = 1.4f;
            underLight.intensity = 0f;
        }

        /// <summary>Colour of hot metal at a heat level 0..1 (dull red → orange → yellow-white).</summary>
        public static Color HotMetal(float t)
        {
            if (t <= 0f) return Color.black;
            if (t < 0.4f) return Color.Lerp(new Color(0.25f, 0.02f, 0f), new Color(0.9f, 0.12f, 0.02f), t / 0.4f);
            if (t < 0.8f) return Color.Lerp(new Color(0.9f, 0.12f, 0.02f), new Color(1f, 0.45f, 0.08f), (t - 0.4f) / 0.4f);
            return Color.Lerp(new Color(1f, 0.45f, 0.08f), new Color(1f, 0.85f, 0.55f), (t - 0.8f) / 0.2f);
        }

        private void Update()
        {
            float target = Mathf.Clamp01((TargetC - AmbientC) / (FullGlowC - AmbientC));
            // A heating element takes a few seconds to come up to temperature and to cool down.
            shown = Mathf.MoveTowards(shown, target, Time.deltaTime * 0.25f);

            Color hot = HotMetal(shown);
            ring.SetColor("_BaseColor", hot * (1f + shown * 2.5f));          // > 1 so bloom picks it up
            lamp.SetColor("_BaseColor", TargetC > AmbientC ? new Color(2.2f, 0.25f, 0.1f) : new Color(0.15f, 0.02f, 0.02f));
            underLight.color = hot;
            underLight.intensity = shown * 2.5f;
        }
    }
}
