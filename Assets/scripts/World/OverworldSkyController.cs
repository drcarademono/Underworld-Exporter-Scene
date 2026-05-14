using UnityEngine;

public class OverworldSkyController : MonoBehaviour
{
    [Header("Dynamic Skies Assets")]
    public Material DaySkyboxMaterial;
    public Material NightSkyboxMaterial;

    [Header("Time of Day")]
    [Range(0f, 24f)] public float SunriseHour = 6f;
    [Range(0f, 24f)] public float SunsetHour = 19f;
    [Range(0.1f, 4f)] public float DawnDuskBlendHours = 1f;

    [Header("Weather")]
    public bool EnableWeather = true;
    public float MinWeatherDurationSeconds = 90f;
    public float MaxWeatherDurationSeconds = 240f;
    [Range(0f, 1f)] public float RainChance = 0.18f;
    [Range(0f, 1f)] public float FogChance = 0.12f;

    [Header("Sun")]
    public Light SunLight;
    public Gradient SunColorByDay;
    public AnimationCurve SunIntensityByDay;

    private enum WeatherState { Clear, Rain, Fog }
    private WeatherState weatherState = WeatherState.Clear;
    private float weatherTimer;

    private Material dayInstance;
    private Material nightInstance;

    public void Initialize(Material dayMat, Material nightMat, Light sun)
    {
        if (dayMat != null)
        {
            dayInstance = new Material(dayMat);
        }
        if (nightMat != null)
        {
            nightInstance = new Material(nightMat);
        }
        DaySkyboxMaterial = dayInstance;
        NightSkyboxMaterial = nightInstance;
        SunLight = sun;

        if (SunColorByDay == null || SunColorByDay.colorKeys.Length == 0)
        {
            SunColorByDay = new Gradient();
            SunColorByDay.SetKeys(
                new[] {
                    new GradientColorKey(new Color(1f,0.55f,0.35f), 0f),
                    new GradientColorKey(new Color(1f,0.95f,0.85f), 0.25f),
                    new GradientColorKey(new Color(1f,0.98f,0.92f), 0.5f),
                    new GradientColorKey(new Color(1f,0.95f,0.85f), 0.75f),
                    new GradientColorKey(new Color(1f,0.5f,0.3f), 1f)
                },
                new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
        }

        if (SunIntensityByDay == null || SunIntensityByDay.length == 0)
        {
            SunIntensityByDay = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.23f, 0.35f),
                new Keyframe(0.5f, 1.2f),
                new Keyframe(0.77f, 0.35f),
                new Keyframe(1f, 0f));
        }

        ChooseNextWeatherDuration();
        ApplyWeatherNow(WeatherState.Clear);
        UpdateSkyImmediate();
    }

    private void Update()
    {
        UpdateSkyImmediate();
        UpdateWeather();
    }

    private void UpdateSkyImmediate()
    {
        float hour = GameClock.Hour + (GameClock.Minute / 60f) + (GameClock.Second / 3600f);
        float dayT = Mathf.Repeat(hour / 24f, 1f);

        bool useDay = IsDaytime(hour);
        Material selected = useDay ? DaySkyboxMaterial : NightSkyboxMaterial;
        if (selected != null && RenderSettings.skybox != selected)
        {
            RenderSettings.skybox = selected;
            DynamicGI.UpdateEnvironment();
        }

        if (SunLight != null)
        {
            float sunAngle = ((hour / 24f) * 360f) - 90f;
            SunLight.transform.rotation = Quaternion.Euler(sunAngle, -30f, 0f);
            SunLight.color = SunColorByDay.Evaluate(dayT);
            float intensity = Mathf.Max(0f, SunIntensityByDay.Evaluate(dayT));
            if (!useDay) { intensity *= 0.2f; }
            if (weatherState == WeatherState.Fog) { intensity *= 0.8f; }
            if (weatherState == WeatherState.Rain) { intensity *= 0.7f; }
            SunLight.intensity = intensity;
        }

        Color ambient = useDay ? new Color(0.56f, 0.66f, 0.56f) : new Color(0.07f, 0.09f, 0.14f);
        if (weatherState == WeatherState.Rain) { ambient *= 0.75f; }
        if (weatherState == WeatherState.Fog) { ambient = Color.Lerp(ambient, new Color(0.45f, 0.45f, 0.48f), 0.5f); }
        RenderSettings.ambientLight = ambient;
    }

    private bool IsDaytime(float hour)
    {
        float dawnStart = SunriseHour - DawnDuskBlendHours;
        float duskEnd = SunsetHour + DawnDuskBlendHours;
        return hour >= dawnStart && hour <= duskEnd;
    }

    private void UpdateWeather()
    {
        if (!EnableWeather) { return; }

        weatherTimer -= Time.deltaTime;
        if (weatherTimer > 0f) { return; }

        float roll = Random.value;
        WeatherState next;
        if (roll < RainChance) { next = WeatherState.Rain; }
        else if (roll < RainChance + FogChance) { next = WeatherState.Fog; }
        else { next = WeatherState.Clear; }

        ApplyWeatherNow(next);
        ChooseNextWeatherDuration();
    }

    private void ChooseNextWeatherDuration()
    {
        weatherTimer = Random.Range(MinWeatherDurationSeconds, MaxWeatherDurationSeconds);
    }

    private void ApplyWeatherNow(WeatherState state)
    {
        weatherState = state;
        switch (state)
        {
            case WeatherState.Clear:
                RenderSettings.fog = false;
                break;
            case WeatherState.Rain:
                RenderSettings.fog = true;
                RenderSettings.fogColor = new Color(0.45f, 0.48f, 0.54f);
                RenderSettings.fogDensity = 0.0055f;
                break;
            case WeatherState.Fog:
                RenderSettings.fog = true;
                RenderSettings.fogColor = new Color(0.6f, 0.61f, 0.62f);
                RenderSettings.fogDensity = 0.01f;
                break;
        }
    }
}
