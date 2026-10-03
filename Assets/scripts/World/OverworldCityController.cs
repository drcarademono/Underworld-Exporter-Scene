using System;
using UnityEngine;

/// <summary>
/// Scene-configurable prototype controller for CITY01847. The exported city
/// uses metre-sized generator coordinates and 64 x 64 metre map pixels.
/// </summary>
public sealed class OverworldCityController : MonoBehaviour
{
    private const float GeneratorUnitsPerMapPixel = 64f;
    private const float CanvasWidthUnits = 1400f;
    private const float CanvasHeightUnits = 1000f;

    [Header("Placement")]
    public bool SpawnCity = true;
    [Tooltip("Northwest city corner in overworld map coordinates.")]
    public Vector2Int Northwest = new Vector2Int(680, 1330);

    [Header("Terrain")]
    [Tooltip("World-space distance outside the city canvas used to blend the plateau into natural terrain.")]
    [Min(0.01f)] public float TerrainBlendDistance = 64f;

    [Header("Building Appearance")]
    [Tooltip("Optional materials assigned cyclically to building cuboids. Leave empty to use UW2_207.")]
    public Material[] BuildingMaterials;
    [Tooltip("Optional texture applied to a runtime copy of UW2_207. Explicit Building Materials take priority.")]
    public Texture2D BuildingTexture;
    [Tooltip("World-space size, in metres, covered by one repeat of a building texture.")]
    [Min(0.01f)] public float BuildingTextureTileSize = 2f;
    public string CityResourcePath = "Cities/city_01847_buildings";

    private bool initialized;
    private float minSampleX;
    private float minSampleZ;
    private float maxSampleX;
    private float maxSampleZ;
    private float blendDistanceSamples;
    private float plateauHeight;
    private Material fallbackMaterial;

    public void Initialize(OverworldTerrainController overworld, Func<int, int, float> sampleNaturalHeight, Material uw207Material)
    {
        initialized = SpawnCity && overworld != null;
        if (!initialized) { return; }

        float tileWorldSize = Mathf.Max(0.01f, overworld.EffectiveTileWorldSize);
        if (!Mathf.Approximately(tileWorldSize, GeneratorUnitsPerMapPixel))
        {
            Debug.LogWarningFormat(
                "CITY01847 uses {0} one-metre units per map pixel, but the overworld map pixel is {1} metres. " +
                "Set Effective Tile World Size to {0} so city chunks and overworld map pixels align.",
                GeneratorUnitsPerMapPixel,
                tileWorldSize);
        }
        minSampleX = Northwest.x;
        minSampleZ = Northwest.y;
        maxSampleX = minSampleX + (CanvasWidthUnits / tileWorldSize);
        maxSampleZ = minSampleZ + (CanvasHeightUnits / tileWorldSize);
        blendDistanceSamples = Mathf.Max(0.01f, TerrainBlendDistance / tileWorldSize);

        int centreX = Mathf.RoundToInt((minSampleX + maxSampleX) * 0.5f);
        int centreZ = Mathf.RoundToInt((minSampleZ + maxSampleZ) * 0.5f);
        plateauHeight = Mathf.Max(0f, sampleNaturalHeight(centreX, centreZ));
        fallbackMaterial = CreateTextureOverrideMaterial(uw207Material);
    }

    public float BlendTerrainHeight(float sampleX, float sampleZ, float naturalHeight)
    {
        if (!initialized) { return naturalHeight; }

        float outsideX = Mathf.Max(minSampleX - sampleX, sampleX - maxSampleX, 0f);
        float outsideZ = Mathf.Max(minSampleZ - sampleZ, sampleZ - maxSampleZ, 0f);
        float outsideDistance = Mathf.Sqrt((outsideX * outsideX) + (outsideZ * outsideZ));
        if (outsideDistance >= blendDistanceSamples) { return naturalHeight; }

        float naturalWeight = Mathf.SmoothStep(0f, 1f, outsideDistance / blendDistanceSamples);
        return Mathf.Lerp(plateauHeight, naturalHeight, naturalWeight);
    }

