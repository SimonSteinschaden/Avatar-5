using System.Collections.Generic;
using UnityEngine;

namespace Avatar5.EditorTools.Rendering
{
    /// <summary>Small procedural meshes for the visual-style scene (own work, no third-party assets).</summary>
    public static class ToonProceduralMeshes
    {
        /// <summary>Low-poly rock: jittered icosphere with flat-shaded faces so cel bands read as facets.</summary>
        public static void BuildRock(Mesh mesh, int seed, int subdivisions, float jitter)
        {
            var random = new System.Random(seed);
            GetIcosphere(subdivisions, out var positions, out var triangles);
            var offsets = new Dictionary<Vector3Int, float>();
            for (var i = 0; i < positions.Count; i++)
            {
                var key = Vector3Int.RoundToInt(positions[i] * 1000f);
                if (!offsets.TryGetValue(key, out var r))
                {
                    r = 1f + ((float)random.NextDouble() * 2f - 1f) * jitter;
                    offsets[key] = r;
                }
                var p = positions[i] * r;
                // Flatten the base so rocks sit on the ground.
                if (p.y < -0.35f) p.y = -0.35f + (p.y + 0.35f) * 0.25f;
                positions[i] = p;
            }
            FlatShade(mesh, positions, triangles, "A5_Rock_" + seed);
        }

        /// <summary>Faceted crystal (hexagonal bipyramid).</summary>
        public static void BuildCrystal(Mesh mesh, int sides, float radius, float height, float tipRatio)
        {
            var positions = new List<Vector3>();
            var triangles = new List<int>();
            var top = new Vector3(0f, height, 0f);
            var bottom = new Vector3(0f, -height * tipRatio, 0f);
            for (var i = 0; i < sides; i++)
            {
                var a0 = Mathf.PI * 2f * i / sides;
                var a1 = Mathf.PI * 2f * (i + 1) / sides;
                var p0 = new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                var p1 = new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                AddTriangle(positions, triangles, p0, top, p1);
                AddTriangle(positions, triangles, p1, bottom, p0);
            }
            FlatShade(mesh, positions, triangles, "A5_Crystal");
        }

        /// <summary>Cone along +Y with base at y=0. Flat-shaded when <paramref name="flat"/> (roofs), smooth otherwise (horns).</summary>
        public static void BuildCone(Mesh mesh, int sides, float radius, float height, bool flat)
        {
            var positions = new List<Vector3>();
            var triangles = new List<int>();
            var top = new Vector3(0f, height, 0f);
            for (var i = 0; i < sides; i++)
            {
                var a0 = Mathf.PI * 2f * (i + 0.5f) / sides;
                var a1 = Mathf.PI * 2f * (i + 1.5f) / sides;
                var p0 = new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                var p1 = new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                AddTriangle(positions, triangles, p0, top, p1);
                AddTriangle(positions, triangles, p1, Vector3.zero, p0);
            }
            if (flat)
            {
                FlatShade(mesh, positions, triangles, "A5_Cone");
                return;
            }

            // Smooth: radial normals on the side, flat on the cap.
            var normals = new List<Vector3>();
            for (var i = 0; i < positions.Count; i += 3)
            {
                var isCap = positions[i + 1] == Vector3.zero;
                for (var k = 0; k < 3; k++)
                {
                    var p = positions[i + k];
                    if (isCap)
                    {
                        normals.Add(Vector3.down);
                        continue;
                    }
                    var radial = new Vector3(p.x, 0f, p.z);
                    if (radial.sqrMagnitude < 1e-6f)
                        radial = new Vector3(positions[i].x + positions[i + 2].x, 0f, positions[i].z + positions[i + 2].z);
                    normals.Add((radial.normalized * height + Vector3.up * radius).normalized);
                }
            }
            Assign(mesh, positions, normals, triangles, "A5_Horn");
        }

        public static void BuildTorus(Mesh mesh, float majorRadius, float minorRadius, int majorSegments, int minorSegments)
        {
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (var i = 0; i <= majorSegments; i++)
            {
                var u = Mathf.PI * 2f * i / majorSegments;
                var center = new Vector3(Mathf.Cos(u) * majorRadius, 0f, Mathf.Sin(u) * majorRadius);
                for (var j = 0; j <= minorSegments; j++)
                {
                    var v = Mathf.PI * 2f * j / minorSegments;
                    var n = new Vector3(Mathf.Cos(u) * Mathf.Cos(v), Mathf.Sin(v), Mathf.Sin(u) * Mathf.Cos(v));
                    positions.Add(center + n * minorRadius);
                    normals.Add(n);
                }
            }
            var stride = minorSegments + 1;
            for (var i = 0; i < majorSegments; i++)
            for (var j = 0; j < minorSegments; j++)
            {
                var a = i * stride + j;
                var b = (i + 1) * stride + j;
                triangles.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
            }
            Assign(mesh, positions, normals, triangles, "A5_Torus");
        }

