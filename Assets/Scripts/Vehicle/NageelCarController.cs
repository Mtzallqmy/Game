using System;
using UnityEngine;

namespace Nageel.Vehicle
{
    public enum DriveType
    {
        AllWheelDrive_4WD,
        RearWheelDrive_2WD,
        FourWheelDrive_LowRange
    }

    public enum TransmissionMode
    {
        Automatic,
        Manual
    }

    [RequireComponent(typeof(Rigidbody))]
    public class NageelCarController : MonoBehaviour
    {
        [Header("Vehicle Dynamics")]
        [Tooltip("Mass in kilograms (Realistic Yemeni 4WD SUV: ~2,150 kg)")]
        public float vehicleMass = 2150f;
        public Vector3 centerOfMassOffset = new Vector3(0f, -0.45f, 0.1f);
        public DriveType driveType = DriveType.AllWheelDrive_4WD;
        public TransmissionMode transmission = TransmissionMode.Automatic;

        [Header("Engine Simulation")]
        public float maxTorque = 480f; // Newton-meters (4.5L V6/V8 Mountain Spec)
        public float idleRPM = 800f;
        public float maxRPM = 6200f;
        public float peakTorqueRPM = 3200f;
        public float currentRPM = 800f;
        public float engineBrakingFactor = 0.35f;

        [Header("Transmission & Gears")]
        public float[] forwardGearRatios = { 3.83f, 2.36f, 1.52f, 1.00f, 0.83f };
        public float reverseGearRatio = 3.55f;
        public float finalDriveRatio = 4.10f;
        public float lowRangeMultiplier = 2.48f;
        public int currentGear = 1; // -1 = Reverse, 0 = Neutral, 1..5 = Forward
        public float gearShiftTime = 0.28f;
        private bool isShifting = false;
        private float shiftTimer = 0f;

        [Header("Steering & Handling")]
        public float maxSteeringAngle = 36f;
        public float highSpeedSteeringAngle = 16f;
        public float steeringSpeed = 7f;
        public float currentSteeringAngle = 0f;

        [Header("Brakes")]
        public float brakeTorque = 3200f;
        public float handbrakeTorque = 6500f;
        [Range(0.5f, 0.9f)] public float frontBrakeBias = 0.65f;
        public bool hasABS = true;

        [Header("Suspension Tuning")]
        public float suspensionSpring = 38000f;
        public float suspensionDamper = 4500f;
        public float suspensionTargetPosition = 0.5f;

        [Header("Wheel Colliders & Meshes")]
        public WheelCollider frontLeftCollider;
        public WheelCollider frontRightCollider;
        public WheelCollider rearLeftCollider;
        public WheelCollider rearRightCollider;

        public Transform frontLeftMesh;
        public Transform frontRightMesh;
        public Transform rearLeftMesh;
        public Transform rearRightMesh;

        [Header("Telemetry / Status (Read-Only)")]
        public float currentSpeedKmh = 0f;
        public float slopeAnglePitch = 0f;
        public float slopeAngleRoll = 0f;
        public bool isHandbrakeActive = false;
        public bool isHillStartAssistActive = false;

        // Internal Input Storage
        private float throttleInput = 0f;
        private float brakeInput = 0f;
        private float steerInput = 0f;
        private bool handbrakeInput = false;

        private Rigidbody rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.mass = vehicleMass;
            rb.centerOfMass = centerOfMassOffset;
        }

        private void Start()
        {
            ConfigureWheelColliders();
        }

        private void ConfigureWheelColliders()
        {
            WheelCollider[] allWheels = { frontLeftCollider, frontRightCollider, rearLeftCollider, rearRightCollider };
            foreach (var wc in allWheels)
            {
                if (wc == null) continue;
                JointSpring spring = wc.suspensionSpring;
                spring.spring = suspensionSpring;
                spring.damper = suspensionDamper;
                spring.targetPosition = suspensionTargetPosition;
                wc.suspensionSpring = spring;
            }
        }

        public void SetInputs(float throttle, float steer, float footBrake, bool handbrake)
        {
            throttleInput = Mathf.Clamp01(throttle);
            steerInput = Mathf.Clamp(steer, -1f, 1f);
            brakeInput = Mathf.Clamp01(footBrake);
            handbrakeInput = handbrake;
        }

        private void FixedUpdate()
        {
            CalculateSpeedAndSlope();
            UpdateSteering();
            UpdateEngineAndGears();
            ApplyDriveTorque();
            ApplyBraking();
            ApplyHillAssistAndEngineBraking();
            UpdateWheelMeshPositions();
        }

