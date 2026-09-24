using System;
using UnityEngine;

namespace OuterSpace
{
    [Serializable]
    public class RingStyleSet
    {
        public Material A, B, C, D, E, F, G;

        public Material For(char name) => name switch
        {
            'A' => A, 'B' => B, 'C' => C, 'D' => D,
            'E' => E, 'F' => F, 'G' => G, _ => null
        };
    }

    /// <summary>
    /// Each named Saturn ring is a separate renderer and material. Radii are measured from
    /// Saturn's center in kilometers. The F ring's 400 km width is an art approximation;
    /// NASA's fact sheet gives its central radius but no inner and outer edges.
    /// </summary>
    public static class StylizedRing
    {
        const int Segments = 128;

        // NASA NSSDCA Saturnian Rings Fact Sheet, 2022. Sorted outward for hierarchy order.
        static readonly (char name, float innerKm, float outerKm)[] Bands =
        {
            ('D', 66900f, 74510f),
            ('C', 74658f, 91975f),
            ('B', 91975f, 117507f),
            ('A', 122340f, 136780f),
            ('F', 139626f, 140026f),
            ('G', 166000f, 173000f),
            ('E', 180000f, 480000f)
        };

        public static void Create(Transform parent, float bodyRadiusKm, RingStyleSet styles)
        {
            if (styles == null || bodyRadiusKm <= 0) return;
            foreach (var band in Bands)
            {
                Material material = styles.For(band.name);
                if (material == null) continue;
                GameObject host = new($"Ring {band.name}");
                host.layer = parent.gameObject.layer;
                host.transform.SetParent(parent, false);
                MeshFilter filter = host.AddComponent<MeshFilter>();
                MeshRenderer renderer = host.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                filter.sharedMesh = Build(band.name, band.innerKm / (2f * bodyRadiusKm),
                    band.outerKm / (2f * bodyRadiusKm));
            }
        }

        static Mesh Build(char name, float inner, float outer)
        {
            // Four radial rows give every ring its own soft edges without a texture atlas.
            float[] radii = { inner, Mathf.Lerp(inner, outer, .08f),
                Mathf.Lerp(inner, outer, .92f), outer };
            byte[] opacity = { 0, 255, 255, 0 };
            int count = radii.Length * (Segments + 1);
            Vector3[] vertices = new Vector3[count];
            Color32[] colors = new Color32[count];
            // x: which edge the row belongs to, y: ring width. The shader uses them to widen
            // a ring seen edge-on to at least a pixel instead of letting it flicker.
            Vector2[] edges = new Vector2[count];
            int[] triangles = new int[(radii.Length - 1) * Segments * 6];
            for (int band = 0; band < radii.Length; band++)
            {
                for (int segment = 0; segment <= Segments; segment++)
                {
                    float angle = segment * 2f * Mathf.PI / Segments;
                    int index = band * (Segments + 1) + segment;
                    vertices[index] = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radii[band];
                    colors[index] = new Color32(255, 255, 255, opacity[band]);
                    edges[index] = new Vector2(band < 2 ? -1f : 1f, outer - inner);
                }
            }
            int cursor = 0;
            for (int band = 0; band < radii.Length - 1; band++)
            for (int segment = 0; segment < Segments; segment++)
            {
                int a = band * (Segments + 1) + segment;
                int b = a + Segments + 1;
                triangles[cursor++] = a;
                triangles[cursor++] = b;
                triangles[cursor++] = a + 1;
                triangles[cursor++] = a + 1;
                triangles[cursor++] = b;
                triangles[cursor++] = b + 1;
            }
            Mesh mesh = new() { name = $"Ring {name}", vertices = vertices,
                colors32 = colors, uv = edges, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
