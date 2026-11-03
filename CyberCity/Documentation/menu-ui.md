# CyberCity menu UI

Implemented from the final 2026-11-03 specification and the five PNG references in `Assets/CyberCity/`. The written specification takes precedence over the reference slider positions.

## Runtime

`GameUI.cs` builds the main menu, Settings modal, username modal and welcome transition with legacy uGUI. The existing 1600x900 reference resolution, CanvasScaler Expand, color tokens and gameplay helpers are retained. Main menu content is centered on other aspect ratios, with an opaque background covering the complete screen.

- Start always opens the username modal, including for returning players.
- The saved valid name is prefilled; input is limited to 16 characters and uses `Game.SetPlayerName` / `PlayerIdentity.TryValidate`.
- Successful submission shows a 1.2-second welcome/progress animation, calls `Game.Begin`, then fades to gameplay over 0.25 seconds.
- W/S or arrow keys move menu focus; Enter selects. Tab navigates modal controls, left/right or A/D adjust a selected slider. ESC returns from Settings/username and cannot skip the welcome transition.
- Mouse controls use the same callbacks. Background menu controls cannot receive clicks through a modal.

## Settings and text

| Preference | Stored value | Effect |
|---|---|---|
| `CyberCityLanguage` | `id`, `en`, `jv`, `su` (default `id`) | Rebuilds menu text immediately using `MenuLocale` |
| `CyberCityEffects` | float 0–1 (default 0.7) | Existing effects AudioSource volume; slider displays 0–100 |
| `CyberCityVfx` | float 0–100 (default 85) | Existing world glow/unlock-fragment opacity and menu lighting |

The project currently has no particle-system, screen-shake or flash implementation to scale. World access and lock indicators remain readable at zero VFX. Settings persist immediately, including zero. Mission text and existing gameplay UI retain their original language and appearance.

`MenuLocale.cs` contains all front-end strings. Indonesian copy preserves the English headings/button labels explicitly supplied in the specification. Jawa and Sunda translations need native-speaker review before release.

## Assets and components

The five font files for the three requested families live under `Assets/CyberCity/Fonts/Resources/CyberCityFonts/`, so they are available in player builds. Pixelify Sans is instantiated at weight 700 from the official variable font; Silkscreen Bold and Chakra Petch Regular/Medium/SemiBold are static TTFs. Source URLs and OFL licenses are in `Assets/CyberCity/Fonts/`.

`MenuSurface`, `MenuGradient`, `MenuTracking` and `MenuButton` provide rounded geometry, gradients, letter spacing and mouse/keyboard focus feedback without generated texture dependencies. The skyline has a fixed seed.

## Verification

- `Tools > CyberCity > Verify menu interactions and capture`: tests keyboard and pointer input, validation, returning players, localization, audio/VFX effects, preference reload, slider endpoints, transition timing and wide/tall layout. Restores saves and preferences. Results: `Documentation/menu-verification.txt`.
- `Tools > CyberCity > Verify complete playthrough`: existing core and complete campaign regression suite, updated for the new Start/username/transition flow. Results: `Documentation/verification.txt`.
- Captured menu screens: `Documentation/Screenshots/Menu/`.

### Input System lifecycle regression

`CyberCityVerification` temporarily changes the existing InputSettings object's focus policies and restores their original values. It never replaces or destroys that object. Unity's InputManager owns the default `HideAndDontSave` settings and destroys them when another settings instance is assigned; the former clone/swap/destroy test setup therefore left a dead native reference on subsequent Play entries with Domain Reload disabled.

Cleanup now also runs on Stop, before assembly reload and on Editor quit. `Tools > CyberCity > Verify input settings lifecycle` checks object survival, value restoration and repeated cleanup. `Verify repeated Play Stop input` exercises three Play/Stop cycles, including an interrupted verification run, while monitoring Input System errors/assertions. Results: `Documentation/input-lifecycle-verification.txt`.
