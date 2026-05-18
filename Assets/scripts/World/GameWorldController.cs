using System;
using UnityEngine;
#if UNITY_EDITOR
#endif
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.AI;

/// <summary>
/// Game world controller for controlling references and various global activities
/// </summary>

public class GameWorldController : UWEBase
{
    public Configuration config;
    public bool EnableUnderworldGenerator = false;
    public bool DoCleanUp = true;
    public GameObject ceiling;

    public WhatTheHellIsSCD_ARK whatTheHellIsThatFileFor;

    public enum UW1_LevelNos
    {
        EntranceLevel = 0,
        MountainMen = 1,
        Swamp = 2,
        Knights = 3,
        Catacombs = 4,
        Seers = 5,
        Tybal = 6,
        Volcano = 7,
        Ethereal = 8
    };

    public static string[] UW1_LevelNames = new string[]
    {
        "Outcast",
        "Dwarf",
        "Swamp",
        "Knight",
        "Tombs",
        "Seers",
        "Tybal",
        "Abyss",
        "Void"
    };

    /// <summary>
    /// First index of the level no for a world
    /// </summary>
    public enum Worlds
    {
        Britannia = 0,
        PrisonTower = 8,
        Killorn = 16,
        Ice = 24,
        Talorus = 32,
        Academy = 40,
        Tomb = 48,
        Pits = 56,
        Ethereal = 64
    };

    public enum UW2_LevelNos
    {
        Britannia0 = 0,
        Britannia1 = 1,
        Britannia2 = 2,
        Britannia3 = 3,
        Britannia4 = 4,
        Prison0 = 8,
        Prison1 = 9,
        Prison2 = 10,
        Prison3 = 11,
        Prison4 = 12,
        Prison5 = 13,
        Prison6 = 14,
        Prison7 = 15,
        Killorn0 = 16,
        Killorn1 = 17,
        Ice0 = 24,
        Ice1 = 25,
        Talorus0 = 32,
        Talorus1 = 33,
        Academy0 = 40,
        Academy1 = 41,
        Academy2 = 42,
        Academy3 = 43,
        Academy4 = 44,
        Academy5 = 45,
        Academy6 = 46,
        Academy7 = 47,
        Tomb0 = 48,
        Tomb1 = 49,
        Tomb2 = 50,
        Tomb3 = 51,
        Pits0 = 56,
        Pits1 = 57,
        Pits2 = 58,
        Ethereal0 = 64,
        Ethereal1 = 65,
        Ethereal2 = 66,
        Ethereal3 = 67,
        Ethereal4 = 68,
        Ethereal5 = 69,
        Ethereal6 = 70,
        Ethereal7 = 71,
        Ethereal8 = 72
    };


    [Header("Controls")]
    public MouseLook MouseX;
    public MouseLook MouseY;

    [Header("World Options")]
    /// <summary>
    /// Enables texture animation effects
    /// </summary>
    public bool EnableTextureAnimation;

    /// <summary>
    /// The grey scale shader. Reference to allow loading of a hidden shader.
    /// </summary>
    public Shader greyScale;

    /// <summary>
    /// The vortex effect shader.  Reference to allow loading of a hidden shader.
    /// </summary>
    public Shader vortex;

    /// <summary>
    /// Is the game at the main menu or should it start at the mainmenu.
    /// </summary>
    public bool AtMainMenu;


    /// <summary>
    /// Enable timer triggers
    /// </summary>
    public bool EnableTimerTriggers = true;

    /// <summary>
    /// The timer execution rate.
    /// </summary>
    public float TimerRate = 1f;


    [Header("Parent Objects")]
    /// <summary>
    /// The level model parent object
    /// </summary>
    public GameObject LevelModel;

    public GameObject TNovaLevelModel;
    public Terrain TNovaTerrain;

    /// <summary>
    /// The level model parent object
    /// </summary>
    public GameObject SceneryModel;


    /// <summary>
    /// Gameobject to load the objects at
    /// </summary>
    public GameObject _ObjectMarker;

    /// <summary>
    /// The instance of this class
    /// </summary>
    public static GameWorldController instance;

    /// <summary>
    /// The game object that picked up items are parented to.
    /// </summary>
    public GameObject InventoryMarker;

    
    /// <summary>
    /// What level number we are currently on.
    /// </summary>	
    public short dungeon_level
    {
        get { return (short)SaveGame.GetAt16(0x5d); }
        set { 
            Debug.Log("Setting level no to " + value);
            SaveGame.SetAt16(0x5d, (byte)value);
            }
    }

    [Header("Level")]
    public static bool LoadingGame = false;
    public static bool NavMeshReady = false;
    public bool[] NavMeshesReady = new bool[4];
    private static string LevelSignature;
    private float nextAudioListenerCheckTime = 0f;
    private int lastOverworldMapPixelX = int.MinValue;
    private int lastOverworldMapPixelY = int.MinValue;
    private bool rebuildingOverworld = false;
    private bool overworldStreamingInitialized = false;
    private Vector2Int lastPlayerChunk = new Vector2Int(int.MinValue, int.MinValue);
    private GameObject OverworldTerrainRoot;
    private Material overworldWaterMat;
    private Material overworldGrassMat;
    private Material overworldStoneMat;
    private Material overworldSnowMat;
    private Material overworldSandMat;
    private Material overworldSwampMat;
    private Dictionary<Vector2Int, GameObject> loadedOverworldChunks = new Dictionary<Vector2Int, GameObject>();
    private HashSet<Vector2Int> lowDetailOverworldChunks = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> noNatureOverworldChunks = new HashSet<Vector2Int>();
    private Texture2D[] overworldWaterFrames = null;
    private int overworldWaterFrameIndex = 0;
    private float overworldWaterAnimTimer = 0f;
    private int[,] overworldTerrainTypeMap = null;
    private OverworldSkyController overworldSkyController = null;
    private int overworldTerrainMapWidth = 0;
    private int overworldTerrainMapHeight = 0;
    private Texture2D cachedOverworldHeightmap = null;
    private Texture2D cachedNatureClimateMap = null;
    private string cachedNatureClimateMapPath = string.Empty;
    private OverworldNatureFlatsController cachedNatureFlatsController = null;
    private bool terrainClassifyUseDesertClimateMap = false;
    private float terrainClassifyClimateInvWorldW = 0f;
    private float terrainClassifyClimateInvWorldH = 0f;
    private Color32 terrainClassifyDesertColor;
    private readonly Queue<OverworldChunkBuildRequest> overworldChunkBuildQueue = new Queue<OverworldChunkBuildRequest>();
    private readonly HashSet<Vector2Int> queuedOverworldChunks = new HashSet<Vector2Int>();
    private readonly Dictionary<Vector2Int, OverworldChunkBuildRequest> pendingOverworldChunkRequests = new Dictionary<Vector2Int, OverworldChunkBuildRequest>();
    private readonly Stack<GameObject> overworldChunkPool = new Stack<GameObject>();
    private Coroutine overworldChunkBuildCoroutine = null;

    private struct OverworldChunkBuildRequest
    {
        public Vector2Int chunkCoord;
        public int sampleStep;
        public bool withCollision;
        public bool withNatureBillboards;
        public bool lowDetail;
        public bool noNature;
    }

    /// <summary>
    /// What level the player starts on in a quick start
    /// </summary>
    public short startLevel = 0;
    /// <summary>
    /// What start position for the player.
    /// </summary>
    public Vector3 StartPos = new Vector3(38f, 4f, 2.7f);

    [Header("Overworld Controller")]
    public OverworldTerrainController OverworldController;
    public bool StartInOverworld
    {
        get { return GetOverworldController().StartInOverworld; }
        set { GetOverworldController().StartInOverworld = value; }
    }


    /// <summary>
    /// Create object reports
    /// </summary>
    public bool CreateReports
    { get { return config.dev.GenerateReports; } }
    public bool ShowOnlyInUse
    { get { return config.dev.ShowOnlyInUse; } }

    [Header("Palettes")]
    /// <summary>
    /// Array of cycled game palettes for animation effects.
    /// </summary>
    public Texture2D[] paletteArray = new Texture2D[8];

    /// <summary>
    /// The index of the palette currently in use
    /// </summary>
    public int paletteIndex = 0;

    /// <summary>
    /// The palette index when going in reverse.
    /// </summary>
    public int paletteIndexReverse = 0;

    /// <summary>
    /// Shared palettes for artwork
    /// </summary>
    public PaletteLoader palLoader;


    [Header("LevelMaps")]
    /// <summary>
    /// The tilemap class for the game
    /// </summary>
    public TileMap[] Tilemaps = new TileMap[9];


    /// <summary>
    /// The auto maps.
    /// </summary>
    public AutoMap[] AutoMaps = new AutoMap[9];

    /// <summary>
    /// The object lists for each level.
    /// </summary>
    public ObjectLoader[] objectList = new ObjectLoader[9];

    /// <summary>
    /// Object list for the player inventory.
    /// </summary>
    public ObjectLoader inventoryLoader = new ObjectLoader();
    [Header("Property Lists")]
    /// <summary>
    /// The object master class for storing and reading object properties in an external file
    /// </summary>
    public ObjectMasters objectMaster;

    /// <summary>
    /// The critter properties from objects.dat
    /// </summary>
    public Critters critterData;


    /// <summary>
    /// The object dat file
    /// </summary>
    public ObjectDatLoader objDat;


    public MagicLookupTable magiclookup;

    /// <summary>
    /// The common object properties for uw
    /// </summary>
    public CommonObjectDatLoader commonObject;

    public ObjectPropLoader ShockObjProp;

    /// <summary>
    /// The terrain data from terrain.dat
    /// </summary>
    public TerrainDatLoader terrainData;

    [Header("Paths")]
    public string Lev_Ark_File_Selected = "";//"DATA\\Lev.ark";
    public string SCD_Ark_File_Selected = "";//"DATA\\SCD.ark";
                                             //Game paths
    public string Path_uw0
    {
        get { return config.paths.PATH_UWDEMO; }
    }
    public string Path_uw1
    {
        get { return config.paths.PATH_UW1; }
    }
    public string Path_uw2
    {
        get { return config.paths.PATH_UW2; }
    }
    public string Path_shock
    {
        get { return config.paths.PATH_SHOCK; }
    }
    public string Path_tnova
    {
        get { return config.paths.PATH_TNOVA; }
    }

    [Header("Material Lists")]
    /// <summary>
    /// The material master list for matching the texture list to materials.
    /// </summary>
    public Material[] MaterialMasterList = new Material[260];

    public Material[] SpecialMaterials = new Material[1];

    /// <summary>
    /// Default material for the editor
    /// </summary>
    public Material Jorge;

    /// <summary>
    /// The materials for doors  (doors.gr)
    /// </summary>
    public Material[] MaterialDoors = new Material[13];

    /// <summary>
    /// The materials for tmobj + models (tmobj.gr)
    /// </summary>
    public Material[] MaterialObj = new Material[54];

    /// <summary>
    /// The default model material.
    /// </summary>
    public Material modelMaterial;


    [Header("Nav Meshes")]
    /// <summary>
    /// Generate Nav meshes or not
    /// </summary>
    public bool bGenNavMeshes = true;
    public int GenNavMeshNextFrame = -1;
    public NavMeshSurface NavMeshLand;
    public NavMeshSurface NavMeshWater;
    public NavMeshSurface NavMeshAir;
    public NavMeshSurface NavMeshLava;
    public int MapMeshLayerMask = 0;
    public int DoorLayerMask = 0;


    [Header("Art Loaders")]
    /// <summary>
    /// The bytloader for bty files
    /// </summary>
    public BytLoader bytloader;
    /// <summary>
    /// The tex loader for textures
    /// </summary>
    public TextureLoader texLoader;
    /// <summary>
    /// The spell icons gr loader
    /// </summary>
    public GRLoader SpellIcons;
    /// <summary>
    /// The object art gr loader
    /// </summary>
    public GRLoader ObjectArt;

    /// <summary>
    /// The door art.
    /// </summary>
    public GRLoader DoorArt;

    /// <summary>
    /// The tm object art.
    /// </summary>
    public GRLoader TmObjArt;

    /// <summary>
    /// The tm flat art.
    /// </summary>
    public GRLoader TmFlatArt;

    /// <summary>
    /// Small animations art.
    /// </summary>
    public GRLoader TmAnimo;

    /// <summary>
    /// The female armor
    /// </summary>
    public GRLoader armor_f;

    /// <summary>
    /// The male armor.
    /// </summary>
    public GRLoader armor_m;

    /// <summary>
    /// The cursors art
    /// </summary>
    public GRLoader grCursors;

    /// <summary>
    /// The health & mana flasks.
    /// </summary>
    public GRLoader grFlasks;

    /// <summary>
    /// The option menus
    /// </summary>
    public GRLoader grOptbtns;

    /// <summary>
    /// The Compass 
    /// </summary>
    public GRLoader grCompass;

    /// <summary>
    /// Cutscene data
    /// </summary>
    public CutsLoader cutsLoader;

    public CritLoader[] critsLoader = new CritLoader[64];

    /// <summary>
    /// The weapon animation frames.
    /// </summary>
    public WeaponAnimation weaps;
    //public WeaponAnimationPlayer WeaponAnim;
    public WeaponsLoader weapongr;

    public int difficulty  //1=standard, 0=easy.
    {
        get
        {
            int offset = 0xB5;
            if (_RES == GAME_UW2) { offset = 0x302; }
            return (SaveGame.GetAt(offset)) & 0x1 ;
        }
        set
        {
            int offset = 0xB5;
            if (_RES == GAME_UW2) { offset = 0x302; }
            byte existingValue = SaveGame.GetAt(offset);
            byte mask = (1);
            if (value==1)
            {//set
                existingValue |= mask;
            }
            else
            {//unset
                existingValue = (byte)(existingValue & (~mask));
            }
            SaveGame.SetAt(offset, existingValue);
        }
    }

    public static bool LoadingObjects = false;

    public struct BablGlobal
    {
        public int ConversationNo;
        public int Size;
        public int[] Globals;
    };

    /// <summary>
    /// Conversation Global data
    /// </summary>
    public BablGlobal[] bGlobals;

    /// <summary>
    /// The virtual machine that runs conversations.
    /// </summary>
    public ConversationVM convVM;

    /// <summary>
    /// Does the world need to be redrawn (partially or completely.
    /// </summary>
    public static bool WorldReRenderPending = false;
    /// <summary>
    /// Does the game objects need to be redrawn. Used by the in game editor.
    /// </summary>
    public static bool ObjectReRenderPending = false;
    /// <summary>
    /// Force the entire world to be redrawn
    /// </summary>
    public static bool FullReRender = false;

    /// <summary>
    /// Key bindings for the game.
    /// </summary>
    //public KeyBindings keybinds
    //{
    //    get
    //    {
    //        return config.keys;
    //    }
    //}

    /// <summary>
    /// Event engine for running scd.ark events.
    /// </summary>
    public event_processor events;

    /// <summary>
    /// Starting X position on the map.
    /// </summary>
    private int startX = -1;
    /// <summary>
    /// Starting Y position on the map
    /// </summary>
    private int startY = -1;
    /// <summary>
    /// Starting height on the map.
    /// </summary>
    private int StartHeight = -1;


    /// <summary>
    /// Load the appropiate game path fro the selected _RES
    /// </summary>
    /// <param name="_RES"></param>
    void LoadPath(string _RES)
    {
        string path = "";

        switch (_RES)
        {
            case GAME_UWDEMO: path = instance.Path_uw0; break;
            case GAME_UW1: path = instance.Path_uw1; break;
            case GAME_UW2: path = instance.Path_uw2; break;
            case GAME_SHOCK: path = instance.Path_shock; break;
            case GAME_TNOVA: path = instance.Path_tnova; break;
        }

        Loader.BasePath = path;
        //Loader.sep = sep;
    }

    /// <summary>
    /// Awake this instance.
    /// </summary>
    /// Should be the very first script to run 
    void Awake()
    {
        instance = this;
        //Set the seperator in file paths.
        // UWClass.sep = Path.DirectorySeparatorChar;
        Lev_Ark_File_Selected = Path.Combine("DATA", "LEV.ARK");
        SCD_Ark_File_Selected = Path.Combine("DATA", "SCD.ARK");

        LoadConfigFile();
        return;
    }


    void Start()
    {
        instance = this;
        AtMainMenu = true;
        //var config = new Configuration();
        //Configuration.Save();
    }

    void Update()
    {
        if (Time.time >= nextAudioListenerCheckTime)
        {
            EnforceSingleAudioListener();
            nextAudioListenerCheckTime = Time.time + 1f;
        }
        PositionDetect();
        UpdateOverworldStreaming();
        UpdateOverworldWaterAnimation();
    }


    private void UpdateOverworldWaterAnimation()
    {
        if (overworldWaterFrames == null || overworldWaterFrames.Length <= 1 || overworldWaterMat == null) { return; }
        OverworldTerrainController overworld = GetOverworldController();
        if (!overworld.AnimateWater) { return; }

        overworldWaterAnimTimer += Time.deltaTime;
        if (overworldWaterAnimTimer < overworld.WaterAnimFrameTime) { return; }

        overworldWaterAnimTimer = 0f;
        overworldWaterFrameIndex = (overworldWaterFrameIndex + 1) % overworldWaterFrames.Length;
        Texture2D frame = overworldWaterFrames[overworldWaterFrameIndex];
        if (frame != null)
        {
            frame.wrapMode = TextureWrapMode.Repeat;
            overworldWaterMat.mainTexture = frame;
        }
    }

    private void UpdateOverworldStreaming()
    {
        if (rebuildingOverworld) { return; }
        if (_RES != GAME_UW2) { return; }
        OverworldTerrainController overworld = GetOverworldController();
        if (!overworld.StartInOverworld) { return; }
        if (UWCharacter.Instance == null) { return; }
        if (!overworldStreamingInitialized) { return; }
        if (OverworldTerrainRoot == null) { return; }

        UpdateOverworldTerrainType(overworld);

        Vector2Int currentChunk = GetPlayerChunkCoord(overworld, UWCharacter.Instance.transform.position);
        if (currentChunk == lastPlayerChunk)
        {
            return;
        }

        lastPlayerChunk = currentChunk;
        Texture2D map = cachedOverworldHeightmap;
        if (map == null) { return; }
        EnsureChunksAround(currentChunk, map, overworld);
        if (overworld.LoadDistantChunks)
        {
            EnsureDistantChunks(currentChunk, map, overworld);
        }
    }

    /// <summary>
    /// Generate NAV meshes for the map.
    /// </summary>
    /// <returns></returns>
    IEnumerator UpdateNavMeshes()
    {
        NavMeshReady = false;
        NavMeshesReady[0] = false;//land
        NavMeshesReady[1] = false;//water
        NavMeshesReady[2] = false;//lava
        //NavMeshesReady[3]=false;//air
        while (LoadingGame)
        {
            yield return new WaitForSeconds(0.1f);
        }
        yield return new WaitForSeconds(0.5f);
        StartCoroutine(GenerateNavmesh(NavMeshLand, 0));//Update nav mesh for the land
        StartCoroutine(GenerateNavmesh(NavMeshWater, 1));//For water
        StartCoroutine(GenerateNavmesh(NavMeshLava, 2));//for lava
        StartCoroutine(GenerateNavmesh(NavMeshAir, 3));//for air


        while (!(
                    (NavMeshesReady[0]) &&
                    (NavMeshesReady[1]) &&
                    (NavMeshesReady[2]) &&
                    (NavMeshesReady[3])
                )
            )
        {
            yield return new WaitForSeconds(0.1f);
        }
        yield return new WaitForSeconds(1.5f);
        NavMeshReady = true;
        yield return 0;
    }

