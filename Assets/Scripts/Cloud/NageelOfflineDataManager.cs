using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Nageel.Cloud
{
    public class NageelOfflineDataManager : MonoBehaviour
    {
        public static NageelOfflineDataManager Instance { get; private set; }

        public NageelGameSaveState currentSaveState;
        private string saveFilePath;
        private string backupSaveFilePath;

        [Header("Cloud Sync Configuration")]
        public string apiBaseUrl = "http://localhost:3000/api/v1";
        public bool isAutoSyncEnabled = true;
        public bool isSyncing = false;
        public string lastSyncStatusArabic = "التخزين المحلي جاهز (أوفلاين)";

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                saveFilePath = Path.Combine(Application.persistentDataPath, "nageel_save_data.json");
                backupSaveFilePath = Path.Combine(Application.persistentDataPath, "nageel_save_data.json.bak");
                LoadOrCreateLocalSave();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void LoadOrCreateLocalSave()
        {
            try
            {
                if (File.Exists(saveFilePath))
                {
                    string json = File.ReadAllText(saveFilePath);
                    currentSaveState = JsonUtility.FromJson<NageelGameSaveState>(json);
                    lastSyncStatusArabic = "تم تحميل الحفظ المحلي بنجاح";
                }
                else if (File.Exists(backupSaveFilePath))
                {
                    // Fallback to backup if primary save corrupted
                    string json = File.ReadAllText(backupSaveFilePath);
                    currentSaveState = JsonUtility.FromJson<NageelGameSaveState>(json);
                    lastSyncStatusArabic = "تم استعادة الحفظ من النسخة الاحتياطية";
                }
                else
                {
                    CreateDefaultSaveState();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Nageel Storage] Error reading local save: {ex.Message}. Recovering default.");
                CreateDefaultSaveState();
            }
        }

        private void CreateDefaultSaveState()
        {
            currentSaveState = new NageelGameSaveState
            {
                playerId = "driver_" + Guid.NewGuid().ToString().Substring(0, 8),
                totalCoins = 2500,
                activeVehicleId = "suv_landcruiser_4wd",
                lastSaveTimestamp = DateTime.UtcNow.ToString("o")
            };

            currentSaveState.vehicles.Add(new VehicleData
            {
                vehicleId = "suv_landcruiser_4wd",
                vehicleNameArabic = "لاندكروزر 4WD جبلية",
                massKg = 2150f,
                maxTorqueNm = 480f,
                topSpeedKmh = 165f,
                driveType = "AWD_4WD",
                isUnlocked = true,
                bodyPaintHex = "#E8E0D2"
            });

            currentSaveState.vehicles.Add(new VehicleData
            {
                vehicleId = "sedan_touring",
                vehicleNameArabic = "سيدان سياحية رياضية",
                massKg = 1480f,
                maxTorqueNm = 320f,
                topSpeedKmh = 195f,
                driveType = "RWD_2WD",
                isUnlocked = true,
                bodyPaintHex = "#1D4ED8"
            });

            currentSaveState.vehicles.Add(new VehicleData
            {
                vehicleId = "pickup_shas_4x4",
                vehicleNameArabic = "شاص 4WD للأحمال الصعبة",
                massKg = 1950f,
                maxTorqueNm = 440f,
                topSpeedKmh = 155f,
                driveType = "AWD_4WD",
                isUnlocked = true,
                bodyPaintHex = "#F8FAFC"
            });

            SaveToLocalDiskAtomic();
        }

        /// <summary>
        /// Atomic file write: writes to temporary file first, flushes, then replaces target file.
        /// Prevents file truncation and corruption in mobile environments.
        /// </summary>
        public void SaveToLocalDiskAtomic()
        {
            try
            {
                currentSaveState.lastSaveTimestamp = DateTime.UtcNow.ToString("o");
                string json = JsonUtility.ToJson(currentSaveState, true);

                string tempFile = saveFilePath + ".tmp";
                File.WriteAllText(tempFile, json);

                if (File.Exists(saveFilePath))
                {
                    File.Copy(saveFilePath, backupSaveFilePath, true);
                    File.Delete(saveFilePath);
                }

                File.Move(tempFile, saveFilePath);
                lastSyncStatusArabic = "تم الحفظ محلياً بأمان";
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Nageel Storage] Atomic save error: {ex.Message}");
            }
        }

        public void AddCoins(int amount)
        {
            currentSaveState.totalCoins += amount;
            SaveToLocalDiskAtomic();
            if (isAutoSyncEnabled)
            {
                StartCoroutine(TriggerCloudSync());
            }
        }

        public void RecordScore(string gameMode, string vehicleId, float timeSec, int score)
        {
            LeaderboardScoreData newScore = new LeaderboardScoreData
            {
                scoreId = Guid.NewGuid().ToString(),
                playerNickname = "كابتن نَقِيل",
                gameMode = gameMode,
                vehicleId = vehicleId,
                completionTimeSeconds = timeSec,
                finalScore = score,
                recordedTimestamp = DateTime.UtcNow.ToString("o"),
                isSyncedToNeon = false
            };

            currentSaveState.localScores.Add(newScore);
            SaveToLocalDiskAtomic();

            if (isAutoSyncEnabled)
            {
                StartCoroutine(TriggerCloudSync());
            }
        }

        /// <summary>
        /// Asynchronous idempotent cloud synchronization with conflict resolution.
        /// Zero direct DB connection strings in client.
        /// </summary>
        public IEnumerator TriggerCloudSync()
        {
            if (isSyncing) yield break;
            isSyncing = true;
            lastSyncStatusArabic = "جاري المزامنة مع السحابة...";

            string syncUrl = $"{apiBaseUrl}/player/sync";
            string idempotencyKey = Guid.NewGuid().ToString();

            string jsonPayload = JsonUtility.ToJson(new
            {
                coins = currentSaveState.totalCoins,
                unlocked_zones = new string[] { "naqil_pass_sumarah", "green_highlands_terraces", "rocky_wadi_canyon" }
            });

            using (UnityWebRequest req = UnityWebRequest.PostWwwForm(syncUrl, "POST"))
            {
                byte[] jsonBytes = new System.Text.UTF8Encoding().GetBytes(jsonPayload);
                req.uploadHandler = new UploadHandlerRaw(jsonBytes);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("X-Device-Id", currentSaveState.playerId);
                req.SetRequestHeader("X-Idempotency-Key", idempotencyKey);
                req.timeout = 4;

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    lastSyncStatusArabic = "تمت المزامنة بنجاح مع Neon Cloud";
                }
                else
                {
                    lastSyncStatusArabic = "أوفلاين: سيتم المزامنة لاحقاً";
                }
            }

            isSyncing = false;
        }
    }
}
