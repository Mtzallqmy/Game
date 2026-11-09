package com.moataz.nageel

import android.graphics.*
import kotlin.math.*

/**
 * 3D Vehicle & Roadside Architecture Renderer for Nageel
 * Implements high-fidelity perspective rendering of Yemeni vehicles,
 * animated steering and suspension dynamics, authentic stone tower houses,
 * watchtowers, and cockpit interiors.
 */
class Vehicle3D {

    // Paints
    private val paint = Paint(Paint.ANTI_ALIAS_FLAG)
    private val strokePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { style = Paint.Style.STROKE }
    private val textPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        typeface = Typeface.DEFAULT_BOLD
        textAlign = Paint.Align.CENTER
    }

    // Vehicle Colors
    private val suvBodyColor = Color.rgb(230, 222, 208)       // Desert Sand
    private val suvDarkColor = Color.rgb(195, 185, 170)
    private val shasBodyColor = Color.rgb(248, 250, 252)      // Bright White
    private val shasDarkColor = Color.rgb(215, 222, 230)
    private val sedanBodyColor = Color.rgb(30, 95, 220)       // Royal Metallic Blue
    private val sedanDarkColor = Color.rgb(20, 65, 160)

    private val tireColor = Color.rgb(28, 28, 32)
    private val rimColor = Color.rgb(200, 205, 215)
    private val glassColor = Color.rgb(25, 38, 55)
    private val brakeLightOn = Color.rgb(255, 30, 30)
    private val brakeLightOff = Color.rgb(140, 20, 20)
    private val reverseLight = Color.rgb(240, 240, 240)
    private val exhaustSmokePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.argb(120, 200, 200, 200)
    }
    private val tireSkidPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.argb(180, 255, 255, 255)
    }

    /**
     * Render the active player's vehicle in 3D Chase camera perspective
     */
    fun drawChaseVehicle(
        canvas: Canvas,
        cx: Float,
        cy: Float,
        scale: Float,
        engine: SimulationEngine
    ) {
        val s = scale
        val steer = engine.steerInput
        val isBraking = engine.brakeInput > 0.1f || engine.isHandbrakeActive
        val isReverse = engine.currentGear == -1
        val rollAngle = steer * 4.5f // Body roll on turns

        canvas.save()
        canvas.translate(cx, cy)
        canvas.rotate(rollAngle)

        // Draw Exhaust Smoke when accelerating
        if (engine.throttleInput > 0.2f && engine.currentSpeedKmh > 5f) {
            val smokeSize = (12f + (engine.throttleInput * 18f)) * s
            canvas.drawCircle(-65f * s, 68f * s, smokeSize, exhaustSmokePaint)
            canvas.drawCircle(65f * s, 68f * s, smokeSize * 0.9f, exhaustSmokePaint)
        }

        // Draw Drift Skid Smoke
        if (engine.isHandbrakeActive && engine.currentSpeedKmh > 20f) {
            canvas.drawCircle(-95f * s, 75f * s, 25f * s, tireSkidPaint)
            canvas.drawCircle(95f * s, 75f * s, 25f * s, tireSkidPaint)
        }

        when (engine.currentVehicleType) {
            SimulationEngine.VehicleType.SUV_4WD -> drawSuvLandCruiser(canvas, s, steer, isBraking, isReverse)
            SimulationEngine.VehicleType.PICKUP_SHAS -> drawPickupShas(canvas, s, steer, isBraking, isReverse)
            SimulationEngine.VehicleType.SEDAN -> drawSedan(canvas, s, steer, isBraking, isReverse)
        }

        canvas.restore()
    }

    private fun drawSuvLandCruiser(canvas: Canvas, s: Float, steer: Float, isBraking: Boolean, isReverse: Boolean) {
        // 1. Shadow underneath
        paint.color = Color.argb(130, 0, 0, 0)
        canvas.drawOval(-120f * s, 60f * s, 120f * s, 95f * s, paint)

        // 2. Off-road Rear Tires (Angled with steering)
        paint.color = tireColor
        canvas.drawRoundRect(-122f * s, 25f * s, -86f * s, 85f * s, 8f * s, 8f * s, paint)
        canvas.drawRoundRect(86f * s, 25f * s, 122f * s, 85f * s, 8f * s, 8f * s, paint)

        // Alloy wheel rims
        paint.color = rimColor
        canvas.drawCircle(-104f * s, 55f * s, 14f * s, paint)
        canvas.drawCircle(104f * s, 55f * s, 14f * s, paint)

        // 3. Main Chassis Body
        paint.color = suvBodyColor
        val bodyPath = Path().apply {
            moveTo(-105f * s, 55f * s)
            lineTo(-105f * s, -15f * s)
            lineTo(-92f * s, -65f * s)
            lineTo(92f * s, -65f * s)
            lineTo(105f * s, -15f * s)
            lineTo(105f * s, 55f * s)
            close()
        }
        canvas.drawPath(bodyPath, paint)

        // 4. Roof Rack with Spare Gear
        paint.color = Color.rgb(60, 65, 75)
        strokePaint.color = Color.rgb(80, 85, 95)
        strokePaint.strokeWidth = 5f * s
        canvas.drawRoundRect(-85f * s, -88f * s, 85f * s, -68f * s, 4f * s, 4f * s, paint)
        canvas.drawLine(-80f * s, -78f * s, 80f * s, -78f * s, strokePaint)

        // Spare wheel on roof rack
        paint.color = tireColor
        canvas.drawRoundRect(-35f * s, -95f * s, 35f * s, -82f * s, 5f * s, 5f * s, paint)

        // 5. Rear Windshield Glass
        paint.color = glassColor
        val glassPath = Path().apply {
            moveTo(-85f * s, -12f * s)
            lineTo(-78f * s, -58f * s)
            lineTo(78f * s, -58f * s)
            lineTo(85f * s, -12f * s)
            close()
        }
        canvas.drawPath(glassPath, paint)

        // Glass highlight reflection
        paint.color = Color.argb(70, 255, 255, 255)
        canvas.drawLine(-65f * s, -16f * s, 65f * s, -54f * s, strokePaint.apply { strokeWidth = 3f * s })

        // 6. Rear Mounted Spare Tire (Iconic Land Cruiser feature)
        paint.color = Color.rgb(38, 42, 48)
        canvas.drawCircle(0f, 15f * s, 36f * s, paint)
        paint.color = suvDarkColor
        canvas.drawCircle(0f, 15f * s, 26f * s, paint)
        paint.color = Color.rgb(212, 175, 55) // Gold emblem
        canvas.drawCircle(0f, 15f * s, 8f * s, paint)

        // 7. Chrome Rear Bumper & Heavy Tow Hitch
        paint.color = Color.rgb(180, 185, 195)
        canvas.drawRoundRect(-108f * s, 48f * s, 108f * s, 68f * s, 6f * s, 6f * s, paint)
        paint.color = Color.rgb(40, 40, 45)
        canvas.drawRect(-12f * s, 64f * s, 12f * s, 76f * s, paint)

        // 8. Dynamic Tail / Brake Lights
        val lightColor = if (isBraking) brakeLightOn else brakeLightOff
        paint.color = lightColor
        canvas.drawRoundRect(-102f * s, -8f * s, -82f * s, 32f * s, 4f * s, 4f * s, paint)
        canvas.drawRoundRect(82f * s, -8f * s, 102f * s, 32f * s, 4f * s, 4f * s, paint)

        if (isReverse) {
            paint.color = reverseLight
            canvas.drawRect(-98f * s, 12f * s, -86f * s, 28f * s, paint)
            canvas.drawRect(86f * s, 12f * s, 98f * s, 28f * s, paint)
        }

        // Tail light glow on brake
        if (isBraking) {
            paint.color = Color.argb(110, 255, 40, 40)
            canvas.drawCircle(-92f * s, 12f * s, 26f * s, paint)
            canvas.drawCircle(92f * s, 12f * s, 26f * s, paint)
        }
    }

    private fun drawPickupShas(canvas: Canvas, s: Float, steer: Float, isBraking: Boolean, isReverse: Boolean) {
        // 1. Ground Shadow
        paint.color = Color.argb(130, 0, 0, 0)
        canvas.drawOval(-120f * s, 60f * s, 120f * s, 95f * s, paint)

        // 2. High clearance tires & mud flaps
        paint.color = tireColor
        canvas.drawRoundRect(-124f * s, 20f * s, -84f * s, 85f * s, 8f * s, 8f * s, paint)
        canvas.drawRoundRect(84f * s, 20f * s, 124f * s, 85f * s, 8f * s, 8f * s, paint)

        paint.color = Color.rgb(45, 50, 58) // Rubber Mudflaps
        canvas.drawRect(-122f * s, 75f * s, -86f * s, 95f * s, paint)
        canvas.drawRect(86f * s, 75f * s, 122f * s, 95f * s, paint)

        // 3. Shas Cabin & Steel Roll Cage
        strokePaint.color = Color.rgb(45, 52, 64)
        strokePaint.strokeWidth = 9f * s
        // Roll bar arches
        canvas.drawLine(-78f * s, -65f * s, 78f * s, -65f * s, strokePaint)
        canvas.drawLine(-78f * s, -65f * s, -78f * s, 15f * s, strokePaint)
        canvas.drawLine(78f * s, -65f * s, 78f * s, 15f * s, strokePaint)
        canvas.drawLine(-78f * s, -65f * s, 0f, 15f * s, strokePaint)
        canvas.drawLine(78f * s, -65f * s, 0f, 15f * s, strokePaint)

        // Cabin Back Window
        paint.color = glassColor
        canvas.drawRoundRect(-65f * s, -58f * s, 65f * s, -15f * s, 6f * s, 6f * s, paint)

        // Cargo Bed Side Walls
        paint.color = shasBodyColor
        canvas.drawRoundRect(-105f * s, -5f * s, 105f * s, 55f * s, 8f * s, 8f * s, paint)

        // Iconic Yemeni "TOYOTA" Tailgate Flap
        paint.color = shasDarkColor
        canvas.drawRoundRect(-96f * s, 10f * s, 96f * s, 48f * s, 4f * s, 4f * s, paint)
        textPaint.color = Color.rgb(180, 30, 30) // Red TOYOTA lettering
        textPaint.textSize = 20f * s
        canvas.drawText("TOYOTA 4WD", 0f, 36f * s, textPaint)

        // Cargo items in bed (Fuel Jerry Cans)
        paint.color = Color.rgb(190, 40, 30)
        canvas.drawRoundRect(-60f * s, -8f * s, -30f * s, 16f * s, 4f * s, 4f * s, paint)
        paint.color = Color.rgb(40, 140, 50)
        canvas.drawRoundRect(-25f * s, -8f * s, 5f * s, 16f * s, 4f * s, 4f * s, paint)

        // Heavy Steel Rear Bumper
        paint.color = Color.rgb(55, 62, 72)
        canvas.drawRoundRect(-108f * s, 50f * s, 108f * s, 68f * s, 5f * s, 5f * s, paint)

        // Lights
        val lightColor = if (isBraking) brakeLightOn else brakeLightOff
        paint.color = lightColor
        canvas.drawRect(-102f * s, 18f * s, -88f * s, 42f * s, paint)
        canvas.drawRect(88f * s, 18f * s, 102f * s, 42f * s, paint)

        if (isBraking) {
            paint.color = Color.argb(120, 255, 40, 40)
            canvas.drawCircle(-95f * s, 30f * s, 26f * s, paint)
            canvas.drawCircle(95f * s, 30f * s, 26f * s, paint)
        }
    }

    private fun drawSedan(canvas: Canvas, s: Float, steer: Float, isBraking: Boolean, isReverse: Boolean) {
        // Ground Shadow
        paint.color = Color.argb(130, 0, 0, 0)
        canvas.drawOval(-115f * s, 55f * s, 115f * s, 85f * s, paint)

        // Tires
        paint.color = tireColor
        canvas.drawRoundRect(-118f * s, 25f * s, -85f * s, 75f * s, 8f * s, 8f * s, paint)
        canvas.drawRoundRect(85f * s, 25f * s, 118f * s, 75f * s, 8f * s, 8f * s, paint)

        // Aerodynamic Body
        paint.color = sedanBodyColor
        val bodyPath = Path().apply {
            moveTo(-102f * s, 48f * s)
            lineTo(-102f * s, 5f * s)
            lineTo(-82f * s, -48f * s)
            lineTo(82f * s, -48f * s)
            lineTo(102f * s, 5f * s)
            lineTo(102f * s, 48f * s)
            close()
        }
        canvas.drawPath(bodyPath, paint)

        // Rear window
        paint.color = glassColor
        val glassPath = Path().apply {
            moveTo(-75f * s, 2f * s)
            lineTo(-68f * s, -42f * s)
            lineTo(68f * s, -42f * s)
            lineTo(75f * s, 2f * s)
            close()
        }
        canvas.drawPath(glassPath, paint)

        // Sport Rear Wing Spoiler
        paint.color = Color.rgb(20, 30, 45)
        canvas.drawRoundRect(-90f * s, -14f * s, 90f * s, -4f * s, 4f * s, 4f * s, paint)
        canvas.drawRect(-75f * s, -4f * s, -68f * s, 8f * s, paint)
        canvas.drawRect(68f * s, -4f * s, 75f * s, 8f * s, paint)

        // Dual Chrome Exhaust Tips
        paint.color = Color.rgb(215, 220, 230)
        canvas.drawCircle(-70f * s, 54f * s, 7f * s, paint)
        canvas.drawCircle(70f * s, 54f * s, 7f * s, paint)

        // Sleek LED Tail Lights
        val lightColor = if (isBraking) brakeLightOn else brakeLightOff
        paint.color = lightColor
        canvas.drawRoundRect(-98f * s, 8f * s, -62f * s, 26f * s, 6f * s, 6f * s, paint)
        canvas.drawRoundRect(62f * s, 8f * s, 98f * s, 26f * s, 6f * s, 6f * s, paint)

        if (isBraking) {
            paint.color = Color.argb(120, 255, 40, 40)
            canvas.drawCircle(-80f * s, 17f * s, 25f * s, paint)
            canvas.drawCircle(80f * s, 17f * s, 25f * s, paint)
        }
    }

    /**
     * Render the Cockpit interior view with interactive steering wheel,
     * working dials, and windshield frame.
     */
    fun drawCockpitView(canvas: Canvas, w: Float, h: Float, engine: SimulationEngine) {
        val hoodColor = when (engine.currentVehicleType) {
            SimulationEngine.VehicleType.SUV_4WD -> suvBodyColor
            SimulationEngine.VehicleType.PICKUP_SHAS -> shasBodyColor
            SimulationEngine.VehicleType.SEDAN -> sedanBodyColor
        }

        // 1. Vehicle Hood in front
        paint.color = hoodColor
        val hoodPath = Path().apply {
            moveTo(w * 0.15f, h)
            lineTo(w * 0.32f, h * 0.60f)
            lineTo(w * 0.68f, h * 0.60f)
            lineTo(w * 0.85f, h)
            close()
        }
        canvas.drawPath(hoodPath, paint)

        // 2. Windshield Pillars
        paint.color = Color.rgb(20, 24, 30)
        canvas.drawRect(0f, 0f, w * 0.12f, h, paint)
        canvas.drawRect(w * 0.88f, 0f, w, h, paint)
        canvas.drawRect(0f, 0f, w, h * 0.08f, paint) // Roof top frame

        // Rearview Mirror
        paint.color = Color.rgb(35, 42, 52)
        canvas.drawRoundRect(w * 0.42f, h * 0.08f, w * 0.58f, h * 0.18f, 10f, 10f, paint)
        paint.color = Color.rgb(65, 80, 100) // Mirror glass reflection
        canvas.drawRoundRect(w * 0.43f, h * 0.09f, w * 0.57f, h * 0.17f, 8f, 8f, paint)

        // 3. Leather Dashboard Console
        paint.color = Color.rgb(18, 22, 28)
        canvas.drawRect(0f, h * 0.72f, w, h, paint)

        // Dash Stitching Line
        strokePaint.color = Color.rgb(75, 85, 100)
        strokePaint.strokeWidth = 3f
        canvas.drawLine(w * 0.10f, h * 0.73f, w * 0.90f, h * 0.73f, strokePaint)

        // 4. Analog Instrument Cluster
        drawInstrumentCluster(canvas, w * 0.55f, h * 0.85f, engine)

        // 5. Interactive Rotating Steering Wheel
        val steerAngle = engine.steerInput * 70f
        val wheelCx = w * 0.28f
        val wheelCy = h * 0.88f

        canvas.save()
        canvas.translate(wheelCx, wheelCy)
        canvas.rotate(steerAngle)

        // Wheel Rim
        strokePaint.color = Color.rgb(38, 44, 52)
        strokePaint.strokeWidth = 26f
        canvas.drawCircle(0f, 0f, 125f, strokePaint)

        // Grip Accents
        strokePaint.color = Color.rgb(60, 70, 84)
        strokePaint.strokeWidth = 8f
        canvas.drawCircle(0f, 0f, 125f, strokePaint)

        // Center Horn Hub & Spokes
        paint.color = Color.rgb(28, 32, 40)
        canvas.drawCircle(0f, 0f, 48f, paint)
        paint.color = Color.rgb(212, 175, 55)
        canvas.drawCircle(0f, 0f, 15f, paint) // Gold emblem

        strokePaint.strokeWidth = 16f
        strokePaint.color = Color.rgb(45, 52, 64)
        canvas.drawLine(-120f, 0f, -45f, 0f, strokePaint)
        canvas.drawLine(45f, 0f, 120f, 0f, strokePaint)
        canvas.drawLine(0f, 45f, 0f, 120f, strokePaint)

        canvas.restore()
    }

    private fun drawInstrumentCluster(canvas: Canvas, cx: Float, cy: Float, engine: SimulationEngine) {
        // Speedometer Dial
        drawDial(canvas, cx - 110f, cy, 65f, "كم/س", engine.currentSpeedKmh, 180f)
        // Tachometer Dial (RPM)
        drawDial(canvas, cx + 110f, cy, 65f, "RPM", engine.currentRPM, engine.maxRPM)
    }

    private fun drawDial(canvas: Canvas, x: Float, y: Float, radius: Float, label: String, value: Float, maxValue: Float) {
        // Dial Face
        paint.color = Color.rgb(10, 14, 20)
        canvas.drawCircle(x, y, radius, paint)
        strokePaint.color = Color.rgb(90, 105, 125)
        strokePaint.strokeWidth = 4f
        canvas.drawCircle(x, y, radius, strokePaint)

        // Needle angle (-135 deg to +135 deg)
        val fraction = (value / maxValue).coerceIn(0f, 1f)
        val angleDeg = -135f + (fraction * 270f)
        val rad = Math.toRadians(angleDeg.toDouble())

        // Needle
        strokePaint.color = Color.rgb(255, 60, 60)
        strokePaint.strokeWidth = 5f
        val nx = x + cos(rad).toFloat() * (radius * 0.85f)
        val ny = y + sin(rad).toFloat() * (radius * 0.85f)
        canvas.drawLine(x, y, nx, ny, strokePaint)

        // Center Pin
        paint.color = Color.WHITE
        canvas.drawCircle(x, y, 6f, paint)

        // Label
        textPaint.color = Color.rgb(200, 210, 225)
        textPaint.textSize = 18f
        canvas.drawText("${round(value).toInt()} $label", x, y + radius + 22f, textPaint)
    }

    /**
     * Render 3D Roadside Architectural Props
     */
    fun drawRoadsideProp(canvas: Canvas, prop: RoadTrack.RoadsideProp, screenX: Float, screenY: Float, scale: Float) {
        val s = scale * prop.scale
        if (s <= 0.005f) return

        when (prop.type) {
            RoadTrack.PropType.YEMENI_TOWER_HOUSE -> drawYemeniTowerHouse(canvas, screenX, screenY, s)
            RoadTrack.PropType.MOUNTAIN_WATCHTOWER -> drawMountainWatchtower(canvas, screenX, screenY, s)
            RoadTrack.PropType.BASALT_BOULDER -> drawBasaltBoulder(canvas, screenX, screenY, s)
            RoadTrack.PropType.ARABIC_SIGN -> drawArabicRoadSign(canvas, screenX, screenY, s, prop.text)
            RoadTrack.PropType.MOUNTAIN_TREE -> drawMountainTree(canvas, screenX, screenY, s)
            else -> {}
        }
    }

    private fun drawYemeniTowerHouse(canvas: Canvas, x: Float, y: Float, s: Float) {
        val w = 240f * s
        val h = 420f * s

        // 1. Multi-story Stone Walls (Warm Yemeni Ochre Stone)
        paint.color = Color.rgb(175, 142, 110)
        canvas.drawRect(x - w * 0.5f, y - h, x + w * 0.5f, y, paint)

        // Stone Layer Bands
        strokePaint.color = Color.rgb(140, 110, 80)
        strokePaint.strokeWidth = 3f * s
        for (i in 1..4) {
            val bandY = y - (h * (i / 5f))
            canvas.drawLine(x - w * 0.5f, bandY, x + w * 0.5f, bandY, strokePaint)
        }

        // 2. Iconic Yemeni Gypsum "Qamariyah" Arched Windows
        paint.color = Color.WHITE
        val windowPaint = Paint().apply { color = Color.rgb(25, 45, 65) }
        val winW = 32f * s
        val winH = 48f * s

        for (floor in 1..4) {
            val fy = y - (h * 0.18f * floor)
            for (col in -1..1) {
                val wx = x + (col * w * 0.30f)
                // White Gypsum Trim
                canvas.drawRoundRect(wx - winW * 0.6f, fy - winH * 0.6f, wx + winW * 0.6f, fy + winH * 0.6f, 10f * s, 10f * s, paint)
                // Glass / Lattice
                canvas.drawRoundRect(wx - winW * 0.45f, fy - winH * 0.45f, wx + winW * 0.45f, fy + winH * 0.45f, 8f * s, 8f * s, windowPaint)
            }
        }

        // 3. Traditional Rooftop Battlements (شرفات حجرية بيضاء)
        val battlementCount = 5
        val bW = w / battlementCount
        for (b in 0 until battlementCount) {
            val bx = (x - w * 0.5f) + b * bW
            canvas.drawRect(bx, y - h - (22f * s), bx + bW * 0.65f, y - h, paint)
        }
    }

    private fun drawMountainWatchtower(canvas: Canvas, x: Float, y: Float, s: Float) {
        val w = 180f * s
        val h = 320f * s

        // Basalt Stone Tower
        paint.color = Color.rgb(95, 82, 70)
        canvas.drawRect(x - w * 0.5f, y - h, x + w * 0.5f, y, paint)

        // Arrow Slits
        paint.color = Color.BLACK
        canvas.drawRect(x - 8f * s, y - h * 0.7f, x + 8f * s, y - h * 0.55f, paint)
        canvas.drawRect(x - 8f * s, y - h * 0.4f, x + 8f * s, y - h * 0.25f, paint)

        // Rooftop Lookout
        paint.color = Color.rgb(75, 65, 55)
        canvas.drawRect(x - w * 0.55f, y - h - (18f * s), x + w * 0.55f, y - h, paint)
    }

    private fun drawBasaltBoulder(canvas: Canvas, x: Float, y: Float, s: Float) {
        paint.color = Color.rgb(110, 88, 70)
        val r = 90f * s
        canvas.drawOval(x - r, y - r * 1.3f, x + r, y, paint)
        paint.color = Color.rgb(80, 62, 48)
        canvas.drawOval(x - r * 0.6f, y - r * 1.1f, x + r * 0.8f, y - r * 0.2f, paint)
    }

    private fun drawArabicRoadSign(canvas: Canvas, x: Float, y: Float, s: Float, text: String) {
        val signW = 320f * s
        val signH = 110f * s

        // Steel Post
        paint.color = Color.rgb(140, 145, 155)
        canvas.drawRect(x - 8f * s, y - signH * 1.6f, x + 8f * s, y, paint)

        // Signboard (Highland Green with White Border)
        paint.color = Color.rgb(22, 101, 52)
        canvas.drawRoundRect(x - signW * 0.5f, y - signH * 1.6f, x + signW * 0.5f, y - signH * 0.5f, 14f * s, 14f * s, paint)

        strokePaint.color = Color.WHITE
        strokePaint.strokeWidth = 4f * s
        canvas.drawRoundRect(x - signW * 0.48f, y - signH * 1.56f, x + signW * 0.48f, y - signH * 0.54f, 10f * s, 10f * s, strokePaint)

        // Arabic Warning Text
        textPaint.color = Color.WHITE
        textPaint.textSize = 28f * s
        canvas.drawText(text, x, y - signH * 0.95f, textPaint)
    }

    private fun drawMountainTree(canvas: Canvas, x: Float, y: Float, s: Float) {
        // Trunk
        paint.color = Color.rgb(85, 65, 45)
        canvas.drawRect(x - 12f * s, y - 110f * s, x + 12f * s, y, paint)

        // Umbrella Crown (Mountain Acacia)
        paint.color = Color.rgb(45, 125, 55)
        canvas.drawOval(x - 90f * s, y - 180f * s, x + 90f * s, y - 90f * s, paint)
        paint.color = Color.rgb(35, 105, 45)
        canvas.drawOval(x - 65f * s, y - 165f * s, x + 65f * s, y - 100f * s, paint)
    }
}
