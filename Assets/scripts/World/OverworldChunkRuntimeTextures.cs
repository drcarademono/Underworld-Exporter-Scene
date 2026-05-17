using UnityEngine;
using System.Collections.Generic;

public class OverworldChunkRuntimeTextures : MonoBehaviour
{
    public Material[] landRuntimeMats;
    public Texture2D tileIdMap;
    public Texture2D atlasTexture;
    public Texture2D waterMask;
    private static Shader transitionShader;

    public void EnsureMaterials(params Material[] landBaseMaterials)
    {
        if (landBaseMaterials == null || landBaseMaterials.Length == 0)
        {
            landRuntimeMats = null;
            return;
        }
        if (landRuntimeMats == null || landRuntimeMats.Length != landBaseMaterials.Length)
        {
            ReleaseRuntimeMaterials();
            landRuntimeMats = new Material[landBaseMaterials.Length];
        }
        for (int i = 0; i < landBaseMaterials.Length; i++)
        {
            if (landRuntimeMats[i] == null && landBaseMaterials[i] != null)
            {
                landRuntimeMats[i] = new Material(landBaseMaterials[i]);
            }
        }
    }

    public void SetTransitionAtlas(OverworldTerrainTexturing.TileAtlasBuild build)
    {
        if (tileIdMap != null && tileIdMap != build.tileIdMap) { Object.Destroy(tileIdMap); }
        if (atlasTexture != null && atlasTexture != build.atlasTexture) { Object.Destroy(atlasTexture); }
        if (waterMask != null && waterMask != build.waterMask) { Object.Destroy(waterMask); }
        tileIdMap = build.tileIdMap;
        atlasTexture = build.atlasTexture;
        waterMask = build.waterMask;

        if (transitionShader == null) { transitionShader = Shader.Find("Custom/OverworldTransitionAtlas"); }
        if (transitionShader == null) { return; }

        if (landRuntimeMats == null) { return; }
        for (int i = 0; i < landRuntimeMats.Length; i++)
        {
            Material mat = landRuntimeMats[i];
            if (mat == null) { continue; }
            mat.shader = transitionShader;
            mat.SetTexture("_TileIdMap", tileIdMap);
            mat.SetTexture("_TileAtlas", atlasTexture);
            mat.SetVector("_AtlasGrid", new Vector4(build.atlasCols, build.atlasRows, 0f, 0f));
            mat.SetTexture("_WaterMask", waterMask);
        }
    }

    public void ReleaseAll()
    {
        if (tileIdMap != null) { Object.Destroy(tileIdMap); tileIdMap = null; }
        if (atlasTexture != null) { Object.Destroy(atlasTexture); atlasTexture = null; }
        if (waterMask != null) { Object.Destroy(waterMask); waterMask = null; }
        ReleaseRuntimeMaterials();
    }

    private void ReleaseRuntimeMaterials()
    {
        if (landRuntimeMats == null) { return; }
        for (int i = 0; i < landRuntimeMats.Length; i++)
        {
            if (landRuntimeMats[i] != null) { Object.Destroy(landRuntimeMats[i]); }
            landRuntimeMats[i] = null;
        }
    }
}
