using UnityEngine;

public class OverworldChunkRuntimeTextures : MonoBehaviour
{
    public Material grassRuntimeMat;
    public Material stoneRuntimeMat;
    public Texture2D chunkTexture;

    public void EnsureMaterials(Material grassBase, Material stoneBase)
    {
        if (grassRuntimeMat == null && grassBase != null) { grassRuntimeMat = new Material(grassBase); }
        if (stoneRuntimeMat == null && stoneBase != null) { stoneRuntimeMat = new Material(stoneBase); }
    }

    public void SetChunkTexture(Texture2D tex)
    {
        if (chunkTexture != null && chunkTexture != tex)
        {
            Object.Destroy(chunkTexture);
        }
        chunkTexture = tex;
        if (grassRuntimeMat != null) { grassRuntimeMat.mainTexture = chunkTexture; grassRuntimeMat.mainTextureScale = Vector2.one; }
        if (stoneRuntimeMat != null) { stoneRuntimeMat.mainTexture = chunkTexture; stoneRuntimeMat.mainTextureScale = Vector2.one; }
    }

    public void ReleaseAll()
    {
        if (chunkTexture != null) { Object.Destroy(chunkTexture); chunkTexture = null; }
        if (grassRuntimeMat != null) { Object.Destroy(grassRuntimeMat); grassRuntimeMat = null; }
        if (stoneRuntimeMat != null) { Object.Destroy(stoneRuntimeMat); stoneRuntimeMat = null; }
    }
}
