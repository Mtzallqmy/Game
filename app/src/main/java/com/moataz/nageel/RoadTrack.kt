package com.moataz.nageel

import android.graphics.Color
import kotlin.math.*

/**
 * 3D Segmented Mountain Road and Environment Network for Nageel
 * Implements a continuous 10.5 km route with authentic curves, elevations,
 * Yemeni architectural roadside props, and true perspective projection.
 */
class RoadTrack {

    data class Point3D(
        var worldX: Float = 0f,
        var worldY: Float = 0f,
        var worldZ: Float = 0f,
        var screenX: Float = 0f,
        var screenY: Float = 0f,
        var screenW: Float = 0f,
        var scale: Float = 0f
    ) {
        fun project(camX: Float, camY: Float, camZ: Float, camDepth: Float, screenWidth: Float, screenHeight: Float, roadWidth: Float) {
            val transX = worldX - camX
            val transY = worldY - camY
            val transZ = worldZ - camZ

            scale = if (transZ > 0) camDepth / transZ else 0.0001f
            screenX = (screenWidth * 0.5f) + (scale * transX * screenWidth * 0.5f)
            screenY = (screenHeight * 0.5f) - (scale * transY * screenHeight * 0.5f)
            screenW = scale * roadWidth * (screenWidth * 0.5f)
        }
    }

    enum class PropType {
        YEMENI_TOWER_HOUSE,
        MOUNTAIN_WATCHTOWER,
        BASALT_BOULDER,
        TERRACE_WALL,
        ARABIC_SIGN,
        MOUNTAIN_TREE
    }

    data class RoadsideProp(
        val type: PropType,
        val offset: Float, // Negative = left of road, Positive = right of road
        val scale: Float = 1.0f,
        val text: String = ""
    )

    data class Segment(
        val index: Int,
        val p1: Point3D,
        val p2: Point3D,
        val curve: Float,
        val zone: SimulationEngine.WorldZone,
        val hasGuardrailLeft: Boolean,
        val hasGuardrailRight: Boolean,
        val props: MutableList<RoadsideProp> = mutableListOf(),
        // Colors
        val grassColor: Int,
        val rumbleColor: Int,
        val roadColor: Int,
        val laneLineColor: Int
    )

    val segments = ArrayList<Segment>(1500)
    val segmentLength = 7.0f // Each segment is 7 meters long
    val roadWidth = 2000.0f  // Half-road width in world units
    val totalSegments = 1500 // 1,500 * 7m = 10,500m (10.5 km)

    init {
        buildWorldTrack()
    }

