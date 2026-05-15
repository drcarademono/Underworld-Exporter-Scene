using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class OverworldNatureBillboardBatch : MonoBehaviour
{
    private const int VertsPerQuad = 4;
    private const int TrisPerQuad = 6;

    private class SubBatch
    {
        public Mesh Mesh;
        public MeshFilter Filter;
        public MeshRenderer Renderer;
        public Vector3[] Centers;
        public float[] Widths;
        public float[] Heights;
        public int[] Order;
        public Vector3[] Verts;
        public Vector2[] Uvs;
        public int[] Tris;
    }

    private readonly List<SubBatch> subBatches = new List<SubBatch>();

    public void Initialize(Vector3[] vertices, int[] grassTriangles, OverworldNatureFlatsController flats, float waterSurfaceEpsilon, Vector2Int chunkCoord)
    {
        if (vertices == null || grassTriangles == null || flats == null || !flats.EnableNatureFlats) { return; }

        Material[] materials = BuildMaterialList(flats);
        if (materials.Length == 0) { return; }

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

        List<int>[] perMaterial = new List<int>[materials.Length];
        for (int i = 0; i < perMaterial.Length; i++) { perMaterial[i] = new List<int>(); }

        int treeCount = flats.TreeMaterials != null ? flats.TreeMaterials.Length : 0;
        int terrainCount = flats.TerrainSpriteMaterials != null ? flats.TerrainSpriteMaterials.Length : 0;

        for (int c = 0; c < candidates.Count; c++)
        {
            int triStart = candidates[c];
            Vector3 center = (vertices[grassTriangles[triStart]] + vertices[grassTriangles[triStart + 1]] + vertices[grassTriangles[triStart + 2]]) / 3f;
            if (!(keepProb >= 1f || Deterministic01(center + new Vector3(37.17f, 0f, -91.73f), flats.NatureSeed) <= keepProb)) { continue; }

            float selector = Deterministic01(center + new Vector3(8.1f, 0f, 11.2f), flats.NatureSeed);
            bool useTerrain = (terrainCount > 0) && (selector < flats.TerrainSpriteChance || treeCount == 0);
            int matIndex = 0;
            if (useTerrain)
            {
                int choice = Mathf.FloorToInt(Deterministic01(center + new Vector3(19.2f, 0f, 5.6f), flats.NatureSeed) * terrainCount);
                matIndex = Mathf.Clamp(treeCount + choice, 0, materials.Length - 1);
            }
            else
            {
                int choice = Mathf.FloorToInt(Deterministic01(center + new Vector3(-9.2f, 0f, -15.6f), flats.NatureSeed) * treeCount);
                matIndex = Mathf.Clamp(choice, 0, materials.Length - 1);
            }
            perMaterial[matIndex].Add(triStart);
        }

        for (int m = 0; m < materials.Length; m++)
        {
            if (perMaterial[m].Count == 0) { continue; }
            BuildSubBatch(perMaterial[m], vertices, grassTriangles, flats, materials[m], chunkCoord, m);
        }
    }

    private void BuildSubBatch(List<int> selectedTriangles, Vector3[] vertices, int[] grassTriangles, OverworldNatureFlatsController flats, Material material, Vector2Int chunkCoord, int materialIndex)
    {
        int quadCount = selectedTriangles.Count;
        SubBatch sb = new SubBatch();
        sb.Centers = new Vector3[quadCount];
        sb.Widths = new float[quadCount];
        sb.Heights = new float[quadCount];
        sb.Order = new int[quadCount];
        sb.Verts = new Vector3[quadCount * VertsPerQuad];
        sb.Uvs = new Vector2[quadCount * VertsPerQuad];
        sb.Tris = new int[quadCount * TrisPerQuad];

        for (int q = 0; q < quadCount; q++)
        {
            int triStart = selectedTriangles[q];
            Vector3 center = (vertices[grassTriangles[triStart]] + vertices[grassTriangles[triStart + 1]] + vertices[grassTriangles[triStart + 2]]) / 3f;
            float selector = Deterministic01(center + new Vector3(8.1f, 0f, 11.2f), flats.NatureSeed);
            bool useTerrain = selector < flats.TerrainSpriteChance;
            float baseWidth = useTerrain ? flats.TerrainSpriteWidth : flats.TreeWidth;
            float baseHeight = useTerrain ? flats.TerrainSpriteHeight : flats.TreeHeight;

            float hJ = Mathf.Lerp(0.9f, 1.1f, Deterministic01(center + new Vector3(13.1f, 0f, -6.3f), flats.NatureSeed));
            float wJ = Mathf.Lerp(0.8f, 1.2f, Deterministic01(center + new Vector3(-2.3f, 0f, 4.7f), flats.NatureSeed));
            sb.Centers[q] = center + (Vector3.up * flats.GroundOffset);
            sb.Widths[q] = baseWidth * wJ;
            sb.Heights[q] = baseHeight * hJ;
            sb.Order[q] = q;

            int vi = q * VertsPerQuad;
            sb.Uvs[vi + 0] = new Vector2(0f, 0f); sb.Uvs[vi + 1] = new Vector2(1f, 0f);
            sb.Uvs[vi + 2] = new Vector2(0f, 1f); sb.Uvs[vi + 3] = new Vector2(1f, 1f);
        }

        GameObject child = new GameObject("NatureSubBatch_" + materialIndex);
        child.transform.SetParent(transform, false);
        sb.Filter = child.AddComponent<MeshFilter>();
        sb.Renderer = child.AddComponent<MeshRenderer>();
        sb.Renderer.sharedMaterial = material;

        sb.Mesh = new Mesh();
        sb.Mesh.name = $"NatureBillboards_{chunkCoord.x}_{chunkCoord.y}_{materialIndex}";
        sb.Mesh.indexFormat = (sb.Verts.Length > 65535) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        RebuildSubBatchGeometry(sb);
        sb.Mesh.vertices = sb.Verts;
        sb.Mesh.uv = sb.Uvs;
        sb.Mesh.triangles = sb.Tris;
        sb.Mesh.RecalculateBounds();
        sb.Filter.sharedMesh = sb.Mesh;
        subBatches.Add(sb);
    }

    private void LateUpdate()
    {
        if (UWCharacter.Instance == null) { return; }
        for (int i = 0; i < subBatches.Count; i++)
        {
            SubBatch sb = subBatches[i];
            RebuildSubBatchGeometry(sb);
            sb.Mesh.vertices = sb.Verts;
            sb.Mesh.triangles = sb.Tris;
            sb.Mesh.RecalculateBounds();
        }
    }

    private void RebuildSubBatchGeometry(SubBatch sb)
    {
        Vector3 camForward = UWCharacter.Instance != null ? UWCharacter.Instance.dirForNPC : Vector3.forward;
        if (camForward.sqrMagnitude < 0.0001f) { camForward = Vector3.forward; }
        Vector3 right = Vector3.Cross(Vector3.up, camForward).normalized;
        if (right.sqrMagnitude < 0.0001f) { right = Vector3.right; }

        Vector3 camPos = (Camera.main != null) ? Camera.main.transform.position : UWCharacter.Instance.CameraPos;
        System.Array.Sort(sb.Order, (a, b) => (sb.Centers[b] - camPos).sqrMagnitude.CompareTo((sb.Centers[a] - camPos).sqrMagnitude));

        for (int q = 0; q < sb.Centers.Length; q++)
        {
            int vi = q * VertsPerQuad;
            Vector3 side = right * (sb.Widths[q] * 0.5f);
            sb.Verts[vi + 0] = sb.Centers[q] - side;
            sb.Verts[vi + 1] = sb.Centers[q] + side;
            sb.Verts[vi + 2] = sb.Centers[q] - side + (Vector3.up * sb.Heights[q]);
            sb.Verts[vi + 3] = sb.Centers[q] + side + (Vector3.up * sb.Heights[q]);
        }

        for (int sorted = 0; sorted < sb.Order.Length; sorted++)
        {
            int q = sb.Order[sorted];
            int vi = q * VertsPerQuad;
            int ti = sorted * TrisPerQuad;
            sb.Tris[ti + 0] = vi + 0; sb.Tris[ti + 1] = vi + 2; sb.Tris[ti + 2] = vi + 1;
            sb.Tris[ti + 3] = vi + 1; sb.Tris[ti + 4] = vi + 2; sb.Tris[ti + 5] = vi + 3;
        }
    }

    private static Material[] BuildMaterialList(OverworldNatureFlatsController flats)
    {
        List<Material> mats = new List<Material>();
        if (flats.TreeMaterials != null) { for (int i = 0; i < flats.TreeMaterials.Length; i++) if (flats.TreeMaterials[i] != null) mats.Add(flats.TreeMaterials[i]); }
        if (flats.TerrainSpriteMaterials != null) { for (int i = 0; i < flats.TerrainSpriteMaterials.Length; i++) if (flats.TerrainSpriteMaterials[i] != null) mats.Add(flats.TerrainSpriteMaterials[i]); }
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
