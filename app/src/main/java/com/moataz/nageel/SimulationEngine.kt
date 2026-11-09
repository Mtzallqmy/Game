package com.moataz.nageel

import kotlin.math.*

/**
 * Nageel Mountain Pass Simulation & Physics Engine
 * Implements full 3D road physics, lateral vehicle dynamics, centrifugal curve forces,
 * multi-gear transmission, AI traffic interaction, and sound synchronization.
 */
class SimulationEngine {

    enum class VehicleType(
        val arabicName: String,
        val massKg: Float,
        val torqueNm: Float,
        val peakRPM: Float,
        val topSpeedKmh: Float,
        val handling: Float
    ) {
        SUV_4WD("لاندكروزر 4WD جبلية", 2250.0f, 510.0f, 3200.0f, 165f, 1.05f),
        PICKUP_SHAS("شاص 4WD للأحمال الصعبة", 1980.0f, 460.0f, 3000.0f, 150f, 0.95f),
        SEDAN("سيدان سياحية رياضية", 1450.0f, 340.0f, 3800.0f, 195f, 1.25f)
    }

    enum class DriveType { AWD_4WD, RWD_2WD, LOW_RANGE_4WD }

    enum class CameraMode(val arabicName: String) {
        CHASE_CLOSE("مطاردة قريبة"),
        CHASE_FAR("مطاردة بعيدة"),
        COCKPIT("مقصورة القيادة"),
        HOOD_BUMPER("غطاء المحرك")
    }

    enum class WorldZone(val arabicName: String, val startDist: Float, val endDist: Float, val friction: Float) {
        NAQIL_PASS("نَقِيل سَمَارَة الجبلي", 0f, 3500f, 1.0f),
        GREEN_HIGHLANDS("المرتفعات الخضراء والمدرجات", 3500f, 7000f, 0.92f),
        ROCKY_WADI("وادي الصخور والممرات الضيقة", 7000f, 10500f, 0.72f)
    }

    enum class WeatherType(val arabicName: String) {
        CLEAR_DAY("مشمس صافٍ"),
        HIGHLAND_FOG("ضباب المرتفعات"),
        MOUNTAIN_RAIN("أمطار جبلية"),
        WADI_DUST("عواصف رملية")
    }

    enum class GameMode(val arabicName: String) {
        FREE_ROAM("قيادة حرة"),
        DELIVERY("رحلة توصيل"),
        HILL_CLIMB("تحدي صعود النقيل")
    }

    enum class QualityLevel(val arabicName: String, val targetFps: Int) {
        LOW("منخفضة (30 FPS)", 30),
        MEDIUM("متوسطة (45 FPS)", 45),
        HIGH("عالية (60 FPS)", 60)
    }

    data class AITraffic(
        var distanceMeters: Float,
        var playerX: Float, // -0.5 = right lane (oncoming/forward)
        var speedKmh: Float,
        val isOncoming: Boolean,
        val carTypeIndex: Int
    )

    // Track instance
    val track = RoadTrack()

    // Sound engine instance
    val soundEngine = SoundEngine()

    // Current Active Vehicle
    var currentVehicleType = VehicleType.SUV_4WD
    val idleRPM = 800.0f
    val maxRPM = 6200.0f

    // Transmission & Gear Ratios
    var forwardGearRatios = floatArrayOf(3.83f, 2.36f, 1.52f, 1.00f, 0.83f)
    var reverseGearRatio = 3.55f
    var finalDriveRatio = 4.10f
    val lowRangeMultiplier = 2.48f

    // Dynamic State
    var currentSpeedKmh = 0.0f
    var currentRPM = 800.0f
    var currentGear = 1
    var driveType = DriveType.AWD_4WD
    var cameraMode = CameraMode.CHASE_CLOSE

    // Lateral Position on the road: -1.0 = left curb, 0.0 = center, +1.0 = right curb
    var playerX = 0.35f // Default right-hand driving lane

    var slopePitch = 12.0f // Incline gradient degrees
    var isHandbrakeActive = false
    var isHillStartActive = false
    var areHeadlightsOn = false

    // Atmosphere & Environment
    var timeOfDayHours = 14.5f // 14:30 afternoon
    var currentWeather = WeatherType.CLEAR_DAY
    var activeGameMode = GameMode.FREE_ROAM
    var currentQuality = QualityLevel.HIGH