    /// <summary>
    /// Build a Nav Mesh for the specified layer.
    /// </summary>
    /// <param name="navmeshobj"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    IEnumerator GenerateNavmesh(NavMeshSurface navmeshobj, int index)
    {
        if (navmeshobj.navMeshData == null)
        {
            navmeshobj.BuildNavMesh();
        }
        else
        {
            AsyncOperation task = navmeshobj.UpdateNavMesh(navmeshobj.navMeshData);
            while (!task.isDone)
            {
                yield return new WaitForSeconds(0.1f);
            }
        }
        NavMeshesReady[index] = true;
        yield return 0;
    }

    void LateUpdate()
    {
        if (WorldReRenderPending)
        {//Level Needs redrawing.
            if ((FullReRender) && (!EditorMode))
            {
                //	CurrentTileMap().CleanUp(_RES);				
            }
            TileMapRenderer.GenerateLevelFromTileMap(instance.LevelModel, instance.SceneryModel, _RES, CurrentTileMap(), CurrentObjectList(), !FullReRender);
            if (ObjectReRenderPending)
            {
                ObjectReRenderPending = false;
                ObjectLoader.RenderObjectList(CurrentObjectList(), CurrentTileMap(), DynamicObjectMarker().gameObject);
            }
            WorldReRenderPending = false;
            FullReRender = false;
            if (!EditorMode)
            {
                NavMeshLand.UpdateNavMesh(NavMeshLand.navMeshData);
                NavMeshWater.UpdateNavMesh(NavMeshWater.navMeshData);
                //NavMeshAir.UpdateNavMesh(NavMeshAir.navMeshData);
                NavMeshLava.UpdateNavMesh(NavMeshLava.navMeshData);
            }
            else
            {
                IngameEditor.instance.RefreshTileMap();
            }
        }
    }

    /// <summary>
    /// Begins the specified game.
    /// </summary>
    /// <param name="res">Res.</param>
    public void Begin(string res)
    {
        //Save config file as paths may have been changed.
        Configuration.Save(config);
        UWHUD.instance.gameSelectUi.SetActive(false);

        string requestedRes = res;
        string effectiveRes = res;
        if (requestedRes == GAME_UWDEMO)
        {
            effectiveRes = GAME_UW2;
            GetOverworldController().StartInOverworld = true;
        }

        LoadPath(effectiveRes);
        _RES = effectiveRes;//game;
        UWClass._RES = effectiveRes;//game;
        SaveGame.InitEmptySaveGame();

        //Set some layers for the AI to use to detect walls and doors.
        MapMeshLayerMask = 1 << LevelModel.layer;
        DoorLayerMask = 1 << LayerMask.NameToLayer("Doors");

        switch (effectiveRes)
        {
            case GAME_TNOVA:
                UWCharacter.Instance.XAxis.enabled = true;
                UWCharacter.Instance.YAxis.enabled = true;
                UWCharacter.Instance.MouseLookEnabled = true;
                UWCharacter.Instance.speedMultiplier = 20;
                break;
            case GAME_SHOCK:
                palLoader = new PaletteLoader(Path.Combine(Loader.BasePath, "res", "DATA", "GAMEPAL.RES"), 700);
                texLoader = new TextureLoader();
                objectMaster = new ObjectMasters();
                ObjectArt = new GRLoader(Path.Combine(Loader.BasePath, "res", "DATA", "OBJART.RES"), 1350);
                ShockObjProp = new ObjectPropLoader();
                UWCharacter.Instance.XAxis.enabled = true;
                UWCharacter.Instance.YAxis.enabled = true;
                UWCharacter.Instance.MouseLookEnabled = true;
                UWCharacter.Instance.speedMultiplier = 20;
                break;
            default:
                StartCoroutine(MusicController.instance.Begin());
                objectMaster = new ObjectMasters();
                objDat = new ObjectDatLoader();
                commonObject = new CommonObjectDatLoader();
                palLoader = new PaletteLoader(Path.Combine(Loader.BasePath, "DATA", "PALS.DAT"), -1);
                magiclookup = new MagicLookupTable();
                //Create palette cycles and store them in the palette array
                PaletteLoader palCycler = new PaletteLoader(Path.Combine(Loader.BasePath, "DATA", "PALS.DAT"), -1);

                for (int c = 0; c <= 27; c++)
                {//Create palette cycles
                    switch (_RES)
                    {
                        case GAME_UW2:
                            Palette.cyclePalette(palCycler.Palettes[0], 224, 16);
                            Palette.cyclePaletteReverse(palCycler.Palettes[0], 3, 6);
                            break;
                        default:
                            Palette.cyclePalette(palCycler.Palettes[0], 48, 16);//Forward
                            Palette.cyclePaletteReverse(palCycler.Palettes[0], 16, 7);//Reverse direction.
                            break;
                    }
                    paletteArray[c] = Palette.toImage(palCycler.Palettes[0]);
                }


                //Create art loaders
                bytloader = new BytLoader();
                texLoader = new TextureLoader();
                ObjectArt = new GRLoader(GRLoader.OBJECTS_GR)
                {
                    xfer = true
                };
                SpellIcons = new GRLoader(GRLoader.SPELLS_GR);
                DoorArt = new GRLoader(GRLoader.DOORS_GR);
                TmObjArt = new GRLoader(GRLoader.TMOBJ_GR);
                TmFlatArt = new GRLoader(GRLoader.TMFLAT_GR);
                TmAnimo = new GRLoader(GRLoader.ANIMO_GR)
                {
                    xfer = true
                }; armor_f = new GRLoader(GRLoader.ARMOR_F_GR);
                armor_m = new GRLoader(GRLoader.ARMOR_M_GR);
                grCursors = new GRLoader(GRLoader.CURSORS_GR);
                grFlasks = new GRLoader(GRLoader.FLASKS_GR);
                grOptbtns = new GRLoader(GRLoader.OPTBTNS_GR);
                grCompass = new GRLoader(GRLoader.COMPASS_GR);
                terrainData = new TerrainDatLoader();
                weaps = new WeaponAnimation();
                break;
        }

        switch (_RES)
        {//Set Start Positions
            case GAME_SHOCK:
            case GAME_TNOVA:
                break;
            case GAME_UW2:
                {
                    if (instance.startLevel == 0)
                    {//Avatar's bedroom
                        instance.StartPos = new Vector3(23.43f, 3.95f, 58.29f);
                    }
                    break;
                }
            case GAME_UWDEMO:
                instance.StartPos = new Vector3(39.06f, 3.96f, 3f); break;
            default:
                {
                    if (instance.startLevel == 0)
                    {//entrance to the abyss
                        instance.StartPos = new Vector3(39.06f, 3.96f, 3f);
                    }
                    break;
                }
        }

        switch (effectiveRes)
        {
            case GAME_TNOVA:
                AtMainMenu = false;
                overworldStreamingInitialized = false;
        TileMapRenderer.EnableCollision = false;
                bGenNavMeshes = false;
                UWHUD.instance.gameObject.SetActive(false);
                UWHUD.instance.window.SetFullScreen();
                //UWCharacter.Instance.isFlying = true;
                UWCharacter.Instance.playerMotor.enabled = true;
                UWCharacter.Instance.playerCam.backgroundColor = Color.white;
                UWCharacter.Instance.transform.position = new Vector3(128f, 256f, 128f);
                SwitchTNovaMap("");
                return;
            case GAME_SHOCK:
                overworldStreamingInitialized = false;
        TileMapRenderer.EnableCollision = false;
                bGenNavMeshes = false;
                AtMainMenu = false;
                UWCharacter.Instance.isFlying = true;
                UWCharacter.Instance.playerMotor.enabled = true;
                UWHUD.instance.gameObject.SetActive(false);
                UWHUD.instance.window.SetFullScreen();
                SwitchLevel(startLevel);
                return;

            case GAME_UWDEMO:
                //case GAME_UW2:
                //UW Demo does not go to the menu. It will load automatically into the gameworld
                AtMainMenu = false;
                UWCharacter.Instance.transform.position = instance.StartPos;
                UWHUD.instance.Begin();
                UWCharacter.Instance.Begin();
                UWCharacter.Instance.playerInventory.Begin();
                StringController.instance.LoadStringsPak(Path.Combine(Loader.BasePath, "DATA", "STRINGS.PAK"));
                break;
            case GAME_UW2:
                UWHUD.instance.Begin();
                UWCharacter.Instance.Begin();
                UWCharacter.Instance.playerInventory.Begin();
                //Quest.QuestVariablesOBSOLETE = new int[250];//UW has a lot more quests. This value needs to be confirmed.
                StringController.instance.LoadStringsPak(Path.Combine(Loader.BasePath, "DATA", "STRINGS.PAK"));
                break;
            default:
                UWHUD.instance.Begin();
                UWCharacter.Instance.Begin();
                UWCharacter.Instance.playerInventory.Begin();
                StringController.instance.LoadStringsPak(Path.Combine(Loader.BasePath, "DATA", "STRINGS.PAK"));
                break;
        }

        if (EnableTextureAnimation == true)
        {
            UWHUD.instance.CutsceneFullPanel.SetActive(false);
            InvokeRepeating("UpdateAnimation", 0.2f, 0.2f);
        }

        if (AtMainMenu)
        {
            SwitchLevel(-1);//Turn off all level maps
            UWHUD.instance.CutsceneFullPanel.SetActive(true);
            UWHUD.instance.mainmenu.gameObject.SetActive(true);
            //Freeze player movement and put them at a set location
            UWCharacter.Instance.playerController.enabled = false;
            UWCharacter.Instance.playerMotor.enabled = false;
            UWCharacter.Instance.transform.position = Vector3.zero;
            MusicController.instance.InIntro = true;//Set music state.
        }
        else
        {
            UWHUD.instance.CutsceneFullPanel.SetActive(false);
            UWHUD.instance.mainmenu.gameObject.SetActive(false);
            UWHUD.instance.RefreshPanels(UWHUD.HUD_MODE_INVENTORY);

            if ((_RES == GAME_UW2) && (GetOverworldController().StartInOverworld))
            {
                SetupOverworldStart();
            }
            else
            {
                SwitchLevel(startLevel);
            }
        }
        return;
    }

    private OverworldTerrainController GetOverworldController()
    {
        if (OverworldController == null)
        {
            OverworldController = FindObjectOfType<OverworldTerrainController>();
        }
        if (OverworldController == null)
        {
            GameObject controllerObj = new GameObject("_OverworldController");
            OverworldController = controllerObj.AddComponent<OverworldTerrainController>();
        }
        return OverworldController;
    }

    public void SetupOverworldStart()
    {
        overworldStreamingInitialized = false;
        TileMapRenderer.EnableCollision = false;

        GameObject existingRoot = GameObject.Find("OverworldTerrainRoot");
        if (existingRoot != null) { Destroy(existingRoot); }
        GameObject existingSun = GameObject.Find("OverworldSun");
        if (existingSun != null) { Destroy(existingSun); }

        if (LevelModel != null) { LevelModel.SetActive(false); }
        if (SceneryModel != null) { SceneryModel.SetActive(false); }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.68f, 0.55f);

        OverworldTerrainController overworld = GetOverworldController();
        Texture2D heightmap = Resources.Load<Texture2D>(overworld.HeightmapResourcePath);
        if (heightmap == null)
        {
            Debug.LogWarning("Could not load overworld heightmap at Resources/" + overworld.HeightmapResourcePath);
            return;
        }
        cachedOverworldHeightmap = heightmap;

        OverworldTerrainRoot = new GameObject("OverworldTerrainRoot");
        loadedOverworldChunks.Clear();
        lowDetailOverworldChunks.Clear();
        noNatureOverworldChunks.Clear();

        int tpp = Mathf.Max(1, overworld.TilesPerPixel);
        overworldTerrainMapWidth = Mathf.Max(2, heightmap.width / tpp);
        overworldTerrainMapHeight = Mathf.Max(2, heightmap.height / tpp);
        overworldTerrainTypeMap = new int[overworldTerrainMapWidth, overworldTerrainMapHeight];
        for (int x = 0; x < overworldTerrainMapWidth; x++)
        {
            for (int y = 0; y < overworldTerrainMapHeight; y++)
            {
                overworldTerrainTypeMap[x, y] = (int)TerrainDatLoader.TerrainTypes.Unknown;
            }
        }

        overworldWaterMat = BuildOverworldSurfaceMaterial(overworld.WaterTextureIndex, null, new Color(0.15f, 0.28f, 0.35f), overworld.ChunkSizeSamples, overworld.ChunkSizeSamples);
        overworldGrassMat = BuildOverworldSurfaceMaterial(overworld.GrassTextureIndex, overworld.GrassMaterialOverride, new Color(0.22f, 0.58f, 0.22f), overworld.ChunkSizeSamples, overworld.ChunkSizeSamples);
        overworldStoneMat = BuildOverworldSurfaceMaterial(overworld.StoneTextureIndex, overworld.StoneMaterialOverride, new Color(0.45f, 0.45f, 0.45f), overworld.ChunkSizeSamples, overworld.ChunkSizeSamples);
        overworldSnowMat = BuildOverworldSurfaceMaterial(overworld.SnowTextureIndex, overworld.SnowMaterialOverride, Color.white, overworld.ChunkSizeSamples, overworld.ChunkSizeSamples);
        overworldSandMat = BuildOverworldSurfaceMaterial(overworld.SandTextureIndex, overworld.SandMaterialOverride, new Color(0.82f, 0.76f, 0.52f), overworld.ChunkSizeSamples, overworld.ChunkSizeSamples);
        overworldSwampMat = BuildOverworldSurfaceMaterial(overworld.SwampTextureIndex, overworld.SwampMaterialOverride, new Color(0.25f, 0.35f, 0.24f), overworld.ChunkSizeSamples, overworld.ChunkSizeSamples);
        if (overworld.AnimateWater)
        {
            int frameCount = Mathf.Max(1, (overworld.WaterTextureAnimEndIndex - overworld.WaterTextureIndex) + 1);
            overworldWaterFrames = new Texture2D[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                overworldWaterFrames[i] = LoadUW2TerrainTexture(overworld.WaterTextureIndex + i);
            }
            overworldWaterFrameIndex = 0;
            overworldWaterAnimTimer = 0f;
        }
        else
        {
            overworldWaterFrames = null;
        }

        overworld.OverworldStartPos = GetOverworldSpawnPosition(heightmap, overworld.TileWorldSize, Mathf.Max(1, overworld.TilesPerPixel), overworld.HeightScale, overworld.PerlinScale, overworld.PerlinStrength, overworld.OverworldStartTile.x, overworld.OverworldStartTile.y);

        int startTotalSeconds = Mathf.Max(0, (overworld.StartHour * 3600) + (overworld.StartMinute * 60) + overworld.StartSecond);
        int clock1 = startTotalSeconds % 255;
        int rem = startTotalSeconds / 255;
        int clock2 = rem % 255;
        int clock3 = rem / 255;
        GameClock.Clock0 = 0;
        GameClock.Clock1 = clock1;
        GameClock.Clock2 = clock2;
        GameClock.Clock3 = clock3;

        GameClock gc = FindObjectOfType<GameClock>();
        if (gc != null)
        {
            gc.clockRate = overworld.ClockRateSecondsPerGameSecond;
        }

        UWCharacter.Instance.playerController.enabled = false;
        UWCharacter.Instance.playerMotor.enabled = false;
        UWCharacter.Instance.transform.position = overworld.OverworldStartPos;
        if (UWCharacter.Instance.playerCam != null)
        {
            UWCharacter.Instance.playerCam.farClipPlane = overworld.OverworldFarClip;
        }

        lastPlayerChunk = GetPlayerChunkCoord(overworld, UWCharacter.Instance.transform.position);
        // Build the player's current chunk immediately so we never spawn over empty air.
        if (!loadedOverworldChunks.ContainsKey(lastPlayerChunk))
        {
            GameObject startChunk = BuildChunk(lastPlayerChunk, heightmap, overworld, 1, true, true);
            if (startChunk != null)
            {
                loadedOverworldChunks[lastPlayerChunk] = startChunk;
                lowDetailOverworldChunks.Remove(lastPlayerChunk);
                noNatureOverworldChunks.Remove(lastPlayerChunk);
            }
        }

        EnsureChunksAround(lastPlayerChunk, heightmap, overworld);
        if (overworld.LoadDistantChunks) { EnsureDistantChunks(lastPlayerChunk, heightmap, overworld); }
        StartOverworldChunkBuildWorker();
        UWCharacter.Instance.playerController.enabled = true;
        UWCharacter.Instance.playerMotor.enabled = true;
        overworldStreamingInitialized = true;

