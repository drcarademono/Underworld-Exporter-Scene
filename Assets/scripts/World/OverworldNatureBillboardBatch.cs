using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class OverworldNatureBillboardBatch : MonoBehaviour
{
    private const int VertsPerQuad = 4;
    private const int TrisPerQuad = 6;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh batchMesh;

    private Vector3[] quadCenters;
    private float[] quadWidths;
    private float[] quadHeights;
    private int[] quadSpriteIndex;

    private Vector3[] meshVerts;
    private Vector2[] meshUvs;
    private int[] meshTris;
    private Vector3[] meshNormals;
    private int[] quadOrder;
    private Rect[] atlasRects;
    private Vector2[] atlasNativeSizes;

    private static Texture2D cachedDensityMap;
    private static string cachedDensityPath;
    private static Texture2D cachedClimateMap;
    private static string cachedClimatePath;

    public void Initialize(Vector3[] vertices, int[] grassTriangles, OverworldNatureFlatsController flats, float waterSurfaceEpsilon, Vector2Int chunkCoord)
    {
        if (vertices == null || grassTriangles == null || flats == null || !flats.EnableNatureFlats) { return; }

        TryLoadControlMaps(flats);
        float natureMapAreaScale = ResolveOverworldAreaScale();
        float densityScaleMultiplier = natureMapAreaScale * natureMapAreaScale;
        int climateId = EstimateClimateId(chunkCoord, flats, vertices);
        OverworldNatureBiomeProfile profile = flats.GetBiomeProfileForClimate(climateId);
        Material atlasMaterial = BuildAtlasMaterial(flats, profile, out atlasRects, out atlasNativeSizes);
        if (atlasMaterial == null || atlasRects == null || atlasRects.Length == 0) { return; }

        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = atlasMaterial;

        List<int> candidates = new List<int>(grassTriangles.Length / 3);
        for (int i = 0; i < grassTriangles.Length; i += 3)
        {
            Vector3 center = (vertices[grassTriangles[i]] + vertices[grassTriangles[i + 1]] + vertices[grassTriangles[i + 2]]) / 3f;
            if (center.y <= waterSurfaceEpsilon) { continue; }
            float macro = SampleMacroNoise(center, flats, profile);
            float cluster = SampleClusterNoise(center, flats);
            float baseDensity = profile != null ? profile.BaseDensity : flats.BaseDensity;
            float clusterDensity = profile != null ? profile.ClusterDensity : flats.ClusterDensity;
            float threshold = Mathf.Lerp(baseDensity, clusterDensity, cluster);
            threshold *= ComputeContextDensity(center, vertices[grassTriangles[i]], vertices[grassTriangles[i+1]], vertices[grassTriangles[i+2]], profile);
            threshold *= ComputeClearingFactor(center, flats, profile, macro);
            threshold *= SampleDensityMap(center, flats, natureMapAreaScale);
            threshold *= densityScaleMultiplier;
            threshold = Mathf.Clamp01(threshold);
            if (Deterministic01(center, flats.NatureSeed) <= threshold) { candidates.Add(i); }
        }
        if (candidates.Count == 0) { return; }

        int maxCount = Mathf.Max(0, flats.MaxBillboardsPerChunk);
        float keepProb = (maxCount <= 0 || candidates.Count <= maxCount) ? 1f : (maxCount / (float)candidates.Count);

        List<int> selected = new List<int>(candidates.Count);
        for (int c = 0; c < candidates.Count; c++)
        {
            int triStart = candidates[c];
            Vector3 center = (vertices[grassTriangles[triStart]] + vertices[grassTriangles[triStart + 1]] + vertices[grassTriangles[triStart + 2]]) / 3f;
            if (keepProb >= 1f || Deterministic01(center + new Vector3(37.17f, 0f, -91.73f), flats.NatureSeed) <= keepProb) { selected.Add(triStart); }
        }
        if (selected.Count == 0) { return; }

        int quadCount = selected.Count;
        quadCenters = new Vector3[quadCount];
        quadWidths = new float[quadCount];
        quadHeights = new float[quadCount];
        quadSpriteIndex = new int[quadCount];
        quadOrder = new int[quadCount];
        meshVerts = new Vector3[quadCount * VertsPerQuad];
        meshUvs = new Vector2[quadCount * VertsPerQuad];
        meshTris = new int[quadCount * TrisPerQuad];
        meshNormals = new Vector3[quadCount * VertsPerQuad];

        for (int q = 0; q < quadCount; q++)
        {
            int triStart = selected[q];
            Vector3 center = (vertices[grassTriangles[triStart]] + vertices[grassTriangles[triStart + 1]] + vertices[grassTriangles[triStart + 2]]) / 3f;
            float macro = SampleMacroNoise(center, flats, profile);
            HabitatType habitat = GetHabitat(macro, profile);

            int spriteIndex;
            NatureCategory category = ChooseCategory(center, flats.NatureSeed, profile, habitat);
            float baseWidth;
            float baseHeight;
            if (!TryPickSpriteIndex(category, center, flats.NatureSeed, out spriteIndex))
            {
                if (!TryPickAnySpriteInPriorityOrder(center, flats.NatureSeed, out spriteIndex, category))
                {
                    continue;
                }
            }

            if (atlasNativeSizes != null && spriteIndex >= 0 && spriteIndex < atlasNativeSizes.Length)
            {
                Vector2 native = atlasNativeSizes[spriteIndex];
                float tileWorldSize = ResolveTerrainTileWorldSize();
                const float terrainTileTexels = 64f;
                float worldUnitsPerPixel = tileWorldSize / terrainTileTexels;
                baseWidth = native.x * worldUnitsPerPixel;
                baseHeight = native.y * worldUnitsPerPixel;
            }
            else if (category == NatureCategory.Tree)
            {
                baseHeight = flats.TreeHeight;
                baseWidth = flats.TreeWidth;
            }
            else
            {
                baseHeight = flats.TerrainSpriteHeight;
                baseWidth = flats.TerrainSpriteWidth;
            }

            quadCenters[q] = center + (Vector3.up * flats.GroundOffset);
            quadWidths[q] = baseWidth;
            quadHeights[q] = baseHeight;
            quadSpriteIndex[q] = spriteIndex;
            quadOrder[q] = q;
        }

        batchMesh = new Mesh();
        batchMesh.name = $"NatureBillboards_{chunkCoord.x}_{chunkCoord.y}";
        batchMesh.indexFormat = (meshVerts.Length > 65535) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;

        RebuildGeometry();
        batchMesh.vertices = meshVerts;
        batchMesh.uv = meshUvs;
        batchMesh.triangles = meshTris;
        batchMesh.RecalculateNormals();
        batchMesh.normals = meshNormals;
        batchMesh.RecalculateBounds();
        meshFilter.sharedMesh = batchMesh;
    }

    private void LateUpdate()
    {
        if (batchMesh == null || UWCharacter.Instance == null) { return; }
        RebuildGeometry();
        batchMesh.vertices = meshVerts;
        batchMesh.uv = meshUvs;
        batchMesh.triangles = meshTris;
        batchMesh.RecalculateBounds();
    }


    private void RebuildGeometry()
    {
        Vector3 camForward = UWCharacter.Instance != null ? UWCharacter.Instance.dirForNPC : Vector3.forward;
        if (camForward.sqrMagnitude < 0.0001f) { camForward = Vector3.forward; }
        Vector3 right = Vector3.Cross(Vector3.up, camForward).normalized;
        if (right.sqrMagnitude < 0.0001f) { right = Vector3.right; }

        Vector3 camPos = (Camera.main != null) ? Camera.main.transform.position : UWCharacter.Instance.CameraPos;
        System.Array.Sort(quadOrder, (a, b) => (quadCenters[b] - camPos).sqrMagnitude.CompareTo((quadCenters[a] - camPos).sqrMagnitude));

        for (int q = 0; q < quadCenters.Length; q++)
        {
            int vi = q * VertsPerQuad;
            Vector3 side = right * (quadWidths[q] * 0.5f);
            meshVerts[vi + 0] = quadCenters[q] - side;
            meshVerts[vi + 1] = quadCenters[q] + side;
            meshVerts[vi + 2] = quadCenters[q] - side + (Vector3.up * quadHeights[q]);
            meshVerts[vi + 3] = quadCenters[q] + side + (Vector3.up * quadHeights[q]);

            meshNormals[vi + 0] = Vector3.up;
            meshNormals[vi + 1] = Vector3.up;
            meshNormals[vi + 2] = Vector3.up;
            meshNormals[vi + 3] = Vector3.up;

            Rect r = atlasRects[Mathf.Clamp(quadSpriteIndex[q], 0, atlasRects.Length - 1)];
            meshUvs[vi + 0] = new Vector2(r.xMin, r.yMin);
            meshUvs[vi + 1] = new Vector2(r.xMax, r.yMin);
            meshUvs[vi + 2] = new Vector2(r.xMin, r.yMax);
            meshUvs[vi + 3] = new Vector2(r.xMax, r.yMax);
        }

        for (int sorted = 0; sorted < quadOrder.Length; sorted++)
        {
            int q = quadOrder[sorted];
            int vi = q * VertsPerQuad;
            int ti = sorted * TrisPerQuad;
            meshTris[ti + 0] = vi + 0; meshTris[ti + 1] = vi + 2; meshTris[ti + 2] = vi + 1;
            meshTris[ti + 3] = vi + 1; meshTris[ti + 4] = vi + 2; meshTris[ti + 5] = vi + 3;
        }
    }


    private Dictionary<NatureCategory, List<int>> categoryAtlasIndices = new Dictionary<NatureCategory, List<int>>();

    private enum HabitatType { Flower, Grass, Forest }
    private enum NatureCategory { Tree, Bush, Flower, Rock }

    private bool TryPickSpriteIndex(NatureCategory category, Vector3 center, int seed, out int spriteIndex)
    {
        spriteIndex = 0;
        if (!categoryAtlasIndices.TryGetValue(category, out var list) || list == null || list.Count == 0) { return false; }
        int choice = Mathf.FloorToInt(Deterministic01(center + new Vector3(19.2f, 0f, 5.6f), seed) * list.Count);
        spriteIndex = list[Mathf.Clamp(choice, 0, list.Count - 1)];
        return true;
    }


    private bool TryPickAnySpriteInPriorityOrder(Vector3 center, int seed, out int spriteIndex, NatureCategory requested)
    {
        if (requested == NatureCategory.Tree)
        {
            if (TryPickSpriteIndex(NatureCategory.Tree, center, seed, out spriteIndex)) { return true; }
            if (TryPickSpriteIndex(NatureCategory.Bush, center, seed, out spriteIndex)) { return true; }
            if (TryPickSpriteIndex(NatureCategory.Flower, center, seed, out spriteIndex)) { return true; }
            return TryPickSpriteIndex(NatureCategory.Rock, center, seed, out spriteIndex);
        }

        if (TryPickSpriteIndex(requested, center, seed, out spriteIndex)) { return true; }
        if (TryPickSpriteIndex(NatureCategory.Bush, center, seed, out spriteIndex)) { return true; }
        if (TryPickSpriteIndex(NatureCategory.Flower, center, seed, out spriteIndex)) { return true; }
        if (TryPickSpriteIndex(NatureCategory.Rock, center, seed, out spriteIndex)) { return true; }
        return TryPickSpriteIndex(NatureCategory.Tree, center, seed, out spriteIndex);
    }


    private static float ResolveTerrainTileWorldSize()
    {
        OverworldTerrainController overworld = Object.FindObjectOfType<OverworldTerrainController>();
        if (overworld != null)
        {
            return Mathf.Max(0.125f, overworld.EffectiveTileWorldSize);
        }
        return 8f;
    }

    private static float ResolveOverworldAreaScale()
    {
        OverworldTerrainController overworld = Object.FindObjectOfType<OverworldTerrainController>();
        if (overworld == null) { return 1f; }
        return Mathf.Max(1f, overworld.OverworldAreaScale);
    }

    private static HabitatType GetHabitat(float macroNoise, OverworldNatureBiomeProfile profile)
    {
        float flowerLimit = profile != null ? profile.FlowerLimit : 0.4f;
        float forestLimit = profile != null ? profile.ForestLimit : 0.7f;
        if (macroNoise < flowerLimit) { return HabitatType.Flower; }
        if (macroNoise < forestLimit) { return HabitatType.Grass; }
        return HabitatType.Forest;
    }

    private static NatureCategory ChooseCategory(Vector3 center, int seed, OverworldNatureBiomeProfile profile, HabitatType habitat)
    {
        float flower=0.2f, bush=0.45f, tree=0.3f, rock=0.05f;
        if (profile != null)
        {
            if (habitat == HabitatType.Flower) { flower = profile.FlowerHabitatFlowerWeight; bush = profile.FlowerHabitatBushWeight; tree = profile.FlowerHabitatTreeWeight; rock = profile.FlowerHabitatRockWeight; }
            else if (habitat == HabitatType.Grass) { flower = profile.GrassHabitatFlowerWeight; bush = profile.GrassHabitatBushWeight; tree = profile.GrassHabitatTreeWeight; rock = profile.GrassHabitatRockWeight; }
            else { flower = profile.ForestHabitatFlowerWeight; bush = profile.ForestHabitatBushWeight; tree = profile.ForestHabitatTreeWeight; rock = profile.ForestHabitatRockWeight; }
        }
        float total = Mathf.Max(0.0001f, flower + bush + tree + rock);
        float r = Deterministic01(center + new Vector3(8.1f, 0f, 11.2f), seed) * total;
        if ((r -= tree) <= 0f) return NatureCategory.Tree;
        if ((r -= bush) <= 0f) return NatureCategory.Bush;
        if ((r -= flower) <= 0f) return NatureCategory.Flower;
        return NatureCategory.Rock;
    }

    private static float ComputeContextDensity(Vector3 center, Vector3 v0, Vector3 v1, Vector3 v2, OverworldNatureBiomeProfile profile)
    {
        if (profile == null) { return 1f; }
        Vector3 n = Vector3.Cross(v1 - v0, v2 - v0).normalized;
        float slopePenalty = Mathf.Clamp01(n.y);
        float slopeFactor = Mathf.Lerp(1f, slopePenalty, Mathf.Clamp01(profile.SlopeDensityMultiplier));
        float elevation01 = Mathf.Clamp01(center.y / 80f);
        float elevFactor = Mathf.Lerp(1f, elevation01, Mathf.Clamp01(profile.ElevationDensityMultiplier * 0.5f));
        return Mathf.Clamp01(slopeFactor * elevFactor);
    }

    private static float ComputeClearingFactor(Vector3 worldPos, OverworldNatureFlatsController flats, OverworldNatureBiomeProfile profile, float macroNoise)
    {
        if (profile == null) { return 1f; }
        float clearNoise = Mathf.PerlinNoise((worldPos.x + flats.NatureSeed * 0.11f) * profile.ClearingNoiseFrequency, (worldPos.z + flats.NatureSeed * 0.73f) * profile.ClearingNoiseFrequency);
        float clearingMask = Mathf.SmoothStep(profile.ClearingThreshold - 0.08f, profile.ClearingThreshold + 0.08f, clearNoise);
        float forestBias = Mathf.Clamp01((macroNoise - profile.ForestLimit) * 4f);
        float reduction = Mathf.Clamp01(profile.ClearingStrength * (0.5f + 0.5f * forestBias) * clearingMask);
        return Mathf.Clamp(1f - reduction, 0.05f, 1f);
    }

    private static string NormalizeResourcesPath(string input)
    {
        if (string.IsNullOrEmpty(input)) { return input; }
        string path = input.Replace('\\', '/').Trim();
        const string resourcesMarker = "Assets/Resources/";
        int idx = path.IndexOf(resourcesMarker, System.StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            path = path.Substring(idx + resourcesMarker.Length);
        }
        if (path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
        {
            path = path.Substring(0, path.Length - 4);
        }
        return path;
    }

    private static void TryLoadControlMaps(OverworldNatureFlatsController flats)
    {
        if (cachedDensityMap == null || cachedDensityPath != flats.NatureDensityMapResourcePath)
        {
            cachedDensityPath = flats.NatureDensityMapResourcePath;
            string densityPath = NormalizeResourcesPath(cachedDensityPath);
            cachedDensityMap = string.IsNullOrEmpty(densityPath) ? null : Resources.Load<Texture2D>(densityPath);
        }
        if (cachedClimateMap == null || cachedClimatePath != flats.NatureClimateMapResourcePath)
        {
            cachedClimatePath = flats.NatureClimateMapResourcePath;
            string climatePath = NormalizeResourcesPath(cachedClimatePath);
            cachedClimateMap = string.IsNullOrEmpty(climatePath) ? null : Resources.Load<Texture2D>(climatePath);
        }
    }

    private static float SampleDensityMap(Vector3 worldPos, OverworldNatureFlatsController flats, float areaScale)
    {
        if (cachedDensityMap == null) { return 1f; }
        float worldWidth = Mathf.Max(1f, flats.NatureMapWorldWidth * areaScale);
        float worldHeight = Mathf.Max(1f, flats.NatureMapWorldHeight * areaScale);

        float u = Mathf.Clamp01(worldPos.x / worldWidth);
        float v = Mathf.Clamp01(worldPos.z / worldHeight);

        // Pixel-accurate sampling (not bilinear): one map pixel controls one world cell region.
        int px = Mathf.Clamp(Mathf.FloorToInt(u * cachedDensityMap.width), 0, cachedDensityMap.width - 1);
        int py = Mathf.Clamp(Mathf.FloorToInt(v * cachedDensityMap.height), 0, cachedDensityMap.height - 1);
        Color c = cachedDensityMap.GetPixel(px, py);
        return Mathf.Clamp01(c.grayscale);
    }

    private static int EstimateClimateId(Vector2Int chunkCoord, OverworldNatureFlatsController flats, Vector3[] vertices)
    {
        if (cachedClimateMap == null || vertices == null || vertices.Length == 0) { return flats.EstimateClimateIdForChunk(chunkCoord); }
        Vector3 center = vertices[vertices.Length / 2];
        float areaScale = ResolveOverworldAreaScale();
        float worldWidth = Mathf.Max(1f, flats.NatureMapWorldWidth * areaScale);
        float worldHeight = Mathf.Max(1f, flats.NatureMapWorldHeight * areaScale);
        float u = Mathf.Clamp01(center.x / worldWidth);
        float v = Mathf.Clamp01(center.z / worldHeight);
        int cx = Mathf.Clamp(Mathf.FloorToInt(u * cachedClimateMap.width), 0, cachedClimateMap.width - 1);
        int cy = Mathf.Clamp(Mathf.FloorToInt(v * cachedClimateMap.height), 0, cachedClimateMap.height - 1);
        Color32 c = cachedClimateMap.GetPixel(cx, cy);
        if (IsNear(c, flats.MountainColor)) { return 1; }
        if (IsNear(c, flats.RainforestColor)) { return 2; }
        if (IsNear(c, flats.DesertColor)) { return 3; }
        if (IsNear(c, flats.SwampColor)) { return 4; }
        // Lava is a terrain overlay; keep mountain-like biome profile for nature decisions.
        if (IsNear(c, flats.LavaColor)) { return 1; }
        // Dirt variants inherit their parent climate for nature spawning.
        if (IsNear(c, flats.DirtTemperateColor)) { return 0; }
        if (IsNear(c, flats.DirtRainforestColor)) { return 2; }
        if (IsNear(c, flats.DirtMountainColor)) { return 1; }
        return 0;
    }

    private static bool IsNear(Color32 a, Color32 b)
    {
        int dr = a.r - b.r; int dg = a.g - b.g; int db = a.b - b.b;
        return (dr * dr + dg * dg + db * db) <= (20 * 20);
    }

    private Material BuildAtlasMaterial(OverworldNatureFlatsController flats, OverworldNatureBiomeProfile profile, out Rect[] rects, out Vector2[] nativeSizes)
    {
        rects = null;
        nativeSizes = null;
        List<Texture2D> textures = new List<Texture2D>();
        List<Vector2> sizes = new List<Vector2>();
        categoryAtlasIndices = new Dictionary<NatureCategory, List<int>>();
        Material baseMat = null;

        void AddFromTextures(Texture2D[] texs, NatureCategory category)
        {
            if (texs == null) { return; }
            if (!categoryAtlasIndices.ContainsKey(category)) { categoryAtlasIndices[category] = new List<int>(); }
            for (int i = 0; i < texs.Length; i++)
            {
                Texture2D tex = texs[i];
                if (tex == null) { continue; }
                if (baseMat == null)
                {
                    Shader litSeed = Shader.Find("Standard");
                    if (litSeed != null) { baseMat = new Material(litSeed); }
                }
                categoryAtlasIndices[category].Add(textures.Count);
                textures.Add(tex);
                sizes.Add(new Vector2(tex.width, tex.height));
            }
        }

        if (profile != null && profile.Categories != null)
        {
            AddFromTextures(profile.Categories.TreeTextures, NatureCategory.Tree);
            AddFromTextures(profile.Categories.BushTextures, NatureCategory.Bush);
            AddFromTextures(profile.Categories.FlowerTextures, NatureCategory.Flower);
            AddFromTextures(profile.Categories.RockTextures, NatureCategory.Rock);
        }

        if (baseMat == null || textures.Count == 0) { return null; }

        Texture2D atlas = new Texture2D(2048, 2048, TextureFormat.RGBA32, false);
        rects = atlas.PackTextures(textures.ToArray(), 2, 2048, false);
        nativeSizes = sizes.ToArray();
        atlas.wrapMode = TextureWrapMode.Clamp;
        atlas.filterMode = FilterMode.Point;

        Shader lit = Shader.Find("Standard");
        Material m = (lit != null) ? new Material(lit) : new Material(baseMat);
        m.mainTexture = atlas;
        if (m.HasProperty("_Glossiness")) { m.SetFloat("_Glossiness", 0f); }
        if (m.HasProperty("_Metallic")) { m.SetFloat("_Metallic", 0f); }
        if (m.HasProperty("_Mode")) { m.SetFloat("_Mode", 1f); }
        if (m.HasProperty("_Cutoff")) { m.SetFloat("_Cutoff", 0.33f); }
        if (m.HasProperty("_Cull")) { m.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off); }
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        m.SetInt("_ZWrite", 1);
        m.DisableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.EnableKeyword("_ALPHATEST_ON");
        m.renderQueue = 2450;
        return m;
    }

    private static float SampleMacroNoise(Vector3 worldPos, OverworldNatureFlatsController flats, OverworldNatureBiomeProfile profile)
    {
        float f = profile != null ? profile.MacroNoiseFrequency : 0.01f;
        float a = profile != null ? profile.MacroNoiseAmplitude : 0.9f;
        float p = profile != null ? profile.MacroNoisePersistence : 0.35f;
        int o = profile != null ? profile.MacroNoiseOctaves : 3;
        float frequency = f;
        float amplitude = a;
        float n = 0f;
        for (int i = 0; i < o; i++)
        {
            n += Mathf.PerlinNoise((worldPos.x + flats.NatureSeed) * frequency, (worldPos.z + flats.NatureSeed * 0.37f) * frequency) * amplitude;
            frequency *= 2f;
            amplitude *= p;
        }
        return Mathf.Clamp01(n);
    }

    private static float SampleClusterNoise(Vector3 worldPos, OverworldNatureFlatsController flats)
    {
        float sampleX = (worldPos.x + flats.NatureSeed) * flats.PerlinScale;
        float sampleY = (worldPos.z + flats.NatureSeed * 0.37f) * flats.PerlinScale;
        return Mathf.SmoothStep(0f, 1f, Mathf.PerlinNoise(sampleX, sampleY));
    }

    private static float Deterministic01(Vector3 worldPos, int seed)
    {
        unchecked
        {
            int hx = Mathf.FloorToInt(worldPos.x * 1000f);
            int hz = Mathf.FloorToInt(worldPos.z * 1000f);
            uint h = ((uint)hx * 374761393u) + ((uint)hz * 668265263u) + ((uint)seed * 2246822519u);
            h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
            return (h & 0x00FFFFFF) / 16777215f;
        }
    }
}