    private fun buildWorldTrack() {
        segments.clear()

        for (i in 0 until totalSegments) {
            val z1 = i * segmentLength
            val z2 = (i + 1) * segmentLength

            // Determine zone
            val zone = when {
                z1 < 3500f -> SimulationEngine.WorldZone.NAQIL_PASS
                z1 < 7000f -> SimulationEngine.WorldZone.GREEN_HIGHLANDS
                else -> SimulationEngine.WorldZone.ROCKY_WADI
            }

            // Elevation profile (Y coordinate in world space)
            val y1 = calculateHeight(z1, zone)
            val y2 = calculateHeight(z2, zone)

            // Curvature (curves and hairpins)
            val curve = calculateCurvature(i, zone)

            // Alternating rumble & road shades
            val isEven = (i / 3) % 2 == 0

            val grassColor: Int
            val rumbleColor: Int
            val roadColor: Int
            val laneLineColor: Int

            when (zone) {
                SimulationEngine.WorldZone.NAQIL_PASS -> {
                    grassColor = if (isEven) Color.rgb(92, 75, 60) else Color.rgb(108, 88, 70)
                    rumbleColor = if (isEven) Color.rgb(220, 38, 38) else Color.rgb(250, 250, 250)
                    roadColor = if (isEven) Color.rgb(42, 44, 50) else Color.rgb(50, 52, 58)
                    laneLineColor = Color.rgb(250, 204, 21) // Bright mountain yellow
                }
                SimulationEngine.WorldZone.GREEN_HIGHLANDS -> {
                    grassColor = if (isEven) Color.rgb(38, 120, 48) else Color.rgb(48, 140, 58)
                    rumbleColor = if (isEven) Color.rgb(245, 158, 11) else Color.rgb(255, 255, 255)
                    roadColor = if (isEven) Color.rgb(52, 56, 62) else Color.rgb(60, 64, 70)
                    laneLineColor = Color.rgb(245, 245, 245) // Crisp highland white
                }
                SimulationEngine.WorldZone.ROCKY_WADI -> {
                    grassColor = if (isEven) Color.rgb(185, 115, 75) else Color.rgb(170, 100, 65)
                    rumbleColor = if (isEven) Color.rgb(140, 90, 60) else Color.rgb(210, 160, 110)
                    roadColor = if (isEven) Color.rgb(165, 135, 102) else Color.rgb(150, 122, 90)
                    laneLineColor = Color.argb(120, 110, 85, 60) // Wadi tire tracks
                }
            }

            // Guardrails on cliff edge (left side climbing up Naqil pass)
            val hasGuardLeft = (zone == SimulationEngine.WorldZone.NAQIL_PASS) || (zone == SimulationEngine.WorldZone.GREEN_HIGHLANDS && i % 40 < 25)
            val hasGuardRight = (zone == SimulationEngine.WorldZone.NAQIL_PASS && i % 60 < 20)

            val p1 = Point3D(0f, y1, z1)
            val p2 = Point3D(0f, y2, z2)

            val seg = Segment(
                index = i,
                p1 = p1,
                p2 = p2,
                curve = curve,
                zone = zone,
                hasGuardrailLeft = hasGuardLeft,
                hasGuardrailRight = hasGuardRight,
                grassColor = grassColor,
                rumbleColor = rumbleColor,
                roadColor = roadColor,
                laneLineColor = laneLineColor
            )

            // Populate roadside props
            populatePropsForSegment(seg, i, zone)

            segments.add(seg)
        }

        // Apply progressive curve displacement to worldX
        var currentX = 0f
        var currentDX = 0f
        for (seg in segments) {
            seg.p1.worldX = currentX
            currentDX += seg.curve
            currentX += currentDX
            seg.p2.worldX = currentX
        }
    }

    private fun calculateHeight(z: Float, zone: SimulationEngine.WorldZone): Float {
        return when (zone) {
            SimulationEngine.WorldZone.NAQIL_PASS -> {
                // Steep climb from 0 to 1,800 units with undulating steep switchbacks
                (z * 0.42f) + (sin(z * 0.003f) * 120f)
            }
            SimulationEngine.WorldZone.GREEN_HIGHLANDS -> {
                // High highland plateau 1,500 units with rolling agricultural hills
                1470f + (sin((z - 3500f) * 0.004f) * 160f) + (cos((z - 3500f) * 0.008f) * 70f)
            }
            SimulationEngine.WorldZone.ROCKY_WADI -> {
                // Valley canyon descent down to 400 units
                1470f - ((z - 7000f) * 0.32f) + (sin((z - 7000f) * 0.005f) * 90f)
            }
        }
    }

    private fun calculateCurvature(index: Int, zone: SimulationEngine.WorldZone): Float {
        return when (zone) {
            SimulationEngine.WorldZone.NAQIL_PASS -> {
                // Sharp mountain hairpins (منعطفات سمارة الشهيرة)
                val period = index % 120
                when {
                    period in 20..50 -> 3.2f   // Hard right hairpin
                    period in 70..100 -> -3.2f // Hard left hairpin
                    else -> 0.0f
                }
            }
            SimulationEngine.WorldZone.GREEN_HIGHLANDS -> {
                // Sweeping scenic curves around agricultural terraces
                val period = index % 150
                when {
                    period in 25..65 -> -1.8f  // Gentle sweeping left
                    period in 95..135 -> 1.8f  // Sweeping right
                    else -> 0.0f
                }
            }
            SimulationEngine.WorldZone.ROCKY_WADI -> {
                // S-bends through the canyon floor around boulders
                val period = index % 80
                when {
                    period in 15..35 -> 2.4f
                    period in 45..65 -> -2.4f
                    else -> 0.0f
                }
            }
        }
    }

