# Cartur's Build Camera

Equip a hammer near a workbench, press **Shift+B**, and the camera detaches from your character.
Look around and move as usual, build and pick pieces as usual, and put the camera away by
pressing Shift+B again, pressing R, or unequipping the tool. Works with the hoe and the cultivator,
and with any modded build tool — anything holding a piece table counts.

## Why this fork exists

[Azumatt's Build Camera Custom Hammers Edition](https://thunderstore.io/c/valheim/p/Azumatt/Build_Camera_Custom_Hammers_Edition/)
is deprecated, and the version still on Thunderstore — 1.3.1 — is broken on Valheim 1.0.

Valheim 1.0 dropped the trailing optional `bool` from `ZInput.GetJoyLeftStickX/Y` and
`GetJoyRightStickX/Y`. C# bakes the resolved argument count into the call site, so the shipped
1.3.1 assembly still references four signatures that no longer exist and throws
`MissingMethodException` out of the camera update every frame a controller is read. Resolving
1.3.1 against Valheim 1.0.16 turns up exactly those four dead references and nothing else.
Azumatt fixed it in a 1.3.2 that only ever reached Nexus, then deprecated the package.

This fork is that code, recompiled against 1.0.16 and maintained.

## Everything it does

- Detached build camera for any tool with a piece table — vanilla and modded hammers, hoe,
  cultivator.
- Its own configurable station range. It does **not** widen workbench coverage or player-base
  protection, so it will not suppress raids.
- The camera cannot cross terrain. HearthBelow cave floors, walls and ceilings use the same
  collision path.
- Optional cozy and minimum-comfort requirement, on camera entry or on camera pickup.
- Pick up items off the ground through the camera, with a configurable HUD warning when the
  cozy rule blocks it. Respects wards, and WardIsLove when installed.
- An equipped Dvergr circlet's light follows the camera, including CircletExtended and
  RaziCirclet custom slots. Position, intensity, range, colour and shadows are configurable.
- A Wisplight's demister ball follows the camera, at 2.5× clearing range by default.
- Separate mouse and controller look inversion.
- World-relative panning instead of camera-relative, if you prefer it.
- ServerSync: configs sync from the server, and the mod version-checks with itself.

## What this fork changed

- **Recompiled for Valheim 1.0.16**, which is the controller fix described above.
- **Exits build mode on death.** Dying used to leave you detached with input suppressed,
  watching your own corpse with no way to take control back.
- **Exits build mode on a portal hop**, which used to strand the camera clamped to a station in
  the world you just left.
- **Refuses to start while the game's own `freefly` camera is active**, instead of putting two
  camera systems on one transform.
- **Auto-pickup falls back to the vanilla ward check** when WardIsLove is installed but its
  `CheckAccess` cannot be resolved. It used to silently return "no access" and turn pickup off
  with nothing in the log.
- **Stopped writing `__runOriginal = true`** in the patch early-outs, which could resurrect a
  method another mod had deliberately skipped.
- **Version handshake has a floor.** It used to require the client and server strings to match
  exactly, so any patch release disconnected everyone who had not updated. It now accepts any
  version at or above a stated minimum, and ServerSync's own check reads the same minimum.
- **Default toggle is Shift+B**, not bare B. Plain B is a crowded key — Cartur's UI HUD quick
  slot 3, EquipmentAndQuickSlots slot 3 and OdinHorse saddlebags all use it — so a single B both
  toggled the camera and fired that quick slot, eating any food sitting in it.
- **Hotbar keys in build camera read the same input vanilla does.** Upstream read the raw number
  row past ZInput, so the keys fired even if you had rebound them, and the `Hotbar1Alt`–
  `Hotbar8Alt` binds were ignored.
- Fixed a null dereference when verbose logging was on and there was no local player.

## Config

`BepInEx/config/com.jekkle.valheim.carturbuildcamera.cfg`, hot-reloaded on save. If you are coming from
Azumatt's version and have no config yet, your existing `Azumatt.BuildCameraCHE.cfg` is copied
over on first run so your settings carry across.

Do not run both mods at once. If Azumatt's is loaded, this one logs a warning and patches
nothing rather than fighting it for the camera.

## Incompatible with

- ValheimPlus first person mode, and Masa's FirstPerson — both take over the camera.

## Compatible with

Azumatt's FirstPersonMode, HearthBelow, CircletExtended, RaziCirclet, WardIsLove.

## More from Cartur

[HD Blood](https://thunderstore.io/c/valheim/p/Cartur/Carturs_HD_Blood/) · [Map Pins](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Map_Pins/) · [Compass and Clock](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Compass_and_Clock/) · [Safe Stamina](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Safe_Stamina/) · [Follow Command](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Follow_Command/) · [Flooring](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Flooring/) · [UI - HUD](https://thunderstore.io/c/valheim/p/Cartur/Carturs_UI_HUD/) · [Waste Management](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Waste_Management/) · [Feeding Trough](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Feeding_Trough/)

Questions, bugs and requests: [Cartur's Mods Discord](https://discord.gg/nd5RqpwNkz)

## Credits and licence

GPL-3.0, the same licence as both upstreams. Source in `LICENSE.txt`.

- **gittywithexcitement** and CookieMilk — the original Valheim Build Camera.
- **MSchmoecker** — automatic tool detection.
- **Azumatt** — the Custom Hammers Edition fork this is built from: ServerSync, the circlet
  light, the demister, the coziness rules and the pickup HUD are all his work.
- **amberbeanzz** — the Wisplight idea.
