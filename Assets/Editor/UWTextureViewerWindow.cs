using System;
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

    private GameChoice selectedGame = GameChoice.UW1;
    private AssetCategory selectedCategory = AssetCategory.Textures;

    private readonly List<TextureEntry> uw1Entries = new List<TextureEntry>();
    private readonly List<TextureEntry> uw2Entries = new List<TextureEntry>();

    [MenuItem("Tools/UW/Texture Viewer (Ground-Floor-Water)")]
    private static void OpenWindow()
    {
        var window = GetWindow<UWTextureViewerWindow>("UW Assets");
        window.minSize = new Vector2(900f, 500f);
    }

    private void OnEnable() => TryLoadPathsFromConfig();

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Underworld Asset Viewer & Exporter", EditorStyles.boldLabel);
        uw1Path = EditorGUILayout.TextField("UW1 Base Path", uw1Path);
        uw2Path = EditorGUILayout.TextField("UW2 Base Path", uw2Path);
        selectedGame = (GameChoice)EditorGUILayout.EnumPopup("Game", selectedGame);
        selectedCategory = (AssetCategory)EditorGUILayout.EnumPopup("Category", selectedCategory);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Load Selection")) { LoadCategory(selectedGame, selectedCategory, false); }
            if (GUILayout.Button("Append Selection")) { LoadCategory(selectedGame, selectedCategory, true); }
            if (GUILayout.Button("Load All Categories")) { LoadAllCategoriesForGame(selectedGame); }
            if (GUILayout.Button("Export Selected Category")) { ExportTextures(GetFilteredEntries(selectedGame, selectedCategory), selectedGame + "_" + selectedCategory); }
            if (GUILayout.Button("Clear Game")) { GetEntries(selectedGame).Clear(); }
        }

        showOnlyLikelyWater = EditorGUILayout.ToggleLeft("Only show likely water textures", showOnlyLikelyWater);
        thumbSize = EditorGUILayout.Slider("Thumbnail Size", thumbSize, 48f, 160f);
        columns = EditorGUILayout.IntSlider("Columns", columns, 4, 12);

        var filtered = GetFilteredEntries(selectedGame, selectedCategory);
        EditorGUILayout.LabelField($"Loaded {selectedGame} / {selectedCategory}: {filtered.Count}", EditorStyles.boldLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawEntries(filtered);
        EditorGUILayout.EndScrollView();
    }

    private void LoadAllCategoriesForGame(GameChoice game)
    {
        GetEntries(game).Clear();
        foreach (AssetCategory category in Enum.GetValues(typeof(AssetCategory)))
        {
            LoadCategory(game, category, true);
        }
    }

    private void LoadCategory(GameChoice game, AssetCategory category, bool append)
    {
        var target = GetEntries(game);
        if (!append)
        {
            target.RemoveAll(x => x.category == category);
        }

        string basePath = game == GameChoice.UW1 ? uw1Path : uw2Path;
        if (!ValidateBasePath(basePath)) { return; }

        string prevRes = UWClass._RES;
        string prevBase = Loader.BasePath;
        var prevInstance = GameWorldController.instance;
        GameObject tempObj = null;

        try
        {
            UWClass._RES = game == GameChoice.UW1 ? UWClass.GAME_UW1 : UWClass.GAME_UW2;
            Loader.BasePath = UWClass.CleanPath(basePath);
            PaletteLoader paletteLoader = new PaletteLoader(Path.Combine(Loader.BasePath, "DATA", "PALS.DAT"), -1);

            tempObj = new GameObject("__UWTextureViewerTempGWC");
            tempObj.hideFlags = HideFlags.HideAndDontSave;
            var tempGwc = tempObj.AddComponent<GameWorldController>();
            tempGwc.palLoader = paletteLoader;
            GameWorldController.instance = tempGwc;

            LoadCategoryInternal(game, category, target, paletteLoader);
        }
        finally
        {
            if (tempObj != null) { DestroyImmediate(tempObj); }
            GameWorldController.instance = prevInstance;
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
                LoadGRSet(target, category, "Tiles", new[] { GRLoader.TMFLAT_GR, GRLoader.TMOBJ_GR, GRLoader.DOORS_GR });
                break;
            case AssetCategory.Sprites:
                LoadGRSet(target, category, "Sprites", new[] { GRLoader.OBJECTS_GR, GRLoader.ANIMO_GR, GRLoader.WEAPONS_GR, GRLoader.BODIES_GR, GRLoader.ARMOR_F_GR, GRLoader.ARMOR_M_GR, GRLoader.HEADS_GR, GRLoader.CHARHEAD_GR, GRLoader.GENHEAD_GR, GRLoader.GHED_GR, GRLoader.FLASKS_GR, GRLoader.SPELLS_GR });
                break;
            case AssetCategory.UI:
                LoadGRSet(target, category, "UI", new[] { GRLoader.PANELS_GR, GRLoader.BUTTONS_GR, GRLoader.CURSORS_GR, GRLoader.CHRBTNS_GR, GRLoader.COMPASS_GR, GRLoader.CONVERSE_GR, GRLoader.CHAINS_GR, GRLoader.DRAGONS_GR, GRLoader.EYES_GR, GRLoader.INV_GR, GRLoader.LFTI_GR, GRLoader.OPBTN_GR, GRLoader.OPTB_GR, GRLoader.OPTBTNS_GR, GRLoader.POWER_GR, GRLoader.QUEST_GR, GRLoader.SCRLEDGE_GR, GRLoader.GEMPT_GR });
                LoadBytCategory(target, category, game);
                break;
        }
    }

    private void LoadTextureCategory(GameChoice game, List<TextureEntry> target, PaletteLoader paletteLoader)
    {
        var textureLoader = new TextureLoader();
        int count = (game == GameChoice.UW1) ? Mathf.Max(1, DetectTextureCount(Path.Combine(Loader.BasePath, "DATA", "W64.TR")) + DetectTextureCount(Path.Combine(Loader.BasePath, "DATA", "F32.TR"))) : Mathf.Max(1, DetectTextureCount(Path.Combine(Loader.BasePath, "DATA", "T64.TR")));
        if (game == GameChoice.UW1 && count <= 1) { count = 314; }
        if (game == GameChoice.UW2 && count <= 1) { count = 256; }
        for (int i = 0; i < count; i++)
        {
            if (!TryLoadTexture(textureLoader, paletteLoader, i, out var tex)) { continue; }
            target.Add(new TextureEntry { texture = tex, label = $"Texture_#{i}", category = AssetCategory.Textures, likelyWater = IsLikelyWater(tex) });
        }
    }

    private void LoadGRSet(List<TextureEntry> target, AssetCategory category, string tag, int[] grIndices)
    {
        foreach (int gr in grIndices)
        {
            var loader = new GRLoader(gr);
            int count = DetectGRImageCount(Path.Combine(Loader.BasePath, "DATA", GetGRFileName(gr)));
            for (int i = 0; i < count; i++)
            {
                Texture2D tex = loader.LoadImageAt(i);
                if (tex == null) { continue; }
                target.Add(new TextureEntry { texture = tex, label = $"{tag}_{GetGRFileName(gr).Replace(".GR", "")}_#{i}", category = category, likelyWater = IsLikelyWater(tex) });
            }
        }
    }

    private void LoadBytCategory(List<TextureEntry> target, AssetCategory category, GameChoice game)
    {
        var loader = new BytLoader();
        int count = game == GameChoice.UW1 ? 10 : 11;
        for (int i = 0; i < count; i++)
        {
            Texture2D tex = loader.LoadImageAt(i);
            if (tex == null) { continue; }
            target.Add(new TextureEntry { texture = tex, label = $"UI_BYT_#{i}", category = category, likelyWater = IsLikelyWater(tex) });
        }
    }

    private List<TextureEntry> GetFilteredEntries(GameChoice game, AssetCategory category)
    {
        List<TextureEntry> src = GetEntries(game);
        return src.FindAll(e => e.category == category && (!showOnlyLikelyWater || e.likelyWater));
    }

    private void DrawEntries(List<TextureEntry> entries)
    {
        if (entries.Count == 0) { EditorGUILayout.LabelField("No assets loaded."); return; }
        int col = 0;
        EditorGUILayout.BeginHorizontal();
        foreach (var entry in entries)
        {
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

    private List<TextureEntry> GetEntries(GameChoice game) => game == GameChoice.UW1 ? uw1Entries : uw2Entries;

    private static int DetectGRImageCount(string path)
    {
        if (!File.Exists(path)) { return 0; }
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length < 3) { return 0; }
        return (int)Loader.ConvertInt16(bytes[1], bytes[2]);
    }

    private static string GetGRFileName(int grIndex)
    {
        string[] names = { "3DWIN.GR", "ANIMO.GR", "ARMOR_F.GR", "ARMOR_M.GR", "BODIES.GR", "BUTTONS.GR", "CHAINS.GR", "CHARHEAD.GR", "CHRBTNS.GR", "COMPASS.GR", "CONVERSE.GR", "CURSORS.GR", "DOORS.GR", "DRAGONS.GR", "EYES.GR", "FLASKS.GR", "GENHEAD.GR", "HEADS.GR", "INV.GR", "LFTI.GR", "OBJECTS.GR", "OPBTN.GR", "OPTB.GR", "OPTBTNS.GR", "PANELS.GR", "POWER.GR", "QUEST.GR", "SCRLEDGE.GR", "SPELLS.GR", "TMFLAT.GR", "TMOBJ.GR", "WEAPONS.GR", "GEMPT.GR", "GHED.GR" };
        return names[grIndex];
    }

    private static int DetectTextureCount(string textureFilePath)
    {
        if (!File.Exists(textureFilePath)) { return 0; }
        byte[] file = File.ReadAllBytes(textureFilePath);
        if (file.Length < 8) { return 0; }
        uint firstOffset = Loader.ConvertInt32(file[4], file[5], file[6], file[7]);
        if (firstOffset <= 4 || firstOffset > file.Length) { return 0; }
        return Mathf.Max(0, (int)((firstOffset - 4) / 4));
    }

    private static bool TryLoadTexture(TextureLoader textureLoader, PaletteLoader paletteLoader, int index, out Texture2D texture)
    {
        texture = null;
        try { texture = textureLoader.LoadImageAt(index, paletteLoader.Palettes[0]); return texture != null; }
        catch { return false; }
    }

    private void ExportTextures(List<TextureEntry> entries, string prefix)
    {
        if (entries == null || entries.Count == 0) { EditorUtility.DisplayDialog("No Assets", "Load assets first.", "OK"); return; }
        string folder = EditorUtility.OpenFolderPanel("Export Assets", Application.dataPath, "");
        if (string.IsNullOrEmpty(folder)) { return; }
        int exported = 0;
        foreach (var entry in entries)
        {
            Texture2D readable = MakeReadableTexture(entry.texture);
            if (readable == null) { continue; }
            byte[] png = readable.EncodeToPNG();
            if (png == null) { continue; }
            string safeLabel = entry.label.Replace("#", "").Replace(" ", "_").Replace("/", "_");
            File.WriteAllBytes(Path.Combine(folder, prefix + "_" + safeLabel + ".png"), png);
            exported++;
        }
        EditorUtility.DisplayDialog("Export Complete", $"Exported {exported} assets to {folder}", "OK");
    }

    private static Texture2D MakeReadableTexture(Texture texture)
    {
        if (texture == null) { return null; }
        RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Graphics.Blit(texture, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        readable.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return readable;
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
            if ((c.b > c.r * 1.15f) && (c.b > c.g * 1.05f) && (c.b > 0.18f)) { blueDominant += 1f; }
        }
        return (blueDominant / pixels.Length) > 0.28f;
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

    private enum GameChoice { UW1, UW2 }
    private enum AssetCategory { Textures, Tiles, Sprites, UI }

    [Serializable] private class ConfigRoot { public ConfigPaths paths; }
    [Serializable] private class ConfigPaths { public string PATH_UW1; public string PATH_UW2; }

    private class TextureEntry
    {
        public Texture2D texture;
        public string label;
        public AssetCategory category;
        public bool likelyWater;
    }
}
