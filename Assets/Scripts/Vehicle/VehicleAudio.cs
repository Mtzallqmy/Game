using UnityEngine;

namespace Nageel.Vehicle
{
    [RequireComponent(typeof(AudioSource))]
    public class VehicleAudio : MonoBehaviour
    {
        public NageelCarController carController;

        [Header("Audio Tuning")]
        public float idlePitch = 0.75f;
        public float maxPitch = 2.3f;
        public float minVolume = 0.25f;
        public float maxVolume = 0.85f;

        [Header("Tire Skid Audio")]
        public AudioSource skidAudioSource;

        private AudioSource engineAudio;

        private void Awake()
        {
            engineAudio = GetComponent<AudioSource>();
            if (carController == null)
            {
                carController = GetComponentInParent<NageelCarController>();
            }

            ConfigureProceduralEngineSound();
        }

        private void ConfigureProceduralEngineSound()
        {
            // If no AudioClip is assigned, synthesize a procedural combustion engine loop
            if (engineAudio.clip == null)
            {
                int sampleRate = 44100;
                float duration = 1.0f;
                int sampleCount = (int)(sampleRate * duration);
                float[] samples = new float[sampleCount];

                // Generate a rich multi-harmonic rumble simulating a V6/V8 cylinder firing cycle
                for (int i = 0; i < sampleCount; i++)
                {
                    float t = (float)i / sampleRate;
                    float freq = 45f; // fundamental engine idle frequency (Hz)
                    float s1 = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.45f;
                    float s2 = Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.30f;
                    float s3 = Mathf.Sin(2f * Mathf.PI * freq * 3f * t) * 0.15f;
                    float noise = (Random.value * 2f - 1f) * 0.10f; // mechanical cylinder noise
                    samples[i] = s1 + s2 + s3 + noise;
                }

                AudioClip clip = AudioClip.Create("ProceduralV8Engine", sampleCount, 1, sampleRate, false);
                clip.SetData(samples, 0);
                engineAudio.clip = clip;
                engineAudio.loop = true;
                engineAudio.Play();
            }
        }

        private void Update()
        {
            if (carController == null || engineAudio == null) return;

            // Pitch linked to RPM
            float rpmRatio = (carController.currentRPM - carController.idleRPM) / (carController.maxRPM - carController.idleRPM);
            rpmRatio = Mathf.Clamp01(rpmRatio);

            float targetPitch = Mathf.Lerp(idlePitch, maxPitch, rpmRatio);
            engineAudio.pitch = Mathf.Lerp(engineAudio.pitch, targetPitch, Time.deltaTime * 8f);

            // Volume modulated by RPM and speed
            float targetVolume = Mathf.Lerp(minVolume, maxVolume, rpmRatio);
            engineAudio.volume = Mathf.Lerp(engineAudio.volume, targetVolume, Time.deltaTime * 6f);

            // Tire skid audio when drifting or locking brakes
            if (skidAudioSource != null)
            {
                bool isSkidding = carController.isHandbrakeActive && carController.currentSpeedKmh > 15f;
                skidAudioSource.volume = Mathf.Lerp(skidAudioSource.volume, isSkidding ? 0.7f : 0f, Time.deltaTime * 10f);
            }
        }
    }
}
