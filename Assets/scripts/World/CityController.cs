using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Places the bundled CITY01847 building test data on the overworld. The
/// controller intentionally only creates simple exterior boxes for this first
/// integration pass.
/// </summary>
public sealed class CityController : MonoBehaviour
{
    private const float MapPixelSize = 64f;
    private const string DefaultBuildingResource = "Cities/city_01847_buildings";

    [Header("City Placement")]
    [Tooltip("Overworld map pixel occupied by the north-west corner of the city.")]
    public Vector2Int NorthwestMapPixelOrigin;

    [Header("Building Appearance")]
    [Tooltip("Texture used by every building. Assign UW2_207 for the initial city.")]
    public Texture2D BuildingTexture;

    [Header("Terrain")]
    [Tooltip("Distance beyond the city edge over which level city ground blends back into the overworld.")]
    [Min(0f)] public float TerrainBlendDistance = MapPixelSize;

    [SerializeField, HideInInspector] private string buildingResourcePath = DefaultBuildingResource;

    private static CityController activeCity;
    private readonly List<GameObject> spawnedBuildings = new List<GameObject>();
    private CityBuildingCollection cityData;
    private Transform buildingRoot;
    private Transform overworldTerrainRoot;
    private Material buildingMaterial;
    private float terrainElevation;
    private bool hasTerrainElevation;

    public float CityWidth { get { return cityData != null ? cityData.width : 1400f; } }
    public float CityDepth { get { return cityData != null ? cityData.depth : 1000f; } }

    private void Awake()
    {
        activeCity = this;
        LoadCityData();
    }

    private void Start()
    {
        // GameWorldController normally supplies the freshly-created root. This
        // fallback also supports entering Play mode with an existing root.
        GameObject existingRoot = GameObject.Find("OverworldTerrainRoot");
        if (existingRoot != null) { AttachToOverworldTerrain(existingRoot.transform); }
    }

    private void OnEnable()
    {
        activeCity = this;
    }

    private void OnDisable()
    {
        if (activeCity == this) { activeCity = null; }
    }

    private void OnValidate()
    {
        TerrainBlendDistance = Mathf.Max(0f, TerrainBlendDistance);
    }

    public void GetTerrainAnchorSample(int samplesPerMapPixel, out int sampleX, out int sampleZ)
    {
        // The centre is less susceptible than an edge to a single local peak.
        sampleX = Mathf.RoundToInt((NorthwestMapPixelOrigin.x * samplesPerMapPixel) + (CityWidth / MapPixelSize * samplesPerMapPixel * 0.5f));
        sampleZ = Mathf.RoundToInt((NorthwestMapPixelOrigin.y * samplesPerMapPixel) + (CityDepth / MapPixelSize * samplesPerMapPixel * 0.5f));
    }

    public void SetTerrainElevation(float elevation)
    {
        terrainElevation = Mathf.Max(0f, elevation);
        hasTerrainElevation = true;

        if (buildingRoot != null)
        {
            Vector3 position = buildingRoot.position;
            position.y = terrainElevation;
            buildingRoot.position = position;
        }
    }

    /// <summary>Called by the overworld height sampler for every terrain vertex.</summary>
    public static float BlendTerrainHeight(float worldX, float worldZ, float naturalHeight)
    {
        CityController city = activeCity;
        if (city == null || !city.isActiveAndEnabled || !city.hasTerrainElevation) { return naturalHeight; }

        float west = city.NorthwestMapPixelOrigin.x * MapPixelSize;
        float north = city.NorthwestMapPixelOrigin.y * MapPixelSize;
        float east = west + city.CityWidth;
        float south = north + city.CityDepth;

        float outsideX = Mathf.Max(west - worldX, 0f, worldX - east);
        float outsideZ = Mathf.Max(north - worldZ, 0f, worldZ - south);
        float outsideDistance = Mathf.Sqrt((outsideX * outsideX) + (outsideZ * outsideZ));
        if (outsideDistance <= 0f) { return city.terrainElevation; }
        if (city.TerrainBlendDistance <= 0f || outsideDistance >= city.TerrainBlendDistance) { return naturalHeight; }

        float naturalWeight = Mathf.SmoothStep(0f, 1f, outsideDistance / city.TerrainBlendDistance);
        return Mathf.Lerp(city.terrainElevation, naturalHeight, naturalWeight);
    }

    [ContextMenu("Rebuild Buildings")]
    public void SpawnBuildings()
    {
        if (overworldTerrainRoot == null)
        {
            GameObject existingRoot = GameObject.Find("OverworldTerrainRoot");
            if (existingRoot != null) { overworldTerrainRoot = existingRoot.transform; }
        }
        if (overworldTerrainRoot == null)
        {
            Debug.LogWarning("CityController is waiting for OverworldTerrainRoot before spawning buildings.", this);
            return;
        }

        if (cityData == null) { LoadCityData(); }
        ClearBuildings();
        if (cityData == null || cityData.buildings == null) { return; }

        GameObject rootObject = new GameObject("CITY01847 Buildings");
        buildingRoot = rootObject.transform;
        buildingRoot.SetParent(overworldTerrainRoot, false);
        buildingRoot.position = new Vector3(
            NorthwestMapPixelOrigin.x * MapPixelSize,
            hasTerrainElevation ? terrainElevation : 0f,
            NorthwestMapPixelOrigin.y * MapPixelSize);

        for (int i = 0; i < cityData.buildings.Length; i++)
        {
            CityBuilding building = cityData.buildings[i];
            if (building == null || building.outer == null || building.outer.Length < 3) { continue; }
            CreateBuilding(building);
        }
    }