    // Inputs
    var throttleInput = 0.0f
    var brakeInput = 0.0f
    var steerInput = 0.0f

    // World Navigation
    var distanceTravelledMeters = 0.0f
    val totalWorldLengthMeters = 10500.0f

    // Collision & Screen Shake
    var cameraShake = 0f
    var collisionTimer = 0f

    // Mission Telemetry
    var missionElapsedTime = 0f
    var missionTargetTime = 300f
    var missionScore = 1000
    var isMissionCompleted = false
    var checkpointsPassed = 0

    // AI Traffic Fleet
    val trafficCars = mutableListOf<AITraffic>()

    init {
        applyVehicleSpecs(VehicleType.SUV_4WD)
        initTraffic()
        soundEngine.start()
    }

    fun applyVehicleSpecs(type: VehicleType) {
        currentVehicleType = type
        when (type) {
            VehicleType.SUV_4WD -> {
                forwardGearRatios = floatArrayOf(3.83f, 2.36f, 1.52f, 1.00f, 0.83f)
                finalDriveRatio = 4.10f
                driveType = DriveType.AWD_4WD
            }
            VehicleType.SEDAN -> {
                forwardGearRatios = floatArrayOf(3.45f, 2.10f, 1.45f, 1.00f, 0.82f, 0.68f)
                finalDriveRatio = 3.65f
                driveType = DriveType.RWD_2WD
            }
            VehicleType.PICKUP_SHAS -> {
                forwardGearRatios = floatArrayOf(4.15f, 2.50f, 1.58f, 1.00f, 0.80f)
                finalDriveRatio = 4.30f
                driveType = DriveType.AWD_4WD
            }
        }
    }

    private fun initTraffic() {
        trafficCars.clear()
        trafficCars.add(AITraffic(380f, -0.45f, 45f, isOncoming = true, carTypeIndex = 0))
        trafficCars.add(AITraffic(750f, 0.45f, 65f, isOncoming = false, carTypeIndex = 1))
        trafficCars.add(AITraffic(1350f, -0.45f, 40f, isOncoming = true, carTypeIndex = 2))
        trafficCars.add(AITraffic(2100f, 0.45f, 55f, isOncoming = false, carTypeIndex = 0))
        trafficCars.add(AITraffic(2950f, -0.45f, 35f, isOncoming = true, carTypeIndex = 1))
        trafficCars.add(AITraffic(4200f, 0.45f, 60f, isOncoming = false, carTypeIndex = 2))
        trafficCars.add(AITraffic(5600f, -0.45f, 48f, isOncoming = true, carTypeIndex = 0))
        trafficCars.add(AITraffic(7400f, 0.45f, 42f, isOncoming = false, carTypeIndex = 1))
        trafficCars.add(AITraffic(8900f, -0.45f, 38f, isOncoming = true, carTypeIndex = 2))
    }