        private void CalculateSpeedAndSlope()
        {
            // Speed in Km/h
            currentSpeedKmh = rb.velocity.magnitude * 3.6f;

            // Pitch & Roll for mountain clinometer
            Vector3 euler = transform.rotation.eulerAngles;
            slopeAnglePitch = (euler.x > 180f) ? euler.x - 360f : euler.x;
            slopeAngleRoll = (euler.z > 180f) ? euler.z - 360f : euler.z;
        }

        private void UpdateSteering()
        {
            // Speed-sensitive steering dampening for realistic high-speed stability
            float speedFactor = Mathf.InverseLerp(0f, 100f, currentSpeedKmh);
            float targetMaxAngle = Mathf.Lerp(maxSteeringAngle, highSpeedSteeringAngle, speedFactor);
            float targetAngle = steerInput * targetMaxAngle;

            currentSteeringAngle = Mathf.Lerp(currentSteeringAngle, targetAngle, Time.fixedDeltaTime * steeringSpeed);

            if (frontLeftCollider) frontLeftCollider.steerAngle = currentSteeringAngle;
            if (frontRightCollider) frontRightCollider.steerAngle = currentSteeringAngle;
        }

        private void UpdateEngineAndGears()
        {
            if (isShifting)
            {
                shiftTimer += Time.fixedDeltaTime;
                if (shiftTimer >= gearShiftTime)
                {
                    isShifting = false;
                    shiftTimer = 0f;
                }
                return;
            }

            // Automatic Gear Shifting based on RPM thresholds
            if (transmission == TransmissionMode.Automatic && currentGear > 0)
            {
                if (currentRPM > 4800f && currentGear < forwardGearRatios.Length)
                {
                    ShiftGear(currentGear + 1);
                }
                else if (currentRPM < 1900f && currentGear > 1)
                {
                    ShiftGear(currentGear - 1);
                }
            }

            // Calculate RPM based on wheel speed and current gear ratio
            float activeRatio = GetActiveGearRatio();
            float wheelRPM = 0f;
            if (rearLeftCollider && rearRightCollider)
            {
                wheelRPM = (Mathf.Abs(rearLeftCollider.rpm) + Mathf.Abs(rearRightCollider.rpm)) * 0.5f;
            }

            float targetRPM = wheelRPM * activeRatio * finalDriveRatio;
            if (driveType == DriveType.FourWheelDrive_LowRange)
            {
                targetRPM *= lowRangeMultiplier;
            }

            // Engine revs up with throttle even at standstill
            targetRPM = Mathf.Max(targetRPM, idleRPM + (throttleInput * (maxRPM - idleRPM) * 0.6f));
            currentRPM = Mathf.Lerp(currentRPM, Mathf.Clamp(targetRPM, idleRPM, maxRPM), Time.fixedDeltaTime * 6f);
        }

        public void ShiftGear(int targetGear)
        {
            if (targetGear == currentGear || isShifting) return;
            isShifting = true;
            shiftTimer = 0f;
            currentGear = targetGear;
        }

        public void ToggleReverse()
        {
            if (currentGear > 0)
                ShiftGear(-1);
            else
                ShiftGear(1);
        }

        public void Toggle4WDLowRange()
        {
            if (driveType == DriveType.FourWheelDrive_LowRange)
                driveType = DriveType.AllWheelDrive_4WD;
            else
                driveType = DriveType.FourWheelDrive_LowRange;
        }

        private float GetActiveGearRatio()
        {
            if (currentGear == 0) return 0f;
            if (currentGear == -1) return reverseGearRatio;
            int idx = Mathf.Clamp(currentGear - 1, 0, forwardGearRatios.Length - 1);
            return forwardGearRatios[idx];
        }

        private float CalculateEngineTorque()
        {
            if (isShifting || currentGear == 0) return 0f;

            // Non-linear torque curve peak at peakTorqueRPM
            float rpmFactor = 1.0f - Mathf.Abs(currentRPM - peakTorqueRPM) / (maxRPM - idleRPM);
            rpmFactor = Mathf.Clamp(rpmFactor, 0.45f, 1.0f);

            float activeRatio = GetActiveGearRatio();
            float totalRatio = activeRatio * finalDriveRatio;
            if (driveType == DriveType.FourWheelDrive_LowRange)
            {
                totalRatio *= lowRangeMultiplier;
            }

            float engineTorque = throttleInput * maxTorque * rpmFactor * totalRatio;
            if (currentGear == -1) engineTorque = -engineTorque;

            return engineTorque;
        }

