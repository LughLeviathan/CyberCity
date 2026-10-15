# CyberCity

CyberCity is a single-player, offline 2D pixel-art cybersecurity learning game for Windows, built in Unity 6000.6.5f1. It follows the gameplay and learning sequence in Section 3 of the supplied proposal, using the existing CyberCityWorld city art.

## Play

Open `Assets/CyberCity/Scenes/CyberCity.unity` and press Play, or launch `Builds/CyberCity/CyberCity.exe` after building the Windows player. Keep the entire player folder together when distributing it.

| Control | Action |
| --- | --- |
| WASD or arrow keys | Move |
| E | Talk to a nearby mentor |
| Mouse | Choose dialogue responses, inspect evidence, use modules |
| J | Mission journal |
| M | City map and district access |
| H | Cybersecurity field guide |
| Escape | Close a module or open the pause menu |

Follow the gold exclamation mark. Stand near the named mentor and press E. Missions can be closed and retried. Opening a module pauses player movement. Music and effects can be toggled in the pause menu.

Use **Save and quit** in the pause menu to exit the Windows player. In the Unity editor this button exits Play mode. If scripts reload during a patrol, the interface rebuilds and returns to the welcome screen; continue to resume the saved mission state.

## Campaign

| Mission | Mentor | Learning activity |
| --- | --- | --- |
| 1. Welcome to CyberCity | Professor Byte | Orientation and independent verification |
| 2. Small habits, strong defenses | Alex | Passwords, MFA, updates |
| 3. The urgent inbox | Maya | Phishing evidence and reporting |
| 4. A convincing imitation | Detective Byte | Fake browser, domain and form investigation |
| 5. A flag in the filesystem | SysAdmin Tux | Linux terminal and hidden-file CTF |
| 6. Who owns this record? | Red | Simulated IDOR, then authorization defense |
| 7. When text becomes markup | Red | Inert XSS model, then output encoding |
| 8. Keep data out of the query | Red | Simulated SQL injection, then bound parameters |
| 9. Connect the evidence | Red | Identify root causes from evidence |
| 10. Repair the trust boundaries | Blue | Match server, rendering, and query defenses |
| 11. The citywide incident | Detective Byte | Combined final incident investigation |
| 12. A citizen worth trusting | Professor Byte | Graduation and patrol report |

Cyber Academy is open at the start. The Internet Cafe unlocks after mission 1, Linux Lab after mission 4, and Web Security Lab after mission 5. Blue's assignment unlocks after mission 9. The shared outdoor map remains explorable; mentor interactions control access to learning content.

Each completed mission awards 150 XP, reduced by 15 for each incorrect answer to a minimum of 75. Maximum campaign score is 1800 XP. Correct answers include explanations; incorrect answers permit another attempt. Evidence investigations require highlighting all suspicious indicators before submitting a verdict. The terminal requires reading the flag file before accepting the flag.

## Saving and content

Progress saves after each mission, each wrong answer, every 15 seconds during a patrol, on pause, and on exit. Saves include mission progress, XP, retry counts, and player position. Writes use a temporary file plus a backup. A damaged primary file falls back to the backup; damaged primary and backup files start a fresh patrol.

Windows save location: `%USERPROFILE%/AppData/LocalLow/Sijaw/CyberCity/cybercity-progress.json`.

The bundled JSON scenario pack is `Assets/CyberCity/Resources/campaign.json`. Its SHA-256 is stored in `campaign_hash.txt`. To edit the campaign, update `Tools/build_campaign.py` and run it with Python 3. The builder regenerates both files with consistent UTF-8 encoding.

An optional replacement pack can be installed under the save folder at `ScenarioPacks/campaign.json`, with a matching `ScenarioPacks/campaign.json.sha256` file. The game validates its schema and checksum before use and falls back to the bundled pack on failure. Checksums detect corruption; they do not authenticate a pack's publisher. There is no network dependency, remote catalog, or automatic updater in this version. Addressables hosting is a future distribution option; the local JSON pack and fallback flow are implemented.

## Project structure

- `Assets/CyberCity/Scripts`: player/input, camera framing, NPCs, campaign, save system, interface, and simulations.
- `Assets/CyberCity/Resources`: bundled scenario pack and checksum.
- `Assets/CyberCity/Generated`: eight placeholder character sheets, six player animation clips, Animator controller, Sprite Atlas, unlit sprite material, and generated audio.
- `Assets/CyberCity/Editor`: repeatable scene builder and verification runner.
- `Assets/CyberCityWorld`: original city art and existing prefab. Its importer was corrected to use the actual nested asset path.
- `Documentation/Screenshots`: captured runtime screens.
- `Documentation/verification.txt`: latest recorded verification results.

The player uses the Unity Input System, Rigidbody2D, a capsule collider, a six-state Animator, and y-based sprite sorting. The city keeps the existing Tilemap and water/prop/building collisions. All seven mentors have placeholder idle animations. Texture imports use point filtering without mipmaps or lossy compression, and the camera uses Unity's Pixel Perfect Camera. The character atlas packs the placeholders. The original synthesized ambience is imported as Vorbis, and short interface cues use ADPCM.

To replace a character, provide a sprite sheet with the same named sprite frames or replace the generated assets and Animator references. Running the scene builder regenerates placeholder art and audio, so keep custom replacements in a separate folder and assign them afterward.

## Rebuild and verify

Use Unity's **Tools > CyberCity** menu:

1. **Build playable game** regenerates placeholders, audio, and the playable scene. It preserves the original art and sets CyberCity as the enabled build scene.
2. **Verify core systems** checks campaign integrity/schema, virtual terminal behavior, atomic saves, and damaged-save recovery.
3. Enter Play mode, then use **Verify complete playthrough**. It drives the actual runtime module buttons and inputs through all twelve missions, tests movement/collision, confirms mentor reachability, checks progressive access and saving, and records screenshots. It restores the pre-test save files when Play mode ends.
4. **Build Windows player** creates `Builds/CyberCity/CyberCity.exe` and its companion files.

The browser, profiles, email messages, file system, flag, query output, and data are fictional. The terminal is an in-memory command interpreter. The web modules model vulnerable behavior without evaluating HTML, executing scripts, opening network connections, or connecting to databases.

This is a playable educational MVP. The main character and mentors are deliberately replaceable placeholders. Red and Blue exercises use bounded simulations and guided decisions. Remote content hosting, multiplayer, real vulnerable services, and interior room maps are outside this version.
