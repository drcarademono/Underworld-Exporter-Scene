# Codex Implementation Brief — Integrate Procedural Cities into Unity Streaming World

## Goal

Integrate the V19 Python city output into the existing Unity RPG world streamer, where one world map pixel is exactly 64 × 64 Unity units.

Do **not** rewrite city generation in C#.

The Python output is authoritative.

Read `UNITY_STREAMING_INTEGRATION.md` before changing code.

## Input package

A city package contains:

```text
settlement.json
entities.json
districts.json
source_city.json
chunks/*.json
interiors/
```

Start with `settlement.json`.

## Required implementation

### 1. JSON model layer

Create serializable runtime DTOs for:

- Settlement manifest
- Chunk manifest/index entry
- Chunk file
- Building
- Door
- Street parent
- Street segment
- District
- Field slice
- Road connector

Preserve unknown/additional fields where practical so schema growth is not brittle.

Use the JSON library already standard in the project. Do not add a second serializer unless necessary.

### 2. Coordinate adapter

Find the project's existing conversion between world map-pixel coordinates and Unity scene coordinates.

Create exactly one city adapter around it.

Exporter convention:

```text
top-down X increases east
top-down Y increases south
1 unit = 1 Unity unit
1 map pixel = 64 units
```

Never scatter `y -> z` or sign inversions throughout the importer.

### 3. Settlement runtime

Implement one runtime object per active settlement that owns:

- manifest;
- entity lookup dictionaries;
- district lookup;
- building active-reference counts;
- instantiated building handles;
- loaded chunk handles.

Key dictionaries should use stable IDs, not array indexes.

### 4. Chunk activation

Hook into the existing 64×64 map-pixel streaming callbacks.

On chunk activation:

```text
load its chunk JSON
activate owned_building_ids
activate referenced_building_ids
spawn chunk street segments
spawn field slices
spawn plaza slices
register districts/connectors
```

On chunk unload, perform the inverse.

### 5. Cross-chunk building ownership

A building may overlap several map pixels.

Instantiate it once.

Use reference counting across active chunks.

`home_map_pixel_xy` is canonical ownership metadata, not a requirement that the home chunk stay loaded.

### 6. Building exterior prototype

For initial integration, construct a simple retro exterior:

- footprint polygon;
- courtyard holes;
- extruded wall mass;
- correct story count and floor/eave heights;
- simple roof/parapet;
- door markers/openings.

Do not yet implement interiors or detailed façade decorations.

### 7. Door objects

Doors are children of buildings.

Use stable door IDs.

Do not instantiate doors from `door_ids_present` independently.

Expose door ID, role, portal group, paired door, and target unit data to gameplay scripts.

### 8. Roads

Use chunk-clipped `street_segments` for local rendering/navigation geometry.

Retain parent `street_id`.

Do not reconstruct the full road in every chunk.

### 9. Road-entry connector metadata

There is **no continent-wide road-network system in the Unity project yet**.

Deserialize and retain `road_connectors` from `settlement.json`, but do not invent or build a runtime world-road graph as part of this milestone.

Use connector records only for:

- debug gizmos/inspection;
- validating that requested road-entry directions exist;
- preserving stable future handoff points for a later continent-road implementation.

Do not use farm tracks or legacy streets as external connectors.

### 10. Districts

Load district metadata once per settlement.

Buildings already carry `district_id`.

Expose an API such as:

```csharp
DistrictRuntime GetDistrict(string districtId);
DistrictRuntime GetDistrictForBuilding(string buildingId);
```

Leave district name null/unnamed for now.

### 11. Development validation

Add editor/development assertions for:

- schema version;
- map pixel size 64;
- missing entity IDs;
- duplicate building instantiation;
- bad chunk references;
- invalid door/building links;
- missing district IDs;
- connector/mask mismatch;
- chunk-local coordinates outside 0..64.

### 12. Debug visualization

Add optional gizmos/debug rendering for:

- map-pixel borders;
- building home pixel;
- cross-pixel building references;
- district ID;
- road connector IDs;
- door IDs;
- street parent IDs.

This will be essential during integration.

## Recommended class shape

```text
ProceduralSettlementDatabase
ProceduralSettlementRuntime
ProceduralSettlementChunk
ProceduralBuildingRuntime
ProceduralDoorRuntime
ProceduralDistrictRuntime
ProceduralWorldCoordinateAdapter
```

Match existing project naming conventions where possible instead of forcing these exact names.

## Important implementation constraints

Do not change existing world streaming behavior unless necessary.

Prefer an adapter that feeds procedural settlement content into the same lifecycle already used for ordinary world-map pixels.

Do not instantiate all 311 sample city chunks at once.

Do not make one GameObject per 1 m tile.

Generate meshes in sensible batches:

- building object / building mesh;
- road segment batch per chunk;
- field surface batch per chunk.

## First milestone

A successful first milestone is:

1. Load seed-1847 sample package.
2. Enter its world-map region.
3. Chunks stream in/out with existing world system.
4. Roads and fields appear only in active pixels.
5. Buildings crossing pixel boundaries never duplicate/disappear prematurely.
6. Building heights are visible.
7. Doors are present with correct IDs.
8. E/W road-entry connector stubs deserialize correctly and can be visualized; no world-road system is required yet.
9. Leaving the settlement unloads it cleanly.

Do not implement NPCs, interiors, windows, district naming, or quest systems in this milestone.
