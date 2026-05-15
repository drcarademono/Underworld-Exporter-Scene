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

    private GameChoice selectedGame = GameChoice.UW1;
    private AssetCategory selectedCategory = AssetCategory.Textures;

    private readonly List<TextureEntry> uw1Entries = new List<TextureEntry>();
    private readonly List<TextureEntry> uw2Entries = new List<TextureEntry>();

    [MenuItem("Tools/UW/Texture Viewer (Ground-Floor-Water)")]
    private static void OpenWindow()
    {
        var window = GetWindow<UWTextureViewerWindow>("UW Textures");
        window.minSize = new Vector2(860f, 500f);
    }

    private void OnEnable() => TryLoadPathsFromConfig();

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Underworld Asset Viewer & Exporter", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Browse and export textures, tiles, sprites, and UI art from UW1/UW2 game DATA files.", MessageType.Info);

        uw1Path = EditorGUILayout.TextField("UW1 Base Path", uw1Path);
        uw2Path = EditorGUILayout.TextField("UW2 Base Path", uw2Path);

        selectedGame = (GameChoice)EditorGUILayout.EnumPopup("Game", selectedGame);
        selectedCategory = (AssetCategory)EditorGUILayout.EnumPopup("Category", selectedCategory);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Load Selection")) { LoadSelection(); }
            if (GUILayout.Button("Load All Categories (Game)")) { LoadAllCategoriesForGame(); }
            if (GUILayout.Button("Export Visible PNGs")) { ExportTextures(GetEntries(selectedGame), selectedGame.ToString()); }
            if (GUILayout.Button("Clear Game")) { GetEntries(selectedGame).Clear(); }
            if (GUILayout.Button("Clear All")) { uw1Entries.Clear(); uw2Entries.Clear(); }
        }

        showOnlyLikelyWater = EditorGUILayout.ToggleLeft("Only show likely water textures", showOnlyLikelyWater);
        thumbSize = EditorGUILayout.Slider("Thumbnail Size", thumbSize, 48f, 160f);
        columns = EditorGUILayout.IntSlider("Columns", columns, 4, 12);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawSection("UW1 Assets", uw1Entries);
        EditorGUILayout.Space(12);
        DrawSection("UW2 Assets", uw2Entries);
        EditorGUILayout.EndScrollView();
    }

    private void LoadSelection()
    {
        LoadCategory(selectedGame, selectedCategory, append: true);
    }

    private void LoadAllCategoriesForGame()
    {
        foreach (AssetCategory category in Enum.GetValues(typeof(AssetCategory)))
        {
            LoadCategory(selectedGame, category, append: true);
        }
    }

    private void LoadCategory(GameChoice game, AssetCategory category, bool append)
    {
        List<TextureEntry> target = GetEntries(game);
        if (!append) { target.Clear(); }

        string basePath = game == GameChoice.UW1 ? uw1Path : uw2Path;
        if (!ValidateBasePath(basePath)) { return; }

        string prevRes = UWClass._RES;
        string prevBase = Loader.BasePath;
        try
        {
            UWClass._RES = game == GameChoice.UW1 ? UWClass.GAME_UW1 : UWClass.GAME_UW2;
            Loader.BasePath = UWClass.CleanPath(basePath);
            var palLoader = new PaletteLoader(Path.Combine(Loader.BasePath, "DATA", "PALS.DAT"), -1);
            LoadCategoryInternal(game, category, target, palLoader);
        }
        finally
        {
            UWClass._RES = prevRes;
            Loader.BasePath = prevBase;
        }
    }

    private void LoadCategoryInternal(GameChoice game, AssetCategory category, List<TextureEntry> target, PaletteLoader palLoader)
    {
        switch (category)
        {
            case AssetCategory.Textures:
                LoadTextureCategory(game, target, palLoader);
                break;
            case AssetCategory.Tiles:
                LoadGRCategory(target, GRLoader.TMFLAT_GR, game.ToString(), "Tiles");
                break;
            case AssetCategory.Sprites:
                LoadGRCategory(target, GRLoader.OBJECTS_GR, game.ToString(), "Sprites");
                LoadGRCategory(target, GRLoader.ANIMO_GR, game.ToString(), "Animo");
                break;
            case AssetCategory.UI:
                LoadGRCategory(target, GRLoader.PANELS_GR, game.ToString(), "UI_Panels");
                LoadGRCategory(target, GRLoader.BUTTONS_GR, game.ToString(), "UI_Buttons");
                LoadGRCategory(target, GRLoader.CURSORS_GR, game.ToString(), "UI_Cursors");
                LoadBytCategory(target, game, game.ToString(), "UI_Byt");
                break;
        }
    }

    private void LoadTextureCategory(GameChoice game, List<TextureEntry> target, PaletteLoader paletteLoader)
    {
        TextureLoader textureLoader = new TextureLoader();
        if (game == GameChoice.UW1)
        {
            int wallCount = DetectTextureCount(Path.Combine(Loader.BasePath, "DATA", "W64.TR"));
            if (wallCount <= 0) { wallCount = 210; }
            int floorCount = DetectTextureCount(Path.Combine(Loader.BasePath, "DATA", "F32.TR"));
            if (floorCount <= 0) { floorCount = 104; }

            for (int i = 0; i < wallCount + floorCount; i++)
            {
                AddTextureEntry(target, textureLoader, paletteLoader, i, game.ToString(), "Texture");
            }
        }
        else
        {
            int uw2Count = DetectTextureCount(Path.Combine(Loader.BasePath, "DATA", "T64.TR"));
            if (uw2Count <= 0) { uw2Count = 256; }
            for (int i = 0; i < uw2Count; i++)
            {
                AddTextureEntry(target, textureLoader, paletteLoader, i, game.ToString(), "Texture");
            }
        }
    }

    private void LoadGRCategory(List<TextureEntry> target, int grIndex, string gameTag, string categoryTag)
    {
        GRLoader loader = new GRLoader(grIndex);
        int count = DetectGRImageCount(Path.Combine(Loader.BasePath, "DATA", GetGRFileName(grIndex)));
        for (int i = 0; i < count; i++)
        {
            Texture2D tex = loader.LoadImageAt(i);
            if (tex == null) { continue; }
            target.Add(new TextureEntry { texture = tex, label = $"{gameTag}_{categoryTag}_#{i}", likelyWater = IsLikelyWater(tex) });
        }
    }

    private void LoadBytCategory(List<TextureEntry> target, GameChoice game, string gameTag, string categoryTag)
    {
        BytLoader loader = new BytLoader();
        int count = game == GameChoice.UW1 ? 10 : 11;
        for (int i = 0; i < count; i++)
        {
            Texture2D tex = loader.LoadImageAt(i);
            if (tex == null) { continue; }
            target.Add(new TextureEntry { texture = tex, label = $"{gameTag}_{categoryTag}_#{i}", likelyWater = IsLikelyWater(tex) });
        }
    }

    private static string GetGRFileName(int grIndex)
    {
        string[] names = { "3DWIN.GR", "ANIMO.GR", "ARMOR_F.GR", "ARMOR_M.GR", "BODIES.GR", "BUTTONS.GR", "CHAINS.GR", "CHARHEAD.GR", "CHRBTNS.GR", "COMPASS.GR", "CONVERSE.GR", "CURSORS.GR", "DOORS.GR", "DRAGONS.GR", "EYES.GR", "FLASKS.GR", "GENHEAD.GR", "HEADS.GR", "INV.GR", "LFTI.GR", "OBJECTS.GR", "OPBTN.GR", "OPTB.GR", "OPTBTNS.GR", "PANELS.GR", "POWER.GR", "QUEST.GR", "SCRLEDGE.GR", "SPELLS.GR", "TMFLAT.GR", "TMOBJ.GR", "WEAPONS.GR", "GEMPT.GR", "GHED.GR" };
        return names[grIndex];
    }

    private static int DetectGRImageCount(string path)
    {
        if (!File.Exists(path)) { return 0; }
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length < 3) { return 0; }
        return (int)Loader.ConvertInt16(bytes[1], bytes[2]);
    }

    private void AddTextureEntry(List<TextureEntry> target, TextureLoader loader, PaletteLoader pal, int idx, string gameTag, string categoryTag)
    {
        if (!TryLoadTexture(loader, pal, idx, out Texture2D tex)) { return; }
        target.Add(new TextureEntry { texture = tex, label = $"{gameTag}_{categoryTag}_#{idx}", likelyWater = IsLikelyWater(tex) });
    }

    private List<TextureEntry> GetEntries(GameChoice game) => game == GameChoice.UW1 ? uw1Entries : uw2Entries;

    private void DrawSection(string title, List<TextureEntry> entries)
    {
        EditorGUILayout.LabelField($"{title} ({entries.Count})", EditorStyles.boldLabel);
        if (entries.Count == 0) { EditorGUILayout.LabelField("No assets loaded."); return; }
        int col = 0;
        EditorGUILayout.BeginHorizontal();
        foreach (var entry in entries)
        {
            if (showOnlyLikelyWater && !entry.likelyWater) { continue; }
            DrawTextureCard(entry);
            if (++col >= columns) { col = 0; EditorGUILayout.EndHorizontal(); EditorGUILayout.BeginHorizontal(); }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawTextureCard(TextureEntry entry)
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(thumbSize + 8)))
        {
            Rect previewRect = GUILayoutUtility.GetRect(thumbSize, thumbSize, GUILayout.Width(thumbSize), GUILayout.Height(thumbSize));
            if (entry.texture != null) { EditorGUI.DrawPreviewTexture(previewRect, entry.texture, null, ScaleMode.ScaleToFit); }
            else { EditorGUI.HelpBox(previewRect, "Missing", MessageType.None); }
            EditorGUILayout.LabelField(entry.label, EditorStyles.miniLabel, GUILayout.Width(thumbSize + 8));
        }
    }

    private void TryLoadPathsFromConfig(){ try { string configPath = Path.Combine(Directory.GetCurrentDirectory(), "config.json"); if (!File.Exists(configPath)) { return; } ConfigRoot config = JsonUtility.FromJson<ConfigRoot>(File.ReadAllText(configPath)); if (config == null || config.paths == null) { return; } if (string.IsNullOrWhiteSpace(uw1Path)) { uw1Path = config.paths.PATH_UW1; } if (string.IsNullOrWhiteSpace(uw2Path)) { uw2Path = config.paths.PATH_UW2; }} catch (Exception ex) { Debug.LogWarning("UWTextureViewer: Unable to load config.json paths: " + ex.Message);} }
    private static int DetectTextureCount(string textureFilePath){ if (!File.Exists(textureFilePath)) { return 0; } byte[] file = File.ReadAllBytes(textureFilePath); if (file.Length < 8) { return 0; } uint firstOffset = Loader.ConvertInt32(file[4], file[5], file[6], file[7]); if (firstOffset <= 4 || firstOffset > file.Length) { return 0; } return Mathf.Max(0, (int)((firstOffset - 4) / 4)); }
    private static bool TryLoadTexture(TextureLoader textureLoader, PaletteLoader paletteLoader, int index, out Texture2D texture){ texture = null; try { texture = textureLoader.LoadImageAt(index, paletteLoader.Palettes[0]); return texture != null; } catch (System.Exception ex) { Debug.LogWarning($"UWTextureViewer: Skipping texture #{index} due to load failure: {ex.Message}"); return false; }}

    private void ExportTextures(List<TextureEntry> entries, string prefix)
    {
        if (entries == null || entries.Count == 0) { EditorUtility.DisplayDialog("No Assets", "Load assets first.", "OK"); return; }
        string folder = EditorUtility.OpenFolderPanel("Export Assets", Application.dataPath, ""); if (string.IsNullOrEmpty(folder)) { return; }
        int exported = 0;
        foreach (var entry in entries)
        {
            if (entry.texture == null) { continue; }
            Texture2D readable = MakeReadableTexture(entry.texture); if (readable == null) { continue; }
            byte[] png = readable.EncodeToPNG(); if (png == null) { continue; }
            string safeLabel = entry.label.Replace("#", "").Replace(" ", "_").Trim();
            File.WriteAllBytes(Path.Combine(folder, $"{prefix}_{safeLabel}.png"), png);
            exported++;
        }
        EditorUtility.DisplayDialog("Export Complete", $"Exported {exported} assets to {folder}", "OK");
    }

    private static Texture2D MakeReadableTexture(Texture texture){ if (texture == null) { return null; } RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear); Graphics.Blit(texture, rt); RenderTexture prev = RenderTexture.active; RenderTexture.active = rt; Texture2D readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false); readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); readable.Apply(); RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); return readable; }
    private static bool ValidateBasePath(string path){ if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) { EditorUtility.DisplayDialog("Invalid Path", "Please provide a valid base game path.", "OK"); return false; } return true; }
    private static bool IsLikelyWater(Texture2D texture){ Color[] pixels = texture.GetPixels(); if (pixels == null || pixels.Length == 0) { return false; } float blueDominant = 0f; for (int i = 0; i < pixels.Length; i++) { Color c = pixels[i]; if ((c.b > c.r * 1.15f) && (c.b > c.g * 1.05f) && (c.b > 0.18f)) { blueDominant += 1f; } } return (blueDominant / pixels.Length) > 0.28f; }

    private enum GameChoice { UW1, UW2 }
    private enum AssetCategory { Textures, Tiles, Sprites, UI }
    [Serializable] private class ConfigRoot { public ConfigPaths paths; }
    [Serializable] private class ConfigPaths { public string PATH_UW1; public string PATH_UW2; }
    private class TextureEntry { public Texture2D texture; public string label; public bool likelyWater; }
}
