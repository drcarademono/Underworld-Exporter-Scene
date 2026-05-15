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
    public int ClimateId;

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
            NewProfile("Desert",224,0.2f,0.5f,0.2f,0.5f,0.15f,2),
            NewProfile("Desert2",225,0.15f,0.35f,0.2f,0.5f,0.15f,2),
            NewProfile("Mountain",226,0.2f,0.45f,0.01f,0.65f,0.3f,2),
            NewProfile("Rainforest",227,0.3f,0.65f,0.04f,0.95f,0.35f,3),
            NewProfile("Swamp",228,0.25f,0.55f,0.01f,0.9f,0.35f,3),
            NewProfile("Subtropical",229,0.25f,0.5f,0.015f,0.9f,0.35f,2),
            NewProfile("WoodlandHills",230,0.2f,0.45f,0.02f,0.95f,0.4f,3),
            NewProfile("Temperate",231,0.25f,0.55f,0.01f,0.9f,0.4f,3),
            NewProfile("HauntedWoodland",232,0.2f,0.45f,0.07f,0.9f,0.35f,3),
        };
    }

    private OverworldNatureBiomeProfile NewProfile(string name, int climate, float baseDensity, float clusterDensity, float freq, float amp, float persistence, int octaves)
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
            FlowerLimit = climate == 226 || climate == 227 || climate == 224 ? 0.2f : 0.4f,
            ForestLimit = climate == 226 ? 0.38f : 0.7f
        };
    }

    public int EstimateClimateIdForChunk(Vector2Int chunkCoord)
    {
        int band = Mathf.Abs(chunkCoord.y % 9);
        switch (band)
        {
            case 0: return 224; // desert
            case 1: return 225; // desert2
            case 2: return 226; // mountain
            case 3: return 227; // rainforest
            case 4: return 228; // swamp
            case 5: return 229; // subtropical
            case 6: return 230; // woodland hills
            case 7: return 231; // temperate
            default: return 232; // haunted woodland
        }
    }
}