    public void SpawnBuildings(Transform parent, OverworldTerrainController overworld)
    {
        if (!initialized || parent == null || overworld == null) { return; }

        TextAsset source = Resources.Load<TextAsset>(CityResourcePath);
        ProceduralCityCuboidCollection city = source == null
            ? null
            : JsonUtility.FromJson<ProceduralCityCuboidCollection>(source.text);
        if (city == null || city.buildings == null)
        {
            Debug.LogWarning("Could not load procedural city buildings at Resources/" + CityResourcePath);
            return;
        }

        GameObject root = new GameObject("ProceduralCity_CITY01847");
        root.transform.SetParent(parent, false);
        float tileWorldSize = overworld.EffectiveTileWorldSize;
        float originWorldX = Northwest.x * tileWorldSize;
        float originWorldZ = Northwest.y * tileWorldSize;

        for (int i = 0; i < city.buildings.Length; i++)
        {
            ProceduralCityCuboid building = city.buildings[i];
            // Exported generator units are metres, and one Unity unit is one metre.
            float width = Mathf.Max(0.01f, building.max_x - building.min_x);
            float depth = Mathf.Max(0.01f, building.max_y - building.min_y);
            float height = Mathf.Max(0.01f, building.height);

            GameObject cuboid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cuboid.name = string.IsNullOrEmpty(building.id) ? "Building" : building.id;
            cuboid.transform.SetParent(root.transform, false);
            cuboid.transform.position = new Vector3(
                originWorldX + ((building.min_x + building.max_x) * 0.5f),
                plateauHeight + (height * 0.5f),
                originWorldZ + ((building.min_y + building.max_y) * 0.5f));
            cuboid.transform.localScale = new Vector3(width, height, depth);

            ApplyWorldSpaceTextureTiling(cuboid, width, height, depth);

            MeshRenderer renderer = cuboid.GetComponent<MeshRenderer>();
            if (renderer != null) { renderer.sharedMaterial = GetBuildingMaterial(i); }
        }

        Debug.Log("Spawned " + city.buildings.Length + " CITY01847 building cuboids at " + Northwest + ".");
    }

    private void ApplyWorldSpaceTextureTiling(GameObject cuboid, float width, float height, float depth)
    {
        MeshFilter filter = cuboid.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) { return; }

        // Unity's primitive cube UVs cover each face once. Rebuild them from
        // the face axes so differently sized walls retain the same texel scale.
        Mesh mesh = filter.mesh;
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector2[] uvs = new Vector2[vertices.Length];
        float tileSize = Mathf.Max(0.01f, BuildingTextureTileSize);

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = vertices[i];
            Vector3 normal = normals[i];
            if (Mathf.Abs(normal.y) > 0.5f)
            {
                uvs[i] = new Vector2((vertex.x + 0.5f) * width, (vertex.z + 0.5f) * depth) / tileSize;
            }
            else if (Mathf.Abs(normal.x) > 0.5f)
            {
                uvs[i] = new Vector2((vertex.z + 0.5f) * depth, (vertex.y + 0.5f) * height) / tileSize;
            }
            else
            {
                uvs[i] = new Vector2((vertex.x + 0.5f) * width, (vertex.y + 0.5f) * height) / tileSize;
            }
        }

        mesh.uv = uvs;
    }

    private Material GetBuildingMaterial(int buildingIndex)
    {
        if (BuildingMaterials != null && BuildingMaterials.Length > 0)
        {
            Material selected = BuildingMaterials[buildingIndex % BuildingMaterials.Length];
            if (selected != null)
            {
                EnsureTextureRepeats(selected);
                return selected;
            }
        }
        EnsureTextureRepeats(fallbackMaterial);
        return fallbackMaterial;
    }

    private static void EnsureTextureRepeats(Material material)
    {
        if (material != null && material.mainTexture != null)
        {
            material.mainTexture.wrapMode = TextureWrapMode.Repeat;
        }
    }

    private Material CreateTextureOverrideMaterial(Material source)
    {
        if (BuildingTexture == null) { return source; }

        Material runtimeMaterial;
        if (source != null)
        {
            runtimeMaterial = new Material(source);
        }
        else
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) { return null; }
            runtimeMaterial = new Material(shader);
        }
        runtimeMaterial.name = "CITY01847_BuildingMaterial";
        runtimeMaterial.mainTexture = BuildingTexture;
        BuildingTexture.wrapMode = TextureWrapMode.Repeat;
        return runtimeMaterial;
    }
}

[Serializable]
public sealed class ProceduralCityCuboidCollection
{
    public ProceduralCityCuboid[] buildings;
}

[Serializable]
public sealed class ProceduralCityCuboid
{
    public string id;
    public float min_x;
    public float min_y;
    public float max_x;
    public float max_y;
    public float height;
}
