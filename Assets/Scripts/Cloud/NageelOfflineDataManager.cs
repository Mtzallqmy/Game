using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Nageel.Cloud
{
    [Serializable]
    internal sealed class ZoneSyncRequest
    {
        public string current_zone;
    }

    [Serializable]
    internal sealed class ZoneSyncResponse
    {
        public bool success;
        public bool synced;
        public string error;
    }

    public class NageelOfflineDataManager : MonoBehaviour
    {
        public static NageelOfflineDataManager Instance { get; private set; }
        public NageelGameSaveState currentSaveState;
        private string saveFilePath;
        private string backupSaveFilePath;

        [Header("GAME API base, including /api/v1; not Neon MCP")]
        public string apiBaseUrl = "";
        public bool isAutoSyncEnabled = true;
        public bool isSyncing = false;
        public string lastSyncStatusArabic = "التخزين المحلي جاهز (أوفلاين)";
        [NonSerialized] private string accessToken;

        public void SetAccessToken(string token) { accessToken = token ?? ""; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            saveFilePath = Path.Combine(Application.persistentDataPath,"nageel_save_data.json");
            backupSaveFilePath = saveFilePath + ".bak";
            LoadOrCreateLocalSave();
        }

        private NageelGameSaveState ReadSave(string path)
        {
            if (!File.Exists(path)) return null;
            var result = JsonUtility.FromJson<NageelGameSaveState>(File.ReadAllText(path));
            if (result == null || String.IsNullOrEmpty(result.playerId))
                throw new InvalidDataException("Nageel save is missing player identity");
            if (result.vehicles == null) result.vehicles = new List<VehicleData>();
            if (result.localScores == null) result.localScores = new List<LeaderboardScoreData>();
            if (result.dlcPackages == null) result.dlcPackages = new List<DLCMapPackageData>();
            if (String.IsNullOrEmpty(result.currentZone)) result.currentZone = "naqil_pass_sumarah";
            return result;
        }

        public void LoadOrCreateLocalSave()
        {
            try { currentSaveState = ReadSave(saveFilePath); }
            catch (Exception e) { Debug.LogWarning("[Nageel Save] Primary invalid: " + e.Message); }
            if (currentSaveState == null)
            {
                try
                {
                    currentSaveState = ReadSave(backupSaveFilePath);
                    if (currentSaveState != null)
                        lastSyncStatusArabic = "تمت استعادة الحفظ المحلي الاحتياطي";
                }
                catch (Exception e) { Debug.LogWarning("[Nageel Save] Backup invalid: " + e.Message); }
            }
            if (currentSaveState == null) CreateDefaultSaveState();
        }

        private void CreateDefaultSaveState()
        {
            currentSaveState = new NageelGameSaveState {
                playerId = "offline_" + Guid.NewGuid().ToString("N"),
                totalCoins = 2500, // Offline display-only balance; NOT trusted by the game API.
                currentZone = "naqil_pass_sumarah",
                activeVehicleId = "suv_landcruiser_4wd",
                lastSaveTimestamp = DateTime.UtcNow.ToString("o")
            };
            currentSaveState.vehicles.Add(new VehicleData {
                vehicleId="suv_landcruiser_4wd",vehicleNameArabic="مركبة جبلية",
                massKg=2150f,maxTorqueNm=480f,topSpeedKmh=165f,
                driveType="AWD_4WD",isUnlocked=true,bodyPaintHex="#E8E0D2"
            });
            SaveToLocalDiskAtomic();
        }

        public void SaveToLocalDiskAtomic()
        {
            try
            {
                currentSaveState.lastSaveTimestamp = DateTime.UtcNow.ToString("o");
                string json = JsonUtility.ToJson(currentSaveState,true);
                string temp = saveFilePath + ".tmp";
                using (var stream = new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(true);
                }
                if (File.Exists(saveFilePath))
                {
                    try { File.Replace(temp,saveFilePath,backupSaveFilePath); }
                    catch (PlatformNotSupportedException)
                    {
                        File.Copy(saveFilePath,backupSaveFilePath,true);
                        File.Delete(saveFilePath);
                        File.Move(temp,saveFilePath);
                    }
                }
                else File.Move(temp,saveFilePath);
                lastSyncStatusArabic = "تم الحفظ محليًا";
            }
            catch (Exception e)
            {
                lastSyncStatusArabic = "تعذر حفظ البيانات المحلية";
                Debug.LogError("[Nageel Save] " + e.Message);
            }
        }

        public void AddCoins(int amount)
        {
            // Local/offline simulation display value. Never send this to the remote server.
            currentSaveState.totalCoins = Mathf.Max(0,currentSaveState.totalCoins+amount);
            SaveToLocalDiskAtomic();
        }

        public void RecordScore(string mode,string vehicleId,float timeSec,int score)
        {
            currentSaveState.localScores.Add(new LeaderboardScoreData {
                scoreId=Guid.NewGuid().ToString(),
                playerNickname="سائق نَقِيل",
                gameMode=mode,
                vehicleId=vehicleId,
                completionTimeSeconds=timeSec,
                finalScore=score,
                recordedTimestamp=DateTime.UtcNow.ToString("o"),
                isSyncedToNeon=false // No verified leaderboard submission yet.
            });
            SaveToLocalDiskAtomic();
        }

        private bool SafeApiUrl()
        {
            Uri url;
            if (!Uri.TryCreate(apiBaseUrl.TrimEnd('/'),UriKind.Absolute,out url)) return false;
            return url.Scheme == Uri.UriSchemeHttps ||
                (Application.isEditor && url.Scheme == Uri.UriSchemeHttp && url.Host == "localhost");
        }

        /// <summary>
        /// Sync only the current unlocked zone using a user JWT. Currency/XP are server-owned.
        /// The save remains available offline when login or the API is unavailable.
        /// </summary>
        public IEnumerator TriggerCloudSync()
        {
            if (isSyncing) yield break;
            if (!isAutoSyncEnabled || !SafeApiUrl() || String.IsNullOrEmpty(accessToken))
            {
                lastSyncStatusArabic = "الحفظ محلي؛ المزامنة تحتاج تسجيل دخول وخادم API";
                yield break;
            }
            isSyncing = true;
            lastSyncStatusArabic = "تجري مزامنة المنطقة الحالية...";
            var payload = new ZoneSyncRequest {
                current_zone = String.IsNullOrEmpty(currentSaveState.currentZone) ?
                    "naqil_pass_sumarah" : currentSaveState.currentZone
            };
            string data = JsonUtility.ToJson(payload);
            using (var req = new UnityWebRequest(apiBaseUrl.TrimEnd('/')+"/player/sync","POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(data));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type","application/json");
                req.SetRequestHeader("Authorization","Bearer "+accessToken);
                req.timeout = 15;
                yield return req.SendWebRequest();
                ZoneSyncResponse answer = null;
                if (req.result == UnityWebRequest.Result.Success && req.responseCode == 200)
                {
                    try { answer = JsonUtility.FromJson<ZoneSyncResponse>(req.downloadHandler.text); }
                    catch (Exception e) { Debug.LogWarning(e.Message); }
                }
                lastSyncStatusArabic = answer != null && answer.success && answer.synced ?
                    "تم حفظ المنطقة عبر الخادم الموثق" :
                    "المزامنة لم تنجح؛ ستبقى البيانات المحلية محفوظة";
            }
            isSyncing = false;
        }
    }
}
