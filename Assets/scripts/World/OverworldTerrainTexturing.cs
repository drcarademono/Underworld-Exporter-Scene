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
        public int firstTileWidth;
        public int firstTileHeight;
        public int minTileWidth;
        public int maxTileWidth;
        public int minTileHeight;
        public int maxTileHeight;
        public int canonicalTileSize;
        public int minTileId;
        public int maxTileId;
        public int waterCenterTiles;
        public int waterTargetTiles;
    }

    public struct TileAtlasBuild
    {
        public Texture2D tileIdMap;
        public Texture2D atlasTexture;
        public Texture2D waterMask;
        public bool[] clampMask;
        public byte[] centerClassMap;
        public byte[] targetClassMap;
        public byte[] maskMap;
        public int atlasCols;
        public int atlasRows;
    }

    private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
    public static TileAtlasBuild BuildChunkTransitionAtlas(int[] terrainClassFull, int width, int height, string assetsRelativeFolder, Texture2D waterBase, Texture2D grassBase, Texture2D stoneBase, Texture2D snowBase, Texture2D swampBase, Texture2D sandBase, out BuildStats stats, int cropTiles = 0)
    {
        stats = new BuildStats();
        TileAtlasBuild build = new TileAtlasBuild();
        if (terrainClassFull == null || width < 2 || height < 2) { return build; }

        int tileW = width - 1;
        int tileH = height - 1;
        int outTileW = tileW - (cropTiles * 2);
        int outTileH = tileH - (cropTiles * 2);
        if (outTileW <= 0 || outTileH <= 0) { return build; }
        int[] ids = new int[outTileW * outTileH];
        for (int i = 0; i < ids.Length; i++) { ids[i] = -1; }
        byte[] waterFlags = new byte[outTileW * outTileH];
        bool[] clampFlags = new bool[outTileW * outTileH];
        byte[] centerClassMap = new byte[outTileW * outTileH];
        byte[] targetClassMap = new byte[outTileW * outTileH];
        byte[] maskMap = new byte[outTileW * outTileH];
        Dictionary<string, int> atlasLookup = new Dictionary<string, int>();
        List<Texture2D> atlasTiles = new List<Texture2D>();

        for (int ty = cropTiles; ty < tileH - cropTiles; ty++)
        {
            for (int tx = cropTiles; tx < tileW - cropTiles; tx++)
            {
                stats.tileCount++;
                int outIdx = ((ty - cropTiles) * outTileW) + (tx - cropTiles);
                int center = GetTileClass(terrainClassFull, width, height, tx, ty);
                if (center == 0) { waterFlags[outIdx] = 255; stats.waterCenterTiles++; }
                int target = GetTransitionTarget(terrainClassFull, width, height, tx, ty, center);
                int mask = BuildMask(terrainClassFull, width, height, tx, ty, target);
                centerClassMap[outIdx] = (byte)Mathf.Clamp(center, 0, 255);
                targetClassMap[outIdx] = (byte)Mathf.Clamp(target, 0, 255);
                maskMap[outIdx] = (byte)Mathf.Clamp(mask, 0, 255);
                if (center == 0 || target == 0)
                {
                    clampFlags[outIdx] = true;
                    if (target == 0) { stats.waterTargetTiles++; }
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
                    tile = GetBaseTexture(center, waterBase, grassBase, stoneBase, snowBase, swampBase, sandBase);
                    stats.fallbackCenterTiles++;
                }

                if (tile == null) { continue; }
                if (!atlasLookup.TryGetValue(key, out int id))
                {
                    id = atlasTiles.Count;
                    atlasLookup[key] = id;
                    atlasTiles.Add(tile);
                }
                ids[outIdx] = id;
            }
        }

        stats.uniqueAtlasTiles = atlasTiles.Count;
        stats.minTileId = int.MaxValue;
        stats.maxTileId = int.MinValue;
        for (int i = 0; i < ids.Length; i++)
        {
            if (ids[i] < 0) { continue; }
            if (ids[i] < stats.minTileId) { stats.minTileId = ids[i]; }
            if (ids[i] > stats.maxTileId) { stats.maxTileId = ids[i]; }
        }
        if (stats.minTileId == int.MaxValue) { stats.minTileId = -1; stats.maxTileId = -1; }
        if (stats.uniqueAtlasTiles > 255)
        {
            UnityEngine.Debug.LogWarning($"OverworldTransitionTexture: chunk atlas has {stats.uniqueAtlasTiles} unique tiles; using 16-bit tile-id encoding.");
        }
        build.tileIdMap = new Texture2D(outTileW, outTileH, TextureFormat.RGBA32, false);
        build.tileIdMap.filterMode = FilterMode.Point;
        build.tileIdMap.wrapMode = TextureWrapMode.Clamp;
        Color32[] mapPixels = new Color32[ids.Length];
        for (int i = 0; i < ids.Length; i++) { int id = Mathf.Clamp(ids[i], 0, 65535); mapPixels[i] = new Color32((byte)(id & 255), (byte)((id >> 8) & 255), 0, 255); }
        build.tileIdMap.SetPixels32(mapPixels);
        build.tileIdMap.Apply(false, false);

        build.waterMask = new Texture2D(outTileW, outTileH, TextureFormat.Alpha8, false);
        build.waterMask.filterMode = FilterMode.Point;
        build.waterMask.wrapMode = TextureWrapMode.Clamp;
        Color32[] waterPixels = new Color32[waterFlags.Length];
        for (int i = 0; i < waterFlags.Length; i++) waterPixels[i] = new Color32(0, 0, 0, waterFlags[i]);
        build.waterMask.SetPixels32(waterPixels);
        build.waterMask.Apply(false, false);

        if (atlasTiles.Count > 0)
        {
            stats.firstTileWidth = atlasTiles[0].width;
            stats.firstTileHeight = atlasTiles[0].height;
            stats.minTileWidth = int.MaxValue; stats.maxTileWidth = 0;
            stats.minTileHeight = int.MaxValue; stats.maxTileHeight = 0;
            for (int i = 0; i < atlasTiles.Count; i++)
            {
                Texture2D t = atlasTiles[i];
                if (t == null) { continue; }
                if (t.width < stats.minTileWidth) stats.minTileWidth = t.width;
                if (t.width > stats.maxTileWidth) stats.maxTileWidth = t.width;
                if (t.height < stats.minTileHeight) stats.minTileHeight = t.height;
                if (t.height > stats.maxTileHeight) stats.maxTileHeight = t.height;
            }
            if (stats.minTileWidth == int.MaxValue) { stats.minTileWidth = 0; stats.minTileHeight = 0; }
        }

        int atlasCols = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, atlasTiles.Count))), 1, 16);
        int atlasRows = Mathf.CeilToInt(atlasTiles.Count / (float)atlasCols);
        int tileSize = 16;
        if (atlasTiles.Count > 0)
        {
            for (int i = 0; i < atlasTiles.Count; i++)
            {
                Texture2D t = atlasTiles[i];
                if (t == null) { continue; }
                tileSize = Mathf.Max(tileSize, t.width, t.height); // canonical size: max dimension
            }
        }
        stats.canonicalTileSize = tileSize;
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
                {
                    int sx = Mathf.Clamp(Mathf.FloorToInt((x / (float)tileSize) * tile.width), 0, tile.width - 1);
                    int sy = Mathf.Clamp(Mathf.FloorToInt((y / (float)tileSize) * tile.height), 0, tile.height - 1);
                    atlasPixels[(oy + y) * build.atlasTexture.width + (ox + x)] = src[sy * tile.width + sx];
                }
        }

        build.atlasTexture.SetPixels32(atlasPixels);
        build.atlasTexture.Apply(false, false);
        build.atlasCols = atlasCols;
        build.atlasRows = atlasRows;
        build.clampMask = clampFlags;
        build.centerClassMap = centerClassMap;
        build.targetClassMap = targetClassMap;
        build.maskMap = maskMap;
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

    private static int Priority(int c) { if (c == 0) return 6; if (c == 6) return 5; if (c == 5) return 4; if (c == 3) return 3; if (c == 2) return 2; return 1; }

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
    private static string ClassName(int c) { if (c == 0) return "water"; if (c == 2) return "stone"; if (c == 3) return "snow"; if (c == 5) return "swamp"; if (c == 6) return "sand"; return "grass"; }
    private static Texture2D GetBaseTexture(int c, Texture2D water, Texture2D grass, Texture2D stone, Texture2D snow, Texture2D swamp, Texture2D sand)
    {
        if (c == 0) return water; if (c == 2) return stone; if (c == 3) return snow; if (c == 5) return swamp; if (c == 6) return sand; return grass;
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
