# Cartur's Build Camera 1.0.0

Another deprecated mod taken over and brought back.

Equip a hammer near a workbench, press Shift+B, and the camera detaches from your character so you can build without climbing on things. It works with the hoe, the cultivator, and any modded build tool.

## Why it needed taking over

Azumatt's Build Camera Custom Hammers Edition is deprecated, and the version still on Thunderstore is broken on Valheim 1.0.

The cause is a small one with a large blast radius. Valheim 1.0 removed a trailing optional argument from four ZInput joystick methods. C# bakes the resolved argument count into the call site, so the shipped assembly still asks for four method signatures that no longer exist, and throws every single frame a controller is read. Resolving that build against Valheim 1.0.16 turns up exactly those four dead references and nothing else.

So the fix is a recompile - but somebody has to actually do it, and keep doing it every time the game updates. That is the whole job of taking a mod over.

## What is in it

Everything the original did: configurable station range that does not widen workbench coverage or suppress raids, terrain collision so the camera cannot clip through the world, optional cozy and comfort requirements, item pickup through the camera with ward support, the Dvergr circlet light following the camera, the Wisplight demister, and server config sync.

Plus a handful of things that were broken and are not any more. Dying in build mode used to leave you detached, watching your own corpse, with no way to take control back. A portal hop stranded the camera at a station in the world you had just left. It fought the game's own freefly camera for the same transform. And the default key was a bare B, which collides with the quick-slot binds a lot of people use - it is Shift+B now.

GPL-3.0, like both mods it descends from, with the source public. Credit where it is owed: gittywithexcitement and CookieMilk wrote the original, MSchmoecker added the automatic tool detection, and Azumatt built the fork this one comes from.

Free on Thunderstore and Nexus, as always.
