using System.Collections.Generic;
using UnityEngine;

namespace Nageel.Environment
{
    [ExecuteInEditMode]
    public class MountainRoadGenerator : MonoBehaviour
    {
        [Header("Road Parameters")]
        public float totalRoadLength = 2500f; // 2.5 Kilometers
        public float roadWidth = 9.0f; // Standard 2-lane mountain highway
        public float segmentLength = 8.0f;
        public float barrierHeight = 0.95f;
        public Material roadMaterial;
        public Material barrierMaterial;
        public Material cliffMaterial;

        [Header("Yemeni Scenery")]
        public GameObject yemeniTowerPrefab;
        public int numberOfTowers = 12;

        [Header("Generated Meshes")]
        public MeshFilter roadMeshFilter;
        public MeshCollider roadMeshCollider;
        public MeshFilter barrierMeshFilter;
        public MeshCollider barrierMeshCollider;

        private void Start()
        {
            GenerateMountainPass();
        }

        [ContextMenu("Regenerate Road")]
        public void GenerateMountainPass()
        {
            int numPoints = Mathf.CeilToInt(totalRoadLength / segmentLength);
            List<Vector3> centerline = GenerateSplinePoints(numPoints);

            BuildRoadMesh(centerline);
            BuildSafetyBarriers(centerline);
            SpawnYemeniWatchtowers(centerline);
        }

        private List<Vector3> GenerateSplinePoints(int numPoints)
        {
            List<Vector3> points = new List<Vector3>(numPoints);

            // Realistic Yemeni Mountain Pass mathematical profile (inspired by Naqil Sumarah & Manakhah)
            // S-curves, hairpin switchbacks, climbing from elevation 100m to 480m over 2.5 km (average 15% incline)
            for (int i = 0; i < numPoints; i++)
            {
                float t = (float)i / numPoints;
                float progressDistance = t * totalRoadLength;

                // Lateral winding (curves and hairpins)
                float x = Mathf.Sin(t * Mathf.PI * 7.5f) * 120f + Mathf.Sin(t * Mathf.PI * 15f) * 45f;

                // Vertical elevation climb with plateaus and steep drops
                float y = Mathf.Pow(t, 0.85f) * 380f + Mathf.Sin(t * Mathf.PI * 4f) * 22f;

                // Forward distance along mountain valley
                float z = progressDistance;

                // Add hairpin switchback bends at t = 0.35 and t = 0.70
                if (t > 0.30f && t < 0.45f)
                {
                    float hairpinFactor = Mathf.Sin((t - 0.30f) / 0.15f * Mathf.PI);
                    x += hairpinFactor * 85f;
                }
                else if (t > 0.65f && t < 0.80f)
                {
                    float hairpinFactor = Mathf.Sin((t - 0.65f) / 0.15f * Mathf.PI);
                    x -= hairpinFactor * 90f;
                }

                points.Add(new Vector3(x, y, z));
            }

            return points;
        }

        private void BuildRoadMesh(List<Vector3> path)
        {
            if (path == null || path.Count < 2) return;

            Mesh roadMesh = new Mesh();
            roadMesh.name = "Yemeni_Naqil_Road_Mesh";

            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            float halfW = roadWidth * 0.5f;

            for (int i = 0; i < path.Count; i++)
            {
                Vector3 current = path[i];
                Vector3 forward = (i < path.Count - 1) ? (path[i + 1] - current).normalized : (current - path[i - 1]).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

                // Outer edge (cliff side)
                Vector3 leftPos = current - right * halfW;
                // Inner edge (mountain side)
                Vector3 rightPos = current + right * halfW;

                vertices.Add(leftPos);
                vertices.Add(rightPos);

                float uvY = (float)i * 0.25f;
                uvs.Add(new Vector2(0f, uvY));
                uvs.Add(new Vector2(1f, uvY));

                if (i < path.Count - 1)
                {
                    int root = i * 2;
                    triangles.Add(root);
                    triangles.Add(root + 2);
                    triangles.Add(root + 1);

                    triangles.Add(root + 1);
                    triangles.Add(root + 2);
                    triangles.Add(root + 3);
                }
            }

            roadMesh.SetVertices(vertices);
            roadMesh.SetUVs(0, uvs);
            roadMesh.SetTriangles(triangles, 0);
            roadMesh.RecalculateNormals();
            roadMesh.RecalculateBounds();

            if (roadMeshFilter == null) roadMeshFilter = gameObject.AddComponent<MeshFilter>();
            if (roadMeshCollider == null) roadMeshCollider = gameObject.AddComponent<MeshCollider>();

            roadMeshFilter.sharedMesh = roadMesh;
            roadMeshCollider.sharedMesh = roadMesh;
        }

        private void BuildSafetyBarriers(List<Vector3> path)
        {
            if (path == null || path.Count < 2) return;

            Mesh barrierMesh = new Mesh();
            barrierMesh.name = "Mountain_Safety_Guardrail_Mesh";

            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            float halfW = roadWidth * 0.5f;

            // Generate barriers along the sheer drop (cliff) side
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 current = path[i];
                Vector3 forward = (i < path.Count - 1) ? (path[i + 1] - current).normalized : (current - path[i - 1]).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

                Vector3 basePos = current - right * (halfW + 0.15f);
                Vector3 topPos = basePos + Vector3.up * barrierHeight;

                vertices.Add(basePos);
                vertices.Add(topPos);

                float uvY = (float)i * 0.5f;
                uvs.Add(new Vector2(0f, uvY));
                uvs.Add(new Vector2(1f, uvY));

                if (i < path.Count - 1)
                {
                    int root = i * 2;
                    triangles.Add(root);
                    triangles.Add(root + 2);
                    triangles.Add(root + 1);

                    triangles.Add(root + 1);
                    triangles.Add(root + 2);
                    triangles.Add(root + 3);
                }
            }

            barrierMesh.SetVertices(vertices);
            barrierMesh.SetUVs(0, uvs);
            barrierMesh.SetTriangles(triangles, 0);
            barrierMesh.RecalculateNormals();
            barrierMesh.RecalculateBounds();

            GameObject barrierObj = new GameObject("SafetyBarriers");
            barrierObj.transform.SetParent(transform, false);

            MeshFilter mf = barrierObj.AddComponent<MeshFilter>();
            MeshRenderer mr = barrierObj.AddComponent<MeshRenderer>();
            MeshCollider mc = barrierObj.AddComponent<MeshCollider>();

            mf.sharedMesh = barrierMesh;
            mc.sharedMesh = barrierMesh;
            if (barrierMaterial != null) mr.sharedMaterial = barrierMaterial;
        }

        private void SpawnYemeniWatchtowers(List<Vector3> path)
        {
            if (yemeniTowerPrefab == null || path == null) return;

            int step = path.Count / (numberOfTowers + 1);
            for (int i = 1; i <= numberOfTowers; i++)
            {
                int idx = i * step;
                if (idx >= path.Count) break;

                Vector3 roadPoint = path[idx];
                Vector3 forward = (idx < path.Count - 1) ? (path[idx + 1] - roadPoint).normalized : Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

                // Place on mountain peaks overlooking the pass
                Vector3 towerPos = roadPoint + right * Random.Range(24f, 45f) + Vector3.up * Random.Range(10f, 28f);
                Quaternion rot = Quaternion.LookRotation(-right, Vector3.up);

                GameObject tower = Instantiate(yemeniTowerPrefab, towerPos, rot, transform);
                tower.name = $"Yemeni_Stone_Tower_{i}";
            }
        }
    }
}
