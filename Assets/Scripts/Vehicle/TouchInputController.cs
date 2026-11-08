using UnityEngine;
using UnityEngine.UI;

namespace Nageel.Vehicle
{
    public class TouchInputController : MonoBehaviour
    {
        [Header("Target Vehicle")]
        public NageelCarController carController;

        [Header("Input Control States")]
        public float throttle = 0f;
        public float steer = 0f;
        public float brake = 0f;
        public bool handbrake = false;

        [Header("Touch Settings")]
        public bool useTiltSteering = false;
        public float tiltSensitivity = 1.8f;
        public float buttonSteerSpeed = 3.5f;

        private bool isLeftSteerPressed = false;
        private bool isRightSteerPressed = false;
        private bool isThrottlePressed = false;
        private bool isBrakePressed = false;

        private Vector3 initialSpawnPosition;
        private Quaternion initialSpawnRotation;

        private void Start()
        {
            if (carController == null)
            {
                carController = GetComponent<NageelCarController>();
            }

            if (carController != null)
            {
                initialSpawnPosition = carController.transform.position;
                initialSpawnRotation = carController.transform.rotation;
            }
        }

        private void Update()
        {
            HandleKeyboardInput();
            HandleTouchSteering();
            HandleTouchPedals();

            if (carController != null)
            {
                carController.SetInputs(throttle, steer, brake, handbrake);
            }
        }

        private void HandleKeyboardInput()
        {
            // Standalone / Unity Editor Testing Fallback
            float kbHorizontal = Input.GetAxis("Horizontal");
            float kbVertical = Input.GetAxis("Vertical");
            bool kbHandbrake = Input.GetKey(KeyCode.Space);

            if (Mathf.Abs(kbHorizontal) > 0.05f)
            {
                steer = kbHorizontal;
            }

            if (kbVertical > 0.05f)
            {
                throttle = kbVertical;
                brake = 0f;
            }
            else if (kbVertical < -0.05f)
            {
                brake = -kbVertical;
                throttle = 0f;
            }

            if (kbHandbrake)
            {
                handbrake = true;
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetVehicle();
            }

            if (Input.GetKeyDown(KeyCode.L))
            {
                Toggle4WDMode();
            }

            if (Input.GetKeyDown(KeyCode.G))
            {
                ToggleReverseGear();
            }
        }

        private void HandleTouchSteering()
        {
            if (useTiltSteering)
            {
                float tiltX = Input.acceleration.x;
                steer = Mathf.Clamp(tiltX * tiltSensitivity, -1f, 1f);
                return;
            }

            float targetSteer = 0f;
            if (isLeftSteerPressed) targetSteer -= 1f;
            if (isRightSteerPressed) targetSteer += 1f;

            // Smooth interpolation for button steering
            steer = Mathf.MoveTowards(steer, targetSteer, Time.deltaTime * buttonSteerSpeed);
        }

        private void HandleTouchPedals()
        {
            // Throttle pedal
            if (isThrottlePressed)
            {
                throttle = Mathf.MoveTowards(throttle, 1f, Time.deltaTime * 4f);
            }
            else if (!Input.GetKey(KeyCode.W) && !Input.GetKey(KeyCode.UpArrow))
            {
                throttle = Mathf.MoveTowards(throttle, 0f, Time.deltaTime * 5f);
            }

            // Foot brake pedal
            if (isBrakePressed)
            {
                brake = Mathf.MoveTowards(brake, 1f, Time.deltaTime * 5f);
            }
            else if (!Input.GetKey(KeyCode.S) && !Input.GetKey(KeyCode.DownArrow))
            {
                brake = Mathf.MoveTowards(brake, 0f, Time.deltaTime * 6f);
            }
        }

        // UI Event Handlers for Mobile Buttons
        public void OnSteerLeftDown() { isLeftSteerPressed = true; }
        public void OnSteerLeftUp() { isLeftSteerPressed = false; }

        public void OnSteerRightDown() { isRightSteerPressed = true; }
        public void OnSteerRightUp() { isRightSteerPressed = false; }

        public void OnThrottleDown() { isThrottlePressed = true; }
        public void OnThrottleUp() { isThrottlePressed = false; }

        public void OnBrakeDown() { isBrakePressed = true; }
        public void OnBrakeUp() { isBrakePressed = false; }

        public void OnHandbrakeToggle()
        {
            handbrake = !handbrake;
        }

        public void ToggleReverseGear()
        {
            if (carController != null)
            {
                carController.ToggleReverse();
            }
        }

        public void Toggle4WDMode()
        {
            if (carController != null)
            {
                carController.Toggle4WDLowRange();
            }
        }

        public void ResetVehicle()
        {
            if (carController != null)
            {
                carController.ResetVehicle(initialSpawnPosition, initialSpawnRotation);
            }
        }
    }
}
