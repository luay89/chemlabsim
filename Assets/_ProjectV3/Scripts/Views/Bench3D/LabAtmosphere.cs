// ChemLabSim v3 — lab atmosphere for the 3D bench
// • Post-processing: bloom (flames, glow and sparks really shine), ACES tone mapping,
//   a light vignette and anti-aliasing.
// • Background: a wall shelf with reagent bottles, slightly out of focus by being dim,
//   so the bench reads as a real lab without drawing attention from the beaker.
// Everything is created in code from materials the build already includes.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ChemLabSimV3.Views.Bench3D
{
    public static class LabAtmosphere
    {
        public const string ProfileResource = "RuntimeShaders/LabPostProcessing";

        /// <summary>Bloom, ACES tone mapping, colour and vignette settings of the lab (used to write the asset).</summary>
        public static VolumeProfile CreateProfile()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.85f);
            bloom.scatter.Override(0.72f);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var colour = profile.Add<ColorAdjustments>(true);
            colour.postExposure.Override(0.45f);   // ACES darkens; bring the scene back up
            colour.contrast.Override(8f);
            colour.saturation.Override(6f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.45f);
            return profile;
        }

        public static void SetUp(Camera cam, Transform parent, bool lowEnd)
        {
            SetUpPostProcessing(cam, parent, lowEnd);
            BuildShelf(parent);
        }

        private static void SetUpPostProcessing(Camera cam, Transform parent, bool lowEnd)
        {
            if (cam == null) return;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = lowEnd ? AntialiasingMode.FastApproximateAntialiasing
                                       : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cam.allowHDR = true;

            // The profile is an asset (written by the build scripts) so the build keeps the
            // bloom/tonemapping shader variants; a profile made only in code gets them stripped.
            var profile = Resources.Load<VolumeProfile>(ProfileResource);
            if (profile == null)
            {
                Debug.LogWarning($"[LabAtmosphere] Resources/{ProfileResource} missing; post-processing is off.");
                return;
            }

            var go = new GameObject("LabPostProcessing");
            go.transform.SetParent(parent, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
        }

        // Liquids people expect on a lab shelf (colours of the real solutions).
        private static readonly Color[] BottleColours =
        {
            new Color(0.23f, 0.52f, 0.86f, 0.75f),   // CuSO4
            new Color(0.48f, 0.12f, 0.64f, 0.80f),   // KMnO4
            new Color(0.96f, 0.49f, 0.10f, 0.75f),   // K2Cr2O7
            new Color(0.72f, 0.85f, 0.66f, 0.55f),   // FeSO4
            new Color(0.85f, 0.90f, 0.95f, 0.35f),   // HCl
            new Color(0.79f, 0.64f, 0.15f, 0.75f),   // FeCl3
            new Color(0.30f, 0.69f, 0.31f, 0.70f),   // NiCl2
            new Color(0.88f, 0.48f, 0.61f, 0.65f),   // CoCl2
        };

        private static void BuildShelf(Transform parent)
        {
            var root = new GameObject("WallShelf").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(0f, 0.92f, 2.6f);   // a band of bottles behind the beaker

            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            board.transform.SetParent(root, false);
            board.transform.localScale = new Vector3(6f, 0.05f, 0.45f);
            board.GetComponent<Renderer>().sharedMaterial = Bench3DMeshes.Lit(new Color(0.16f, 0.12f, 0.09f), 0.35f);
            Object.Destroy(board.GetComponent<Collider>());

            var glass = Bench3DMeshes.Glass(new Color(0.12f, 0.14f, 0.16f, 0.25f), 2985, CullMode.Back);
            var random = new System.Random(11);
            float x = -2.6f;
            int i = 0;
            while (x < 2.6f)
            {
                float r = 0.08f + (float)random.NextDouble() * 0.06f;
                float h = 0.28f + (float)random.NextDouble() * 0.22f;
                if (Mathf.Abs(x) > 0.05f)   // keep the space right behind the beaker clear
                    AddBottle(root, new Vector3(x + r, 0.025f, (float)random.NextDouble() * 0.12f - 0.06f),
                              r, h, glass, BottleColours[i++ % BottleColours.Length]);
                x += r * 2f + 0.07f + (float)random.NextDouble() * 0.12f;
            }
        }

        private static void AddBottle(Transform parent, Vector3 pos, float radius, float height, Material glass, Color liquid)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Bottle";
            body.transform.SetParent(parent, false);
            body.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            body.transform.localPosition = pos + Vector3.up * height * 0.5f;
            body.GetComponent<Renderer>().sharedMaterial = glass;
            Object.Destroy(body.GetComponent<Collider>());

            var fill = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fill.name = "Liquid";
            fill.transform.SetParent(parent, false);
            float level = height * (0.45f + liquid.a * 0.3f);
            fill.transform.localScale = new Vector3(radius * 1.8f, level * 0.5f, radius * 1.8f);
            fill.transform.localPosition = pos + Vector3.up * (level * 0.5f + 0.01f);
            fill.GetComponent<Renderer>().sharedMaterial = Bench3DMeshes.Glass(liquid, 2984, CullMode.Back);
            Object.Destroy(fill.GetComponent<Collider>());

            var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name = "Cap";
            cap.transform.SetParent(parent, false);
            cap.transform.localScale = new Vector3(radius * 1.1f, 0.025f, radius * 1.1f);
            cap.transform.localPosition = pos + Vector3.up * (height + 0.025f);
            cap.GetComponent<Renderer>().sharedMaterial = Bench3DMeshes.Lit(new Color(0.08f, 0.08f, 0.09f), 0.5f);
            Object.Destroy(cap.GetComponent<Collider>());
        }
    }
}
