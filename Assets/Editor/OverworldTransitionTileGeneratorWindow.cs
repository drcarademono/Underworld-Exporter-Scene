using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class OverworldTransitionTileGeneratorWindow : EditorWindow
{
    private enum BlendMode { HardMask, OrderedDither, PerlinBorder }

    private OverworldTerrainController controller;
    private Texture2D grassTexture;
    private Texture2D stoneTexture;
    private Texture2D waterTexture;

    private BlendMode blendMode = BlendMode.OrderedDither;
    private int tileSize = 64;
    private int variantsPerMask = 1; // 1-3
    private int seed = 1337;
    private int ditherThresholdBias = 0;
    private float perlinScale = 10f;
    private float perlinStrength = 0.18f;
    private float borderWidth = 0.12f;
    private float borderStochasticity = 0.35f;
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
        if (GUILayout.Button("Generate 3-family first-pass set (grass/water/stone)", GUILayout.Height(36)))
        {
            GenerateAll();
        }
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

        // Attempt to load UW2 terrain indices via private GameWorldController loader.
        TryPullViaGameWorldController(controller.GrassTextureIndex, ref grassTexture);
        TryPullViaGameWorldController(controller.StoneTextureIndex, ref stoneTexture);
        TryPullViaGameWorldController(controller.WaterTextureIndex, ref waterTexture);

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
        if (grassTexture == null || stoneTexture == null || waterTexture == null)
        {
            EditorUtility.DisplayDialog("Missing Textures", "Assign grass, stone, and water textures first.", "OK");
            return;
        }

        EnsureFolder(outputFolder);

        GenerateFamily("grass", "water", grassTexture, waterTexture);
        GenerateFamily("grass", "stone", grassTexture, stoneTexture);
        GenerateFamily("stone", "water", stoneTexture, waterTexture);

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

        float edgeDistance = DistanceToMaskBoundary(mask, fx, fy);
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

    private static float DistanceToMaskBoundary(int mask, float fx, float fy)
    {
        bool n = (mask & 1) != 0;
        bool e = (mask & 2) != 0;
        bool s = (mask & 4) != 0;
        bool w = (mask & 8) != 0;

        float d = float.NegativeInfinity;
        if (n) { d = Mathf.Max(d, fy - 0.5f); }
        if (e) { d = Mathf.Max(d, fx - 0.5f); }
        if (s) { d = Mathf.Max(d, 0.5f - fy); }
        if (w) { d = Mathf.Max(d, 0.5f - fx); }
        return d;
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

    private static bool IsPixelInTarget(int mask, int x, int y, int size)
    {
        bool n = (mask & 1) != 0;
        bool e = (mask & 2) != 0;
        bool s = (mask & 4) != 0;
        bool w = (mask & 8) != 0;

        float fx = (x + 0.5f) / size;
        float fy = (y + 0.5f) / size;

        bool inN = fy > 0.5f;
        bool inE = fx > 0.5f;
        bool inS = fy < 0.5f;
        bool inW = fx < 0.5f;

        return (n && inN) || (e && inE) || (s && inS) || (w && inW);
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
}
