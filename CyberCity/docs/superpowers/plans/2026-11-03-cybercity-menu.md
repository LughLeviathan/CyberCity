# CyberCity menu implementation plan

**Goal:** Implement the user's final 2026-11-03 UI specification and local PNG references.
**Architecture:** Keep code-built legacy uGUI in GameUI.cs; add focused localization and drawing helpers. Bundle OFL fonts in Fonts/Resources for player builds. Keep existing gameplay UI and mission text intact.
**Tech stack:** Unity 6000.6, uGUI, Input System, C#.
**Spec:** User's final CyberCity UI Spec in this chat; Assets/CyberCity/01 Main Menu.png through 04 Gameplay Transition.png and Components.png. Text takes precedence over inconsistent screenshot values.

## Constraints and decisions
- Keep 1600x900, CanvasScaler Expand, existing ten color tokens and gameplay helper APIs.
- Main menu Start always opens the username modal; validated name precedes a 1.2 second transition and Game.Begin.
- Bundle Silkscreen Bold, Pixelify Sans Bold, Chakra Petch Regular/Medium/SemiBold, retaining OFL notices.
- Persist language, effects volume and VFX intensity immediately. Reuse CyberCityEffects (0..1), add CyberCityVfx (0..100), CyberCityLanguage (id/en/jv/su).
- Localize only the four new screens; preserve authored English branding and names across language columns. Jawa/Sunda require native speaker review before release.
- VFX controls existing world glow/unlock fragments and menu lighting. No unrelated new shake/flash feature.
- Work in the active Unity checkout so editor validation sees the changes. Preserve user PNG/ZIP inputs and saved progress; do not commit or publish.

## Tasks
- [x] Add a runtime menu-flow verification and demonstrate failure against the old UI.
- [x] Bundle fonts and licenses; build rounded/stroked primitives, letter spacing and button focus feedback.
- [x] Implement main menu, settings, username and animated transition in GameUI.cs; wire keyboard routing and existing save/name validation.
- [x] Add the four-language key table, persistent SFX/VFX settings, and world VFX integration.
- [x] Update obsolete playthrough menu expectations. Run core, menu flow and complete gameplay verification; inspect screens at 1600x900, wide and tall aspect ratios.

## Review focus
- Enter must not submit twice or start gameplay before valid input/transition completion.
- ESC cancels submenus, does not expose gameplay before Begin, and cannot skip the transition.
- Language refresh must preserve values and focus; longer translations/name lengths must fit.
- Slider endpoints and existing saved 0 values must survive reopen/startup.
- Tests restore user's saves, preferences and window resolution; imported font references must work in player builds.

## Evidence / rulings
- Figma unavailable in preceding turn; user supplied authoritative spec and all five PNGs.
- Existing effects are WorldArea.glow and unlock fragments; no particle system or screen-shake implementation exists.

- Verification: 61 menu interaction checks passed, including actual pointer/keyboard events and 1600x900 / 2560x1080 / 1440x1080 captures.
- Verification: 350 complete-playthrough checks passed (including 169 core checks and all 11 missions through runtime UI).
- Verification: Unity GUID/meta checker examined 982 files; 0 errors, 0 warnings. git diff --check passed.
- Independent runtime-code review found the slider thumb vertical-stretch issue; corrected and verified as 36x36 outer / 28x28 fill. No unresolved code findings.
- Rendering fixes: custom Graphics explicitly require CanvasRenderer; title/input font leading is allowed to exceed the line box without clipping glyphs.
- Test harness waits for rendered frames after replacing a modal before synthetic pointer hits; immediate same-frame depth=-1 is expected in uGUI.
- No commits or publication performed. Original saved progress and preferences restored after all tests.
