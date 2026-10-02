# Unity Streaming Integration Guide — Procedural City Generator V19

This document is the runtime contract between `retro_citygen_v19.py` and the Unity RPG's existing streaming world.

The city generator remains **1 Unity unit / ~1 metre** internally. Unity's streaming world is divided into **64 × 64 Unity-unit map pixels**. V19 does not force roads, buildings, fields, plazas, or districts to fit inside those 64 m squares. Instead, it spatially indexes generated settlement content onto the map-pixel grid.

---

## 1. Core principle

There are two coordinate systems:

### Settlement-local generator coordinates

- integer coordinates;
- `1 generator unit = 1 Unity unit ≈ 1 metre`;
- `x` increases east/right;
- `y` increases south/down;
- `(0,0)` is the northwest corner of the generator canvas;
- roads/building edges remain cardinal or true 45° diagonals.

These are the authoritative geometry coordinates.

### World map-pixel coordinates

- each map pixel is `64 × 64` Unity units by default;
- `map_pixel_x` increases east/right;
- `map_pixel_y` increases south/down;
- settlement-local `(0,0)` is anchored exactly to the northwest corner of the map pixel passed with `--world-map-x` and `--world-map-y`.

For example:

```text
--world-map-x 8421
--world-map-y 3760

world unit origin:
X = 8421 * 64 = 538944
Y = 3760 * 64 = 240640
```

A settlement-local point `(137,92)` therefore has top-down world coordinates:

```text
(539081, 240732)
```

and is indexed to the appropriate 64 m map pixel.

### Unity X/Z warning

The exporter intentionally stores the second horizontal axis as **top-down Y**, not as Unity Z.

Do not assume that exported `world_unit_y` equals Unity `transform.position.z`.

The Unity importer must route these coordinates through the RPG's existing world-coordinate adapter:

```text
generator/world-map X,Y
        ↓
existing Daggerfall-style world coordinate conversion
        ↓
Unity X,Z
```

If the project defines Unity +Z as south, the mapping may be `z = y`. If +Z is north, it will require inversion/offset. This is an engine-level convention and must be handled in one central adapter, not scattered throughout city loading code.

---

## 2. Command-line export

Example:

```bash
python retro_citygen.py \
    --seed 1847 \
    --population 8000 \
    --road-entries 00100010 \
    --world-map-x 8421 \
    --world-map-y 3760 \
    --settlement-id CITY01847 \
    --unity-streaming-export ./CITY01847 \
    --out city_01847.svg
```

`--world-map-z` is accepted as an alias for `--world-map-y` if the calling world generator naturally calls its second horizontal axis Z.

The default map-pixel side is 64 units. It can technically be changed with `--map-pixel-size`, but the RPG should leave it at 64.

`--unity-streaming-export` requires both world-map coordinates.

The settlement origin is always map-pixel aligned:

```text
world_unit_origin_x % 64 == 0
world_unit_origin_y % 64 == 0
```

---

## 3. Output package

A streaming export is self-contained:

```text
CITY01847/
├── settlement.json
├── entities.json
├── districts.json
├── source_city.json
├── chunks/
│   ├── 8421_3761.json
│   ├── 8422_3761.json
│   ├── ...
└── interiors/
    └── README.txt
```

### `settlement.json`

Small manifest and spatial index.

Contains:

- settlement ID and seed;
- population;
- coordinate-system contract;
- map-pixel size;
- world-map pixel origin;
- world-unit origin;
- urban map-pixel bounds;
- full settlement map-pixel bounds;
- road-entry mask;
- future world-road connector metadata (stored only; no runtime road-network integration is assumed yet);
- chunk index;
- file paths;
- entity ownership rules.

Load this first.

### `entities.json`

Settlement-level entity registry.

Contains:

- buildings;
- doors;
- parent street/network entities;
- special sites;
- fringe plots;
- farm fields;
- farmsteads.

For the current prototype it is acceptable to load this once when the settlement enters the active streaming region. If profiling later shows that very large capitals make this too expensive, this registry can be sharded without changing the chunk ownership model.

### `districts.json`

All barrio/district metadata and geometry.

Contains stable district IDs/seeds, social/economic identity, wealth, density, prestige, anchor, tags, block membership, geometry, and the map pixels touched by the district.

