using System.Collections.Generic;
using UnityEngine;

namespace Nageel.Environment
{
    public class WorldChunkManager : MonoBehaviour
    {
        [Header("Target Tracking")]
        public Transform playerVehicleTransform;

        [Header("Streaming & LOD Distances")]
        public float activeChunkDistance = 1400f; // High detail: colliders & full props
        public float lodProxyDistance = 2800f;    // Low detail: simplified mesh
        public float cullDistance = 4200f;        // Occluded / Culled completely

        [Header("Chunk Configuration")]
        public int totalChunks = 10; // 10 chunks x ~1,050m = 10.5 km total world
        public float chunkLength = 1050f;

        [System.Serializable]
        public class ChunkData
        {
            public int chunkIndex;
            public float startDistance;
            public float endDistance;
            public GameObject rootObject;
            public bool isHighDetailActive;
            public bool isVisible;
        }

        private List<ChunkData> chunks = new List<ChunkData>();
        private float lastUpdateTime = 0f;
        private const float UPDATE_INTERVAL = 0.5f; // Check chunk distances twice per second

        private void Start()
        {
            InitializeChunks();
        }

        private void InitializeChunks()
        {
            chunks.Clear();
            for (int i = 0; i < totalChunks; i++)
            {
                ChunkData cd = new ChunkData
                {
                    chunkIndex = i,
                    startDistance = i * chunkLength,
                    endDistance = (i + 1) * chunkLength,
                    isHighDetailActive = true,
                    isVisible = true
                };

                // Create container gameobject if not already present
                string chunkName = $"WorldChunk_{i + 1}_{cd.startDistance:F0}m_to_{cd.endDistance:F0}m";
                Transform existing = transform.Find(chunkName);
                if (existing != null)
                {
                    cd.rootObject = existing.gameObject;
                }
                else
                {
                    GameObject go = new GameObject(chunkName);
                    go.transform.SetParent(transform, false);
                    cd.rootObject = go;
                }

                chunks.Add(cd);
            }
        }

        private void Update()
        {
            if (Time.time - lastUpdateTime < UPDATE_INTERVAL) return;
            lastUpdateTime = Time.time;

            if (playerVehicleTransform == null)
            {
                Nageel.Vehicle.NageelCarController car = FindObjectOfType<Nageel.Vehicle.NageelCarController>();
                if (car != null) playerVehicleTransform = car.transform;
                if (playerVehicleTransform == null) return;
            }

            UpdateChunkVisibility(playerVehicleTransform.position.z);
        }

        public void UpdateChunkVisibility(float playerZDistance)
        {
            for (int i = 0; i < chunks.Count; i++)
            {
                ChunkData chunk = chunks[i];
                float chunkMidZ = (chunk.startDistance + chunk.endDistance) * 0.5f;
                float deltaDist = Mathf.Abs(playerZDistance - chunkMidZ);

                if (deltaDist <= activeChunkDistance)
                {
                    // Active High Detail
                    chunk.isVisible = true;
                    chunk.isHighDetailActive = true;
                    if (chunk.rootObject != null && !chunk.rootObject.activeSelf)
                    {
                        chunk.rootObject.SetActive(true);
                    }
                }
                else if (deltaDist <= lodProxyDistance)
                {
                    // Medium LOD Proxy
                    chunk.isVisible = true;
                    chunk.isHighDetailActive = false;
                    if (chunk.rootObject != null && !chunk.rootObject.activeSelf)
                    {
                        chunk.rootObject.SetActive(true);
                    }
                }
                else
                {
                    // Beyond Cull Distance: Occlude completely for high mobile FPS
                    chunk.isVisible = false;
                    chunk.isHighDetailActive = false;
                    if (chunk.rootObject != null && chunk.rootObject.activeSelf)
                    {
                        chunk.rootObject.SetActive(false);
                    }
                }
            }
        }

        public int GetActiveChunkCount()
        {
            int count = 0;
            foreach (var c in chunks)
            {
                if (c.isVisible) count++;
            }
            return count;
        }
    }
}
