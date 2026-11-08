package com.moataz.nageel

import android.annotation.SuppressLint
import android.content.Context
import android.graphics.*
import android.os.Bundle
import android.view.MotionEvent
import android.view.View
import android.view.Window
import android.view.WindowManager
import androidx.appcompat.app.AppCompatActivity
import kotlin.math.*

class MainActivity : AppCompatActivity() {

    private lateinit var gameView: GameRenderView

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        // Fullscreen immersive mode for mobile driving experience
        requestWindowFeature(Window.FEATURE_NO_TITLE)
        window.setFlags(
            WindowManager.LayoutParams.FLAG_FULLSCREEN,
            WindowManager.LayoutParams.FLAG_FULLSCREEN
        )
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)

        gameView = GameRenderView(this)
        setContentView(gameView)
    }

    override fun onResume() {
        super.onResume()
        gameView.resume()
    }

    override fun onPause() {
        super.onPause()
        gameView.pause()
    }

    @SuppressLint("ViewConstructor")
    class GameRenderView(context: Context) : View(context), Runnable {

        private val engine = SimulationEngine()
        private var isRunning = false
        private var gameThread: Thread? = null

        // PBR & Visual Paints
        private val skyPaint = Paint()
        private val mountainPaint = Paint()
        private val terraceGreenPaint = Paint().apply { color = Color.rgb(46, 125, 50) }
        private val canyonRockPaint = Paint().apply { color = Color.rgb(180, 110, 75) }
        private val roadPaint = Paint()
        private val roadLinePaint = Paint().apply {
            color = Color.rgb(250, 204, 21)
            strokeWidth = 6f
            style = Paint.Style.STROKE
        }
        private val barrierPaint = Paint().apply { color = Color.rgb(203, 213, 225) }
        private val headlightBeamPaint = Paint().apply {
            color = Color.argb(85, 255, 250, 200)
            style = Paint.Style.FILL
        }

        // Vehicle Paints
        private val carSuvPaint = Paint().apply { color = Color.rgb(235, 228, 215) }      // Desert Sand Land Cruiser
        private val carSedanPaint = Paint().apply { color = Color.rgb(37, 99, 235) }       // Royal Blue Sedan
        private val carPickupPaint = Paint().apply { color = Color.rgb(248, 250, 252) }    // White Yemeni Shas
        private val carWindowPaint = Paint().apply { color = Color.rgb(30, 41, 59) }
        private val wheelPaint = Paint().apply { color = Color.rgb(24, 24, 27) }
        private val wheelRimPaint = Paint().apply { color = Color.rgb(156, 163, 175) }
        private val bullbarPaint = Paint().apply { color = Color.rgb(51, 65, 85) }
        private val taillightPaint = Paint().apply { color = Color.rgb(239, 68, 68) }

        // HUD Paints
        private val hudBgPaint = Paint().apply { color = Color.argb(205, 15, 23, 42) }
        private val hudTextPaint = Paint().apply {
            color = Color.WHITE
            textSize = 30f
            isAntiAlias = true
            typeface = Typeface.DEFAULT_BOLD
        }
        private val hudGoldPaint = Paint().apply {
            color = Color.rgb(245, 158, 11)
            textSize = 38f
            isAntiAlias = true
            typeface = Typeface.DEFAULT_BOLD
        }
        private val hudSmallPaint = Paint().apply {
            color = Color.rgb(203, 213, 225)
            textSize = 22f
            isAntiAlias = true
        }
        private val controlBtnPaint = Paint().apply {
            color = Color.argb(165, 30, 41, 59)
            style = Paint.Style.FILL
        }
        private val controlBtnActivePaint = Paint().apply {
            color = Color.argb(230, 245, 158, 11)
            style = Paint.Style.FILL
        }

        // Touch Control States
        private var steerLeftPressed = false
        private var steerRightPressed = false
        private var gasPressed = false
        private var brakePressed = false

        init {
            isFocusable = true
        }

        fun resume() {
            isRunning = true
            gameThread = Thread(this).apply { start() }
        }

        fun pause() {
            isRunning = false
            try {
                gameThread?.join()
            } catch (e: InterruptedException) {
                e.printStackTrace()
            }
        }

        override fun run() {
            var lastTime = System.nanoTime()
            while (isRunning) {
                val now = System.nanoTime()
                val delta = ((now - lastTime) / 1_000_000_000.0).toFloat()
                lastTime = now

                // Forward inputs into physics simulation engine
                engine.steerInput = when {
                    steerLeftPressed -> -1.0f
                    steerRightPressed -> 1.0f
                    else -> 0.0f
                }
                engine.throttleInput = if (gasPressed) 1.0f else 0.0f
                engine.brakeInput = if (brakePressed) 1.0f else 0.0f

                engine.update(delta)

                postInvalidate()

                try {
                    val frameSleep = when (engine.currentQuality) {
                        SimulationEngine.QualityLevel.LOW -> 32L // ~30 FPS
                        SimulationEngine.QualityLevel.MEDIUM -> 22L // ~45 FPS
                        SimulationEngine.QualityLevel.HIGH -> 16L // ~60 FPS
                    }
                    Thread.sleep(frameSleep)
                } catch (e: InterruptedException) {
                    break
                }
            }
        }

        @SuppressLint("DrawAllocation")
        override fun onDraw(canvas: Canvas) {
            super.onDraw(canvas)
            val w = width.toFloat()
            val h = height.toFloat()
            if (w <= 0 || h <= 0) return

            val currentZone = engine.getCurrentZone()

            // 1. Draw Dynamic Sky (Day / Sunset / Night / Fog)
            drawSkyAndAtmosphere(canvas, w, h, currentZone)

            // 2. Draw Mountains, Terraces, or Canyon Cliffs based on Active Zone
            drawEnvironmentScenery(canvas, w, h, currentZone)

            // 3. Draw Road Network (Asphalt in Zone 1/2 vs Gravel Dirt in Zone 3)
            drawRoad(canvas, w, h, currentZone)

            // 4. Draw AI Traffic Ahead on the Road
            drawAITraffic(canvas, w, h)

            // 5. Draw Player Vehicle according to active Camera Mode
            when (engine.cameraMode) {
                SimulationEngine.CameraMode.CHASE_CLOSE -> drawChaseVehicle(canvas, w * 0.50f, h * 0.74f, 1.0f)
                SimulationEngine.CameraMode.CHASE_FAR -> drawChaseVehicle(canvas, w * 0.50f, h * 0.70f, 0.75f)
                SimulationEngine.CameraMode.COCKPIT -> drawCockpitInterior(canvas, w, h)
                SimulationEngine.CameraMode.HOOD_BUMPER -> drawHoodView(canvas, w, h)
            }

            // 6. Draw Weather Particles (Raindrops or Wadi Dust)
            drawWeatherParticles(canvas, w, h)

            // 7. Draw Arabic HUD & Dashboard Telemetry
            drawComprehensiveHUD(canvas, w, h, currentZone)

            // 8. Draw Touch Controls & Quick-Action Switchers
            drawTouchControls(canvas, w, h)
        }

        private fun drawSkyAndAtmosphere(canvas: Canvas, w: Float, h: Float, zone: SimulationEngine.WorldZone) {
            val hour = engine.timeOfDayHours

            // Dynamic sky gradient based on time of day
            when {
                engine.currentWeather == SimulationEngine.WeatherType.MOUNTAIN_RAIN -> {
                    skyPaint.color = Color.rgb(90, 100, 115) // Overcast storm
                }
                engine.currentWeather == SimulationEngine.WeatherType.HIGHLAND_FOG -> {
                    skyPaint.color = Color.rgb(195, 205, 215) // Misty Highland fog
                }
                hour in 6.0f..8.0f -> {
                    skyPaint.color = Color.rgb(240, 180, 140) // Amber Dawn
                }
                hour in 8.0f..17.0f -> {
                    skyPaint.color = Color.rgb(125, 175, 225) // Clear Blue Mountain Sky
                }
                hour in 17.0f..19.5f -> {
                    skyPaint.color = Color.rgb(235, 120, 60)  // Fiery Yemen Mountain Sunset
                }
                else -> {
                    skyPaint.color = Color.rgb(15, 23, 42)    // Night Sky
                }
            }

            canvas.drawRect(0f, 0f, w, h * 0.48f, skyPaint)

            // Stars at Night
            if (hour < 5.5f || hour > 19.5f) {
                val starPaint = Paint().apply { color = Color.WHITE }
                for (i in 0..30) {
                    val sx = (sin(i.toDouble() * 37.0) * 0.5 + 0.5).toFloat() * w
                    val sy = (cos(i.toDouble() * 19.0) * 0.5 + 0.5).toFloat() * (h * 0.35f)
                    canvas.drawCircle(sx, sy, 2.5f, starPaint)
                }
            }
        }

        private fun drawEnvironmentScenery(canvas: Canvas, w: Float, h: Float, zone: SimulationEngine.WorldZone) {
            when (zone) {
                SimulationEngine.WorldZone.NAQIL_PASS -> {
                    // Jagged Basalt Mountain Ridges & Yemeni Watchtowers
                    mountainPaint.color = Color.rgb(105, 88, 72)
                    val passPeaks = Path().apply {
                        moveTo(0f, h * 0.48f)
                        lineTo(w * 0.18f, h * 0.20f)
                        lineTo(w * 0.40f, h * 0.35f)
                        lineTo(w * 0.65f, h * 0.16f)
                        lineTo(w * 0.88f, h * 0.38f)
                        lineTo(w, h * 0.25f)
                        lineTo(w, h * 0.55f)
                        lineTo(0f, h * 0.55f)
                        close()
                    }
                    canvas.drawPath(passPeaks, mountainPaint)

                    // Overlooking Yemeni Stone Watchtower on Peak
                    val towerPaint = Paint().apply { color = Color.rgb(85, 72, 60) }
                    canvas.drawRect(w * 0.62f, h * 0.11f, w * 0.68f, h * 0.17f, towerPaint)
                }

                SimulationEngine.WorldZone.GREEN_HIGHLANDS -> {
                    // Lush Green Terraced Slopes & Traditional Yemeni Villages
                    val terracePath = Path().apply {
                        moveTo(0f, h * 0.48f)
                        lineTo(w * 0.25f, h * 0.28f)
                        lineTo(w * 0.55f, h * 0.36f)
                        lineTo(w * 0.80f, h * 0.24f)
                        lineTo(w, h * 0.38f)
                        lineTo(w, h * 0.55f)
                        lineTo(0f, h * 0.55f)
                        close()
                    }
                    canvas.drawPath(terracePath, terraceGreenPaint)

                    // Tiered Stone Terrace Strips
                    val wallPaint = Paint().apply {
                        color = Color.rgb(130, 115, 95)
                        strokeWidth = 5f
                    }
                    canvas.drawLine(w * 0.05f, h * 0.42f, w * 0.35f, h * 0.38f, wallPaint)
                    canvas.drawLine(w * 0.60f, h * 0.44f, w * 0.90f, h * 0.36f, wallPaint)

                    // Yemeni Multi-tier Tower House in Village
                    val housePaint = Paint().apply { color = Color.rgb(160, 135, 110) }
                    val whiteTrim = Paint().apply { color = Color.WHITE }
                    canvas.drawRect(w * 0.72f, h * 0.22f, w * 0.79f, h * 0.36f, housePaint)
                    canvas.drawRect(w * 0.72f, h * 0.22f, w * 0.79f, h * 0.24f, whiteTrim) // Gypsum top trim
                }

                SimulationEngine.WorldZone.ROCKY_WADI -> {
                    // Arid Canyon Red Rock Walls & Natural Boulders
                    val canyonPath = Path().apply {
                        moveTo(0f, h * 0.48f)
                        lineTo(w * 0.15f, h * 0.15f)
                        lineTo(w * 0.35f, h * 0.30f)
                        lineTo(w * 0.60f, h * 0.22f)
                        lineTo(w * 0.85f, h * 0.14f)
                        lineTo(w, h * 0.35f)
                        lineTo(w, h * 0.55f)
                        lineTo(0f, h * 0.55f)
                        close()
                    }
                    canvas.drawPath(canyonPath, canyonRockPaint)

                    // Natural Wadi Boulders along the gorge
                    val boulderPaint = Paint().apply { color = Color.rgb(115, 80, 55) }
                    canvas.drawOval(w * 0.12f, h * 0.52f, w * 0.24f, h * 0.58f, boulderPaint)
                    canvas.drawOval(w * 0.76f, h * 0.50f, w * 0.86f, h * 0.56f, boulderPaint)
                }
            }
        }

        private fun drawRoad(canvas: Canvas, w: Float, h: Float, zone: SimulationEngine.WorldZone) {
            val isWadi = (zone == SimulationEngine.WorldZone.ROCKY_WADI)

            roadPaint.color = if (isWadi) Color.rgb(165, 135, 105) else Color.rgb(45, 48, 55)

            // Dynamic Road Curve vanishing perspective influenced by steering
            val curveOffset = -engine.steerInput * (w * 0.08f)
            val roadTopLeft = (w * 0.46f) + curveOffset
            val roadTopRight = (w * 0.54f) + curveOffset

            val roadPath = Path().apply {
                moveTo(roadTopLeft, h * 0.48f)
                lineTo(roadTopRight, h * 0.48f)
                lineTo(w * 0.94f, h)
                lineTo(w * 0.06f, h)
                close()
            }
            canvas.drawPath(roadPath, roadPaint)

            if (!isWadi) {
                // Highway Yellow Center Line
                val midTop = (roadTopLeft + roadTopRight) * 0.5f
                canvas.drawLine(midTop, h * 0.48f, w * 0.50f, h, roadLinePaint)

                // Safety Guardrails on Cliff Side (Zone 1 & 2)
                canvas.drawLine(roadTopLeft, h * 0.48f, w * 0.06f, h, barrierPaint)
            } else {
                // Wadi Dirt Wheel Tracks
                val trackPaint = Paint().apply {
                    color = Color.rgb(130, 105, 80)
                    strokeWidth = 8f
                    style = Paint.Style.STROKE
                }
                canvas.drawLine((roadTopLeft + roadTopRight) * 0.45f, h * 0.48f, w * 0.35f, h, trackPaint)
                canvas.drawLine((roadTopLeft + roadTopRight) * 0.55f, h * 0.48f, w * 0.65f, h, trackPaint)
            }

            // Headlight beams illuminating the road in the dark
            if (engine.areHeadlightsOn) {
                val beamPath = Path().apply {
                    moveTo(w * 0.38f, h * 0.70f)
                    lineTo(roadTopLeft, h * 0.48f)
                    lineTo(roadTopRight, h * 0.48f)
                    lineTo(w * 0.62f, h * 0.70f)
                    close()
                }
                canvas.drawPath(beamPath, headlightBeamPaint)
            }
        }

        private fun drawAITraffic(canvas: Canvas, w: Float, h: Float) {
            val trafficPaint = Paint().apply { color = Color.rgb(234, 179, 8) } // Mountain Taxi Yellow

            for (car in engine.trafficCars) {
                val deltaDist = car.distanceMeters - engine.distanceTravelledMeters
                // Only render if visible in forward distance window (0 to 350 meters)
                if (deltaDist in 15f..350f) {
                    val perspective = 1.0f - (deltaDist / 350f)
                    val carY = (h * 0.48f) + (h * 0.28f) * (1.0f - perspective)
                    val laneSide = if (car.isOncoming) -1f else 1f
                    val carX = (w * 0.50f) + (laneSide * (w * 0.18f) * (1.0f - perspective * 0.7f))

                    val carW = 40f * (1.0f - perspective * 0.7f)
                    val carH = 22f * (1.0f - perspective * 0.7f)

                    canvas.drawRect(carX - carW * 0.5f, carY - carH, carX + carW * 0.5f, carY, trafficPaint)
                }
            }
        }

        private fun drawChaseVehicle(canvas: Canvas, cx: Float, cy: Float, scale: Float) {
            val s = scale
            val steerAngle = engine.steerInput * 18f

            when (engine.currentVehicleType) {
                SimulationEngine.VehicleType.SUV_4WD -> {
                    // 1. Yemeni 4WD Mountain SUV (Land Cruiser)
                    // Rugged Wheels
                    canvas.drawRoundRect(cx - (110f * s), cy + (40f * s), cx - (80f * s), cy + (100f * s), 8f, 8f, wheelPaint)
                    canvas.drawRoundRect(cx + (80f * s), cy + (40f * s), cx + (110f * s), cy + (100f * s), 8f, 8f, wheelPaint)

                    // 4WD Wide Body Chassis
                    canvas.drawRoundRect(cx - (98f * s), cy - (20f * s), cx + (98f * s), cy + (60f * s), 12f * s, 12f * s, carSuvPaint)

                    // High Cabin & Tinted Windows
                    canvas.drawRoundRect(cx - (82f * s), cy - (85f * s), cx + (82f * s), cy - (20f * s), 14f * s, 14f * s, carSuvPaint)
                    canvas.drawRoundRect(cx - (72f * s), cy - (75f * s), cx + (72f * s), cy - (25f * s), 8f * s, 8f * s, carWindowPaint)

                    // Heavy Duty Roof Carrier Rack
                    canvas.drawRect(cx - (85f * s), cy - (96f * s), cx + (85f * s), cy - (88f * s), bullbarPaint)

                    // Taillights
                    canvas.drawRect(cx - (95f * s), cy + (5f * s), cx - (78f * s), cy + (25f * s), taillightPaint)
                    canvas.drawRect(cx + (78f * s), cy + (5f * s), cx + (95f * s), cy + (25f * s), taillightPaint)
                }

                SimulationEngine.VehicleType.SEDAN -> {
                    // 2. Mountain Touring Sedan (Lower, Sleek, Aerodynamic)
                    // Low-profile Alloy Wheels
                    canvas.drawRoundRect(cx - (105f * s), cy + (48f * s), cx - (80f * s), cy + (95f * s), 8f, 8f, wheelPaint)
                    canvas.drawRoundRect(cx + (80f * s), cy + (48f * s), cx + (105f * s), cy + (95f * s), 8f, 8f, wheelPaint)

                    // Sleek Low Chassis
                    canvas.drawRoundRect(cx - (92f * s), cy - (10f * s), cx + (92f * s), cy + (58f * s), 16f * s, 16f * s, carSedanPaint)

                    // Curved Aerodynamic Cabin
                    canvas.drawRoundRect(cx - (76f * s), cy - (65f * s), cx + (76f * s), cy - (10f * s), 20f * s, 20f * s, carSedanPaint)
                    canvas.drawRoundRect(cx - (68f * s), cy - (58f * s), cx + (68f * s), cy - (15f * s), 12f * s, 12f * s, carWindowPaint)

                    // Rear Lip Spoiler
                    canvas.drawRect(cx - (82f * s), cy - (16f * s), cx + (82f * s), cy - (10f * s), bullbarPaint)

                    // Horizontal LED Taillight Strip
                    canvas.drawRect(cx - (90f * s), cy + (8f * s), cx + (90f * s), cy + (20f * s), taillightPaint)
                }

                SimulationEngine.VehicleType.PICKUP_SHAS -> {
                    // 3. Yemeni Shas Pickup (High Clearance, Roll Bar, Open Bed)
                    // Mud Terrain Wheels
                    canvas.drawRoundRect(cx - (112f * s), cy + (40f * s), cx - (78f * s), cy + (105f * s), 8f, 8f, wheelPaint)
                    canvas.drawRoundRect(cx + (78f * s), cy + (40f * s), cx + (112f * s), cy + (105f * s), 8f, 8f, wheelPaint)

                    // Heavy Truck Bed & Body
                    canvas.drawRoundRect(cx - (96f * s), cy - (20f * s), cx + (96f * s), cy + (65f * s), 8f * s, 8f * s, carPickupPaint)

                    // Single Cab
                    canvas.drawRoundRect(cx - (80f * s), cy - (88f * s), cx + (80f * s), cy - (20f * s), 10f * s, 10f * s, carPickupPaint)
                    canvas.drawRoundRect(cx - (70f * s), cy - (78f * s), cx + (70f * s), cy - (25f * s), 6f * s, 6f * s, carWindowPaint)

                    // Tubular Heavy-Duty Roll Bar
                    val rollBarPaint = Paint().apply {
                        color = Color.rgb(30, 41, 59)
                        strokeWidth = 8f * s
                        style = Paint.Style.STROKE
                    }
                    canvas.drawRect(cx - (76f * s), cy - (96f * s), cx + (76f * s), cy - (25f * s), rollBarPaint)

                    // Rear Mudflaps & Taillights
                    canvas.drawRect(cx - (94f * s), cy + (8f * s), cx - (76f * s), cy + (26f * s), taillightPaint)
                    canvas.drawRect(cx + (76f * s), cy + (8f * s), cx + (94f * s), cy + (26f * s), taillightPaint)
                }
            }
        }

        private fun drawCockpitInterior(canvas: Canvas, w: Float, h: Float) {
            // Hood & Windshield Framing
            val hoodPaint = Paint().apply {
                color = when (engine.currentVehicleType) {
                    SimulationEngine.VehicleType.SUV_4WD -> Color.rgb(215, 205, 190)
                    SimulationEngine.VehicleType.SEDAN -> Color.rgb(30, 64, 175)
                    SimulationEngine.VehicleType.PICKUP_SHAS -> Color.rgb(230, 230, 230)
                }
            }

            val hoodPath = Path().apply {
                moveTo(w * 0.16f, h)
                lineTo(w * 0.32f, h * 0.62f)
                lineTo(w * 0.68f, h * 0.62f)
                lineTo(w * 0.84f, h)
                close()
            }
            canvas.drawPath(hoodPath, hoodPaint)

            // Windshield Pillars (A-Pillars)
            val pillarPaint = Paint().apply { color = Color.rgb(24, 28, 36) }
            canvas.drawRect(0f, 0f, w * 0.12f, h, pillarPaint)
            canvas.drawRect(w * 0.88f, 0f, w, h, pillarPaint)

            // Dashboard & Instrument Cluster Console
            val dashPaint = Paint().apply { color = Color.rgb(18, 22, 28) }
            canvas.drawRect(0f, h * 0.78f, w, h, dashPaint)

            // Animated Responsive Steering Wheel
            val steerAngle = engine.steerInput * 45f
            val steerX = w * 0.32f + (engine.steerInput * 35f)
            val steerY = h * 0.87f

            val rimPaint = Paint().apply {
                color = Color.rgb(45, 52, 64)
                style = Paint.Style.STROKE
                strokeWidth = 24f
                isAntiAlias = true
            }
            canvas.drawCircle(steerX, steerY, 115f, rimPaint)

            // Steering Spokes
            val spokePaint = Paint().apply {
                color = Color.rgb(71, 85, 105)
                strokeWidth = 14f
            }
            canvas.drawLine(steerX - 110f, steerY, steerX + 110f, steerY, spokePaint)
            canvas.drawLine(steerX, steerY, steerX, steerY + 110f, spokePaint)

            // Cockpit Analog Speedometer & Tachometer Dials
            drawCockpitDials(canvas, w * 0.58f, h * 0.88f)
        }

        private fun drawCockpitDials(canvas: Canvas, cx: Float, cy: Float) {
            val dialBg = Paint().apply { color = Color.rgb(10, 14, 20) }
            val dialBorder = Paint().apply {
                color = Color.rgb(100, 116, 139)
                style = Paint.Style.STROKE
                strokeWidth = 4f
            }
            val needlePaint = Paint().apply {
                color = Color.rgb(239, 68, 68)
                strokeWidth = 5f
            }

            // Speedometer Dial
            canvas.drawCircle(cx, cy, 60f, dialBg)
            canvas.drawCircle(cx, cy, 60f, dialBorder)
            val speedAngle = Math.toRadians((135.0 - (engine.currentSpeedKmh / 180.0) * 270.0)).toFloat()
            canvas.drawLine(cx, cy, cx + cos(speedAngle) * 50f, cy - sin(speedAngle) * 50f, needlePaint)

            // Tachometer Dial
            canvas.drawCircle(cx + 140f, cy, 60f, dialBg)
            canvas.drawCircle(cx + 140f, cy, 60f, dialBorder)
            val rpmAngle = Math.toRadians((135.0 - (engine.currentRPM / engine.maxRPM) * 270.0)).toFloat()
            canvas.drawLine(cx + 140f, cy, cx + 140f + cos(rpmAngle) * 50f, cy - sin(rpmAngle) * 50f, needlePaint)
        }

        private fun drawHoodView(canvas: Canvas, w: Float, h: Float) {
            // Low Bumper View: Minimal nose frame for maximum road speed sensation
            val nosePaint = Paint().apply { color = Color.rgb(30, 41, 59) }
            canvas.drawRect(w * 0.25f, h * 0.94f, w * 0.75f, h, nosePaint)
        }

        private fun drawWeatherParticles(canvas: Canvas, w: Float, h: Float) {
            when (engine.currentWeather) {
                SimulationEngine.WeatherType.MOUNTAIN_RAIN -> {
                    val rainPaint = Paint().apply {
                        color = Color.argb(170, 200, 225, 255)
                        strokeWidth = 3f
                    }
                    for (i in 0..40) {
                        val rx = (sin(i * 47.0 + engine.distanceTravelledMeters * 0.1) * 0.5 + 0.5).toFloat() * w
                        val ry = ((i * 31 + System.currentTimeMillis() / 2) % h.toInt()).toFloat()
                        canvas.drawLine(rx, ry, rx - 10f, ry + 28f, rainPaint)
                    }
                }
                SimulationEngine.WeatherType.WADI_DUST -> {
                    val dustPaint = Paint().apply {
                        color = Color.argb(90, 217, 180, 130)
                    }
                    for (i in 0..25) {
                        val dx = (sin(i * 19.0) * 0.5 + 0.5).toFloat() * w
                        val dy = (h * 0.55f) + (cos(i * 13.0) * 0.5 + 0.5).toFloat() * (h * 0.40f)
                        canvas.drawCircle(dx, dy, 18f + (i % 12), dustPaint)
                    }
                }
                else -> {}
            }
        }

        private fun drawComprehensiveHUD(canvas: Canvas, w: Float, h: Float, zone: SimulationEngine.WorldZone) {
            // 1. Top Status Banner: Active Zone, Altitude & Road Condition
            canvas.drawRoundRect(w * 0.15f, 15f, w * 0.85f, 75f, 16f, 16f, hudBgPaint)
            val altitudeMeters = (2400f + (engine.distanceTravelledMeters / 10500f) * 450f).toInt()
            val bannerTitle = "${zone.arabicName} — الارتفاع: ${altitudeMeters}م — ${engine.currentVehicleType.arabicName}"
            hudTextPaint.textAlign = Paint.Align.CENTER
            canvas.drawText(bannerTitle, w * 0.50f, 56f, hudTextPaint)

            // 2. Left HUD Panel: Speedometer, RPM & Gear
            canvas.drawRoundRect(25f, 25f, 310f, 240f, 20f, 20f, hudBgPaint)
            hudGoldPaint.textAlign = Paint.Align.LEFT
            canvas.drawText("${round(engine.currentSpeedKmh).toInt()}", 50f, 90f, hudGoldPaint)
            hudTextPaint.textAlign = Paint.Align.LEFT
            canvas.drawText("كم/س", 145f, 88f, hudTextPaint)

            hudGoldPaint.textSize = 30f
            canvas.drawText("${round(engine.currentRPM).toInt()}", 50f, 145f, hudGoldPaint)
            hudSmallPaint.textAlign = Paint.Align.LEFT
            canvas.drawText("دورة/دقيقة", 160f, 142f, hudSmallPaint)

            val gearStr = if (engine.currentGear == -1) "R" else "D${engine.currentGear}"
            val driveStr = if (engine.driveType == SimulationEngine.DriveType.LOW_RANGE_4WD) "4WD-L" else "4WD-H"
            canvas.drawText("التعشيق: $gearStr  |  $driveStr", 50f, 195f, hudTextPaint)
            canvas.drawText(engine.cameraMode.arabicName, 50f, 225f, hudSmallPaint)

            // 3. Right HUD Panel: Clinometer, Distance & Neon Cloud
            canvas.drawRoundRect(w - 330f, 25f, w - 25f, 240f, 20f, 20f, hudBgPaint)
            hudTextPaint.textSize = 24f
            canvas.drawText("الميل الجبلي: ${engine.slopePitch.toInt()}°", w - 305f, 70f, hudTextPaint)
            val kmTravelled = (engine.distanceTravelledMeters / 1000f)
            canvas.drawText("المسافة: ${String.format("%.1f", kmTravelled)} / 10.5 كم", w - 305f, 110f, hudTextPaint)
            canvas.drawText("الطقس: ${engine.currentWeather.arabicName}", w - 305f, 150f, hudTextPaint)

            // Neon Cloud Indicator Badge
            val neonBadgePaint = Paint().apply { color = Color.rgb(16, 185, 129) } // Green Online
            canvas.drawCircle(w - 295f, 190f, 9f, neonBadgePaint)
            canvas.drawText("Neon Cloud: متصل", w - 275f, 198f, hudTextPaint)
            canvas.drawText("${engine.currentQuality.arabicName}", w - 305f, 226f, hudSmallPaint)

            // 4. Mission & Game Mode Banner (Bottom Center)
            canvas.drawRoundRect(w * 0.28f, 90f, w * 0.72f, 145f, 14f, 14f, hudBgPaint)
            hudTextPaint.textSize = 24f
            hudTextPaint.textAlign = Paint.Align.CENTER
            val modeStatus = when (engine.activeGameMode) {
                SimulationEngine.GameMode.FREE_ROAM -> "النمط: قيادة حرة — استكشف 10.5 كم كاملة"
                SimulationEngine.GameMode.DELIVERY -> "رحلة وصول: توصيل بضائع | النقاط: ${engine.missionScore}"
                SimulationEngine.GameMode.HILL_CLIMB -> "تحدي صعود النقيل | الوقت: ${engine.missionElapsedTime.toInt()}ث | نقاط: ${engine.missionScore}"
            }
            canvas.drawText(modeStatus, w * 0.50f, 126f, hudTextPaint)
        }

        private fun drawTouchControls(canvas: Canvas, w: Float, h: Float) {
            val btnRadius = 60f

            // Left Steering Touch Buttons
            val leftBtnPaint = if (steerLeftPressed) controlBtnActivePaint else controlBtnPaint
            canvas.drawCircle(105f, h - 105f, btnRadius, leftBtnPaint)
            hudGoldPaint.textAlign = Paint.Align.CENTER
            canvas.drawText("◀", 105f, h - 92f, hudGoldPaint)

            val rightBtnPaint = if (steerRightPressed) controlBtnActivePaint else controlBtnPaint
            canvas.drawCircle(245f, h - 105f, btnRadius, rightBtnPaint)
            canvas.drawText("▶", 245f, h - 92f, hudGoldPaint)

            // Right Pedals: Brake & Throttle
            val brakeBtnPaint = if (brakePressed) controlBtnActivePaint else controlBtnPaint
            canvas.drawRoundRect(w - 270f, h - 175f, w - 175f, h - 40f, 18f, 18f, brakeBtnPaint)
            hudTextPaint.textSize = 26f
            hudTextPaint.textAlign = Paint.Align.CENTER
            canvas.drawText("فرامل", w - 222f, h - 100f, hudTextPaint)

            val gasBtnPaint = if (gasPressed) controlBtnActivePaint else controlBtnPaint
            canvas.drawRoundRect(w - 150f, h - 210f, w - 45f, h - 40f, 18f, 18f, gasBtnPaint)
            canvas.drawText("بنزين", w - 98f, h - 118f, hudTextPaint)

            // Handbrake Button
            val hbPaint = if (engine.isHandbrakeActive) controlBtnActivePaint else controlBtnPaint
            canvas.drawRoundRect(w - 385f, h - 110f, w - 290f, h - 40f, 14f, 14f, hbPaint)
            hudTextPaint.textSize = 22f
            canvas.drawText("فرامل يد", w - 337f, h - 68f, hudTextPaint)

            // Quick-Action Utility Buttons (Bottom Center Row)
            val btnW = w * 0.085f
            val btnH = 55f
            val btnY = h - 70f

            // 1. Vehicle Switcher
            canvas.drawRoundRect(w * 0.33f, btnY, w * 0.33f + btnW, btnY + btnH, 12f, 12f, controlBtnPaint)
            canvas.drawText("سيارة", w * 0.33f + btnW * 0.5f, btnY + 36f, hudTextPaint)

            // 2. Camera Switcher
            canvas.drawRoundRect(w * 0.43f, btnY, w * 0.43f + btnW, btnY + btnH, 12f, 12f, controlBtnPaint)
            canvas.drawText("كاميرا", w * 0.43f + btnW * 0.5f, btnY + 36f, hudTextPaint)

            // 3. Weather Switcher
            canvas.drawRoundRect(w * 0.53f, btnY, w * 0.53f + btnW, btnY + btnH, 12f, 12f, controlBtnPaint)
            canvas.drawText("طقس", w * 0.53f + btnW * 0.5f, btnY + 36f, hudTextPaint)

            // 4. Game Mode Switcher
            canvas.drawRoundRect(w * 0.63f, btnY, w * 0.63f + btnW, btnY + btnH, 12f, 12f, controlBtnPaint)
            canvas.drawText("نمط", w * 0.63f + btnW * 0.5f, btnY + 36f, hudTextPaint)

            // 5. Quality Switcher (Top Right Corner)
            canvas.drawRoundRect(w - 180f, 250f, w - 25f, 305f, 12f, 12f, controlBtnPaint)
            canvas.drawText("جودة", w - 102f, 286f, hudTextPaint)
        }

        @SuppressLint("ClickableViewAccessibility")
        override fun onTouchEvent(event: MotionEvent): Boolean {
            val w = width.toFloat()
            val h = height.toFloat()

            when (event.actionMasked) {
                MotionEvent.ACTION_DOWN, MotionEvent.ACTION_POINTER_DOWN -> {
                    val idx = event.actionIndex
                    val x = event.getX(idx)
                    val y = event.getY(idx)

                    // Steering Left & Right
                    if (hypot((x - 105.0), (y - (h - 105.0))) < 85.0) steerLeftPressed = true
                    if (hypot((x - 245.0), (y - (h - 105.0))) < 85.0) steerRightPressed = true

                    // Brake & Gas
                    if (x in (w - 270f)..(w - 175f) && y in (h - 175f)..(h - 40f)) brakePressed = true
                    if (x in (w - 150f)..(w - 45f) && y in (h - 210f)..(h - 40f)) gasPressed = true

                    // Handbrake Toggle
                    if (x in (w - 385f)..(w - 290f) && y in (h - 110f)..(h - 40f)) {
                        engine.isHandbrakeActive = !engine.isHandbrakeActive
                    }

                    val btnW = w * 0.085f
                    val btnY = h - 70f
                    // Switch Vehicle
                    if (x in (w * 0.33f)..(w * 0.33f + btnW) && y in btnY..(btnY + 55f)) {
                        engine.cycleVehicle()
                    }
                    // Switch Camera
                    if (x in (w * 0.43f)..(w * 0.43f + btnW) && y in btnY..(btnY + 55f)) {
                        engine.cycleCamera()
                    }
                    // Cycle Weather
                    if (x in (w * 0.53f)..(w * 0.53f + btnW) && y in btnY..(btnY + 55f)) {
                        engine.cycleWeather()
                    }
                    // Cycle Game Mode
                    if (x in (w * 0.63f)..(w * 0.63f + btnW) && y in btnY..(btnY + 55f)) {
                        engine.cycleGameMode()
                    }
                    // Cycle Quality
                    if (x in (w - 180f)..(w - 25f) && y in 250f..305f) {
                        engine.cycleQuality()
                    }
                }
                MotionEvent.ACTION_UP, MotionEvent.ACTION_POINTER_UP -> {
                    val idx = event.actionIndex
                    val x = event.getX(idx)
                    val y = event.getY(idx)

                    if (hypot((x - 105.0), (y - (h - 105.0))) < 110.0) steerLeftPressed = false
                    if (hypot((x - 245.0), (y - (h - 105.0))) < 110.0) steerRightPressed = false
                    if (x in (w - 270f)..(w - 175f)) brakePressed = false
                    if (x in (w - 150f)..(w - 45f)) gasPressed = false
                }
                MotionEvent.ACTION_CANCEL -> {
                    steerLeftPressed = false
                    steerRightPressed = false
                    gasPressed = false
                    brakePressed = false
                }
            }
            return true
        }
    }
}
