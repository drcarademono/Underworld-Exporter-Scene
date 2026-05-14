using System;
using System.Collections.Generic;
using UnityEngine;

public class OverworldSkyController : MonoBehaviour
{
    [Serializable]
    private struct SkyboxPreset
    {
        public float SunSize;
        public int SunSizeConvergence;
        public float AtmosphereLerpDuration;
        public float AtmosphereNormalThickness;
        public float AtmosphereDawnDuskThickness;
        public float AtmosphereLerp;
        public string SkyTint;
        public string GroundColor;
        public string AmbientColor;
        public float AmbientIntensity;
        public float Exposure;
        public float NightStartHeight;
        public float NightEndHeight;
        public float SkyFadeStart;
        public float SkyEndStart;
        public float stepSize;
        public string FogDayColor;
        public string FogNightColor;
        public float FogDistance;
        public string MoonNightColor;
        public float CloudFadeHeight;
        public string TopCloudsFlat;
        public string BottomCloudsFlat;
    }

    [Serializable]
    private struct CloudPreset
    {
        public string CloudsTextureFile;
        public string CloudsNormalTextureFile;
        public float TilingX;
        public float TilingY;
        public float OffsetX;
        public float OffsetY;
        public string DayColor;
        public string NightColor;
        public float AlphaTreshold;
        public float AlphaMax;
        public float ColorBoost;
        public float NormalEffect;
        public float NormalSpeed;
        public float Opacity;
        public float Speed;
        public float Direction;
        public float Bending;
        public float BlendSpeed;
        public float BlendScale;
        public float BlendLB;
        public float BlendUB;
        public float SunColorScale;
        public float SunColorLerpScale;
        public string SunColor;
    }

    private enum WeatherState { Sunny, Cloudy, Overcast, Rain, Fog, Thunder, Snow }

    public Light SunLight;
    public float MinWeatherDurationSeconds = 120f;
    public float MaxWeatherDurationSeconds = 300f;

    private Material runtimeSky;
    private float weatherTimer;
    private WeatherState weatherState = WeatherState.Sunny;
    private readonly Dictionary<WeatherState, SkyboxPreset> presets = new Dictionary<WeatherState, SkyboxPreset>();

    public void Initialize(Material dayMat, Material nightMat, Light sun)
    {
        Material baseMat = dayMat != null ? dayMat : nightMat;
        if (baseMat == null) { return; }
        runtimeSky = new Material(baseMat);
        RenderSettings.skybox = runtimeSky;
        DynamicGI.UpdateEnvironment();

        SunLight = sun;
        LoadPresets();
        ApplyPreset(weatherState);
        ChooseNextWeatherDuration();
    }

    private void Update()
    {
        if (runtimeSky == null) { return; }
        UpdateTimeOfDay();
        UpdateWeather();
    }

    private void UpdateTimeOfDay()
    {
        float hour = GameClock.Hour + (GameClock.Minute / 60f) + (GameClock.Second / 3600f);
        float sunAngle = ((hour / 24f) * 360f) - 90f;
        if (SunLight != null)
        {
            SunLight.transform.rotation = Quaternion.Euler(sunAngle, -30f, 0f);
        }
    }

    private void UpdateWeather()
    {
        weatherTimer -= Time.deltaTime;
        if (weatherTimer > 0f) { return; }

        float roll = UnityEngine.Random.value;
        if (roll < 0.25f) weatherState = WeatherState.Sunny;
        else if (roll < 0.45f) weatherState = WeatherState.Cloudy;
        else if (roll < 0.58f) weatherState = WeatherState.Overcast;
        else if (roll < 0.72f) weatherState = WeatherState.Fog;
        else if (roll < 0.86f) weatherState = WeatherState.Rain;
        else if (roll < 0.94f) weatherState = WeatherState.Thunder;
        else weatherState = WeatherState.Snow;

        ApplyPreset(weatherState);
        ChooseNextWeatherDuration();
    }

    private void ChooseNextWeatherDuration()
    {
        weatherTimer = UnityEngine.Random.Range(MinWeatherDurationSeconds, MaxWeatherDurationSeconds);
    }

    private void LoadPresets()
    {
        LoadPreset(WeatherState.Sunny, "SkyboxSunny");
        LoadPreset(WeatherState.Cloudy, "SkyboxCloudy");
        LoadPreset(WeatherState.Overcast, "SkyboxOvercast");
        LoadPreset(WeatherState.Rain, "SkyboxRain");
        LoadPreset(WeatherState.Fog, "SkyboxFog");
        LoadPreset(WeatherState.Thunder, "SkyboxThunder");
        LoadPreset(WeatherState.Snow, "SkyboxSnow");
    }

    private void LoadPreset(WeatherState state, string fileName)
    {
        TextAsset asset = Resources.Load<TextAsset>("DynamicSkies/SkyboxSettings/" + fileName);
        if (asset == null)
        {
            Debug.LogWarning("Missing sky preset: " + fileName);
            return;
        }

        try
        {
            var preset = JsonUtility.FromJson<SkyboxPreset>(asset.text);
            presets[state] = preset;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed parsing sky preset " + fileName + ": " + ex.Message);
        }
    }