    fun update(deltaTime: Float) {
        val dt = deltaTime.coerceIn(0.001f, 0.05f)

        // Advance Day/Night time (1 sec = 12 sec game time)
        timeOfDayHours = (timeOfDayHours + (dt * 12f) / 3600f * 24f) % 24f
        areHeadlightsOn = timeOfDayHours < 6f || timeOfDayHours > 18.5f || currentWeather == WeatherType.MOUNTAIN_RAIN

        // Active segment and zone
        val currentSeg = track.findSegment(distanceTravelledMeters)
        val currentZone = currentSeg.zone

        // 1. Friction & Road Grip
        var surfaceGrip = when (currentWeather) {
            WeatherType.MOUNTAIN_RAIN -> currentZone.friction * 0.82f
            WeatherType.WADI_DUST -> currentZone.friction * 0.75f
            else -> currentZone.friction
        }

        // Off-road shoulder slowdown
        val isOffroad = abs(playerX) > 1.0f
        if (isOffroad) {
            surfaceGrip *= 0.55f
        }

        // 2. Centrifugal Force and Lateral Steering
        val speedRatio = (currentSpeedKmh / currentVehicleType.topSpeedKmh).coerceIn(0f, 1f)
        val lateralAgility = currentVehicleType.handling * (0.85f + speedRatio * 1.35f)

        // Player steering input
        playerX += steerInput * lateralAgility * (dt * 1.8f)

        // Centrifugal force pushing outward on mountain curves
        val centrifugalForce = (currentSeg.curve * 0.035f) * (currentSpeedKmh / 100f).pow(1.5f)
        playerX -= centrifugalForce * dt

        // Collision with left/right guardrails or mountain slopes
        if (playerX < -1.4f) {
            playerX = -1.38f
            currentSpeedKmh *= 0.94f
            cameraShake = 0.25f
        } else if (playerX > 1.4f) {
            playerX = 1.38f
            currentSpeedKmh *= 0.94f
            cameraShake = 0.25f
        }

        // 3. Slope Pitch (Degrees)
        slopePitch = when (currentZone) {
            WorldZone.NAQIL_PASS -> {
                val t = distanceTravelledMeters / 3500f
                12f + sin(t * Math.PI.toFloat() * 6f) * 5f
            }
            WorldZone.GREEN_HIGHLANDS -> {
                val t = (distanceTravelledMeters - 3500f) / 3500f
                4f + sin(t * Math.PI.toFloat() * 4f) * 3f
            }
            WorldZone.ROCKY_WADI -> {
                val t = (distanceTravelledMeters - 7000f) / 3500f
                -8f + sin(t * Math.PI.toFloat() * 5f) * 4f
            }
        }

        // 4. Engine RPM Simulation
        val activeGearRatio = if (currentGear > 0) forwardGearRatios[currentGear - 1] else reverseGearRatio
        val lowMultiplier = if (driveType == DriveType.LOW_RANGE_4WD) lowRangeMultiplier else 1.0f

        val targetRPM = if (currentGear > 0) {
            val ratio = activeGearRatio * finalDriveRatio * lowMultiplier
            val wheelRpm = (currentSpeedKmh / 3.6f) / (2.0f * Math.PI.toFloat() * 0.42f) * 60.0f
            max(idleRPM, wheelRpm * ratio + (throttleInput * 1200f))
        } else {
            idleRPM + (throttleInput * (maxRPM - idleRPM))
        }

        currentRPM += (targetRPM.coerceIn(idleRPM, maxRPM) - currentRPM) * (dt * 6.5f)

        // Automatic gear shifting
        if (currentRPM > 4900f && currentGear < forwardGearRatios.size) {
            currentGear++
        } else if (currentRPM < 1950f && currentGear > 1) {
            currentGear--
        }

        // 5. Torque & Acceleration Forces
        val rpmFactor = (1.0f - abs(currentRPM - currentVehicleType.peakRPM) / (maxRPM - idleRPM)).coerceIn(0.45f, 1.0f)
        val driveTorque = throttleInput * currentVehicleType.torqueNm * rpmFactor * activeGearRatio * finalDriveRatio * lowMultiplier
        val driveForce = (driveTorque / 0.42f) * surfaceGrip

        // Resistances
        val gravityForce = currentVehicleType.massKg * 9.81f * sin(Math.toRadians(slopePitch.toDouble())).toFloat()
        val rollingResistance = currentVehicleType.massKg * 9.81f * (0.018f / surfaceGrip)
        val aeroDrag = 0.5f * 1.225f * 0.45f * 2.6f * (currentSpeedKmh / 3.6f).pow(2)

        // Braking Forces
        val footBrakeForce = brakeInput * 9200.0f * surfaceGrip
        val handBrakeForce = if (isHandbrakeActive) 16000.0f else 0.0f
        val totalBrake = footBrakeForce + handBrakeForce

        var netForce = driveForce - gravityForce - rollingResistance - aeroDrag

        // Engine compression braking
        if (throttleInput < 0.05f && currentSpeedKmh > 5f && currentGear in 1..2) {
            netForce -= (3f - currentGear) * 1400f
        }

        if (totalBrake > 0f) {
            netForce -= totalBrake * sign(if (currentSpeedKmh != 0f) currentSpeedKmh else 1f)
        }

        // Hill-start assist holds vehicle on > 7° slopes when stationary
        if (abs(slopePitch) > 7f && currentSpeedKmh < 2f && throttleInput < 0.1f && brakeInput < 0.1f) {
            isHillStartActive = true
            netForce = 0.0f
            currentSpeedKmh = 0.0f
        } else {
            isHillStartActive = false
        }

        // Acceleration and Speed integration
        val accel = netForce / currentVehicleType.massKg
        val currentSpeedMs = (currentSpeedKmh / 3.6f) + (accel * dt)
        currentSpeedKmh = max(0.0f, min(currentSpeedMs * 3.6f, currentVehicleType.topSpeedKmh))

        // Distance Travelled Integration (Camera Z)
        distanceTravelledMeters += (currentSpeedKmh / 3.6f) * dt
        if (distanceTravelledMeters >= totalWorldLengthMeters) {
            distanceTravelledMeters = 0.0f // Seamless loop
        }

        // 6. Camera Shake Decay
        if (cameraShake > 0f) {
            cameraShake = max(0f, cameraShake - dt * 1.5f)
        }

        // 7. Update AI Traffic
        updateAITraffic(dt)

        // 8. Update Sound Engine parameters
        soundEngine.targetRPM = currentRPM
        soundEngine.throttle = throttleInput
        soundEngine.isBraking = brakeInput > 0.1f
        soundEngine.isDrifting = (isHandbrakeActive && currentSpeedKmh > 20f) || (abs(steerInput) > 0.8f && currentSpeedKmh > 75f)

        // 9. Mission Telemetry
        if (activeGameMode != GameMode.FREE_ROAM && !isMissionCompleted) {
            missionElapsedTime += dt
            if (distanceTravelledMeters >= (checkpointsPassed + 1) * 2500f) {
                checkpointsPassed++
                missionScore += 600
                if (checkpointsPassed >= 4 || distanceTravelledMeters >= 10000f) {
                    isMissionCompleted = true
                    val timeBonus = max(0f, missionTargetTime - missionElapsedTime) * 10f
                    missionScore += timeBonus.toInt()
                }
            }
        }
    }