        Light sunLight = FindObjectOfType<Light>();
        if (sunLight == null || sunLight.type != LightType.Directional)
        {
            Light[] lights = FindObjectsOfType<Light>();
            sunLight = null;
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                {
                    sunLight = lights[i];
                    break;
                }
            }
        }

        if (sunLight == null)
        {
            GameObject sun = new GameObject("OverworldSun");
            sunLight = sun.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.color = new Color(1f, 0.97f, 0.9f);
            sunLight.intensity = 1.0f;
            sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }

        Material daySky = Resources.Load<Material>("DynamicSkies/Materials/BLBSkyboxMaterial");
        Material nightSky = Resources.Load<Material>("DynamicSkies/Materials/BLBSkyboxNoSunMaterial");
        if ((daySky != null) || (nightSky != null))
        {
            overworldSkyController = FindObjectOfType<OverworldSkyController>();
            if (overworldSkyController == null)
            {
                GameObject skyControllerObj = new GameObject("OverworldSkyController");
                overworldSkyController = skyControllerObj.AddComponent<OverworldSkyController>();
            }
            overworldSkyController.Initialize(daySky, nightSky, sunLight);
        }
    }

    private Vector2Int GetPlayerChunkCoord(OverworldTerrainController overworld, Vector3 worldPos)
    {
        float sampleX = worldPos.x / Mathf.Max(0.01f, overworld.TileWorldSize);
        float sampleY = worldPos.z / Mathf.Max(0.01f, overworld.TileWorldSize);
        int chunkX = Mathf.FloorToInt(sampleX / Mathf.Max(1, overworld.ChunkSizeSamples));
        int chunkY = Mathf.FloorToInt(sampleY / Mathf.Max(1, overworld.ChunkSizeSamples));
        return new Vector2Int(chunkX, chunkY);
    }

    private void EnsureAllChunks(Texture2D heightmap, OverworldTerrainController overworld)
    {
        int tilesPerPixel = Mathf.Max(1, overworld.TilesPerPixel);
        int chunkSize = Mathf.Max(2, overworld.ChunkSizeSamples);
        int totalSampleWidth = Mathf.Max(2, heightmap.width / tilesPerPixel);
        int totalSampleHeight = Mathf.Max(2, heightmap.height / tilesPerPixel);

        int maxChunkX = Mathf.CeilToInt(totalSampleWidth / (float)chunkSize) - 1;
        int maxChunkY = Mathf.CeilToInt(totalSampleHeight / (float)chunkSize) - 1;

        for (int cy = 0; cy <= maxChunkY; cy++)
        {
            for (int cx = 0; cx <= maxChunkX; cx++)
            {
                Vector2Int cc = new Vector2Int(cx, cy);
                if (!loadedOverworldChunks.ContainsKey(cc))
                {
                    EnqueueOverworldChunkBuild(cc, 1, true, true, false, false);
                }
            }
        }
        StartOverworldChunkBuildWorker();
    }

    private void UpdateOverworldTerrainType(OverworldTerrainController overworld)
    {
        if (UWCharacter.Instance == null) { return; }
        if (overworldTerrainTypeMap == null) { return; }

        int sampleX = Mathf.Clamp(Mathf.FloorToInt(UWCharacter.Instance.transform.position.x / Mathf.Max(0.01f, overworld.TileWorldSize)), 0, overworldTerrainMapWidth - 1);
        int sampleY = Mathf.Clamp(Mathf.FloorToInt(UWCharacter.Instance.transform.position.z / Mathf.Max(0.01f, overworld.TileWorldSize)), 0, overworldTerrainMapHeight - 1);

        int terrainNo = overworldTerrainTypeMap[sampleX, sampleY];
        if (terrainNo == (int)TerrainDatLoader.TerrainTypes.Unknown)
        {
            terrainNo = TerrainDatLoader.Normal;
        }

        UWCharacter.Instance.CurrentTerrain = terrainNo;
        UWCharacter.Instance.terrainType = TerrainDatLoader.getTerrain(terrainNo);
    }

    private void EnsureChunksAround(Vector2Int centerChunk, Texture2D heightmap, OverworldTerrainController overworld)
    {
        HashSet<Vector2Int> wanted = new HashSet<Vector2Int>();
        HashSet<Vector2Int> keepHighDetail = new HashSet<Vector2Int>();
        int unloadRadius = overworld.ActiveChunkRadius + Mathf.Max(0, overworld.HighDetailUnloadMargin);

        for (int y = -unloadRadius; y <= unloadRadius; y++)
        {
            for (int x = -unloadRadius; x <= unloadRadius; x++)
            {
                keepHighDetail.Add(new Vector2Int(centerChunk.x + x, centerChunk.y + y));
            }
        }

        for (int y = -overworld.ActiveChunkRadius; y <= overworld.ActiveChunkRadius; y++)
        {
            for (int x = -overworld.ActiveChunkRadius; x <= overworld.ActiveChunkRadius; x++)
            {
                Vector2Int cc = new Vector2Int(centerChunk.x + x, centerChunk.y + y);
                wanted.Add(cc);
                if (!loadedOverworldChunks.ContainsKey(cc))
                {
                    EnqueueOverworldChunkBuild(cc, 1, true, true, false, false);
                }
                else if (lowDetailOverworldChunks.Contains(cc) || noNatureOverworldChunks.Contains(cc))
                {
                    EnqueueOverworldChunkBuild(cc, 1, true, true, false, false);
                }
            }
        }

        List<Vector2Int> toRemove = new List<Vector2Int>();
        List<Vector2Int> existingChunkKeys = new List<Vector2Int>(loadedOverworldChunks.Keys);
        for (int i = 0; i < existingChunkKeys.Count; i++)
        {
            Vector2Int key = existingChunkKeys[i];
            if (!keepHighDetail.Contains(key) && !lowDetailOverworldChunks.Contains(key))
            {
                RecycleOverworldChunk(key);
                noNatureOverworldChunks.Remove(key);
                toRemove.Add(key);
            }
        }
        foreach (var k in toRemove) { loadedOverworldChunks.Remove(k); }
        StartOverworldChunkBuildWorker();
    }


    private OverworldNatureFlatsController GetOverworldNatureFlatsController()
    {
        return UnityEngine.Object.FindObjectOfType<OverworldNatureFlatsController>();
    }

    private void EnsureDistantChunks(Vector2Int centerChunk, Texture2D heightmap, OverworldTerrainController overworld)
    {
        int tilesPerPixel = Mathf.Max(1, overworld.TilesPerPixel);
        int chunkSize = Mathf.Max(2, overworld.ChunkSizeSamples);
        int totalSampleWidth = Mathf.Max(2, heightmap.width / tilesPerPixel);
        int totalSampleHeight = Mathf.Max(2, heightmap.height / tilesPerPixel);
        int maxChunkX = Mathf.CeilToInt(totalSampleWidth / (float)chunkSize) - 1;
        int maxChunkY = Mathf.CeilToInt(totalSampleHeight / (float)chunkSize) - 1;
        int activeRadius = Mathf.Max(0, overworld.ActiveChunkRadius);
        int transitionRadius = activeRadius + 1;
        int distantStep = Mathf.Max(2, overworld.DistantChunkStep);

        foreach (Vector2Int cc in EnumerateChunksByDistance(centerChunk, maxChunkX, maxChunkY))
        {
            if (Mathf.Abs(cc.x - centerChunk.x) <= activeRadius && Mathf.Abs(cc.y - centerChunk.y) <= activeRadius) { continue; }
            bool inTransitionBand = (Mathf.Abs(cc.x - centerChunk.x) <= transitionRadius) && (Mathf.Abs(cc.y - centerChunk.y) <= transitionRadius);
            int sampleStep = inTransitionBand ? 1 : distantStep;

            if (!loadedOverworldChunks.ContainsKey(cc))
            {
                EnqueueOverworldChunkBuild(cc, sampleStep, true, false, sampleStep > 1, true);
            }
            else if (lowDetailOverworldChunks.Contains(cc) && (sampleStep == 1))
            {
                EnqueueOverworldChunkBuild(cc, 1, true, false, false, true);
            }
        }
        StartOverworldChunkBuildWorker();
    }

    private IEnumerable<Vector2Int> EnumerateChunksByDistance(Vector2Int centerChunk, int maxChunkX, int maxChunkY)
    {
        int clampedCenterX = Mathf.Clamp(centerChunk.x, 0, maxChunkX);
        int clampedCenterY = Mathf.Clamp(centerChunk.y, 0, maxChunkY);
        int maxRadius = Mathf.Max(
            Mathf.Max(clampedCenterX, maxChunkX - clampedCenterX),
            Mathf.Max(clampedCenterY, maxChunkY - clampedCenterY));

        yield return new Vector2Int(clampedCenterX, clampedCenterY);
        for (int r = 1; r <= maxRadius; r++)
        {
            int minX = clampedCenterX - r;
            int maxX = clampedCenterX + r;
            int minY = clampedCenterY - r;
            int maxY = clampedCenterY + r;

            for (int x = minX; x <= maxX; x++)
            {
                if (x >= 0 && x <= maxChunkX)
                {
                    if (minY >= 0 && minY <= maxChunkY) { yield return new Vector2Int(x, minY); }
                    if (maxY >= 0 && maxY <= maxChunkY && maxY != minY) { yield return new Vector2Int(x, maxY); }
                }
            }
            for (int y = minY + 1; y <= maxY - 1; y++)
            {
                if (y >= 0 && y <= maxChunkY)
                {
                    if (minX >= 0 && minX <= maxChunkX) { yield return new Vector2Int(minX, y); }
                    if (maxX >= 0 && maxX <= maxChunkX && maxX != minX) { yield return new Vector2Int(maxX, y); }
                }
            }
        }
    }

    private void EnqueueOverworldChunkBuild(Vector2Int coord, int sampleStep, bool withCollision, bool withNatureBillboards, bool lowDetail, bool noNature)
    {
        OverworldChunkBuildRequest req = new OverworldChunkBuildRequest
        {
            chunkCoord = coord,
            sampleStep = sampleStep,
            withCollision = withCollision,
            withNatureBillboards = withNatureBillboards,
            lowDetail = lowDetail,
            noNature = noNature
        };
        pendingOverworldChunkRequests[coord] = req;
        if (queuedOverworldChunks.Add(coord))
        {
            overworldChunkBuildQueue.Enqueue(req);
        }
    }

    private void StartOverworldChunkBuildWorker()
    {
        if (overworldChunkBuildCoroutine == null)
        {
            overworldChunkBuildCoroutine = StartCoroutine(ProcessOverworldChunkBuildQueue());
        }
    }

    private IEnumerator ProcessOverworldChunkBuildQueue()
    {
        while (overworldChunkBuildQueue.Count > 0)
        {
            float frameBudget = 0.004f;
            float start = Time.realtimeSinceStartup;
            OverworldTerrainController overworld = GetOverworldController();
            while (overworldChunkBuildQueue.Count > 0 && (Time.realtimeSinceStartup - start) < frameBudget)
            {
                var req = overworldChunkBuildQueue.Dequeue();
                queuedOverworldChunks.Remove(req.chunkCoord);
                if (!pendingOverworldChunkRequests.ContainsKey(req.chunkCoord)) { continue; }
                if (pendingOverworldChunkRequests[req.chunkCoord].sampleStep != req.sampleStep) { continue; }
                pendingOverworldChunkRequests.Remove(req.chunkCoord);
                GameObject chunk = BuildChunk(req.chunkCoord, cachedOverworldHeightmap, overworld, req.sampleStep, req.withCollision, req.withNatureBillboards);
                if (chunk != null)
                {
                    if (loadedOverworldChunks.ContainsKey(req.chunkCoord))
                    {
                        GameObject oldChunk = loadedOverworldChunks[req.chunkCoord];
                        if ((oldChunk != null) && (oldChunk != chunk))
                        {
                            oldChunk.SetActive(false);
                            oldChunk.transform.SetParent(OverworldTerrainRoot != null ? OverworldTerrainRoot.transform : null, false);
                            overworldChunkPool.Push(oldChunk);
                        }
                    }
                    loadedOverworldChunks[req.chunkCoord] = chunk;
                    if (req.lowDetail) { lowDetailOverworldChunks.Add(req.chunkCoord); } else { lowDetailOverworldChunks.Remove(req.chunkCoord); }
                    if (req.noNature) { noNatureOverworldChunks.Add(req.chunkCoord); } else { noNatureOverworldChunks.Remove(req.chunkCoord); }
                }
            }
            yield return null;
        }
        overworldChunkBuildCoroutine = null;
    }

    private void RecycleOverworldChunk(Vector2Int coord)
    {
        if (!loadedOverworldChunks.ContainsKey(coord)) { return; }
        GameObject go = loadedOverworldChunks[coord];
        loadedOverworldChunks.Remove(coord);
        pendingOverworldChunkRequests.Remove(coord);
        if (go == null) { return; }
        ReleaseOverworldRuntimeMaterials(go);
        go.SetActive(false);
        go.transform.SetParent(OverworldTerrainRoot != null ? OverworldTerrainRoot.transform : null, false);
        overworldChunkPool.Push(go);
    }
    private void ReleaseOverworldRuntimeMaterials(GameObject go)
    {
        if (go == null) { return; }
        OverworldChunkRuntimeTextures rt = go.GetComponent<OverworldChunkRuntimeTextures>();
        if (rt != null) { rt.ReleaseAll(); }
    }

    private GameObject BuildChunk(Vector2Int chunkCoord, Texture2D heightmap, OverworldTerrainController overworld, int sampleStep = 1, bool withCollision = true, bool withNatureBillboards = true)
    {
        PrepareTerrainClassificationContext(overworld);
        int tilesPerPixel = Mathf.Max(1, overworld.TilesPerPixel);
        int chunkSize = Mathf.Max(2, overworld.ChunkSizeSamples);
        int totalSampleWidth = Mathf.Max(2, heightmap.width / tilesPerPixel);
        int totalSampleHeight = Mathf.Max(2, heightmap.height / tilesPerPixel);

        int startX = chunkCoord.x * chunkSize;
        int startY = chunkCoord.y * chunkSize;
        int endX = Mathf.Min(totalSampleWidth - 1, startX + chunkSize);
        int endY = Mathf.Min(totalSampleHeight - 1, startY + chunkSize);
        if ((endX - startX) < 1 || (endY - startY) < 1) { return null; }

        int baseSampleStep = Mathf.Max(1, sampleStep);
        int decimationStep = Mathf.Max(1, overworld.TerrainDecimationStep);
        int meshSampleStep = baseSampleStep; // keep high-resolution mesh topology
        int geometrySampleStep = Mathf.Max(1, baseSampleStep * decimationStep); // snap heights to coarse sampling
        int sampleWidth = ((endX - startX) / meshSampleStep) + 1;
        int sampleHeight = ((endY - startY) / meshSampleStep) + 1;

        Vector3[] vertices = new Vector3[sampleWidth * sampleHeight];
        Vector2[] uvs = new Vector2[sampleWidth * sampleHeight];
        int[] triangles = new int[(sampleWidth - 1) * (sampleHeight - 1) * 6];
        int[] terrainClassByVertex = new int[sampleWidth * sampleHeight]; //0=water,1=grass,2=stone,3=snow

        int fullSampleWidth = ((endX - startX) / baseSampleStep) + 1;
        int fullSampleHeight = ((endY - startY) / baseSampleStep) + 1;
        int[] terrainClassFull = new int[fullSampleWidth * fullSampleHeight];

        for (int fz = 0; fz < fullSampleHeight; fz++)
        {
            for (int fx = 0; fx < fullSampleWidth; fx++)
            {
                int fullGlobalX = Mathf.Min(endX, startX + (fx * baseSampleStep));
                int fullGlobalZ = Mathf.Min(endY, startY + (fz * baseSampleStep));
                int fullPx = Mathf.Clamp(fullGlobalX * tilesPerPixel, 0, heightmap.width - 1);
                int fullPz = Mathf.Clamp(fullGlobalZ * tilesPerPixel, 0, heightmap.height - 1);
                float fullElevation = SampleSmoothedHeight(heightmap, fullPx, fullPz);
                float fullShapedElevation = Mathf.Pow(fullElevation, 1.65f);
                float fullNoise = (Mathf.PerlinNoise((fullGlobalX + 101.231f) * overworld.PerlinScale, (fullGlobalZ + 77.777f) * overworld.PerlinScale) * 2f) - 1f;
                float fullPerlinDisplacement = fullNoise * overworld.PerlinStrength * Mathf.Max(1f, overworld.HeightScale * 0.2f);
                float fullY = fullShapedElevation * overworld.HeightScale + fullPerlinDisplacement - overworld.SeaLevelOffset;
                if (fullY < 0f) { fullY = 0f; }

                int fullIndex = (fz * fullSampleWidth) + fx;
                terrainClassFull[fullIndex] = ClassifyOverworldTerrainSample(fullY, fullGlobalX, fullGlobalZ, fullPx, fullPz, tilesPerPixel, heightmap, overworld);
            }
        }

        for (int z = 0; z < sampleHeight; z++)
        {
            for (int x = 0; x < sampleWidth; x++)
            {
                int index = z * sampleWidth + x;
                int globalX = Mathf.Min(endX, startX + (x * meshSampleStep));
                int globalZ = Mathf.Min(endY, startY + (z * meshSampleStep));

                int localX = globalX - startX;
                int localZ = globalZ - startY;
                int baseX = (localX / geometrySampleStep) * geometrySampleStep;
                int baseZ = (localZ / geometrySampleStep) * geometrySampleStep;
                int nextX = Mathf.Min(baseX + geometrySampleStep, endX - startX);
                int nextZ = Mathf.Min(baseZ + geometrySampleStep, endY - startY);

                int gx0 = startX + baseX;
                int gz0 = startY + baseZ;
                int gx1 = startX + nextX;
                int gz1 = startY + nextZ;

                float tx = (nextX == baseX) ? 0f : (localX - baseX) / (float)(nextX - baseX);
                float tz = (nextZ == baseZ) ? 0f : (localZ - baseZ) / (float)(nextZ - baseZ);

                float y00 = SampleTerrainHeightAt(gx0, gz0, tilesPerPixel, heightmap, overworld);
                float y10 = SampleTerrainHeightAt(gx1, gz0, tilesPerPixel, heightmap, overworld);
                float y01 = SampleTerrainHeightAt(gx0, gz1, tilesPerPixel, heightmap, overworld);
                float y11 = SampleTerrainHeightAt(gx1, gz1, tilesPerPixel, heightmap, overworld);
                float y0 = Mathf.Lerp(y00, y10, tx);
                float y1 = Mathf.Lerp(y01, y11, tx);
                float y = Mathf.Lerp(y0, y1, tz);
                if (y < 0f) { y = 0f; }

                if ((globalX >= 0) && (globalX < overworldTerrainMapWidth) && (globalZ >= 0) && (globalZ < overworldTerrainMapHeight))
                {
                    int px = Mathf.Clamp(globalX * tilesPerPixel, 0, heightmap.width - 1);
                    int pz = Mathf.Clamp(globalZ * tilesPerPixel, 0, heightmap.height - 1);
                    if (y <= overworld.WaterSurfaceEpsilon)
                    {
                        int terrainType = TerrainDatLoader.Water;
                        float hE = SampleSmoothedHeight(heightmap, Mathf.Clamp(px + tilesPerPixel, 0, heightmap.width - 1), pz);
                        float hW = SampleSmoothedHeight(heightmap, Mathf.Clamp(px - tilesPerPixel, 0, heightmap.width - 1), pz);
                        float hN = SampleSmoothedHeight(heightmap, px, Mathf.Clamp(pz + tilesPerPixel, 0, heightmap.height - 1));
                        float hS = SampleSmoothedHeight(heightmap, px, Mathf.Clamp(pz - tilesPerPixel, 0, heightmap.height - 1));
                        float dx = hE - hW;
                        float dz = hN - hS;

                        if (Mathf.Abs(dx) > 0.015f || Mathf.Abs(dz) > 0.015f)
                        {
                            if (Mathf.Abs(dx) > Mathf.Abs(dz))
                            {
                                terrainType = (dx > 0f) ? TerrainDatLoader.WaterFlowWest : TerrainDatLoader.WaterFlowEast;
                            }
                            else
                            {
                                terrainType = (dz > 0f) ? TerrainDatLoader.WaterFlowSouth : TerrainDatLoader.WaterFlowNorth;
                            }
                        }

                        overworldTerrainTypeMap[globalX, globalZ] = terrainType;
                    }
                    else
                    {
                        overworldTerrainTypeMap[globalX, globalZ] = TerrainDatLoader.Normal;
                    }
                }

                vertices[index] = new Vector3(globalX * overworld.TileWorldSize, y, globalZ * overworld.TileWorldSize);
                uvs[index] = new Vector2(x / (float)(sampleWidth - 1), z / (float)(sampleHeight - 1));
                int fullX = Mathf.Clamp((globalX - startX) / baseSampleStep, 0, fullSampleWidth - 1);
                int fullZ = Mathf.Clamp((globalZ - startY) / baseSampleStep, 0, fullSampleHeight - 1);
                terrainClassByVertex[index] = terrainClassFull[(fullZ * fullSampleWidth) + fullX];

                if ((x < sampleWidth - 1) && (z < sampleHeight - 1))
                {
                    int bl = index; int br = index + 1; int tl = index + sampleWidth; int tr = index + sampleWidth + 1;
                    triangles[0 + (6 * ((z * (sampleWidth - 1)) + x))] = bl;
                    triangles[1 + (6 * ((z * (sampleWidth - 1)) + x))] = tl;
                    triangles[2 + (6 * ((z * (sampleWidth - 1)) + x))] = tr;
                    triangles[3 + (6 * ((z * (sampleWidth - 1)) + x))] = bl;
                    triangles[4 + (6 * ((z * (sampleWidth - 1)) + x))] = tr;
                    triangles[5 + (6 * ((z * (sampleWidth - 1)) + x))] = br;
                }
            }
        }

        Mesh mesh = new Mesh();
        mesh.indexFormat = (vertices.Length > 65535) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        int[] activeLandClasses = new int[] { 1, 2, 3, 5, 6 };
        int subMeshCount = 1 + activeLandClasses.Length; // water + N land classes
        List<int>[] triLists = new List<int>[subMeshCount];
        for (int i = 0; i < subMeshCount; i++) { triLists[i] = new List<int>(); }
        for (int z = 0; z < sampleHeight - 1; z++)
        {
            for (int x = 0; x < sampleWidth - 1; x++)
            {
                int bl = (z * sampleWidth) + x;
                int br = bl + 1;
                int tl = bl + sampleWidth;
                int tr = tl + 1;

                // Pure full-resolution classification: classify each coarse triangle separately by
                // sampling the underlying full-resolution class grid and splitting by triangle half.
                int fullStartX = Mathf.Clamp((x * meshSampleStep) / baseSampleStep, 0, fullSampleWidth - 1);
                int fullStartZ = Mathf.Clamp((z * meshSampleStep) / baseSampleStep, 0, fullSampleHeight - 1);
                int fullEndX = Mathf.Clamp(((x + 1) * meshSampleStep) / baseSampleStep, 0, fullSampleWidth - 1);
                int fullEndZ = Mathf.Clamp(((z + 1) * meshSampleStep) / baseSampleStep, 0, fullSampleHeight - 1);
                int[] tri0Counts = new int[8];
                int[] tri1Counts = new int[8];
                int fullWidth = Mathf.Max(1, fullEndX - fullStartX);
                int fullHeight = Mathf.Max(1, fullEndZ - fullStartZ);
                for (int fz = fullStartZ; fz <= fullEndZ; fz++)
                {
                    float nz = (fz - fullStartZ) / (float)fullHeight;
                    for (int fx = fullStartX; fx <= fullEndX; fx++)
                    {
                        float nx = (fx - fullStartX) / (float)fullWidth;
                        int c = terrainClassFull[(fz * fullSampleWidth) + fx];
                        bool inTri0 = nx >= nz; // (bl,tr,br) half
                        if (inTri0)
                        {
                            tri0Counts[Mathf.Clamp(c, 0, 7)]++;
                        }
                        else
                        {
                            tri1Counts[Mathf.Clamp(c, 0, 7)]++;
                        }
                    }
                }

                int tri0Class = DominantClass(tri0Counts, activeLandClasses);
                int tri1Class = DominantClass(tri1Counts, activeLandClasses);

                // First-principles quad classification from corner classes.
                bool cornerBLWater = terrainClassByVertex[bl] == 0;
                bool cornerBRWater = terrainClassByVertex[br] == 0;
                bool cornerTLWater = terrainClassByVertex[tl] == 0;
                bool cornerTRWater = terrainClassByVertex[tr] == 0;
                int cornerWaterCount = (cornerBLWater ? 1 : 0) + (cornerBRWater ? 1 : 0) + (cornerTLWater ? 1 : 0) + (cornerTRWater ? 1 : 0);

                bool clampQuadToWaterPlane = false;

                if (cornerWaterCount >= 3)
                {
                    // Full water tile.
                    tri0Class = 0;
                    tri1Class = 0;
                    clampQuadToWaterPlane = true;
                }
                else if (cornerWaterCount > 0)
                {
                    // Shoreline transition tile: choose shoreline land class from non-water corners,
                    // not broad triangle counts, to avoid grass islands and class mismatches at complex shorelines.
                    int shorelineClass = 1;
                    int[] corners = { terrainClassByVertex[bl], terrainClassByVertex[br], terrainClassByVertex[tl], terrainClassByVertex[tr] };
                    for (int i = 0; i < corners.Length; i++)
                    {
                        int c = corners[i];
                        if (c == 0) { continue; }
                        if (c == 6) { shorelineClass = 6; break; } // Prefer sand coastlines where present.
                        if ((c == 5) && shorelineClass != 6) { shorelineClass = 5; continue; }
                        if ((c == 3) && shorelineClass != 6) { shorelineClass = 3; continue; }
                        if ((c == 2) && shorelineClass == 1) { shorelineClass = 2; }
                    }

                    tri0Class = shorelineClass;
                    tri1Class = shorelineClass;
                    clampQuadToWaterPlane = true;
                }

                bool onChunkBorder = (x == 0) || (z == 0) || (x == sampleWidth - 2) || (z == sampleHeight - 2);
                if (onChunkBorder)
                {
                    // Keep shoreline transitions visible at chunk edges.
                    // Border quads with water should be clamped flat, but not forcibly reclassified to full water,
                    // otherwise transition tiles cannot render on the land/transition materials.
                    bool hasAnyWaterSample = (tri0Counts[0] > 0) || (tri1Counts[0] > 0) || (cornerWaterCount > 0);
                    if (hasAnyWaterSample)
                    {
                        clampQuadToWaterPlane = true;
                    }
                }

                if (clampQuadToWaterPlane || (tri0Class == 0))
                {
                    vertices[bl].y = 0f;
                    vertices[tr].y = 0f;
                    vertices[br].y = 0f;
                }
                if (clampQuadToWaterPlane || (tri1Class == 0))
                {
                    vertices[bl].y = 0f;
                    vertices[tl].y = 0f;
                    vertices[tr].y = 0f;
                }

                AddTriangleToClass(bl, tr, br, tri0Class, triLists, activeLandClasses);
                AddTriangleToClass(bl, tl, tr, tri1Class, triLists, activeLandClasses);
            }
        }
        mesh.vertices = vertices;
        mesh.subMeshCount = subMeshCount;
        for (int sm = 0; sm < subMeshCount; sm++) { mesh.SetTriangles(triLists[sm], sm); }

        GameObject go = (overworldChunkPool.Count > 0) ? overworldChunkPool.Pop() : new GameObject();
        go.name = $"OverworldTerrain_{chunkCoord.x}_{chunkCoord.y}";
        go.SetActive(true);
        go.transform.SetParent(OverworldTerrainRoot.transform, true);
        for (int c = go.transform.childCount - 1; c >= 0; c--) { Destroy(go.transform.GetChild(c).gameObject); }
        MeshFilter mf = go.GetComponent<MeshFilter>();
        if (mf == null) { mf = go.AddComponent<MeshFilter>(); }
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mr == null) { mr = go.AddComponent<MeshRenderer>(); }
        mf.sharedMesh = mesh;
        if (withCollision)
        {
            MeshCollider mc = go.GetComponent<MeshCollider>();
            if (mc == null) { mc = go.AddComponent<MeshCollider>(); }
            mc.sharedMesh = mesh;
        }
        else
        {
            MeshCollider mc = go.GetComponent<MeshCollider>();
            if (mc != null) { mc.sharedMesh = null; Destroy(mc); }
        }
        if (overworld.UseTransitionTileTexturing && withCollision && (sampleStep <= 1))
        {
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            Texture2D waterBase = (overworldWaterMat != null) ? (overworldWaterMat.mainTexture as Texture2D) : null;
            Texture2D grassBase = (overworldGrassMat != null) ? (overworldGrassMat.mainTexture as Texture2D) : null;
            Texture2D stoneBase = (overworldStoneMat != null) ? (overworldStoneMat.mainTexture as Texture2D) : null;
            Texture2D snowBase = (overworldSnowMat != null) ? (overworldSnowMat.mainTexture as Texture2D) : null;
            Texture2D sandBase = (overworld.SandMaterialOverride != null) ? (overworld.SandMaterialOverride.mainTexture as Texture2D) : MaybeEnableOverworldMipmaps(LoadUW2TerrainTexture(overworld.SandTextureIndex), overworld);
            Texture2D swampBase = (overworld.SwampMaterialOverride != null) ? (overworld.SwampMaterialOverride.mainTexture as Texture2D) : MaybeEnableOverworldMipmaps(LoadUW2TerrainTexture(overworld.SwampTextureIndex), overworld);
            OverworldTerrainTexturing.BuildStats stats;
            int texWidth = fullSampleWidth + 2;
            int texHeight = fullSampleHeight + 2;
            int[] terrainClassExpanded = new int[texWidth * texHeight];
            for (int ez = 0; ez < texHeight; ez++)
            {
                for (int ex = 0; ex < texWidth; ex++)
                {
                    int fullGlobalX = Mathf.Clamp(startX + ((ex - 1) * baseSampleStep), 0, totalSampleWidth - 1);
                    int fullGlobalZ = Mathf.Clamp(startY + ((ez - 1) * baseSampleStep), 0, totalSampleHeight - 1);
                    int fullPx = Mathf.Clamp(fullGlobalX * tilesPerPixel, 0, heightmap.width - 1);
                    int fullPz = Mathf.Clamp(fullGlobalZ * tilesPerPixel, 0, heightmap.height - 1);
                    float fullElevation = SampleSmoothedHeight(heightmap, fullPx, fullPz);
                    float fullShapedElevation = Mathf.Pow(fullElevation, 1.65f);
                    float fullNoise = (Mathf.PerlinNoise((fullGlobalX + 101.231f) * overworld.PerlinScale, (fullGlobalZ + 77.777f) * overworld.PerlinScale) * 2f) - 1f;
                    float fullPerlinDisplacement = fullNoise * overworld.PerlinStrength * Mathf.Max(1f, overworld.HeightScale * 0.2f);
                    float fullY = fullShapedElevation * overworld.HeightScale + fullPerlinDisplacement - overworld.SeaLevelOffset;
                    if (fullY < 0f) { fullY = 0f; }
                    int idx = ez * texWidth + ex;
                    terrainClassExpanded[idx] = ClassifyOverworldTerrainSample(fullY, fullGlobalX, fullGlobalZ, fullPx, fullPz, tilesPerPixel, heightmap, overworld);
                }
            }

            OverworldTerrainTexturing.TileAtlasBuild atlasBuild = OverworldTerrainTexturing.BuildChunkTransitionAtlas(
                terrainClassExpanded,
                texWidth,
                texHeight,
                overworld.TransitionTilesFolder,
                waterBase,
                grassBase,
                stoneBase,
                snowBase,
                swampBase,
                sandBase,
                out stats,
                1);
            int diagUvEdgeVertexCount = 0;
            for (int vi = 0; vi < uvs.Length; vi++)
            {
                float ux = uvs[vi].x; float uz = uvs[vi].y;
                if (ux <= 0f || ux >= 1f || uz <= 0f || uz >= 1f) { diagUvEdgeVertexCount++; }
            }

            int diagClampQuadCount = 0;
            int diagMeshWaterQuadCount = 0;
            int diagAtlasWaterQuadCount = 0;
            int diagMeshAtlasDisagreeCount = 0;

            if (atlasBuild.tileIdMap != null && atlasBuild.atlasTexture != null)
            {
                if (atlasBuild.clampMask != null)
                {
                    for (int tz = 0; tz < sampleHeight - 1; tz++)
                    {
                        for (int tx = 0; tx < sampleWidth - 1; tx++)
                        {
                            bool atlasClamp = atlasBuild.clampMask[(tz * (sampleWidth - 1)) + tx];
                            if (atlasClamp) { diagClampQuadCount++; }
                            int blq = (tz * sampleWidth) + tx;
                            int brq = blq + 1;
                            int tlq = blq + sampleWidth;
                            int trq = tlq + 1;
                            bool meshWater = (terrainClassByVertex[blq] == 0) || (terrainClassByVertex[brq] == 0) || (terrainClassByVertex[tlq] == 0) || (terrainClassByVertex[trq] == 0);
                            bool atlasWater = atlasClamp;
                            if (meshWater) { diagMeshWaterQuadCount++; }
                            if (atlasWater) { diagAtlasWaterQuadCount++; }
                            if (meshWater != atlasWater) { diagMeshAtlasDisagreeCount++; }
                            if (!atlasClamp) { continue; }
                            int bl = (tz * sampleWidth) + tx;
                            int br = bl + 1;
                            int tl = bl + sampleWidth;
                            int tr = tl + 1;
                            vertices[bl].y = 0f;
                            vertices[br].y = 0f;
                            vertices[tl].y = 0f;
                            vertices[tr].y = 0f;
                        }
                    }
                    mesh.vertices = vertices;
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                }

                atlasBuild.tileIdMap.name = $"OWChunkTileIds_{chunkCoord.x}_{chunkCoord.y}";
                atlasBuild.atlasTexture.name = $"OWChunkAtlas_{chunkCoord.x}_{chunkCoord.y}";
                OverworldChunkRuntimeTextures rt = go.GetComponent<OverworldChunkRuntimeTextures>();
                if (rt == null) { rt = go.AddComponent<OverworldChunkRuntimeTextures>(); }
                rt.EnsureMaterials(overworldGrassMat, overworldStoneMat, overworldSnowMat, overworldSwampMat, overworldSandMat);
                rt.SetTransitionAtlas(atlasBuild);
                Material[] landMats = rt.landRuntimeMats;
                mr.materials = new Material[]
                {
                    overworldWaterMat,
                    (landMats != null && landMats.Length > 0) ? landMats[0] : overworldGrassMat, // grass
                    (landMats != null && landMats.Length > 1) ? landMats[1] : overworldStoneMat, // stone
                    (landMats != null && landMats.Length > 2) ? landMats[2] : overworldSnowMat,  // snow
                    (landMats != null && landMats.Length > 3) ? landMats[3] : overworldSwampMat, // swamp
                    (landMats != null && landMats.Length > 4) ? landMats[4] : overworldSandMat,  // sand
                };
            }
            else
            {
                mr.materials = BuildChunkMaterials(overworldGrassMat, overworldStoneMat, overworldSnowMat, overworldSwampMat, overworldSandMat);
            }
            sw.Stop();
            if (overworld.TransitionTexturingDiagnostics && (((chunkCoord.x + chunkCoord.y) % Mathf.Max(1, overworld.TransitionDiagLogEveryNChunks)) == 0))
            {
                UnityEngine.Debug.Log($"OverworldTransitionTexture chunk={chunkCoord} ms={sw.ElapsedMilliseconds} tiles={stats.tileCount} transitions={stats.transitionTiles} fallback={stats.fallbackCenterTiles} missing={stats.missingTransitionFiles} atlasTiles={stats.uniqueAtlasTiles} tileIdRange={stats.minTileId}-{stats.maxTileId} firstTile={stats.firstTileWidth}x{stats.firstTileHeight} minTile={stats.minTileWidth}x{stats.minTileHeight} maxTile={stats.maxTileWidth}x{stats.maxTileHeight} canonicalTile={stats.canonicalTileSize} waterCenter={stats.waterCenterTiles} waterTarget={stats.waterTargetTiles} uvEdgeVerts={diagUvEdgeVertexCount} clampQuads={diagClampQuadCount} meshWaterQuads={diagMeshWaterQuadCount} atlasWaterQuads={diagAtlasWaterQuadCount} meshAtlasDisagree={diagMeshAtlasDisagreeCount}");
            }
        }
        else
        {
            mr.materials = BuildChunkMaterials(overworldGrassMat, overworldStoneMat, overworldSnowMat, overworldSwampMat, overworldSandMat);
        }

        OverworldNatureFlatsController natureFlats = GetOverworldNatureFlatsController();
        if (withCollision && withNatureBillboards && (natureFlats != null) && natureFlats.EnableNatureFlats)
        {
            GameObject natureBillboards = new GameObject("NatureBillboards");
            natureBillboards.transform.SetParent(go.transform, false);
            OverworldNatureBillboardBatch batch = natureBillboards.AddComponent<OverworldNatureBillboardBatch>();
            batch.Initialize(vertices, triLists[1].ToArray(), natureFlats, overworld.WaterSurfaceEpsilon, chunkCoord);
        }

        if (geometrySampleStep > 1)
        {
            AddDistantChunkSkirt(go.transform, vertices, terrainClassByVertex, sampleWidth, sampleHeight, Mathf.Max(2f, geometrySampleStep * overworld.TileWorldSize * 0.35f) * 5f, overworld.TileWorldSize);
        }
        if (withCollision)
        {
            GameObject waterContact = new GameObject("WaterContact");
            waterContact.transform.SetParent(go.transform, false);
            float chunkWorldWidth = (sampleWidth - 1) * overworld.TileWorldSize * meshSampleStep;
            float chunkWorldHeight = (sampleHeight - 1) * overworld.TileWorldSize * meshSampleStep;
            waterContact.transform.position = new Vector3(
                startX * overworld.TileWorldSize + (chunkWorldWidth * 0.5f),
                0f,
                startY * overworld.TileWorldSize + (chunkWorldHeight * 0.5f));

            BoxCollider waterCol = waterContact.AddComponent<BoxCollider>();
            waterCol.size = new Vector3(chunkWorldWidth, 0.5f, chunkWorldHeight);
            waterCol.center = Vector3.zero;

            waterContact.layer = LayerMask.NameToLayer("Water");
            if ((_RES == GAME_UW2) && (overworld.WaterTextureIndex == 193))
            {
                waterContact.AddComponent<TileContactMud>();
            }
            else
            {
                waterContact.AddComponent<TileContactWater>();
            }
        }

        return go;
    }

    private void AddDistantChunkSkirt(Transform parent, Vector3[] vertices, int[] terrainClassByVertex, int sampleWidth, int sampleHeight, float skirtDepth, float tileWorldSize)
    {
        if (vertices == null || vertices.Length == 0) { return; }
        List<Vector3> skirtVerts = new List<Vector3>();
        List<Vector2> skirtUvs = new List<Vector2>();
        int[] activeLandClasses = new int[] { 1, 2, 3, 5, 6 };
        int subMeshCount = 1 + activeLandClasses.Length;
        List<int>[] triLists = new List<int>[subMeshCount];
        for (int i = 0; i < subMeshCount; i++) { triLists[i] = new List<int>(); }

        void AddEdge(int a, int b)
        {
            int baseIndex = skirtVerts.Count;
            Vector3 va = vertices[a];
            Vector3 vb = vertices[b];
            skirtVerts.Add(va);
            skirtVerts.Add(vb);
            // Vertical skirt (90 degrees down from the terrain edge).
            Vector3 verticalOffset = Vector3.down * skirtDepth;
            skirtVerts.Add(va + verticalOffset);
            skirtVerts.Add(vb + verticalOffset);
            int ax = a % sampleWidth; int az = a / sampleWidth;
            int bx = b % sampleWidth; int bz = b / sampleWidth;
            // Skirt UVs need to match terrain material texel density; terrain is effectively 64x larger in UV world scale.
            float skirtUvTileSize = Mathf.Max(0.01f, tileWorldSize * 64f);
            float edgeU0 = (Mathf.Abs(va.x - vb.x) > Mathf.Abs(va.z - vb.z))
                ? (Mathf.Min(va.x, vb.x) / skirtUvTileSize)
                : (Mathf.Min(va.z, vb.z) / skirtUvTileSize);
            float edgeU1 = edgeU0 + (Vector3.Distance(new Vector3(va.x, 0f, va.z), new Vector3(vb.x, 0f, vb.z)) / skirtUvTileSize);
            float topV0 = va.y / skirtUvTileSize;
            float topV1 = vb.y / skirtUvTileSize;
            float bottomV0 = (va.y - skirtDepth) / skirtUvTileSize;
            float bottomV1 = (vb.y - skirtDepth) / skirtUvTileSize;
            // World-space UV mapping to keep square texels on vertical skirts.
            // Rotate skirt UVs 90 degrees clockwise to align with terrain texture orientation.
            skirtUvs.Add(new Vector2(topV0, -edgeU0));
            skirtUvs.Add(new Vector2(topV1, -edgeU1));
            skirtUvs.Add(new Vector2(bottomV0, -edgeU0));
            skirtUvs.Add(new Vector2(bottomV1, -edgeU1));
            int edgeClass = 1;
            if (terrainClassByVertex != null && terrainClassByVertex.Length > Mathf.Max(a, b))
            {
                edgeClass = (terrainClassByVertex[a] == terrainClassByVertex[b]) ? terrainClassByVertex[a] : terrainClassByVertex[a];
            }
            int sm = SubmeshIndexForClass(edgeClass, activeLandClasses);
            List<int> target = triLists[sm];
            // Wind outward from chunk to keep normals facing away from terrain edge.
            target.Add(baseIndex + 0); target.Add(baseIndex + 1); target.Add(baseIndex + 2);
            target.Add(baseIndex + 1); target.Add(baseIndex + 3); target.Add(baseIndex + 2);
        }

        for (int x = 0; x < sampleWidth - 1; x++) { AddEdge(x, x + 1); } // north edge
        for (int x = 0; x < sampleWidth - 1; x++) { int z = sampleHeight - 1; AddEdge(z * sampleWidth + x + 1, z * sampleWidth + x); } // south edge
        for (int z = 0; z < sampleHeight - 1; z++) { AddEdge((z + 1) * sampleWidth, z * sampleWidth); } // west edge
        for (int z = 0; z < sampleHeight - 1; z++) { int x = sampleWidth - 1; AddEdge(z * sampleWidth + x, (z + 1) * sampleWidth + x); } // east edge

        if (skirtVerts.Count == 0) { return; }
        Mesh skirtMesh = new Mesh();
        skirtMesh.indexFormat = (skirtVerts.Count > 65535) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        skirtMesh.SetVertices(skirtVerts);
        skirtMesh.SetUVs(0, skirtUvs);
        skirtMesh.subMeshCount = subMeshCount;
        for (int sm = 0; sm < subMeshCount; sm++) { skirtMesh.SetTriangles(triLists[sm], sm); }
        OverworldTerrainController overworld = GetOverworldController();
        skirtMesh.RecalculateNormals();
        if (overworld.SkirtUseUpwardNormals)
        {
            // Mitigate dark seam shading by blending computed normals toward upward normals.
            // A blend of 0 keeps natural lighting; 1 fully forces upward normals.
            Vector3[] skirtNormals = skirtMesh.normals;
            float upBlend = Mathf.Clamp01(overworld.SkirtUpwardNormalBlend);
            for (int i = 0; i < skirtNormals.Length; i++)
            {
                skirtNormals[i] = Vector3.Lerp(skirtNormals[i], Vector3.up, upBlend).normalized;
            }
            skirtMesh.normals = skirtNormals;
        }
        skirtMesh.RecalculateBounds();

        GameObject skirt = new GameObject("ChunkSkirt");
        skirt.transform.SetParent(parent, false);
        MeshFilter mf = skirt.AddComponent<MeshFilter>();
        MeshRenderer mr = skirt.AddComponent<MeshRenderer>();
        mf.sharedMesh = skirtMesh;
        mr.sharedMaterials = BuildChunkMaterials(overworldGrassMat, overworldStoneMat, overworldSnowMat, overworldSwampMat, overworldSandMat);
        mr.shadowCastingMode = overworld.SkirtCastShadows
            ? UnityEngine.Rendering.ShadowCastingMode.On
            : UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = overworld.SkirtReceiveShadows;
    }


    private static void AddTriangleToClass(int i0, int i1, int i2, int terrainClass, List<int>[] triLists, int[] activeLandClasses)
    {
        int sm = SubmeshIndexForClass(terrainClass, activeLandClasses);
        triLists[sm].Add(i0); triLists[sm].Add(i1); triLists[sm].Add(i2);
    }

    private static int DominantClass(int[] counts, int[] activeLandClasses)
    {
        int waterCount = SafeCount(counts, 0);

        int landMax = 0;
        for (int i = 0; i < activeLandClasses.Length; i++)
        {
            int c = activeLandClasses[i];
            int ct = counts[Mathf.Clamp(c, 0, counts.Length - 1)];
            if (ct > landMax) { landMax = ct; }
        }

        // Water should only win by strict local majority, never by tie-break priority.
        if (waterCount >= 3 && waterCount > landMax) { return 0; }

        int bestClass = (activeLandClasses.Length > 0) ? activeLandClasses[0] : 1;
        int bestCount = SafeCount(counts, bestClass);
        for (int i = 1; i < activeLandClasses.Length; i++)
        {
            int c = activeLandClasses[i];
            int ct = counts[Mathf.Clamp(c, 0, counts.Length - 1)];
            if (ct > bestCount || (ct == bestCount && TerrainPriority(c) > TerrainPriority(bestClass)))
            {
                bestCount = ct;
                bestClass = c;
            }
        }

        return bestClass;
    }

    private static int SafeCount(int[] counts, int idx)
    {
        return (idx >= 0 && idx < counts.Length) ? counts[idx] : 0;
    }

    private static int TerrainPriority(int terrainClass)
    {
        if (terrainClass == 0) return 100;
        if (terrainClass == 6) return 40; // prefer sand at mixed coasts
        if (terrainClass == 5) return 35; // swamp coast next priority
        if (terrainClass == 3) return 30;
        if (terrainClass == 2) return 20;
        return 10;
    }

    private static int SubmeshIndexForClass(int terrainClass, int[] activeLandClasses)
    {
        if (terrainClass == 0) { return 0; }
        for (int i = 0; i < activeLandClasses.Length; i++)
            if (activeLandClasses[i] == terrainClass) { return i + 1; }
        return 1;
    }

    private Material[] BuildChunkMaterials(params Material[] landMaterials)
    {
        Material[] result = new Material[1 + landMaterials.Length];
        result[0] = overworldWaterMat;
        for (int i = 0; i < landMaterials.Length; i++) { result[i + 1] = landMaterials[i]; }
        return result;
    }

    private static bool IsSnowAtHeight(float worldHeight, int sampleX, int sampleZ, OverworldTerrainController overworld)
    {
        float scale = Mathf.Max(0.001f, overworld.SnowNoiseScale);
        float noise = (Mathf.PerlinNoise((sampleX + 311.113f) / scale, (sampleZ + 517.731f) / scale) * 2f) - 1f;
        float noisySnowLine = overworld.SnowLineAltitude + (noise * overworld.SnowNoiseAmplitude);
        float halfWidth = Mathf.Max(0f, overworld.SnowTransitionWidth * 0.5f);

        if (worldHeight >= noisySnowLine + halfWidth) { return true; }
        if (worldHeight <= noisySnowLine - halfWidth) { return false; }

        float t = Mathf.InverseLerp(noisySnowLine - halfWidth, noisySnowLine + halfWidth, worldHeight);
        float blendNoise = Mathf.PerlinNoise((sampleX + 911.921f) / scale, (sampleZ + 127.357f) / scale);
        return blendNoise <= t;
    }

    private static bool IsStoneAtHeight(float worldHeight, int sampleX, int sampleZ, OverworldTerrainController overworld)
    {
        float scale = Mathf.Max(0.001f, overworld.StoneNoiseScale);
        float noise = (Mathf.PerlinNoise((sampleX + 147.271f) / scale, (sampleZ + 693.487f) / scale) * 2f) - 1f;
        float noisyStoneLine = overworld.StoneLineAltitude + (noise * overworld.StoneNoiseAmplitude);
        float halfWidth = Mathf.Max(0f, overworld.StoneTransitionWidth * 0.5f);

        if (worldHeight >= noisyStoneLine + halfWidth) { return true; }
        if (worldHeight <= noisyStoneLine - halfWidth) { return false; }

        float t = Mathf.InverseLerp(noisyStoneLine - halfWidth, noisyStoneLine + halfWidth, worldHeight);
        float blendNoise = Mathf.PerlinNoise((sampleX + 823.619f) / scale, (sampleZ + 219.043f) / scale);
        return blendNoise <= t;
    }
    private int ClassifyOverworldTerrainSample(float worldHeight, int sampleX, int sampleZ, int px, int pz, int tilesPerPixel, Texture2D heightmap, OverworldTerrainController overworld)
    {
        if (worldHeight <= overworld.WaterSurfaceEpsilon) { return 0; }
        if (IsSnowAtHeight(worldHeight, sampleX, sampleZ, overworld)) { return 3; }
        if (IsStoneAtHeight(worldHeight, sampleX, sampleZ, overworld)) { return 2; }
        // Preserve original stone patterning (slope-based) below the stone line,
        // while allowing stone line to add/force more stone at higher altitude.
        float hE = SampleSmoothedHeight(heightmap, Mathf.Clamp(px + tilesPerPixel, 0, heightmap.width - 1), pz);
        float hW = SampleSmoothedHeight(heightmap, Mathf.Clamp(px - tilesPerPixel, 0, heightmap.width - 1), pz);
        float hN = SampleSmoothedHeight(heightmap, px, Mathf.Clamp(pz + tilesPerPixel, 0, heightmap.height - 1));
        float hS = SampleSmoothedHeight(heightmap, px, Mathf.Clamp(pz - tilesPerPixel, 0, heightmap.height - 1));
        float slopeMagnitude = Mathf.Sqrt(((hE - hW) * (hE - hW)) + ((hN - hS) * (hN - hS)));
        bool baseStone = slopeMagnitude > 0.022f;

        if (baseStone) { return 2; }

        // Sand/swamp are climate overlays on grassy land only; they must not override stone.
        if (IsDesertClimateAtSample(sampleX, sampleZ, overworld)) { return 6; }
        if (IsSwampClimateAtSample(sampleX, sampleZ, overworld)) { return 5; }
        return 1;
    }

    private bool IsDesertClimateAtSample(int sampleX, int sampleZ, OverworldTerrainController overworld)
    {
        if (!terrainClassifyUseDesertClimateMap || cachedNatureClimateMap == null) { return false; }
        float tileWorldSize = Mathf.Max(0.01f, overworld.TileWorldSize);
        float worldX = sampleX * tileWorldSize;
        float worldZ = sampleZ * tileWorldSize;
        float u = Mathf.Clamp01(worldX * terrainClassifyClimateInvWorldW);
        float v = Mathf.Clamp01(worldZ * terrainClassifyClimateInvWorldH);
        int px = Mathf.Clamp(Mathf.RoundToInt(u * (cachedNatureClimateMap.width - 1)), 0, cachedNatureClimateMap.width - 1);
        int py = Mathf.Clamp(Mathf.RoundToInt(v * (cachedNatureClimateMap.height - 1)), 0, cachedNatureClimateMap.height - 1);
        Color32 c = cachedNatureClimateMap.GetPixel(px, py);
        return (c.r == terrainClassifyDesertColor.r) && (c.g == terrainClassifyDesertColor.g) && (c.b == terrainClassifyDesertColor.b);
    }

    private bool IsSwampClimateAtSample(int sampleX, int sampleZ, OverworldTerrainController overworld)
    {
        if (!terrainClassifyUseDesertClimateMap || cachedNatureClimateMap == null || cachedNatureFlatsController == null) { return false; }
        float tileWorldSize = Mathf.Max(0.01f, overworld.TileWorldSize);
        float worldX = sampleX * tileWorldSize;
        float worldZ = sampleZ * tileWorldSize;
        float u = Mathf.Clamp01(worldX * terrainClassifyClimateInvWorldW);
        float v = Mathf.Clamp01(worldZ * terrainClassifyClimateInvWorldH);
        int px = Mathf.Clamp(Mathf.RoundToInt(u * (cachedNatureClimateMap.width - 1)), 0, cachedNatureClimateMap.width - 1);
        int py = Mathf.Clamp(Mathf.RoundToInt(v * (cachedNatureClimateMap.height - 1)), 0, cachedNatureClimateMap.height - 1);
        Color32 c = cachedNatureClimateMap.GetPixel(px, py);
        Color32 swamp = cachedNatureFlatsController.SwampColor;
        return (c.r == swamp.r) && (c.g == swamp.g) && (c.b == swamp.b);
    }

    private void PrepareTerrainClassificationContext(OverworldTerrainController overworld)
    {
        cachedNatureFlatsController = GetOverworldNatureFlatsController();
        cachedNatureClimateMap = (cachedNatureFlatsController != null) ? GetNatureClimateMap(cachedNatureFlatsController) : null;
        terrainClassifyUseDesertClimateMap = cachedNatureFlatsController != null && cachedNatureClimateMap != null;
        if (!terrainClassifyUseDesertClimateMap) { return; }
        terrainClassifyClimateInvWorldW = 1f / Mathf.Max(1f, cachedNatureFlatsController.NatureMapWorldWidth);
        terrainClassifyClimateInvWorldH = 1f / Mathf.Max(1f, cachedNatureFlatsController.NatureMapWorldHeight);
        terrainClassifyDesertColor = cachedNatureFlatsController.DesertColor;
    }

    private Texture2D GetNatureClimateMap(OverworldNatureFlatsController flats)
    {
        string requestedPath = NormalizeResourcesPath(flats.NatureClimateMapResourcePath);
        if (cachedNatureClimateMap != null && string.Equals(cachedNatureClimateMapPath, requestedPath, StringComparison.Ordinal))
        {
            return cachedNatureClimateMap;
        }
        cachedNatureClimateMapPath = requestedPath;
        cachedNatureClimateMap = string.IsNullOrEmpty(requestedPath) ? null : Resources.Load<Texture2D>(requestedPath);
        return cachedNatureClimateMap;
    }

    private static string NormalizeResourcesPath(string path)
    {
        if (string.IsNullOrEmpty(path)) { return string.Empty; }
        string p = path.Replace("\\", "/");
        int idx = p.IndexOf("Resources/", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0) { p = p.Substring(idx + "Resources/".Length); }
        if (p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) { p = p.Substring("Assets/".Length); }
        int dot = p.LastIndexOf('.');
        if (dot > 0) { p = p.Substring(0, dot); }
        return p.TrimStart('/');
    }

    private float SampleTerrainHeightAt(int sampleX, int sampleZ, int tilesPerPixel, Texture2D heightmap, OverworldTerrainController overworld)
    {
        int px = Mathf.Clamp(sampleX * tilesPerPixel, 0, heightmap.width - 1);
        int pz = Mathf.Clamp(sampleZ * tilesPerPixel, 0, heightmap.height - 1);
        float elevation = SampleSmoothedHeight(heightmap, px, pz);
        float shapedElevation = Mathf.Pow(elevation, 1.65f);
        float noise = (Mathf.PerlinNoise((sampleX + 101.231f) * overworld.PerlinScale, (sampleZ + 77.777f) * overworld.PerlinScale) * 2f) - 1f;
        float perlinDisplacement = noise * overworld.PerlinStrength * Mathf.Max(1f, overworld.HeightScale * 0.2f);
        return shapedElevation * overworld.HeightScale + perlinDisplacement - overworld.SeaLevelOffset;
    }

    private Material BuildOverworldSurfaceMaterial(int textureIndex, Material overrideMaterial, Color fallbackColor, int sampleWidth, int sampleHeight)
    {
        OverworldTerrainController overworld = GetOverworldController();
        Material baseMat = null;
        if ((MaterialMasterList != null) && (textureIndex >= 0) && (textureIndex < MaterialMasterList.Length))
        {
            baseMat = MaterialMasterList[textureIndex];
        }

        Material result = (baseMat != null) ? new Material(baseMat) : new Material(Shader.Find("Standard"));
        Shader overworldUnifiedShader = Shader.Find("Custom/OverworldUnifiedTerrain");
        if (overworldUnifiedShader != null)
        {
            result.shader = overworldUnifiedShader;
        }

        if ((overrideMaterial != null) && (overrideMaterial.mainTexture != null))
        {
            Texture overrideTexture = overrideMaterial.mainTexture;
            overrideTexture.wrapMode = TextureWrapMode.Repeat;
            Texture2D overrideTexture2D = overrideTexture as Texture2D;
            result.mainTexture = (overrideTexture2D != null) ? MaybeEnableOverworldMipmaps(overrideTexture2D, overworld) : overrideTexture;

            if ((baseMat != null) && (baseMat.mainTextureScale != Vector2.zero))
            {
                result.mainTextureScale = baseMat.mainTextureScale;
            }
            else
            {
                result.mainTextureScale = new Vector2(sampleWidth, sampleHeight);
            }
        }
        else
        {
            ConfigureTerrainMaterial(result, MaybeEnableOverworldMipmaps(LoadUW2TerrainTexture(textureIndex), overworld), fallbackColor, sampleWidth, sampleHeight);
        }

        result.SetFloat("_Glossiness", 0f);
        result.SetFloat("_Metallic", 0f);
        return result;
    }

    private Texture2D MaybeEnableOverworldMipmaps(Texture2D input, OverworldTerrainController overworld)
    {
        if (input == null) { return null; }
        if ((overworld == null) || !overworld.EnableOverworldTerrainMipmaps) { return input; }
        if (input.mipmapCount > 1) { return input; }

        try
        {
            Texture2D mipTex = new Texture2D(input.width, input.height, TextureFormat.RGBA32, true);
            mipTex.SetPixels32(input.GetPixels32());
            mipTex.Apply(true, false);
            mipTex.wrapMode = input.wrapMode;
            mipTex.filterMode = input.filterMode;
            mipTex.anisoLevel = Mathf.Max(1, input.anisoLevel);
            mipTex.name = input.name + "_Mip";
            return mipTex;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to generate mipmaps for overworld texture " + input.name + ": " + ex.Message);
            return input;
        }
    }

    private Texture2D LoadUW2TerrainTexture(int textureIndex)
    {
        string prevRes = UWClass._RES;
        string prevBase = Loader.BasePath;
        PaletteLoader prevPal = palLoader;

        try
        {
            UWClass._RES = GAME_UW2;
            Loader.BasePath = UWClass.CleanPath(Path_uw2);
            PaletteLoader uw2Palette = new PaletteLoader(Path.Combine(Loader.BasePath, "DATA", "PALS.DAT"), -1);
            TextureLoader uw2TexLoader = new TextureLoader();
            return uw2TexLoader.LoadImageAt(textureIndex, uw2Palette.Palettes[0]);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to load UW2 terrain texture index " + textureIndex + ": " + ex.Message);
            return null;
        }
        finally
        {
            UWClass._RES = prevRes;
            Loader.BasePath = prevBase;
            palLoader = prevPal;
        }
    }

    private void ConfigureTerrainMaterial(Material mat, Texture2D texture, Color fallbackColor, int sampleWidth, int sampleHeight)
    {
        if (texture != null)
        {
            texture.wrapMode = TextureWrapMode.Repeat;
            mat.mainTexture = texture;
            mat.mainTextureScale = new Vector2(sampleWidth, sampleHeight);
        }
        else
        {
            mat.color = fallbackColor;
        }
        mat.SetFloat("_Glossiness", 0f);
        mat.SetFloat("_Metallic", 0f);
    }

    private float SampleSmoothedHeight(Texture2D heightmap, int px, int py)
    {
        float total = 0f;
        int samples = 0;
        for (int oy = -1; oy <= 1; oy++)
        {
            for (int ox = -1; ox <= 1; ox++)
            {
                int sx = Mathf.Clamp(px + ox, 0, heightmap.width - 1);
                int sy = Mathf.Clamp(py + oy, 0, heightmap.height - 1);
                total += heightmap.GetPixel(sx, sy).grayscale;
                samples++;
            }
        }
        return (samples > 0) ? total / samples : 0f;
    }

    private Vector3 GetOverworldSpawnPosition(Texture2D heightmap, float tileWorldSize, int tilesPerPixel, float heightScale, float perlinScale, float perlinStrength, int tileX, int tileY)
    {
        float sampleXf = tileX / (float)tilesPerPixel;
        float sampleYf = tileY / (float)tilesPerPixel;
        int px = Mathf.Clamp(Mathf.RoundToInt(sampleXf * tilesPerPixel), 0, heightmap.width - 1);
        int py = Mathf.Clamp(Mathf.RoundToInt(sampleYf * tilesPerPixel), 0, heightmap.height - 1);

        float elevation = SampleSmoothedHeight(heightmap, px, py);
        float shapedElevation = Mathf.Pow(elevation, 1.65f);
        float noise = Mathf.PerlinNoise((sampleXf + 101.231f) * perlinScale, (sampleYf + 77.777f) * perlinScale) - 0.5f;
        float y = shapedElevation * heightScale + noise * perlinStrength - GetOverworldController().SeaLevelOffset;
        if (y < 0f)
        {
            y = 0f;
        }

        return new Vector3(tileX * (tileWorldSize / tilesPerPixel), y + 2.5f, tileY * (tileWorldSize / tilesPerPixel));
    }

    /// <summary>
    /// Updates the global shader parameter for the colorpalette shaders at set intervals. To enable texture animation
    /// </summary>
    void UpdateAnimation()
    {
        Shader.SetGlobalTexture("_ColorPaletteIn", paletteArray[paletteIndex]);

        if (paletteIndex < paletteArray.GetUpperBound(0))
        {
            paletteIndex++;
        }
        else
        {
            paletteIndex = 0;
        }
        return;
    }

    /// <summary>
    /// Finds the tile or wall at the specified coordinates.
    /// </summary>
    /// <returns>The tile.</returns>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <param name="surface">Surface.</param>
    public static GameObject FindTile(int x, int y, int surface)
    {
        string tileName = GetTileName(x, y, surface);
        Transform found = instance.LevelModel.transform.Find(tileName);
        if (found != null)
        {
            return found.gameObject;
        }
        Debug.Log("Cannot find " + tileName);
        return null;
    }

    /// <summary>
    /// Gets the gameobject name for the specified tile x,y and surface. Eg Wall_02_03, Tile_22_23
    /// </summary>
    /// <returns>The tile name.</returns>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <param name="surface">Surface.</param>
    /// Surfaces are 
    public static string GetTileName(int x, int y, int surface)
    {//Assumes we'll only ever need to deal with open/solid tiles with floors and ceilings.
        string tileName;
        string X; string Y;
        X = x.ToString("D2");
        Y = y.ToString("D2");
        switch (surface)
        {
            case TileMap.SURFACE_WALL:  //SURFACE_WALL:
                {
                    tileName = "Wall_" + X + "_" + Y;
                    break;
                }
            case TileMap.SURFACE_CEIL: //SURFACE_CEIL:
                {
                    tileName = "Ceiling_" + X + "_" + Y;
                    break;
                }
            case TileMap.SURFACE_FLOOR:
            case TileMap.SURFACE_SLOPE:
            default:
                {
                    tileName = "Tile_" + X + "_" + Y;
                    break;
                }
        }
        return tileName;
    }

    /// <summary>
    /// Finds a tile in the current level by name
    /// </summary>
    /// <returns>The tile by name.</returns>
    /// <param name="tileName">Tile name.</param>
    public static GameObject FindTileByName(string tileName)
    {
        return instance.LevelModel.transform.Find(tileName).gameObject;
    }

    /// <summary>
    /// Returns the transform of the levels object marker where objects are generated on.
    /// </summary>
    /// <returns>The marker.</returns>
    public Transform DynamicObjectMarker()
    {
        return _ObjectMarker.transform;
    }

    /// <summary>
    /// Switches the level to another one. Disables the map and level objects of the old one.
    /// </summary>
    /// <param name="newLevelNo">New level no.</param>
    public void SwitchLevel(short newLevelNo)
    {
        if (newLevelNo != -1)
        {
            if (GameWorldController.instance.AtMainMenu)
            //if (LevelNo == -1)
            {//I'm at the main menu. Load up the file data now.
                critsLoader = new CritLoader[64];//Clear out npc animations
                //Initialise various objects as appropiate for the current game.
                InitLevelData();
            }

            if (_RES == GAME_UW2)
            {//Set the game to use UW2 music.
                MusicController.instance.ChangeTrackListForUW2(newLevelNo);
            }

            //Check loading
            if (Tilemaps[newLevelNo] == null)
            {//Data has not been loaded for this level yet
                Tilemaps[newLevelNo] = new TileMap(newLevelNo);

                if (_RES != GAME_SHOCK)
                {
                    //Load Lev.ark data for the objects and tile map
                    Tilemaps[newLevelNo].lev_ark_block = LoadLevArkBlock(newLevelNo);

                    if (GameWorldController.instance.config.dev.GenerateReports)
                    {
                        //Write the unpacked buffer to file.
                        File.WriteAllBytes(Path.Combine(Loader.BasePath , "unpacked_" + newLevelNo + ".ark"),Tilemaps[newLevelNo].lev_ark_block.Data);
                    }


                    if (_RES == GAME_UW1)
                    {//Load the overlays.
                        DataLoader.LoadUWBlock(LevArk.lev_ark_file_data, newLevelNo + 9, 0x180, out Tilemaps[newLevelNo].ovl_ark_block);
                    }

                    //Load lev.ark data fror the texture map.
                    Tilemaps[newLevelNo].tex_ark_block = LoadTexArkBlock(newLevelNo, Tilemaps[newLevelNo].tex_ark_block);

                    if ((Tilemaps[newLevelNo].lev_ark_block.DataLen > 0) && (Tilemaps[newLevelNo].tex_ark_block.DataLen > 0))
                    {
                        if (EnableUnderworldGenerator)
                        {
                            UnderworldGenerator.instance.GenerateLevel(UnderworldGenerator.instance.Seed);
                            Tilemaps[newLevelNo] = UnderworldGenerator.instance.CreateTileMap(newLevelNo);
                            startX = UnderworldGenerator.instance.startX;
                            startY = UnderworldGenerator.instance.startY;
                        }
                        else
                        {
                            Tilemaps[newLevelNo].BuildTileMapUW(newLevelNo, Tilemaps[newLevelNo].lev_ark_block, Tilemaps[newLevelNo].tex_ark_block, Tilemaps[newLevelNo].ovl_ark_block);
                        }

                        //Load game objects from the levark data
                        objectList[newLevelNo] = new ObjectLoader();
                        objectList[newLevelNo].LoadObjectList(Tilemaps[newLevelNo], Tilemaps[newLevelNo].lev_ark_block);

                        if (CreateReports)
                        {
                            CreateObjectReport(objectList[newLevelNo].objInfo, newLevelNo, objectList[newLevelNo]);
                        }
                        if (EnableUnderworldGenerator)
                        {
                            //Clear all objects for the random generator
                            //for (int i = 0; i <= objectList[newLevelNo].objInfo.GetUpperBound(0); i++)
                            //{
                            //    objectList[newLevelNo].objInfo[i].InUseFlag = 0;
                            //}
                        }
                    }
                    else
                    {//load an empty level
                     //TODO:
                    }
                }
                else
                {//Build a SS1 level.
                    Tilemaps[newLevelNo].BuildTileMapShock(LevArk.lev_ark_file_data, newLevelNo);
                    objectList[newLevelNo] = new ObjectLoader();
                    objectList[newLevelNo].LoadObjectListShock(Tilemaps[newLevelNo], LevArk.lev_ark_file_data);
                }

                if (EditorMode == false)
                {//Reduce complexity of the level geometry.
                    Tilemaps[newLevelNo].CleanUp(_RES);
                }
            }

            if ((_RES != GAME_SHOCK) && (dungeon_level != -1))
            {
                //Call special events for inventory objects on level transition out of the current level.
                foreach (Transform t in instance.InventoryMarker.transform)
                {
                    if (t.gameObject.GetComponent<object_base>() != null)
                    {
                        t.gameObject.GetComponent<object_base>().InventoryEventOnLevelExit();
                    }
                }
            }

            //Tell the game we are now using the new level no.
            dungeon_level = newLevelNo;

            switch (_RES)
            {
                case GAME_SHOCK:
                    break;
                default:
                    if (EditorMode == false)
                    {
                        if (LoadingGame == false)
                        {
                            //Call events for inventory objects on level transition into a new level.
                            foreach (Transform t in instance.InventoryMarker.transform)
                            {
                                if (t.gameObject.GetComponent<object_base>() != null)
                                {
                                    t.gameObject.GetComponent<object_base>().InventoryEventOnLevelEnter();
                                }
                            }
                            foreach (Transform t in instance.DynamicObjectMarker())
                            {
                                if (t.gameObject.GetComponent<Container>() != null)
                                {
                                    t.gameObject.GetComponent<Container>().UpdateContainerLinks();
                                }
                            }
                        }
                    }
                    break;
            }

            //Render the tile map based on the loaded data.
            TileMapRenderer.GenerateLevelFromTileMap(LevelModel, SceneryModel, _RES, Tilemaps[newLevelNo], objectList[newLevelNo], false);

            //Positions the character on the new level map.
            PlaceCharacter(newLevelNo);

            switch (_RES)
            {
                case GAME_SHOCK:
                //break;
                default:
                    ObjectLoader.RenderObjectList(objectList[newLevelNo], Tilemaps[newLevelNo], DynamicObjectMarker().gameObject);
                    Debug.Log("Free Static Object Pointer is " + objectList[newLevelNo].NoOfFreeStatic);
                    Debug.Log("Free Mobile Object Pointer is " + objectList[newLevelNo].NoOfFreeMobile);
                    break;
            }

            //Update nav meshes when the "signature" of the level loaded is different from the previous one.
            if ((bGenNavMeshes) && (!EditorMode))
            {
                string newSignature = CurrentTileMap().getSignature();
                if (newSignature != LevelSignature)
                {
                    NavMeshReady = false;
                    StartCoroutine(UpdateNavMeshes());
                }
                LevelSignature = newSignature;
            }

            if ((dungeon_level == 7) && (_RES == GAME_UW1))
            {//Create the special lava for the UW1 endgame.
                CreateShrineLava();
            }
        }
        if ((_RES == GAME_UW2) && (EditorMode == false))
        {
            if (events != null)
            {
                if (!LoadingGame)
                {
                    events.ProcessEvents();
                }
            }
        }
    }

    /// <summary>
    /// Create shrine lava for the abyss in UW1.
    /// </summary>
    private void CreateShrineLava()
    {
        GameObject shrineLava = new GameObject();
        shrineLava.transform.parent = SceneryModel.transform;
        shrineLava.transform.localPosition = new Vector3(-39f, 39.61f, 0.402f);
        shrineLava.transform.localScale = new Vector3(6f, 0.2f, 4.8f);
        shrineLava.AddComponent<ShrineLava>();
        shrineLava.AddComponent<BoxCollider>();
        shrineLava.GetComponent<BoxCollider>().isTrigger = true;
    }

    /// <summary>
    /// Positions the character on the map.
    /// </summary>
    /// <param name="newLevelNo"></param>
    private void PlaceCharacter(short newLevelNo)
    {
        if ((startX != -1) && (startY != -1))
        {
            float targetX = startX * 1.2f + 0.6f;
            float targetY = startY * 1.2f + 0.6f;
            float Height;
            if (StartHeight == -1)
            {
                Height = instance.Tilemaps[newLevelNo].GetFloorHeight(startX, startY) * 0.15f;
            }
            else
            {
                Height = StartHeight * 0.15f;
            }

            UWCharacter.Instance.transform.position = new Vector3(targetX, Height + 0.5f, targetY);
            // Debug.Log("Spawning at " + UWCharacter.Instance.transform.position + " using floorheight " + GameWorldController.instance.Tilemaps[newLevelNo].GetFloorHeight(startX, startY));
            UWCharacter.Instance.TeleportPosition = new Vector3(targetX, Height + 0.1f, targetY);
            if (EnableUnderworldGenerator)
            {
                instance.StartPos = UWCharacter.Instance.transform.position;
            }
        }
        startX = -1; startY = -1;
    }

    /// <summary>
    /// Loads texture map data blocks
    /// </summary>
    /// <param name="newLevelNo"></param>
    /// <param name="tex_ark_block"></param>
    /// <returns></returns>
    private static DataLoader.UWBlock LoadTexArkBlock(short newLevelNo, DataLoader.UWBlock tex_ark_block)
    {
        //Load the texture maps
        switch (_RES)
        {
            case GAME_UWDEMO:
                Loader.ReadStreamFile(Path.Combine(Loader.BasePath, "DATA", "LEVEL13.TXM"), out tex_ark_block.Data);
                tex_ark_block.DataLen = tex_ark_block.Data.GetUpperBound(0);
                break;
            case GAME_UW2:
                DataLoader.LoadUWBlock(LevArk.lev_ark_file_data, newLevelNo + 80, -1, out tex_ark_block);
                break;
            case GAME_UW1:
            default:
                DataLoader.LoadUWBlock(LevArk.lev_ark_file_data, newLevelNo + 18, 0x7a, out tex_ark_block);
                break;
        }

        return tex_ark_block;
    }


    /// <summary>
    /// Loads the LevArk Block Data
    /// </summary>
    /// <param name="newLevelNo"></param>
    /// <returns>Raw Lev Ark Data</returns>
    private static DataLoader.UWBlock LoadLevArkBlock(short newLevelNo)
    {
        DataLoader.UWBlock lev_ark_block;
        if (_RES == GAME_UWDEMO)
        {//In UWDemo there is no block structure. Just copy the data directly from file.
            lev_ark_block = new DataLoader.UWBlock
            {
                DataLen = 0x7c06,
                Data = LevArk.lev_ark_file_data
            };
        }
        else
        {
            //Load the tile and object blocks
            DataLoader.LoadUWBlock(LevArk.lev_ark_file_data, newLevelNo, 0x7c08, out lev_ark_block);
            //Trim to the correct size for lev ark blocks.
            Array.Resize(ref lev_ark_block.Data, 0x7c08);
        }

        return lev_ark_block;
    }

    /// <summary>
    /// Switchs the level and puts the player at the floor level of the new level
    /// </summary>
    /// <param name="newLevelNo">New level no.</param>
    /// <param name="newTileX">New tile x.</param>
    /// <param name="newTileY">New tile y.</param>
    public void SwitchLevel(short newLevelNo, short newTileX, short newTileY)
    {
        startX = newTileX;
        startY = newTileY;
        StartHeight = -1;
        SwitchLevel(newLevelNo);
    }

    /// <summary>
    /// Switchs the level and puts the player at the specified height
    /// </summary>
    /// <param name="newLevelNo"></param>
    /// <param name="newTileX"></param>
    /// <param name="newTileY"></param>
    /// <param name="newStartHeight"></param>
    public void SwitchLevel(short newLevelNo, short newTileX, short newTileY, short newStartHeight)
    {
        startX = newTileX;
        startY = newTileY;
        StartHeight = newStartHeight;
        SwitchLevel(newLevelNo);
    }

    private void EnforceSingleAudioListener()
    {
        AudioListener[] listeners = FindObjectsOfType<AudioListener>();
        if ((listeners == null) || (listeners.Length <= 1))
        {
            return;
        }

        if ((UWCharacter.Instance == null) || (UWCharacter.Instance.playerCam == null))
        {
            return;
        }

        AudioListener preferred = UWCharacter.Instance.playerCam.GetComponent<AudioListener>();
        if (preferred == null)
        {
            preferred = listeners[0];
        }

        for (int i = 0; i < listeners.Length; i++)
        {
            listeners[i].enabled = (listeners[i] == preferred);
        }
    }

    /// <summary>
    /// Detects where the player currently is an updates their swimming state and auto map as needed.
    /// </summary>
    public void PositionDetect()
    {
        if ((AtMainMenu == true) || (WindowDetect.InMap))
        {
            return;
        }
        if ((_RES != GAME_UW1) && (_RES != GAME_UWDEMO) && (_RES != GAME_UW2))
        {
            return;
        }
        if ((CurrentTileMap() == null) || (CurrentAutoMap() == null))
        {
            return;
        }

        TileMap.visitTileX = (short)(UWCharacter.Instance.transform.position.x / 1.2f);
        TileMap.visitTileY = (short)(UWCharacter.Instance.transform.position.z / 1.2f);

        UWCharacter.Instance.x_position = (int)(UWCharacter.Instance.transform.position.x * SaveGame.Ratio);
        UWCharacter.Instance.y_position = (int)(UWCharacter.Instance.transform.position.z * SaveGame.Ratio);
        UWCharacter.Instance.z_position = (int)((UWCharacter.Instance.transform.position.y - SaveGame.VertAdjust) * SaveGame.Ratio);
        UWCharacter.Instance.heading = (int)(this.transform.eulerAngles.y * (255f / 360f));


        if (EditorMode)
        {
            if ((TileMap.visitedTileX != TileMap.visitTileX) || (TileMap.visitedTileY != TileMap.visitTileY))
            {
                if (IngameEditor.FollowMeMode)
                {
                    IngameEditor.UpdateFollowMeMode(TileMap.visitTileX, TileMap.visitTileY);
                }
            }
        }

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if
                    (
                        (
                                (TileMap.visitTileX + x >= 0) && (TileMap.visitTileX + x <= TileMap.TileMapSizeX)
                        )
                        &&
                        (
                                (TileMap.visitTileY + y >= 0) && (TileMap.visitTileY + y <= TileMap.TileMapSizeY)
                        )
                    )
                {
                    CurrentAutoMap().MarkTile(TileMap.visitTileX + x, TileMap.visitTileY + y, CurrentTileMap().Tiles[TileMap.visitTileX + x, TileMap.visitTileY + y].tileType, AutoMap.GetDisplayType(CurrentTileMap().Tiles[TileMap.visitTileX + x, TileMap.visitTileY + y]));
                }
            }
        }
        TileMap.visitedTileX = TileMap.visitTileX;
        TileMap.visitedTileY = TileMap.visitTileY;
        UWCharacter.Instance.CurrentTerrain = CurrentTileMap().Tiles[TileMap.visitTileX, TileMap.visitTileY].terrain;
        UWCharacter.Instance.terrainType = TerrainDatLoader.getTerrain(UWCharacter.Instance.CurrentTerrain);
    }


    ///// <summary>
    ///// Moves the object to the game world where it will be managed by the objectloader list
    ///// </summary>
    ///// <param name="obj">Object.</param>
    //public static void MoveToWorld(GameObject obj)
    //{
    //    MoveToWorld(obj.GetComponent<ObjectInteraction>());
    //}

    /// <summary>
    /// Moves to world (from inventory) and assigns it to the world object list.
    /// </summary>
    /// <returns>The to world.</returns>
    /// <param name="obj">Object.</param>
    public static ObjectInteraction MoveToWorld(ObjectInteraction obj, bool staticObject = true)
    {
        //Add item to a free slot on the item list and point the instance back to this.
        obj.UpdatePosition();

        obj.transform.parent = instance.DynamicObjectMarker();
        //Find an index for the object.
        short NewIndex;
        if (staticObject)
        {
            if (!CurrentObjectList().GetFreeStaticObject(out NewIndex))
            {
                Debug.Log("Unable to find a free static slot for this object"); return null;
            }
            NewIndex = CurrentObjectList().GetStaticAtSlot(NewIndex);
        }
        else
        {
            if (!CurrentObjectList().GetFreeMobileObject(out NewIndex))
            {
                Debug.Log("Unable to find a free mobile slot for this object"); return null;
            }
            NewIndex = CurrentObjectList().GetMobileAtSlot(NewIndex);
        }
        //Destroy the existing object instance at the new slot.
        if (CurrentObjectList().objInfo[NewIndex].instance != null)
        {
            Debug.Log("MoveToWorld:Destroying " + CurrentObjectList().objInfo[NewIndex].instance.name);
            Destroy(CurrentObjectList().objInfo[NewIndex].instance.gameObject);
        }


        CurrentObjectList().objInfo[NewIndex] = new ObjectLoaderInfo(NewIndex, CurrentTileMap(), true);
        //Copy existing static info from inventory to objectdata
        for (int i = 0; i < 8; i++)
        {
            CurrentObjectList().objInfo[NewIndex].DataBuffer[CurrentObjectList().objInfo[NewIndex].PTR + i] = obj.BaseObjectData.InventoryData[i];
        }
        //Link the instances
        CurrentObjectList().objInfo[NewIndex].instance = obj;
        obj.BaseObjectData = CurrentObjectList().objInfo[NewIndex];

        //Rename the instance
        obj.transform.name =ObjectInteraction.UniqueObjectName(obj);

        Container cnt = obj.GetComponent<Container>();
        if (cnt != null)
        {//Object has a container that has objects that need to be moved as well
            for (int i = 0; i < cnt.items.GetUpperBound(0); i++)
            {
                if (cnt.items[i] != null)
                {
                    MoveToWorld(cnt.items[i], true); //Move container objects as static objects into the world. (The parent might be mobile)
                }
            }

            UpdateContainerLinkedChain(cnt);
        }

        obj.GetComponent<object_base>().MoveToWorldEvent();
        if (ConversationVM.InConversation)
        {
            Debug.Log("Use of MoveToWorld in conversation. Review usage to avoid object list corruption! " + obj.name);
            //ConversationVM.BuildObjectList();//Reflect changes to object lists
        }

        return obj;
    }

    public static void UpdateContainerLinkedChain(Container cnt)
    {
        //Relink container contents
        bool isNext = false;//What property should be updated.
        ObjectInteraction parentItem = cnt.objInt();
        parentItem.link = 0; //Assume no object in container.
        for (int i = 0; i < cnt.items.GetUpperBound(0); i++)
        {
            if (cnt.items[i] != null)
            {
                //linked or next item found.
                if (isNext)
                {
                    parentItem.next = cnt.items[i].ObjectIndex;
                }
                else
                {
                    parentItem.link = cnt.items[i].ObjectIndex;
                    isNext = true; //any item after the first linked item must be a next.
                }
                parentItem = cnt.items[i];//Move to next item.
                parentItem.next = 0;//Assume next is going to be no object.                    
            }
        }
    }

    /// <summary>
    /// Moves to inventory where it will no longer be managed by the objectloader list.
    /// </summary>
    /// <param name="obj">Object.</param>
    public static ObjectInteraction MoveToInventory(GameObject obj)
    {
        return MoveToInventory(obj.GetComponent<ObjectInteraction>());
    }


    /// <summary>
    /// Moves an object to inventory and removes it from the world map
    /// </summary>
    /// <param name="obj">Object.</param>
    public static ObjectInteraction MoveToInventory(ObjectInteraction obj)
    {//Break the instance back to the object list
        ObjectInteraction.UnlinkItemFromTileMapChain(obj, obj.ObjectTileX, obj.ObjectTileY);

        obj.transform.parent = instance.InventoryMarker.transform;
        //Copy loader data to obj.
        byte[] NewinventoryData = new byte[8];
        for (int i = 0; i < 8; i++)
        {
            NewinventoryData[i] = obj.BaseObjectData.DataBuffer[obj.BaseObjectData.PTR + i];
        }
        ObjectLoaderInfo newObj = new ObjectLoaderInfo(0, CurrentTileMap(), false)
        {
            parentList = instance.inventoryLoader,
            InventoryData = NewinventoryData
        };

       // obj.BaseObjectData.InUseFlag = 0;//This frees up the slot to be replaced with another item.	
        obj.BaseObjectData.instance = null;
        if (_RES == GAME_UW2)//Does this need to be done for uw1 as well.
        {
            ObjectLoaderInfo.CleanUp(obj.BaseObjectData);
        }

        if (obj.BaseObjectData.IsStatic)
        {
            CurrentObjectList().ReleaseFreeStaticObject(obj.BaseObjectData.index);
        }
        else
        {
            CurrentObjectList().ReleaseFreeMobileObject(obj.BaseObjectData.index);
        }


        //Link instances
        newObj.instance = obj;
        obj.BaseObjectData = newObj;

        Container cnt = obj.GetComponent<Container>();
        if (cnt != null)
        {//Object has a container that has objects that need to be moved as well
            for (int i = 0; i < cnt.items.GetUpperBound(0); i++)
            {
                if (cnt.items[i] != null)
                {
                    MoveToInventory(cnt.items[i]); //Move container objects as static objects into the world. (The parent might be mobile)
                }
            }
        }

        obj.GetComponent<object_base>().MoveToInventoryEvent();
        if (ConversationVM.InConversation)
        {
            Debug.Log("MoveToInventory in converstion. Check that it works");
         //   ConversationVM.BuildObjectList();//Reflect changes to object lists
        }
        return obj;
    }


    public static void MoveFromMobileToStatic(ObjectInteraction obj)
    {
        var beforename = obj.name;
        var oldindex = obj.BaseObjectData.index;
        //Find a slot in the static list.
        short NewIndex;
        if (!CurrentObjectList().GetFreeStaticObject(out NewIndex))
        {
            Debug.Log("Unable to find a free static slot for this object"); return;
        }

        NewIndex = CurrentObjectList().GetStaticAtSlot(NewIndex);

        //release from mobile ist
        CurrentObjectList().ReleaseFreeMobileObject(obj.BaseObjectData.index);

        //Destroy the existing object instance at the new slot.
        if (CurrentObjectList().objInfo[NewIndex].instance != null)
        {
            Destroy(CurrentObjectList().objInfo[NewIndex].instance.gameObject);
        }
        //CurrentObjectList().objInfo[NewIndex] = new ObjectLoaderInfo(NewIndex, CurrentTileMap(), true);
        //Copy existing static info from inventory to objectdata
        var dstObjectData = CurrentObjectList().objInfo[NewIndex];
        var srcObjectData = CurrentObjectList().objInfo[obj.BaseObjectData.index];

        for (int i = 0; i < 8; i++)
        {
            dstObjectData.DataBuffer[dstObjectData.PTR + i] = srcObjectData.DataBuffer[srcObjectData.PTR + i];
           // dstObjectData.DataBuffer[dstObjectData.PTR+i] = obj.BaseObjectData.DataBuffer[obj.i];
            //  CurrentObjectList().objInfo[NewIndex].DataBuffer[CurrentObjectList().objInfo[NewIndex].PTR + i] = obj.BaseObjectData.DataBuffer[i];
        }

        //Clear the data in the mobile slot
        for (int i = 0; i <= 0x1a; i++)
        {
           obj.BaseObjectData.DataBuffer[i]=0;
        }
        //ReLink the instances
        obj.BaseObjectData.instance = null;
        CurrentObjectList().objInfo[NewIndex].instance = obj;
        obj.BaseObjectData = CurrentObjectList().objInfo[NewIndex];

        //Rename the instance
        obj.transform.name = ObjectInteraction.UniqueObjectName(obj);


        Debug.Log("Moving " + beforename + " from mobile #" + oldindex + " to static #" + NewIndex + ". It is now " + obj.name);

    }

    /// <summary>
    /// Updates the positions of all game objects
    /// </summary>
    public void UpdatePositions()
    {
        foreach (Transform t in instance.DynamicObjectMarker())
        {
            if (t.gameObject.GetComponent<ObjectInteraction>() != null)
            {
                t.gameObject.GetComponent<ObjectInteraction>().UpdatePosition();
            }
        }
    }


    /// <summary>
    /// Inits the level object data, maps and textures objects as required by each game.
    /// </summary>
    void InitLevelData()
    {
        // Path to lev.ark file to load
        string Lev_Ark_File;

        switch (_RES)
        {
            case GAME_SHOCK:
                Tilemaps = new TileMap[15];
                objectList = new ObjectLoader[15];
                break;
            case GAME_UWDEMO:
                Tilemaps = new TileMap[1];
                objectList = new ObjectLoader[1];
                AutoMaps = new AutoMap[1];
                break;
            case GAME_UW2:
                Tilemaps = new TileMap[80];//Not all are in use.
                objectList = new ObjectLoader[80];
                AutoMaps = new AutoMap[80];
                break;
            case GAME_UW1:
            default:
                Tilemaps = new TileMap[9];
                objectList = new ObjectLoader[9];
                AutoMaps = new AutoMap[9];
                break;
        }

        switch (_RES)
        {
            case GAME_SHOCK:
                MaterialMasterList = new Material[273];
                break;
            case GAME_UWDEMO:
                MaterialMasterList = new Material[58];
                break;
            case GAME_UW2:
                MaterialMasterList = new Material[256];//For each texture in UW2
                break;
            case GAME_UW1:
            default:
                MaterialMasterList = new Material[260];//For each texture in UW1
                break;
        }

        //Load up my map materials
        for (int i = 0; i <= MaterialMasterList.GetUpperBound(0); i++)
        {
            if (File.Exists(texLoader.ModPath(i)))
            {
                MaterialMasterList[i] = (Material)Resources.Load("Materials/ModShaders/" + _RES + "_" + i.ToString("d3"));
            }
            else
            {
                MaterialMasterList[i] = (Material)Resources.Load(_RES + "/Materials/textures/" + _RES + "_" + i.ToString("d3"));
            }
            switch (MaterialMasterList[i].shader.name.ToUpper())
            {
                case "COLOURREPLACEMENT":
                case "COLOURREPLACEMENTREVERSE":
                    MaterialMasterList[i].mainTexture = texLoader.LoadImageAt(i, 1);//load a greyscale texture for use with the shader.
                    break;
                case "BASICUWSHADER":
                    MaterialMasterList[i].mainTexture = texLoader.LoadImageAt(i, 0);
                    break;
                case "LEGACY SHADERS/BUMPED DIFFUSE":
                    {
                        Texture2D loadedTexture = texLoader.LoadImageAt(i, 2);//Get normal map from mod directory
                        MaterialMasterList[i].mainTexture = texLoader.LoadImageAt(i, 0);
                        if (loadedTexture != null)
                        {
                            MaterialMasterList[i].SetTexture("_BumpMap", TextureLoader.NormalMap(loadedTexture, TextureLoader.BumpMapStrength));
                        }
                    }
                    break;
                default:
                    Debug.Log(i + " is " + MaterialMasterList[i].shader.name);
                    MaterialMasterList[i].mainTexture = texLoader.LoadImageAt(i, 0);
                    break;
            }
        }
        if (_RES == GAME_UW1)
        {
            SpecialMaterials[0] = (Material)Resources.Load(_RES + "/Materials/textures/" + _RES + "_224_maze");
            SpecialMaterials[0].mainTexture = texLoader.LoadImageAt(224);
        }
        MaterialObj = new Material[TmObjArt.NoOfFileImages()];

        //Load the materials for the TMOBJ file
        for (int i = 0; i <= MaterialObj.GetUpperBound(0); i++)
        {
            MaterialObj[i] = (Material)Resources.Load(_RES + "/Materials/tmobj/tmobj_" + i.ToString("d2"));
            if (MaterialObj[i] != null)
            {
                MaterialObj[i].mainTexture = TmObjArt.LoadImageAt(i);
            }
        }

        switch (_RES)
        {
            case GAME_SHOCK:
                break;

            default:
                //Load up my door texture
                for (int i = 0; i <= MaterialDoors.GetUpperBound(0); i++)
                {
                    MaterialDoors[i] = (Material)Resources.Load(_RES + "/Materials/doors/doors_" + i.ToString("d2") + "_material");
                    MaterialDoors[i].mainTexture = DoorArt.LoadImageAt(i);
                }
                break;

        }

        //Load up my tile maps
        //First read in my lev_ark file
        switch (_RES)
        {
            case GAME_SHOCK:
                Lev_Ark_File = Path.Combine("RES", "DATA", "ARCHIVE.DAT");
                break;
            case GAME_UWDEMO:
                Lev_Ark_File = Path.Combine("DATA", "LEVEL13.ST");
                break;
            case GAME_UW2:
            case GAME_UW1:
            default:
                Lev_Ark_File = Lev_Ark_File_Selected; //"DATA\\lev.ark";//Eventually this will be a save game.
                break;
        }
        var toLoad = Path.Combine(Loader.BasePath, Lev_Ark_File);
        if (!Loader.ReadStreamFile(toLoad, out LevArk.lev_ark_file_data))
        {
            Debug.Log(toLoad + "File not loaded");
            Application.Quit();
        }

        //Load up auto map data
        switch (_RES)
        {
            case GAME_UWDEMO:
                AutoMaps[0] = new AutoMap();
                AutoMaps[0].InitAutoMapDemo();
                break;
            case GAME_UW1:
                for (int i = 0; i <= AutoMaps.GetUpperBound(0); i++)
                {
                    AutoMaps[i] = new AutoMap();
                    AutoMaps[i].InitAutoMapUW1(i, LevArk.lev_ark_file_data);
                }
                break;
            case GAME_UW2:
                for (int i = 0; i <= AutoMaps.GetUpperBound(0); i++)
                {
                    AutoMaps[i] = new AutoMap();
                    AutoMaps[i].InitAutoMapUW2(i, LevArk.lev_ark_file_data);
                }
                break;
        }

        switch (_RES)
        {
            case GAME_UW2:
                events = new event_processor();
                if (whatTheHellIsThatFileFor != null)
                {
                    whatTheHellIsThatFileFor.DumpScdArkInfo(SCD_Ark_File_Selected);
                }
                break;
        }
    }


    /// <summary>
    /// Inits the B globals.
    /// </summary>
    /// <param name="SlotNo">Slot no.</param>
    public void InitBGlobals(int SlotNo)
    {
        byte[] bglob_data;
        if (SlotNo == 0)
        {//Init from BABGLOBS.DAT. Initialise the data.
            if (Loader.ReadStreamFile(Path.Combine(Loader.BasePath, "DATA", "BABGLOBS.DAT"), out bglob_data))
            {
                int NoOfSlots = bglob_data.GetUpperBound(0) / 4;
                int add_ptr = 0;
                bGlobals = new BablGlobal[NoOfSlots + 1];
                for (int i = 0; i <= NoOfSlots; i++)
                {
                    bGlobals[i].ConversationNo = (int)Loader.getValAtAddress(bglob_data, add_ptr, 16);
                    bGlobals[i].Size = (int)Loader.getValAtAddress(bglob_data, add_ptr + 2, 16);
                    bGlobals[i].Globals = new int[bGlobals[i].Size];
                    add_ptr += 4;
                }
            }
        }
        else
        {
            int NoOfSlots = 0;//Assumes the same no of slots that is in the babglobs is in bglobals.
            if (Loader.ReadStreamFile(Path.Combine(Loader.BasePath, "DATA", "BABGLOBS.DAT"), out bglob_data))
            {
                NoOfSlots = bglob_data.GetUpperBound(0) / 4;
                NoOfSlots++;
            }
            if (Loader.ReadStreamFile(Path.Combine(Loader.BasePath, "SAVE" + SlotNo, "BGLOBALS.DAT"), out bglob_data))
            {
                //int NoOfSlots = bglob_data.GetUpperBound(0)/4;
                int add_ptr = 0;
                bGlobals = new BablGlobal[NoOfSlots];
                for (int i = 0; i < NoOfSlots; i++)
                {

                    bGlobals[i].ConversationNo = (int)Loader.getValAtAddress(bglob_data, add_ptr, 16);
                    bGlobals[i].Size = (int)Loader.getValAtAddress(bglob_data, add_ptr + 2, 16);
                    bGlobals[i].Globals = new int[bGlobals[i].Size];
                    add_ptr += 4;
                    for (int g = 0; g < bGlobals[i].Size; g++)
                    {
                        bGlobals[i].Globals[g] = (int)Loader.getValAtAddress(bglob_data, add_ptr, 16);
                        if (bGlobals[i].Globals[g] == 65535)
                        {
                            bGlobals[i].Globals[g] = 0;
                        }
                        add_ptr += 2;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Writes the BGlobals data to file
    /// </summary>
    /// <param name="SlotNo">Slot no.</param>
    public void WriteBGlobals(int SlotNo)
    {
        int fileSize = 0;
        for (int c = 0; c <= bGlobals.GetUpperBound(0); c++)
        {
            fileSize += 4;  //No and size
            fileSize += bGlobals[c].Size * 2;
        }
        //Create an output byte array
        Byte[] output = new byte[fileSize];
        int add_ptr = 0;
        for (int c = 0; c <= bGlobals.GetUpperBound(0); c++)
        {
            //Write Slot No
            output[add_ptr] = (byte)(bGlobals[c].ConversationNo & 0xff);
            output[add_ptr + 1] = (byte)((bGlobals[c].ConversationNo >> 8) & 0xff);
            //Write Size
            output[add_ptr + 2] = (byte)(bGlobals[c].Size & 0xff);
            output[add_ptr + 3] = (byte)((bGlobals[c].Size >> 8) & 0xff);
            add_ptr += 4;
            for (int g = 0; g <= bGlobals[c].Globals.GetUpperBound(0); g++)
            {
                output[add_ptr] = (byte)(bGlobals[c].Globals[g] & 0xff);
                output[add_ptr + 1] = (byte)((bGlobals[c].Globals[g] >> 8) & 0xff);
                add_ptr += 2;
            }
        }
        File.WriteAllBytes(Path.Combine(Loader.BasePath, "SAVE" + SlotNo, "BGLOBALS.DAT"), output);

    }

    /// <summary>
    /// Switchs to a Terra nova map.
    /// </summary>
    /// <param name="levelFileName">Level file name.</param>
    public void SwitchTNovaMap(string levelFileName)
    {
        string path;
        if (levelFileName == "")
        {
            path = NovaLevelSelect.MapSelected;
        }
        else
        {
            path = levelFileName;
        }

        if (Loader.ReadStreamFile(path, out byte[] archive_ark))
        {
            if (!DataLoader.LoadChunk(archive_ark, 86, out DataLoader.Chunk lev_ark))
            {
                return;
            }
            UWCharacter.Instance.playerCam.GetComponent<Light>().range = 2000f;
            UWCharacter.Instance.playerCam.farClipPlane = 30000f;
            TNovaTerrain.gameObject.SetActive(true);
            TileMapRenderer.RenderTNovaMapTerrain(TNovaLevelModel.transform, lev_ark.data);
        }

        //Try and play sound file from a tnova res file
        if (Loader.ReadStreamFile("C:\\Games\\Terra Nova\\CD\\Terra_Nova\\SPEECH\\RESBRK01.RES", out byte[] sound_ark))
        {
            if (!DataLoader.LoadChunk(sound_ark, 3308, out DataLoader.Chunk voc_file))
            {
                return;
            }
            VocLoader voc = new VocLoader(voc_file.data, "tnova");
            MusicController.instance.Aud.clip = voc.Audio;
            MusicController.instance.Aud.loop = true;
            MusicController.instance.Aud.Play();
        }
    }



    /// <summary>
    /// Loads the config file.
    /// </summary>
    /// <returns><c>true</c>, if config file was loaded, <c>false</c> otherwise.</returns>
    bool LoadConfigFile()
    {
        config = Configuration.Read();

        // Configuration.Save(config);
        return true;
        //string fileName = Application.dataPath + sep + ".." + sep + "config.ini";
        //if (File.Exists(fileName))
        //{
        //    string line;
        //    StreamReader fileReader = new StreamReader(fileName, Encoding.Default);
        //    //string PreviousKey="";
        //    //string PreviousValue="";
        //    using (fileReader)
        //    {
        //        // While there's lines left in the text file, do this:
        //        do
        //        {
        //            line = fileReader.ReadLine();
        //            if (line != null)
        //            {
        //                if (line.Length > 1)
        //                {
        //                    if ((line.Substring(1, 1) != ";") && (line.Contains("=")))//Is not a commment and contains a param
        //                    {
        //                        string[] entries = line.Split('=');
        //                        //int val = 0;
        //                        //string pathfound="";
        //                        KeyCode keyCodeToUse;
        //                        config.chartoKeycode.TryGetValue(entries[1].ToLower(), out keyCodeToUse);

        //                        switch (entries[0].ToUpper())
        //                        {
        //                            case "MOUSEX"://Mouse sensitivity X
        //                                {
        //                                    float val = 15f;
        //                                    if (float.TryParse(entries[1], out val))
        //                                    {
        //                                        MouseX.sensitivityX = val;
        //                                    }
        //                                    config.mouse.mouseX = val;
        //                                    break;
        //                                }
        //                            case "MOUSEY"://Mouse sensitivity Y
        //                                {
        //                                    float val = 15f;
        //                                    if (float.TryParse(entries[1], out val))
        //                                    {
        //                                        MouseY.sensitivityY = val;
        //                                    }
        //                                    config.mouse.mouseY = val;
        //                                    break;
        //                                }
        //                            case "PATH_UW0":
        //                                {
        //                                    //path_uw0 = UWClass.CleanPath(entries[1]);
        //                                    config.paths.PATH_UWDEMO = path_uw0;
        //                                    break;
        //                                }
        //                            case "PATH_UW1":
        //                                {
        //                                    //path_uw1 = UWClass.CleanPath(entries[1]);
        //                                    config.paths.PATH_UW1 = path_uw1;
        //                                    break;
        //                                }
        //                            case "PATH_UW2":
        //                                {
        //                                    //path_uw2 = UWClass.CleanPath(entries[1]);
        //                                    config.paths.PATH_UW2 = path_uw2;
        //                                    break;
        //                                }
        //                            case "PATH_SHOCK":
        //                                {
        //                                   // path_shock = UWClass.CleanPath(entries[1]);
        //                                    config.paths.PATH_SHOCK = path_shock;
        //                                    break;
        //                                }
        //                            case "PATH_TNOVA":
        //                                {
        //                                    //path_tnova = UWClass.CleanPath(entries[1]);
        //                                    config.paths.PATH_TNOVA = path_tnova;
        //                                    break;
        //                                }

        //                            case "FLYUP":
        //                                GameWorldController.instance.config.FlyUp = keyCodeToUse; break;
        //                            case "FLYDOWN":
        //                                GameWorldController.instance.config.FlyDown = keyCodeToUse; break;
        //                            case "TOGGLEMOUSELOOK":
        //                                GameWorldController.instance.config.ToggleMouseLook = keyCodeToUse; break;
        //                            case "TOGGLEFULLSCREEN":
        //                                GameWorldController.instance.config.ToggleFullScreen = keyCodeToUse; break;
        //                            case "INTERACTIONOPTIONS":
        //                                GameWorldController.instance.config.InteractionOptions = keyCodeToUse; break;
        //                            case "INTERACTIONTALK":
        //                                GameWorldController.instance.config.InteractionTalk = keyCodeToUse; break;
        //                            case "INTERACTIONPICKUP":
        //                                GameWorldController.instance.config.InteractionPickup = keyCodeToUse; break;
        //                            case "INTERACTIONLOOK":
        //                                GameWorldController.instance.config.InteractionLook = keyCodeToUse; break;
        //                            case "INTERACTIONATTACK":
        //                                GameWorldController.instance.config.InteractionAttack = keyCodeToUse; break;
        //                            case "INTERACTIONUSE":
        //                                GameWorldController.instance.config.InteractionUse = keyCodeToUse; break;
        //                            case "CASTSPELL":
        //                                GameWorldController.instance.config.CastSpell = keyCodeToUse; break;
        //                            case "TRACKSKILL":
        //                                GameWorldController.instance.config.TrackSkill = keyCodeToUse; break;


        //                            case "DEFAULTLIGHTLEVEL":
        //                                {
        //                                    float lightlevel = 16f;
        //                                    if (float.TryParse(entries[1], out lightlevel))
        //                                    {
        //                                       // LightSource.BaseBrightness = lightlevel;
        //                                    }
        //                                    config.camera.DefaultLightLevel = lightlevel;
        //                                    break;
        //                                }

        //                            case "FOV":
        //                                {
        //                                    float fov = 75f;
        //                                    if (float.TryParse(entries[1], out fov))
        //                                    {
        //                                        Camera.main.fieldOfView = fov;
        //                                    }
        //                                    config.camera.FOV = fov;
        //                                    break;

        //                                }
        //                            case "INFINITEMANA":
        //                                {
        //                                   // Magic.InfiniteMana = (entries[1] == "1");
        //                                    config.cheats.InfiniteMana = Magic.InfiniteMana;
        //                                    break;
        //                                }

        //                            case "GODMODE":
        //                                {
        //                                    //UWCharacter.Invincible = (entries[1] == "1");
        //                                    config.cheats.GodMode = UWCharacter.Invincible;
        //                                    break;
        //                                }

        //                            case "CONTEXTUIENABLED":
        //                                {
        //                                    //WindowDetectUW.ContextUIEnabled = (entries[1] == "1");
        //                                    config.ui.ContextUIEnabled = WindowDetectUW.ContextUIEnabled;
        //                                    break;
        //                                }

        //                            case "UW1_SOUNDBANK":
        //                                {
        //                                    //MusicController.UW1Path = UWClass.CleanPath(entries[1]);
        //                                    config.audio.UW1_SOUNDBANK = MusicController.UW1Path;
        //                                    break;
        //                                }
        //                            case "UW2_SOUNDBANK":
        //                                {
        //                                    //MusicController.UW2Path = UWClass.CleanPath(entries[1]);
        //                                    config.audio.UW2_SOUNDBANK = MusicController.UW2Path;
        //                                    break;
        //                                }
        //                            case "GENREPORT":
        //                                {
        //                                    //CreateReports = (entries[1] == "1");
        //                                    config.dev.GenerateReports = CreateReports;
        //                                    break;
        //                                }
        //                            case "SHOWINUSE"://only show inuse objects in reports
        //                                {
        //                                    //ShowOnlyInUse = (entries[1] == "1");
        //                                    config.dev.ShowOnlyInUse = ShowOnlyInUse;
        //                                    break;
        //                                }
        //                            case "AUTOKEYUSE":
        //                                {
        //                                   // UWCharacter.AutoKeyUse = (entries[1] == "1");
        //                                    config.ui.AutoKey = UWCharacter.AutoKeyUse;
        //                                    break;
        //                                }
        //                            case "AUTOEAT":
        //                                {
        //                                    //UWCharacter.AutoEat = (entries[1] == "1");
        //                                    break;
        //                                }
        //                        }
        //                    }
        //                }

        //            }
        //        }
        //        while (line != null);
        //        fileReader.Close();
        //        Configuration.Save(config);
        //        return true;
        //    }
        //}
        //else
        //{
        //    return false;
        //}
    }


    /// <summary>
    /// Creates a report of the objects in the level in an xml format
    /// </summary>
    /// <param name="objList"></param>
    void CreateObjectReport(ObjectLoaderInfo[] objList, int ReportLevelNo, ObjectLoader list)
    {
        StreamWriter writer = new StreamWriter(Application.dataPath + "//..//_objectreport.xml");// true);
        writer.WriteLine("<ObjectReport level =" + ReportLevelNo + "> ");
        //writer.WriteLine("\t<level>" + ReportLevelNo + "</level>");
        for (int o = 0; o <= objList.GetUpperBound(0); o++)
        {
            //if (((objList[o].InUseFlag == 0) && (!ShowOnlyInUse)) || (objList[o].InUseFlag == 1))
            if (true)
            {
                //if
                //((objList[o].GetItemType() == ObjectInteraction.A_CHECK_VARIABLE_TRAP)||(objList[o].GetItemType() == ObjectInteraction.A_SET_VARIABLE_TRAP))
                //{
                WriteObjectXML(objList, writer, o);
                //}               
            }
        }        
        writer.WriteLine("</ObjectReport>");

       
        writer.WriteLine("<freeobjectreport>");
        writer.WriteLine("<mobile Size=" + list.NoOfFreeMobile +">");
        for (short i=0; i<=254;i++)
        {
            writer.WriteLine("\t<mobile index=" + i + ">" + list.GetMobileAtSlot(i) + "</mobile>");
        }
        writer.WriteLine("</mobile>");
        writer.WriteLine("<static Size=" + list.NoOfFreeStatic + ">");
        for (short i = 0; i <= 768; i++)
        {
            writer.WriteLine("\t<static index=" + i + ">" + list.GetStaticAtSlot(i) + "</static>");
        }
        writer.WriteLine("</static>");

        writer.WriteLine("</freeobjectreport>");

        writer.Close();
    }

    private static void WriteObjectXML(ObjectLoaderInfo[] objList, StreamWriter writer, int o)
    {
        writer.WriteLine("\t<Object>");
        writer.WriteLine("\t\t<ObjectName>" + ObjectLoader.UniqueObjectNameEditor(objList[o]) + "</ObjectName>");
        writer.WriteLine("\t\t<Index>" + o + "</Index>");
        writer.WriteLine("\t\t<Address>" + objList[o].address + "</Address>");
        writer.WriteLine("\t\t<StaticProperties>");
        writer.WriteLine("\t\t\t<ItemID>" + objList[o].item_id + "</ItemID>");
        //writer.WriteLine("\t\t\t<InUse>" + objList[o].InUseFlag + "</InUse>");
        writer.WriteLine("\t\t\t<Flags>" + objList[o].flags + "</Flags>");
        writer.WriteLine("\t\t\t<Enchant>" + objList[o].enchantment + "</Enchant>");
        writer.WriteLine("\t\t\t<DoorDir>" + objList[o].doordir + "</DoorDir>");
        writer.WriteLine("\t\t\t<Invis>" + objList[o].invis + "</Invis>");
        writer.WriteLine("\t\t\t<IsQuant>" + objList[o].is_quant + "</IsQuant>");
        writer.WriteLine("\t\t\t<Texture>" + objList[o].Obsolete_texture + "</Texture>");
        writer.WriteLine("\t\t\t<Position>");
        writer.WriteLine("\t\t\t\t<ObjectTileX>" + objList[o].ObjectTileX + "</ObjectTileX>");
        writer.WriteLine("\t\t\t\t<ObjectTileY>" + objList[o].ObjectTileY + "</ObjectTileY>");
        writer.WriteLine("\t\t\t\t<heading>" + objList[o].heading + "</heading>");
        writer.WriteLine("\t\t\t\t<xpos>" + objList[o].xpos + "</xpos>");
        writer.WriteLine("\t\t\t\t<ypos>" + objList[o].ypos + "</ypos>");
        writer.WriteLine("\t\t\t\t<zpos>" + objList[o].zpos + "</zpos>");
        writer.WriteLine("\t\t\t</Position>");
        writer.WriteLine("\t\t\t<Quality>" + objList[o].quality + "</Quality>");
        writer.WriteLine("\t\t\t<Next>" + objList[o].next + "</Next>");
        writer.WriteLine("\t\t\t<Owner>" + objList[o].owner + "</Owner>");
        writer.WriteLine("\t\t\t<Link>" + objList[o].link + "</Link>");
        writer.WriteLine("\t\t</StaticProperties>");
        if (o < 256)
        {//mobile info
            writer.WriteLine("\t\t<MobileProperties>");
            writer.WriteLine("\t\t\t<npc_hp>" + objList[o].npc_hp + "</npc_hp>");
            writer.WriteLine("\t\t\t<ProjectileHeading>" + objList[o].ProjectileHeading + "</ProjectileHeading>");
            writer.WriteLine("\t\t\t<MobileUnk_0xA>" + objList[o].MobileUnk_0xA + "</MobileUnk_0xA>");

            writer.WriteLine("\t\t\t<npc_goal>" + objList[o].npc_goal + "</npc_goal>");
            writer.WriteLine("\t\t\t<npc_gtarg>" + objList[o].npc_gtarg + "</npc_gtarg>");
            writer.WriteLine("\t\t\t<AnimationFrame>" + objList[o].AnimationFrame + "</AnimationFrame>");
            int OriginX = (objList[o].AnimationFrame << 12) | (objList[o].npc_gtarg << 4) | objList[o].npc_goal & 0xF;
            writer.WriteLine("\t\t\t<CoOrdinateX>" + objList[o].CoordinateX + "</CoOrdinateX>");
            writer.WriteLine("\t\t\t<CoOrdinateY>" + OriginX + "</CoOrdinateY>");
            writer.WriteLine("\t\t\t<npc_level>" + objList[o].npc_level + "</npc_level>");
            writer.WriteLine("\t\t\t<MobileUnk_0xD_4_FF>" + objList[o].MobileUnk_0xD_4_FF + "</MobileUnk_0xD_4_FF>");
            writer.WriteLine("\t\t\t<MobileUnk_0xD_12_1>" + objList[o].MobileUnk_0xD_12_1 + "</MobileUnk_0xD_12_1>");
            writer.WriteLine("\t\t\t<npc_talkedto>" + objList[o].npc_talkedto + "</npc_talkedto>");
            writer.WriteLine("\t\t\t<npc_attitude>" + objList[o].npc_attitude + "</npc_attitude>");
            //int val = (npc_attitude << 13) | (npc_talkedto << 12) | (MobileUnk_0xD_12_1 << 11) | (MobileUnk_0xD_4_FF << 4) | (npc_level & 0xF);
            int OriginY = (objList[o].npc_attitude << 13) | (objList[o].npc_talkedto << 12) | (objList[o].MobileUnk_0xD_12_1 << 11) | (objList[o].MobileUnk_0xD_4_FF << 4) | (objList[o].npc_level & 0xF);
            writer.WriteLine("\t\t\t<CoOrdinateY>" + OriginY + "</CoOrdinateY>");

            writer.WriteLine("\t\t\t<MobileUnk_0xF_0_3F>" + objList[o].MobileUnk_0xF_0_3F + "</MobileUnk_0xF_0_3F>");
            writer.WriteLine("\t\t\t<npc_height>" + objList[o].npc_height + "</npc_height>");
            writer.WriteLine("\t\t\t<MobileUnk_0xF_C_F>" + objList[o].MobileUnk_0xF_C_F + "</MobileUnk_0xF_C_F>");
            writer.WriteLine("\t\t\t<MobileUnk_0x11>" + objList[o].MobileUnk_0x11 + "</MobileUnk_0x11>");
            writer.WriteLine("\t\t\t<ProjectileSourceID>" + objList[o].ProjectileSourceID + "</ProjectileSourceID>");
            writer.WriteLine("\t\t\t<MobileUnk_0x13>" + objList[o].MobileUnk_0x13 + "</MobileUnk_0x13>");
            writer.WriteLine("\t\t\t<Projectile_Speed>" + objList[o].Projectile_Speed + "</Projectile_Speed>");
            writer.WriteLine("\t\t\t<Projectile_Pitch>" + objList[o].Projectile_Pitch + "</Projectile_Pitch>");
            //writer.WriteLine("\t\t\t<Projectile_Sign>" + objList[o].Projectile_Sign + "</Projectile_Sign>");
            writer.WriteLine("\t\t\t<npc_voidanim>" + objList[o].npc_animation + "</npc_voidanim>");
           // writer.WriteLine("\t\t\t<MobileUnk_0x15_4_1F>" + objList[o].MobileUnk_0x15_4_1F + "</MobileUnk_0x15_4_1F>");
            writer.WriteLine("\t\t\t<MobileUnk_0x16_0_F>" + objList[o].MobileUnk_0x16_0_F + "</MobileUnk_0x16_0_F>");
            writer.WriteLine("\t\t\t<npc_yhome>" + objList[o].npc_yhome + "</npc_yhome>");
            writer.WriteLine("\t\t\t<npc_xhome>" + objList[o].npc_xhome + "</npc_xhome>");
            writer.WriteLine("\t\t\t<npc_heading>" + objList[o].npc_heading + "</npc_heading>");
            writer.WriteLine("\t\t\t<MobileUnk_0x18_5_7>" + objList[o].MobileUnk_0x18_5_7 + "</MobileUnk_0x18_5_7>");
            writer.WriteLine("\t\t\t<npc_hunger>" + objList[o].npc_hunger + "</npc_hunger>");
            writer.WriteLine("\t\t\t<MobileUnk_0x19_6_3>" + objList[o].MobileUnk_0x19_6_3 + "</MobileUnk_0x19_6_3>");
            writer.WriteLine("\t\t\t<npc_whoami>" + objList[o].npc_whoami + "</npc_whoami>");
            writer.WriteLine("\t\t</MobileProperties>");
        }
        writer.WriteLine("\t</Object>");
    }

    /// <summary>
    /// Gets what world is associated with the current level
    /// </summary>
    /// <param name="levelNo"></param>
    /// <returns></returns>
    public static Worlds GetWorld(int levelNo)
    {
        if (_RES != GAME_UW2) { return Worlds.Britannia; }
        switch ((UW2_LevelNos)levelNo)
        {
            case UW2_LevelNos.Britannia0:
            case UW2_LevelNos.Britannia1:
            case UW2_LevelNos.Britannia2:
            case UW2_LevelNos.Britannia3:
            case UW2_LevelNos.Britannia4:
                return Worlds.Britannia;
            case UW2_LevelNos.Prison0:
            case UW2_LevelNos.Prison1:
            case UW2_LevelNos.Prison2:
            case UW2_LevelNos.Prison3:
            case UW2_LevelNos.Prison4:
            case UW2_LevelNos.Prison5:
            case UW2_LevelNos.Prison6:
            case UW2_LevelNos.Prison7:
                return Worlds.PrisonTower;
            case UW2_LevelNos.Killorn0:
            case UW2_LevelNos.Killorn1:
                return Worlds.Killorn;
            case UW2_LevelNos.Ice0:
            case UW2_LevelNos.Ice1:
                return Worlds.Ice;
            case UW2_LevelNos.Talorus0:
            case UW2_LevelNos.Talorus1:
                return Worlds.Talorus;
            case UW2_LevelNos.Academy0:
            case UW2_LevelNos.Academy1:
            case UW2_LevelNos.Academy2:
            case UW2_LevelNos.Academy3:
            case UW2_LevelNos.Academy4:
            case UW2_LevelNos.Academy5:
            case UW2_LevelNos.Academy6:
            case UW2_LevelNos.Academy7:
                return Worlds.Academy;
            case UW2_LevelNos.Tomb0:
            case UW2_LevelNos.Tomb1:
            case UW2_LevelNos.Tomb2:
            case UW2_LevelNos.Tomb3:
                return Worlds.Tomb;
            case UW2_LevelNos.Pits0:
            case UW2_LevelNos.Pits1:
            case UW2_LevelNos.Pits2:
                return Worlds.Pits;
            case UW2_LevelNos.Ethereal0:
            case UW2_LevelNos.Ethereal1:
            case UW2_LevelNos.Ethereal2:
            case UW2_LevelNos.Ethereal3:
            case UW2_LevelNos.Ethereal4:
            case UW2_LevelNos.Ethereal5:
            case UW2_LevelNos.Ethereal6:
            case UW2_LevelNos.Ethereal7:
            case UW2_LevelNos.Ethereal8:
                return Worlds.Ethereal;
            default:
                Debug.Log("Unknown level/world");
                return Worlds.Ethereal;
        }
    }
}
