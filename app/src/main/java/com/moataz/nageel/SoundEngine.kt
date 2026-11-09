package com.moataz.nageel

import android.media.AudioAttributes
import android.media.AudioFormat
import android.media.AudioTrack
import java.util.Random
import kotlin.math.*

/**
 * Procedural Real-time Audio Engine for Nageel Mountain Pass
 * Synthesizes dynamic V6/V8 engine sound, gear shift burble, tire screeches,
 * and crash effects via Android AudioTrack without requiring external audio files.
 */
class SoundEngine {

    private var audioTrack: AudioTrack? = null
    private var isPlaying = false
    private var soundThread: Thread? = null

    // Audio parameters (controlled by simulation physics)
    @Volatile var targetRPM: Float = 800f
    @Volatile var throttle: Float = 0f
    @Volatile var isBraking: Boolean = false
    @Volatile var isDrifting: Boolean = false
    @Volatile var isMuted: Boolean = false

    private val sampleRate = 22050
    private val bufferSize = AudioTrack.getMinBufferSize(
        sampleRate,
        AudioFormat.CHANNEL_OUT_MONO,
        AudioFormat.ENCODING_PCM_16BIT
    ).coerceAtLeast(2048)

    fun start() {
        if (isPlaying) return
        try {
            audioTrack = AudioTrack.Builder()
                .setAudioAttributes(
                    AudioAttributes.Builder()
                        .setUsage(AudioAttributes.USAGE_GAME)
                        .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION)
                        .build()
                )
                .setAudioFormat(
                    AudioFormat.Builder()
                        .setEncoding(AudioFormat.ENCODING_PCM_16BIT)
                        .setSampleRate(sampleRate)
                        .setChannelMask(AudioFormat.CHANNEL_OUT_MONO)
                        .build()
                )
                .setBufferSizeInBytes(bufferSize)
                .setTransferMode(AudioTrack.MODE_STREAM)
                .build()

            audioTrack?.play()
            isPlaying = true

            soundThread = Thread({ generateAudioLoop() }, "NageelSoundEngine").apply {
                priority = Thread.NORM_PRIORITY
                start()
            }
        } catch (e: Exception) {
            e.printStackTrace()
        }
    }

    fun stop() {
        isPlaying = false
        try {
            soundThread?.join(500)
        } catch (ignored: Exception) {}
        try {
            audioTrack?.stop()
            audioTrack?.release()
        } catch (ignored: Exception) {}
        audioTrack = null
    }

    private fun generateAudioLoop() {
        val shortBuffer = ShortArray(bufferSize / 2)
        var phase = 0.0
        var phaseSub = 0.0
        var screechPhase = 0.0
        var smoothedRPM = 800f
        val random = Random()

        while (isPlaying) {
            if (isMuted) {
                shortBuffer.fill(0)
                audioTrack?.write(shortBuffer, 0, shortBuffer.size)
                continue
            }

            // Smooth RPM transition
            smoothedRPM += (targetRPM - smoothedRPM) * 0.15f
            val baseFreq = 28.0 + (smoothedRPM / 6000.0) * 165.0
            val throttleGain = 0.35 + (throttle * 0.45)

            for (i in shortBuffer.indices) {
                // Fundamental engine pulse + harmonics (V8 rumble)
                val fundamental = sin(phase)
                val subHarmonic = sin(phaseSub) * 0.6
                val secondHarmonic = sin(phase * 2.0) * 0.4
                val thirdHarmonic = sin(phase * 3.0) * 0.25
                val exhaustGrit = (random.nextFloat() * 2f - 1f) * (0.08f + throttle * 0.12f)

                var sample = (fundamental * 0.5 + subHarmonic + secondHarmonic + thirdHarmonic + exhaustGrit) * throttleGain

                // Tire screech synthesis when drifting or locking brakes
                if (isDrifting || (isBraking && smoothedRPM > 2200f)) {
                    val screechNoise = (random.nextFloat() * 2f - 1f)
                    val screechSine = sin(screechPhase)
                    val screechMod = (screechSine * 0.5 + screechNoise * 0.5) * 0.35
                    sample += screechMod
                    screechPhase += (2.0 * Math.PI * 1800.0) / sampleRate
                }

                // Soft clipping limiter
                val clamped = sample.coerceIn(-1.0, 1.0)
                shortBuffer[i] = (clamped * 16000.0).toInt().toShort()

                val freqStep = (2.0 * Math.PI * baseFreq) / sampleRate
                phase = (phase + freqStep) % (2.0 * Math.PI)
                phaseSub = (phaseSub + freqStep * 0.5) % (2.0 * Math.PI)
            }

            audioTrack?.write(shortBuffer, 0, shortBuffer.size)
        }
    }
}