    private fun updateAITraffic(dt: Float) {
        for (car in trafficCars) {
            // AI speed
            val moveDelta = (car.speedKmh / 3.6f) * dt
            if (car.isOncoming) {
                car.distanceMeters -= moveDelta
                if (car.distanceMeters < 50f) car.distanceMeters = totalWorldLengthMeters - 100f
            } else {
                car.distanceMeters += moveDelta
                if (car.distanceMeters > totalWorldLengthMeters - 100f) car.distanceMeters = 50f
            }

            // Anti-collision / Overtake check with player
            val distDiff = car.distanceMeters - distanceTravelledMeters
            if (abs(distDiff) < 14f && abs(car.playerX - playerX) < 0.45f) {
                // Scrape collision with AI car
                currentSpeedKmh = max(0f, currentSpeedKmh - 25f)
                cameraShake = 0.4f
            }
        }
    }

    fun getCurrentZone(): WorldZone {
        return track.findSegment(distanceTravelledMeters).zone
    }

    fun cycleVehicle() {
        val nextIdx = (currentVehicleType.ordinal + 1) % VehicleType.values().size
        applyVehicleSpecs(VehicleType.values()[nextIdx])
    }

    fun cycleCamera() {
        val nextIdx = (cameraMode.ordinal + 1) % CameraMode.values().size
        cameraMode = CameraMode.values()[nextIdx]
    }

    fun cycleWeather() {
        val nextIdx = (currentWeather.ordinal + 1) % WeatherType.values().size
        currentWeather = WeatherType.values()[nextIdx]
    }

    fun cycleGameMode() {
        val nextIdx = (activeGameMode.ordinal + 1) % GameMode.values().size
        activeGameMode = GameMode.values()[nextIdx]
        missionElapsedTime = 0f
        checkpointsPassed = 0
        missionScore = 1000
        isMissionCompleted = false
    }

    fun cycleQuality() {
        val nextIdx = (currentQuality.ordinal + 1) % QualityLevel.values().size
        currentQuality = QualityLevel.values()[nextIdx]
    }

    fun toggle4WD() {
        driveType = if (driveType == DriveType.LOW_RANGE_4WD) DriveType.AWD_4WD else DriveType.LOW_RANGE_4WD
    }

    fun toggleMute() {
        soundEngine.isMuted = !soundEngine.isMuted
    }

    fun resetVehicle() {
        currentSpeedKmh = 0.0f
        currentRPM = idleRPM
        currentGear = 1
        distanceTravelledMeters = 0.0f
        playerX = 0.35f
        isHandbrakeActive = false
        missionElapsedTime = 0f
        isMissionCompleted = false
        checkpointsPassed = 0
    }
}
