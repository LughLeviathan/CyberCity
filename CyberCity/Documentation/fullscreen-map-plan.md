# Fullscreen map implementation plan

Spec: the user's fullscreen map and minimal HUD task (27 October 2026).
Execute inline using executing-plans, with one fresh review after verification.

## Constraints and decisions
- Branch `feature/gameplay-revision`; backup tag `pre-fullscreen-map` resolves to `cc5e714`.
- Starting tree is clean except the existing `ProjectSettings/ProjectSettings.asset` change. Preserve its bytes and exclude it from all commits.
- Keep map 1024×576 / 32×18, spawn, collision and movement limits unchanged.
- Use installed URP PixelPerfectCamera: reference 1024×576, UpscaleRenderTexture, StretchFill (aspect preserving), Point final filter. No packages.
- Keep J/M/H/ESC/E and all mission/modal behavior. Remove only HUD objective presentation.
- Use corner anchors for minimal HUD; public QuestButtonSlot contains the existing Journal button.
- URP clears crop bars black; cover only those bars with Ink UI rectangles. Modal scrims cover the whole display and modal panels remain centered.
- Preserve existing source art and screenshot evidence; new captures use fullscreen-*.

## Tasks
- [x] A. Add framing / HUD regressions and observe failure against the old scene.
- [x] B. Implement camera and overlay; rebuild through CyberCityBuilder; verify core and runtime contracts.
- [x] C. Run complete playthrough, shortcuts, label registration, modal containment and capture initial/interact/modal/open states at 1920×1080, 1366×768, 2560×1440, 1280×720, 1920×1200. Also test 2560×1080 and 1440×1080.
- [x] D. Inspect screenshots, review changes, document evidence and limitations; commit with `ui:` prefix, no push.

## Review focus
- Point filter uses the installed package's serialized field (no public setter).
- UI labels follow final pixel camera viewport through resize and crop bars.
- XP/name refresh and user quest slot survive initialization/reload.
- Modal content fits tall/wide displays and retains existing shortcuts.
- Input settings/save state restored by verification; user ProjectSettings change excluded.

## Ledger
- Initial audit: only user ProjectSettings diff. Backup tag verified before any code changes.
- Camera and HUD share screen framing; tests must use final world-to-screen projection and actual Canvas geometry.
- Tool fallback: Unity MCP is absent from enabled connector tools; use the already installed Unity relay over local stdio for Editor operations.
- RED: `VerifyScene` rejected old camera reference, before production changes.
- GREEN: rebuilt scene/art contracts, 169 core checks; first full runtime pass: 333 checks, all 11 missions and 7 aspect/resolution cases. User ProjectSettings SHA-256 unchanged.
- Screenshot inspection found CaptureScreenshot is deferred: opening Journal or closing Atlas in the same action changes the captured frame. Separate these actions and verify capture positions are walkable before final evidence run.
- Second run correctly rejected the Cafe capture fixture: (218,411) overlaps Alex's collision circle. A Physics2D query found (230,406) clear and still closest to Alex; use that stance.
- Final review found only the cosmetic Welcome gap at non-16:9. Fix it to satisfy full modal coverage: observed RED at 2560×1080 (only 1920 px covered), then stretch background and center its content. Add wide/tall Welcome regressions.
- Screenshot inspection also moved E prompt to the clear southern waterfront above the footer, avoiding overlap with the central globe. Mission actors and doors are all farther north.
- Editor resize timing: a later run reached the 2560×1440 assertion before the Game View render target adopted the selected size (the same matrix had passed previously). Queue now waits for actual Screen dimensions, with a 20-second failure timeout, then allows camera/canvas layout to settle. It does not relax framing assertions.
- Final GREEN: 350 runtime checks including 169 core, 27 October 2026 11:54:15 UTC. All 70 captures are from that final run; 28 resolution/state images passed file/dimension/bar/state validation. Scene/art contract and input restoration independently confirmed afterwards.
- Final review: Welcome coverage fixed and wide/tall regression passed; no deferred findings. On 2 November, artifact checks reran successfully and the ProjectSettings hash still matched the original user-owned version. No package or art changes, no push.
