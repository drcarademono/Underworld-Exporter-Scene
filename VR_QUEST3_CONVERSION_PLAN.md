# Meta Quest 3 VR Conversion Plan

## Goal
Convert this Unity project into a comfortable, performant VR game targeting **Meta Quest 3** using **OpenXR**, while preserving core gameplay systems and adapting interaction, UI, and controls for room-scale + seated VR play.

---

## 1) Discovery & Baseline Audit (Week 0)

### 1.1 Technical inventory
- Confirm Unity version and render pipeline in use.
- Identify player controller stack (camera, movement, input, combat, interaction, menus).
- Enumerate UI systems (HUD canvases, world-space vs screen-space, legacy input hooks).
- List camera effects incompatible with VR comfort (camera bob, screen shake, post effects).

### 1.2 Build + performance baseline
- Produce baseline Android/PC builds and capture:
  - CPU/GPU frame timing
  - draw calls / batches
  - overdraw hotspots
  - memory footprint and GC allocations
- Document current feature parity and known regressions.

### 1.3 Output
- Create a “VR migration matrix” mapping each subsystem to one of:
  - keep as-is
  - adapt
  - replace

---

## 2) Platform Setup for Quest 3 + OpenXR (Week 1)

### 2.1 Unity package setup
- Install/verify:
  - XR Plugin Management
  - OpenXR plugin
  - XR Interaction Toolkit (XRI)
  - Input System package (if not already primary)
- Enable OpenXR for Android build target.
- Activate Quest/OpenXR interaction profiles (Touch Plus controllers, hand tracking optional phase).

### 2.2 Project settings (Android/Quest)
- Target Android API and architecture required for Quest deployment.
- IL2CPP + ARM64.
- Vulkan first, fallback GLES3 only if required by content/shaders.
- Configure fixed foveated rendering and dynamic resolution hooks.
- Set color space and quality tiers tuned for standalone VR.

### 2.3 Fullscreen behavior
- Replace desktop “fullscreen toggle” assumptions with VR presentation model:
  - in-HMD immersive display is always full-screen by design.
  - disable windowed-only UI pathways and desktop resolution switchers in VR mode.
- Keep non-VR fallback path for editor/desktop testing.

---

## 3) Player Rig Migration to OpenXR (Weeks 1–2)

### 3.1 Introduce XR origin
- Add XR Origin (camera offset, tracked head, left/right controllers).
- Create `VRPlayerRig` prefab as the canonical VR rig.
- Preserve existing player state object (stats, inventory, quest data) and bind to XR rig.

### 3.2 Decouple camera assumptions
- Remove hard dependencies on a single monoscopic camera transform.
- Route “look direction” queries through an abstraction:
  - `IViewPoseProvider` for head/gaze
  - `IAimPoseProvider` for dominant hand / gaze fallback

### 3.3 Motion + collision
- Replace/augment existing character motor with XRI-compatible locomotion controller.
- Maintain capsule/body collision anchored to HMD-relative position.
- Add step offset and slope limits tuned for comfort and level geometry.

---

## 4) Controls & Input Action Redesign (Weeks 2–3)

### 4.1 Input architecture
- Migrate from legacy input polling to Input Actions (if still legacy-driven).
- Define action maps:
  - `Locomotion`
  - `Interaction`
  - `Combat`
  - `UI`
  - `System`

### 4.2 Suggested Quest 3 mappings
- Left stick: move
- Right stick: snap turn (default), optional smooth turn
- A/X: primary interact / confirm (contextual)
- B/Y: secondary action / cancel
- Grip: grab/hold mode
- Trigger: use/attack/cast
- Menu button: pause/system menu

### 4.3 Accessibility and comfort options
- Snap vs smooth turning
- Vignette during movement
- Height calibration (standing/seated)
- Dominant hand toggle
- Locomotion speed presets

---

## 5) Interaction Model Conversion (Weeks 3–4)

### 5.1 Core interaction
- Replace center-screen raycast interactions with hand/controller rays + direct interactor volumes.
- Keep legacy interaction backend where possible; add adapter layer from XRI events.

### 5.2 Object handling
- Convert pickup/use objects to XR grabbables where appropriate.
- Decide object classes:
  - direct hand grab (small items)
  - ray-select then confirm (distant/precise items)
  - non-grabbable interactables (levers, doors, runes, UI widgets)

### 5.3 Combat in VR
- Separate melee and ranged schemes:
  - Melee: velocity/gesture threshold + anti-exploit clamping.
  - Ranged/magic: hand-aim projectile origin with optional aim assist.
- Rework hit feedback to avoid aggressive camera motion.

