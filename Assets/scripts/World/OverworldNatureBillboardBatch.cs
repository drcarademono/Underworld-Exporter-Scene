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
    private Vector3[] meshVerts;
    private Vector2[] meshUvs;
    private int[] meshTris;

    public void Initialize(Vector3[] vertices, int[] grassTriangles, OverworldTerrainController settings, Vector2Int chunkCoord)
    {
        if (vertices == null || grassTriangles == null || settings == null) { return; }
        if (settings.NatureBillboardMaterial == null || !settings.EnableNatureBillboards) { return; }

        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = settings.NatureBillboardMaterial;

        List<int> candidates = new List<int>(grassTriangles.Length / 3);
        for (int i = 0; i < grassTriangles.Length; i += 3)
        {
            Vector3 center = (vertices[grassTriangles[i]] + vertices[grassTriangles[i + 1]] + vertices[grassTriangles[i + 2]]) / 3f;
            if (center.y <= settings.WaterSurfaceEpsilon) { continue; }

            float clusterNoise = SampleClusterNoise(center, settings);
            float threshold = Mathf.Lerp(settings.NatureBillboardBaseDensity, settings.NatureBillboardClusterDensity, clusterNoise);
            if (Deterministic01(center, settings.NatureBillboardSeed) <= threshold)
            {
                candidates.Add(i);
            }
        }

        if (candidates.Count == 0) { return; }

        int maxCount = Mathf.Max(0, settings.MaxNatureBillboardsPerChunk);
        float keepProb = (maxCount <= 0 || candidates.Count <= maxCount) ? 1f : (maxCount / (float)candidates.Count);

        List<int> selected = new List<int>(Mathf.Min(candidates.Count, Mathf.Max(1, maxCount)));
        for (int c = 0; c < candidates.Count; c++)
        {
            int triStart = candidates[c];
            Vector3 center = (vertices[grassTriangles[triStart]] + vertices[grassTriangles[triStart + 1]] + vertices[grassTriangles[triStart + 2]]) / 3f;
            if (keepProb >= 1f || Deterministic01(center + new Vector3(37.17f, 0f, -91.73f), settings.NatureBillboardSeed) <= keepProb)
            {
                selected.Add(triStart);
            }
        }

        if (selected.Count == 0) { return; }

        int quadCount = selected.Count;
        quadCenters = new Vector3[quadCount];
        quadWidths = new float[quadCount];
        quadHeights = new float[quadCount];
        meshVerts = new Vector3[quadCount * VertsPerQuad];
        meshUvs = new Vector2[quadCount * VertsPerQuad];
        meshTris = new int[quadCount * TrisPerQuad];

        for (int q = 0; q < quadCount; q++)
        {
            int triStart = selected[q];
            Vector3 center = (vertices[grassTriangles[triStart]] + vertices[grassTriangles[triStart + 1]] + vertices[grassTriangles[triStart + 2]]) / 3f;
            float heightJitter = Mathf.Lerp(0.9f, 1.1f, Deterministic01(center + new Vector3(13.1f, 0f, -6.3f), settings.NatureBillboardSeed));
            float widthJitter = Mathf.Lerp(0.8f, 1.2f, Deterministic01(center + new Vector3(-2.3f, 0f, 4.7f), settings.NatureBillboardSeed));

            quadCenters[q] = center + (Vector3.up * settings.NatureBillboardGroundOffset);
            quadWidths[q] = settings.NatureBillboardWidth * widthJitter;
            quadHeights[q] = settings.NatureBillboardHeight * heightJitter;

            int vi = q * VertsPerQuad;
            meshUvs[vi + 0] = new Vector2(0f, 0f);
            meshUvs[vi + 1] = new Vector2(1f, 0f);
            meshUvs[vi + 2] = new Vector2(0f, 1f);
            meshUvs[vi + 3] = new Vector2(1f, 1f);

            int ti = q * TrisPerQuad;
            meshTris[ti + 0] = vi + 0;
            meshTris[ti + 1] = vi + 2;
            meshTris[ti + 2] = vi + 1;
            meshTris[ti + 3] = vi + 1;
            meshTris[ti + 4] = vi + 2;
            meshTris[ti + 5] = vi + 3;
        }

        batchMesh = new Mesh();
        batchMesh.name = $"NatureBillboards_{chunkCoord.x}_{chunkCoord.y}";
        batchMesh.indexFormat = (meshVerts.Length > 65535) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;

        RebuildBillboardVerts();
        batchMesh.vertices = meshVerts;
        batchMesh.uv = meshUvs;
        batchMesh.triangles = meshTris;
        batchMesh.RecalculateNormals();
        batchMesh.RecalculateBounds();
        meshFilter.sharedMesh = batchMesh;
    }

    private void LateUpdate()
    {
        if (batchMesh == null || UWCharacter.Instance == null) { return; }
        RebuildBillboardVerts();
        batchMesh.vertices = meshVerts;
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
            int vi = q * VertsPerQuad;
            Vector3 center = quadCenters[q];
            float halfWidth = quadWidths[q] * 0.5f;
            float height = quadHeights[q];
            Vector3 side = right * halfWidth;

            meshVerts[vi + 0] = center - side;
            meshVerts[vi + 1] = center + side;
            meshVerts[vi + 2] = center - side + (Vector3.up * height);
            meshVerts[vi + 3] = center + side + (Vector3.up * height);
        }
    }

    private static float SampleClusterNoise(Vector3 worldPos, OverworldTerrainController settings)
    {
        float sampleX = (worldPos.x + settings.NatureBillboardSeed) * settings.NatureBillboardPerlinScale;
        float sampleY = (worldPos.z + settings.NatureBillboardSeed * 0.37f) * settings.NatureBillboardPerlinScale;
        return Mathf.SmoothStep(0f, 1f, Mathf.PerlinNoise(sampleX, sampleY));
    }

    private static float Deterministic01(Vector3 worldPos, int seed)
    {
        unchecked
        {
            int hx = Mathf.FloorToInt(worldPos.x * 1000f);
            int hz = Mathf.FloorToInt(worldPos.z * 1000f);
            uint h = ((uint)hx * 374761393u) + ((uint)hz * 668265263u) + ((uint)seed * 2246822519u);
            h ^= h >> 13;
            h *= 1274126177u;
            h ^= h >> 16;
            return (h & 0x00FFFFFF) / 16777215f;
        }
    }
}
