using UnityEngine;

public class OverworldTerrainController : MonoBehaviour
{
    [Header("Overworld Start")]
    public bool StartInOverworld = true;
    public Vector3 OverworldStartPos = new Vector3(0f, 2f, 0f);
    public Vector2Int OverworldStartTile = new Vector2Int(1418, 1398);

    [Header("Heightmap Sampling")]
    public string HeightmapResourcePath = "UIX/Britannia_Corv_Heightmap";
    public int TilesPerPixel = 8;
    public float TileWorldSize = 8f;

    [Header("Chunking")]
    [Range(16,128)] public int ChunkSizeSamples = 64;
    [Range(1,6)] public int ActiveChunkRadius = 1;
    [Range(0,3)] public int HighDetailUnloadMargin = 1;
    [Range(1,8)] public int TerrainDecimationStep = 1;
    public bool LoadWholeMapAtStartup = false;
    public bool LoadDistantChunks = true;
    [Range(2, 12)] public int DistantChunkStep = 6;
    
    [Header("LOD Skirt Lighting")]
    public bool SkirtUseUpwardNormals = true;
    [Range(0f, 1f)] public float SkirtUpwardNormalBlend = 0.5f;
    public bool SkirtCastShadows = false;
    public bool SkirtReceiveShadows = false;


    [Header("Overworld Time")]
    [Range(0,23)] public int StartHour = 12;
    [Range(0,59)] public int StartMinute = 0;
    [Range(0,59)] public int StartSecond = 0;
    [Range(0.05f, 20f)] public float ClockRateSecondsPerGameSecond = 1f;

    [Header("View Distance")]
    [Range(100f, 20000f)] public float OverworldFarClip = 6000f;

    [Header("Terrain Shape")]
    [Range(1f, 120f)] public float HeightScale = 42f;
    [Range(0f, 10f)] public float PerlinStrength = 3.5f;
    [Range(0.0005f, 0.03f)] public float PerlinScale = 0.006f;
    [Range(0f, 30f)] public float SeaLevelOffset = 6f;
    [Range(0.3f, 0.95f)] public float SteepSlopeNormalThreshold = 0.78f;

    [Header("UW2 Terrain Texture Indices")]
    public int WaterTextureIndex = 184;
    public int WaterTextureAnimEndIndex = 188;
    public bool AnimateWater = true;
    [Range(0.05f, 1f)] public float WaterAnimFrameTime = 0.2f;

    [Header("Water Classification")]
    [Range(0f, 0.2f)] public float WaterSurfaceEpsilon = 0.02f;
    [Header("Snow Classification")]
    [Range(0f, 120f)] public float SnowLineAltitude = 40f;
    [Range(0f, 30f)] public float SnowTransitionWidth = 6f;
    [Range(0f, 20f)] public float SnowNoiseScale = 5f;
    [Range(0f, 20f)] public float SnowNoiseAmplitude = 3f;
    public bool RenderSnowUsingStoneLayer = true;
    public int GrassTextureIndex = 181;
    public int StoneTextureIndex = 253;
    public int DirtTextureIndex = 182;
    public int SandTextureIndex = 183;
    public int SwampTextureIndex = 251;
    public int SnowTextureIndex = 248;
    public int LavaTextureIndex = 252;

    [Header("Custom Material Overrides")]
    public Material GrassMaterialOverride;
    public Material StoneMaterialOverride;
    public Material DirtMaterialOverride;
    public Material SandMaterialOverride;
    public Material SwampMaterialOverride;
    public Material SnowMaterialOverride;
    public Material LavaMaterialOverride;


    [Header("Texture Filtering")]
    public bool EnableOverworldTerrainMipmaps = false;

    [Header("Transition Tiles")]
    public bool UseTransitionTileTexturing = true;
    [Range(8,128)] public int TransitionPixelsPerTile = 32;
    public string TransitionTilesFolder = "Assets/Generated/OverworldTransitions";

    [Header("Transition Debug/Perf")]
    public bool TransitionTexturingDiagnostics = true;
    [Range(1, 200)] public int TransitionDiagLogEveryNChunks = 8;
}