        /// <summary>Subdivided XZ plane centred on the origin (vertex waves need the density).</summary>
        public static void BuildGrid(Mesh mesh, float sizeX, float sizeZ, int cellsX, int cellsZ)
        {
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (var z = 0; z <= cellsZ; z++)
            for (var x = 0; x <= cellsX; x++)
            {
                positions.Add(new Vector3((x / (float)cellsX - 0.5f) * sizeX, 0f, (z / (float)cellsZ - 0.5f) * sizeZ));
                normals.Add(Vector3.up);
                uvs.Add(new Vector2(x / (float)cellsX, z / (float)cellsZ));
            }
            var stride = cellsX + 1;
            for (var z = 0; z < cellsZ; z++)
            for (var x = 0; x < cellsX; x++)
            {
                var a = z * stride + x;
                triangles.AddRange(new[] { a, a + stride, a + 1, a + 1, a + stride, a + stride + 1 });
            }
            mesh.Clear();
            mesh.name = "A5_WaterGrid";
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
        }

        private static void AddTriangle(List<Vector3> positions, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            triangles.Add(positions.Count); positions.Add(a);
            triangles.Add(positions.Count); positions.Add(b);
            triangles.Add(positions.Count); positions.Add(c);
        }

        private static void FlatShade(Mesh mesh, List<Vector3> positions, List<int> triangles, string name)
        {
            // Orient every face away from the mesh centroid, independent of the source winding.
            var centroid = Vector3.zero;
            foreach (var index in triangles) centroid += positions[index];
            centroid /= Mathf.Max(1, triangles.Count);

            var flatPositions = new List<Vector3>(triangles.Count);
            var flatNormals = new List<Vector3>(triangles.Count);
            var flatTriangles = new List<int>(triangles.Count);
            for (var i = 0; i < triangles.Count; i += 3)
            {
                var a = positions[triangles[i]];
                var b = positions[triangles[i + 1]];
                var c = positions[triangles[i + 2]];
                var n = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(n, (a + b + c) / 3f - centroid) < 0f)
                {
                    (b, c) = (c, b);
                    n = -n;
                }
                foreach (var p in new[] { a, b, c })
                {
                    flatTriangles.Add(flatPositions.Count);
                    flatPositions.Add(p);
                    flatNormals.Add(n);
                }
            }
            Assign(mesh, flatPositions, flatNormals, flatTriangles, name);
        }

        private static void Assign(Mesh mesh, List<Vector3> positions, List<Vector3> normals, List<int> triangles, string name)
        {
            // Unity front faces are clockwise; make each triangle agree with its vertex normals.
            for (var i = 0; i < triangles.Count; i += 3)
            {
                var a = positions[triangles[i]];
                var b = positions[triangles[i + 1]];
                var c = positions[triangles[i + 2]];
                var vertexNormal = normals[triangles[i]] + normals[triangles[i + 1]] + normals[triangles[i + 2]];
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), vertexNormal) < 0f)
                    (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
            }

            mesh.Clear();
            mesh.name = name;
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            var uvs = new List<Vector2>(positions.Count);
            foreach (var p in positions) uvs.Add(new Vector2(p.x + p.z, p.y));
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
        }

        private static void GetIcosphere(int subdivisions, out List<Vector3> positions, out List<int> triangles)
        {
            var t = (1f + Mathf.Sqrt(5f)) / 2f;
            positions = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (var i = 0; i < positions.Count; i++) positions[i] = positions[i].normalized;
            triangles = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            for (var s = 0; s < subdivisions; s++)
            {
                var cache = new Dictionary<long, int>();
                var next = new List<int>();
                for (var i = 0; i < triangles.Count; i += 3)
                {
                    var a = triangles[i];
                    var b = triangles[i + 1];
                    var c = triangles[i + 2];
                    var ab = Midpoint(positions, cache, a, b);
                    var bc = Midpoint(positions, cache, b, c);
                    var ca = Midpoint(positions, cache, c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                triangles = next;
            }
        }

        private static int Midpoint(List<Vector3> positions, Dictionary<long, int> cache, int a, int b)
        {
            var key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
            if (cache.TryGetValue(key, out var index)) return index;
            positions.Add(((positions[a] + positions[b]) * 0.5f).normalized);
            index = positions.Count - 1;
            cache[key] = index;
            return index;
        }
    }
}
