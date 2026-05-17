using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class OverworldTransitionTileGeneratorWindow : EditorWindow
{
    private const string PrefPrefix = "UW.OverworldTransitionTileGen.";
    [Serializable]
    private class TerrainTextureEntry
    {
        public string name;
        public Texture2D texture;
    }

    private enum BlendMode { HardMask, OrderedDither, PerlinBorder }

    private OverworldTerrainController controller;
    private Texture2D grassTexture;
    private Texture2D stoneTexture;
    private Texture2D waterTexture;
    private Texture2D dirtTexture;
    private Texture2D sandTexture;
    private Texture2D swampTexture;
    private Texture2D snowTexture;
    private Texture2D lavaTexture;

    private BlendMode blendMode = BlendMode.OrderedDither;
    private int tileSize = 64;
    private int variantsPerMask = 1; // 1-3
    private int seed = 1337;
    private int ditherThresholdBias = 0;
    private float perlinScale = 10f;
    private float perlinStrength = 0.18f;
    private float borderWidth = 0.12f;
    private float borderStochasticity = 0.35f;
    private float centerFillBoost = 0.08f;
    private float m15CenterFillBoost = 0.08f;
    private float m15Roughness = 0.08f;
    private float elbowRoundness = 0.16f;
    private string outputFolder = "Assets/Generated/OverworldTransitions";

    private static readonly int[,] Bayer4x4 = new int[4, 4]
    {
        { 0,  8,  2, 10 },
        {12,  4, 14,  6 },
        { 3, 11,  1,  9 },
        {15,  7, 13,  5 }
    };

    [MenuItem("Tools/UW/Overworld Transition Tile Generator")]
    public static void ShowWindow()
    {
        var window = GetWindow<OverworldTransitionTileGeneratorWindow>("Transition Tile Gen");
        window.minSize = new Vector2(520, 520);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Overworld 16-Mask Transition Tile Generator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Generates m00..m15 transition tiles with naming convention tr_<from>_to_<to>_mXX[_vN].", MessageType.Info);

        controller = (OverworldTerrainController)EditorGUILayout.ObjectField("Controller", controller, typeof(OverworldTerrainController), true);

        if (GUILayout.Button("Pull textures from controller/scene"))
        {
            PullFromController();
        }

        grassTexture = (Texture2D)EditorGUILayout.ObjectField("Grass Texture", grassTexture, typeof(Texture2D), false);
        stoneTexture = (Texture2D)EditorGUILayout.ObjectField("Stone Texture", stoneTexture, typeof(Texture2D), false);
        waterTexture = (Texture2D)EditorGUILayout.ObjectField("Water Texture", waterTexture, typeof(Texture2D), false);
        dirtTexture = (Texture2D)EditorGUILayout.ObjectField("Dirt Texture", dirtTexture, typeof(Texture2D), false);
        sandTexture = (Texture2D)EditorGUILayout.ObjectField("Sand Texture", sandTexture, typeof(Texture2D), false);
        swampTexture = (Texture2D)EditorGUILayout.ObjectField("Swamp Texture", swampTexture, typeof(Texture2D), false);
        snowTexture = (Texture2D)EditorGUILayout.ObjectField("Snow Texture", snowTexture, typeof(Texture2D), false);
        lavaTexture = (Texture2D)EditorGUILayout.ObjectField("Lava Texture", lavaTexture, typeof(Texture2D), false);

        blendMode = (BlendMode)EditorGUILayout.EnumPopup("Blend Mode", blendMode);
        tileSize = EditorGUILayout.IntSlider("Output Tile Size", tileSize, 16, 256);
        variantsPerMask = EditorGUILayout.IntSlider("Variants Per Mask", variantsPerMask, 1, 3);
        seed = EditorGUILayout.IntField("Seed", seed);

        using (new EditorGUI.DisabledScope(blendMode != BlendMode.OrderedDither))
        {
            ditherThresholdBias = EditorGUILayout.IntSlider("Dither Bias", ditherThresholdBias, -4, 4);
        }

        using (new EditorGUI.DisabledScope(blendMode != BlendMode.PerlinBorder))
        {
            perlinScale = EditorGUILayout.Slider("Perlin Scale", perlinScale, 1f, 64f);
            perlinStrength = EditorGUILayout.Slider("Perlin Strength", perlinStrength, 0f, 0.5f);
            borderWidth = EditorGUILayout.Slider("Border Width", borderWidth, 0.02f, 0.45f);
            borderStochasticity = EditorGUILayout.Slider("Border Stochasticity", borderStochasticity, 0f, 1f);
            centerFillBoost = EditorGUILayout.Slider("Center Fill Boost", centerFillBoost, 0f, 0.35f);
            m15CenterFillBoost = EditorGUILayout.Slider("M15 Center Fill Boost", m15CenterFillBoost, 0f, 0.35f);
            m15Roughness = EditorGUILayout.Slider("M15 Roughness", m15Roughness, 0f, 0.35f);
            elbowRoundness = EditorGUILayout.Slider("Elbow Roundness", elbowRoundness, 0f, 0.35f);
        }

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        if (GUILayout.Button("...", GUILayout.Width(40)))
        {
            string selected = EditorUtility.OpenFolderPanel("Select output folder", Application.dataPath, "");
            if (!string.IsNullOrEmpty(selected))
            {
                if (selected.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase))
                {
                    outputFolder = "Assets" + selected.Substring(Application.dataPath.Length);
                }
                else
                {
                    EditorUtility.DisplayDialog("Invalid Folder", "Please choose a folder under Assets/", "OK");
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        if (GUILayout.Button("Generate transition set for all assigned terrain textures", GUILayout.Height(36)))
        {
            GenerateAll();
        }
    }

    private void OnEnable()
    {
        LoadPrefs();
    }

    private void OnDisable()
    {
        SavePrefs();
    }

    private void PullFromController()
    {
        if (controller == null)
        {
            Debug.LogWarning("TransitionTileGen: No OverworldTerrainController assigned.");
            return;
        }

        // Grass/stone from overrides if provided.
        if (controller.GrassMaterialOverride != null)
        {
            grassTexture = controller.GrassMaterialOverride.mainTexture as Texture2D;
        }
        if (controller.StoneMaterialOverride != null)
        {
            stoneTexture = controller.StoneMaterialOverride.mainTexture as Texture2D;
        }
        if (controller.DirtMaterialOverride != null)
        {
            dirtTexture = controller.DirtMaterialOverride.mainTexture as Texture2D;
        }
        if (controller.SandMaterialOverride != null)
        {
            sandTexture = controller.SandMaterialOverride.mainTexture as Texture2D;
        }
        if (controller.SwampMaterialOverride != null)
        {
            swampTexture = controller.SwampMaterialOverride.mainTexture as Texture2D;
        }
        if (controller.SnowMaterialOverride != null)
        {
            snowTexture = controller.SnowMaterialOverride.mainTexture as Texture2D;
        }
        if (controller.LavaMaterialOverride != null)
        {
            lavaTexture = controller.LavaMaterialOverride.mainTexture as Texture2D;
        }

        // Attempt to load UW2 terrain indices via private GameWorldController loader.
        TryPullViaGameWorldController(controller.GrassTextureIndex, ref grassTexture);
        TryPullViaGameWorldController(controller.StoneTextureIndex, ref stoneTexture);
        TryPullViaGameWorldController(controller.WaterTextureIndex, ref waterTexture);
        TryPullViaGameWorldController(controller.DirtTextureIndex, ref dirtTexture);
        TryPullViaGameWorldController(controller.SandTextureIndex, ref sandTexture);
        TryPullViaGameWorldController(controller.SwampTextureIndex, ref swampTexture);
        TryPullViaGameWorldController(controller.SnowTextureIndex, ref snowTexture);
        TryPullViaGameWorldController(controller.LavaTextureIndex, ref lavaTexture);

        Debug.Log("TransitionTileGen: Pull complete. Verify texture slots before generating.");
    }

    private void TryPullViaGameWorldController(int textureIndex, ref Texture2D destination)
    {
        if (destination != null) { return; }
        var gwc = FindObjectOfType<GameWorldController>();
        if (gwc == null) { return; }

        var method = typeof(GameWorldController).GetMethod("LoadUW2TerrainTexture", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (method == null) { return; }

        try
        {
            var tex = method.Invoke(gwc, new object[] { textureIndex }) as Texture2D;
            if (tex != null)
            {
                destination = tex;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("TransitionTileGen: Could not pull texture index " + textureIndex + ": " + ex.Message);
        }
    }

    private void GenerateAll()
    {
        var terrains = BuildAssignedTerrainList();
        if (terrains.Count < 2)
        {
            EditorUtility.DisplayDialog("Missing Textures", "Assign at least two terrain textures before generating.", "OK");
            return;
        }

        EnsureFolder(outputFolder);

        for (int i = 0; i < terrains.Count - 1; i++)
        {
            for (int j = i + 1; j < terrains.Count; j++)
            {
                GenerateFamily(terrains[i].name, terrains[j].name, terrains[i].texture, terrains[j].texture);
                GenerateFamily(terrains[j].name, terrains[i].name, terrains[j].texture, terrains[i].texture);
            }
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Done", "Generated transition tiles in " + outputFolder, "OK");
    }

    private void GenerateFamily(string fromName, string toName, Texture2D fromTex, Texture2D toTex)
    {
        Texture2D srcA = ResampleNearest(fromTex, tileSize, tileSize);
        Texture2D srcB = ResampleNearest(toTex, tileSize, tileSize);

        for (int mask = 0; mask < 16; mask++)
        {
            for (int v = 0; v < variantsPerMask; v++)
            {
                Texture2D outTex = new Texture2D(tileSize, tileSize, TextureFormat.RGBA32, false);
                outTex.filterMode = FilterMode.Point;
                outTex.wrapMode = TextureWrapMode.Clamp;

                int vSeed = seed + (mask * 7919) + (v * 104729);
                for (int y = 0; y < tileSize; y++)
                {
                    for (int x = 0; x < tileSize; x++)
                    {
                        bool toSide = IsPixelInTarget(mask, x, y, tileSize);
                        if (!toSide)
                        {
                            outTex.SetPixel(x, y, srcA.GetPixel(x, y));
                            continue;
                        }

                        bool chooseB = ChooseTargetPixel(mask, x, y, tileSize, vSeed);
                        outTex.SetPixel(x, y, chooseB ? srcB.GetPixel(x, y) : srcA.GetPixel(x, y));
                    }
                }

                outTex.Apply(false, false);

                string maskName = mask.ToString("D2");
                string variantSuffix = variantsPerMask > 1 ? "_v" + (v + 1) : string.Empty;
                string fileName = $"tr_{fromName}_to_{toName}_m{maskName}{variantSuffix}.png";
                string path = Path.Combine(outputFolder, fileName).Replace("\\", "/");
                File.WriteAllBytes(path, outTex.EncodeToPNG());
            }
        }
    }

    private List<TerrainTextureEntry> BuildAssignedTerrainList()
    {
        var terrains = new List<TerrainTextureEntry>();
        AddTerrainIfAssigned(terrains, "water", waterTexture);
        AddTerrainIfAssigned(terrains, "grass", grassTexture);
        AddTerrainIfAssigned(terrains, "stone", stoneTexture);
        AddTerrainIfAssigned(terrains, "dirt", dirtTexture);
        AddTerrainIfAssigned(terrains, "sand", sandTexture);
        AddTerrainIfAssigned(terrains, "swamp", swampTexture);
        AddTerrainIfAssigned(terrains, "snow", snowTexture);
        AddTerrainIfAssigned(terrains, "lava", lavaTexture);
        return terrains;
    }

    private static void AddTerrainIfAssigned(List<TerrainTextureEntry> terrains, string terrainName, Texture2D texture)
    {
        if (texture == null) { return; }
        terrains.Add(new TerrainTextureEntry { name = terrainName, texture = texture });
    }


    private bool ChooseTargetPixel(int mask, int x, int y, int size, int vSeed)
    {
        switch (blendMode)
        {
            case BlendMode.HardMask:
                return true;
            case BlendMode.OrderedDither:
                return OrderedDitherPass(x, y, vSeed);
            case BlendMode.PerlinBorder:
                return PerlinBorderPass(mask, x, y, size, vSeed);
            default:
                return true;
        }
    }



    private bool PerlinBorderPass(int mask, int x, int y, int size, int vSeed)
    {
        float fx = (x + 0.5f) / size;
        float fy = (y + 0.5f) / size;

        float edgeDistance = TargetShapeDistance(mask, fx, fy);
        if (edgeDistance > borderWidth)
        {
            return true; // Deep interior remains target texture.
        }

        float n = Mathf.PerlinNoise((x + (vSeed * 0.001f)) / perlinScale, (y + (vSeed * 0.002f)) / perlinScale);
        float signedNoise = (n * 2f) - 1f;

        // Organic boundary shift from Perlin field.
        float shiftedDistance = edgeDistance + (signedNoise * perlinStrength);

        // Stochastic breakup near the boundary: stronger randomness close to edge, fades to interior.
        float edgeBandT = Mathf.Clamp01((borderWidth - Mathf.Abs(edgeDistance)) / Mathf.Max(0.0001f, borderWidth));
        float localRandom = Hash2DTo4Bit(x, y, vSeed) / 15f;
        float jitter = (localRandom - 0.5f) * 2f * borderStochasticity * edgeBandT;

        return (shiftedDistance + jitter) >= 0f;
    }

    private bool OrderedDitherPass(int x, int y, int vSeed)
    {
        int threshold = Bayer4x4[x & 3, y & 3] + ditherThresholdBias;
        threshold = Mathf.Clamp(threshold, 0, 15);
        int noise = Hash2DTo4Bit(x, y, vSeed); // 0..15
        return noise >= threshold;
    }

    private static int Hash2DTo4Bit(int x, int y, int s)
    {
        unchecked
        {
            int h = s;
            h ^= x * 374761393;
            h ^= y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= (h >> 16);
            return h & 15;
        }
    }

    private bool IsPixelInTarget(int mask, int x, int y, int size)
    {
        float fx = (x + 0.5f) / size;
        float fy = (y + 0.5f) / size;
        return TargetShapeDistance(mask, fx, fy) >= 0f;
    }

    private float TargetShapeDistance(int mask, float fx, float fy)
    {
        bool n = (mask & 1) != 0;
        bool e = (mask & 2) != 0;
        bool s = (mask & 4) != 0;
        bool w = (mask & 8) != 0;

        // For m15 ("island" shape), use dedicated boost so island size is controllable independently.
        float effectiveCenterFillBoost = (mask == 15) ? m15CenterFillBoost : centerFillBoost;

        // Positive boost should enlarge the center patch/stripe of the non-target texture.
        // So we shrink cardinal target bands toward edges as boost increases.
        float baseHalf = Mathf.Clamp(0.5f - effectiveCenterFillBoost, 0.05f, 0.5f);

        // For m15 (all sides active), use a circular center island mask to avoid lumpy square-ish shapes.
        if (mask == 15)
        {
            float islandRadius = Mathf.Clamp(effectiveCenterFillBoost * 1.15f, 0.04f, 0.45f);
            float roughNoise = Mathf.PerlinNoise((fx * tileSize / Mathf.Max(0.001f, perlinScale)) + (seed * 0.007f), (fy * tileSize / Mathf.Max(0.001f, perlinScale)) + (seed * 0.011f));
            float roughSigned = (roughNoise * 2f) - 1f;
            islandRadius += roughSigned * m15Roughness * 0.15f;
            islandRadius = Mathf.Clamp(islandRadius, 0.02f, 0.48f);
            float distToCenter = Vector2.Distance(new Vector2(fx, fy), new Vector2(0.5f, 0.5f));
            return distToCenter - islandRadius; // target outside the island circle
        }

        float dN = n ? (fy - (1f - baseHalf)) : float.NegativeInfinity;
        float dE = e ? (fx - (1f - baseHalf)) : float.NegativeInfinity;
        float dS = s ? (baseHalf - fy) : float.NegativeInfinity;
        float dW = w ? (baseHalf - fx) : float.NegativeInfinity;
        float d = Mathf.Max(Mathf.Max(dN, dE), Mathf.Max(dS, dW));

        if ((n && e) || (e && s) || (s && w) || (w && n))
        {
            float cx = e ? (1f - baseHalf) : baseHalf;
            float cy = n ? (1f - baseHalf) : baseHalf;
            if (e && s) { cy = baseHalf; }
            if (w && s) { cx = baseHalf; cy = baseHalf; }
            if (w && n) { cx = baseHalf; }

            float radial = elbowRoundness - Vector2.Distance(new Vector2(fx, fy), new Vector2(cx, cy));
            d = Mathf.Max(d, radial);
        }

        return d;
    }

    private static Texture2D ResampleNearest(Texture2D src, int width, int height)
    {
        Texture2D dst = new Texture2D(width, height, TextureFormat.RGBA32, false);
        dst.filterMode = FilterMode.Point;
        dst.wrapMode = TextureWrapMode.Repeat;

        for (int y = 0; y < height; y++)
        {
            int sy = Mathf.FloorToInt((y / (float)height) * src.height) % src.height;
            for (int x = 0; x < width; x++)
            {
                int sx = Mathf.FloorToInt((x / (float)width) * src.width) % src.width;
                dst.SetPixel(x, y, src.GetPixel(sx, sy));
            }
        }

        dst.Apply(false, false);
        return dst;
    }

    private static void EnsureFolder(string folder)
    {
        string normalized = folder.Replace("\\", "/");
        if (!normalized.StartsWith("Assets"))
        {
            throw new Exception("Output folder must be under Assets/");
        }

        string[] parts = normalized.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    private void LoadPrefs()
    {
        tileSize = EditorPrefs.GetInt(PrefPrefix + nameof(tileSize), tileSize);
        variantsPerMask = EditorPrefs.GetInt(PrefPrefix + nameof(variantsPerMask), variantsPerMask);
        seed = EditorPrefs.GetInt(PrefPrefix + nameof(seed), seed);
        blendMode = (BlendMode)EditorPrefs.GetInt(PrefPrefix + nameof(blendMode), (int)blendMode);
        ditherThresholdBias = EditorPrefs.GetInt(PrefPrefix + nameof(ditherThresholdBias), ditherThresholdBias);
        perlinScale = EditorPrefs.GetFloat(PrefPrefix + nameof(perlinScale), perlinScale);
        perlinStrength = EditorPrefs.GetFloat(PrefPrefix + nameof(perlinStrength), perlinStrength);
        borderWidth = EditorPrefs.GetFloat(PrefPrefix + nameof(borderWidth), borderWidth);
        borderStochasticity = EditorPrefs.GetFloat(PrefPrefix + nameof(borderStochasticity), borderStochasticity);
        centerFillBoost = EditorPrefs.GetFloat(PrefPrefix + nameof(centerFillBoost), centerFillBoost);
        m15CenterFillBoost = EditorPrefs.GetFloat(PrefPrefix + nameof(m15CenterFillBoost), m15CenterFillBoost);
        m15Roughness = EditorPrefs.GetFloat(PrefPrefix + nameof(m15Roughness), m15Roughness);
        elbowRoundness = EditorPrefs.GetFloat(PrefPrefix + nameof(elbowRoundness), elbowRoundness);
        outputFolder = EditorPrefs.GetString(PrefPrefix + nameof(outputFolder), outputFolder);
    }

    private void SavePrefs()
    {
        EditorPrefs.SetInt(PrefPrefix + nameof(tileSize), tileSize);
        EditorPrefs.SetInt(PrefPrefix + nameof(variantsPerMask), variantsPerMask);
        EditorPrefs.SetInt(PrefPrefix + nameof(seed), seed);
        EditorPrefs.SetInt(PrefPrefix + nameof(blendMode), (int)blendMode);
        EditorPrefs.SetInt(PrefPrefix + nameof(ditherThresholdBias), ditherThresholdBias);
        EditorPrefs.SetFloat(PrefPrefix + nameof(perlinScale), perlinScale);
        EditorPrefs.SetFloat(PrefPrefix + nameof(perlinStrength), perlinStrength);
        EditorPrefs.SetFloat(PrefPrefix + nameof(borderWidth), borderWidth);
        EditorPrefs.SetFloat(PrefPrefix + nameof(borderStochasticity), borderStochasticity);
        EditorPrefs.SetFloat(PrefPrefix + nameof(centerFillBoost), centerFillBoost);
        EditorPrefs.SetFloat(PrefPrefix + nameof(m15CenterFillBoost), m15CenterFillBoost);
        EditorPrefs.SetFloat(PrefPrefix + nameof(m15Roughness), m15Roughness);
        EditorPrefs.SetFloat(PrefPrefix + nameof(elbowRoundness), elbowRoundness);
        EditorPrefs.SetString(PrefPrefix + nameof(outputFolder), outputFolder ?? "Assets/Generated/OverworldTransitions");
    }
}
