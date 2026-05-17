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
    public static TileAtlasBuild BuildChunkTransitionAtlas(int[] terrainClassFull, int width, int height, string assetsRelativeFolder, Texture2D waterBase, Texture2D grassBase, Texture2D stoneBase, Texture2D snowBase, Texture2D sandBase, out BuildStats stats, int cropTiles = 0)
    {
        stats = new BuildStats();
        TileAtlasBuild build = new TileAtlasBuild();
        if (terrainClassFull == null || width < 2 || height < 2) { return build; }

        int tileW = width - 1;
        int tileH = height - 1;
        int outTileW = tileW - (cropTiles * 2);
        int outTileH = tileH - (cropTiles * 2);
        if (outTileW <= 0 || outTileH <= 0) { return build; }
        byte[] ids = new byte[outTileW * outTileH];
        byte[] waterFlags = new byte[outTileW * outTileH];
        bool[] clampFlags = new bool[outTileW * outTileH];
        Dictionary<string, byte> atlasLookup = new Dictionary<string, byte>();
        List<Texture2D> atlasTiles = new List<Texture2D>();

        for (int ty = cropTiles; ty < tileH - cropTiles; ty++)
        {
            for (int tx = cropTiles; tx < tileW - cropTiles; tx++)
            {
                stats.tileCount++;
                int center = GetTileClass(terrainClassFull, width, height, tx, ty);
                if (center == 0) { waterFlags[((ty - cropTiles) * outTileW) + (tx - cropTiles)] = 255; }
                int target = GetTransitionTarget(terrainClassFull, width, height, tx, ty, center);
                int mask = BuildMask(terrainClassFull, width, height, tx, ty, target);
                if (center == 0 || target == 0)
                {
                    clampFlags[((ty - cropTiles) * outTileW) + (tx - cropTiles)] = true;
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
                    key = $"base_{ClassName(center)}";
                }

                if (tile == null)
                {
                    tile = GetBaseTexture(center, waterBase, grassBase, stoneBase, snowBase, sandBase);
                    stats.fallbackCenterTiles++;
                }

                if (tile == null) { continue; }
                if (!atlasLookup.TryGetValue(key, out byte id))
                {
                    id = (byte)Mathf.Clamp(atlasTiles.Count, 0, 255);
                    atlasLookup[key] = id;
                    atlasTiles.Add(tile);
                }
                ids[((ty - cropTiles) * outTileW) + (tx - cropTiles)] = id;
            }
        }

        stats.uniqueAtlasTiles = atlasTiles.Count;
        build.tileIdMap = new Texture2D(outTileW, outTileH, TextureFormat.Alpha8, false);
        build.tileIdMap.filterMode = FilterMode.Point;
        build.tileIdMap.wrapMode = TextureWrapMode.Clamp;
        Color32[] mapPixels = new Color32[ids.Length];
        for (int i = 0; i < ids.Length; i++) mapPixels[i] = new Color32(0, 0, 0, ids[i]);
        build.tileIdMap.SetPixels32(mapPixels);
        build.tileIdMap.Apply(false, false);

        build.waterMask = new Texture2D(outTileW, outTileH, TextureFormat.Alpha8, false);
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

    private static int GetTransitionTarget(int[] data, int w, int h, int tx, int ty, int center)
    {
        int best = center;

        // Land tiles transition "up" to higher-priority neighbors (e.g. stone->water).
        if (center != 0)
        {
            TryPromoteTile(data, w, h, tx, ty + 1, ref best, center);
            TryPromoteTile(data, w, h, tx + 1, ty, ref best, center);
            TryPromoteTile(data, w, h, tx, ty - 1, ref best, center);
            TryPromoteTile(data, w, h, tx - 1, ty, ref best, center);
            return best;
        }

        // Water tiles need the inverse at chunk boundaries: transition to strongest non-water neighbor
        // so water-side transition tiles can be emitted when the opposite shore tile is in another chunk.
        int bestNonWater = -1;
        TryPromoteWaterSide(data, w, h, tx, ty + 1, ref bestNonWater);
        TryPromoteWaterSide(data, w, h, tx + 1, ty, ref bestNonWater);
        TryPromoteWaterSide(data, w, h, tx, ty - 1, ref bestNonWater);
        TryPromoteWaterSide(data, w, h, tx - 1, ty, ref bestNonWater);
        return (bestNonWater >= 0) ? bestNonWater : center;
    }

    private static void TryPromoteTile(int[] d, int w, int h, int tx, int ty, ref int best, int center)
    {
        int v = GetTileClass(d, w, h, tx, ty);
        if (v < 0) return;
        if (Priority(v) > Priority(best) && Priority(v) > Priority(center)) best = v;
    }

    private static void TryPromoteWaterSide(int[] d, int w, int h, int tx, int ty, ref int bestNonWater)
    {
        int v = GetTileClass(d, w, h, tx, ty);
        if (v <= 0) return;
        if (bestNonWater < 0 || Priority(v) > Priority(bestNonWater)) bestNonWater = v;
    }

    private static int Priority(int c) { if (c == 0) return 5; if (c == 3) return 4; if (c == 2) return 3; if (c == 6) return 2; return 1; }

    private static int BuildMask(int[] d, int w, int h, int tx, int ty, int target)
    {
        int m = 0;
        if (GetTileClass(d, w, h, tx, ty + 1) == target) m |= 1;
        if (GetTileClass(d, w, h, tx + 1, ty) == target) m |= 2;
        if (GetTileClass(d, w, h, tx, ty - 1) == target) m |= 4;
        if (GetTileClass(d, w, h, tx - 1, ty) == target) m |= 8;
        return m;
    }

    private static int GetTileClass(int[] d, int w, int h, int tx, int ty)
    {
        if (tx < 0 || ty < 0 || tx >= w - 1 || ty >= h - 1) return -1;
        int bl = d[ty * w + tx];
        int br = d[ty * w + (tx + 1)];
        int tl = d[(ty + 1) * w + tx];
        int tr = d[(ty + 1) * w + (tx + 1)];

        int[] counts = new int[7];
        CountClass(bl, counts); CountClass(br, counts); CountClass(tl, counts); CountClass(tr, counts);
        if (counts[0] >= 3) return 0;
        int bestClass = 1; int best = counts[1];
        for (int c = 2; c <= 6; c++)
            if (counts[c] > best || (counts[c] == best && Priority(c) > Priority(bestClass))) { best = counts[c]; bestClass = c; }
        return bestClass;
    }

    private static void CountClass(int c, int[] counts) { counts[Mathf.Clamp(c, 0, 6)]++; }
    private static string ClassName(int c) { if (c == 0) return "water"; if (c == 2) return "stone"; if (c == 3) return "snow"; if (c == 6) return "sand"; return "grass"; }
    private static Texture2D GetBaseTexture(int c, Texture2D water, Texture2D grass, Texture2D stone, Texture2D snow, Texture2D sand)
    {
        if (c == 0) return water; if (c == 2) return stone; if (c == 3) return snow; if (c == 6) return sand; return grass;
    }

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
