using UnityEngine;

namespace Nageel.Weather
{
    public enum WeatherType
    {
        ClearDay,       // طقس مشمس صافٍ
        HighlandFog,    // ضباب المرتفعات الجبلية
        MountainRain,   // أمطار جبلية ورعد
        DustWindWadi    // رياح وغبار الوادي
    }

    public enum TimeOfDayPeriod
    {
        MorningDawn,
        Daylight,
        SunsetMaghrib,
        Night
    }

    public class DayNightWeatherSystem : MonoBehaviour
    {
        [Header("Sun & Sky Lighting")]
        public Light directionalSunLight;
        [Range(0f, 24f)] public float timeOfDayHours = 14.5f; // Starts at afternoon
        public float timeScale = 0.5f; // Cycle speed multiplier
        public bool autoAdvanceTime = true;

        [Header("Atmospheric Weather")]
        public WeatherType currentWeather = WeatherType.ClearDay;
        public ParticleSystem rainParticleSystem;
        public ParticleSystem dustParticleSystem;

        [Header("Fog & Atmosphere Tuning")]
        public float baseFogDensity = 0.0015f;
        public float highlandFogDensity = 0.012f;
        public float rainFogDensity = 0.008f;

        [Header("Headlights & Vehicle Emissive")]
        public bool isHeadlightsRequired = false;

        private void Start()
        {
            ApplyAtmosphericSettings();
        }

        private void Update()
        {
            if (autoAdvanceTime)
            {
                timeOfDayHours += (Time.deltaTime * timeScale) / 3600f * 24f;
                if (timeOfDayHours >= 24f) timeOfDayHours -= 24f;
            }

            UpdateSunRotation();
            UpdateFogAndAmbient();
        }

        private void UpdateSunRotation()
        {
            if (directionalSunLight == null) return;

            // 0h = -90 (midnight), 6h = 0 (sunrise), 12h = 90 (noon), 18h = 180 (sunset)
            float sunPitch = ((timeOfDayHours / 24f) * 360f) - 90f;
            directionalSunLight.transform.rotation = Quaternion.Euler(sunPitch, 160f, 0f);

            // Sunlight intensity and color transitions
            if (timeOfDayHours >= 6f && timeOfDayHours < 8f)
            {
                // Dawn: Soft Golden Pink
                float t = (timeOfDayHours - 6f) / 2f;
                directionalSunLight.intensity = Mathf.Lerp(0.1f, 1.1f, t);
                directionalSunLight.color = Color.Lerp(new Color(1f, 0.6f, 0.4f), new Color(1f, 0.95f, 0.85f), t);
                isHeadlightsRequired = false;
            }
            else if (timeOfDayHours >= 8f && timeOfDayHours < 17f)
            {
                // Day: Clear bright desert sun
                directionalSunLight.intensity = 1.35f;
                directionalSunLight.color = new Color(1f, 0.98f, 0.92f);
                isHeadlightsRequired = false;
            }
            else if (timeOfDayHours >= 17f && timeOfDayHours < 19.5f)
            {
                // Sunset (Maghrib): Deep Yemen mountain amber/orange
                float t = (timeOfDayHours - 17f) / 2.5f;
                directionalSunLight.intensity = Mathf.Lerp(1.35f, 0.2f, t);
                directionalSunLight.color = Color.Lerp(new Color(1f, 0.75f, 0.4f), new Color(0.95f, 0.42f, 0.15f), t);
                isHeadlightsRequired = (t > 0.6f);
            }
            else
            {
                // Night: Cool deep blue moonlight
                directionalSunLight.intensity = 0.08f;
                directionalSunLight.color = new Color(0.2f, 0.35f, 0.6f);
                isHeadlightsRequired = true;
            }
        }

        private void UpdateFogAndAmbient()
        {
            RenderSettings.fog = true;

            float targetFogDensity = baseFogDensity;
            Color targetFogColor = directionalSunLight != null ? directionalSunLight.color * 0.75f : Color.gray;

            switch (currentWeather)
            {
                case WeatherType.ClearDay:
                    targetFogDensity = baseFogDensity;
                    break;
                case WeatherType.HighlandFog:
                    targetFogDensity = highlandFogDensity;
                    targetFogColor = Color.Lerp(targetFogColor, new Color(0.85f, 0.88f, 0.92f), 0.8f);
                    break;
                case WeatherType.MountainRain:
                    targetFogDensity = rainFogDensity;
                    targetFogColor = new Color(0.35f, 0.38f, 0.45f);
                    break;
                case WeatherType.DustWindWadi:
                    targetFogDensity = baseFogDensity * 3.5f;
                    targetFogColor = new Color(0.78f, 0.68f, 0.52f);
                    break;
            }

            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, targetFogDensity, Time.deltaTime * 1.5f);
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetFogColor, Time.deltaTime * 1.5f);
        }

        public void SetWeather(WeatherType newWeather)
        {
            currentWeather = newWeather;
            ApplyAtmosphericSettings();
        }

        public void CycleWeather()
        {
            int next = ((int)currentWeather + 1) % 4;
            SetWeather((WeatherType)next);
        }

        private void ApplyAtmosphericSettings()
        {
            if (rainParticleSystem != null)
            {
                if (currentWeather == WeatherType.MountainRain)
                {
                    if (!rainParticleSystem.isPlaying) rainParticleSystem.Play();
                }
                else
                {
                    if (rainParticleSystem.isPlaying) rainParticleSystem.Stop();
                }
            }

            if (dustParticleSystem != null)
            {
                if (currentWeather == WeatherType.DustWindWadi)
                {
                    if (!dustParticleSystem.isPlaying) dustParticleSystem.Play();
                }
                else
                {
                    if (dustParticleSystem.isPlaying) dustParticleSystem.Stop();
                }
            }
        }

        public TimeOfDayPeriod GetCurrentPeriod()
        {
            if (timeOfDayHours >= 5.5f && timeOfDayHours < 8.0f) return TimeOfDayPeriod.MorningDawn;
            if (timeOfDayHours >= 8.0f && timeOfDayHours < 17.0f) return TimeOfDayPeriod.Daylight;
            if (timeOfDayHours >= 17.0f && timeOfDayHours < 19.5f) return TimeOfDayPeriod.SunsetMaghrib;
            return TimeOfDayPeriod.Night;
        }

        public string GetArabicWeatherDescription()
        {
            switch (currentWeather)
            {
                case WeatherType.ClearDay: return "صافٍ ومشمس";
                case WeatherType.HighlandFog: return "ضباب كثيف بالمرتفعات";
                case WeatherType.MountainRain: return "أمطار جبلية ورياح";
                case WeatherType.DustWindWadi: return "عواصف رملية بالوادي";
                default: return "معتدل";
            }
        }
    }
}
