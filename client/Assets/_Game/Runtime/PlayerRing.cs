using UnityEngine;

namespace Blastlands.Runtime
{
    public static class PlayerRing
    {
        private const int Segments = 40;

        private static Mesh shared;

        public static void Attach(GameObject player, Color color, float radius, float width, float lift)
        {
            var ring = new GameObject("Ring");
            ring.transform.SetParent(player.transform, false);

            Vector3 parentScale = player.transform.localScale;
            ring.transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);
            ring.transform.localPosition = new Vector3(0f, lift / parentScale.y, 0f);

            ring.AddComponent<MeshFilter>().sharedMesh = Build(radius, width);

            var renderer = ring.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = MatchPalette.CreateEmissive(color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static Mesh Build(float radius, float width)
        {
            if (shared != null)
            {
                return shared;
            }

            var vertices = new Vector3[Segments * 2];
            var normals = new Vector3[Segments * 2];
            var triangles = new int[Segments * 6];
            float inner = Mathf.Max(0f, radius - width);

            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = direction * inner;
                vertices[(i * 2) + 1] = direction * radius;
                normals[i * 2] = Vector3.up;
                normals[(i * 2) + 1] = Vector3.up;

                int next = (i + 1) % Segments;
                int t = i * 6;
                triangles[t] = i * 2;
                triangles[t + 1] = next * 2;
                triangles[t + 2] = (i * 2) + 1;
                triangles[t + 3] = (i * 2) + 1;
                triangles[t + 4] = next * 2;
                triangles[t + 5] = (next * 2) + 1;
            }

            shared = new Mesh { name = "Player ring", vertices = vertices, normals = normals, triangles = triangles };
            shared.RecalculateBounds();
            return shared;
        }
    }
}
