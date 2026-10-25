# Stage 1 — cleaned reference

- Canvas 1024×576, uniform 0.64 scale; 47-color palette (39 extracted + eight exact brand colors).
- `ref_clean.aseprite` retains reference preparation, facade repainting, scenery and individual transplanted reference trees.
- `ref_clean.png` is the MCP Aseprite quantized/exported tracing base.
- Source HUD occlusions were filled with adjacent skyline/water textures; the hidden south retaining wall was reconstructed in Aseprite. The two unavailable sites were replaced with fenced gardens, reference-derived trees, benches and lamps. Surface signs are blank for later localized Unity text.
- Existing reference characters were removed in preprocessing. Door, window, stair and workstation patches were repaired using MCP drawing/copy tools.
- Aseprite mutators explicitly call `spr:saveAs(spr.filename)`; on-disk files were checked at each drawing pass. No `run_lua_script` call was used.
- Important provenance detail: the initial `extract_palette` invocation targeted the supplied JPEG before the workspace copy was made. This tool saves its input and re-encoded that JPEG; no earlier byte-identical snapshot is available. All subsequent edits use the workspace copy or new source files. The comparison baseline is the preserved decoded/re-encoded reference, whose SHA-256 is recorded in `preprocess-log.json`.
- Final review/metrics and semantic layer extraction are stage 2. No runtime integration is claimed.
