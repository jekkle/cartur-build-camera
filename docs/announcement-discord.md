**Cartur's Build Camera 1.0.0** is out.

Equip a hammer near a workbench, press **Shift+B**, and the camera detaches. Build without climbing on things. Works with the hoe, the cultivator, and any modded build tool.

**Why it exists:** Azumatt's *Build Camera Custom Hammers Edition* is deprecated, and the 1.3.1 still on Thunderstore is broken on Valheim 1.0. The game dropped a trailing optional argument from four `ZInput` joystick methods, and C# bakes the argument count into the call site - so that build references four signatures that no longer exist and throws every frame a controller is read. This is that mod, ported from 1.3.3 and recompiled against 1.0.16, and it will be kept working.

Everything upstream did is here: cozy restrictions, camera pickup, the Dvergr circlet light following the camera, the Wisplight demister, CircletExtended / RaziCirclet / WardIsLove support, ServerSync.

**Also fixed:**
- Dying in build mode used to leave you detached watching your own corpse with no way back
- So did a portal hop, with the camera stranded at a station in the world you left
- Won't fight the game's own `freefly` camera any more
- Default key is Shift+B, not bare B, which collided with common quick-slot binds

GPL-3.0, source at <https://github.com/jekkle/cartur-build-camera>

Thunderstore: <https://thunderstore.io/c/valheim/p/Cartur/Carturs_Build_Camera/>

*Note:* I don't own a controller, so the controller fix is verified statically - every reference resolves against 1.0.16 and the four dead calls are gone - but not play-tested with a pad. If you use one, I'd genuinely like to hear how it goes in #bug-reports.
