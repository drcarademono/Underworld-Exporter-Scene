using System;
using UnityEngine;

/// <summary>
/// Scene-configurable prototype controller for CITY01847. The exported city
/// uses metre-sized generator coordinates and 64 x 64 metre map pixels.
/// </summary>
public sealed class OverworldCityController : MonoBehaviour
{
    private const float CanvasWidthUnits = 1400f;
    private const float CanvasHeightUnits = 1000f;

    [Header("Placement")]
    public bool SpawnCity = true;
    [Tooltip("Northwest city corner in overworld map coordinates.")]
    public Vector2Int Northwest = new Vector2Int(680, 1330);
    [Tooltip("Scale applied to exported metre-sized X/Z coordinates.")]
    [Min(0.01f)] public float HorizontalScale = 1f;
    [Tooltip("Scale applied to each building's exported eave height.")]
    [Min(0.01f)] public float BuildingHeightScale = 1f;

    [Header("Terrain")]
    [Tooltip("World-space distance outside the city canvas used to blend the plateau into natural terrain.")]
    [Min(0.01f)] public float TerrainBlendDistance = 64f;

    [Header("Building Appearance")]
    [Tooltip("Optional materials assigned cyclically to building cuboids. Leave empty to use UW2_207.")]
    public Material[] BuildingMaterials;
    [Tooltip("Optional texture applied to a runtime copy of UW2_207. Explicit Building Materials take priority.")]
    public Texture2D BuildingTexture;
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
        minSampleX = Northwest.x;
        minSampleZ = Northwest.y;
        maxSampleX = minSampleX + ((CanvasWidthUnits * HorizontalScale) / tileWorldSize);
        maxSampleZ = minSampleZ + ((CanvasHeightUnits * HorizontalScale) / tileWorldSize);
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
            float width = Mathf.Max(0.01f, (building.max_x - building.min_x) * HorizontalScale);
            float depth = Mathf.Max(0.01f, (building.max_y - building.min_y) * HorizontalScale);
            float height = Mathf.Max(0.01f, building.height * BuildingHeightScale);

            GameObject cuboid = CreateBuildingCuboid(width, height, depth);
            cuboid.name = string.IsNullOrEmpty(building.id) ? "Building" : building.id;
            cuboid.transform.SetParent(root.transform, false);
            cuboid.transform.position = new Vector3(
                originWorldX + (((building.min_x + building.max_x) * 0.5f) * HorizontalScale),
                plateauHeight + (height * 0.5f),
                originWorldZ + (((building.min_y + building.max_y) * 0.5f) * HorizontalScale));
            MeshRenderer renderer = cuboid.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GetBuildingMaterial(i);
                EnsureTextureRepeats(renderer.sharedMaterial);
            }
        }

        Debug.Log("Spawned " + city.buildings.Length + " CITY01847 building cuboids at " + Northwest + ".");
    }

    private static GameObject CreateBuildingCuboid(float width, float height, float depth)
    {
        GameObject cuboid = new GameObject();
        MeshFilter filter = cuboid.AddComponent<MeshFilter>();
        cuboid.AddComponent<MeshRenderer>();

        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float halfDepth = depth * 0.5f;
        Mesh mesh = new Mesh { name = "CityBuildingCuboid" };
        mesh.vertices = new[]
        {
            // South, north, west, east, top, and bottom. Each face needs its own
            // vertices so its UV axes can follow that face's physical dimensions.
            new Vector3(-halfWidth, -halfHeight, -halfDepth), new Vector3(-halfWidth, halfHeight, -halfDepth), new Vector3(halfWidth, halfHeight, -halfDepth), new Vector3(halfWidth, -halfHeight, -halfDepth),
            new Vector3(halfWidth, -halfHeight, halfDepth), new Vector3(halfWidth, halfHeight, halfDepth), new Vector3(-halfWidth, halfHeight, halfDepth), new Vector3(-halfWidth, -halfHeight, halfDepth),
            new Vector3(-halfWidth, -halfHeight, halfDepth), new Vector3(-halfWidth, halfHeight, halfDepth), new Vector3(-halfWidth, halfHeight, -halfDepth), new Vector3(-halfWidth, -halfHeight, -halfDepth),
            new Vector3(halfWidth, -halfHeight, -halfDepth), new Vector3(halfWidth, halfHeight, -halfDepth), new Vector3(halfWidth, halfHeight, halfDepth), new Vector3(halfWidth, -halfHeight, halfDepth),
            new Vector3(-halfWidth, halfHeight, -halfDepth), new Vector3(-halfWidth, halfHeight, halfDepth), new Vector3(halfWidth, halfHeight, halfDepth), new Vector3(halfWidth, halfHeight, -halfDepth),
            new Vector3(-halfWidth, -halfHeight, halfDepth), new Vector3(-halfWidth, -halfHeight, -halfDepth), new Vector3(halfWidth, -halfHeight, -halfDepth), new Vector3(halfWidth, -halfHeight, halfDepth)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f), new Vector2(0f, height), new Vector2(width, height), new Vector2(width, 0f),
            new Vector2(0f, 0f), new Vector2(0f, height), new Vector2(width, height), new Vector2(width, 0f),
            new Vector2(0f, 0f), new Vector2(0f, height), new Vector2(depth, height), new Vector2(depth, 0f),
            new Vector2(0f, 0f), new Vector2(0f, height), new Vector2(depth, height), new Vector2(depth, 0f),
            new Vector2(0f, 0f), new Vector2(0f, depth), new Vector2(width, depth), new Vector2(width, 0f),
            new Vector2(0f, 0f), new Vector2(0f, depth), new Vector2(width, depth), new Vector2(width, 0f)
        };
        mesh.triangles = new[]
        {
            0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7,
            8, 9, 10, 8, 10, 11, 12, 13, 14, 12, 14, 15,
            16, 17, 18, 16, 18, 19, 20, 21, 22, 20, 22, 23
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        filter.sharedMesh = mesh;

        BoxCollider collider = cuboid.AddComponent<BoxCollider>();
        collider.size = new Vector3(width, height, depth);
        return cuboid;
    }

    private static void EnsureTextureRepeats(Material material)
    {
        if (material != null && material.mainTexture != null)
        {
            material.mainTexture.wrapMode = TextureWrapMode.Repeat;
        }
    }

    private Material GetBuildingMaterial(int buildingIndex)
    {
        if (BuildingMaterials != null && BuildingMaterials.Length > 0)
        {
            Material selected = BuildingMaterials[buildingIndex % BuildingMaterials.Length];
            if (selected != null) { return selected; }
        }
        return fallbackMaterial;
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
