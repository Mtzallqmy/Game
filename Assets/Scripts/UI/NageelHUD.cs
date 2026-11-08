using UnityEngine;
using UnityEngine.UI;
using Nageel.Vehicle;

namespace Nageel.UI
{
    public class NageelHUD : MonoBehaviour
    {
        [Header("Vehicle Reference")]
        public NageelCarController carController;

        [Header("UI Text Displays")]
        public Text speedText;
        public Text rpmText;
        public Text gearText;
        public Text driveModeText;
        public Text inclineText;
        public Text locationText;
        public Text handbrakeWarningText;

        [Header("Gauges & Dials")]
        public Image speedGaugeFill;
        public Image rpmGaugeFill;
        public RectTransform speedNeedle;
        public RectTransform rpmNeedle;

        [Header("UI Panels")]
        public GameObject touchControlsPanel;

        private void Start()
        {
            if (carController == null)
            {
                carController = FindObjectOfType<NageelCarController>();
            }

            if (locationText != null)
            {
                locationText.text = ArabicTextShaper.Shape("نقيل سمارة — الارتفاع: 2,800 م — احذر المنعطفات الحادة");
            }
        }

        private void Update()
        {
            if (carController == null) return;

            UpdateSpeedometer();
            UpdateTachometer();
            UpdateTransmissionUI();
            UpdateClinometer();
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
                speedGaugeFill.fillAmount = Mathf.Clamp01(speed / 160f);
            }

            if (speedNeedle != null)
            {
                // Rotate needle from +135 deg (0 km/h) to -135 deg (160 km/h)
                float angle = Mathf.Lerp(135f, -135f, speed / 160f);
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

        private void UpdateTransmissionUI()
        {
            if (gearText != null)
            {
                string gearStr = "";
                if (carController.currentGear == -1)
                {
                    gearStr = "R (" + ArabicTextShaper.Shape("رجوع") + ")";
                }
                else if (carController.currentGear == 0)
                {
                    gearStr = "N (" + ArabicTextShaper.Shape("فاضي") + ")";
                }
                else
                {
                    gearStr = "D" + carController.currentGear;
                }
                gearText.text = gearStr;
            }

            if (driveModeText != null)
            {
                string modeStr = carController.driveType == DriveType.FourWheelDrive_LowRange
                    ? "4WD " + ArabicTextShaper.Shape("ثقيل (منحدرات)")
                    : "4WD " + ArabicTextShaper.Shape("عادي (دفع رباعي)");
                driveModeText.text = modeStr;
            }

            if (handbrakeWarningText != null)
            {
                handbrakeWarningText.gameObject.SetActive(carController.isHandbrakeActive);
                if (carController.isHandbrakeActive)
                {
                    handbrakeWarningText.text = ArabicTextShaper.Shape("فرامل اليد مفعلة");
                }
            }
        }

        private void UpdateClinometer()
        {
            if (inclineText != null)
            {
                float pitch = Mathf.Round(carController.slopeAnglePitch);
                float roll = Mathf.Round(carController.slopeAngleRoll);
                string inclineStr = ArabicTextShaper.Shape("الميلان") + $": {pitch}° | " + ArabicTextShaper.Shape("الميل الجانبي") + $": {roll}°";
                inclineText.text = inclineStr;
            }
        }
    }
}
