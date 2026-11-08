using UnityEngine;

namespace Nageel.Camera
{
    public enum CameraViewMode
    {
        ChaseClose,         // 1. مطاردة قريبة
        ChaseFar,           // 2. مطاردة بعيدة وسينمائية
        CockpitInterior,    // 3. مقصورة داخلية (مع المقود والعدادات)
        HoodBumper          // 4. غطاء المحرك والمصد الأمامي
    }

    public class CameraController : MonoBehaviour
    {
        [Header("Target Tracking")]
        public Transform vehicleTarget;
        public Rigidbody vehicleRigidbody;

        [Header("Active View Mode")]
        public CameraViewMode currentView = CameraViewMode.ChaseClose;

        [Header("1. Chase Close Settings")]
        public Vector3 chaseCloseOffset = new Vector3(0f, 2.1f, -4.8f);
        public float positionDamping = 7f;
        public float rotationDamping = 6f;
        public float baseFOV = 60f;
        public float maxFOV = 78f;
        public float fovSpeedFactor = 0.15f;

        [Header("2. Chase Far Settings")]
        public Vector3 chaseFarOffset = new Vector3(0f, 3.8f, -8.5f);
        public float farFOV = 68f;

        [Header("3. Cockpit Interior Settings")]
        public Vector3 cockpitOffset = new Vector3(-0.38f, 1.25f, 0.25f); // Left driver seat behind wheel
        public float cockpitVibrationAmount = 0.02f;

        [Header("4. Hood / Bumper Settings")]
        public Vector3 hoodOffset = new Vector3(0f, 0.95f, 1.85f); // Front bumper looking forward

        private UnityEngine.Camera cam;

        private void Awake()
        {
            cam = GetComponent<UnityEngine.Camera>();
        }

        private void LateUpdate()
        {
            if (vehicleTarget == null) return;

            switch (currentView)
            {
                case CameraViewMode.ChaseClose:
                    UpdateChaseCamera(chaseCloseOffset, baseFOV);
                    break;
                case CameraViewMode.ChaseFar:
                    UpdateChaseCamera(chaseFarOffset, farFOV);
                    break;
                case CameraViewMode.CockpitInterior:
                    UpdateInteriorCamera(cockpitOffset, 65f);
                    break;
                case CameraViewMode.HoodBumper:
                    UpdateInteriorCamera(hoodOffset, 74f);
                    break;
            }
        }

        private void UpdateChaseCamera(Vector3 offset, float targetBaseFOV)
        {
            Vector3 targetPosition = vehicleTarget.position + vehicleTarget.TransformDirection(offset);

            // Avoid clipping below road surface
            if (targetPosition.y < vehicleTarget.position.y + 0.6f)
            {
                targetPosition.y = vehicleTarget.position.y + 0.6f;
            }

            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * positionDamping);

            Vector3 lookTarget = vehicleTarget.position + vehicleTarget.forward * 4.5f + Vector3.up * 1.0f;
            Quaternion targetRotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationDamping);

            if (cam != null && vehicleRigidbody != null)
            {
                float speedKmh = vehicleRigidbody.velocity.magnitude * 3.6f;
                float dynamicFOV = Mathf.Clamp(targetBaseFOV + (speedKmh * fovSpeedFactor), targetBaseFOV, maxFOV);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, dynamicFOV, Time.deltaTime * 3f);
            }
        }

        private void UpdateInteriorCamera(Vector3 offset, float fixedFOV)
        {
            Vector3 basePos = vehicleTarget.TransformPoint(offset);
            float speedKmh = vehicleRigidbody != null ? vehicleRigidbody.velocity.magnitude * 3.6f : 0f;
            float vibration = (speedKmh / 100f) * cockpitVibrationAmount;

            Vector3 shake = new Vector3(
                (Mathf.PerlinNoise(Time.time * 25f, 0f) - 0.5f) * vibration,
                (Mathf.PerlinNoise(0f, Time.time * 25f) - 0.5f) * vibration,
                0f
            );

            transform.position = basePos + vehicleTarget.TransformDirection(shake);
            transform.rotation = vehicleTarget.rotation;

            if (cam != null)
            {
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fixedFOV, Time.deltaTime * 5f);
            }
        }

        public void CycleCameraView()
        {
            int next = ((int)currentView + 1) % 4;
            currentView = (CameraViewMode)next;
        }

        public string GetArabicCameraName()
        {
            switch (currentView)
            {
                case CameraViewMode.ChaseClose: return "مطاردة قريبة";
                case CameraViewMode.ChaseFar: return "مطاردة بعيدة / بانوراما";
                case CameraViewMode.CockpitInterior: return "مقصورة القيادة الداخلية";
                case CameraViewMode.HoodBumper: return "غطاء المحرك / سينمائية";
                default: return "كاميرا";
            }
        }
    }
}
