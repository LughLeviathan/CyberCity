# CyberCity world map — stages 0–2

**Goal:** Produce a reviewable layered Aseprite map faithful to the supplied reference; stop before Unity integration.

**Authority:** The user's detailed 2026-10-26 brief specifies the approach, tools and the approval gate after stage 2. No additional design gate is needed.

**Architecture:** Reference preprocessing and segmentation in Pillow/NumPy; all final pixel edits, palette snapping, source documents and exports through MCP Aseprite. Six separate building layers, water, ground, props, scenery_edge and glow. Hidden reference and collision guides never exported.

**Technical frame:** Unity 6000.6.5f1; 32 px tile / 32 PPU; Point; no compression/mipmaps; no new Unity dependencies; no game networking. Branch `feature/gameplay-revision`. Existing ProjectSettings.asset modification belongs to the user.

## Work

- [x] Inspect reference, Builder, importer, existing textures/scene, gameplay, UI, progression and reachability code.
- [x] MCP workflow guide; create, draw, export and verify a 32×32 smoke test.
- [ ] Record scale and asset inventory; checkpoint stage 0.
- [ ] Preserve the reference, extract <=40 colors, add eight exact brand colors, remove HUD/text/people with reference-derived preprocessing.
- [ ] Redraw the two right-side sites as fenced gardens, rebuild concealed edges with Aseprite drawing tools, quantize and export ref_clean.png; checkpoint stage 1.
- [ ] Import ref_clean.png as tracing reference; split semantic layers with binary masks, reconstruct hidden surfaces, refine major edges and props with MCP tools.
- [ ] View each major layer, inspect composite and enlarged details, verify palette/layer coverage and exports.
- [ ] Measure whole-image and right-area-excluded SSIM/MSE, record landmark offsets, export preview/comparison, checkpoint stage 2.
- [ ] Stop and request approval before stage 3.

## Review focus

1. No HUD, baked lettering, reference characters, unknown-area locks or coming-soon graphics.
2. No empty placeholder semantic layers; hidden underpainting prevents holes when buildings move/hide.
3. Single building art with separable neon; no duplicate locked-state art.
4. Every building/bridge/plaza landmark within one 32 px tile of its scaled reference; deliberate garden replacements masked separately in measurements.
5. Honest scope: collage/inpainting and finite palette cannot reproduce the illustration exactly; stage 2 does not claim collisions, animation, reachability or runtime unlock checks.

## Deferred integration contract (stages 3–5)

Canvas choice: 1024×576, 32×18 tiles. PixelPerfectCamera reference 1024×576, centered at (16,-9); proposed player clamp x .5–31.5, y -17.5–-.5. Apply together in Builder, WorldCamera, PlayerController, ProgressStore, spawn/reset/fallback and VerifyReachability after approval. HUD must fit around the complete view. Reference player feet (771,550) become (493.44,352), approximately Unity (15.42,-11).

Existing character frames are actually 24×32, visible alpha bbox 18×27. Pad to requested 32×32 without enlarging visible artwork at integration. Preserve sprite IDs/animation references.

Use CampaignLoader.UnlockForArea / UnlockForNpc and Progress.completed. Area thresholds 0/1/4/5; NPC thresholds retain their distinct mission indices. Completion event drives unlock animations/toasts. Roads remain open. Player House/Cyber Corp remain decoration. No second unlock system.

Old runtime PNGs/scenes remain usable during the art gate. They will be moved to the prepared `_old/` archive when the replacement importer is installed; old source documents can be archived now. Unity core/playthrough verification and Game View screenshots are deferred to stage 5.
