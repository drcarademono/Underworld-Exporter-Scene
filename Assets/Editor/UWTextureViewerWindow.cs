using System.Collections.Generic;
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public class UWTextureViewerWindow : EditorWindow
{
    private string uw1Path = string.Empty;
    private string uw2Path = string.Empty;
    private Vector2 scroll;
    private float thumbSize = 96f;
    private int columns = 8;
    private bool showOnlyLikelyWater = false;

    private readonly List<TextureEntry> uw1Entries = new List<TextureEntry>();
    private readonly List<TextureEntry> uw2Entries = new List<TextureEntry>();

    [MenuItem("Tools/UW/Texture Viewer (Ground-Floor-Water)")]
    private static void OpenWindow()
    {
        var window = GetWindow<UWTextureViewerWindow>("UW Textures");
        window.minSize = new Vector2(760f, 420f);
    }


    private void OnEnable()
    {
        TryLoadPathsFromConfig();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Underworld Ground/Floor/Water Texture Viewer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Loads UW1 and UW2 textures directly from DATA files and auto-fills paths from config.json when available. UW1 uses detected floor range from F32.TR. UW2 uses detected range from T64.TR.", MessageType.Info);

        uw1Path = EditorGUILayout.TextField("UW1 Base Path", uw1Path);
        uw2Path = EditorGUILayout.TextField("UW2 Base Path", uw2Path);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Load UW1")) { LoadUW1(); }
            if (GUILayout.Button("Load UW2")) { LoadUW2(); }
            if (GUILayout.Button("Load Both")) { LoadUW1(); LoadUW2(); }
            if (GUILayout.Button("Export UW1 PNGs")) { ExportTextures(uw1Entries, "UW1"); }
            if (GUILayout.Button("Export UW2 PNGs")) { ExportTextures(uw2Entries, "UW2"); }
            if (GUILayout.Button("Clear")) { uw1Entries.Clear(); uw2Entries.Clear(); }
        }

        showOnlyLikelyWater = EditorGUILayout.ToggleLeft("Only show likely water textures", showOnlyLikelyWater);
        thumbSize = EditorGUILayout.Slider("Thumbnail Size", thumbSize, 48f, 160f);
        columns = EditorGUILayout.IntSlider("Columns", columns, 4, 12);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawSection("UW1 Floor Textures", uw1Entries);
        EditorGUILayout.Space(12);
        DrawSection("UW2 Textures", uw2Entries);
        EditorGUILayout.EndScrollView();
    }

    private void DrawSection(string title, List<TextureEntry> entries)
    {
        EditorGUILayout.LabelField($"{title} ({entries.Count})", EditorStyles.boldLabel);
        if (entries.Count == 0)
        {
            EditorGUILayout.LabelField("No textures loaded.");
            return;
        }

        int col = 0;
        EditorGUILayout.BeginHorizontal();
        foreach (var entry in entries)
        {
            if (showOnlyLikelyWater && !entry.likelyWater) { continue; }

            DrawTextureCard(entry);
            col++;
            if (col >= columns)
            {
                col = 0;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawTextureCard(TextureEntry entry)
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(thumbSize + 8)))
        {
            Rect previewRect = GUILayoutUtility.GetRect(thumbSize, thumbSize, GUILayout.Width(thumbSize), GUILayout.Height(thumbSize));
            if (entry.texture != null)
            {
                EditorGUI.DrawPreviewTexture(previewRect, entry.texture, null, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUI.HelpBox(previewRect, "Missing", MessageType.None);
            }
            EditorGUILayout.LabelField(entry.label, EditorStyles.miniLabel, GUILayout.Width(thumbSize + 8));
        }
    }

    private void LoadUW1()
    {
        uw1Entries.Clear();
        if (!ValidateBasePath(uw1Path)) { return; }

        string prevRes = UWClass._RES;
        string prevBase = Loader.BasePath;
        try
        {
            UWClass._RES = UWClass.GAME_UW1;
            Loader.BasePath = UWClass.CleanPath(uw1Path);

            PaletteLoader paletteLoader = new PaletteLoader(Path.Combine(Loader.BasePath, "DATA", "PALS.DAT"), -1);
            TextureLoader textureLoader = new TextureLoader();

            int uw1FloorCount = DetectTextureCount(Path.Combine(Loader.BasePath, "DATA", "F32.TR"));
            if (uw1FloorCount <= 0) { uw1FloorCount = 104; }

            for (int i = 210; i < 210 + uw1FloorCount; i++)
            {
                if (!TryLoadTexture(textureLoader, paletteLoader, i, out Texture2D tex)) { continue; }
                uw1Entries.Add(new TextureEntry { texture = tex, label = $"#{i}", likelyWater = IsLikelyWater(tex) });
            }
        }
        finally
        {
            UWClass._RES = prevRes;
            Loader.BasePath = prevBase;
        }
    }

    private void LoadUW2()
    {
        uw2Entries.Clear();
        if (!ValidateBasePath(uw2Path)) { return; }

        string prevRes = UWClass._RES;
        string prevBase = Loader.BasePath;
        try
        {
            UWClass._RES = UWClass.GAME_UW2;
            Loader.BasePath = UWClass.CleanPath(uw2Path);

            PaletteLoader paletteLoader = new PaletteLoader(Path.Combine(Loader.BasePath, "DATA", "PALS.DAT"), -1);
            TextureLoader textureLoader = new TextureLoader();

            int uw2Count = DetectTextureCount(Path.Combine(Loader.BasePath, "DATA", "T64.TR"));
            if (uw2Count <= 0) { uw2Count = 256; }

            for (int i = 0; i < uw2Count; i++)
            {
                if (!TryLoadTexture(textureLoader, paletteLoader, i, out Texture2D tex)) { continue; }
                uw2Entries.Add(new TextureEntry { texture = tex, label = $"#{i}", likelyWater = IsLikelyWater(tex) });
            }
        }
        finally
        {
            UWClass._RES = prevRes;
            Loader.BasePath = prevBase;
        }
    }

    private void TryLoadPathsFromConfig()
    {
        try
        {
            string configPath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
            if (!File.Exists(configPath)) { return; }

            ConfigRoot config = JsonUtility.FromJson<ConfigRoot>(File.ReadAllText(configPath));
            if (config == null || config.paths == null) { return; }

            if (string.IsNullOrWhiteSpace(uw1Path)) { uw1Path = config.paths.PATH_UW1; }
            if (string.IsNullOrWhiteSpace(uw2Path)) { uw2Path = config.paths.PATH_UW2; }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("UWTextureViewer: Unable to load config.json paths: " + ex.Message);
        }
    }

    private static int DetectTextureCount(string textureFilePath)
    {
        if (!File.Exists(textureFilePath)) { return 0; }

        byte[] file = File.ReadAllBytes(textureFilePath);
        if (file.Length < 8) { return 0; }

        uint firstOffset = Loader.ConvertInt32(file[4], file[5], file[6], file[7]);
        if (firstOffset <= 4 || firstOffset > file.Length) { return 0; }

        int count = (int)((firstOffset - 4) / 4);
        return Mathf.Max(0, count);
    }

    private static bool TryLoadTexture(TextureLoader textureLoader, PaletteLoader paletteLoader, int index, out Texture2D texture)
    {
        texture = null;
        try
        {
            texture = textureLoader.LoadImageAt(index, paletteLoader.Palettes[0]);
            return texture != null;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"UWTextureViewer: Skipping texture #{index} due to load failure: {ex.Message}");
            return false;
        }
    }

    private void ExportTextures(List<TextureEntry> entries, string prefix)
    {
        if (entries == null || entries.Count == 0)
        {
            EditorUtility.DisplayDialog("No Textures", "Load textures first.", "OK");
            return;
        }

        string folder = EditorUtility.OpenFolderPanel("Export Textures", Application.dataPath, "");
        if (string.IsNullOrEmpty(folder)) { return; }

        int exported = 0;
        foreach (var entry in entries)
        {
            if (entry.texture == null) { continue; }
            byte[] png = entry.texture.EncodeToPNG();
            if (png == null) { continue; }
            string safeLabel = entry.label.Replace("#", "").Trim();
            string fileName = $"{prefix}_{safeLabel}.png";
            File.WriteAllBytes(Path.Combine(folder, fileName), png);
            exported++;
        }

        EditorUtility.DisplayDialog("Export Complete", $"Exported {exported} textures to {folder}", "OK");
    }

    private static bool ValidateBasePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            EditorUtility.DisplayDialog("Invalid Path", "Please provide a valid base game path.", "OK");
            return false;
        }
        return true;
    }

    private static bool IsLikelyWater(Texture2D texture)
    {
        Color[] pixels = texture.GetPixels();
        if (pixels == null || pixels.Length == 0) { return false; }

        float blueDominant = 0f;
        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i];
            if ((c.b > c.r * 1.15f) && (c.b > c.g * 1.05f) && (c.b > 0.18f))
            {
                blueDominant += 1f;
            }
        }

        float ratio = blueDominant / pixels.Length;
        return ratio > 0.28f;
    }

    [Serializable]
    private class ConfigRoot
    {
        public ConfigPaths paths;
    }

    [Serializable]
    private class ConfigPaths
    {
        public string PATH_UW1;
        public string PATH_UW2;
    }

    private class TextureEntry
    {
        public Texture2D texture;
        public string label;
        public bool likelyWater;
    }
}
