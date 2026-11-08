using System.Collections.Generic;
using UnityEngine;
using Nageel.Environment;

namespace Nageel.Traffic
{
    public class AITrafficManager : MonoBehaviour
    {
        [Header("Traffic Population")]
        public GameObject trafficVehiclePrefab;
        public int maxSimultaneousVehicles = 8;
        public float spawnRadius = 350f;
        public float despawnRadius = 600f;

        [Header("References")]
        public MountainRoadGenerator roadGenerator;
        public Transform playerTransform;

        private List<AITrafficVehicle> activeVehicles = new List<AITrafficVehicle>();
        private List<Vector3> roadSpline;

        private void Start()
        {
            if (roadGenerator == null) roadGenerator = FindObjectOfType<MountainRoadGenerator>();
            if (roadGenerator != null)
            {
                roadSpline = roadGenerator.GetSplinePoints();
            }

            SpawnInitialTraffic();
        }

        private void SpawnInitialTraffic()
        {
            if (trafficVehiclePrefab == null || roadSpline == null || roadSpline.Count < 10) return;

            float playerDist = playerTransform != null ? playerTransform.position.z : 50f;

            for (int i = 0; i < maxSimultaneousVehicles; i++)
            {
                // Alternate between ahead, behind, and oncoming
                bool oncoming = (i % 2 == 1);
                float offset = (i + 1) * 75f * (oncoming ? 1f : -0.7f);
                float spawnDist = Mathf.Clamp(playerDist + offset, 50f, 10000f);

                SpawnTrafficCar(spawnDist, oncoming);
            }
        }

        private void SpawnTrafficCar(float distanceAlongRoad, bool oncoming)
        {
            GameObject carObj = Instantiate(trafficVehiclePrefab, transform);
            carObj.name = $"AITraffic_{activeVehicles.Count + 1}";

            AITrafficVehicle ai = carObj.GetComponent<AITrafficVehicle>() ?? carObj.AddComponent<AITrafficVehicle>();
            ai.Initialize(roadSpline, distanceAlongRoad, oncoming);
            activeVehicles.Add(ai);
        }

        private void Update()
        {
            if (playerTransform == null)
            {
                Nageel.Vehicle.NageelCarController car = FindObjectOfType<Nageel.Vehicle.NageelCarController>();
                if (car != null) playerTransform = car.transform;
                if (playerTransform == null) return;
            }

            ManageTrafficRecycling();
        }

        private void ManageTrafficRecycling()
        {
            float playerZ = playerTransform.position.z;

            for (int i = 0; i < activeVehicles.Count; i++)
            {
                AITrafficVehicle v = activeVehicles[i];
                float dist = Mathf.Abs(v.transform.position.z - playerZ);

                if (dist > despawnRadius)
                {
                    // Recycle vehicle ahead of player
                    bool oncoming = (Random.value > 0.5f);
                    float newDist = playerZ + (oncoming ? Random.Range(200f, 350f) : Random.Range(150f, 300f));
                    newDist = Mathf.Clamp(newDist, 20f, 10200f);
                    v.Initialize(roadSpline, newDist, oncoming);
                }
            }
        }

        public int GetActiveVehicleCount()
        {
            return activeVehicles.Count;
        }
    }
}
