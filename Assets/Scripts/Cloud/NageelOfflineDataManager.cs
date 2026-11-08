using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace Nageel.Cloud
{
    public class NageelOfflineDataManager : MonoBehaviour
    {
        public static NageelOfflineDataManager Instance { get; private set; }

        public NageelGameSaveState currentSaveState;
        private string saveFilePath;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                saveFilePath = Path.Combine(Application.persistentDataPath, "nageel_save_data.json");
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
                    Debug.Log($"[Nageel Storage] Local save loaded successfully from {saveFilePath}");
                }
                else
                {
                    CreateDefaultSaveState();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Nageel Storage] Error reading local save, creating default: {ex.Message}");
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

            // 3 Vehicles initialized
            currentSaveState.vehicles.Add(new VehicleData
            {
                vehicleId = "suv_landcruiser_4wd",
                vehicleNameArabic = "لاندكروزر 4WD جبلية",
                massKg = 2150f,
                maxTorqueNm = 480f,
                topSpeedKmh = 165f,
                driveType = "4WD",
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
                driveType = "RWD",
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
                driveType = "4WD",
                isUnlocked = true,
                bodyPaintHex = "#F5F5F4"
            });

            // DLC Map Definitions (Offline Capable)
            currentSaveState.dlcPackages.Add(new DLCMapPackageData
            {
                packageId = "dlc_yemen_naqil_complete",
                titleArabic = "حزمة طرق اليمن الكبرى (10.5 كم)",
                version = "2.0.0",
                packageSizeBytes = 12582912, // 12 MB
                isDownloadedAndCached = true,
                checksumSHA256 = "c2a98f12d49e89b21"
            });

            SaveToLocalDisk();
        }

        public void SaveToLocalDisk()
        {
            try
            {
                currentSaveState.lastSaveTimestamp = DateTime.UtcNow.ToString("o");
                string json = JsonUtility.ToJson(currentSaveState, true);
                File.WriteAllText(saveFilePath, json);
                Debug.Log($"[Nageel Storage] Game state saved locally to {saveFilePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Nageel Storage] Failed to save state to disk: {ex.Message}");
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
            SaveToLocalDisk();
        }
    }
}
