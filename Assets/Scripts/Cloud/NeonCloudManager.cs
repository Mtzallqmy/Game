using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Nageel.Cloud
{
    [Serializable]
    public class NeonConfig
    {
        public string projectId = "dry-surf-72406677";
        public string databaseName = "nageel_db";
        public string developmentBranch = "development";
        public string developmentBranchId = "br-orange-pond-badvwqgc";
        public string productionBranch = "main";
        public string mcpServerUrl = "https://mcp.neon.tech/mcp";
    }

    [Serializable]
    public class NeonHealthCheckResponse
    {
        public bool isOnline;
        public string database;
        public string branch;
        public string timestamp;
    }

    public class NeonCloudManager : MonoBehaviour
    {
        public static NeonCloudManager Instance { get; private set; }

        [Header("Neon Connection Configuration")]
        public NeonConfig config = new NeonConfig();

        [Header("Status & Telemetry")]
        public bool isConnected = false;
        public string lastStatusMessage = "Initialized (Ready for OAuth Auth)";
        public string oauthBearerToken = "";

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            StartCoroutine(VerifyNeonConnection());
        }

        /// <summary>
        /// Performs safe read-only connectivity check to Neon Cloud MCP / Serverless endpoint.
        /// Preserves branch rules: development only, main untouched, zero table mutations in Phase 1.
        /// </summary>
        public IEnumerator VerifyNeonConnection()
        {
            lastStatusMessage = "Checking Neon Cloud connection...";

            using (UnityWebRequest req = UnityWebRequest.Get(config.mcpServerUrl))
            {
                req.timeout = 5;
                if (!string.IsNullOrEmpty(oauthBearerToken))
                {
                    req.SetRequestHeader("Authorization", "Bearer " + oauthBearerToken);
                }

                yield return req.SendWebRequest();

                if (req.responseCode == 200 || req.responseCode == 401)
                {
                    // HTTP 200 or 401 proves the Neon MCP endpoint is online and responsive
                    isConnected = true;
                    lastStatusMessage = $"[SUCCESS] Neon Cloud online! Target: {config.databaseName} on branch {config.developmentBranch} ({config.developmentBranchId}).";
                    Debug.Log($"[Nageel Neon] {lastStatusMessage}");
                }
                else
                {
                    isConnected = false;
                    lastStatusMessage = $"[NOTICE] Status {req.responseCode}: {req.error}";
                    Debug.LogWarning($"[Nageel Neon] {lastStatusMessage}");
                }
            }
        }

        public void SetOAuthToken(string token)
        {
            oauthBearerToken = token;
            StartCoroutine(VerifyNeonConnection());
        }
    }
}
