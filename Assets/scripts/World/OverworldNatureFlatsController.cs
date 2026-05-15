using UnityEngine;

public class OverworldNatureFlatsController : MonoBehaviour
{
    [Header("Enable")]
    public bool EnableNatureFlats = true;

    [Header("Material Pools")]
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
}