District names remain `null` until the later naming system is implemented.

### `source_city.json`

The full generator-native V19 city JSON.

This is primarily for debugging, tooling, regeneration checks, editor inspection, and future migration. Runtime code should generally consume the streaming schema instead.

### `chunks/<x>_<y>.json`

One file per 64 × 64 world map pixel that contains settlement content.

Chunk files are the streaming unit.

### `interiors/`

Reserved for future per-building interior files.

Recommended future convention:

```text
interiors/B000417.json
```

Outdoor streaming does not load full interiors.

---

## 4. Entity ID rules

### Settlement-local IDs

Examples:

```text
B000417
B000417-D01
S00031
D04
F000027
SP0003
FR0012
FS0004
```

These are compact and stable *within the settlement*.

### Global entity IDs

The streaming entity registry also supplies namespaced IDs:

```text
CITY01847:B000417
CITY01847:B000417-D01
CITY01847:S00031
CITY01847:D04
```

Use namespaced `entity_id` values for:

- save-game state;
- quests;
- persistent NPC references;
- ownership/state changes;
- cross-settlement systems.

Chunk files use compact local IDs because the settlement context is already known.

---

## 5. Building streaming ownership

Buildings may freely cross 64 m map-pixel boundaries.

Do **not** split or duplicate them.

Every building exports:

```json
{
  "local_entity_id": "B000417",
  "entity_id": "CITY01847:B000417",

  "home_map_pixel_xy": [8423, 3762],

  "overlapping_map_pixels_xy": [
    [8423, 3762],
    [8424, 3762],
    [8423, 3763]
  ]
}
```

The **home map pixel** is the pixel containing the building centroid.

It is the canonical owner for indexing/persistence.

Other intersected chunks reference the same building.

### Runtime rule

Maintain a per-settlement building activation/refcount table.

When a chunk loads:

```text
for owned_building_ids + referenced_building_ids:
    increment building active reference count
    if count changes 0 -> 1:
        instantiate building
```

When a chunk unloads:

```text
decrement active reference count
if count changes 1 -> 0:
    unload building exterior
```

This guarantees that a cathedral crossing four map pixels is instantiated exactly once and remains alive while any relevant chunk is active.

**Do not require the home chunk itself to remain loaded.** The home chunk is canonical ownership metadata, not a prerequisite for rendering.

---

## 6. Doors

Doors remain children of buildings.

A door has:

```json
{
  "door_id": "B000417-D01",
  "entity_id": "CITY01847:B000417-D01",
  "building_id": 417,
  "building_entity_id": "CITY01847:B000417",
  "role": "primary_entrance"
}
```

Chunk files expose `door_ids_present` only as a spatial convenience.

**Never instantiate a second independent door because it appears in a chunk.**

Instantiate building doors as part of the building object.

The midpoint spatial record is useful for:

- interaction indexing;
- navigation entry points;
- proximity queries;
- map markers.

Paired zaguán doors preserve `portal_group_id` and `paired_door_id` for the future interior generator.

---

## 7. Streets and roads

Streets are handled differently from buildings.

`entities.json` stores a stable parent street entity:

```text
S00031
```

with its complete settlement-local path.

Chunk files contain **clipped geometry segments**:

```json
{
  "segment_id": "S00031@8423_3762#01",
  "street_id": "S00031",
  "kind": "civic",
  "width_units": 9,
  "points_map_pixel_local_xy": [...]
}
```

These are chunk-owned render/navigation geometry.

Recommended runtime:

```text
load pixel
  → instantiate that pixel's street segments

unload pixel
  → destroy those segment objects
```

The stable parent `street_id` allows:

- route continuity;
- future street naming;
- navigation graph stitching;
- district-boundary logic;
- address generation.

Do not treat clipped segments as independent streets.

---

## 8. Continent-wide road connectors

The explicit eight-bit road-entry mask remains authoritative:

```text
N, NE, E, SE, S, SW, W, NW
```

Only enabled directions create **potential major world-road connection stubs**. V19 does not assume that the Unity game already has a continent-wide road graph.

Each connector exports a stable connector ID such as:

```text
CITY01847-ROAD-E
```

and three spatial points:

