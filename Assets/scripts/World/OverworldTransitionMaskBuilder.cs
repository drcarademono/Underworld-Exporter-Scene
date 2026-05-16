using UnityEngine;

public static class OverworldTransitionMaskBuilder
{
    public static Texture2D BuildMaskTexture(int[] terrainClassFull, int width, int height, int targetClass)
    {
        if (terrainClassFull == null || width < 2 || height < 2) return null;
        int tw = width - 1;
        int th = height - 1;
        Texture2D tex = new Texture2D(tw, th, TextureFormat.R8, false, true);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color32[] px = new Color32[tw * th];
        for (int y = 0; y < th; y++)
        {
            for (int x = 0; x < tw; x++)
            {
                int c = terrainClassFull[(y * width) + x];
                byte v = (byte)((c == targetClass) ? 255 : 0);
                px[(y * tw) + x] = new Color32(v, v, v, 255);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        return tex;
    }
}