        private void ApplyDriveTorque()
        {
            float totalTorque = CalculateEngineTorque();
            float frontTorque = 0f;
            float rearTorque = 0f;

            switch (driveType)
            {
                case DriveType.RearWheelDrive_2WD:
                    rearTorque = totalTorque * 0.5f;
                    break;
                case DriveType.AllWheelDrive_4WD:
                case DriveType.FourWheelDrive_LowRange:
                    // 50/50 4WD split across all four wheels
                    frontTorque = totalTorque * 0.25f;
                    rearTorque = totalTorque * 0.25f;
                    break;
            }

            if (frontLeftCollider) frontLeftCollider.motorTorque = frontTorque;
            if (frontRightCollider) frontRightCollider.motorTorque = frontTorque;
            if (rearLeftCollider) rearLeftCollider.motorTorque = rearTorque;
            if (rearRightCollider) rearRightCollider.motorTorque = rearTorque;
        }

        private void ApplyBraking()
        {
            isHandbrakeActive = handbrakeInput;

            float footBrakeFront = brakeInput * brakeTorque * frontBrakeBias;
            float footBrakeRear = brakeInput * brakeTorque * (1f - frontBrakeBias);

            // ABS Modulation: Reduce brake torque if wheel lock is imminent
            if (hasABS && brakeInput > 0.8f && currentSpeedKmh > 20f)
            {
                float absPulse = (Mathf.Sin(Time.time * 30f) + 1f) * 0.5f;
                footBrakeFront *= Mathf.Lerp(0.75f, 1.0f, absPulse);
                footBrakeRear *= Mathf.Lerp(0.75f, 1.0f, absPulse);
            }

            if (frontLeftCollider) frontLeftCollider.brakeTorque = footBrakeFront;
            if (frontRightCollider) frontRightCollider.brakeTorque = footBrakeFront;

            // Handbrake overrides rear wheel brakes completely
            if (handbrakeInput)
            {
                if (rearLeftCollider) rearLeftCollider.brakeTorque = handbrakeTorque;
                if (rearRightCollider) rearRightCollider.brakeTorque = handbrakeTorque;
            }
            else
            {
                if (rearLeftCollider) rearLeftCollider.brakeTorque = footBrakeRear;
                if (rearRightCollider) rearRightCollider.brakeTorque = footBrakeRear;
            }
        }

        private void ApplyHillAssistAndEngineBraking()
        {
            // Mountain Steep Slope Hill-Start Assist:
            // Prevents vehicle from rolling backward when starting on a > 8 degree incline
            bool onSteepIncline = Mathf.Abs(slopeAnglePitch) > 7f;
            if (onSteepIncline && currentSpeedKmh < 3f && throttleInput < 0.1f && brakeInput < 0.1f)
            {
                isHillStartAssistActive = true;
                rb.velocity = Vector3.Lerp(rb.velocity, Vector3.zero, Time.fixedDeltaTime * 4f);
            }
            else
            {
                isHillStartAssistActive = false;
            }

            // Mountain Engine Braking:
            // When descending steep roads in 1st/2nd gear with zero throttle, engine resistance slows car
            if (throttleInput <= 0.05f && currentSpeedKmh > 10f && currentGear > 0 && currentGear <= 2)
            {
                float descentResistance = engineBrakingFactor * (3f - currentGear) * 800f;
                rb.AddForce(-transform.forward * descentResistance, ForceMode.Force);
            }
        }

        private void UpdateWheelMeshPositions()
        {
            UpdateSingleWheel(frontLeftCollider, frontLeftMesh);
            UpdateSingleWheel(frontRightCollider, frontRightMesh);
            UpdateSingleWheel(rearLeftCollider, rearLeftMesh);
            UpdateSingleWheel(rearRightCollider, rearRightMesh);
        }

        private void UpdateSingleWheel(WheelCollider col, Transform mesh)
        {
            if (col == null || mesh == null) return;
            col.GetWorldPose(out Vector3 pos, out Quaternion rot);
            mesh.position = pos;
            mesh.rotation = rot;
        }

        public void ResetVehicle(Vector3 respawnPosition, Quaternion respawnRotation)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            transform.position = respawnPosition + Vector3.up * 1.5f;
            transform.rotation = respawnRotation;
            currentGear = 1;
            currentRPM = idleRPM;
        }
    }
}
