using UnityEngine;
using Nageel.Vehicle;

namespace Nageel.Recovery
{
    /// <summary>
    /// Audible synthesized engine placeholder for the proof-of-drive scene.
    /// Replace with licensed real engine samples before the art-quality milestone.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class RecoveryEngineSynth : MonoBehaviour
    {
        public NageelCarController car;
        private AudioSource source;
        private AudioClip clip;
        private double phase;
        private const int SampleRate = 44100;

        private void Awake()
        {
            if (car == null) car = GetComponent<NageelCarController>();
            source = GetComponent<AudioSource>();
            source.spatialBlend = 0f;
            source.volume = 0.35f;
            source.loop = true;
            clip = AudioClip.Create("Nageel_ProceduralEngine_Placeholder", SampleRate, 1,
                SampleRate, true, GenerateSamples);
            source.clip = clip;
        }

        private void OnEnable() { if (source != null && source.clip != null) source.Play(); }
        private void OnDisable() { if (source != null) source.Stop(); }

        private void GenerateSamples(float[] data)
        {
            float rpm = car != null ? car.currentRPM : 800f;
            double frequency = Mathf.Clamp(rpm / 60f * 2f, 28f, 210f);
            double increment = 2.0 * System.Math.PI * frequency / SampleRate;
            for (int i = 0; i < data.Length; i++)
            {
                phase += increment;
                if (phase >= 2.0 * System.Math.PI) phase -= 2.0 * System.Math.PI;
                data[i] = (float)(0.18 * System.Math.Sin(phase)
                    + 0.07 * System.Math.Sin(phase * 2.0)
                    + 0.03 * System.Math.Sin(phase * 0.5));
            }
        }
    }
}