    private void ApplyPreset(WeatherState state)
    {
        if (!presets.TryGetValue(state, out var p)) { return; }

        runtimeSky.SetFloat("_SunSize", p.SunSize);
        runtimeSky.SetFloat("_SunSizeConvergence", p.SunSizeConvergence);
        runtimeSky.SetFloat("_AtmosphereLerpDuration", p.AtmosphereLerpDuration);
        runtimeSky.SetFloat("_AtmosphereNormalThickness", p.AtmosphereNormalThickness);
        runtimeSky.SetFloat("_AtmosphereDawnDuskThickness", p.AtmosphereDawnDuskThickness);
        runtimeSky.SetFloat("_AtmosphereLerp", p.AtmosphereLerp);
        runtimeSky.SetColor("_SkyTint", ParseHexColor(p.SkyTint, new Color(0.5f, 0.6f, 0.9f, 1f)));
        runtimeSky.SetColor("_GroundColor", ParseHexColor(p.GroundColor, new Color(0.3f, 0.3f, 0.3f, 1f)));
        runtimeSky.SetColor("_FogDayColor", ParseHexColor(p.FogDayColor, Color.gray));
        runtimeSky.SetColor("_FogNightColor", ParseHexColor(p.FogNightColor, Color.black));
        runtimeSky.SetFloat("_Exposure", p.Exposure);
        runtimeSky.SetFloat("_NightStartHeight", p.NightStartHeight);
        runtimeSky.SetFloat("_NightEndHeight", p.NightEndHeight);
        runtimeSky.SetFloat("_SkyFadeStart", p.SkyFadeStart);
        runtimeSky.SetFloat("_SkyFadeEnd", p.SkyEndStart);
        runtimeSky.SetFloat("_stepSize", p.stepSize);
        runtimeSky.SetFloat("_FogDistance", p.FogDistance);
        runtimeSky.SetFloat("_CloudFadeHeight", p.CloudFadeHeight);
        runtimeSky.SetColor("_MoonNightColor", ParseHexColor(p.MoonNightColor, new Color(0f,0f,0.15f,1f)));

        ApplyCloudPreset(p.TopCloudsFlat, true);
        ApplyCloudPreset(p.BottomCloudsFlat, false);

        RenderSettings.ambientLight = ParseHexColor(p.AmbientColor, new Color(0.4f, 0.4f, 0.45f, 1f)) * Mathf.Max(0.1f, p.AmbientIntensity);
        RenderSettings.fog = true;
        RenderSettings.fogColor = ParseHexColor(p.FogDayColor, Color.gray);
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = Mathf.Clamp01(1f / Mathf.Max(256f, p.FogDistance));

        DynamicGI.UpdateEnvironment();
    }

    private void ApplyCloudPreset(string cloudJson, bool top)
    {
        if (string.IsNullOrEmpty(cloudJson)) { return; }
        CloudPreset c = JsonUtility.FromJson<CloudPreset>(cloudJson);
        string prefix = top ? "_CloudTop" : "_Cloud";

        Texture2D diffuse = Resources.Load<Texture2D>("DynamicSkies/Textures/" + c.CloudsTextureFile);
        Texture2D normal = Resources.Load<Texture2D>("DynamicSkies/Textures/" + c.CloudsNormalTextureFile);
        if (diffuse != null) runtimeSky.SetTexture(prefix + "Diffuse", diffuse);
        if (normal != null) runtimeSky.SetTexture(prefix + "Normal", normal);

        runtimeSky.SetTextureScale(prefix + "Diffuse", new Vector2(c.TilingX <= 0f ? 1f : c.TilingX, c.TilingY <= 0f ? 1f : c.TilingY));
        runtimeSky.SetTextureOffset(prefix + "Diffuse", new Vector2(c.OffsetX, c.OffsetY));

        Color dayColor = ParseHexColor(c.DayColor, Color.white);
        Color nightColor = ParseHexColor(c.NightColor, Color.gray);
        dayColor.a = Mathf.Clamp01(c.Opacity);
        nightColor.a = Mathf.Clamp01(c.Opacity);
        runtimeSky.SetColor(prefix + "Color", dayColor);
        runtimeSky.SetColor(prefix + "NightColor", nightColor);
        runtimeSky.SetFloat(prefix + "AlphaCutoff", c.AlphaTreshold);
        runtimeSky.SetFloat(prefix + "AlphaMax", c.AlphaMax);
        runtimeSky.SetFloat(prefix + "ColorBoost", c.ColorBoost);
        runtimeSky.SetFloat(prefix + "NormalEffect", c.NormalEffect);
        runtimeSky.SetFloat(prefix + "Opacity", c.Opacity);
        runtimeSky.SetFloat(prefix + "Bending", c.Bending);

        if (top)
        {
            runtimeSky.SetFloat("_CloudTopSunScale", c.SunColorScale);
            runtimeSky.SetFloat("_CloudTopSunLerpScale", c.SunColorLerpScale);
            runtimeSky.SetColor("_CloudTopSunColor", ParseHexColor(c.SunColor, Color.white));
        }
        else
        {
            runtimeSky.SetFloat("_CloudNormalSpeed", c.NormalSpeed);
            runtimeSky.SetFloat("_CloudSpeed", c.Speed);
            runtimeSky.SetFloat("_CloudDirection", c.Direction);
            runtimeSky.SetFloat("_CloudBlendSpeed", c.BlendSpeed);
            runtimeSky.SetFloat("_CloudBlendScale", c.BlendScale);
            runtimeSky.SetFloat("_CloudBlendLB", c.BlendLB);
            runtimeSky.SetFloat("_CloudBlendUB", c.BlendUB);
            runtimeSky.SetFloat("_CloudSunScale", c.SunColorScale);
            runtimeSky.SetFloat("_CloudSunLerpScale", c.SunColorLerpScale);
            runtimeSky.SetColor("_CloudSunColor", ParseHexColor(c.SunColor, Color.white));
        }
    }

    private static Color ParseHexColor(string hex, Color fallback)
    {
        if (string.IsNullOrEmpty(hex)) { return fallback; }
        if (!hex.StartsWith("#")) { hex = "#" + hex; }
        if (ColorUtility.TryParseHtmlString(hex, out Color c)) { return c; }
        return fallback;
    }
}
