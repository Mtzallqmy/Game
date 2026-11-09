using System;
using System.Collections.Generic;

namespace Nageel.Cloud
{
    [Serializable]
    public class MapZoneData
    {
        public string zoneId;
        public string zoneNameArabic;
        public float startMeters;
        public float endMeters;
        public float elevationMinMeters;
        public float elevationMaxMeters;
        public float surfaceFriction;
        public string roadType;
        public string defaultWeather;
    }

    [Serializable]
    public class VehicleData
    {
        public string vehicleId;
        public string vehicleNameArabic;
        public float massKg;
        public float maxTorqueNm;
        public float topSpeedKmh;
        public string driveType;
        public bool isUnlocked;
        public string bodyPaintHex;
        public int upgradeStageEngine;
        public int upgradeStageSuspension;
    }

    [Serializable]
    public class LeaderboardScoreData
    {
        public string scoreId;
        public string playerNickname;
        public string gameMode;
        public string vehicleId;
        public float completionTimeSeconds;
        public int finalScore;
        public string recordedTimestamp;
        public bool isSyncedToNeon;
    }

    [Serializable]
    public class DLCMapPackageData
    {
        public string packageId;
        public string titleArabic;
        public string version;
        public float packageSizeBytes;
        public bool isDownloadedAndCached;
        public string checksumSHA256;
    }

    [Serializable]
    public class NageelGameSaveState
    {
        public string playerId;
        public int totalCoins;
        public string currentZone = "naqil_pass_sumarah";
        public string activeVehicleId;
        public List<VehicleData> vehicles = new List<VehicleData>();
        public List<LeaderboardScoreData> localScores = new List<LeaderboardScoreData>();
        public List<DLCMapPackageData> dlcPackages = new List<DLCMapPackageData>();
        public string lastSaveTimestamp;
    }
}
