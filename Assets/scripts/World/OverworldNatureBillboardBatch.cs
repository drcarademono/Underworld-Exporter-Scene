using System;
using UnityEngine;

[DisallowMultipleComponent]
public class OverworldNatureBillboardBatch : MonoBehaviour
{
    private const int VertsPerQuad = 4;
    private const int TrisPerQuad = 6;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh batchMesh;
    private bool initialized = false;

    public void Initialize(Vector3[] vertices, int[] grassTriangles, OverworldTerrainController settings, Vector2Int chunkCoord)
    {
        if (initialized || vertices == null || grassTriangles == null || settings == null) { return; }
        initialized = true;

        if (settings.NatureBillboardMaterial == null || !settings.EnableNatureBillboards) { return; }

        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = settings.NatureBillboardMaterial;

        int quadBudget = Mathf.Min(settings.MaxNatureBillboardsPerChunk, Mathf.Max(0, grassTriangles.Length / 3));
        if (quadBudget <= 0) { return; }

        Vector3[] outVerts = new Vector3[quadBudget * VertsPerQuad];
        Vector2[] outUvs = new Vector2[quadBudget * VertsPerQuad];
        int[] outTris = new int[quadBudget * TrisPerQuad];
        int quadCount = 0;

        for (int i = 0; i < grassTriangles.Length; i += 3)
        {
            if (quadCount >= quadBudget) { break; }

            Vector3 center = (vertices[grassTriangles[i]] + vertices[grassTriangles[i + 1]] + vertices[grassTriangles[i + 2]]) / 3f;
            float placement = SampleClusterNoise(center, settings);
            float threshold = Mathf.Lerp(settings.NatureBillboardBaseDensity, settings.NatureBillboardClusterDensity, placement);
            if (Deterministic01(center, settings.NatureBillboardSeed) > threshold) { continue; }

            float heightJitter = Mathf.Lerp(0.9f, 1.1f, Deterministic01(center + new Vector3(13.1f, 0f, -6.3f), settings.NatureBillboardSeed));
            float widthJitter = Mathf.Lerp(0.8f, 1.2f, Deterministic01(center + new Vector3(-2.3f, 0f, 4.7f), settings.NatureBillboardSeed));
            float quadHeight = settings.NatureBillboardHeight * heightJitter;
            float quadWidth = settings.NatureBillboardWidth * widthJitter;
            float yOffset = settings.NatureBillboardGroundOffset;

            int vi = quadCount * VertsPerQuad;
            outVerts[vi + 0] = new Vector3(center.x - (quadWidth * 0.5f), center.y + yOffset, center.z);
            outVerts[vi + 1] = new Vector3(center.x + (quadWidth * 0.5f), center.y + yOffset, center.z);
            outVerts[vi + 2] = new Vector3(center.x - (quadWidth * 0.5f), center.y + yOffset + quadHeight, center.z);
            outVerts[vi + 3] = new Vector3(center.x + (quadWidth * 0.5f), center.y + yOffset + quadHeight, center.z);

            outUvs[vi + 0] = new Vector2(0f, 0f);
            outUvs[vi + 1] = new Vector2(1f, 0f);
            outUvs[vi + 2] = new Vector2(0f, 1f);
            outUvs[vi + 3] = new Vector2(1f, 1f);

            int ti = quadCount * TrisPerQuad;
            outTris[ti + 0] = vi + 0;
            outTris[ti + 1] = vi + 2;
            outTris[ti + 2] = vi + 1;
            outTris[ti + 3] = vi + 1;
            outTris[ti + 4] = vi + 2;
            outTris[ti + 5] = vi + 3;
            quadCount++;
        }

        if (quadCount <= 0) { return; }

        Array.Resize(ref outVerts, quadCount * VertsPerQuad);
        Array.Resize(ref outUvs, quadCount * VertsPerQuad);
        Array.Resize(ref outTris, quadCount * TrisPerQuad);

        batchMesh = new Mesh();
        batchMesh.name = $"NatureBillboards_{chunkCoord.x}_{chunkCoord.y}";
        batchMesh.indexFormat = (outVerts.Length > 65535) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        batchMesh.vertices = outVerts;
        batchMesh.uv = outUvs;
        batchMesh.triangles = outTris;
        batchMesh.RecalculateBounds();
        meshFilter.sharedMesh = batchMesh;
    }

    private static float SampleClusterNoise(Vector3 worldPos, OverworldTerrainController settings)
    {
        float n = Mathf.PerlinNoise((worldPos.x + settings.NatureBillboardSeed) * settings.NatureBillboardPerlinScale, (worldPos.z + settings.NatureBillboardSeed * 0.37f) * settings.NatureBillboardPerlinScale);
        return Mathf.SmoothStep(0f, 1f, n);
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

    private void LateUpdate()
    {
        if (meshRenderer == null || !meshRenderer.enabled || UWCharacter.Instance == null) { return; }
        Vector3 camDir = UWCharacter.Instance.dirForNPC;
        if (camDir == Vector3.zero) { return; }
        transform.rotation = Quaternion.LookRotation(camDir, Vector3.up);
    }
}
