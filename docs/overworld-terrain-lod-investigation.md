# Overworld Terrain Seam Investigation (May 17, 2026)

## Scope reviewed

- `Assets/scripts/World/GameWorldController.cs` (chunk scheduling, chunk build pipeline, mesh generation, water/terrain classing, seam skirts).
- `Assets/scripts/World/OverworldTerrainController.cs` (runtime tuning knobs controlling chunk size/radius/decimation).

## What the current system does

1. Chunks are generated in high-detail near the player and low-detail farther away.
2. Far chunks are made coarse by increasing `sampleStep` and by additionally snapping heights to a coarser `geometrySampleStep` (`baseSampleStep * TerrainDecimationStep`).
3. When `geometrySampleStep > 1`, an extra "ChunkSkirt" mesh is generated around all four chunk edges.
4. Each skirt quad is angled outward/downward and textured with the same material set used by the terrain surfaces.

This hides visual cracks between chunks with different geometric fidelity, but it introduces edge artifacts:

- visible vertical/angled walls on ridges and coastlines,
- silhouette pops during chunk promotion/demotion,
- texture stretching and lighting mismatch on steep borders,
- overdraw and extra draw-call/triangle cost for every distant chunk.

## Root cause

The crack is not a rendering bug; it is a topology mismatch:

- High-detail chunks preserve more vertex samples.
- Low-detail chunks represent the same boundary with fewer control points / coarser snapped heights.
- Adjacent meshes therefore do not share an identical border polyline.

Skirts conceal the gap after the fact, instead of enforcing border compatibility at mesh build time.

## Better solution: **Edge-conforming LOD with a morph ring**

### 1) Enforce shared border vertices (replace skirts)

For each chunk, derive the final border from neighbors before triangulation:

- Keep independent interior decimation.
- Make border sampling step equal to the *finest* neighbor touching that edge.
- Sample border heights from the same source function (`SampleTerrainHeightAt`) so both sides produce identical XYZ values.
- Triangulate the interior to that border using an adaptive grid/T-junction-safe pattern (or split edge segments where needed).

Result: watertight geometry without any hidden geometry walls.

### 2) Add a narrow geomorph band (reduce popping)

Even with watertight borders, full chunk swaps can pop.

Use a 1-2 cell "morph ring" inside each chunk edge:

- Keep target positions for current LOD and next LOD.
- Blend vertex Y (and optionally XZ for full geomorph) using a distance-based or hysteresis-driven `morphAlpha` over ~0.3-0.8s.
- Update normals from morphed geometry.

Result: smoother transition as chunks change LOD level.

### 3) Introduce LOD hysteresis and asynchronous neighbor-aware rebuilds

Current queueing already supports rebuild requests; extend policy:

- Promote to higher detail when entering radius R.
- Demote only after leaving radius R + H (hysteresis margin).
- When one chunk changes LOD, mark touching neighbors "edge dirty" and rebuild only border/index buffers when possible.

Result: fewer oscillations and stable seams around the player.

## Why this is better than skirts

- **Visual quality:** no exposed walls, better skyline/coast silhouettes.
- **Lighting correctness:** no abrupt normal discontinuity from artificial vertical faces.
- **Performance:** fewer extra seam triangles and less overdraw in distance.
- **Deterministic seams:** border identity is guaranteed by construction.

## Implementation sketch in this codebase

1. Add per-edge descriptors in chunk build output:
   - edge sample step,
   - edge vertex positions,
   - optional cached hash for dirty checks.
2. In `BuildChunk(...)`, compute intended LOD, then query neighbor intended LOD (or pending requests) before creating vertices.
3. Build border vertex arrays first (N/S/E/W), then fill interior.
4. Replace `AddDistantChunkSkirt(...)` call path with edge-conforming border generation.
5. Add morph data buffers (`currY`, `targetY`) and blend in update/coroutine while chunk is active.
6. Keep skirts as debug fallback only (feature flag).

## Incremental rollout plan

1. **Phase A:** Edge-conforming borders only (no morph), skirts disabled behind flag.
2. **Phase B:** Morph ring for Y only + hysteresis thresholds.
3. **Phase C:** Border-only partial rebuild optimization + telemetry (build ms, seam mismatch counters).

## Validation checklist

- Zero visible cracks with mixed LOD rings around player.
- No chunk-border wall silhouettes at sunrise/sunset directional lighting.
- Stable frame time during traversal (no oscillatory rebuild churn).
- Coastline continuity preserved at all LOD boundaries.
- Water contact colliders still aligned where required.

## Notes

If desired, I can implement **Phase A** directly in `GameWorldController` next, behind a runtime toggle for A/B comparison against the current skirt approach.
