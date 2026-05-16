using UnityEngine;

[System.Serializable]
public class OverworldNatureCategoryMaterials
{
    public Material[] Trees;
    public Material[] Bushes;
    public Material[] Flowers;
    public Material[] Rocks;
}

[System.Serializable]
public class OverworldNatureBiomeProfile
{
    public string Name;
    public int ClimateId; // 0=Temperate,1=Mountain,2=Rainforest,3=Desert

    [Header("Distribution")]
    [Range(0f, 1f)] public float BaseDensity = 0.05f;
    [Range(0f, 1f)] public float ClusterDensity = 0.45f;
    [Range(0.0001f, 0.2f)] public float MacroNoiseFrequency = 0.01f;
    [Range(0.1f, 2f)] public float MacroNoiseAmplitude = 0.9f;
    [Range(0f, 1f)] public float MacroNoisePersistence = 0.35f;
    [Range(1, 5)] public int MacroNoiseOctaves = 3;

    [Header("Habitat thresholds")]
    [Range(0f, 1f)] public float FlowerLimit = 0.4f;
    [Range(0f, 1f)] public float ForestLimit = 0.7f;


    [Header("Clearings")]
    [Range(0.0001f, 0.2f)] public float ClearingNoiseFrequency = 0.006f;
    [Range(0f, 1f)] public float ClearingThreshold = 0.62f;
    [Range(0f, 1f)] public float ClearingStrength = 0.75f;

    [Header("Category Weights (Flower Habitat)")]
    [Range(0f, 1f)] public float FlowerHabitatFlowerWeight = 0.65f;
    [Range(0f, 1f)] public float FlowerHabitatBushWeight = 0.25f;
    [Range(0f, 1f)] public float FlowerHabitatTreeWeight = 0.08f;
    [Range(0f, 1f)] public float FlowerHabitatRockWeight = 0.02f;

    [Header("Category Weights (Grass Habitat)")]
    [Range(0f, 1f)] public float GrassHabitatFlowerWeight = 0.2f;
    [Range(0f, 1f)] public float GrassHabitatBushWeight = 0.5f;
    [Range(0f, 1f)] public float GrassHabitatTreeWeight = 0.25f;
    [Range(0f, 1f)] public float GrassHabitatRockWeight = 0.05f;

    [Header("Category Weights (Forest Habitat)")]
    [Range(0f, 1f)] public float ForestHabitatFlowerWeight = 0.03f;
    [Range(0f, 1f)] public float ForestHabitatBushWeight = 0.24f;
    [Range(0f, 1f)] public float ForestHabitatTreeWeight = 0.68f;
    [Range(0f, 1f)] public float ForestHabitatRockWeight = 0.05f;

    [Header("Context")]
    [Range(0f, 3f)] public float ElevationDensityMultiplier = 1f;
    [Range(0f, 2f)] public float SlopeDensityMultiplier = 0.6f;

    [Header("Category Sets")]
    public OverworldNatureCategoryMaterials Categories;
}

public class OverworldNatureFlatsController : MonoBehaviour
{
    [Header("Enable")]
    public bool EnableNatureFlats = true;

    [Header("Legacy Material Pools")]
    public Material[] TreeMaterials;
    public Material[] TerrainSpriteMaterials;

    [Header("Determinism + Distribution")]
    [Range(0, 2048)] public int NatureSeed = 7341;
    [Range(0f, 1f)] public float BaseDensity = 0.05f;
    [Range(0f, 1f)] public float ClusterDensity = 0.45f;
    [Range(0.0001f, 0.02f)] public float PerlinScale = 0.0025f;
    [Range(0f, 1f)] public float TerrainSpriteChance = 0.22f;

    [Header("Size + Placement")]
    [Range(0.5f, 16f)] public float TreeWidth = 4.5f;
    [Range(1f, 30f)] public float TreeHeight = 9f;
    [Range(0.5f, 16f)] public float TerrainSpriteWidth = 3.5f;
    [Range(0.5f, 16f)] public float TerrainSpriteHeight = 3.5f;
    [Range(-2f, 2f)] public float GroundOffset = 0.2f;
    [Range(0, 4000)] public int MaxBillboardsPerChunk = 1000;

    

    
    [Header("Nature Control Maps (2048x2048)")]
    public string NatureDensityMapResourcePath = "UIX/nature_density_map";
    public string NatureClimateMapResourcePath = "UIX/nature_climate_map";
    [Range(1024f, 65536f)] public float NatureMapWorldWidth = 16384f;
    [Range(1024f, 65536f)] public float NatureMapWorldHeight = 16384f;

    [Header("Climate Map Colors")]
    public Color32 MountainColor = new Color32(0, 255, 0, 255);
    public Color32 RainforestColor = new Color32(0, 0, 255, 255);
    public Color32 DesertColor = new Color32(255, 255, 0, 255);

