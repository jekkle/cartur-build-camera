# Changelog

## 1.0.0

First release of the fork. Feature-identical to Azumatt's Build Camera Custom Hammers Edition
1.3.3, plus:

* Recompiled against Valheim 1.0.16. Fixes the controller crash — 1.3.1 on Thunderstore still
  references `ZInput.GetJoyLeftStickX/Y(bool)` and `GetJoyRightStickX/Y(bool)`, which Valheim
  1.0 removed the trailing optional parameter from.
* Build mode now ends on death. It used to leave you detached with input suppressed.
* Build mode now ends on a portal hop, instead of stranding the camera at a station in the
  previous world.
* Build mode refuses to start while the game's own `freefly` camera is active.
* Camera auto-pickup falls back to the vanilla ward check when WardIsLove is installed but its
  `CheckAccess` cannot be resolved. It used to silently disable pickup with nothing logged.
* Patch early-outs no longer write `__runOriginal = true`, which could resurrect a method
  another mod had deliberately skipped.
* Version handshake accepts any client at or above a minimum-version floor instead of demanding
  an exact string match, so a patch release no longer disconnects everyone who has not updated
  yet. Both the hand-rolled handshake and ServerSync's own check now read the same floor —
  raising only ServerSync's would have changed nothing, because the handshake overruled it.
* Default toggle is **Shift+B**, not the bare B upstream used. Plain B is crowded — Cartur's UI
  HUD quick slot 3, EquipmentAndQuickSlots slot 3 and OdinHorse saddlebags all bind it, so one
  press toggled the camera *and* used that slot, eating whatever food was in it. BepInEx's
  `KeyboardShortcut` blocks a bare-B shortcut while Shift is held, so the two no longer collide.
  Upgrading from Azumatt's config keeps whatever key you already had; change it by hand if you
  want Shift+B.
* Hotbar keys in build camera mode now read the same input as vanilla. Upstream read the raw
  number row via `Input.GetKeyDown(KeyCode.Alpha1 + i)`, bypassing ZInput, so slots fired even
  for players who had rebound those keys — and it ignored the `Hotbar1Alt`–`Hotbar8Alt` binds
  entirely, which vanilla honours.
* Fixed a null dereference in verbose logging when there was no local player.
* Stands down and patches nothing if `Azumatt.BuildCameraCHE` is also loaded.
* Seeds its config from `Azumatt.BuildCameraCHE.cfg` on first run if you have one.
