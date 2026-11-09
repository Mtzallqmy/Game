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
import java.util.Random
import kotlin.math.*

class MainActivity : AppCompatActivity() {

    private lateinit var gameView: GameRenderView

    @Suppress("DEPRECATION")
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

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

    override fun onDestroy() {
        super.onDestroy()
        gameView.destroy()
    }

    @SuppressLint("ViewConstructor")
    class GameRenderView(context: Context) : View(context), Runnable {

        private val engine = SimulationEngine()
        private val vehicle3D = Vehicle3D()
        private var isRunning = false
        private var gameThread: Thread? = null

        // Onboarding Modal State
        private var showOnboardingModal = false
        private var onboardingProgress = 1.0f

        // Rendering Paints & Helpers
        private val renderPaint = Paint(Paint.ANTI_ALIAS_FLAG)
        private val strokePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { style = Paint.Style.STROKE }
        private val hudTextPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
            color = Color.WHITE
            textSize = 28f
            typeface = Typeface.DEFAULT_BOLD
        }
        private val hudGoldPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
            color = Color.rgb(245, 158, 11)
            textSize = 38f
            typeface = Typeface.DEFAULT_BOLD
        }
        private val hudSmallPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
            color = Color.rgb(203, 213, 225)
            textSize = 20f
        }
        private val hudBgPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
            color = Color.argb(215, 15, 23, 42)
        }
        private val controlBtnPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
            color = Color.argb(165, 30, 41, 59)
        }
        private val controlBtnActivePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
            color = Color.argb(235, 245, 158, 11)
        }

        // Touch Control States
        private var steerLeftPressed = false
        private var steerRightPressed = false
        private var gasPressed = false
        private var brakePressed = false

        // Random for particles
        private val random = Random()

        init {
            isFocusable = true
        }

        fun resume() {
            isRunning = true
            engine.soundEngine.start()
            gameThread = Thread(this, "NageelRenderLoop").apply { start() }
        }

        fun pause() {
            isRunning = false
            engine.soundEngine.stop()
            try {
                gameThread?.join(500)
            } catch (e: InterruptedException) {
                e.printStackTrace()
            }
        }

        fun destroy() {
            pause()
            engine.soundEngine.stop()
        }

        override fun run() {
            var lastTime = System.nanoTime()
            while (isRunning) {
                val now = System.nanoTime()
                val delta = ((now - lastTime) / 1_000_000_000.0).toFloat()
                lastTime = now

                // Forward touch inputs
                engine.steerInput = when {
                    steerLeftPressed && !steerRightPressed -> -1.0f
                    steerRightPressed && !steerLeftPressed -> 1.0f
                    else -> 0.0f
                }
                engine.throttleInput = if (gasPressed) 1.0f else 0.0f
                engine.brakeInput = if (brakePressed) 1.0f else 0.0f

                engine.update(delta)

                postInvalidate()

                try {
                    val sleepMs = when (engine.currentQuality) {
                        SimulationEngine.QualityLevel.LOW -> 30L
                        SimulationEngine.QualityLevel.MEDIUM -> 20L
                        SimulationEngine.QualityLevel.HIGH -> 16L
                    }
                    Thread.sleep(sleepMs)
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

            // Apply camera shake on impacts
            if (engine.cameraShake > 0f) {
                val shakeX = (random.nextFloat() * 2f - 1f) * (engine.cameraShake * 18f)
                val shakeY = (random.nextFloat() * 2f - 1f) * (engine.cameraShake * 18f)
                canvas.translate(shakeX, shakeY)
            }

            val currentZone = engine.getCurrentZone()

            // 1. Draw Multi-Layer Parallax Background & Dynamic Sky
            drawSkyAndParallax(canvas, w, h, currentZone)

            // 2. Draw 3D Segmented Mountain Road with True Perspective
            draw3DRoadNetwork(canvas, w, h)

            // 3. Draw Player Vehicle based on Camera Mode
            when (engine.cameraMode) {
                SimulationEngine.CameraMode.CHASE_CLOSE -> {
                    vehicle3D.drawChaseVehicle(canvas, w * 0.50f, h * 0.78f, 1.05f, engine)
                }
                SimulationEngine.CameraMode.CHASE_FAR -> {
                    vehicle3D.drawChaseVehicle(canvas, w * 0.50f, h * 0.74f, 0.78f, engine)
                }
                SimulationEngine.CameraMode.COCKPIT -> {
                    vehicle3D.drawCockpitView(canvas, w, h, engine)
                }
                SimulationEngine.CameraMode.HOOD_BUMPER -> {
                    drawHoodBumperView(canvas, w, h)
                }
            }

            // 4. Draw Weather & Speed Effects
            drawWeatherAndSpeedLines(canvas, w, h)

            // 5. Draw Arabic HUD & Telemetry
            drawComprehensiveHUD(canvas, w, h, currentZone)

            // 6. Draw Touch Controls & Switchers
            drawTouchControls(canvas, w, h)

            // 7. Draw Onboarding Modal (if opened)
            if (showOnboardingModal) {
                drawOnboardingModal(canvas, w, h)
            }
        }

        private fun drawSkyAndParallax(canvas: Canvas, w: Float, h: Float, zone: SimulationEngine.WorldZone) {
            val hour = engine.timeOfDayHours
            val horizonY = h * 0.44f

            // Sky gradient color
            val skyTopColor: Int
            val skyBottomColor: Int

            when {
                engine.currentWeather == SimulationEngine.WeatherType.MOUNTAIN_RAIN -> {
                    skyTopColor = Color.rgb(65, 75, 90)
                    skyBottomColor = Color.rgb(120, 130, 145)
                }
                engine.currentWeather == SimulationEngine.WeatherType.HIGHLAND_FOG -> {
                    skyTopColor = Color.rgb(180, 195, 210)
                    skyBottomColor = Color.rgb(215, 225, 235)
                }
                hour in 6.0f..8.0f -> { // Dawn
                    skyTopColor = Color.rgb(120, 140, 190)
                    skyBottomColor = Color.rgb(245, 185, 140)
                }
                hour in 8.0f..17.0f -> { // Clear Day
                    skyTopColor = Color.rgb(55, 135, 230)
                    skyBottomColor = Color.rgb(175, 215, 255)
                }
                hour in 17.0f..19.5f -> { // Yemeni Sunset
                    skyTopColor = Color.rgb(115, 45, 95)
                    skyBottomColor = Color.rgb(240, 125, 55)
                }
                else -> { // Night
                    skyTopColor = Color.rgb(8, 12, 24)
                    skyBottomColor = Color.rgb(22, 32, 54)
                }
            }

            // Draw Sky Rect
            renderPaint.shader = LinearGradient(0f, 0f, 0f, horizonY, skyTopColor, skyBottomColor, Shader.TileMode.CLAMP)
            canvas.drawRect(0f, 0f, w, horizonY + 2f, renderPaint)
            renderPaint.shader = null

            // Night Stars
            if (hour < 5.8f || hour > 19.5f) {
                renderPaint.color = Color.WHITE
                for (i in 0..35) {
                    val sx = (sin(i.toDouble() * 43.0) * 0.5 + 0.5).toFloat() * w
                    val sy = (cos(i.toDouble() * 23.0) * 0.5 + 0.5).toFloat() * (horizonY * 0.75f)
                    canvas.drawCircle(sx, sy, 2.2f, renderPaint)
                }
            }

            // Distant Mountain Ranges (Parallax scroll with car curve)
            val baseCurveShift = (engine.distanceTravelledMeters * 0.05f + engine.playerX * 80f) % w

            // Far Mountains Layer
            val farMountainColor = when (zone) {
                SimulationEngine.WorldZone.NAQIL_PASS -> Color.rgb(85, 70, 60)
                SimulationEngine.WorldZone.GREEN_HIGHLANDS -> Color.rgb(40, 85, 50)
                SimulationEngine.WorldZone.ROCKY_WADI -> Color.rgb(145, 85, 55)
            }
            renderPaint.color = farMountainColor

            val farMountainPath = Path().apply {
                moveTo(0f, horizonY)
                var px = 0f
                while (px <= w + 120f) {
                    val peakH = (sin((px + baseCurveShift) * 0.008) * 0.5 + 0.5).toFloat() * (h * 0.16f)
                    lineTo(px, horizonY - peakH)
                    px += 90f
                }
                lineTo(w, horizonY)
                close()
            }
            canvas.drawPath(farMountainPath, renderPaint)

            // Mid-Ground Ridges Layer
            val midRidgeColor = when (zone) {
                SimulationEngine.WorldZone.NAQIL_PASS -> Color.rgb(65, 52, 44)
                SimulationEngine.WorldZone.GREEN_HIGHLANDS -> Color.rgb(28, 70, 36)
                SimulationEngine.WorldZone.ROCKY_WADI -> Color.rgb(120, 68, 42)
            }
            renderPaint.color = midRidgeColor

            val midRidgePath = Path().apply {
                moveTo(0f, horizonY)
                var px = 0f
                while (px <= w + 120f) {
                    val peakH = (cos((px + baseCurveShift * 1.5) * 0.012) * 0.5 + 0.5).toFloat() * (h * 0.10f)
                    lineTo(px, horizonY - peakH)
                    px += 70f
                }
                lineTo(w, horizonY)
                close()
            }
            canvas.drawPath(midRidgePath, renderPaint)
        }

        private fun draw3DRoadNetwork(canvas: Canvas, w: Float, h: Float) {
            val track = engine.track
            val camZ = engine.distanceTravelledMeters
            val baseSegment = track.findSegment(camZ)
            val baseIdx = baseSegment.index

            val drawDistance = 140 // Render 140 segments ahead (~1,000m)
            val camDepth = 0.82f
            val camHeight = when (engine.cameraMode) {
                SimulationEngine.CameraMode.CHASE_CLOSE -> 1350f
                SimulationEngine.CameraMode.CHASE_FAR -> 1800f
                SimulationEngine.CameraMode.COCKPIT -> 950f
                SimulationEngine.CameraMode.HOOD_BUMPER -> 720f
            }

            val camX = baseSegment.p1.worldX + (engine.playerX * track.roadWidth)
            val camY = camHeight + baseSegment.p1.worldY

            var maxClipY = h // Horizon occlusion clipping

            // 1. Project all visible segments
            for (n in 0 until drawDistance) {
                val segIdx = (baseIdx + n) % track.totalSegments
                val seg = track.segments[segIdx]

                val loopedZ1 = if (seg.p1.worldZ < camZ) seg.p1.worldZ + (track.totalSegments * track.segmentLength) else seg.p1.worldZ
                val loopedZ2 = if (seg.p2.worldZ < camZ) seg.p2.worldZ + (track.totalSegments * track.segmentLength) else seg.p2.worldZ

                // Project points
                seg.p1.project(camX, camY, loopedZ1, camDepth, w, h, track.roadWidth)
                seg.p2.project(camX, camY, loopedZ2, camDepth, w, h, track.roadWidth)
            }

            // 2. Render Road Surfaces & Curbs (Near to Far)
            for (n in 0 until drawDistance) {
                val segIdx = (baseIdx + n) % track.totalSegments
                val seg = track.segments[segIdx]

                val p1 = seg.p1
                val p2 = seg.p2

                // Behind camera or clipped by hill crest
                if (p1.screenY >= maxClipY || p2.screenY >= p1.screenY) continue

                // 2a. Grass / Mountain Terrain Background
                renderPaint.color = seg.grassColor
                canvas.drawRect(0f, p2.screenY, w, p1.screenY, renderPaint)

                // 2b. Left and Right Rumble Curbs
                val r1w = p1.screenW * 1.14f
                val r2w = p2.screenW * 1.14f
                renderPaint.color = seg.rumbleColor
                drawTrapezoid(canvas, p1.screenX, p1.screenY, r1w, p2.screenX, p2.screenY, r2w)

                // 2c. Main Road Asphalt / Gravel
                renderPaint.color = seg.roadColor
                drawTrapezoid(canvas, p1.screenX, p1.screenY, p1.screenW, p2.screenX, p2.screenY, p2.screenW)

                // 2d. Center Lane Markings
                val isDashedVisible = (seg.index / 2) % 2 == 0
                if (isDashedVisible || seg.zone == SimulationEngine.WorldZone.NAQIL_PASS) {
                    renderPaint.color = seg.laneLineColor
                    val lw1 = max(3f, p1.screenW * 0.035f)
                    val lw2 = max(2f, p2.screenW * 0.035f)

                    if (seg.zone == SimulationEngine.WorldZone.NAQIL_PASS) {
                        // Double Yellow Line for Mountain Pass Hairpins
                        drawTrapezoid(canvas, p1.screenX - lw1 * 1.2f, p1.screenY, lw1, p2.screenX - lw2 * 1.2f, p2.screenY, lw2)
                        drawTrapezoid(canvas, p1.screenX + lw1 * 1.2f, p1.screenY, lw1, p2.screenX + lw2 * 1.2f, p2.screenY, lw2)
                    } else {
                        // Single Dash for Highlands & Wadi
                        drawTrapezoid(canvas, p1.screenX, p1.screenY, lw1, p2.screenX, p2.screenY, lw2)
                    }
                }

                // 2e. Guardrail along Mountain Cliff
                if (seg.hasGuardrailLeft) {
                    renderPaint.color = Color.rgb(195, 205, 218)
                    val guardOffset1 = p1.screenX - p1.screenW * 1.08f
                    val guardOffset2 = p2.screenX - p2.screenW * 1.08f
                    val postH1 = max(4f, 28f * p1.scale * 1200f)
                    val postH2 = max(3f, 28f * p2.scale * 1200f)

                    // Post
                    canvas.drawRect(guardOffset1 - 3f, p1.screenY - postH1, guardOffset1 + 3f, p1.screenY, renderPaint)
                    // Beam
                    strokePaint.color = Color.rgb(180, 190, 205)
                    strokePaint.strokeWidth = max(2f, 6f * p1.scale * 1200f)
                    canvas.drawLine(guardOffset1, p1.screenY - postH1 * 0.7f, guardOffset2, p2.screenY - postH2 * 0.7f, strokePaint)
                }

                maxClipY = p2.screenY
            }

            // 3. Render Roadside Props & AI Traffic (Far to Near for proper Z-Order)
            for (n in drawDistance - 1 downTo 0) {
                val segIdx = (baseIdx + n) % track.totalSegments
                val seg = track.segments[segIdx]
                val p = seg.p1

                if (p.screenY <= 0 || p.screenY >= h || p.scale <= 0.0005f) continue

                // 3a. Roadside Props
                for (prop in seg.props) {
                    val propScreenX = p.screenX + (p.screenW * prop.offset)
                    vehicle3D.drawRoadsideProp(canvas, prop, propScreenX, p.screenY, p.scale * 1200f)
                }

                // 3b. AI Traffic Vehicles
                val segStartDist = seg.index * track.segmentLength
                val segEndDist = (seg.index + 1) * track.segmentLength

                for (traffic in engine.trafficCars) {
                    if (traffic.distanceMeters in segStartDist..segEndDist) {
                        val carScreenX = p.screenX + (p.screenW * traffic.playerX)
                        val trafficScale = p.scale * 1050f
                        drawAITrafficCar(canvas, carScreenX, p.screenY, trafficScale, traffic)
                    }
                }
            }

            // 4. Headlight beam overlay at night
            if (engine.areHeadlightsOn) {
                val beamPaint = Paint().apply {
                    shader = RadialGradient(
                        w * 0.50f, h * 0.85f, w * 0.55f,
                        Color.argb(90, 255, 250, 220),
                        Color.TRANSPARENT,
                        Shader.TileMode.CLAMP
                    )
                }
                canvas.drawRect(0f, h * 0.40f, w, h, beamPaint)
            }
        }

        private fun drawTrapezoid(canvas: Canvas, x1: Float, y1: Float, w1: Float, x2: Float, y2: Float, w2: Float) {
            val path = Path().apply {
                moveTo(x1 - w1, y1)
                lineTo(x1 + w1, y1)
                lineTo(x2 + w2, y2)
                lineTo(x2 - w2, y2)
                close()
            }
            canvas.drawPath(path, renderPaint)
        }

        private fun drawAITrafficCar(canvas: Canvas, x: Float, y: Float, scale: Float, car: SimulationEngine.AITraffic) {
            val s = scale
            if (s <= 0.01f) return

            val carColor = when (car.carTypeIndex) {
                0 -> Color.rgb(230, 215, 195) // Land Cruiser SUV
                1 -> Color.rgb(240, 242, 245) // White Shas
                else -> Color.rgb(215, 45, 45) // Red Sedan
            }

            renderPaint.color = carColor
            val carW = 180f * s
            val carH = 110f * s

            // Body
            canvas.drawRoundRect(x - carW * 0.5f, y - carH, x + carW * 0.5f, y, 10f * s, 10f * s, renderPaint)

            // Cabin Roof
            renderPaint.color = Color.rgb(30, 35, 45) // Dark glass
            canvas.drawRoundRect(x - carW * 0.38f, y - carH * 1.35f, x + carW * 0.38f, y - carH * 0.75f, 6f * s, 6f * s, renderPaint)

            // Lights
            if (car.isOncoming) {
                // Bright Headlights
                renderPaint.color = Color.rgb(255, 255, 220)
                canvas.drawCircle(x - carW * 0.35f, y - carH * 0.3f, 14f * s, renderPaint)
                canvas.drawCircle(x + carW * 0.35f, y - carH * 0.3f, 14f * s, renderPaint)
            } else {
                // Red Taillights
                renderPaint.color = Color.rgb(240, 30, 30)
                canvas.drawRect(x - carW * 0.44f, y - carH * 0.45f, x - carW * 0.28f, y - carH * 0.25f, renderPaint)
                canvas.drawRect(x + carW * 0.28f, y - carH * 0.45f, x + carW * 0.44f, y - carH * 0.25f, renderPaint)
            }
        }

        private fun drawHoodBumperView(canvas: Canvas, w: Float, h: Float) {
            val hoodPaint = Paint().apply { color = Color.rgb(32, 40, 52) }
            val hoodPath = Path().apply {
                moveTo(w * 0.20f, h)
                lineTo(w * 0.35f, h * 0.90f)
                lineTo(w * 0.65f, h * 0.90f)
                lineTo(w * 0.80f, h)
                close()
            }
            canvas.drawPath(hoodPath, hoodPaint)
        }

        private fun drawWeatherAndSpeedLines(canvas: Canvas, w: Float, h: Float) {
            // Speed Lines at High Speed (> 90 km/h)
            if (engine.currentSpeedKmh > 90f) {
                val speedLinePaint = Paint().apply {
                    color = Color.argb(80, 255, 255, 255)
                    strokeWidth = 3f
                }
                for (i in 0..12) {
                    val sx = if (i % 2 == 0) random.nextFloat() * (w * 0.20f) else w - random.nextFloat() * (w * 0.20f)
                    val sy = random.nextFloat() * h
                    canvas.drawLine(sx, sy, sx + (if (i % 2 == 0) 35f else -35f), sy + 45f, speedLinePaint)
                }
            }

            // Weather Particles
            when (engine.currentWeather) {
                SimulationEngine.WeatherType.MOUNTAIN_RAIN -> {
                    val rainPaint = Paint().apply {
                        color = Color.argb(160, 210, 230, 255)
                        strokeWidth = 3.5f
                    }
                    for (i in 0..45) {
                        val rx = random.nextFloat() * w
                        val ry = random.nextFloat() * h
                        canvas.drawLine(rx, ry, rx - 8f, ry + 32f, rainPaint)
                    }
                }
                SimulationEngine.WeatherType.WADI_DUST -> {
                    val dustPaint = Paint().apply { color = Color.argb(95, 215, 175, 125) }
                    for (i in 0..30) {
                        val dx = random.nextFloat() * w
                        val dy = (h * 0.45f) + random.nextFloat() * (h * 0.55f)
                        canvas.drawCircle(dx, dy, 15f + random.nextInt(18), dustPaint)
                    }
                }
                else -> {}
            }
        }

        private fun drawComprehensiveHUD(canvas: Canvas, w: Float, h: Float, zone: SimulationEngine.WorldZone) {
            // 1. Top Status Banner: Active Zone, Altitude & Vehicle
            canvas.drawRoundRect(w * 0.14f, 15f, w * 0.86f, 75f, 18f, 18f, hudBgPaint)
            val altitudeMeters = (2400f + (engine.distanceTravelledMeters / 10500f) * 450f).toInt()
            val bannerTitle = "${zone.arabicName} — الارتفاع: ${altitudeMeters}م — ${engine.currentVehicleType.arabicName}"
            hudTextPaint.textAlign = Paint.Align.CENTER
            hudTextPaint.textSize = 28f
            canvas.drawText(bannerTitle, w * 0.50f, 55f, hudTextPaint)

            // 2. Left HUD Panel: Digital Speedometer, Gear & RPM
            canvas.drawRoundRect(25f, 25f, 310f, 240f, 22f, 22f, hudBgPaint)
            hudGoldPaint.textAlign = Paint.Align.LEFT
            hudGoldPaint.textSize = 42f
            canvas.drawText("${round(engine.currentSpeedKmh).toInt()}", 50f, 92f, hudGoldPaint)
            hudTextPaint.textAlign = Paint.Align.LEFT
            hudTextPaint.textSize = 26f
            canvas.drawText("كم/س", 150f, 90f, hudTextPaint)

            hudGoldPaint.textSize = 30f
            canvas.drawText("${round(engine.currentRPM).toInt()}", 50f, 145f, hudGoldPaint)
            hudSmallPaint.textAlign = Paint.Align.LEFT
            canvas.drawText("دورة/دقيقة", 165f, 142f, hudSmallPaint)

            val gearStr = if (engine.currentGear == -1) "R" else "D${engine.currentGear}"
            val driveStr = if (engine.driveType == SimulationEngine.DriveType.LOW_RANGE_4WD) "4WD-L" else "4WD-H"
            canvas.drawText("التعشيق: $gearStr  |  $driveStr", 50f, 195f, hudTextPaint)
            canvas.drawText(engine.cameraMode.arabicName, 50f, 225f, hudSmallPaint)

            // 3. Right HUD Panel: Clinometer, Distance & Audio
            canvas.drawRoundRect(w - 330f, 25f, w - 25f, 240f, 22f, 22f, hudBgPaint)
            hudTextPaint.textSize = 24f
            canvas.drawText("الميل الجبلي: ${engine.slopePitch.toInt()}°", w - 305f, 70f, hudTextPaint)
            val kmTravelled = (engine.distanceTravelledMeters / 1000f)
            canvas.drawText("المسافة: ${String.format("%.1f", kmTravelled)} / 10.5 كم", w - 305f, 110f, hudTextPaint)
            canvas.drawText("الطقس: ${engine.currentWeather.arabicName}", w - 305f, 150f, hudTextPaint)

            val neonBadgePaint = Paint().apply { color = Color.rgb(16, 185, 129) }
            canvas.drawCircle(w - 295f, 190f, 9f, neonBadgePaint)
            canvas.drawText("الصوت: ${if (engine.soundEngine.isMuted) "مكتوم 🔇" else "مفعل 🔊"}", w - 275f, 198f, hudTextPaint)
            canvas.drawText("${engine.currentQuality.arabicName}", w - 305f, 226f, hudSmallPaint)

            // 4. Center Game Mode Banner
            canvas.drawRoundRect(w * 0.28f, 90f, w * 0.72f, 145f, 14f, 14f, hudBgPaint)
            hudTextPaint.textAlign = Paint.Align.CENTER
            hudTextPaint.textSize = 24f
            val modeStatus = when (engine.activeGameMode) {
                SimulationEngine.GameMode.FREE_ROAM -> "النمط: قيادة حرة — استكشف مسار 10.5 كم"
                SimulationEngine.GameMode.DELIVERY -> "رحلة توصيل | النقاط: ${engine.missionScore}"
                SimulationEngine.GameMode.HILL_CLIMB -> "تحدي صعود النقيل | ${engine.missionElapsedTime.toInt()}ث | نقاط: ${engine.missionScore}"
            }
            canvas.drawText(modeStatus, w * 0.50f, 126f, hudTextPaint)
        }

        private fun drawTouchControls(canvas: Canvas, w: Float, h: Float) {
            val btnRadius = 65f

            // Left Steering Touch Buttons
            val leftBtnPaint = if (steerLeftPressed) controlBtnActivePaint else controlBtnPaint
            canvas.drawCircle(115f, h - 110f, btnRadius, leftBtnPaint)
            hudGoldPaint.textAlign = Paint.Align.CENTER
            hudGoldPaint.textSize = 44f
            canvas.drawText("◀", 115f, h - 95f, hudGoldPaint)

            val rightBtnPaint = if (steerRightPressed) controlBtnActivePaint else controlBtnPaint
            canvas.drawCircle(265f, h - 110f, btnRadius, rightBtnPaint)
            canvas.drawText("▶", 265f, h - 95f, hudGoldPaint)

            // Right Pedals: Brake & Gas
            val brakeBtnPaint = if (brakePressed) controlBtnActivePaint else controlBtnPaint
            canvas.drawRoundRect(w - 280f, h - 185f, w - 175f, h - 40f, 20f, 20f, brakeBtnPaint)
            hudTextPaint.textSize = 28f
            hudTextPaint.textAlign = Paint.Align.CENTER
            canvas.drawText("فرامل", w - 228f, h - 105f, hudTextPaint)

            val gasBtnPaint = if (gasPressed) controlBtnActivePaint else controlBtnPaint
            canvas.drawRoundRect(w - 155f, h - 225f, w - 40f, h - 40f, 20f, 20f, gasBtnPaint)
            canvas.drawText("بنزين", w - 98f, h - 125f, hudTextPaint)

            // Handbrake Button
            val hbPaint = if (engine.isHandbrakeActive) controlBtnActivePaint else controlBtnPaint
            canvas.drawRoundRect(w - 395f, h - 115f, w - 300f, h - 40f, 16f, 16f, hbPaint)
            hudTextPaint.textSize = 22f
            canvas.drawText("فرامل يد", w - 347f, h - 72f, hudTextPaint)

            // Quick-Action Bottom Bar
            val btnW = w * 0.088f
            val btnH = 55f
            val btnY = h - 70f

            // 1. Vehicle Switcher
            canvas.drawRoundRect(w * 0.31f, btnY, w * 0.31f + btnW, btnY + btnH, 14f, 14f, controlBtnPaint)
            canvas.drawText("سيارة", w * 0.31f + btnW * 0.5f, btnY + 36f, hudTextPaint)

            // 2. Camera Switcher
            canvas.drawRoundRect(w * 0.405f, btnY, w * 0.405f + btnW, btnY + btnH, 14f, 14f, controlBtnPaint)
            canvas.drawText("كاميرا", w * 0.405f + btnW * 0.5f, btnY + 36f, hudTextPaint)

            // 3. Weather Switcher
            canvas.drawRoundRect(w * 0.50f, btnY, w * 0.50f + btnW, btnY + btnH, 14f, 14f, controlBtnPaint)
            canvas.drawText("طقس", w * 0.50f + btnW * 0.5f, btnY + 36f, hudTextPaint)

            // 4. Sound Mute Toggle
            canvas.drawRoundRect(w * 0.595f, btnY, w * 0.595f + btnW, btnY + btnH, 14f, 14f, controlBtnPaint)
            canvas.drawText("صوت", w * 0.595f + btnW * 0.5f, btnY + 36f, hudTextPaint)

            // 5. Game Mode Switcher
            canvas.drawRoundRect(w * 0.69f, btnY, w * 0.69f + btnW, btnY + btnH, 14f, 14f, controlBtnPaint)
            canvas.drawText("نمط", w * 0.69f + btnW * 0.5f, btnY + 36f, hudTextPaint)

            // 6. Quality Switcher (Top Right Corner)
            canvas.drawRoundRect(w - 180f, 250f, w - 25f, 305f, 12f, 12f, controlBtnPaint)
            canvas.drawText("جودة", w - 102f, 286f, hudTextPaint)
        }

        private fun drawOnboardingModal(canvas: Canvas, w: Float, h: Float) {
            val mw = w * 0.70f
            val mh = h * 0.72f
            val mx = (w - mw) * 0.5f
            val my = (h - mh) * 0.5f

            canvas.drawRoundRect(mx, my, mx + mw, my + mh, 24f, 24f, hudBgPaint)

            hudGoldPaint.textSize = 36f
            hudGoldPaint.textAlign = Paint.Align.CENTER
            canvas.drawText("نَقِيل — محاكاة القيادة الجبلية في اليمن", mx + mw * 0.5f, my + 60f, hudGoldPaint)

            val bodyPaint = Paint().apply {
                color = Color.WHITE
                textSize = 26f
                isAntiAlias = true
                textAlign = Paint.Align.RIGHT
            }

            var lineY = my + 130f
            canvas.drawText("• محرك فيزيائي ثلاثي الأبعاد متكامل للطرق والمنعطفات", mx + mw - 40f, lineY, bodyPaint)
            lineY += 45f
            canvas.drawText("• صوت محرك V8 ديناميكي ومؤثرات تفحيط واحتكاك حقيقية", mx + mw - 40f, lineY, bodyPaint)
            lineY += 45f
            canvas.drawText("• 3 مناطق يمنية أصيلة: نقيل سمارة، مدرجات إب، وادي بنا", mx + mw - 40f, lineY, bodyPaint)
            lineY += 45f
            canvas.drawText("• 3 مركبات يمنية مميزة: لاندكروزر 4WD، شاص حوض، سيدان", mx + mw - 40f, lineY, bodyPaint)

            // Close button
            val btnCloseW = 260f
            val btnCloseH = 60f
            val btnCloseX = mx + (mw - btnCloseW) * 0.5f
            val btnCloseY = my + mh - 75f

            canvas.drawRoundRect(btnCloseX, btnCloseY, btnCloseX + btnCloseW, btnCloseY + btnCloseH, 16f, 16f, controlBtnActivePaint)
            hudTextPaint.color = Color.rgb(15, 23, 42)
            hudTextPaint.textSize = 28f
            hudTextPaint.textAlign = Paint.Align.CENTER
            canvas.drawText("بدء القيادة", btnCloseX + btnCloseW * 0.5f, btnCloseY + 42f, hudTextPaint)
            hudTextPaint.color = Color.WHITE
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

                    // Onboarding Modal dismiss
                    if (showOnboardingModal) {
                        val mw = w * 0.70f
                        val mh = h * 0.72f
                        val mx = (w - mw) * 0.5f
                        val my = (h - mh) * 0.5f
                        val btnCloseW = 260f
                        val btnCloseH = 60f
                        val btnCloseX = mx + (mw - btnCloseW) * 0.5f
                        val btnCloseY = my + mh - 75f

                        if (x in btnCloseX..(btnCloseX + btnCloseW) && y in btnCloseY..(btnCloseY + btnCloseH)) {
                            showOnboardingModal = false
                        }
                        return true
                    }

                    // Steering Left & Right
                    if (hypot((x - 115.0), (y - (h - 110.0))) < 90.0) steerLeftPressed = true
                    if (hypot((x - 265.0), (y - (h - 110.0))) < 90.0) steerRightPressed = true

                    // Brake & Gas
                    if (x in (w - 280f)..(w - 175f) && y in (h - 185f)..(h - 40f)) brakePressed = true
                    if (x in (w - 155f)..(w - 40f) && y in (h - 225f)..(h - 40f)) gasPressed = true

                    // Handbrake Toggle
                    if (x in (w - 395f)..(w - 300f) && y in (h - 115f)..(h - 40f)) {
                        engine.isHandbrakeActive = !engine.isHandbrakeActive
                    }

                    // Bottom Bar Switches
                    val btnW = w * 0.088f
                    val btnY = h - 70f
                    // Vehicle
                    if (x in (w * 0.31f)..(w * 0.31f + btnW) && y in btnY..(btnY + 55f)) {
                        engine.cycleVehicle()
                    }
                    // Camera
                    if (x in (w * 0.405f)..(w * 0.405f + btnW) && y in btnY..(btnY + 55f)) {
                        engine.cycleCamera()
                    }
                    // Weather
                    if (x in (w * 0.50f)..(w * 0.50f + btnW) && y in btnY..(btnY + 55f)) {
                        engine.cycleWeather()
                    }
                    // Sound Mute Toggle
                    if (x in (w * 0.595f)..(w * 0.595f + btnW) && y in btnY..(btnY + 55f)) {
                        engine.toggleMute()
                    }
                    // Game Mode
                    if (x in (w * 0.69f)..(w * 0.69f + btnW) && y in btnY..(btnY + 55f)) {
                        engine.cycleGameMode()
                    }
                    // Quality
                    if (x in (w - 180f)..(w - 25f) && y in 250f..305f) {
                        engine.cycleQuality()
                    }
                }
                MotionEvent.ACTION_UP, MotionEvent.ACTION_POINTER_UP -> {
                    val idx = event.actionIndex
                    val x = event.getX(idx)
                    val y = event.getY(idx)

                    if (hypot((x - 115.0), (y - (h - 110.0))) < 115.0) steerLeftPressed = false
                    if (hypot((x - 265.0), (y - (h - 110.0))) < 115.0) steerRightPressed = false
                    if (x in (w - 280f)..(w - 175f)) brakePressed = false
                    if (x in (w - 155f)..(w - 40f)) gasPressed = false
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
