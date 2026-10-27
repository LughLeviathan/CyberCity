# World map integration review

Fresh-context read-only review of the branch and pending verification changes found no P0/P1 issues. Three P2 findings were addressed:

1. **Door selection:** a nearby NPC previously intercepted E even when the door was closer. Runtime and HUD now share the nearest-target decision. The Cafe/Maya fixture presses E through the real input path.
2. **Camera zoom:** full-frame integer zoom could clip the map at 2048×1152 or overlap the console at larger sizes. Camera reference is now 1344×704, reserving space for the 1024×576 map and HUD. Runtime framing is checked at 1600×900, 1920×1080, 2048×1152, 2560×1440 and 3840×2160.
3. **Editor reload:** nonserialized mission event subscriptions were lost after script reload. RestoreInterface now reinitializes area visuals/subscriptions without replaying unlock. Verification clears the event delegates, invokes that restoration path, then checks later actual mission unlock events.

The minor lifetime-animation-counter issue was fixed by resetting the diagnostic on NewPatrol. No campaign unlock logic was duplicated or replaced.

Reviewer inspected source and installed PixelPerfectCamera implementation; the reviewer did not run Unity. Runtime evidence is produced by CyberCityVerification and recorded in verification.txt. Menu invocation alone is not treated as a passing test.
