using UnityEngine;

public class OverworldChunkRuntimeTextures : MonoBehaviour
{
    public Material grassRuntimeMat;
    public Material stoneRuntimeMat;
    public Texture2D tileIdMap;
    public Texture2D atlasTexture;
    private static Shader transitionShader;

    public void EnsureMaterials(Material grassBase, Material stoneBase)
    {
        if (grassRuntimeMat == null && grassBase != null) { grassRuntimeMat = new Material(grassBase); }
        if (stoneRuntimeMat == null && stoneBase != null) { stoneRuntimeMat = new Material(stoneBase); }
    }

    public void SetTransitionAtlas(OverworldTerrainTexturing.TileAtlasBuild build)
    {
        if (tileIdMap != null && tileIdMap != build.tileIdMap) { Object.Destroy(tileIdMap); }
        if (atlasTexture != null && atlasTexture != build.atlasTexture) { Object.Destroy(atlasTexture); }
        tileIdMap = build.tileIdMap;
        atlasTexture = build.atlasTexture;

        if (transitionShader == null) { transitionShader = Shader.Find("Custom/OverworldTransitionAtlas"); }
        if (transitionShader == null) { return; }

        if (grassRuntimeMat != null)
        {
            grassRuntimeMat.shader = transitionShader;
            grassRuntimeMat.SetTexture("_TileIdMap", tileIdMap);
            grassRuntimeMat.SetTexture("_TileAtlas", atlasTexture);
            grassRuntimeMat.SetVector("_AtlasGrid", new Vector4(build.atlasCols, build.atlasRows, 0f, 0f));
        }
        if (stoneRuntimeMat != null)
        {
            stoneRuntimeMat.shader = transitionShader;
            stoneRuntimeMat.SetTexture("_TileIdMap", tileIdMap);
            stoneRuntimeMat.SetTexture("_TileAtlas", atlasTexture);
            stoneRuntimeMat.SetVector("_AtlasGrid", new Vector4(build.atlasCols, build.atlasRows, 0f, 0f));
        }
    }

    public void ReleaseAll()
    {
        if (tileIdMap != null) { Object.Destroy(tileIdMap); tileIdMap = null; }
        if (atlasTexture != null) { Object.Destroy(atlasTexture); atlasTexture = null; }
        if (grassRuntimeMat != null) { Object.Destroy(grassRuntimeMat); grassRuntimeMat = null; }
        if (stoneRuntimeMat != null) { Object.Destroy(stoneRuntimeMat); stoneRuntimeMat = null; }
    }
}
