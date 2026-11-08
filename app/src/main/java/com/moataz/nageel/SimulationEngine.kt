package com.moataz.nageel

import kotlin.math.*

/**
 * Nageel Mountain Pass Simulation & Physics Engine — Phase 2
 * Implements comprehensive multi-vehicle mountain dynamics:
 * - 3 Distinct Vehicles: SUV 4WD (2,150 kg), Sedan (1,480 kg), Pickup Shas (1,950 kg)
 * - 10.5 km Continuous World Network across 3 Connected Zones:
 *     1. Naqil Mountain Pass (0 to 3,500m) - Steep asphalt climb & hairpins
 *     2. Green Highland Terraces (3,500 to 7,000m) - Agricultural terraces & stone villages
 *     3. Rocky Wadi Valley (7,000 to 10,500m) - Gravel canyon track & boulders
 * - 4 Selectable Camera Modes (Chase Close, Chase Far, Cockpit Interior, Hood/Bumper)
 * - Dynamic Day/Night Cycle & 4 Weather Types (Clear, Highland Fog, Rain, Wadi Dust)
 * - Smart AI Traffic Vehicles with Curve Slowdown & Anti-Collision
 * - 3 Game Modes (Free Roam, Delivery Mission, Hill Climb Time Attack)
 * - Quality Profiles (Low 30 FPS, Medium Balanced, High 60 FPS)
 * - Cloud & Local Data Persistence Schemas (Zero hardcoded secrets, Neon OAuth ready)
 */
class SimulationEngine {

    enum class VehicleType(val arabicName: String, val massKg: Float, val torqueNm: Float, val peakRPM: Float) {
        SUV_4WD("لاندكروزر 4WD جبلية", 2150.0f, 480.0f, 3200.0f),
        SEDAN("سيدان سياحية رياضية", 1480.0f, 320.0f, 3800.0f),
        PICKUP_SHAS("شاص 4WD للأحمال الصعبة", 1950.0f, 440.0f, 3000.0f)
    }

    enum class DriveType { AWD_4WD, RWD_2WD, LOW_RANGE_4WD }
    enum class CameraMode(val arabicName: String) {
        CHASE_CLOSE("مطاردة قريبة"),
        CHASE_FAR("مطاردة بعيدة / بانوراما"),
        COCKPIT("مقصورة القيادة الداخلية"),
        HOOD_BUMPER("غطاء المحرك / سينمائية")
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
        WADI_DUST("عواصف رملية بالوادي")
    }

    enum class GameMode(val arabicName: String) {
        FREE_ROAM("القيادة الحرة"),
        DELIVERY("رحلات الوصول"),
        HILL_CLIMB("التحدي الجبلي")
    }

    enum class QualityLevel(val arabicName: String, val targetFps: Int) {
        LOW("منخفضة (30 FPS)", 30),
        MEDIUM("متوسطة (متوازنة)", 45),
        HIGH("عالية (60 FPS)", 60)
    }

    // AI Traffic Car Data
    data class AITraffic(
        var distanceMeters: Float,
        var speedKmh: Float,
        val isOncoming: Boolean,
        val carTypeIndex: Int
    )

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

    var slopePitch = 12.0f // Incline gradient degrees
    var slopeRoll = 3.0f

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

    // World Navigation (Total length 10.5 km)
    var distanceTravelledMeters = 0.0f
    val totalWorldLengthMeters = 10500.0f

