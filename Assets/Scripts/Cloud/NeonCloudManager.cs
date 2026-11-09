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
        public string developmentBranchId = "br-orange-pond-badvwqgc";
        // Configure the deployed GAME API URL, never the administrative Neon MCP server.
        public string apiBaseUrl = "";
    }

    [Serializable]
    internal class GameApiHealth
    {
        public bool online;
        public bool database;
        public string database_name;
    }

    public class NeonCloudManager : MonoBehaviour
    {
        public static NeonCloudManager Instance { get; private set; }
        public NeonConfig config = new NeonConfig();
        public bool isConnected = false;
        public string lastStatusMessage = "Game API not configured";
        [NonSerialized] private string accessToken;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start() { StartCoroutine(VerifyNeonConnection()); }

        private bool SafeApiUrl()
        {
            Uri url;
            if (!Uri.TryCreate(config.apiBaseUrl.TrimEnd('/'), UriKind.Absolute, out url)) return false;
            return url.Scheme == Uri.UriSchemeHttps ||
                (Application.isEditor && url.Scheme == Uri.UriSchemeHttp && url.Host == "localhost");
        }

        public IEnumerator VerifyNeonConnection()
        {
            isConnected = false;
            if (!SafeApiUrl())
            {
                lastStatusMessage = "Configure an HTTPS game API endpoint before cloud sync";
                yield break;
            }
            lastStatusMessage = "Checking GAME API database health...";
            using (var req = UnityWebRequest.Get(config.apiBaseUrl.TrimEnd('/') + "/health"))
            {
                req.timeout = 8;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success || req.responseCode != 200)
                {
                    lastStatusMessage = "Game API health unavailable: HTTP " + req.responseCode;
                    yield break;
                }
                GameApiHealth data = null;
                try { data = JsonUtility.FromJson<GameApiHealth>(req.downloadHandler.text); }
                catch (Exception e) { Debug.LogWarning(e.Message); }
                isConnected = data != null && data.online && data.database &&
                    data.database_name == config.databaseName;
                lastStatusMessage = isConnected ?
                    "Verified live game API + Postgres health" :
                    "API answered, but Postgres health is not verified";
            }
        }

        /// <summary>
        /// Accept a JWT from a future real sign-in flow. No administrative MCP tokens.
        /// Tokens are never serialized into the Unity scene/inspector.
        /// </summary>
        public void SetAccessToken(string token)
        {
            accessToken = token ?? "";
            if (NageelOfflineDataManager.Instance != null)
                NageelOfflineDataManager.Instance.SetAccessToken(accessToken);
        }
    }
}