    /// <summary>
    /// Parents city geometry to the same lifecycle root as the streamed
    /// overworld chunks, then creates the buildings after that root exists.
    /// </summary>
    public void AttachToOverworldTerrain(Transform terrainRoot)
    {
        if (terrainRoot == null) { return; }
        overworldTerrainRoot = terrainRoot;
        SpawnBuildings();
    }

    private void LoadCityData()
    {
        TextAsset json = Resources.Load<TextAsset>(string.IsNullOrEmpty(buildingResourcePath) ? DefaultBuildingResource : buildingResourcePath);
        if (json == null)
        {
            Debug.LogError("CityController could not load Resources/" + buildingResourcePath + ".json", this);
            return;
        }

        cityData = JsonUtility.FromJson<CityBuildingCollection>(json.text);
    }

    private void CreateBuilding(CityBuilding building)
    {
        float minX = float.PositiveInfinity;
        float minZ = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxZ = float.NegativeInfinity;
        for (int i = 0; i < building.outer.Length; i++)
        {
            float[] point = building.outer[i];
            if (point == null || point.Length < 2) { continue; }
            minX = Mathf.Min(minX, point[0]);
            maxX = Mathf.Max(maxX, point[0]);
            minZ = Mathf.Min(minZ, point[1]);
            maxZ = Mathf.Max(maxZ, point[1]);
        }

        if (float.IsInfinity(minX) || maxX <= minX || maxZ <= minZ) { return; }
        float height = Mathf.Max(0.1f, building.eave_height_m);
        Mesh mesh = CreateTiledCuboid(maxX - minX, height, maxZ - minZ);
        GameObject instance = new GameObject(string.IsNullOrEmpty(building.local_entity_id) ? "City Building" : building.local_entity_id);
        instance.transform.SetParent(buildingRoot, false);
        instance.transform.localPosition = new Vector3((minX + maxX) * 0.5f, height * 0.5f, (minZ + maxZ) * 0.5f);

        MeshFilter filter = instance.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = instance.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = GetBuildingMaterial();
        MeshCollider collider = instance.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        spawnedBuildings.Add(instance);
    }

    private void ClearBuildings()
    {
        for (int i = spawnedBuildings.Count - 1; i >= 0; i--)
        {
            if (spawnedBuildings[i] != null)
            {
                if (Application.isPlaying) { Destroy(spawnedBuildings[i]); }
                else { DestroyImmediate(spawnedBuildings[i]); }
            }
        }
        spawnedBuildings.Clear();

        if (buildingRoot != null)
        {
            if (Application.isPlaying) { Destroy(buildingRoot.gameObject); }
            else { DestroyImmediate(buildingRoot.gameObject); }
            buildingRoot = null;
        }
    }

    private Material GetBuildingMaterial()
    {
        if (buildingMaterial != null) { return buildingMaterial; }
        Shader shader = Shader.Find("Standard");
        buildingMaterial = new Material(shader);
        buildingMaterial.name = "City Building (UW2_207)";
        buildingMaterial.mainTexture = BuildingTexture;
        if (BuildingTexture != null) { BuildingTexture.wrapMode = TextureWrapMode.Repeat; }
        return buildingMaterial;
    }

    private static Mesh CreateTiledCuboid(float width, float height, float depth)
    {
        float x = width * 0.5f;
        float y = height * 0.5f;
        float z = depth * 0.5f;
        Vector3[] vertices =
        {
            new Vector3(-x,-y,-z), new Vector3(x,-y,-z), new Vector3(x,y,-z), new Vector3(-x,y,-z),
            new Vector3(x,-y,-z), new Vector3(x,-y,z), new Vector3(x,y,z), new Vector3(x,y,-z),
            new Vector3(x,-y,z), new Vector3(-x,-y,z), new Vector3(-x,y,z), new Vector3(x,y,z),
            new Vector3(-x,-y,z), new Vector3(-x,-y,-z), new Vector3(-x,y,-z), new Vector3(-x,y,z),
            new Vector3(-x,y,-z), new Vector3(x,y,-z), new Vector3(x,y,z), new Vector3(-x,y,z),
            new Vector3(-x,-y,z), new Vector3(x,-y,z), new Vector3(x,-y,-z), new Vector3(-x,-y,-z)
        };
        Vector2[] uvs =
        {
            new Vector2(0,0), new Vector2(width,0), new Vector2(width,height), new Vector2(0,height),
            new Vector2(0,0), new Vector2(depth,0), new Vector2(depth,height), new Vector2(0,height),
            new Vector2(0,0), new Vector2(width,0), new Vector2(width,height), new Vector2(0,height),
            new Vector2(0,0), new Vector2(depth,0), new Vector2(depth,height), new Vector2(0,height),
            new Vector2(0,0), new Vector2(width,0), new Vector2(width,depth), new Vector2(0,depth),
            new Vector2(0,0), new Vector2(width,0), new Vector2(width,depth), new Vector2(0,depth)
        };
        int[] triangles =
        {
            0,2,1, 0,3,2, 4,6,5, 4,7,6, 8,10,9, 8,11,10,
            12,14,13, 12,15,14, 16,18,17, 16,19,18, 20,22,21, 20,23,22
        };
        Mesh mesh = new Mesh();
        mesh.name = "Tiled City Cuboid";
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    [Serializable]
    private sealed class CityBuildingCollection
    {
        public float width;
        public float depth;
        public CityBuilding[] buildings;
    }

    [Serializable]
    private sealed class CityBuilding
    {
        public string local_entity_id;
        public float[][] outer;
        public float eave_height_m;
    }
}
