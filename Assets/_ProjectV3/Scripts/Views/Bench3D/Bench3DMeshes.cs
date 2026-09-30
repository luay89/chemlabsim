// ChemLabSim v3 — Procedural meshes and runtime materials for the 3D lab bench.
// No imported assets: the beaker, liquid column and particles are generated in code
// so the bench works in any scene without prefab or material wiring.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChemLabSimV3.Views.Bench3D
{
    internal static class Bench3DMeshes
    {
        /// <summary>Open cylinder wall between y0 and y1. Normals point outward, or inward when <paramref name="inward"/>.</summary>
        public static void AddWall(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t,
                                   float radius, float y0, float y1, int segments, bool inward)
        {
            int start = v.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v.Add(dir * radius + Vector3.up * y0);
                v.Add(dir * radius + Vector3.up * y1);
                n.Add(inward ? -dir : dir);
                n.Add(inward ? -dir : dir);
                uv.Add(new Vector2(i / (float)segments, 0f));
                uv.Add(new Vector2(i / (float)segments, 1f));
            }
            for (int i = 0; i < segments; i++)
            {
                int a = start + i * 2, b = a + 1, c = a + 2, d = a + 3;
                if (!inward) { t.Add(a); t.Add(b); t.Add(c); t.Add(c); t.Add(b); t.Add(d); }   // clockwise seen from outside
                else         { t.Add(a); t.Add(c); t.Add(b); t.Add(c); t.Add(d); t.Add(b); }
            }
        }

        /// <summary>Flat ring (annulus) at height y facing up or down. innerRadius 0 gives a disc.</summary>
        public static void AddRing(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t,
                                   float innerRadius, float outerRadius, float y, int segments, bool up)
        {
            int start = v.Count;
            var normal = up ? Vector3.up : Vector3.down;
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v.Add(dir * innerRadius + Vector3.up * y);
                v.Add(dir * outerRadius + Vector3.up * y);
                n.Add(normal);
                n.Add(normal);
                uv.Add(new Vector2(0.5f + dir.x * 0.5f * innerRadius / outerRadius, 0.5f + dir.z * 0.5f * innerRadius / outerRadius));
                uv.Add(new Vector2(0.5f + dir.x * 0.5f, 0.5f + dir.z * 0.5f));
            }
            for (int i = 0; i < segments; i++)
            {
                int a = start + i * 2, b = a + 1, c = a + 2, d = a + 3;
                if (!up) { t.Add(a); t.Add(b); t.Add(c); t.Add(c); t.Add(b); t.Add(d); }
                else     { t.Add(a); t.Add(c); t.Add(b); t.Add(c); t.Add(d); t.Add(b); }   // clockwise seen from above
            }
        }

        public static Mesh Build(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Closed cylinder of height 1 (y 0..1) — scaled on Y to follow the liquid level.</summary>
        public static Mesh LiquidColumn(float radius, int segments)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            AddWall(v, n, uv, t, radius, 0f, 1f, segments, false);
            AddRing(v, n, uv, t, 0f, radius, 1f, segments, true);
            AddRing(v, n, uv, t, 0f, radius, 0f, segments, false);
            return Build("LiquidColumn", v, n, uv, t);
        }

        // ── Materials ───────────────────────────────────────────────────

        public static Material Lit(Color color, float smoothness, float metallic = 0f)
        {
            var mat = new Material(FindShader("Universal Render Pipeline/Lit", "Standard"));
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Glossiness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            return mat;
        }

        /// <summary>URP Lit, transparent with premultiplied blending so specular highlights stay bright (glass).</summary>
        public static Material Glass(Color tint, int renderQueue, CullMode cull)
        {
            var mat = Lit(tint, 0.96f);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 1f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_SrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlendAlpha", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.SetFloat("_Cull", (float)cull);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = renderQueue;
            return mat;
        }

        /// <summary>URP particle material. additive = true for glowing sparks.</summary>
        public static Material Particle(Texture2D texture, bool additive, int renderQueue)
        {
            var mat = new Material(FindShader("Universal Render Pipeline/Particles/Unlit", "Sprites/Default"));
            mat.SetTexture("_BaseMap", texture);
            mat.SetTexture("_MainTex", texture);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", additive ? 2f : 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = renderQueue;
            return mat;
        }

        public static Shader FindShader(string preferred, string fallback)
        {
            var shader = Shader.Find(preferred);
            if (shader == null)
            {
                Debug.LogWarning($"[LabBench3D] Shader '{preferred}' not found; using '{fallback}'.");
                shader = Shader.Find(fallback);
            }
            return shader;
        }

        /// <summary>Soft round sprite (white, alpha falls off to the edge).</summary>
        public static Texture2D SoftCircle(int size, float hardness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "SoftCircle" };
            float half = (size - 1) * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                float a = Mathf.Clamp01((1f - d) / Mathf.Max(0.01f, 1f - hardness));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
