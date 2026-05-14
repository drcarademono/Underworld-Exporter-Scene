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
    [Range(1,6)] public int ActiveChunkRadius = 2;
    public bool LoadWholeMapAtStartup = false;

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
    public int GrassTextureIndex = 181;
    public int StoneTextureIndex = 253;
}
