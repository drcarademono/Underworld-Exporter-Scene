using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class OverworldTerrainTexturing
{
    private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

    public static Texture2D BuildChunkTransitionTexture(int[] terrainClassFull, int width, int height, int pixelsPerTile, string assetsRelativeFolder)
    {
        if (terrainClassFull == null || width < 2 || height < 2) { return null; }
        int tileW = width - 1;
        int tileH = height - 1;
        int outW = tileW * pixelsPerTile;
        int outH = tileH * pixelsPerTile;
        Texture2D output = new Texture2D(outW, outH, TextureFormat.RGBA32, false);
        output.filterMode = FilterMode.Point;
        output.wrapMode = TextureWrapMode.Clamp;

        for (int ty = 0; ty < tileH; ty++)
        {
            for (int tx = 0; tx < tileW; tx++)
            {
                int center = terrainClassFull[(ty * width) + tx];
                int target = GetTransitionTarget(terrainClassFull, width, height, tx, ty, center);
                int mask = BuildMask(terrainClassFull, width, height, tx, ty, target);
                string from = ClassName(center);
                string to = ClassName(target);
                Texture2D tile = LoadTile(from, to, mask, assetsRelativeFolder);
                if (tile == null) { continue; }
                BlitNearest(tile, output, tx * pixelsPerTile, ty * pixelsPerTile, pixelsPerTile, pixelsPerTile);
            }
        }
        output.Apply(false, false);
        return output;
    }

    private static int GetTransitionTarget(int[] data, int w, int h, int x, int y, int c)
    {
        int best = c;
        TryPromote(data,w,h,x,y+1,ref best,c);
        TryPromote(data,w,h,x+1,y,ref best,c);
        TryPromote(data,w,h,x,y-1,ref best,c);
        TryPromote(data,w,h,x-1,y,ref best,c);
        return best;
    }

    private static void TryPromote(int[] d,int w,int h,int x,int y,ref int best,int c){if(x<0||y<0||x>=w||y>=h)return; int v=d[y*w+x]; if(Priority(v)>Priority(best)&&Priority(v)>Priority(c)) best=v;}
    private static int Priority(int c){ if(c==0)return 3; if(c==2)return 2; return 1; }

    private static int BuildMask(int[] d, int w, int h, int x, int y, int target)
    {
        int m = 0;
        if (Get(d,w,h,x,y+1)==target) m |= 1;
        if (Get(d,w,h,x+1,y)==target) m |= 2;
        if (Get(d,w,h,x,y-1)==target) m |= 4;
        if (Get(d,w,h,x-1,y)==target) m |= 8;
        return m;
    }
    private static int Get(int[] d,int w,int h,int x,int y){ if(x<0||y<0||x>=w||y>=h) return -1; return d[y*w+x]; }
    private static string ClassName(int c){ if(c==0) return "water"; if(c==2) return "stone"; return "grass"; }

    private static Texture2D LoadTile(string from, string to, int mask, string folder)
    {
        string file = $"tr_{from}_to_{to}_m{mask:D2}.png";
        string key = folder + "/" + file;
        if (cache.TryGetValue(key, out var t)) { return t; }
        string full = Path.Combine(Application.dataPath, folder.Replace("Assets/", ""), file);
        if (!File.Exists(full)) { cache[key] = null; return null; }
        byte[] bytes = File.ReadAllBytes(full);
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(bytes, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        cache[key] = tex;
        return tex;
    }

    private static void BlitNearest(Texture2D src, Texture2D dst, int dx, int dy, int w, int h)
    {
        for (int y = 0; y < h; y++)
        {
            int sy = Mathf.FloorToInt((y / (float)h) * src.height);
            for (int x = 0; x < w; x++)
            {
                int sx = Mathf.FloorToInt((x / (float)w) * src.width);
                dst.SetPixel(dx + x, dy + y, src.GetPixel(sx, sy));
            }
        }
    }
}
