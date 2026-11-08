using UnityEngine;

namespace Nageel.Performance
{
    public enum QualityProfile
    {
        Low,        // أجهزة اقتصادية (استهداف 30 FPS مستقرة وتوفير بطارية)
        Medium,     // أجهزة متوسطة متوازنة
        High        // أجهزة قوية رائدة (60 FPS ومؤثرات واقعية كاملة)
    }

    public class QualityManager : MonoBehaviour
    {
        public static QualityManager Instance { get; private set; }

        [Header("Active Quality Profile")]
        public QualityProfile currentProfile = QualityProfile.Medium;

        [Header("Runtime Metrics (Read-Only)")]
        public float currentFPS = 30f;
        private float frameTimeAccumulator = 0f;
        private int frameCount = 0;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            ApplyQualityProfile(currentProfile);
        }

        public void SetQuality(QualityProfile profile)
        {
            currentProfile = profile;
            ApplyQualityProfile(profile);
        }

        public void CycleQuality()
        {
            int next = ((int)currentProfile + 1) % 3;
            SetQuality((QualityProfile)next);
        }

        private void ApplyQualityProfile(QualityProfile profile)
        {
            switch (profile)
            {
                case QualityProfile.Low:
                    Application.targetFrameRate = 30;
                    QualitySettings.shadowDistance = 25f;
                    QualitySettings.shadows = ShadowQuality.HardOnly;
                    QualitySettings.lodBias = 0.7f;
                    QualitySettings.vSyncCount = 0;
                    QualitySettings.particleRaycastBudget = 64;
                    break;

                case QualityProfile.Medium:
                    Application.targetFrameRate = 45;
                    QualitySettings.shadowDistance = 70f;
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = ShadowResolution.Medium;
                    QualitySettings.lodBias = 1.0f;
                    QualitySettings.vSyncCount = 0;
                    QualitySettings.particleRaycastBudget = 256;
                    break;

                case QualityProfile.High:
                    Application.targetFrameRate = 60;
                    QualitySettings.shadowDistance = 160f;
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = ShadowResolution.High;
                    QualitySettings.lodBias = 1.5f;
                    QualitySettings.vSyncCount = 0;
                    QualitySettings.particleRaycastBudget = 1024;
                    break;
            }

            Debug.Log($"[Nageel Quality] Applied Profile: {profile} (Target FPS: {Application.targetFrameRate})");
        }

        private void Update()
        {
            frameCount++;
            frameTimeAccumulator += Time.unscaledDeltaTime;

            if (frameTimeAccumulator >= 0.5f)
            {
                currentFPS = frameCount / frameTimeAccumulator;
                frameCount = 0;
                frameTimeAccumulator = 0f;
            }
        }

        public string GetArabicProfileName()
        {
            switch (currentProfile)
            {
                case QualityProfile.Low: return "منخفضة (30 FPS)";
                case QualityProfile.Medium: return "متوسطة (متوازنة)";
                case QualityProfile.High: return "عالية (60 FPS)";
                default: return "جودة";
            }
        }
    }
}
