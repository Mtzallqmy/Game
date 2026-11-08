using UnityEngine;
using Nageel.Weather;
using Nageel.Environment;
using Nageel.Vehicle;

namespace Nageel.Audio
{
    public class EnvironmentAudioManager : MonoBehaviour
    {
        [Header("Audio Sources")]
        public AudioSource mountainWindSource;
        public AudioSource rainWeatherSource;
        public AudioSource surfaceFrictionSource;
        public AudioSource villageAmbienceSource;

        [Header("System References")]
        public NageelCarController carController;
        public DayNightWeatherSystem weatherSystem;

        private void Start()
        {
            SetupAudioSources();
        }

        private void SetupAudioSources()
        {
            if (mountainWindSource == null) mountainWindSource = CreateLoopingSource("MountainWindSource", 0.35f);
            if (rainWeatherSource == null) rainWeatherSource = CreateLoopingSource("RainWeatherSource", 0.45f);
            if (surfaceFrictionSource == null) surfaceFrictionSource = CreateLoopingSource("SurfaceFrictionSource", 0.40f);
            if (villageAmbienceSource == null) villageAmbienceSource = CreateLoopingSource("VillageAmbienceSource", 0.20f);

            // Procedural generation if audio clips are null
            SynthesizeLoopClips();
        }

        private AudioSource CreateLoopingSource(string name, float defaultVolume)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(transform, false);
            AudioSource src = obj.AddComponent<AudioSource>();
            src.loop = true;
            src.playOnAwake = false;
            src.volume = defaultVolume;
            return src;
        }

        private void SynthesizeLoopClips()
        {
            int sampleRate = 22050;

            // 1. Mountain Wind (low pass pink noise)
            if (mountainWindSource.clip == null)
            {
                int count = sampleRate * 2;
                float[] windData = new float[count];
                float b0 = 0f, b1 = 0f;
                for (int i = 0; i < count; i++)
                {
                    float white = Random.value * 2f - 1f;
                    b0 = 0.96f * b0 + white * 0.04f;
                    b1 = 0.92f * b1 + b0 * 0.08f;
                    windData[i] = b1 * 0.35f;
                }
                AudioClip windClip = AudioClip.Create("ProceduralMountainWind", count, 1, sampleRate, false);
                windClip.SetData(windData, 0);
                mountainWindSource.clip = windClip;
                mountainWindSource.Play();
            }

            // 2. Rain Ambience
            if (rainWeatherSource.clip == null)
            {
                int count = sampleRate * 2;
                float[] rainData = new float[count];
                for (int i = 0; i < count; i++)
                {
                    rainData[i] = (Random.value * 2f - 1f) * 0.25f;
                }
                AudioClip rainClip = AudioClip.Create("ProceduralRainfall", count, 1, sampleRate, false);
                rainClip.SetData(rainData, 0);
                rainWeatherSource.clip = rainClip;
                rainWeatherSource.Play();
            }

            // 3. Wadi Gravel Crunch / Surface Friction
            if (surfaceFrictionSource.clip == null)
            {
                int count = sampleRate * 1;
                float[] surfData = new float[count];
                for (int i = 0; i < count; i++)
                {
                    surfData[i] = (Random.value * 2f - 1f) * 0.15f;
                }
                AudioClip surfClip = AudioClip.Create("ProceduralSurfaceFriction", count, 1, sampleRate, false);
                surfClip.SetData(surfData, 0);
                surfaceFrictionSource.clip = surfClip;
                surfaceFrictionSource.Play();
            }
        }

        private void Update()
        {
            if (carController == null)
            {
                carController = FindObjectOfType<NageelCarController>();
                if (carController == null) return;
            }

            float speedKmh = carController.currentSpeedKmh;
            float zDist = carController.transform.position.z;
            float altitude = carController.transform.position.y;

            // 1. Wind modulation: Increases with altitude and car speed
            float targetWindVol = Mathf.Clamp(0.15f + (speedKmh / 140f) * 0.45f + (altitude / 1000f) * 0.25f, 0.1f, 0.85f);
            mountainWindSource.volume = Mathf.Lerp(mountainWindSource.volume, targetWindVol, Time.deltaTime * 2f);
            mountainWindSource.pitch = Mathf.Lerp(mountainWindSource.pitch, 0.8f + (speedKmh / 120f) * 0.4f, Time.deltaTime * 2f);

            // 2. Rain modulation: High volume during MountainRain, low otherwise
            bool isRaining = weatherSystem != null && weatherSystem.currentWeather == WeatherType.MountainRain;
            float targetRainVol = isRaining ? 0.70f : 0.0f;
            rainWeatherSource.volume = Mathf.Lerp(rainWeatherSource.volume, targetRainVol, Time.deltaTime * 3f);

            // 3. Surface Friction: Extra crunchy gravel in Zone 3 (Rocky Wadi Valley)
            bool isInWadi = zDist > 7000f;
            float speedRatio = Mathf.Clamp01(speedKmh / 80f);
            float targetFrictionVol = speedRatio * (isInWadi ? 0.65f : 0.25f);
            surfaceFrictionSource.volume = Mathf.Lerp(surfaceFrictionSource.volume, targetFrictionVol, Time.deltaTime * 4f);
            surfaceFrictionSource.pitch = isInWadi ? 0.85f : 1.25f;

            // 4. Village Ambience: Active when near Zone 2 Highland village (3,500m to 7,000m)
            bool isNearVillage = zDist >= 3500f && zDist <= 7000f;
            float targetVillageVol = isNearVillage ? 0.30f : 0.0f;
            if (villageAmbienceSource != null)
            {
                villageAmbienceSource.volume = Mathf.Lerp(villageAmbienceSource.volume, targetVillageVol, Time.deltaTime * 2f);
            }
        }
    }
}
