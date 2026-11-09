# CyberCity world map — stages 0–2

**Goal:** Produce a reviewable layered Aseprite map faithful to the supplied reference; stop before Unity integration.

**Authority:** The user's detailed 2026-10-26 brief specifies the approach, tools and the approval gate after stage 2. No additional design gate is needed.

**Architecture:** Reference preprocessing and segmentation in Pillow/NumPy; all final pixel edits, palette snapping, source documents and exports through MCP Aseprite. Six separate building layers, water, ground, props, scenery_edge and glow. Hidden reference and collision guides never exported.

**Technical frame:** Unity 6000.6.5f1; 32 px tile / 32 PPU; Point; no compression/mipmaps; no new Unity dependencies; no game networking. Branch `feature/gameplay-revision`. Existing ProjectSettings.asset modification belongs to the user.

## Work

- [x] Inspect reference, Builder, importer, existing textures/scene, gameplay, UI, progression and reachability code.
- [x] MCP workflow guide; create, draw, export and verify a 32×32 smoke test.
- [x] Record scale and asset inventory; checkpoint stage 0.
- [x] Preserve the reference, extract <=40 colors, add eight exact brand colors, remove HUD/text/people with reference-derived preprocessing. Initial extraction re-encoded the input JPEG; disclosed in report.
- [x] Redraw the two right-side sites as fenced gardens, rebuild concealed edges with Aseprite drawing tools, quantize and export ref_clean.png; checkpoint stage 1.
- [x] Import ref_clean.png as tracing reference; split semantic layers with binary masks, reconstruct hidden surfaces, refine major edges and props with MCP tools.
- [x] View each major layer, inspect composite and enlarged details, verify palette/layer coverage and exports.
- [x] Measure whole-image and right-area-excluded SSIM/MSE, record landmark offsets, export preview/comparison, checkpoint stage 2.
- [x] Stop at the stage-2 review handoff. User subsequently approved stages 3–5 with slightly wider roads; see world-integration-plan.md.

## Review focus

1. No HUD, baked lettering, reference characters, unknown-area locks or coming-soon graphics.
2. No empty placeholder semantic layers; hidden underpainting prevents holes when buildings move/hide.
3. Single building art with separable neon; no duplicate locked-state art.
4. Every building/bridge/plaza landmark within one 32 px tile of its scaled reference; deliberate garden replacements masked separately in measurements.
5. Honest scope: collage/inpainting and finite palette cannot reproduce the illustration exactly; stage 2 does not claim collisions, animation, reachability or runtime unlock checks.

## Deferred integration contract (stages 3–5)

Canvas choice: 1024×576, 32×18 tiles. Initial camera proposal was 1024×576; implementation uses **1344×704** to reserve HUD space before choosing integer zoom, while the map remains 1024×576. Map center (16,-9), clamp x .5–31.5 and y -17.5–-.5. Builder, WorldCamera, PlayerController, ProgressStore, spawn/reset/fallback and VerifyReachability now share WorldLayout. Reference player feet (771,550) become (493.44,352), rounded spawn (493/32,-352/32).

Existing character frames are actually 24×32, visible alpha bbox 18×27. Pad to requested 32×32 without enlarging visible artwork at integration. Preserve sprite IDs/animation references.

Use CampaignLoader.UnlockForArea / UnlockForNpc and Progress.completed. Area thresholds 0/1/4/5; NPC thresholds retain their distinct mission indices. Completion event drives unlock animations/toasts. Roads remain open. Player House/Cyber Corp remain decoration. No second unlock system.

Old runtime assets were kept during the art gate, then moved with their metadata to `_old/legacy_runtime` after confirming no external GUID references remained. The stage-0 snapshot also remains in `_old/CyberCityWorld`. Unity verification and screenshots are tracked in the integration plan/report.
