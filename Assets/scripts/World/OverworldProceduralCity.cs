using System;
using UnityEngine;

/// <summary>
/// Minimal CITY01847 prototype: a blended terrain plateau and one textured
/// cuboid per exported building. Streaming, roads, doors, and interiors are
/// deliberately outside this prototype's scope.
/// </summary>
public sealed class OverworldProceduralCity
{
    private const float GeneratorUnitsPerMapPixel = 64f;
    private const float CanvasWidthUnits = 1400f;
    private const float CanvasHeightUnits = 1000f;

    private readonly bool enabled;
    private readonly float minX;
    private readonly float minZ;
    private readonly float maxX;
    private readonly float maxZ;
    private readonly float blendDistance;
    private readonly float plateauHeight;

    public OverworldProceduralCity(OverworldTerrainController settings, Func<int, int, float> sampleNaturalHeight)
    {
        enabled = settings != null && settings.SpawnProceduralCity;
        if (!enabled) { return; }

        minX = settings.ProceduralCityNorthwest.x;
        minZ = settings.ProceduralCityNorthwest.y;
        maxX = minX + (CanvasWidthUnits / GeneratorUnitsPerMapPixel);
        maxZ = minZ + (CanvasHeightUnits / GeneratorUnitsPerMapPixel);
        blendDistance = Mathf.Max(0.01f, settings.ProceduralCityTerrainBlend);

        int centreX = Mathf.RoundToInt((minX + maxX) * 0.5f);
        int centreZ = Mathf.RoundToInt((minZ + maxZ) * 0.5f);
        plateauHeight = Mathf.Max(0f, sampleNaturalHeight(centreX, centreZ));
    }

    public float BlendTerrainHeight(float sampleX, float sampleZ, float naturalHeight)
    {
        if (!enabled) { return naturalHeight; }

        float outsideX = Mathf.Max(minX - sampleX, sampleX - maxX, 0f);
        float outsideZ = Mathf.Max(minZ - sampleZ, sampleZ - maxZ, 0f);
        float outsideDistance = Mathf.Sqrt((outsideX * outsideX) + (outsideZ * outsideZ));
        if (outsideDistance >= blendDistance) { return naturalHeight; }

        // SmoothStep has a zero derivative at both ends, avoiding a visible
        // crease at the plateau edge and where it meets untouched terrain.
        float naturalWeight = Mathf.SmoothStep(0f, 1f, outsideDistance / blendDistance);
        return Mathf.Lerp(plateauHeight, naturalHeight, naturalWeight);
    }

    public void SpawnBuildings(Transform parent, OverworldTerrainController settings, Material material)
    {
        if (!enabled || parent == null || settings == null) { return; }

        TextAsset source = Resources.Load<TextAsset>(settings.ProceduralCityResourcePath);
        if (source == null)
        {
            Debug.LogWarning("Could not load procedural city buildings at Resources/" + settings.ProceduralCityResourcePath);
            return;
        }

        ProceduralCityCuboidCollection city = JsonUtility.FromJson<ProceduralCityCuboidCollection>(source.text);
        if (city == null || city.buildings == null)
        {
            Debug.LogWarning("Procedural city building data is empty or invalid.");
            return;
        }

        GameObject root = new GameObject("ProceduralCity_CITY01847");
        root.transform.SetParent(parent, false);
        float mapPixelWorldSize = settings.EffectiveTileWorldSize;
        float generatorWorldScale = mapPixelWorldSize / GeneratorUnitsPerMapPixel;
        float originWorldX = settings.ProceduralCityNorthwest.x * mapPixelWorldSize;
        float originWorldZ = settings.ProceduralCityNorthwest.y * mapPixelWorldSize;

        for (int i = 0; i < city.buildings.Length; i++)
        {
            ProceduralCityCuboid building = city.buildings[i];
            float width = Mathf.Max(0.01f, (building.max_x - building.min_x) * generatorWorldScale);
            float depth = Mathf.Max(0.01f, (building.max_y - building.min_y) * generatorWorldScale);
            float height = Mathf.Max(0.01f, building.height * generatorWorldScale);

            GameObject cuboid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cuboid.name = string.IsNullOrEmpty(building.id) ? "Building" : building.id;
            cuboid.transform.SetParent(root.transform, false);
            cuboid.transform.position = new Vector3(
                originWorldX + (((building.min_x + building.max_x) * 0.5f) * generatorWorldScale),
                plateauHeight + (height * 0.5f),
                originWorldZ + (((building.min_y + building.max_y) * 0.5f) * generatorWorldScale));
            cuboid.transform.localScale = new Vector3(width, height, depth);

            MeshRenderer renderer = cuboid.GetComponent<MeshRenderer>();
            if (renderer != null && material != null) { renderer.sharedMaterial = material; }
        }

        Debug.Log("Spawned " + city.buildings.Length + " CITY01847 building cuboids at overworld map pixel " + settings.ProceduralCityNorthwest + ".");
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