    // Game Mode Telemetry
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
        trafficCars.add(AITraffic(350f, 50f, isOncoming = true, carTypeIndex = 0))
        trafficCars.add(AITraffic(700f, 65f, isOncoming = false, carTypeIndex = 1))
        trafficCars.add(AITraffic(1200f, 45f, isOncoming = true, carTypeIndex = 2))
        trafficCars.add(AITraffic(1850f, 55f, isOncoming = false, carTypeIndex = 0))
        trafficCars.add(AITraffic(2600f, 40f, isOncoming = true, carTypeIndex = 1))
    }

    fun update(deltaTime: Float) {
        val dt = deltaTime.coerceIn(0.001f, 0.05f)

        // Advance Day/Night time (1 sec = 12 sec game time)
        timeOfDayHours = (timeOfDayHours + (dt * 12f) / 3600f * 24f) % 24f
        areHeadlightsOn = timeOfDayHours < 6f || timeOfDayHours > 18.5f || currentWeather == WeatherType.MOUNTAIN_RAIN

        // 1. Calculate Active Zone & Surface Grip
        val currentZone = getCurrentZone()
        val surfaceGrip = when (currentWeather) {
            WeatherType.MOUNTAIN_RAIN -> currentZone.friction * 0.82f
            WeatherType.WADI_DUST -> currentZone.friction * 0.75f
            else -> currentZone.friction
        }

        // Adjust slope pitch based on zone altitude profile
        slopePitch = when (currentZone) {
            WorldZone.NAQIL_PASS -> {
                // Steep climb 10 to 18 degrees with hairpin crests
                val t = distanceTravelledMeters / 3500f
                12f + sin(t * Math.PI.toFloat() * 6f) * 5f
            }
            WorldZone.GREEN_HIGHLANDS -> {
                // High rolling plateau 3 to 7 degrees
                val t = (distanceTravelledMeters - 3500f) / 3500f
                4f + sin(t * Math.PI.toFloat() * 4f) * 3f
            }
            WorldZone.ROCKY_WADI -> {
                // Valley descent -6 to -12 degrees
                val t = (distanceTravelledMeters - 7000f) / 3500f
                -8f + sin(t * Math.PI.toFloat() * 5f) * 4f
            }
        }

        // 2. Engine RPM Simulation
        val activeGearRatio = if (currentGear > 0) forwardGearRatios[currentGear - 1] else reverseGearRatio
        val lowMultiplier = if (driveType == DriveType.LOW_RANGE_4WD) lowRangeMultiplier else 1.0f

        val targetRPM = if (currentGear > 0) {
            val ratio = activeGearRatio * finalDriveRatio * lowMultiplier
            val wheelRpm = (currentSpeedKmh / 3.6f) / (2.0f * Math.PI.toFloat() * 0.42f) * 60.0f
            max(idleRPM, wheelRpm * ratio + (throttleInput * 1200f))
        } else {
            idleRPM + (throttleInput * (maxRPM - idleRPM))
        }

        currentRPM = currentRPM + (targetRPM.coerceIn(idleRPM, maxRPM) - currentRPM) * (dt * 6.0f)

        // Automatic gear shifting
        if (currentRPM > 4900f && currentGear < forwardGearRatios.size) {
            currentGear++
        } else if (currentRPM < 1950f && currentGear > 1) {
            currentGear--
        }

        // 3. Torque & Acceleration Forces
        val rpmFactor = (1.0f - abs(currentRPM - currentVehicleType.peakRPM) / (maxRPM - idleRPM)).coerceIn(0.45f, 1.0f)
        val driveTorque = throttleInput * currentVehicleType.torqueNm * rpmFactor * activeGearRatio * finalDriveRatio * lowMultiplier
        val driveForce = (driveTorque / 0.42f) * surfaceGrip

        // Resistances
        val gravityForce = currentVehicleType.massKg * 9.81f * sin(Math.toRadians(slopePitch.toDouble())).toFloat()
        val rollingResistance = currentVehicleType.massKg * 9.81f * (0.015f / surfaceGrip)
        val aeroDrag = 0.5f * 1.225f * 0.45f * 2.6f * (currentSpeedKmh / 3.6f).pow(2)

        // Braking Forces
        val footBrakeForce = brakeInput * 8800.0f * surfaceGrip
        val handBrakeForce = if (isHandbrakeActive) 14500.0f else 0.0f
        val totalBrake = footBrakeForce + handBrakeForce

        var netForce = driveForce - gravityForce - rollingResistance - aeroDrag

        // Engine compression braking on steep descents
        if (throttleInput < 0.05f && currentSpeedKmh > 5f && currentGear in 1..2) {
            netForce -= (3f - currentGear) * 1250f
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
        currentSpeedKmh = max(0.0f, currentSpeedMs * 3.6f)

        distanceTravelledMeters += (currentSpeedKmh / 3.6f) * dt
        if (distanceTravelledMeters > totalWorldLengthMeters) {
            distanceTravelledMeters = 0.0f // Seamless loop
        }

        // 4. Update Mission Telemetry
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

        // 5. Update AI Traffic along the road
        updateAITraffic(dt)
    }

    private fun updateAITraffic(dt: Float) {
        for (car in trafficCars) {
            // AI slows down on sharp turns (simulated around 1,200m, 2,800m, 5,500m, 8,200m)
            val isNearHairpin = abs(car.distanceMeters - 1200f) < 150f ||
                    abs(car.distanceMeters - 2800f) < 150f ||
                    abs(car.distanceMeters - 5500f) < 150f ||
                    abs(car.distanceMeters - 8200f) < 150f

            val targetSpeed = if (isNearHairpin) 28f else 58f
            car.speedKmh += (targetSpeed - car.speedKmh) * (dt * 2f)

            val moveDelta = (car.speedKmh / 3.6f) * dt
            if (car.isOncoming) {
                car.distanceMeters -= moveDelta
                if (car.distanceMeters < 50f) car.distanceMeters = totalWorldLengthMeters - 100f
            } else {
                car.distanceMeters += moveDelta
                if (car.distanceMeters > totalWorldLengthMeters - 100f) car.distanceMeters = 50f
            }
        }
    }

    fun getCurrentZone(): WorldZone {
        return when {
            distanceTravelledMeters < 3500f -> WorldZone.NAQIL_PASS
            distanceTravelledMeters < 7000f -> WorldZone.GREEN_HIGHLANDS
            else -> WorldZone.ROCKY_WADI
        }
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

    fun resetVehicle() {
        currentSpeedKmh = 0.0f
        currentRPM = idleRPM
        currentGear = 1
        distanceTravelledMeters = 0.0f
        isHandbrakeActive = false
        missionElapsedTime = 0f
        isMissionCompleted = false
        checkpointsPassed = 0
    }
}
