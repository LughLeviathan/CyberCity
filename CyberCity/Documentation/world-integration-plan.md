# World integration — approved continuation

Spec: user brief, stage-2 review and approval with slightly wider roads (2026-10-26).
Base: d2badc0. Branch: feature/gameplay-revision. Existing ProjectSettings.asset edits are user-owned.

Ruling: approval covers stages 3–5; widen selected roads by 8–12 px without relocating the six buildings or changing the 1024×576 canvas. Use the existing checkout so Unity MCP stays connected. No new Unity packages or networked game code.

- [x] A. Revise road shoulders via MCP Aseprite, inspect/export, preserve accepted art in _old/stage2-approved. Source and PNG palette remain <=48 colors.
- [x] B. Export deduplicated 32×32 ground tiles, tile indices, six bottom-pivot buildings, per-building glow, props and 3 water frames via MCP; add rebuildable import path. Validate export reconstruction/import settings. Commit stage 3.
- [x] C. Integrate scene/colliders/NPCs/32×32 padded characters, shared map dimensions/spawn, camera/HUD/TMP signs. Add event-driven area visuals using CampaignLoader.UnlockForArea and Progress.completed. Preserve NPC unlockAfter, 1.65 interaction, gold objective markers; add map objective. Commit stage 4.
- [ ] D. Extend verification: all progression thresholds 0–11, no permanent locks/decor locks, paths to every NPC and door, map bounds/settings/rebuild. Run core and actual UI playthrough. Capture 1920×1080 Game View states and update metrics/report. Commit stage 5.
- [ ] E. Fresh independent final code review as required by executing-plans, resolve material issues, final evidence and status.

Interfaces: art manifest defines pixel-space bounds/pivots/footprints (top-left origin). WorldLayout is the sole runtime dimensions/spawn source. WorldArea reads existing campaign unlock logic and subscribes to ProgressChanged. Builder owns all scene creation. Verification checks the built scene and actual UI; no parallel unlock system.

Review focus: saved positions from old map; doors alongside locked NPCs; repeat load/new patrol vs one-shot unlock; screen aspect and HUD occlusion; rebuild idempotency and importer/sprite-reference stability.

## Ledger

- Start: stage 2 approved; requests for another art approval are unnecessary. Road change is small and reversible; visual/export checks suffice. Functional verification is added before runtime implementation and run red/green where feasible.
- Stage 3: MCP exports reconstruct the widened map with **zero differing pixels**, 308 unique tiles / 576 cells, 39 building/prop sprites, six glow overlays, three water frames, 47 colors. Art test first failed for missing world.json, then passed after exports/import. No runtime scene integration claimed at this checkpoint.
- Stage 4: Builder creates the new Tilemap, six bottom-pivot SortingGroups, 33 prop groups, water animation, four event-driven area visuals, TMP signs and static footprints. One shared WorldLayout controls spawn/dimensions/clamp. Core passed 169 checks; actual UI playthrough passed 277, including all NPC/door paths. Expanded screenshot/unlock assertions are stage 5.
- Ruling: preserve per-NPC campaign thresholds (Maya/Detective/Blue unlock later than their area's first mentor). An open building routes its door to the current mentor in that area, otherwise its first mentor; the existing NPC gate remains authoritative. No new unlock table in runtime.
- Ruling: LegacyRuntime.ttf cannot supply a TMP font face. Use the Essential Resources already shipped in the installed UGUI package, committed under Assets/TextMesh Pro; Packages manifest/lock remain unchanged. TMP resource import is asynchronous, so Builder now reads committed resources instead of importing during a rebuild.
- Ruling: URP PixelPerfectCamera resets partial Camera.rect. A fixed camera offset centers the whole map to the left of the mission console; verify projected bounds against actual HUD extents at 1920x1080. No scrolling.
- Verification note: initial synthetic WASD test failed once; repeat with position/input diagnostic passed the complete route. Expanded run retains the diagnostic and will check motion again.
