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

        // Fullscreen immersive mode
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

        // Paints
        private val skyPaint = Paint().apply { color = Color.rgb(135, 175, 215) }
        private val mountainPaint = Paint().apply { color = Color.rgb(105, 88, 72) }
        private val roadPaint = Paint().apply { color = Color.rgb(42, 44, 48) }
        private val roadLinePaint = Paint().apply {
            color = Color.rgb(245, 190, 40)
            strokeWidth = 6f
            style = Paint.Style.STROKE
        }
        private val barrierPaint = Paint().apply { color = Color.rgb(180, 185, 190) }
        private val carBodyPaint = Paint().apply { color = Color.rgb(235, 228, 215) } // Desert Sand
        private val carWindowPaint = Paint().apply { color = Color.rgb(30, 45, 60) }
        private val wheelPaint = Paint().apply { color = Color.rgb(25, 25, 28) }
        private val hudBgPaint = Paint().apply { color = Color.argb(190, 15, 23, 42) }
        private val hudTextPaint = Paint().apply {
            color = Color.WHITE
            textSize = 34f
            isAntiAlias = true
            typeface = Typeface.DEFAULT_BOLD
        }
        private val hudGoldPaint = Paint().apply {
            color = Color.rgb(245, 158, 11)
            textSize = 42f
            isAntiAlias = true
            typeface = Typeface.DEFAULT_BOLD
        }
        private val controlBtnPaint = Paint().apply {
            color = Color.argb(160, 30, 41, 59)
            style = Paint.Style.FILL
        }
        private val controlBtnActivePaint = Paint().apply {
            color = Color.argb(220, 245, 158, 11)
            style = Paint.Style.FILL
        }

        // Touch Control Touch Areas
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

                // Update input state into physics engine
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
                    Thread.sleep(16) // ~60 FPS
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

            // 1. Draw Mountain Pass Sky & Peaks
            canvas.drawRect(0f, 0f, w, h * 0.45f, skyPaint)

            val mountainPath = Path().apply {
                moveTo(0f, h * 0.45f)
                lineTo(w * 0.20f, h * 0.22f)
                lineTo(w * 0.45f, h * 0.35f)
                lineTo(w * 0.70f, h * 0.18f)
                lineTo(w, h * 0.40f)
                lineTo(w, h * 0.55f)
                lineTo(0f, h * 0.55f)
                close()
            }
            canvas.drawPath(mountainPath, mountainPaint)

            // 2. Draw Mountain Pass Winding Road (Perspective)
            val roadPath = Path().apply {
                moveTo(w * 0.46f, h * 0.45f) // Road vanishing point
                lineTo(w * 0.54f, h * 0.45f)
                lineTo(w * 0.90f, h)
                lineTo(w * 0.10f, h)
                close()
            }
            canvas.drawPath(roadPath, roadPaint)

            // Center Road Line
            canvas.drawLine(w * 0.50f, h * 0.45f, w * 0.50f, h, roadLinePaint)

            // Safety Guardrails on Drop-Off Edge
            canvas.drawLine(w * 0.10f, h, w * 0.46f, h * 0.45f, barrierPaint)

            // 3. Draw 4WD Land Cruiser (Chase View)
            if (engine.cameraMode == SimulationEngine.CameraMode.CHASE) {
                drawChaseVehicle(canvas, w * 0.50f, h * 0.74f)
            } else {
                drawCockpitView(canvas, w, h)
            }

            // 4. Draw Modern Arabic HUD & Dashboard
            drawHUD(canvas, w, h)

            // 5. Draw Touch Screen Controls
            drawTouchControls(canvas, w, h)
        }

        private fun drawChaseVehicle(canvas: Canvas, cx: Float, cy: Float) {
            val steerAngle = engine.steerInput * 18f

            // Wheels
            canvas.drawRoundRect(cx - 110f, cy + 40f, cx - 80f, cy + 100f, 8f, 8f, wheelPaint)
            canvas.drawRoundRect(cx + 80f, cy + 40f, cx + 110f, cy + 100f, 8f, 8f, wheelPaint)

            // 4WD Body Chassis
            canvas.drawRoundRect(cx - 95f, cy - 20f, cx + 95f, cy + 60f, 12f, 12f, carBodyPaint)

            // Cabin & Roof Rack
            canvas.drawRoundRect(cx - 80f, cy - 80f, cx + 80f, cy - 20f, 14f, 14f, carBodyPaint)
            canvas.drawRoundRect(cx - 70f, cy - 70f, cx + 70f, cy - 25f, 8f, 8f, carWindowPaint)

            // Roof Carrier Bar
            canvas.drawRect(cx - 85f, cy - 90f, cx + 85f, cy - 82f, barrierPaint)

            // Taillights
            val lightPaint = Paint().apply { color = Color.rgb(220, 38, 38) }
            canvas.drawRect(cx - 92f, cy + 5f, cx - 75f, cy + 25f, lightPaint)
            canvas.drawRect(cx + 75f, cy + 5f, cx + 92f, cy + 25f, lightPaint)
        }

        private fun drawCockpitView(canvas: Canvas, w: Float, h: Float) {
            // Hood & Windshield Interior Framing
            val hoodPaint = Paint().apply { color = Color.rgb(215, 205, 190) }
            val hoodPath = Path().apply {
                moveTo(w * 0.20f, h)
                lineTo(w * 0.35f, h * 0.65f)
                lineTo(w * 0.65f, h * 0.65f)
                lineTo(w * 0.80f, h)
                close()
            }
            canvas.drawPath(hoodPath, hoodPaint)

            // Dashboard & Steering Wheel Rim
            val dashPaint = Paint().apply { color = Color.rgb(20, 24, 30) }
            canvas.drawRect(0f, h * 0.82f, w, h, dashPaint)

            // Steering Wheel (Driver Position)
            val steerX = w * 0.32f + (engine.steerInput * 40f)
            val steerY = h * 0.88f
            val wheelRimPaint = Paint().apply {
                color = Color.rgb(40, 45, 55)
                style = Paint.Style.STROKE
                strokeWidth = 22f
                isAntiAlias = true
            }
            canvas.drawCircle(steerX, steerY, 110f, wheelRimPaint)
        }

        private fun drawHUD(canvas: Canvas, w: Float, h: Float) {
            // Top Status Bar: Mountain Pass & Elevation
            canvas.drawRoundRect(w * 0.20f, 15f, w * 0.80f, 75f, 16f, 16f, hudBgPaint)
            val passTitle = "نَقِيل سَمَارَة — الارتفاع: 2,800 م — احذر المنعطفات"
            hudTextPaint.textAlign = Paint.Align.CENTER
            canvas.drawText(passTitle, w * 0.50f, 56f, hudTextPaint)

            // Left HUD Panel: Speedometer & Tachometer
            canvas.drawRoundRect(30f, 30f, 290f, 230f, 20f, 20f, hudBgPaint)
            hudGoldPaint.textAlign = Paint.Align.LEFT
            canvas.drawText("${round(engine.currentSpeedKmh).toInt()}", 55f, 95f, hudGoldPaint)
            hudTextPaint.textAlign = Paint.Align.LEFT
            canvas.drawText("كم/س", 145f, 92f, hudTextPaint)

            hudGoldPaint.textSize = 32f
            canvas.drawText("${round(engine.currentRPM).toInt()}", 55f, 155f, hudGoldPaint)
            hudTextPaint.textSize = 24f
            canvas.drawText("دورة/دقيقة", 165f, 153f, hudTextPaint)

            // Gear & 4WD Indicator
            val gearStr = if (engine.currentGear == -1) "R" else "D${engine.currentGear}"
            val driveStr = if (engine.driveType == SimulationEngine.DriveType.LOW_RANGE_4WD) "4WD-L" else "4WD-H"
            canvas.drawText("التعشيق: $gearStr  |  $driveStr", 55f, 205f, hudTextPaint)

            // Right HUD Panel: Mountain Clinometer & Neon Status
            canvas.drawRoundRect(w - 320f, 30f, w - 30f, 210f, 20f, 20f, hudBgPaint)
            hudTextPaint.textSize = 26f
            canvas.drawText("الميلان الجبلي: ${engine.slopePitch.toInt()}°", w - 300f, 75f, hudTextPaint)
            canvas.drawText("المسافة: ${engine.distanceTravelledMeters.toInt()} م", w - 300f, 115f, hudTextPaint)

            // Neon Cloud Badge
            val neonBadgePaint = Paint().apply { color = Color.rgb(16, 185, 129) } // Green
            canvas.drawCircle(w - 290f, 160f, 10f, neonBadgePaint)
            canvas.drawText("Neon Cloud: متصل", w - 265f, 168f, hudTextPaint)
        }

        private fun drawTouchControls(canvas: Canvas, w: Float, h: Float) {
            val btnRadius = 65f

            // Left Steering Buttons
            val leftBtnPaint = if (steerLeftPressed) controlBtnActivePaint else controlBtnPaint
            canvas.drawCircle(110f, h - 110f, btnRadius, leftBtnPaint)
            hudGoldPaint.textAlign = Paint.Align.CENTER
            canvas.drawText("◀", 110f, h - 96f, hudGoldPaint)

            val rightBtnPaint = if (steerRightPressed) controlBtnActivePaint else controlBtnPaint
            canvas.drawCircle(260f, h - 110f, btnRadius, rightBtnPaint)
            canvas.drawText("▶", 260f, h - 96f, hudGoldPaint)

            // Right Pedals: Brake & Throttle
            val brakeBtnPaint = if (brakePressed) controlBtnActivePaint else controlBtnPaint
            canvas.drawRoundRect(w - 290f, h - 180f, w - 190f, h - 40f, 20f, 20f, brakeBtnPaint)
            hudTextPaint.textSize = 28f
            hudTextPaint.textAlign = Paint.Align.CENTER
            canvas.drawText("فرامل", w - 240f, h - 100f, hudTextPaint)

            val gasBtnPaint = if (gasPressed) controlBtnActivePaint else controlBtnPaint
            canvas.drawRoundRect(w - 160f, h - 220f, w - 50f, h - 40f, 20f, 20f, gasBtnPaint)
            canvas.drawText("بنزين", w - 105f, h - 120f, hudTextPaint)

            // Handbrake & Function Buttons
            val hbPaint = if (engine.isHandbrakeActive) controlBtnActivePaint else controlBtnPaint
            canvas.drawRoundRect(w - 410f, h - 120f, w - 310f, h - 40f, 16f, 16f, hbPaint)
            hudTextPaint.textSize = 24f
            canvas.drawText("فرامل يد", w - 360f, h - 72f, hudTextPaint)

            // Camera Switch & Reset
            canvas.drawRoundRect(w * 0.40f, h - 90f, w * 0.49f, h - 30f, 14f, 14f, controlBtnPaint)
            canvas.drawText("كاميرا", w * 0.445f, h - 52f, hudTextPaint)

            canvas.drawRoundRect(w * 0.51f, h - 90f, w * 0.60f, h - 30f, 14f, 14f, controlBtnPaint)
            canvas.drawText("إعادة", w * 0.555f, h - 52f, hudTextPaint)
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

                    // Steering Left (circle around 110, h - 110)
                    if (hypot((x - 110.0), (y - (h - 110.0))) < 90.0) {
                        steerLeftPressed = true
                    }
                    // Steering Right (circle around 260, h - 110)
                    if (hypot((x - 260.0), (y - (h - 110.0))) < 90.0) {
                        steerRightPressed = true
                    }
                    // Brake Pedal
                    if (x in (w - 290f)..(w - 190f) && y in (h - 180f)..(h - 40f)) {
                        brakePressed = true
                    }
                    // Gas Pedal
                    if (x in (w - 160f)..(w - 50f) && y in (h - 220f)..(h - 40f)) {
                        gasPressed = true
                    }
                    // Handbrake Toggle
                    if (x in (w - 410f)..(w - 310f) && y in (h - 120f)..(h - 40f)) {
                        engine.isHandbrakeActive = !engine.isHandbrakeActive
                    }
                    // Camera Switch
                    if (x in (w * 0.40f)..(w * 0.49f) && y in (h - 90f)..(h - 30f)) {
                        engine.cycleCamera()
                    }
                    // Reset Vehicle
                    if (x in (w * 0.51f)..(w * 0.60f) && y in (h - 90f)..(h - 30f)) {
                        engine.resetVehicle()
                    }
                }
                MotionEvent.ACTION_UP, MotionEvent.ACTION_POINTER_UP -> {
                    val idx = event.actionIndex
                    val x = event.getX(idx)
                    val y = event.getY(idx)

                    if (hypot((x - 110.0), (y - (h - 110.0))) < 110.0) steerLeftPressed = false
                    if (hypot((x - 260.0), (y - (h - 110.0))) < 110.0) steerRightPressed = false
                    if (x in (w - 290f)..(w - 190f)) brakePressed = false
                    if (x in (w - 160f)..(w - 50f)) gasPressed = false
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