    [Header("Biome Profiles")]
    public OverworldNatureBiomeProfile[] BiomeProfiles;

    public OverworldNatureBiomeProfile GetBiomeProfileForClimate(int climateId)
    {
        if (BiomeProfiles != null)
        {
            for (int i = 0; i < BiomeProfiles.Length; i++)
            {
                if (BiomeProfiles[i] != null && BiomeProfiles[i].ClimateId == climateId) { return BiomeProfiles[i]; }
            }
        }
        return null;
    }

    
    private void Reset()
    {
        if (BiomeProfiles != null && BiomeProfiles.Length > 0) { return; }
        BiomeProfiles = new OverworldNatureBiomeProfile[]
        {
            // Temperate Woodland (WO 231/223 style)
            NewProfile("Temperate", 0, 0.25f, 0.55f, 0.01f, 0.9f, 0.4f, 3, 0.4f, 0.7f,
                0.65f, 0.25f, 0.08f, 0.02f,
                0.20f, 0.50f, 0.25f, 0.05f,
                0.03f, 0.24f, 0.68f, 0.05f,
                0.006f, 0.62f, 0.75f),

            // Mountain (WO 226)
            NewProfile("Mountain", 1, 0.20f, 0.45f, 0.015f, 0.65f, 0.3f, 2, 0.2f, 0.38f,
                0.55f, 0.20f, 0.05f, 0.20f,
                0.12f, 0.28f, 0.40f, 0.20f,
                0.02f, 0.13f, 0.70f, 0.15f,
                0.008f, 0.58f, 0.65f),

            // Rainforest (WO 227)
            NewProfile("Rainforest", 2, 0.30f, 0.65f, 0.04f, 0.95f, 0.35f, 3, 0.2f, 0.5f,
                0.55f, 0.35f, 0.08f, 0.02f,
                0.18f, 0.52f, 0.26f, 0.04f,
                0.02f, 0.30f, 0.64f, 0.04f,
                0.005f, 0.60f, 0.70f),

            // Desert (WO 224)
            NewProfile("Desert", 3, 0.12f, 0.30f, 0.15f, 0.5f, 0.15f, 2, 0.2f, 0.5f,
                0.15f, 0.35f, 0.00f, 0.50f,
                0.05f, 0.35f, 0.05f, 0.55f,
                0.00f, 0.20f, 0.20f, 0.60f,
                0.01f, 0.52f, 0.85f),
        };
    }

    private OverworldNatureBiomeProfile NewProfile(
        string name, int climate, float baseDensity, float clusterDensity, float freq, float amp, float persistence, int octaves,
        float flowerLimit, float forestLimit,
        float flowerHabitatFlowerWeight, float flowerHabitatBushWeight, float flowerHabitatTreeWeight, float flowerHabitatRockWeight,
        float grassHabitatFlowerWeight, float grassHabitatBushWeight, float grassHabitatTreeWeight, float grassHabitatRockWeight,
        float forestHabitatFlowerWeight, float forestHabitatBushWeight, float forestHabitatTreeWeight, float forestHabitatRockWeight,
        float clearingNoiseFrequency, float clearingThreshold, float clearingStrength)
    {
        return new OverworldNatureBiomeProfile
        {
            Name = name,
            ClimateId = climate,
            BaseDensity = baseDensity,
            ClusterDensity = clusterDensity,
            MacroNoiseFrequency = freq,
            MacroNoiseAmplitude = amp,
            MacroNoisePersistence = persistence,
            MacroNoiseOctaves = octaves,
            FlowerLimit = flowerLimit,
            ForestLimit = forestLimit,
            FlowerHabitatFlowerWeight = flowerHabitatFlowerWeight,
            FlowerHabitatBushWeight = flowerHabitatBushWeight,
            FlowerHabitatTreeWeight = flowerHabitatTreeWeight,
            FlowerHabitatRockWeight = flowerHabitatRockWeight,
            GrassHabitatFlowerWeight = grassHabitatFlowerWeight,
            GrassHabitatBushWeight = grassHabitatBushWeight,
            GrassHabitatTreeWeight = grassHabitatTreeWeight,
            GrassHabitatRockWeight = grassHabitatRockWeight,
            ForestHabitatFlowerWeight = forestHabitatFlowerWeight,
            ForestHabitatBushWeight = forestHabitatBushWeight,
            ForestHabitatTreeWeight = forestHabitatTreeWeight,
            ForestHabitatRockWeight = forestHabitatRockWeight,
            ClearingNoiseFrequency = clearingNoiseFrequency,
            ClearingThreshold = clearingThreshold,
            ClearingStrength = clearingStrength,
        };
    }

    public int EstimateClimateIdForChunk(Vector2Int chunkCoord)
    {
        // Fallback climate estimate used only when climate control maps are unavailable.
        // Per design, default to Temperate when no climate-map data can be resolved.
        return 0;
    }
}
