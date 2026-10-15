// ChemLabSim v3 — printed volume scale on a piece of 3D glassware
// One mesh for all the marks (thin bands that follow the curve of the glass, just outside its
// surface) and a world-space number next to every labelled mark, all built once from a
// GraduatedScale, so the marks sit exactly where the scale puts them. Drawn after the glass
// (render queue 3020) so the glass does not wash them out; unlit, so lighting never hides them.

using System.Collections.Generic;
using ChemLabSimV3.Engine.Chemistry;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemLabSimV3.Views.Bench3D
{
    public static class GlasswareScale
    {
        private const int RenderQueue = 3020;
        private const int ArcSegments = 4;
        private static Material markMaterial, labelMaterial;

        public struct Style
        {
            public float Radius;          // outer radius of the glass
            public float ZeroY;           // local height of the zero-volume level (for a burette: its capacity mark)
            public float CentreDegrees;   // where the scale sits around the glass; 0 = facing the camera (−z)
            public float MinorLength, MajorLength;   // mark lengths (world units, along the surface)
            public float MinorThickness, MajorThickness;
            public float FontSize;        // world-space TMP size
            public string UnitText;       // printed above the top mark, e.g. "mL" or "100 mL"
        }

        /// <summary>Builds the scale under <paramref name="parent"/> (vessel space). Returns its root.</summary>
        public static GameObject Build(Transform parent, GraduatedScale scale, Style s)
        {
            var root = new GameObject("Scale_" + scale.Vessel.Replace(' ', '_'));
            root.transform.SetParent(parent, false);

            float r = s.Radius + 0.0015f;   // just outside the glass: no z-fighting, no visible gap
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            var marks = scale.Marks();
            foreach (var m in marks)
            {
                float y = s.ZeroY + (float)m.Height;
                float len = m.Major || m.Labelled ? s.MajorLength : s.MinorLength;
                float thick = m.Major || m.Labelled ? s.MajorThickness : s.MinorThickness;
                float half = len / r * Mathf.Rad2Deg * 0.5f;
                // Marks start at the same edge (left) like printed glassware; longer ones reach further.
                float a0 = s.CentreDegrees - s.MajorLength / r * Mathf.Rad2Deg * 0.5f;
                AddArcBand(v, c, t, r, y, thick, a0, a0 + half * 2f, new Color(0.95f, 0.97f, 1f, 0.9f));
                if (m.Labelled) AddLabel(root.transform, Number(m.Ml), r, y, a0 + half * 2f + 1.5f, s.FontSize, TextAlignmentOptions.MidlineLeft);
            }
            if (!string.IsNullOrEmpty(s.UnitText))
            {
                // Above the top mark (the capacity mark, or a burette's 0 mark).
                float aUnit = s.CentreDegrees - s.MajorLength / r * Mathf.Rad2Deg * 0.5f;
                float unitY = s.ZeroY + (float)scale.CapacityHeight + s.FontSize * 0.12f;
                AddLabel(root.transform, s.UnitText, r, unitY, aUnit, s.FontSize * 0.9f, TextAlignmentOptions.BottomLeft);
            }

            var mesh = new Mesh { name = "ScaleMarks" };
            mesh.SetVertices(v);
            mesh.SetColors(c);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            var go = new GameObject("Marks");
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MarkMaterial();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return root;
        }

        /// <summary>A band of constant height on the cylinder surface between two angles (degrees).</summary>
        private static void AddArcBand(List<Vector3> v, List<Color> c, List<int> t, float r, float y, float thick,
                                       float fromDeg, float toDeg, Color colour)
        {
            int start = v.Count;
            for (int i = 0; i <= ArcSegments; i++)
            {
                float a = Mathf.Lerp(fromDeg, toDeg, i / (float)ArcSegments) * Mathf.Deg2Rad;
                var p = new Vector3(r * Mathf.Sin(a), 0f, -r * Mathf.Cos(a));
                v.Add(p + Vector3.up * (y - thick * 0.5f));
                v.Add(p + Vector3.up * (y + thick * 0.5f));
                c.Add(colour); c.Add(colour);
            }
            for (int i = 0; i < ArcSegments; i++)
            {
                int k = start + i * 2;
                // Facing outwards (towards the camera side of the glass).
                t.Add(k); t.Add(k + 1); t.Add(k + 3);
                t.Add(k); t.Add(k + 3); t.Add(k + 2);
            }
        }

        private static void AddLabel(Transform parent, string text, float r, float y, float angleDeg, float size, TextAlignmentOptions align)
        {
            var go = new GameObject("Label_" + text);
            go.transform.SetParent(parent, false);
            float a = angleDeg * Mathf.Deg2Rad;
            go.transform.localPosition = new Vector3(r * Mathf.Sin(a), y, -r * Mathf.Cos(a));
            go.transform.localRotation = Quaternion.Euler(0f, -angleDeg, 0f);   // tangent to the glass, readable from the front
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSharedMaterial = LabelMaterial(tmp.fontSharedMaterial);   // before any style change
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.enableWordWrapping = false;
            tmp.color = new Color(0.95f, 0.97f, 1f, 0.95f);
            tmp.rectTransform.pivot = new Vector2(0f, 0.5f);
            tmp.rectTransform.sizeDelta = new Vector2(0.3f, 0.06f);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static string Number(double ml) => ml.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

        // Shared by every scale: one material for the marks, one (font) material for the numbers.
        private static Material MarkMaterial()
        {
            if (markMaterial == null)
                markMaterial = Bench3DMeshes.Particle(Texture2D.whiteTexture, false, RenderQueue);
            return markMaterial;
        }

        private static Material LabelMaterial(Material fontMaterial)
        {
            if (labelMaterial == null && fontMaterial != null)
            {
                // A dark outline keeps white numbers readable over pale liquids and precipitates.
                labelMaterial = new Material(fontMaterial) { name = "GlasswareLabel", renderQueue = RenderQueue };
                labelMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
                labelMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
                labelMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.04f, 0.08f, 0.14f, 0.8f));
            }
            return labelMaterial != null ? labelMaterial : fontMaterial;
        }
    }
}
