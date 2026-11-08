package com.moataz.nageel

import kotlin.math.*

/**
 * Nageel 4WD Vehicle Physics & Mountain Simulation Engine.
 * Implements realistic vehicle dynamics:
 * - 2,150 kg mass (Yemeni Mountain 4x4 SUV)
 * - 480 Nm torque curve with peak at 3,200 RPM
 * - 5-speed transmission + 4WD Low-Range transfer case
 * - Dynamic slope incline & descent engine braking
 * - Mobile touch input & chase/cockpit cameras
 */
class SimulationEngine {

    enum class DriveType { AWD_4WD, RWD_2WD, LOW_RANGE_4WD }
    enum class CameraMode { CHASE, COCKPIT, HOOD }

    // Vehicle Specifications
    val vehicleMass = 2150.0f
    val maxTorque = 480.0f
    val idleRPM = 800.0f
    val maxRPM = 6200.0f
    val peakTorqueRPM = 3200.0f

    val forwardGearRatios = floatArrayOf(3.83f, 2.36f, 1.52f, 1.00f, 0.83f)
    val reverseGearRatio = 3.55f
    val finalDriveRatio = 4.10f
    val lowRangeMultiplier = 2.48f

    // Dynamic State
    var currentSpeedKmh = 0.0f
    var currentRPM = 800.0f
    var currentGear = 1
    var driveType = DriveType.AWD_4WD
    var cameraMode = CameraMode.CHASE

    var slopePitch = 12.0f // Degrees (Yemeni mountain road gradient)
    var slopeRoll = 3.0f

    var isHandbrakeActive = false
    var isHillStartActive = false

    // Inputs
    var throttleInput = 0.0f
    var brakeInput = 0.0f
    var steerInput = 0.0f

    // World position (meters along pass)
    var distanceTravelledMeters = 0.0f
    val totalPassLengthMeters = 2500.0f

    fun update(deltaTime: Float) {
        val dt = deltaTime.coerceIn(0.001f, 0.05f)

        // 1. Calculate Engine RPM
        val targetRPM = if (currentGear > 0) {
            val ratio = forwardGearRatios[currentGear - 1] * finalDriveRatio *
                    (if (driveType == DriveType.LOW_RANGE_4WD) lowRangeMultiplier else 1.0f)
            val wheelRpm = (currentSpeedKmh / 3.6f) / (2.0f * Math.PI.toFloat() * 0.42f) * 60.0f
            max(idleRPM, wheelRpm * ratio + (throttleInput * 1200f))
        } else {
            idleRPM + (throttleInput * (maxRPM - idleRPM))
        }

        currentRPM = currentRPM + (targetRPM.coerceIn(idleRPM, maxRPM) - currentRPM) * (dt * 6.0f)

        // Automatic gear shifting
        if (currentRPM > 4800f && currentGear < forwardGearRatios.size) {
            currentGear++
        } else if (currentRPM < 1900f && currentGear > 1) {
            currentGear--
        }

        // 2. Drive Torque & Acceleration
        val rpmFactor = (1.0f - abs(currentRPM - peakTorqueRPM) / (maxRPM - idleRPM)).coerceIn(0.45f, 1.0f)
        val activeRatio = if (currentGear > 0) forwardGearRatios[currentGear - 1] else reverseGearRatio
        val lowMultiplier = if (driveType == DriveType.LOW_RANGE_4WD) lowRangeMultiplier else 1.0f

        val driveTorque = throttleInput * maxTorque * rpmFactor * activeRatio * finalDriveRatio * lowMultiplier
        val driveForce = (driveTorque / 0.42f) // Force = Torque / wheel radius

        // 3. Resistances (Aerodynamic drag, rolling resistance, gravity slope)
        val gravityForce = vehicleMass * 9.81f * sin(Math.toRadians(slopePitch.toDouble())).toFloat()
        val rollingResistance = vehicleMass * 9.81f * 0.015f
        val aeroDrag = 0.5f * 1.225f * 0.45f * 2.6f * (currentSpeedKmh / 3.6f).pow(2)

        // Braking Force
        val footBrakeForce = brakeInput * 8500.0f
        val handBrakeForce = if (isHandbrakeActive) 14000.0f else 0.0f
        val totalBrake = footBrakeForce + handBrakeForce

        // Net Force along road slope
        var netForce = driveForce - gravityForce - rollingResistance - aeroDrag

        // Engine compression braking on steep descents
        if (throttleInput < 0.05f && currentSpeedKmh > 5f && currentGear in 1..2) {
            netForce -= (3f - currentGear) * 1200f
        }

        if (totalBrake > 0f) {
            netForce -= totalBrake * sign(if (currentSpeedKmh != 0f) currentSpeedKmh else 1f)
        }

        // Hill-start assist holds vehicle on > 7° slopes when standstill
        if (abs(slopePitch) > 7f && currentSpeedKmh < 2f && throttleInput < 0.1f && brakeInput < 0.1f) {
            isHillStartActive = true
            netForce = 0.0f
            currentSpeedKmh = 0.0f
        } else {
            isHillStartActive = false
        }

        // Acceleration and Speed integration
        val accel = netForce / vehicleMass
        val currentSpeedMs = (currentSpeedKmh / 3.6f) + (accel * dt)
        currentSpeedKmh = max(0.0f, currentSpeedMs * 3.6f)

        distanceTravelledMeters += (currentSpeedKmh / 3.6f) * dt
        if (distanceTravelledMeters > totalPassLengthMeters) {
            distanceTravelledMeters = totalPassLengthMeters
        }
    }

    fun toggle4WD() {
        driveType = if (driveType == DriveType.LOW_RANGE_4WD) {
            DriveType.AWD_4WD
        } else {
            DriveType.LOW_RANGE_4WD
        }
    }

    fun cycleCamera() {
        cameraMode = when (cameraMode) {
            CameraMode.CHASE -> CameraMode.COCKPIT
            CameraMode.COCKPIT -> CameraMode.HOOD
            CameraMode.HOOD -> CameraMode.CHASE
        }
    }

    fun resetVehicle() {
        currentSpeedKmh = 0.0f
        currentRPM = idleRPM
        currentGear = 1
        distanceTravelledMeters = 0.0f
        isHandbrakeActive = false
    }
}
