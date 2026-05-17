using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class OverworldTerrainTexturing
{
    public struct BuildStats
    {
        public int tileCount;
        public int transitionTiles;
        public int fallbackCenterTiles;
        public int missingTransitionFiles;
        public int uniqueAtlasTiles;
    }

    public struct TileAtlasBuild
    {
        public Texture2D tileIdMap;
        public Texture2D atlasTexture;
        public Texture2D waterMask;
        public bool[] clampMask;
        public int atlasCols;
        public int atlasRows;
    }

    private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
    public static TileAtlasBuild BuildChunkTransitionAtlas(int[] terrainClassFull, int width, int height, string assetsRelativeFolder, Texture2D waterBase, Texture2D grassBase, Texture2D stoneBase, out BuildStats stats)
    {
        stats = new BuildStats();
        TileAtlasBuild build = new TileAtlasBuild();
        if (terrainClassFull == null || width < 2 || height < 2) { return build; }

        int tileW = width - 1;
        int tileH = height - 1;
        byte[] ids = new byte[tileW * tileH];
        byte[] waterFlags = new byte[tileW * tileH];
        bool[] clampFlags = new bool[tileW * tileH];
        Dictionary<string, byte> atlasLookup = new Dictionary<string, byte>();
        List<Texture2D> atlasTiles = new List<Texture2D>();

        for (int ty = 0; ty < tileH; ty++)
        {
            for (int tx = 0; tx < tileW; tx++)
            {
                stats.tileCount++;
                int center = terrainClassFull[(ty * width) + tx];
                if (center == 0) { waterFlags[(ty * tileW) + tx] = 255; }
                int target = GetTransitionTarget(terrainClassFull, width, height, tx, ty, center);
                int mask = BuildMask(terrainClassFull, width, height, tx, ty, target);
                if (center == 0 || target == 0)
                {
                    clampFlags[(ty * tileW) + tx] = true;
                }

                Texture2D tile = null;
                string key;
                if (target != center)
                {
                    key = $"tr_{ClassName(center)}_to_{ClassName(target)}_m{mask:D2}.png";
                    tile = LoadTransitionTile(ClassName(center), ClassName(target), mask, assetsRelativeFolder);
                    if (tile != null) { stats.transitionTiles++; }
                    else { stats.missingTransitionFiles++; }
                }
                else
                {
                    key = (center == 0) ? "base_water" : (center == 2 ? "base_stone" : "base_grass");
                }

                if (tile == null)
                {
                    tile = (center == 0) ? waterBase : ((center == 2) ? stoneBase : grassBase);
                    stats.fallbackCenterTiles++;
                }

                if (tile == null) { continue; }
                if (!atlasLookup.TryGetValue(key, out byte id))
                {
                    id = (byte)Mathf.Clamp(atlasTiles.Count, 0, 255);
                    atlasLookup[key] = id;
                    atlasTiles.Add(tile);
                }
                ids[(ty * tileW) + tx] = id;
            }
        }

        stats.uniqueAtlasTiles = atlasTiles.Count;
        build.tileIdMap = new Texture2D(tileW, tileH, TextureFormat.Alpha8, false);
        build.tileIdMap.filterMode = FilterMode.Point;
        build.tileIdMap.wrapMode = TextureWrapMode.Clamp;
        Color32[] mapPixels = new Color32[ids.Length];
        for (int i = 0; i < ids.Length; i++) mapPixels[i] = new Color32(0, 0, 0, ids[i]);
        build.tileIdMap.SetPixels32(mapPixels);
        build.tileIdMap.Apply(false, false);

        build.waterMask = new Texture2D(tileW, tileH, TextureFormat.Alpha8, false);
        build.waterMask.filterMode = FilterMode.Point;
        build.waterMask.wrapMode = TextureWrapMode.Clamp;
        Color32[] waterPixels = new Color32[waterFlags.Length];
        for (int i = 0; i < waterFlags.Length; i++) waterPixels[i] = new Color32(0, 0, 0, waterFlags[i]);
        build.waterMask.SetPixels32(waterPixels);
        build.waterMask.Apply(false, false);

        int atlasCols = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, atlasTiles.Count))), 1, 16);
        int atlasRows = Mathf.CeilToInt(atlasTiles.Count / (float)atlasCols);
        int tileSize = (atlasTiles.Count > 0) ? atlasTiles[0].width : 16;
        build.atlasTexture = new Texture2D(atlasCols * tileSize, atlasRows * tileSize, TextureFormat.RGBA32, false);
        build.atlasTexture.filterMode = FilterMode.Point;
        build.atlasTexture.wrapMode = TextureWrapMode.Clamp;
        Color32[] atlasPixels = new Color32[build.atlasTexture.width * build.atlasTexture.height];

        for (int i = 0; i < atlasTiles.Count; i++)
        {
            Texture2D tile = atlasTiles[i];
            Color32[] src = tile.GetPixels32();
            int ox = (i % atlasCols) * tileSize;
            int oy = (i / atlasCols) * tileSize;
            for (int y = 0; y < tileSize; y++)
                for (int x = 0; x < tileSize; x++)
                    atlasPixels[(oy + y) * build.atlasTexture.width + (ox + x)] = src[y * tile.width + x];
        }

        build.atlasTexture.SetPixels32(atlasPixels);
        build.atlasTexture.Apply(false, false);
        build.atlasCols = atlasCols;
        build.atlasRows = atlasRows;
        build.clampMask = clampFlags;
        return build;
    }

    private static int GetTransitionTarget(int[] data, int w, int h, int x, int y, int c)
    {
        int best = c;
        TryPromote(data, w, h, x, y + 1, ref best, c);
        TryPromote(data, w, h, x + 1, y, ref best, c);
        TryPromote(data, w, h, x, y - 1, ref best, c);
        TryPromote(data, w, h, x - 1, y, ref best, c);
        return best;
    }

    private static void TryPromote(int[] d, int w, int h, int x, int y, ref int best, int c)
    {
        if (x < 0 || y < 0 || x >= w || y >= h) return;
        int v = d[y * w + x];
        if (Priority(v) > Priority(best) && Priority(v) > Priority(c)) best = v;
    }

    private static int Priority(int c) { if (c == 0) return 3; if (c == 2) return 2; return 1; }

    private static int BuildMask(int[] d, int w, int h, int x, int y, int target)
    {
        int m = 0;
        if (Get(d, w, h, x, y + 1) == target) m |= 1;
        if (Get(d, w, h, x + 1, y) == target) m |= 2;
        if (Get(d, w, h, x, y - 1) == target) m |= 4;
        if (Get(d, w, h, x - 1, y) == target) m |= 8;
        return m;
    }

    private static int Get(int[] d, int w, int h, int x, int y) { if (x < 0 || y < 0 || x >= w || y >= h) return -1; return d[y * w + x]; }
    private static string ClassName(int c) { if (c == 0) return "water"; if (c == 2) return "stone"; return "grass"; }

    private static Texture2D LoadTransitionTile(string from, string to, int mask, string folder)
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

}
