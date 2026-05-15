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
    private int[] quadOrder;
    private Rect[] atlasRects;

    public void Initialize(Vector3[] vertices, int[] grassTriangles, OverworldNatureFlatsController flats, float waterSurfaceEpsilon, Vector2Int chunkCoord)
    {
        if (vertices == null || grassTriangles == null || flats == null || !flats.EnableNatureFlats) { return; }

        Material atlasMaterial = BuildAtlasMaterial(flats, out atlasRects);
        if (atlasMaterial == null || atlasRects == null || atlasRects.Length == 0) { return; }

        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = atlasMaterial;

        List<int> candidates = new List<int>(grassTriangles.Length / 3);
        for (int i = 0; i < grassTriangles.Length; i += 3)
        {
            Vector3 center = (vertices[grassTriangles[i]] + vertices[grassTriangles[i + 1]] + vertices[grassTriangles[i + 2]]) / 3f;
            if (center.y <= waterSurfaceEpsilon) { continue; }
            float cluster = SampleClusterNoise(center, flats);
            float threshold = Mathf.Lerp(flats.BaseDensity, flats.ClusterDensity, cluster);
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

        int treeCount = flats.TreeMaterials != null ? flats.TreeMaterials.Length : 0;
        int terrainCount = flats.TerrainSpriteMaterials != null ? flats.TerrainSpriteMaterials.Length : 0;

        for (int q = 0; q < quadCount; q++)
        {
            int triStart = selected[q];
            Vector3 center = (vertices[grassTriangles[triStart]] + vertices[grassTriangles[triStart + 1]] + vertices[grassTriangles[triStart + 2]]) / 3f;
            float selector = Deterministic01(center + new Vector3(8.1f, 0f, 11.2f), flats.NatureSeed);
            bool useTerrain = (terrainCount > 0) && (selector < flats.TerrainSpriteChance || treeCount == 0);

            int spriteIndex;
            float baseWidth;
            float baseHeight;
            if (useTerrain)
            {
                int choice = Mathf.FloorToInt(Deterministic01(center + new Vector3(19.2f, 0f, 5.6f), flats.NatureSeed) * terrainCount);
                spriteIndex = Mathf.Clamp(treeCount + choice, 0, atlasRects.Length - 1);
                baseWidth = flats.TerrainSpriteWidth;
                baseHeight = flats.TerrainSpriteHeight;
            }
            else
            {
                int choice = Mathf.FloorToInt(Deterministic01(center + new Vector3(-9.2f, 0f, -15.6f), flats.NatureSeed) * treeCount);
                spriteIndex = Mathf.Clamp(choice, 0, atlasRects.Length - 1);
                baseWidth = flats.TreeWidth;
                baseHeight = flats.TreeHeight;
            }

            float hJ = Mathf.Lerp(0.9f, 1.1f, Deterministic01(center + new Vector3(13.1f, 0f, -6.3f), flats.NatureSeed));
            float wJ = Mathf.Lerp(0.8f, 1.2f, Deterministic01(center + new Vector3(-2.3f, 0f, 4.7f), flats.NatureSeed));
            quadCenters[q] = center + (Vector3.up * flats.GroundOffset);
            quadWidths[q] = baseWidth * wJ;
            quadHeights[q] = baseHeight * hJ;
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

    private static Material BuildAtlasMaterial(OverworldNatureFlatsController flats, out Rect[] rects)
    {
        rects = null;
        List<Texture2D> textures = new List<Texture2D>();
        Material baseMat = null;

        void AddFrom(Material[] mats)
        {
            if (mats == null) { return; }
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) { continue; }
                Texture2D tex = mats[i].mainTexture as Texture2D;
                if (tex == null) { continue; }
                if (baseMat == null) { baseMat = mats[i]; }
                textures.Add(tex);
            }
        }

        AddFrom(flats.TreeMaterials);
        AddFrom(flats.TerrainSpriteMaterials);

        if (baseMat == null || textures.Count == 0) { return null; }

        Texture2D atlas = new Texture2D(2048, 2048, TextureFormat.RGBA32, false);
        rects = atlas.PackTextures(textures.ToArray(), 2, 2048, false);
        atlas.wrapMode = TextureWrapMode.Clamp;
        atlas.filterMode = FilterMode.Point;

        Material m = new Material(baseMat);
        m.mainTexture = atlas;
        return m;
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