    private fun populatePropsForSegment(seg: Segment, i: Int, zone: SimulationEngine.WorldZone) {
        when (zone) {
            SimulationEngine.WorldZone.NAQIL_PASS -> {
                // Yemeni Fortresses & Watchtowers on high cliffs
                if (i % 85 == 15) {
                    seg.props.add(RoadsideProp(PropType.MOUNTAIN_WATCHTOWER, offset = -2.4f, scale = 1.3f))
                }
                if (i % 65 == 40) {
                    seg.props.add(RoadsideProp(PropType.BASALT_BOULDER, offset = 1.9f, scale = 1.1f))
                }
                // Arabic Road Signs
                if (i == 30) {
                    seg.props.add(RoadsideProp(PropType.ARABIC_SIGN, offset = -1.6f, text = "⚠️ نقيل سمارة: هدأ السرعة"))
                } else if (i == 180) {
                    seg.props.add(RoadsideProp(PropType.ARABIC_SIGN, offset = -1.6f, text = "حصن سمارة التاريخي — 2800م"))
                } else if (i == 350) {
                    seg.props.add(RoadsideProp(PropType.ARABIC_SIGN, offset = -1.6f, text = "منعطفات حادة — التزم بمسارك"))
                }
            }
            SimulationEngine.WorldZone.GREEN_HIGHLANDS -> {
                // Traditional Yemeni Multi-Story Stone Tower Houses with Qamariyah windows
                if (i % 55 == 10) {
                    seg.props.add(RoadsideProp(PropType.YEMENI_TOWER_HOUSE, offset = 2.2f, scale = 1.25f))
                }
                if (i % 70 == 35) {
                    seg.props.add(RoadsideProp(PropType.YEMENI_TOWER_HOUSE, offset = -2.3f, scale = 1.15f))
                }
                if (i % 25 == 5) {
                    seg.props.add(RoadsideProp(PropType.MOUNTAIN_TREE, offset = -1.8f, scale = 0.9f))
                }
                if (i % 30 == 18) {
                    seg.props.add(RoadsideProp(PropType.MOUNTAIN_TREE, offset = 1.8f, scale = 1.0f))
                }
                // Arabic Road Signs
                if (i == 510) {
                    seg.props.add(RoadsideProp(PropType.ARABIC_SIGN, offset = -1.6f, text = "🌿 مدرجات إب الخضراء"))
                } else if (i == 750) {
                    seg.props.add(RoadsideProp(PropType.ARABIC_SIGN, offset = -1.6f, text = "قرية جبلية — انتبه للمشاة"))
                }
            }
            SimulationEngine.WorldZone.ROCKY_WADI -> {
                // Canyon Boulders and Acacia Trees
                if (i % 35 == 8) {
                    seg.props.add(RoadsideProp(PropType.BASALT_BOULDER, offset = -1.9f, scale = 1.3f))
                }
                if (i % 45 == 22) {
                    seg.props.add(RoadsideProp(PropType.BASALT_BOULDER, offset = 2.0f, scale = 1.4f))
                }
                if (i % 50 == 12) {
                    seg.props.add(RoadsideProp(PropType.MOUNTAIN_TREE, offset = 2.2f, scale = 0.85f))
                }
                // Arabic Road Signs
                if (i == 1020) {
                    seg.props.add(RoadsideProp(PropType.ARABIC_SIGN, offset = -1.6f, text = "وادي بَنَا — خطر السيول المفاجئة 🌊"))
                } else if (i == 1250) {
                    seg.props.add(RoadsideProp(PropType.ARABIC_SIGN, offset = -1.6f, text = "طريق صخري — استخدم 4WD-L"))
                } else if (i == 1450) {
                    seg.props.add(RoadsideProp(PropType.ARABIC_SIGN, offset = -1.6f, text = "نهاية المسار — أحسنت الوصول 🏁"))
                }
            }
        }
    }

    fun findSegment(positionZ: Float): Segment {
        var idx = (floor(positionZ / segmentLength) % totalSegments).toInt()
        if (idx < 0) idx += totalSegments
        return segments[idx]
    }
}