---

## 6) UI/UX Adaptation for VR (Weeks 4–6)

### 6.1 Replace screen-space HUD
- Migrate to world-space/diegetic UI:
  - wrist HUD for quick stats
  - floating anchored panels for inventory/map/spells
- Maintain readable angular size and depth-safe placement.

### 6.2 Menu flow
- Rebuild pause/options/character screens as VR panels.
- Ensure every action is controller navigable (no mouse-only flow).
- Add XR pointer + direct poke support where appropriate.

### 6.3 Typography and readability
- Increase font sizes and contrast for headset optics.
- Avoid thin strokes and tiny iconography.
- Standardize minimum distance and panel scale rules.

---

## 7) Comfort Hardening (Weeks 5–6)

### 7.1 Remove discomfort sources
- Disable camera bob/shake/head-jolt scripts in VR mode.
- Review post-processing (motion blur, chromatic aberration, aggressive DOF).
- Avoid forced camera cuts; use fades for teleports/transitions.

### 7.2 Locomotion comfort systems
- Snap turn by default.
- Optional teleport locomotion for sensitive users.
- Dynamic vignette and horizon stabilization options.

---

## 8) Performance Optimization for Standalone Quest 3 (Weeks 6–8)

### 8.1 Frame budget targets
- Target stable framerate suitable for VR comfort (72/90Hz modes as feasible).
- Lock profiling sessions on-device, not editor-only.

### 8.2 Rendering optimization
- Reduce transparent overdraw (especially particles/UI).
- Bake lighting where possible; constrain real-time shadows.
- Add LODs/occlusion culling for dense scenes.
- Texture memory review + compression strategy for mobile VR.

### 8.3 Script/runtime optimization
- Remove per-frame allocations in hot paths.
- Pool frequently spawned objects (projectiles, fx, UI elements).
- Profile physics cost from interactables and NPCs.

---

## 9) Audio & Haptics (Week 8)

- Ensure 3D spatial audio works with head tracking.
- Add haptic feedback profile for:
  - grab confirm
  - UI click
  - hit/block/spell cast
- Tune haptics by interaction class to avoid fatigue.

---

## 10) QA, Playtesting, and Certification Readiness (Weeks 9–10)

### 10.1 Test matrix
- Hardware: Quest 3 standalone (primary), Quest Link (secondary), editor XR simulation.
- Gameplay: movement, combat, interaction, inventory, save/load, scene transitions.
- Comfort: seated/standing, left/right-handed, smooth/snap locomotion.

### 10.2 Regression gates
- No critical blocker in 30-minute comfort session.
- No controller dead-ends in UI.
- No major FPS drops in target-heavy scenes.

### 10.3 Release preparation
- Finalize Android manifest/permissions.
- Validate startup flow and recenter behavior.
- Produce release notes and known-issues list.

---

## 11) Suggested Implementation Backlog (Epics)

1. **XR Foundation**
   - OpenXR + Android Quest setup
   - XR Origin integration
2. **Input Migration**
   - Action maps + rebinding support
3. **VR Locomotion & Comfort**
   - movement modes + vignette + turn options
4. **Interaction Layer**
   - hand/ray interaction adapters for legacy object logic
5. **VR UI Overhaul**
   - inventory/map/spellbook and pause/options in world-space
6. **Combat Rework**
   - melee/ranged VR tuning + haptics
7. **Performance Pass**
   - render/script/physics optimization for Quest
8. **QA & Stabilization**
   - bug burn-down and release candidate

---

## 12) Risk Register & Mitigations

- **Risk:** Legacy systems tightly coupled to mouse + screen center.
  - **Mitigation:** Adapter interfaces (`IInteractionSource`, `IViewPoseProvider`) and phased replacement.
- **Risk:** UI complexity too high for direct 1:1 port.
  - **Mitigation:** Prioritize task flows; redesign into fewer contextual panels.
- **Risk:** Performance regression on standalone hardware.
  - **Mitigation:** On-device profiling from week 1 and strict performance budgets.
- **Risk:** Motion sickness from original movement/camera feel.
  - **Mitigation:** Comfort-first defaults, disable camera effects, broaden locomotion options.

---

## 13) Definition of Done (VR Milestone)

- Game boots and runs on Meta Quest 3 using OpenXR.
- Player uses XR rig for head/controller tracking.
- Core gameplay loop (move, interact, combat, inventory, save/load) is fully playable in VR.
- All critical UI is accessible in VR without mouse/keyboard.
- Comfort options exposed and functional.
- Performance hits target framerate in representative scenes.
- No P0/P1 blockers in final QA pass.