1. `city_entry_point` — where the major route meets the developed settlement;
2. `major_road_outer_point` — where the broad urban approach transitions outward;
3. `world_connector_point` — reserved future handoff point for a continent-level road graph.

Each point also gets:

- settlement-local coordinate;
- world-unit coordinate;
- global map pixel;
- local coordinate inside that map pixel.

For the **current Unity milestone**, simply deserialize and retain these connector records. They are useful for debug visualization and future world generation, but no runtime road-network manager is required.

Later, when a continent-wide road graph exists, it can associate one of its road edges with a connector ID such as `CITY01847-ROAD-E`. The intended future relation is:

```text
future continent road edge
        ↓
CITY01847-ROAD-E
        ↓
local approach road
        ↓
principal thoroughfare
        ↓
market/plaza system
```

Until then, treat connectors as **dormant metadata**. Do not infer external connections from arbitrary farm tracks or legacy streets.

---

## 9. Fields and agricultural terrain

Fields can cross map-pixel boundaries.

Unlike buildings, their visible geometry is already clipped into each chunk as `field_slices`.

Each field slice records:

- stable parent field ID;
- land-use kind;
- agricultural zone;
- one or more chunk-local polygons.

These polygons use map-pixel-local coordinates, normally in the range `0..64`.

This allows field rendering to be purely chunk-scoped.

The full parent field remains in `entities.json` for semantic systems.

---

## 10. Plaza

The plaza is settlement-level conceptually but exported as clipped `plaza_slices` in affected chunks.

Render the per-chunk geometry.

Do not create a second independent plaza entity for every slice.

---

## 11. Districts/barrios

Districts remain settlement-level semantic entities.

Chunk files contain:

```json
"district_ids": ["D03", "D04"]
```

for every district touching that map pixel.

Buildings also carry their own `district_id`.

This gives three query paths:

- building → district directly;
- current chunk → possible districts;
- position → test against district polygons if exact barrio-at-position is needed.

District IDs/seeds are already suitable for the future district-name generator.

---

## 12. Special sites, fringe plots, and farmsteads

Chunk files list intersecting IDs:

```text
special_site_ids
fringe_plot_ids
farmstead_ids
```

Their semantic metadata is in `entities.json`.

Their contained buildings still use the normal building lifecycle.

This prevents a farmstead building from becoming a separate incompatible streaming entity type.

---

## 13. Chunk schema

Every chunk contains:

```text
map_pixel_xy
map_pixel_size_units
world_bounds_units_xy
settlement_local_bounds_xy

district_ids

owned_building_ids
referenced_building_ids
door_ids_present

street_segments
field_slices
plaza_slices

special_site_ids
fringe_plot_ids
farmstead_ids

road_connector_ids
```

The chunk file itself contains no duplicate full building geometry.

---

## 14. Recommended Unity runtime architecture

Suggested components:

```text
WorldMapStreamingManager
    existing world/map-pixel streaming authority

SettlementStreamingManager
    knows active settlements and their manifests

SettlementRuntime
    one loaded settlement
    manifest
    entity registry
    district registry
    active building refcounts
    chunk runtimes

SettlementChunkRuntime
    one active 64x64 map pixel
    street segment GameObjects
    field/plaza renderers
    references to active building IDs

BuildingRuntime
    one canonical exterior instance
    shell/floors/roof
    door children
    later: NPC/home/business state

WorldCoordinateAdapter
    ONLY class that converts exported top-down XY to Unity XYZ
```

Avoid putting coordinate-conversion code directly in building, road, or door classes.

---

## 15. Recommended load sequence

When a settlement enters streaming range:

```text
1. Read settlement.json.
2. Verify schema version.
3. Read entities.json.
4. Read districts.json.
5. Read and retain road connectors as metadata only; optionally expose them in debug gizmos. Do not require a world-road system.
6. Wait for normal map-pixel streaming callbacks.
```

When map pixel `(x,y)` activates:

```text
1. Read chunks/x_y.json.
2. Activate owned + referenced buildings through refcount registry.
3. Instantiate clipped street segments.
4. Instantiate field slices.
5. Instantiate plaza slice if present.
6. Register local district IDs.
7. Register connector marker if present.
```

When it deactivates:

```text
1. Remove chunk-owned street/field/plaza objects.
2. Decrement building references.
3. Destroy building exterior only when reference count reaches zero.
4. Remove chunk-level spatial registrations.
```

When player leaves the entire settlement region:

```text
1. Ensure all chunks are inactive.
2. Persist dirty building/door/NPC state by namespaced entity ID.
3. Unload entity and district registries.
4. Keep only world-level settlement metadata if needed.
```

---

## 16. Geometry construction in Unity

Do not regenerate the city algorithmically in Unity.

Python is the authoritative generator.

Unity should construct geometry from exported data.

For each building:

```text
outer polygon
holes/courtyards
stories
floor heights
vertical features
doors
```

Recommended generation order:

```text
footprint polygon
→ floor slab/walls
→ carve courtyard holes
→ extrude stories
→ roof/parapet
→ place door openings
→ later windows/decorations
```

All horizontal geometry is already on the 1 m integer/45° grid.

---

## 17. Building vertical data

Buildings export:

```text
stories
ground_floor_height_m
upper_floor_height_m
eave_height_m
vertical_features
```

`stories` means ordinary usable floors.

Bell towers, civic towers, etc. are separate `vertical_features`.

Do not render a bell tower by interpreting it as extra normal building stories.

---

## 18. Interior streaming

Future interiors should remain separate from outdoor chunk JSON.

Recommended contract:

```text
building B000417 exterior active outdoors
        ↓ player uses door
load interiors/B000417.json
        ↓
spawn interior
```

Door IDs are already stable enough to serve as transition endpoints.

Zaguán `portal_group_id` data should constrain future interior layout.

---

## 19. Save-game ownership

Use **global entity IDs**, never raw array indexes.

Good:

```text
CITY01847:B000417
CITY01847:B000417-D03
```

Bad:

```text
building index 416
chunk-local building #12
```

Likewise, future NPC IDs should be settlement/world namespaced.

A save should record semantic changes, not generated baseline data:

```text
door locked/unlocked
shop owner dead/alive
building burned
quest state
container inventory delta
NPC state
```

Regenerate/load baseline city, then apply persistent deltas.

---

## 20. Floating-origin / huge-world caution

The package exposes absolute top-down world-unit coordinates for debugging and indexing.

If the RPG uses a floating-origin system, **do not instantiate GameObjects at those raw absolute coordinates**.

Use the existing world streaming origin to convert:

```text
global map pixel + map-pixel-local position
        ↓
current local Unity scene position
```

This is one reason chunk-local geometry is explicitly exported.

---

## 21. Data validation Unity should perform

On import:

```text
settlement schema version supported
map pixel size == 64
world origin map-pixel aligned
all chunk files named in manifest exist
every building referenced by chunks exists
every building has exactly one home/owner pixel
all referenced pixels occur in building overlap list
door building IDs resolve
street segment parent IDs resolve
district IDs resolve
connector directions agree with road-entry mask
chunk-local geometry lies within 0..64 (+ boundary epsilon)
```

Fail loudly in editor/development builds.

---

## 22. Current sample scale

Seed 1847, population 8,000, E/W **future road-entry stubs**:

```text
716 buildings
1,763 doors
208 parent streets
60 farm fields
311 occupied streaming map pixels
249 buildings cross at least one map-pixel boundary
largest building overlap: 4 map pixels
```

This is exactly why canonical ownership/reference handling is necessary.

---

## 23. What Codex should NOT do

Do not:

- port the procedural city generator to C#;
- quantize buildings to 64 m squares;
- split cross-pixel buildings into independent buildings;
- create doors independently of their building;
- infer world roads from any road that reaches farmland;
- assume exported top-down Y equals Unity Z;
- load interiors with outdoor chunks;
- use array positions as persistent IDs;
- instantiate raw absolute coordinates if the RPG already uses a floating origin.

---

## 24. Future-compatible extensions

The schema is deliberately ready for:

- district names;
- street names;
- generated interiors;
- windows and façade decorations;
- households/NPCs;
- businesses/inventories;
- quest locations;
- ownership/state changes;
- future world-road edge IDs (once a world road network exists);
- terrain/elevation;
- water/bridges;
- city walls/gates.

These should extend stable entities rather than change the map-pixel ownership model.
