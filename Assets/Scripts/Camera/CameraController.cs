using UnityEngine;

namespace Nageel.Camera
{
    public enum CameraViewMode
    {
        ChaseExternal,
        CockpitInterior,
        HoodView
    }

    public class CameraController : MonoBehaviour
    {
        [Header("Target Target")]
        public Transform vehicleTarget;
        public Rigidbody vehicleRigidbody;

        [Header("View Mode")]
        public CameraViewMode currentView = CameraViewMode.ChaseExternal;

        [Header("Chase Camera Settings")]
        public Vector3 chaseOffset = new Vector3(0f, 2.4f, -5.2f);
        public float positionDamping = 6f;
        public float rotationDamping = 5f;
        public float baseFOV = 60f;
        public float maxFOV = 78f;
        public float fovSpeedFactor = 0.15f;

        [Header("Interior / Cockpit Settings")]
        public Vector3 cockpitOffset = new Vector3(-0.38f, 1.25f, 0.25f); // Left driver seat
        public Vector3 hoodOffset = new Vector3(0f, 1.15f, 1.6f);
        public float cockpitVibrationAmount = 0.02f;

        private UnityEngine.Camera cam;
        private Vector3 currentVelocity;

        private void Awake()
        {
            cam = GetComponent<UnityEngine.Camera>();
        }

        private void LateUpdate()
        {
            if (vehicleTarget == null) return;

            switch (currentView)
            {
                case CameraViewMode.ChaseExternal:
                    UpdateChaseCamera();
                    break;
                case CameraViewMode.CockpitInterior:
                    UpdateInteriorCamera(cockpitOffset);
                    break;
                case CameraViewMode.HoodView:
                    UpdateInteriorCamera(hoodOffset);
                    break;
            }
        }

        private void UpdateChaseCamera()
        {
            // Calculate target position relative to vehicle rotation
            Vector3 targetPosition = vehicleTarget.position + vehicleTarget.TransformDirection(chaseOffset);

            // Avoid clipping into mountain ground
            if (targetPosition.y < vehicleTarget.position.y + 0.8f)
            {
                targetPosition.y = vehicleTarget.position.y + 0.8f;
            }

            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * positionDamping);

            // Look ahead towards vehicle plus slight forward look target
            Vector3 lookTarget = vehicleTarget.position + vehicleTarget.forward * 4.0f + Vector3.up * 1.1f;
            Quaternion targetRotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationDamping);

            // Dynamic FOV adjustment with vehicle speed
            if (cam != null && vehicleRigidbody != null)
            {
                float speedKmh = vehicleRigidbody.velocity.magnitude * 3.6f;
                float targetFOV = Mathf.Clamp(baseFOV + (speedKmh * fovSpeedFactor), baseFOV, maxFOV);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * 3f);
            }
        }

        private void UpdateInteriorCamera(Vector3 offset)
        {
            // Position rigidly with car cabin plus subtle vibration
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
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 68f, Time.deltaTime * 5f);
            }
        }

        public void CycleCameraView()
        {
            int next = ((int)currentView + 1) % 3;
            currentView = (CameraViewMode)next;
        }
    }
}
