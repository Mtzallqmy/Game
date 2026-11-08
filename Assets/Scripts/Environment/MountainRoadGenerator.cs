using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nageel.Environment
{
    public enum WorldZone
    {
        NaqilMountainPass,      // Zone 1: 0 - 3,500m (Steep asphalt pass, hairpins, cliff drop-offs)
        GreenHighlandTerraces,  // Zone 2: 3,500 - 7,000m (Agricultural terraces, stone villages, mist)
        RockyWadiValley         // Zone 3: 7,000 - 10,500m (Dirt/gravel tracks, canyon walls, boulders)
    }

    [System.Serializable]
    public struct ZoneInfo
    {
        public WorldZone zone;
        public string arabicName;
        public float startDistance;
        public float endDistance;
        public float surfaceFriction;
        public string roadType;
    }

    [ExecuteInEditMode]
    public class MountainRoadGenerator : MonoBehaviour
    {
        [Header("Road Network Parameters (8 - 12 km Target)")]
        public float totalRoadLength = 10500f; // 10.5 Kilometers total continuous network
        public float roadWidthAsphalt = 9.0f;  // Zone 1 & 2 standard two-lane highway
        public float roadWidthWadi = 7.5f;     // Zone 3 unpaved canyon track
        public float segmentLength = 10.0f;
        public float barrierHeight = 0.95f;

        [Header("Zone Materials (PBR)")]
        public Material asphaltRoadMaterial;
        public Material wadiDirtRoadMaterial;
        public Material barrierMaterial;
        public Material mountainRockMaterial;
        public Material terraceGreenMaterial;

        [Header("Environment Prefabs & Props")]
        public GameObject yemeniTowerPrefab;
        public GameObject yemeniTerracePrefab;
        public GameObject wadiBoulderPrefab;

        [Header("Generated Meshes & Colliders")]
        public MeshFilter roadMeshFilter;
        public MeshCollider roadMeshCollider;
        public Transform chunksContainer;

        // Spline Data Cache for Navigation & Traffic
        private List<Vector3> splinePoints = new List<Vector3>();

        public static readonly ZoneInfo[] ZONES = new ZoneInfo[]
        {
            new ZoneInfo {
                zone = WorldZone.NaqilMountainPass,
                arabicName = "نَقِيل سَمَارَة الجبلي",
                startDistance = 0f,
                endDistance = 3500f,
                surfaceFriction = 1.0f,
                roadType = "أسفلت جبلي وعر"
            },
            new ZoneInfo {
                zone = WorldZone.GreenHighlandTerraces,
                arabicName = "المرتفعات الخضراء والمدرجات الزراعية",
                startDistance = 3500f,
                endDistance = 7000f,
                surfaceFriction = 0.92f,
                roadType = "طريق قروي معبد وضبابي"
            },
            new ZoneInfo {
                zone = WorldZone.RockyWadiValley,
                arabicName = "وادي الصخور والممرات الضيقة",
                startDistance = 7000f,
                endDistance = 10500f,
                surfaceFriction = 0.72f,
                roadType = "مسار ترابي وصخري غير معبد"
            }
        };

        private void Start()
        {
            GenerateCompleteWorldNetwork();
        }

        [ContextMenu("Regenerate 10.5km World")]
        public void GenerateCompleteWorldNetwork()
        {
            int numPoints = Mathf.CeilToInt(totalRoadLength / segmentLength);
            splinePoints = GenerateSplinePoints(numPoints);

            BuildRoadMesh(splinePoints);
            BuildSafetyBarriers(splinePoints);
            SpawnZoneScenery(splinePoints);
        }

        public List<Vector3> GenerateSplinePoints(int numPoints)
        {
            List<Vector3> points = new List<Vector3>(numPoints);

            for (int i = 0; i < numPoints; i++)
            {
                float t = (float)i / numPoints;
                float progressDistance = t * totalRoadLength;

                float x = 0f;
                float y = 0f;
                float z = progressDistance;

                if (progressDistance < 3500f)
                {
                    // Zone 1: Steep Naqil Mountain Pass (0 to 3,500m)
                    // High amplitude hairpin switchbacks, climbing from 120m to 780m elevation
                    float zoneT = progressDistance / 3500f;
                    x = Mathf.Sin(zoneT * Mathf.PI * 6.5f) * 140f + Mathf.Sin(zoneT * Mathf.PI * 13f) * 55f;
                    y = 120f + Mathf.Pow(zoneT, 0.88f) * 660f + Mathf.Sin(zoneT * Mathf.PI * 5f) * 25f;

                    // Severe hairpin bends at 1,200m and 2,600m
                    if (zoneT > 0.30f && zoneT < 0.45f)
                    {
                        x += Mathf.Sin((zoneT - 0.30f) / 0.15f * Mathf.PI) * 110f;
                    }
                    else if (zoneT > 0.70f && zoneT < 0.85f)
                    {
                        x -= Mathf.Sin((zoneT - 0.70f) / 0.15f * Mathf.PI) * 120f;
                    }
                }
                else if (progressDistance < 7000f)
                {
                    // Zone 2: Green Highlands & Terraces (3,500m to 7,000m)
                    // High altitude plateau (780m - 980m), gentle sweeping curves between village terraced hills
                    float zoneT = (progressDistance - 3500f) / 3500f;
                    float baseZoneX = Mathf.Sin(Mathf.PI * 6.5f) * 140f; // Continuous continuity from zone 1
                    x = baseZoneX + Mathf.Sin(zoneT * Mathf.PI * 4.0f) * 95f + Mathf.Cos(zoneT * Mathf.PI * 8.0f) * 40f;
                    y = 780f + Mathf.Sin(zoneT * Mathf.PI * 3f) * 80f + (zoneT * 120f);
                }
                else
                {
                    // Zone 3: Rocky Wadi Valley (7,000m to 10,500m)
                    // Descending into dry canyon gorge (980m down to 430m), winding through boulder fields
                    float zoneT = (progressDistance - 7000f) / 3500f;
                    float baseZoneX = points.Count > 0 ? points[points.Count - 1].x : 0f;
                    x = baseZoneX + Mathf.Sin(zoneT * Mathf.PI * 5.5f) * 85f + Mathf.Sin(zoneT * Mathf.PI * 11f) * 35f;
                    y = 980f - Mathf.Pow(zoneT, 0.90f) * 550f + Mathf.Sin(zoneT * Mathf.PI * 6f) * 18f;
                }

                points.Add(new Vector3(x, y, z));
            }

            return points;
        }

        private void BuildRoadMesh(List<Vector3> path)
        {
            if (path == null || path.Count < 2) return;

            Mesh roadMesh = new Mesh();
            roadMesh.name = "Nageel_Connected_10km_Road_Mesh";

            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            for (int i = 0; i < path.Count; i++)
            {
                Vector3 current = path[i];
                Vector3 forward = (i < path.Count - 1) ? (path[i + 1] - current).normalized : (current - path[i - 1]).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

                float dist = (float)i * segmentLength;
                float currentWidth = (dist < 7000f) ? roadWidthAsphalt : roadWidthWadi;
                float halfW = currentWidth * 0.5f;

                Vector3 leftPos = current - right * halfW;
                Vector3 rightPos = current + right * halfW;

                vertices.Add(leftPos);
                vertices.Add(rightPos);

                float uvY = (float)i * 0.20f;
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

            if (roadMeshFilter == null) roadMeshFilter = gameObject.GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
            if (roadMeshCollider == null) roadMeshCollider = gameObject.GetComponent<MeshCollider>() ?? gameObject.AddComponent<MeshCollider>();

            roadMeshFilter.sharedMesh = roadMesh;
            roadMeshCollider.sharedMesh = roadMesh;
        }

        private void BuildSafetyBarriers(List<Vector3> path)
        {
            if (path == null || path.Count < 2) return;

            Mesh barrierMesh = new Mesh();
            barrierMesh.name = "Nageel_Guardrail_Mesh";

            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            // Erect barriers primarily along Zone 1 (precipice cliff side) and dangerous turns in Zone 2
            for (int i = 0; i < path.Count; i++)
            {
                float dist = (float)i * segmentLength;
                if (dist > 7200f) continue; // Wadi canyon is open natural boulders rather than steel guardrails

                Vector3 current = path[i];
                Vector3 forward = (i < path.Count - 1) ? (path[i + 1] - current).normalized : (current - path[i - 1]).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

                float halfW = roadWidthAsphalt * 0.5f;
                Vector3 basePos = current - right * (halfW + 0.20f);
                Vector3 topPos = basePos + Vector3.up * barrierHeight;

                int vIdx = vertices.Count;
                vertices.Add(basePos);
                vertices.Add(topPos);

                float uvY = (float)i * 0.4f;
                uvs.Add(new Vector2(0f, uvY));
                uvs.Add(new Vector2(1f, uvY));

                if (i > 0 && dist <= 7200f)
                {
                    int prev = vIdx - 2;
                    triangles.Add(prev);
                    triangles.Add(prev + 1);
                    triangles.Add(vIdx);

                    triangles.Add(vIdx);
                    triangles.Add(prev + 1);
                    triangles.Add(vIdx + 1);
                }
            }

            barrierMesh.SetVertices(vertices);
            barrierMesh.SetUVs(0, uvs);
            barrierMesh.SetTriangles(triangles, 0);
            barrierMesh.RecalculateNormals();

            Transform existingBarriers = transform.Find("SafetyBarriers");
            GameObject barrierObj = existingBarriers != null ? existingBarriers.gameObject : new GameObject("SafetyBarriers");
            barrierObj.transform.SetParent(transform, false);

            MeshFilter mf = barrierObj.GetComponent<MeshFilter>() ?? barrierObj.AddComponent<MeshFilter>();
            MeshRenderer mr = barrierObj.GetComponent<MeshRenderer>() ?? barrierObj.AddComponent<MeshRenderer>();
            MeshCollider mc = barrierObj.GetComponent<MeshCollider>() ?? barrierObj.AddComponent<MeshCollider>();

            mf.sharedMesh = barrierMesh;
            mc.sharedMesh = barrierMesh;
            if (barrierMaterial != null) mr.sharedMaterial = barrierMaterial;
        }

        private void SpawnZoneScenery(List<Vector3> path)
        {
            if (path == null || path.Count < 10) return;

            Transform sceneryRoot = transform.Find("ZoneScenery");
            if (sceneryRoot == null)
            {
                GameObject root = new GameObject("ZoneScenery");
                root.transform.SetParent(transform, false);
                sceneryRoot = root.transform;
            }

            // Clean previous children
            while (sceneryRoot.childCount > 0)
            {
                DestroyImmediate(sceneryRoot.GetChild(0).gameObject);
            }

            // Spawn props along the 3 connected zones
            for (int i = 10; i < path.Count - 10; i += 18)
            {
                float dist = (float)i * segmentLength;
                Vector3 pos = path[i];
                Vector3 forward = (path[i + 1] - pos).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

                if (dist < 3500f)
                {
                    // Zone 1: Watchtowers on high rocky ridges
                    if (yemeniTowerPrefab != null && i % 36 == 0)
                    {
                        Vector3 towerPos = pos + right * UnityEngine.Random.Range(28f, 55f) + Vector3.up * UnityEngine.Random.Range(12f, 35f);
                        Instantiate(yemeniTowerPrefab, towerPos, Quaternion.LookRotation(-right, Vector3.up), sceneryRoot);
                    }
                }
                else if (dist < 7000f)
                {
                    // Zone 2: Green Agricultural Terraces & Highland Stone Villages
                    if (yemeniTerracePrefab != null && i % 24 == 0)
                    {
                        Vector3 terracePos = pos + right * UnityEngine.Random.Range(22f, 40f) - Vector3.up * UnityEngine.Random.Range(4f, 12f);
                        Instantiate(yemeniTerracePrefab, terracePos, Quaternion.LookRotation(forward, Vector3.up), sceneryRoot);
                    }
                    if (yemeniTowerPrefab != null && i % 42 == 0)
                    {
                        Vector3 villageHousePos = pos - right * UnityEngine.Random.Range(25f, 45f) + Vector3.up * UnityEngine.Random.Range(5f, 15f);
                        Instantiate(yemeniTowerPrefab, villageHousePos, Quaternion.identity, sceneryRoot);
                    }
                }
                else
                {
                    // Zone 3: Rocky Wadi Valley (Canyon Boulders & Natural Slabs)
                    if (wadiBoulderPrefab != null && i % 20 == 0)
                    {
                        Vector3 boulderPos = pos + right * UnityEngine.Random.Range(10f, 24f) + Vector3.up * 0.5f;
                        Instantiate(wadiBoulderPrefab, boulderPos, UnityEngine.Random.rotation, sceneryRoot);

                        Vector3 oppositePos = pos - right * UnityEngine.Random.Range(10f, 22f) + Vector3.up * 0.5f;
                        Instantiate(wadiBoulderPrefab, oppositePos, UnityEngine.Random.rotation, sceneryRoot);
                    }
                }
            }
        }

        public static ZoneInfo GetZoneAtDistance(float distanceMeters)
        {
            if (distanceMeters < 3500f) return ZONES[0];
            if (distanceMeters < 7000f) return ZONES[1];
            return ZONES[2];
        }

        public List<Vector3> GetSplinePoints()
        {
            if (splinePoints == null || splinePoints.Count == 0)
            {
                int numPoints = Mathf.CeilToInt(totalRoadLength / segmentLength);
                splinePoints = GenerateSplinePoints(numPoints);
            }
            return splinePoints;
        }
    }
}
