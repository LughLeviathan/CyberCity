# CyberCity Gameplay Revision Implementation Plan

**Goal:** Deliver the user's six ordered phases in Unity 6000.6.5f1, using English uGUI and fictional, in-memory exercises.

**Architecture:** Keep Campaign as the verified scenario pack, ProgressStore as the atomic save boundary, and GameUI as the shared palette/widget factory. Separate terminal, browser and SOC rules from their UI; ModuleUI routes exercises. No new packages.

**Spec:** The user's gameplay revision request in this chat (phases 0–5 and Definition of Done).

## Constraints and decisions

- Work on `feature/gameplay-revision` in the existing Unity-connected checkout. Preserve the pre-existing ProjectSettings.asset change; exclude it from commits.
- No network, OS commands, WebView, real JavaScript or SQL execution in game simulation.
- SHA-256 uses exact UTF-8 campaign text; update bundled hash whenever content changes. Preserve verified local packs and offline fallback.
- Preserve atomic `.tmp` replacement and `.bak` recovery. Reset legacy v1 progress with a visible explanation because the old ordinal route cannot safely map to the new practical requirements.
- Keep 150 XP maximum per mission, 15 XP per retry, 75 XP floor; hints deduct 5 XP each. Derive campaign maximum from mission count.
- Retain current uGUI coordinate/palette pattern. Add separate model files for reusable browser and SOC rules and separate UI files for their screens.

## Review focus

- Invalid/corrupt saves, changed pack mission IDs and legacy v1 must never index outside arrays or silently skip new exercises.
- Closing/reopening a lab must preserve earned flags and hint costs, with no duplicate XP.
- Patching without first demonstrating the attack, or retesting a different input, must not complete a browser exercise.
- Noise and malformed filters must not pass SOC investigation gates.
- All dynamically displayed prose must resolve `{player}` and keep rich text disabled, including fallback content.

## Ordered tasks and verification contracts

- [x] Phase 0 — Campaign.cs: dynamic count, unique IDs, optional non-quiz questions and new kind validation. ProgressStore.cs: campaign-aware v2 state and safe v1 reset. GameUI.cs: dynamic progress and score display. Builder: campaign-derived mentor unlocks; update current scene. Verification: reduced-pack acceptance, invalid packs, save bounds/backup, dynamic playthrough. Run Verify core systems; commit.
- [x] Phase 1 — PlayerIdentity.cs: trim/validate 3–16 letters/digits/spaces/underscore and Personalize. Start/pause/name label, NPC/toast/debrief rendering. Test valid/invalid names and unresolved tokens; run core + UI checks; commit.
- [ ] Phase 2 — VirtualTerminal.cs: three challenge filesystems, commands/pipes, safe errors and least privilege. Save flag/hint state, challenge board. Test every correct/wrong route and save resume; run core + terminal UI; commit.
- [ ] Phase 3 — WebScenario JSON models + WebLabSession.cs and FakeBrowser.cs. Editable address/history/reload, forms, network panel, data-driven patterns, source patch and identical-input retest. Test multiple payloads, false positives and patch ordering; run core + browser UI; commit.
- [ ] Phase 4 — SocScenario JSON + SocSession.cs and SocUI.cs. Alert queue, packet table/details, filters, sequential deterministic triage/IP/classification/response/report. Test noise/invalid filters/wrong reports and valid route; run core + SOC UI; commit.
- [ ] Phase 5 — Final chain reuses browser IDOR/SQLi then SOC analysis/response; graduation shows username, XP and area badges. Final campaign has 11 ordered missions and 6 quiz questions. Update verification for actual complete UI route; run both Verify menus, inspect screenshots/console, check GUID/meta integrity and prohibited APIs; commit.

## Execution ledger

- Baseline: all requested files read; Unity MCP root/version confirmed. Existing ProjectSettings.asset modification recorded. Original Verify core systems invoked before changes.

- Phase 0 complete: RED short pack rejected by old exact-count validation; GREEN VerifyCore 27 checks. Dynamic save bounds, legacy reset, changed-route fallback and backup recovery verified. Fixed existing InvalidDataException fallback gap in both save and optional-pack paths.

- Phase 1 complete: missing persisted identity reproduced before implementation. VerifyCore 43/43; complete runtime UI playthrough 113 checks passed. Name validation, trimming, save round-trip and unresolved-token checks included. SHA-256 regenerated.
