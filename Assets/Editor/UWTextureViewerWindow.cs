using System.Collections.Generic;
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

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Underworld Ground/Floor/Water Texture Viewer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Loads UW1 and UW2 textures directly from DATA files. UW1 shows floor range (210-313). UW2 shows full texture set (0-255).", MessageType.Info);

        uw1Path = EditorGUILayout.TextField("UW1 Base Path", uw1Path);
        uw2Path = EditorGUILayout.TextField("UW2 Base Path", uw2Path);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Load UW1")) { LoadUW1(); }
            if (GUILayout.Button("Load UW2")) { LoadUW2(); }
            if (GUILayout.Button("Load Both")) { LoadUW1(); LoadUW2(); }
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
            GUILayout.Label(entry.texture, GUILayout.Width(thumbSize), GUILayout.Height(thumbSize));
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

            for (int i = 210; i <= 313; i++)
            {
                Texture2D tex = textureLoader.LoadImageAt(i, paletteLoader.Palettes[0]);
                if (tex == null) { continue; }
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

            for (int i = 0; i <= 255; i++)
            {
                Texture2D tex = textureLoader.LoadImageAt(i, paletteLoader.Palettes[0]);
                if (tex == null) { continue; }
                uw2Entries.Add(new TextureEntry { texture = tex, label = $"#{i}", likelyWater = IsLikelyWater(tex) });
            }
        }
        finally
        {
            UWClass._RES = prevRes;
            Loader.BasePath = prevBase;
        }
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

    private class TextureEntry
    {
        public Texture2D texture;
        public string label;
        public bool likelyWater;
    }
}
