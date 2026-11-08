using System;
using System.Collections.Generic;
using UnityEngine;
using Nageel.Vehicle;
using Nageel.Environment;

namespace Nageel.Gameplay
{
    public enum GameMode
    {
        FreeRoam,               // القيادة الحرة
        DeliveryMission,        // رحلات الوصول وتوصيل البضائع
        HillClimbTimeAttack     // التحديات الجبلية وصعود القمم
    }

    [System.Serializable]
    public class CheckpointData
    {
        public string checkpointName;
        public float distanceAlongRoad;
        public bool isPassed;
        public float splitTimeSeconds;
    }

    public class GameModeManager : MonoBehaviour
    {
        public static GameModeManager Instance { get; private set; }

        [Header("Active Game Mode")]
        public GameMode activeMode = GameMode.FreeRoam;

        [Header("Timer & Scoring")]
        public float elapsedTimeSeconds = 0f;
        public float targetTimeSeconds = 300f; // 5 min delivery limit
        public int currentScore = 0;
        public bool isMissionActive = false;
        public bool isMissionCompleted = false;

        [Header("Checkpoints (Hill Climb & Delivery)")]
        public List<CheckpointData> checkpoints = new List<CheckpointData>();
        public int currentCheckpointIndex = 0;

        [Header("Vehicle Reference")]
        public NageelCarController carController;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            SetupMode(activeMode);
        }

        public void SetGameMode(GameMode mode)
        {
            activeMode = mode;
            SetupMode(mode);
        }

        public void CycleGameMode()
        {
            int next = ((int)activeMode + 1) % 3;
            SetGameMode((GameMode)next);
        }

        private void SetupMode(GameMode mode)
        {
            elapsedTimeSeconds = 0f;
            currentScore = 1000;
            currentCheckpointIndex = 0;
            isMissionCompleted = false;
            checkpoints.Clear();

            switch (mode)
            {
                case GameMode.FreeRoam:
                    isMissionActive = false;
                    break;

                case GameMode.DeliveryMission:
                    isMissionActive = true;
                    targetTimeSeconds = 360f; // 6 minutes to traverse to Highland Market
                    checkpoints.Add(new CheckpointData { checkpointName = "نقطة انطلاق أسفل النقيل", distanceAlongRoad = 300f });
                    checkpoints.Add(new CheckpointData { checkpointName = "منعطف سمارة الأوسط", distanceAlongRoad = 1800f });
                    checkpoints.Add(new CheckpointData { checkpointName = "بوابة المرتفعات الخضراء", distanceAlongRoad = 3600f });
                    checkpoints.Add(new CheckpointData { checkpointName = "سوق القرية الجبلية", distanceAlongRoad = 5500f });
                    break;

                case GameMode.HillClimbTimeAttack:
                    isMissionActive = true;
                    targetTimeSeconds = 240f; // 4 minutes sprint to summit
                    checkpoints.Add(new CheckpointData { checkpointName = "خط البداية - أسفل العقبة", distanceAlongRoad = 100f });
                    checkpoints.Add(new CheckpointData { checkpointName = "المنعطف الحاد الأول", distanceAlongRoad = 1100f });
                    checkpoints.Add(new CheckpointData { checkpointName = "منعطف قمة الحصن", distanceAlongRoad = 2400f });
                    checkpoints.Add(new CheckpointData { checkpointName = "قمة نقيل سمارة (2800م)", distanceAlongRoad = 3500f });
                    break;
            }
        }

        private void Update()
        {
            if (carController == null)
            {
                carController = FindObjectOfType<NageelCarController>();
                if (carController == null) return;
            }

            if (!isMissionActive || isMissionCompleted) return;

            elapsedTimeSeconds += Time.deltaTime;

            // Check distance progress
            float carZ = carController.transform.position.z;
            if (currentCheckpointIndex < checkpoints.Count)
            {
                CheckpointData targetCp = checkpoints[currentCheckpointIndex];
                if (carZ >= targetCp.distanceAlongRoad)
                {
                    targetCp.isPassed = true;
                    targetCp.splitTimeSeconds = elapsedTimeSeconds;
                    currentScore += 500;
                    currentCheckpointIndex++;

                    if (currentCheckpointIndex >= checkpoints.Count)
                    {
                        // Mission Complete!
                        isMissionCompleted = true;
                        float timeBonus = Mathf.Max(0f, targetTimeSeconds - elapsedTimeSeconds) * 10f;
                        currentScore += Mathf.RoundToInt(timeBonus);
                        Debug.Log($"[Nageel Mission] Mission Completed! Final Score: {currentScore}");
                    }
                }
            }

            // Time out condition for delivery
            if (activeMode == GameMode.DeliveryMission && elapsedTimeSeconds >= targetTimeSeconds)
            {
                isMissionActive = false;
                Debug.Log("[Nageel Mission] Delivery Time Expired!");
            }
        }

        public string GetArabicModeName()
        {
            switch (activeMode)
            {
                case GameMode.FreeRoam: return "القيادة الحرة (استكشاف 10.5 كم)";
                case GameMode.DeliveryMission: return "رحلات الوصول وتوصيل البضائع";
                case GameMode.HillClimbTimeAttack: return "التحدي الجبلي — صعود النقيل";
                default: return "نمط اللعب";
            }
        }

        public string GetMissionStatusText()
        {
            if (activeMode == GameMode.FreeRoam)
            {
                return "استكشف الطرق والجبال بحرية تامة";
            }

            if (isMissionCompleted)
            {
                return $"تم الإنجاز بنجاح! النقاط: {currentScore} — الوقت: {elapsedTimeSeconds:F1}ث";
            }

            string cpName = currentCheckpointIndex < checkpoints.Count ? checkpoints[currentCheckpointIndex].checkpointName : "الوصول";
            float remaining = Mathf.Max(0f, targetTimeSeconds - elapsedTimeSeconds);
            return $"الهدف: {cpName} | المتبقي: {remaining:F0}ث | النقاط: {currentScore}";
        }
    }
}
