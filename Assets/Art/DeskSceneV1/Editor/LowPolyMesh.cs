using System;
using System.Collections.Generic;
using UnityEngine;

namespace PurpleGlove.Editor
{
    // All measurements and UV distances are in metres. Flat normals are intentional.
    public sealed class LowPolyMesh
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<Material> materials = new List<Material>();
        readonly List<List<int>> triangles = new List<List<int>>();
        public Material[] Materials => materials.ToArray();

        int Slot(Material material)
        {
            int index = materials.IndexOf(material);
            if (index >= 0) return index;
            materials.Add(material); triangles.Add(new List<int>());
            return materials.Count - 1;
        }

        static Vector2 Project(Vector3 p, Vector3 normal)
        {
            var n = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            var vAxis = n.y >= n.x && n.y >= n.z ? Vector3.right : Vector3.up;
            var v = (vAxis - normal * Vector3.Dot(vAxis, normal)).normalized;
            var u = Vector3.Cross(v, normal).normalized;
            return new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v));
        }

        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Material material, Vector3 outward)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-14f) return;
            if (Vector3.Dot(n, outward) < 0) { var swap = b; b = c; c = swap; n = -n; }
            // Vector3.Normalize rounds tiny cross-products to zero; millimetre bevels still need unit normals.
            n /= Mathf.Sqrt(n.sqrMagnitude);
            int slot = Slot(material);
            foreach (var p in new[] { a, b, c })
            {
                triangles[slot].Add(vertices.Count);
                vertices.Add(p); normals.Add(n); uv.Add(Project(p, n));
            }
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Material material, Vector3 outward)
        {
            Triangle(a, b, c, material, outward); Triangle(a, c, d, material, outward);
        }

        public void Polygon(Vector3[] points, Material material, Vector3 outward)
        {
            var center = Vector3.zero;
            foreach (var p in points) center += p;
            center /= points.Length;
            for (int i = 0; i < points.Length; i++)
                Triangle(center, points[i], points[(i + 1) % points.Length], material, outward);
        }

        public void Box(Vector3 center, Vector3 size, Material material, float bevel = 0.002f, Quaternion? rotation = null)
        {
            var q = rotation ?? Quaternion.identity;
            float x = size.x / 2, y = size.y / 2, z = size.z / 2;
            float b = Mathf.Min(Mathf.Max(0.00001f, bevel), Mathf.Min(x, Mathf.Min(y, z)) * 0.45f);
            Vector3[][] rings = new Vector3[4][];
            for (int ring = 0; ring < 4; ring++)
            {
                float inset = ring == 0 || ring == 3 ? b : 0;
                float height = ring == 0 ? -y : ring == 1 ? -y + b : ring == 2 ? y - b : y;
                float rx = x - inset, rz = z - inset, cut = b * 0.8f;
                var local = new[] { new Vector3(-rx + cut, height, -rz), new Vector3(rx - cut, height, -rz), new Vector3(rx, height, -rz + cut), new Vector3(rx, height, rz - cut), new Vector3(rx - cut, height, rz), new Vector3(-rx + cut, height, rz), new Vector3(-rx, height, rz - cut), new Vector3(-rx, height, -rz + cut) };
                rings[ring] = Array.ConvertAll(local, p => center + q * p);
            }
            Polygon(rings[0], material, q * Vector3.down);
            Polygon(rings[3], material, q * Vector3.up);
            for (int ring = 0; ring < 3; ring++)
            for (int i = 0; i < 8; i++)
            {
                int j = (i + 1) % 8;
                var mid = (rings[ring][i] + rings[ring][j] + rings[ring + 1][j] + rings[ring + 1][i]) / 4;
                Quad(rings[ring][i], rings[ring][j], rings[ring + 1][j], rings[ring + 1][i], material, mid - center);
            }
        }

        public void Lathe(Vector3 center, Vector2[] profile, int sides, Material material, bool capBottom = true, bool capTop = true, Quaternion? rotation = null)
        {
            var q = rotation ?? Quaternion.identity;
            for (int row = 0; row < profile.Length - 1; row++)
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides;
                Vector3 p0 = new Vector3(Mathf.Cos(a) * profile[row].x, profile[row].y, Mathf.Sin(a) * profile[row].x);
                Vector3 p1 = new Vector3(Mathf.Cos(b) * profile[row].x, profile[row].y, Mathf.Sin(b) * profile[row].x);
                Vector3 p2 = new Vector3(Mathf.Cos(b) * profile[row + 1].x, profile[row + 1].y, Mathf.Sin(b) * profile[row + 1].x);
                Vector3 p3 = new Vector3(Mathf.Cos(a) * profile[row + 1].x, profile[row + 1].y, Mathf.Sin(a) * profile[row + 1].x);
                var delta = profile[row + 1] - profile[row];
                var outward = q * new Vector3(Mathf.Cos((a + b) / 2) * delta.y, -delta.x, Mathf.Sin((a + b) / 2) * delta.y);
                Quad(center + q * p0, center + q * p1, center + q * p2, center + q * p3, material, outward);
            }
            if (capBottom) Disc(center + q * Vector3.up * profile[0].y, profile[0].x, sides, material, q * Vector3.down);
            if (capTop) Disc(center + q * Vector3.up * profile[profile.Length - 1].y, profile[profile.Length - 1].x, sides, material, q * Vector3.up);
        }

        public void Disc(Vector3 center, float radius, int sides, Material material, Vector3 normal)
        {
            var q = Quaternion.FromToRotation(Vector3.up, normal);
            var points = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2 / sides;
                points[i] = center + q * new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            }
            Polygon(points, material, normal);
        }

        public void Rod(Vector3 a, Vector3 b, float radius, Material material, int sides = 6)
        {
            var q = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            Lathe(a, new[] { new Vector2(radius, 0), new Vector2(radius, Vector3.Distance(a, b)) }, sides, material, true, true, q);
        }

        public void Ring(Vector3 center, float radius, float thickness, Material material, Vector3 normal, int sides = 12)
        {
            var q = Quaternion.FromToRotation(Vector3.forward, normal);
            for (int i = 0; i < sides; i++)
            {
                float a = i * 2 * Mathf.PI / sides, b = (i + 1) * 2 * Mathf.PI / sides;
                Rod(center + q * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius, center + q * new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0) * radius, thickness, material, 4);
            }
        }

        public void Sheet(float width, float depth, Material material, float lift = 0, bool bookPage = false)
        {
            const int columns = 8, rows = 6;
            Vector3 Point(int x, int z)
            {
                float px = (x / (float)columns - 0.5f) * width, pz = (z / (float)rows - 0.5f) * depth;
                float wave = Mathf.Sin(x * 1.83f + z * 0.73f) * 0.0014f;
                float fold = bookPage ? 0.019f * Mathf.Sin(Mathf.Abs(px) / (width / 2) * Mathf.PI) - 0.012f * Mathf.Exp(-Mathf.Abs(px) * 60) : 0.003f * Mathf.Abs(Mathf.Sin(px / width * Mathf.PI * 4));
                if (x == 0 || x == columns) px += Mathf.Sin(z * 2.9f) * 0.0015f;
                if (z == 0 || z == rows) pz += Mathf.Sin(x * 3.7f) * 0.0015f;
                return new Vector3(px, lift + wave + fold, pz);
            }
            for (int z = 0; z < rows; z++)
            for (int x = 0; x < columns; x++)
            {
                var a = Point(x, z); var b = Point(x + 1, z); var c = Point(x + 1, z + 1); var d = Point(x, z + 1);
                Quad(a, b, c, d, material, Vector3.up);
                var t = Vector3.down * 0.0015f;
                Quad(a + t, b + t, c + t, d + t, material, Vector3.down);
                if (x == 0) Quad(a, d, d + t, a + t, material, Vector3.left);
                if (x == columns - 1) Quad(b, c, c + t, b + t, material, Vector3.right);
                if (z == 0) Quad(a, b, b + t, a + t, material, Vector3.back);
                if (z == rows - 1) Quad(d, c, c + t, d + t, material, Vector3.forward);
            }
        }

        public Mesh Create(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv);
            mesh.subMeshCount = triangles.Count;
            for (int i = 0; i < triangles.Count; i++) mesh.SetTriangles(triangles[i], i);
            BakeOutlineNormals(mesh);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            return mesh;
        }

        // Angle-weighted normals welded by position prevent cracks in the expanded outline shell.
        // UV0 and the flat lighting normals are kept intact.
        static void BakeOutlineNormals(Mesh mesh)
        {
            var positions = mesh.vertices;
            var faceNormals = mesh.normals;
            var indices = mesh.triangles;
            var keys = new Vector3Int[positions.Length];
            var sums = new Dictionary<Vector3Int, Vector3>();
            for (int i = 0; i < positions.Length; i++)
                keys[i] = new Vector3Int(Mathf.RoundToInt(positions[i].x * 1000000), Mathf.RoundToInt(positions[i].y * 1000000), Mathf.RoundToInt(positions[i].z * 1000000));
            for (int t = 0; t < indices.Length; t += 3)
            for (int corner = 0; corner < 3; corner++)
            {
                int i = indices[t + corner];
                var a = positions[indices[t + (corner + 1) % 3]] - positions[i];
                var b = positions[indices[t + (corner + 2) % 3]] - positions[i];
                float angle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(a, b) / (a.magnitude * b.magnitude), -1, 1));
                sums.TryGetValue(keys[i], out var sum);
                sums[keys[i]] = sum + faceNormals[i] * angle;
            }
            var smooth = new List<Vector3>(positions.Length);
            for (int i = 0; i < positions.Length; i++)
            {
                var normal = sums[keys[i]];
                smooth.Add(normal.sqrMagnitude > 1e-10f ? normal.normalized : faceNormals[i]);
            }
            mesh.SetUVs(2, smooth);
        }
    }
}
