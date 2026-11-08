using UnityEngine;
using UnityEngine.UI;
using Nageel.Vehicle;
using Nageel.Environment;
using Nageel.Camera;
using Nageel.Weather;
using Nageel.Gameplay;
using Nageel.Performance;

namespace Nageel.UI
{
    public class NageelHUD : MonoBehaviour
    {
        [Header("System References")]
        public NageelCarController carController;
        public CameraController cameraController;
        public DayNightWeatherSystem weatherSystem;
        public GameModeManager gameModeManager;
        public QualityManager qualityManager;

        [Header("UI Text Displays")]
        public Text speedText;
        public Text rpmText;
        public Text gearText;
        public Text locationText;
        public Text vehicleInfoText;
        public Text cameraModeText;
        public Text weatherText;
        public Text missionStatusText;
        public Text fpsQualityText;

        [Header("Gauges & Dials")]
        public Image speedGaugeFill;
        public Image rpmGaugeFill;
        public RectTransform speedNeedle;
        public RectTransform rpmNeedle;

        [Header("UI Panels")]
        public GameObject touchControlsPanel;

        private void Start()
        {
            FindReferencesIfMissing();
        }

        private void FindReferencesIfMissing()
        {
            if (carController == null) carController = FindObjectOfType<NageelCarController>();
            if (cameraController == null) cameraController = FindObjectOfType<CameraController>();
            if (weatherSystem == null) weatherSystem = FindObjectOfType<DayNightWeatherSystem>();
            if (gameModeManager == null) gameModeManager = FindObjectOfType<GameModeManager>();
            if (qualityManager == null) qualityManager = FindObjectOfType<QualityManager>();
        }

        private void Update()
        {
            if (carController == null)
            {
                FindReferencesIfMissing();
                if (carController == null) return;
            }

            UpdateSpeedometer();
            UpdateTachometer();
            UpdateLocationAndZone();
            UpdateTelemetryAndModes();
        }

        private void UpdateSpeedometer()
        {
            float speed = carController.currentSpeedKmh;

            if (speedText != null)
            {
                string label = $"{Mathf.RoundToInt(speed)} " + ArabicTextShaper.Shape("كم/س");
                speedText.text = label;
            }

            if (speedGaugeFill != null)
            {
                speedGaugeFill.fillAmount = Mathf.Clamp01(speed / 180f);
            }

            if (speedNeedle != null)
            {
                float angle = Mathf.Lerp(135f, -135f, speed / 180f);
                speedNeedle.localEulerAngles = new Vector3(0f, 0f, angle);
            }
        }

        private void UpdateTachometer()
        {
            float rpm = carController.currentRPM;

            if (rpmText != null)
            {
                string label = $"{Mathf.RoundToInt(rpm)} " + ArabicTextShaper.Shape("دورة/دقيقة");
                rpmText.text = label;
            }

            if (rpmGaugeFill != null)
            {
                rpmGaugeFill.fillAmount = Mathf.Clamp01(rpm / carController.maxRPM);
            }

            if (rpmNeedle != null)
            {
                float angle = Mathf.Lerp(135f, -135f, rpm / carController.maxRPM);
                rpmNeedle.localEulerAngles = new Vector3(0f, 0f, angle);
            }
        }

        private void UpdateLocationAndZone()
        {
            if (locationText == null) return;

            float zDist = carController.transform.position.z;
            float altitude = 2400f + carController.transform.position.y;
            ZoneInfo zone = MountainRoadGenerator.GetZoneAtDistance(zDist);

            string msg = $"{zone.arabicName} — الارتفاع: {altitude:F0}م — {zone.roadType}";
            locationText.text = ArabicTextShaper.Shape(msg);
        }

        private void UpdateTelemetryAndModes()
        {
            // Gear & Vehicle Info
            if (gearText != null)
            {
                string g = carController.currentGear == -1 ? "R" : $"D{carController.currentGear}";
                string d = carController.driveType == DriveType.FourWheelDrive_LowRange ? "4WD-L" : "4WD-H";
                gearText.text = ArabicTextShaper.Shape($"التعشيق: {g} | {d}");
            }

            if (vehicleInfoText != null)
            {
                vehicleInfoText.text = ArabicTextShaper.Shape(carController.GetArabicVehicleName());
            }

            // Camera view name
            if (cameraModeText != null && cameraController != null)
            {
                cameraModeText.text = ArabicTextShaper.Shape(cameraController.GetArabicCameraName());
            }

            // Weather & Time
            if (weatherText != null && weatherSystem != null)
            {
                string wDesc = weatherSystem.GetArabicWeatherDescription();
                string timeStr = $"{Mathf.FloorToInt(weatherSystem.timeOfDayHours):D2}:00";
                weatherText.text = ArabicTextShaper.Shape($"الطقس: {wDesc} ({timeStr})");
            }

            // Mission / Game Mode
            if (missionStatusText != null && gameModeManager != null)
            {
                missionStatusText.text = ArabicTextShaper.Shape(gameModeManager.GetMissionStatusText());
            }

            // FPS & Quality
            if (fpsQualityText != null && qualityManager != null)
            {
                fpsQualityText.text = $"{qualityManager.currentFPS:F0} FPS | " + ArabicTextShaper.Shape(qualityManager.GetArabicProfileName());
            }
        }

        // Button Callbacks
        public void OnClickSwitchVehicle()
        {
            if (carController != null) carController.CycleVehicle();
        }

        public void OnClickSwitchCamera()
        {
            if (cameraController != null) cameraController.CycleCameraView();
        }

        public void OnClickCycleWeather()
        {
            if (weatherSystem != null) weatherSystem.CycleWeather();
        }

        public void OnClickCycleGameMode()
        {
            if (gameModeManager != null) gameModeManager.CycleGameMode();
        }

        public void OnClickToggleHeadlights()
        {
            if (carController != null) carController.ToggleHeadlights();
        }

        public void OnClickCycleQuality()
        {
            if (qualityManager != null) qualityManager.CycleQuality();
        }
    }
}
