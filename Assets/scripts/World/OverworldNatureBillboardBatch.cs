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
    private int[] quadMatIndex;

    private Vector3[] meshVerts;
    private Vector2[] meshUvs;
    private int[] quadOrder;
    private List<int>[] submeshTriangles;
    private Material[] runtimeMaterials;

    public void Initialize(Vector3[] vertices, int[] grassTriangles, OverworldNatureFlatsController flats, float waterSurfaceEpsilon, Vector2Int chunkCoord)
    {
        if (vertices == null || grassTriangles == null || flats == null) { return; }
        if (!flats.EnableNatureFlats) { return; }

        runtimeMaterials = BuildMaterialList(flats);
        if (runtimeMaterials.Length == 0) { return; }

        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterials = runtimeMaterials;

        List<int> candidates = new List<int>(grassTriangles.Length / 3);
        for (int i = 0; i < grassTriangles.Length; i += 3)
        {
            Vector3 center = (vertices[grassTriangles[i]] + vertices[grassTriangles[i + 1]] + vertices[grassTriangles[i + 2]]) / 3f;
            if (center.y <= waterSurfaceEpsilon) { continue; }

            float clusterNoise = SampleClusterNoise(center, flats);
            float threshold = Mathf.Lerp(flats.BaseDensity, flats.ClusterDensity, clusterNoise);
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
        quadMatIndex = new int[quadCount];
        meshVerts = new Vector3[quadCount * VertsPerQuad];
        meshUvs = new Vector2[quadCount * VertsPerQuad];
        quadOrder = new int[quadCount];

        int treeCount = flats.TreeMaterials != null ? flats.TreeMaterials.Length : 0;
        int terrainCount = flats.TerrainSpriteMaterials != null ? flats.TerrainSpriteMaterials.Length : 0;

        for (int q = 0; q < quadCount; q++)
        {
            int triStart = selected[q];
            Vector3 center = (vertices[grassTriangles[triStart]] + vertices[grassTriangles[triStart + 1]] + vertices[grassTriangles[triStart + 2]]) / 3f;
            float selector = Deterministic01(center + new Vector3(8.1f, 0f, 11.2f), flats.NatureSeed);
            bool useTerrainSprite = (terrainCount > 0) && (selector < flats.TerrainSpriteChance || treeCount == 0);

            float widthBase = useTerrainSprite ? flats.TerrainSpriteWidth : flats.TreeWidth;
            float heightBase = useTerrainSprite ? flats.TerrainSpriteHeight : flats.TreeHeight;
            float heightJitter = Mathf.Lerp(0.9f, 1.1f, Deterministic01(center + new Vector3(13.1f, 0f, -6.3f), flats.NatureSeed));
            float widthJitter = Mathf.Lerp(0.8f, 1.2f, Deterministic01(center + new Vector3(-2.3f, 0f, 4.7f), flats.NatureSeed));

            quadCenters[q] = center + (Vector3.up * flats.GroundOffset);
            quadWidths[q] = widthBase * widthJitter;
            quadHeights[q] = heightBase * heightJitter;
            quadOrder[q] = q;

            if (useTerrainSprite)
            {
                int matChoice = Mathf.FloorToInt(Deterministic01(center + new Vector3(19.2f, 0f, 5.6f), flats.NatureSeed) * terrainCount);
                quadMatIndex[q] = treeCount + Mathf.Clamp(matChoice, 0, Mathf.Max(0, terrainCount - 1));
            }
            else
            {
                int matChoice = Mathf.FloorToInt(Deterministic01(center + new Vector3(-9.2f, 0f, -15.6f), flats.NatureSeed) * treeCount);
                quadMatIndex[q] = Mathf.Clamp(matChoice, 0, Mathf.Max(0, treeCount - 1));
            }

            int vi = q * VertsPerQuad;
            meshUvs[vi + 0] = new Vector2(0f, 0f);
            meshUvs[vi + 1] = new Vector2(1f, 0f);
            meshUvs[vi + 2] = new Vector2(0f, 1f);
            meshUvs[vi + 3] = new Vector2(1f, 1f);
        }

        submeshTriangles = new List<int>[runtimeMaterials.Length];
        for (int i = 0; i < submeshTriangles.Length; i++) { submeshTriangles[i] = new List<int>(quadCount * TrisPerQuad / runtimeMaterials.Length + 6); }

        batchMesh = new Mesh();
        batchMesh.name = $"NatureBillboards_{chunkCoord.x}_{chunkCoord.y}";
        batchMesh.indexFormat = (meshVerts.Length > 65535) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        batchMesh.subMeshCount = runtimeMaterials.Length;

        RebuildBillboardVerts();
        RebuildTriangleOrder();
        batchMesh.vertices = meshVerts;
        batchMesh.uv = meshUvs;
        for (int sm = 0; sm < submeshTriangles.Length; sm++) { batchMesh.SetTriangles(submeshTriangles[sm], sm); }
        batchMesh.RecalculateNormals();
        batchMesh.RecalculateBounds();
        meshFilter.sharedMesh = batchMesh;
    }

    private void LateUpdate()
    {
        if (batchMesh == null || UWCharacter.Instance == null) { return; }
        RebuildBillboardVerts();
        RebuildTriangleOrder();
        batchMesh.vertices = meshVerts;
        for (int sm = 0; sm < submeshTriangles.Length; sm++) { batchMesh.SetTriangles(submeshTriangles[sm], sm); }
        batchMesh.RecalculateBounds();
    }

    private void RebuildBillboardVerts()
    {
        if (quadCenters == null || quadCenters.Length == 0) { return; }
        Vector3 camForward = UWCharacter.Instance != null ? UWCharacter.Instance.dirForNPC : Vector3.forward;
        if (camForward.sqrMagnitude < 0.0001f) { camForward = Vector3.forward; }
        Vector3 right = Vector3.Cross(Vector3.up, camForward).normalized;
        if (right.sqrMagnitude < 0.0001f) { right = Vector3.right; }

        for (int q = 0; q < quadCenters.Length; q++)
        {
            quadOrder[q] = q;
            int vi = q * VertsPerQuad;
            Vector3 center = quadCenters[q];
            Vector3 side = right * (quadWidths[q] * 0.5f);
            float height = quadHeights[q];
            meshVerts[vi + 0] = center - side;
            meshVerts[vi + 1] = center + side;
            meshVerts[vi + 2] = center - side + (Vector3.up * height);
            meshVerts[vi + 3] = center + side + (Vector3.up * height);
        }
    }

    private void RebuildTriangleOrder()
    {
        if (quadCenters == null || submeshTriangles == null || quadOrder == null) { return; }
        for (int i = 0; i < submeshTriangles.Length; i++) { submeshTriangles[i].Clear(); }

        Vector3 camPos = (Camera.main != null) ? Camera.main.transform.position : (UWCharacter.Instance != null ? UWCharacter.Instance.CameraPos : Vector3.zero);
        System.Array.Sort(quadOrder, (a, b) => (quadCenters[b] - camPos).sqrMagnitude.CompareTo((quadCenters[a] - camPos).sqrMagnitude));

        for (int sorted = 0; sorted < quadOrder.Length; sorted++)
        {
            int q = quadOrder[sorted];
            int vi = q * VertsPerQuad;
            List<int> tris = submeshTriangles[Mathf.Clamp(quadMatIndex[q], 0, submeshTriangles.Length - 1)];
            tris.Add(vi + 0); tris.Add(vi + 2); tris.Add(vi + 1);
            tris.Add(vi + 1); tris.Add(vi + 2); tris.Add(vi + 3);
        }
    }

    private static Material[] BuildMaterialList(OverworldNatureFlatsController flats)
    {
        List<Material> mats = new List<Material>();
        int targetQueue = 3000;
        if (flats.TreeMaterials != null)
        {
            for (int i = 0; i < flats.TreeMaterials.Length; i++)
            {
                Material src = flats.TreeMaterials[i];
                if (src == null) { continue; }
                Material inst = new Material(src);
                inst.renderQueue = targetQueue;
                mats.Add(inst);
            }
        }
        if (flats.TerrainSpriteMaterials != null)
        {
            for (int i = 0; i < flats.TerrainSpriteMaterials.Length; i++)
            {
                Material src = flats.TerrainSpriteMaterials[i];
                if (src == null) { continue; }
                Material inst = new Material(src);
                inst.renderQueue = targetQueue;
                mats.Add(inst);
            }
        }
        return mats.ToArray();
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
